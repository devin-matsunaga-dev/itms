using Itms.Platform.Http;
using Itms.Platform.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Itms.UnitTests.Platform;

public sealed class ProblemDetailsMapperTests
{
    [Theory]
    [InlineData(ErrorKind.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorKind.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorKind.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorKind.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorKind.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorKind.Unexpected, StatusCodes.Status500InternalServerError)]
    public void Each_error_kind_maps_to_its_status_code(ErrorKind kind, int expected)
    {
        ProblemDetailsMapper.StatusCodeFor(new Error("code", "message", kind)).ShouldBe(expected);
    }

    [Fact]
    public void A_forbidden_error_is_a_403_and_never_a_disguised_404()
    {
        var problem = ProblemDetailsMapper.ToProblem(Error.Forbidden("ticket.forbidden", "Not your ticket."))
            .ShouldBeOfType<ProblemHttpResult>();

        problem.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void A_problem_carries_the_error_code_the_client_can_switch_on()
    {
        var problem = ProblemDetailsMapper.ToProblem(Error.Conflict("ticket.illegal_transition", "Cannot reopen."))
            .ShouldBeOfType<ProblemHttpResult>();

        problem.ProblemDetails.Status.ShouldBe(StatusCodes.Status409Conflict);
        problem.ProblemDetails.Title.ShouldBe("Conflict");
        problem.ProblemDetails.Detail.ShouldBe("Cannot reopen.");
        problem.ProblemDetails.Type.ShouldNotBeNullOrWhiteSpace();
        problem.ProblemDetails.Extensions["code"].ShouldBe("ticket.illegal_transition");
    }

    [Fact]
    public void A_validation_error_with_field_messages_becomes_a_validation_problem()
    {
        var error = Error.Validation(
            "validation.failed",
            "One or more fields are invalid.",
            new Dictionary<string, string[]>
            {
                ["subject"] = ["Subject is required."],
                ["priority"] = ["Priority is required.", "Priority must be known."],
            });

        var problem = ProblemDetailsMapper.ToProblem(error).ShouldBeOfType<ProblemHttpResult>();
        var details = problem.ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>();

        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        details.Errors["subject"].ShouldBe(["Subject is required."]);
        details.Errors["priority"].Length.ShouldBe(2);
        details.Extensions["code"].ShouldBe("validation.failed");
    }

    [Fact]
    public void A_validation_error_without_field_messages_is_still_a_400_problem()
    {
        var problem = ProblemDetailsMapper.ToProblem(Error.Validation("import.too_large", "The file is too large."))
            .ShouldBeOfType<ProblemHttpResult>();

        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void A_successful_result_becomes_the_success_response_and_a_failure_becomes_the_problem()
    {
        Result.Success("INC-0001").ToOk().ShouldBeOfType<Ok<string>>();
        Result.Success("INC-0001").ToCreated(v => $"/api/v1/tickets/{v}").ShouldBeOfType<Created<string>>();
        Result.Success().ToNoContent().ShouldBeOfType<NoContent>();

        Result.Failure<string>(Error.NotFound("ticket.not_found", "No such ticket."))
            .ToOk()
            .ShouldBeOfType<ProblemHttpResult>()
            .StatusCode.ShouldBe(StatusCodes.Status404NotFound);

        Result.Failure(Error.NotFound("ticket.not_found", "No such ticket."))
            .ToNoContent()
            .ShouldBeOfType<ProblemHttpResult>()
            .StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void A_projection_is_applied_only_on_success()
    {
        Result.Success(21).ToOk(v => v * 2).ShouldBeOfType<Ok<int>>().Value.ShouldBe(42);

        Result.Failure<int>(Error.Forbidden("f", "m"))
            .ToOk<int, int>(_ => throw new InvalidOperationException("The projection must not run on a failure."))
            .ShouldBeOfType<ProblemHttpResult>();
    }
}
