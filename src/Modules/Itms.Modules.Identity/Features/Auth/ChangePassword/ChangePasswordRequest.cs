namespace Itms.Modules.Identity.Features.Auth.ChangePassword;

/// <summary>A password change by the account holder.</summary>
/// <param name="CurrentPassword">The password in force. Proving possession is what stops a borrowed session changing it.</param>
/// <param name="NewPassword">The replacement. Checked against the password policy server-side.</param>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
