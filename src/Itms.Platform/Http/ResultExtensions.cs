using Itms.Platform.Results;
using Microsoft.AspNetCore.Http;
// Aliased because Microsoft.AspNetCore.Http.Results collides with this project's
// Itms.Platform.Results namespace.
using MinimalApi = Microsoft.AspNetCore.Http.Results;

namespace Itms.Platform.Http;

/// <summary>
/// The endpoint-layer half of <see cref="Result"/>: handlers decide what happened,
/// these decide what that looks like over HTTP. Endpoints stay one line and no
/// endpoint invents its own status code for "not found".
/// </summary>
public static class ResultExtensions
{
    /// <summary>200 with the value, or the mapped problem response.</summary>
    public static IResult ToOk<TValue>(this Result<TValue> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Match(value => MinimalApi.Ok(value), ProblemDetailsMapper.ToProblem);
    }

    /// <summary>200 with a projection of the value, or the mapped problem response.</summary>
    public static IResult ToOk<TValue, TResponse>(this Result<TValue> result, Func<TValue, TResponse> project)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(project);
        return result.Match(value => MinimalApi.Ok(project(value)), ProblemDetailsMapper.ToProblem);
    }

    /// <summary>201 with a <c>Location</c> header, or the mapped problem response.</summary>
    public static IResult ToCreated<TValue>(this Result<TValue> result, Func<TValue, string> location)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(location);
        return result.Match(value => MinimalApi.Created(location(value), value), ProblemDetailsMapper.ToProblem);
    }

    /// <summary>204, or the mapped problem response.</summary>
    public static IResult ToNoContent(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Match(MinimalApi.NoContent, ProblemDetailsMapper.ToProblem);
    }

    /// <summary>204, or the mapped problem response, discarding the produced value.</summary>
    public static IResult ToNoContent<TValue>(this Result<TValue> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Match(_ => MinimalApi.NoContent(), ProblemDetailsMapper.ToProblem);
    }
}
