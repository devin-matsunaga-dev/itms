using Itms.Platform.Results;

namespace Itms.UnitTests.Platform;

public sealed class ResultTests
{
    [Fact]
    public void Success_has_no_error()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Failure_carries_the_error()
    {
        var result = Result.Failure(Error.Conflict("ticket.illegal_transition", "Cannot reopen a closed ticket."));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("ticket.illegal_transition");
        result.Error.Kind.ShouldBe(ErrorKind.Conflict);
    }

    [Fact]
    public void Success_of_T_exposes_the_value()
    {
        var result = Result.Success(42);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        result.TryGetValue(out var value).ShouldBeTrue();
        value.ShouldBe(42);
    }

    [Fact]
    public void Reading_the_value_of_a_failure_throws()
    {
        var result = Result.Failure<int>(Error.NotFound("ticket.not_found", "No such ticket."));

        Should.Throw<InvalidOperationException>(() => result.Value)
            .Message.ShouldContain("ticket.not_found");
    }

    [Fact]
    public void TryGetValue_on_a_failure_reports_false()
    {
        var result = Result.Failure<string>(Error.NotFound("asset.not_found", "No such asset."));

        result.TryGetValue(out var value).ShouldBeFalse();
        value.ShouldBeNull();
    }

    [Fact]
    public void An_error_converts_implicitly_to_a_failed_result()
    {
        Result untyped = Error.Forbidden("ticket.forbidden", "Not your ticket.");
        Result<int> typed = Error.Forbidden("ticket.forbidden", "Not your ticket.");

        untyped.IsFailure.ShouldBeTrue();
        typed.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void A_value_converts_implicitly_to_a_successful_result()
    {
        Result<string> result = "INC-0001";

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("INC-0001");
    }

    [Fact]
    public void Map_transforms_a_success()
    {
        var result = Result.Success(21).Map(v => v * 2);

        result.Value.ShouldBe(42);
    }

    [Fact]
    public void Map_passes_a_failure_through_untouched()
    {
        var error = Error.Unexpected("db.unavailable", "The database is unreachable.");

        var result = Result.Failure<int>(error).Map(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
    }

    [Fact]
    public void Match_selects_the_branch_that_happened()
    {
        Result.Success(7).Match(v => $"ok:{v}", e => $"err:{e.Code}").ShouldBe("ok:7");
        Result.Failure<int>(Error.Conflict("c", "m")).Match(v => $"ok:{v}", e => $"err:{e.Code}").ShouldBe("err:c");
        Result.Success().Match(() => "ok", e => e.Code).ShouldBe("ok");
    }

    [Fact]
    public void A_validation_error_carries_per_field_messages()
    {
        var error = Error.Validation(
            "validation.failed",
            "One or more fields are invalid.",
            new Dictionary<string, string[]> { ["subject"] = ["Subject is required."] });

        error.Kind.ShouldBe(ErrorKind.Validation);
        error.FieldErrors!["subject"].ShouldBe(["Subject is required."]);
    }
}
