namespace Itms.Modules.Directory.Features.Departments.CreateDepartment;

/// <summary>The body of <c>POST /api/v1/departments</c>.</summary>
/// <param name="Name">The display name. Unique, case-insensitively.</param>
/// <param name="Code">A short code such as <c>FIN</c>, or <see langword="null"/>. Unique when present.</param>
/// <param name="Description">Free text, or <see langword="null"/>.</param>
public sealed record CreateDepartmentRequest(string Name, string? Code, string? Description);
