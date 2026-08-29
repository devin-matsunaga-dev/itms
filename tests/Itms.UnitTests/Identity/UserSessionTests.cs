using Itms.Modules.Identity.Domain;

namespace Itms.UnitTests.Identity;

/// <summary>
/// The session row is what makes revocation real, so its rules are worth asserting
/// away from the database that stores it.
/// </summary>
public sealed class UserSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_opened_session_expires_a_lifetime_from_now()
    {
        var session = UserSession.Open(Guid.CreateVersion7(), Now, TimeSpan.FromHours(24), "10.0.0.1", "agent");

        session.IssuedAt.ShouldBe(Now);
        session.ExpiresAt.ShouldBe(Now.AddHours(24));
        session.RevokedAt.ShouldBeNull();
        session.IsActive(Now).ShouldBeTrue();
    }

    [Fact]
    public void A_session_is_dead_at_its_expiry_instant_not_after_it()
    {
        var session = UserSession.Open(Guid.CreateVersion7(), Now, TimeSpan.FromHours(1), null, null);

        session.IsActive(Now.AddHours(1).AddTicks(-1)).ShouldBeTrue();
        session.IsActive(Now.AddHours(1)).ShouldBeFalse();
    }

    [Fact]
    public void Revoking_ends_the_session_immediately()
    {
        var session = UserSession.Open(Guid.CreateVersion7(), Now, TimeSpan.FromHours(24), null, null);

        session.Revoke(Now.AddMinutes(5), "logout");

        session.IsActive(Now.AddMinutes(6)).ShouldBeFalse();
        session.RevokedReason.ShouldBe("logout");
    }

    [Fact]
    public void Revoking_twice_keeps_the_first_reason()
    {
        var session = UserSession.Open(Guid.CreateVersion7(), Now, TimeSpan.FromHours(24), null, null);

        session.Revoke(Now, "logout");
        session.Revoke(Now.AddMinutes(1), "password_changed");

        // The first reason is the true one; a later sweep must not rewrite history.
        session.RevokedAt.ShouldBe(Now);
        session.RevokedReason.ShouldBe("logout");
    }

    [Fact]
    public void A_long_user_agent_is_truncated_rather_than_rejected()
    {
        var session = UserSession.Open(
            Guid.CreateVersion7(),
            Now,
            TimeSpan.FromHours(1),
            ipAddress: null,
            userAgent: new string('x', 900));

        session.UserAgent!.Length.ShouldBe(512);
    }
}
