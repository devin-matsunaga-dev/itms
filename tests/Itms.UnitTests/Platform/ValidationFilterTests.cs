using FluentValidation;
using Itms.Platform.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Itms.UnitTests.Platform;

public sealed class ValidationFilterTests
{
    private sealed record CreateTicket(string Subject, TicketRequester Requester);

    private sealed record TicketRequester(string Email);

    private sealed class CreateTicketValidator : AbstractValidator<CreateTicket>
    {
        public CreateTicketValidator()
        {
            RuleFor(x => x.Subject).NotEmpty().WithMessage("Subject is required.");
            RuleFor(x => x.Requester.Email).EmailAddress().WithMessage("A valid email is required.");
        }
    }

    [Fact]
    public async Task A_valid_request_reaches_the_handler()
    {
        var handlerRan = false;

        var response = await InvokeAsync(
            new CreateTicket("Laptop won't boot", new TicketRequester("user@example.com")),
            registerValidator: true,
            () =>
            {
                handlerRan = true;
                return Results.Ok("INC-0001");
            });

        handlerRan.ShouldBeTrue();
        response.ShouldBeOfType<Ok<string>>();
    }

    [Fact]
    public async Task An_invalid_request_short_circuits_before_the_handler()
    {
        var handlerRan = false;

        var response = await InvokeAsync(
            new CreateTicket(string.Empty, new TicketRequester("not-an-email")),
            registerValidator: true,
            () =>
            {
                handlerRan = true;
                return Results.Ok();
            });

        handlerRan.ShouldBeFalse();
        var problem = response.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problem.ProblemDetails.Extensions["code"].ShouldBe("validation.failed");
    }

    [Fact]
    public async Task Field_names_come_back_camel_cased_so_the_client_can_map_them_onto_the_form()
    {
        var response = await InvokeAsync(
            new CreateTicket(string.Empty, new TicketRequester("not-an-email")),
            registerValidator: true,
            () => Results.Ok());

        var details = response.ShouldBeOfType<ProblemHttpResult>()
            .ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>();
        details.Errors.Keys.ShouldBe(["subject", "requester.email"], ignoreOrder: true);
        details.Errors["subject"].ShouldBe(["Subject is required."]);
    }

    [Fact]
    public async Task A_request_with_no_registered_validator_is_passed_through()
    {
        var handlerRan = false;

        await InvokeAsync(
            new CreateTicket(string.Empty, new TicketRequester("nope")),
            registerValidator: false,
            () =>
            {
                handlerRan = true;
                return Results.Ok();
            });

        handlerRan.ShouldBeTrue();
    }

    [Fact]
    public async Task A_missing_request_body_is_a_400_rather_than_a_null_reference_in_the_handler()
    {
        var context = BuildContext(argument: "not the request model", registerValidator: true);

        var response = await new ValidationFilter<CreateTicket>()
            .InvokeAsync(context, _ => ValueTask.FromResult<object?>(Results.Ok()));

        var problem = response.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problem.ProblemDetails.Extensions["code"].ShouldBe("request.missing");
    }

    private static async Task<object?> InvokeAsync(CreateTicket request, bool registerValidator, Func<IResult> handler)
    {
        var context = BuildContext(request, registerValidator);

        return await new ValidationFilter<CreateTicket>()
            .InvokeAsync(context, _ => ValueTask.FromResult<object?>(handler()));
    }

    private static EndpointFilterInvocationContext BuildContext(object argument, bool registerValidator)
    {
        var services = new ServiceCollection();
        if (registerValidator)
        {
            services.AddSingleton<IValidator<CreateTicket>, CreateTicketValidator>();
        }

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        return EndpointFilterInvocationContext.Create(httpContext, argument);
    }
}
