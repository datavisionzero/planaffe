using System.Net;
using System.Net.Sockets;

namespace Planaffe.Api.Http;

/// <summary>
/// Small bounded rolling-window limiter for the doors a stranger can knock on:
/// password sign-ins, device logins, password recovery, and the current
/// password a password change asks for.
/// </summary>
/// <remarks>
/// <para>
/// An attempt is reserved before the password is checked and given back when
/// it was right. Counting only after the check let every request of a burst
/// through the gate while the others were still hashing, so that five
/// attempts per window became as many as arrived at once.
/// </para>
/// <para>
/// A source is an address, and an IPv6 address is its /64: whoever holds one
/// usually holds all of it, and a limit per full address is one they step
/// around by changing the last half.
/// </para>
/// </remarks>
public sealed class LoginThrottle(TimeProvider clock)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RecoveryWindow = TimeSpan.FromMinutes(5);
    private const int AccountLimit = 5, AddressLimit = 20, RecoveryAccountLimit = 1, MaximumKeys = 4096;
    private readonly Dictionary<string, List<DateTimeOffset>> attempts = new(StringComparer.Ordinal);
    private readonly object gate = new();

    /// <summary>One attempt, counted before its check, to be given back if it succeeds.</summary>
    public readonly record struct Reservation(string Account, string? Address, DateTimeOffset At);

    /// <summary>What a request for a recovery email is let through to.</summary>
    public enum Recovery
    {
        /// <summary>Send the email.</summary>
        Send,

        /// <summary>
        /// The account asked a moment ago: answer as always and send nothing,
        /// so that nobody can flood a mailbox or keep replacing its live link.
        /// </summary>
        Quiet,

        /// <summary>The source asked too often: <c>login-throttled</c>.</summary>
        Throttled,
    }

    /// <summary>The source a limit counts: an IPv6 address by its /64, an IPv4-mapped one as the IPv4 it is.</summary>
    public static string SourceOf(IPAddress? address)
    {
        if (address is null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }

    /// <summary>
    /// Reserves a sign-in attempt for the account and the source, or says how
    /// long until one is free again.
    /// </summary>
    public bool TryReserve(string normalizedEmail, string source, out Reservation reservation, out TimeSpan retryAfter) =>
        TryReserve("account:" + normalizedEmail, AccountLimit, "address:" + source, out reservation, out retryAfter);

    /// <summary>
    /// A password change's check of the current password, counted per user: a
    /// session or token in the wrong hands must not be a way to guess the
    /// password without limit (ADR 0025).
    /// </summary>
    public bool TryReservePasswordChange(Guid userId, out Reservation reservation, out TimeSpan retryAfter) =>
        TryReserve("password:" + userId.ToString("n"), AccountLimit, address: null, out reservation, out retryAfter);

    /// <summary>
    /// A right password: the account's failures are forgotten, and the
    /// attempt is given back to the source, which counts failures only.
    /// </summary>
    public void Succeeded(Reservation reservation)
    {
        lock (gate)
        {
            attempts.Remove(reservation.Account);
            Remove(reservation.Address, reservation.At);
        }
    }

    /// <summary>An attempt that never reached the check — the request was refused before it — is given back whole.</summary>
    public void GiveBack(Reservation reservation)
    {
        lock (gate)
        {
            Remove(reservation.Account, reservation.At);
            Remove(reservation.Address, reservation.At);
        }
    }

    /// <summary>
    /// A device login begun from a source, in its own counter. Separate from
    /// the sign-in counters on purpose: a machine that begins logins must not
    /// be able to lock a person out of the password screen.
    /// </summary>
    public bool TryBeginDevice(string source, out TimeSpan retryAfter)
    {
        lock (gate)
        {
            var key = "device:" + source;
            retryAfter = TimeSpan.Zero;
            if (Count(key, Window) >= AddressLimit)
            {
                retryAfter = Wait(key, Window);
                return false;
            }

            Add(key, clock.GetUtcNow(), Window);
            return true;
        }
    }

    /// <summary>
    /// A request for a recovery email, counted per source and per account
    /// whether the account exists or not, so that the verdict tells nobody
    /// which ones do.
    /// </summary>
    public Recovery TryRecover(string normalizedEmail, string source, out TimeSpan retryAfter)
    {
        lock (gate)
        {
            var now = clock.GetUtcNow();
            var address = "recovery-address:" + source;
            var account = "recovery-account:" + normalizedEmail;
            retryAfter = TimeSpan.Zero;
            if (Count(address, Window) >= AddressLimit)
            {
                retryAfter = Wait(address, Window);
                return Recovery.Throttled;
            }

            Add(address, now, Window);
            if (Count(account, RecoveryWindow) >= RecoveryAccountLimit)
            {
                return Recovery.Quiet;
            }

            Add(account, now, RecoveryWindow);
            return Recovery.Send;
        }
    }

    private bool TryReserve(string account, int accountLimit, string? address,
        out Reservation reservation, out TimeSpan retryAfter)
    {
        lock (gate)
        {
            reservation = default;
            retryAfter = TimeSpan.Zero;
            if (Count(account, Window) >= accountLimit)
            {
                retryAfter = Wait(account, Window);
                return false;
            }

            if (address is not null && Count(address, Window) >= AddressLimit)
            {
                retryAfter = Wait(address, Window);
                return false;
            }

            // Unique per key, so that giving a reservation back removes that
            // one and not another made in the same tick.
            var at = clock.GetUtcNow();
            while (Holds(account, at) || (address is not null && Holds(address, at)))
            {
                at = at.AddTicks(1);
            }

            Add(account, at, Window);
            if (address is not null)
            {
                Add(address, at, Window);
            }

            reservation = new Reservation(account, address, at);
            return true;
        }
    }

    private bool Holds(string key, DateTimeOffset at) => attempts.TryGetValue(key, out var list) && list.Contains(at);

    private void Remove(string? key, DateTimeOffset at)
    {
        if (key is not null && attempts.TryGetValue(key, out var list))
        {
            list.Remove(at);
        }
    }

    private int Count(string key, TimeSpan window)
    {
        if (!attempts.TryGetValue(key, out var list))
        {
            return 0;
        }

        Prune(list, window);
        return list.Count;
    }

    // Until the oldest attempt leaves the window, and at least a second.
    private TimeSpan Wait(string key, TimeSpan window)
    {
        var wait = attempts[key].Min() + window - clock.GetUtcNow();
        return wait < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : wait;
    }

    private void Add(string key, DateTimeOffset at, TimeSpan window)
    {
        if (!attempts.TryGetValue(key, out var list))
        {
            attempts[key] = list = [];
        }

        Prune(list, window);
        list.Add(at);
        TrimStore();
    }

    private void Prune(List<DateTimeOffset> list, TimeSpan window)
    {
        var floor = clock.GetUtcNow() - window;
        list.RemoveAll(time => time <= floor);
    }

    // Judged by the longer window, so that no key goes while it still counts.
    private void TrimStore()
    {
        if (attempts.Count <= MaximumKeys)
        {
            return;
        }

        var empty = attempts
            .Where(pair => { Prune(pair.Value, Window); return pair.Value.Count == 0; })
            .Select(pair => pair.Key)
            .Take(attempts.Count - MaximumKeys)
            .ToList();
        foreach (var key in empty)
        {
            attempts.Remove(key);
        }
    }
}
