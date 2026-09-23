using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Planaffe.Domain;

namespace Planaffe.Application.Acts;

/// <summary>
/// What every list and every bulk act checks the same way: the size of a page
/// and the keys of a bulk request (<c>docs/api.md</c>, Pagination).
/// </summary>
internal static class Paging
{
    /// <summary>The page size asked for, or the default.</summary>
    /// <exception cref="Refusal"><c>validation</c> outside 1 to <see cref="ListIssues.MaximumLimit"/>.</exception>
    public static int Limit(int? requested, int defaultLimit) =>
        (requested ?? defaultLimit) is var limit and >= 1 and <= ListIssues.MaximumLimit
            ? limit
            : throw Refusal.Validation("limit", $"limit is 1 to {ListIssues.MaximumLimit}; larger pages are refused, not truncated (ADR 0012).");

    /// <exception cref="Refusal"><c>validation</c> on none or a repeated one; <c>too-many</c> over the bulk maximum.</exception>
    public static IReadOnlyList<string> BulkKeys(IReadOnlyList<string>? keys)
    {
        if (keys is null || keys.Count == 0)
        {
            throw Refusal.Validation("keys", "At least one issue key.");
        }
        if (keys.Count > CreateIssues.MaximumPerRequest)
        {
            throw new Refusal(RefusalCode.TooMany, $"At most {CreateIssues.MaximumPerRequest} issue keys in one request.");
        }
        if (keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != keys.Count)
        {
            throw Refusal.Validation("keys", "An issue key may occur only once.");
        }

        return keys;
    }
}

/// <summary>
/// The cursor of a page (<c>docs/api.md</c>, Pagination): opaque to the client,
/// the position of the last item plus a fingerprint of what the list was asked
/// for, so that a cursor handed to a different request is refused rather than
/// paging through the wrong list. Base64url over JSON; every list's payload is
/// its own, the codec is this one.
/// </summary>
internal static class Cursor<TPayload>
    where TPayload : class
{
    public static string Encode(TPayload payload) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <returns>The payload, or <c>null</c> for anything this server did not write.</returns>
    public static TPayload? Decode(string cursor)
    {
        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            return JsonSerializer.Deserialize<TPayload>(Convert.FromBase64String(base64));
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Sixteen hex characters of the SHA-256 of what the list was asked for, as JSON.</summary>
    public static string Fingerprint<T>(T asked) => Fingerprint(JsonSerializer.Serialize(asked));

    public static string Fingerprint(string asked) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(asked)))[..16];
}
