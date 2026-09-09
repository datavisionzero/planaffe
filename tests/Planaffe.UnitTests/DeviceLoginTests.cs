using Planaffe.Domain.Identities;

namespace Planaffe.UnitTests;

public sealed class UserCodeTests
{
    [Fact]
    public void A_code_is_eight_consonants_and_can_never_read_as_a_word()
    {
        for (var i = 0; i < 200; i++)
        {
            var code = UserCode.Issue();

            Assert.Equal(UserCode.Length, code.Length);
            Assert.All(code, character => Assert.Contains(character, UserCode.Alphabet));
            // No vowel, so no word; no digit, so neither half of `0`/`O`,
            // `1`/`I`, `5`/`S` or `2`/`Z` can be typed for the other.
            Assert.DoesNotContain(code, character => "AEIOU".Contains(character) || char.IsDigit(character));
        }
    }

    [Theory]
    [InlineData("bcdf-ghjk")]
    [InlineData("BCDF-GHJK")]
    [InlineData(" bcdfghjk ")]
    [InlineData("BCDF GHJK")]
    public void Case_spaces_and_dashes_are_how_it_was_shown_and_not_what_it_is(string typed) =>
        Assert.Equal("BCDFGHJK", UserCode.Normalize(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("BCDFGHJ")]
    [InlineData("BCDFGHJKL")]
    [InlineData("BCDFGHJ0")]
    [InlineData("AEIOUAEI")]
    public void What_is_not_a_code_normalizes_to_nothing_rather_than_throwing(string? typed) =>
        Assert.Equal(string.Empty, UserCode.Normalize(typed));

    [Fact]
    public void A_code_is_shown_in_two_groups() => Assert.Equal("BCDF-GHJK", UserCode.ForReading("BCDFGHJK"));
}

public sealed class DeviceCodeTests
{
    [Fact]
    public void A_device_code_carries_no_token_prefix_and_is_two_hundred_and_fifty_six_bits()
    {
        var code = DeviceCode.Issue();

        Assert.DoesNotContain(TokenSecret.Prefix, code, StringComparison.Ordinal);
        Assert.Equal(32, DeviceCode.Hash(code).Length);
        Assert.NotEqual(code, DeviceCode.Issue());
    }
}

public sealed class DeviceLoginTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void A_login_begins_pending_and_the_code_is_returned_rather_than_stored()
    {
        var (login, deviceCode, userCode) = DeviceLogin.Begin(Now);

        Assert.Equal(DeviceLoginState.Pending, login.StateAt(Now));
        Assert.Equal(Now + DeviceLogin.Lifetime, login.ExpiresAt);
        Assert.Equal(userCode, login.UserCode);
        Assert.Equal(DeviceCode.Hash(deviceCode), login.DeviceCodeHash);
    }

    [Fact]
    public void Nobody_confirming_in_time_is_expired_rather_than_pending()
    {
        var (login, _, _) = DeviceLogin.Begin(Now);

        Assert.Equal(DeviceLoginState.Expired, login.StateAt(Now + DeviceLogin.Lifetime));
        Assert.False(login.ApproveBy(Guid.NewGuid(), Now + DeviceLogin.Lifetime));
    }

    [Fact]
    public void A_confirmed_login_names_who_confirmed_it_and_cannot_be_confirmed_twice()
    {
        var (login, _, _) = DeviceLogin.Begin(Now);
        var user = Guid.NewGuid();

        Assert.True(login.ApproveBy(user, Now));
        Assert.Equal(DeviceLoginState.Approved, login.StateAt(Now));
        Assert.Equal(user, login.ApprovedByUserId);
        Assert.False(login.ApproveBy(Guid.NewGuid(), Now));
        Assert.False(login.Deny(Now));
        Assert.Equal(user, login.ApprovedByUserId);
    }

    [Fact]
    public void A_refused_login_stays_refused()
    {
        var (login, _, _) = DeviceLogin.Begin(Now);

        Assert.True(login.Deny(Now));
        Assert.Equal(DeviceLoginState.Denied, login.StateAt(Now));
        Assert.False(login.ApproveBy(Guid.NewGuid(), Now));
        Assert.Equal(DeviceLoginState.Denied, login.StateAt(Now + DeviceLogin.Lifetime));
    }

    [Fact]
    public void An_approval_nobody_collected_in_time_does_not_redeem()
    {
        var (login, _, _) = DeviceLogin.Begin(Now);
        login.ApproveBy(Guid.NewGuid(), Now);

        Assert.False(login.RedeemTo(Guid.NewGuid(), Now + DeviceLogin.Lifetime));
        Assert.Null(login.IssuedTokenId);
    }

    [Fact]
    public void A_device_code_redeems_exactly_once_and_stays_redeemed_after_it_expires()
    {
        var (login, _, _) = DeviceLogin.Begin(Now);
        login.ApproveBy(Guid.NewGuid(), Now);
        var token = Guid.NewGuid();

        Assert.True(login.RedeemTo(token, Now));
        Assert.Equal(token, login.IssuedTokenId);
        Assert.False(login.RedeemTo(Guid.NewGuid(), Now));
        Assert.Equal(token, login.IssuedTokenId);
        Assert.Equal(DeviceLoginState.Redeemed, login.StateAt(Now + DeviceLogin.Lifetime));
    }

    [Fact]
    public void A_login_nobody_confirmed_cannot_be_redeemed()
    {
        var (login, _, _) = DeviceLogin.Begin(Now);

        Assert.False(login.RedeemTo(Guid.NewGuid(), Now));
        Assert.Equal(DeviceLoginState.Pending, login.StateAt(Now));
    }
}
