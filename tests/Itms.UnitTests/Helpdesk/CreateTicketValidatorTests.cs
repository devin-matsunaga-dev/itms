using Itms.Modules.Helpdesk.Domain;
using Itms.Modules.Helpdesk.Features.Tickets.CreateTicket;

namespace Itms.UnitTests.Helpdesk;

/// <summary>
/// The shape rules on a create request.
/// </summary>
/// <remarks>
/// Shape only — whether the category exists or the requester is active needs the database
/// and belongs to the handler. What matters here is that the bounds match
/// <see cref="Ticket"/>'s own constants, because <see cref="Ticket.Create"/> throws on the
/// same ones and a validator that let something past would turn a caller's mistake into a
/// 500.
/// </remarks>
public sealed class CreateTicketValidatorTests
{
    private static readonly CreateTicketValidator Validator = new();

    private static CreateTicketRequest Valid() => new(
        "Laptop will not charge",
        "It stops charging at 40% and the light goes amber.",
        Guid.CreateVersion7(),
        Guid.CreateVersion7());

    [Fact]
    public void A_complete_request_passes()
    {
        Validator.Validate(Valid()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void The_requester_and_department_are_genuinely_optional()
    {
        var request = Valid() with { RequesterId = null, DepartmentId = null };

        Validator.Validate(request).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_subject_is_refused(string subject)
    {
        var result = Validator.Validate(Valid() with { Subject = subject });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateTicketRequest.Subject));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_description_is_refused(string description)
    {
        var result = Validator.Validate(Valid() with { Description = description });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateTicketRequest.Description));
    }

    [Fact]
    public void A_subject_at_the_column_length_is_accepted()
    {
        var request = Valid() with { Subject = new string('x', Ticket.SubjectMaxLength) };

        Validator.Validate(request).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void A_subject_one_character_over_is_refused()
    {
        var request = Valid() with { Subject = new string('x', Ticket.SubjectMaxLength + 1) };

        Validator.Validate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void A_description_one_character_over_is_refused()
    {
        var request = Valid() with { Description = new string('x', Ticket.DescriptionMaxLength + 1) };

        Validator.Validate(request).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void A_missing_category_is_refused()
    {
        var result = Validator.Validate(Valid() with { CategoryId = Guid.Empty });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateTicketRequest.CategoryId));
    }

    [Fact]
    public void A_missing_priority_is_refused()
    {
        var result = Validator.Validate(Valid() with { PriorityId = Guid.Empty });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateTicketRequest.PriorityId));
    }

    /// <summary>
    /// An explicitly empty Guid is an uninitialised field, not an omission — and it would
    /// otherwise reach <c>IUserLookup</c> as a real id and come back as "no such user",
    /// which tells the caller nothing about what they actually did wrong.
    /// </summary>
    [Fact]
    public void An_explicitly_empty_requester_is_refused_rather_than_treated_as_absent()
    {
        var result = Validator.Validate(Valid() with { RequesterId = Guid.Empty });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateTicketRequest.RequesterId));
    }

    [Fact]
    public void An_explicitly_empty_department_is_refused_rather_than_treated_as_absent()
    {
        var result = Validator.Validate(Valid() with { DepartmentId = Guid.Empty });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateTicketRequest.DepartmentId));
    }
}
