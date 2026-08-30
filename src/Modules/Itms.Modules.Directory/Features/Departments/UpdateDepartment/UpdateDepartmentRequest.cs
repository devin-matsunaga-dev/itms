namespace Itms.Modules.Directory.Features.Departments.UpdateDepartment;

/// <summary>The body of <c>PUT /api/v1/departments/{id}</c>.</summary>
/// <remarks>
/// A full replacement rather than a patch: every field is sent, and a null code or
/// description clears it. Activation is not here — retiring a department is its own
/// action, not a checkbox on an edit form, so it cannot be flipped by accident.
/// </remarks>
/// <param name="Name">The display name.</param>
/// <param name="Code">The short code, or <see langword="null"/> to clear it.</param>
/// <param name="Description">Free text, or <see langword="null"/> to clear it.</param>
public sealed record UpdateDepartmentRequest(string Name, string? Code, string? Description);
