namespace Planaffe.Domain.Identities;

/// <summary>
/// A human identity, authenticated by a session in the browser or by a user
/// token at the console, and possibly holding the administrator role
/// (<c>CONTEXT.md</c>, User).
/// </summary>
/// <remarks>
/// Cut one authenticates by token only; the password and the browser session
/// are cut-three columns and a cut-three table (<c>docs/storage.md</c>, The
/// doors left open).
/// </remarks>
public sealed class User : Identity
{
    private User()
    {
    }

    private User(Guid id, string name, bool administrator, DateTimeOffset createdAt)
        : base(id, name, administrator, createdAt)
    {
    }

    public override IdentityKind Kind => IdentityKind.User;

    public static User Create(string name, bool administrator, DateTimeOffset createdAt) =>
        new(Guid.CreateVersion7(), NormalizeName(name), administrator, createdAt);
}
