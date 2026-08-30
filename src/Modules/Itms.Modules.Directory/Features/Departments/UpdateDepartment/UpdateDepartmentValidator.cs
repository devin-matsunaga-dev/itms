using FluentValidation;
using Itms.Modules.Directory.Domain;

namespace Itms.Modules.Directory.Features.Departments.UpdateDepartment;

/// <summary>Checks the shape of an update request before the handler runs.</summary>
public sealed class UpdateDepartmentValidator : AbstractValidator<UpdateDepartmentRequest>
{
    /// <summary>Builds the rules.</summary>
    public UpdateDepartmentValidator()
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
