namespace Itms.Modules.Identity.Features.Auth.Login;

/// <summary>Credentials offered at sign-in.</summary>
/// <param name="UserName">The sign-in name or the email address; either is accepted.</param>
/// <param name="Password">The password. Never logged, never echoed, never stored outside its hash.</param>
public sealed record LoginRequest(string UserName, string Password);
