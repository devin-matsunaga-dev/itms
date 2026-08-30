using FluentValidation;
using Itms.Modules.Directory.Domain;

namespace Itms.Modules.Directory.Features.Departments.CreateDepartment;

/// <summary>Checks the shape of a create request before the handler runs.</summary>
/// <remarks>
/// Uniqueness is not checked here: it needs the database, and a validator that queried
/// one would still lose the race to the unique index. The handler owns that, and returns
/// a 409 rather than a 400.
/// </remarks>
public sealed class CreateDepartmentValidator : AbstractValidator<CreateDepartmentRequest>
{
    /// <summary>Builds the rules.</summary>
    public CreateDepartmentValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Enter a department name.")
            .MaximumLength(Department.NameMaxLength);

        RuleFor(request => request.Code)
            .MaximumLength(Department.CodeMaxLength);

        RuleFor(request => request.Description)
            .MaximumLength(Department.DescriptionMaxLength);
    }
}
