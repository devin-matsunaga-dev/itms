using System.Net;
using System.Net.Http.Json;
using Itms.IntegrationTests.Identity;

namespace Itms.IntegrationTests.DirectoryModule;

/// <summary>
/// The department endpoints over the wire: uniqueness, retirement instead of deletion,
/// and the role boundary SPEC.md §13 puts around administration.
/// </summary>
[Collection(IdentityTestGroup.Name)]
public sealed class DepartmentEndpointTests(IdentityWebFixture fixture) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => new(fixture.ResetAsync());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task An_admin_creates_a_department_and_can_read_it_back()
    {
        using var admin = await SignedInAsync("admin");

        var created = await DirectoryClient.CreateDepartmentAsync(admin, "Information Technology", "IT", Token);

        created.Name.ShouldBe("Information Technology");
        created.Code.ShouldBe("IT");
        created.IsActive.ShouldBeTrue();

        var fetched = await admin.GetAsync(new Uri($"/api/v1/departments/{created.Id}", UriKind.Relative), Token);
        fetched.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await DirectoryClient.ReadAsync<DepartmentDto>(fetched, Token)).Id.ShouldBe(created.Id);
    }

    [Fact]
    public async Task Creation_returns_a_location_header_pointing_at_the_new_department()
    {
        using var admin = await SignedInAsync("admin");

        var response = await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Post,
            "/api/v1/departments",
            new { name = "Finance", code = "FIN", description = (string?)null },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await DirectoryClient.ReadAsync<DepartmentDto>(response, Token);
        response.Headers.Location!.ToString().ShouldEndWith($"/api/v1/departments/{created.Id}");
    }

    /// <summary>Case-insensitively: the unique index sits on the normalised column.</summary>
    [Fact]
    public async Task A_duplicate_department_name_is_refused_regardless_of_case()
    {
        using var admin = await SignedInAsync("admin");
        await DirectoryClient.CreateDepartmentAsync(admin, "Finance", null, Token);

        var response = await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Post,
            "/api/v1/departments",
            new { name = "finance", code = (string?)null, description = (string?)null },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await DirectoryClient.ReadAsync<ProblemDto>(response, Token);
        problem.Code.ShouldBe("directory.duplicate_department_name");
        problem.Detail!.ShouldContain("Finance");
    }

    [Fact]
    public async Task A_duplicate_department_code_is_refused()
    {
        using var admin = await SignedInAsync("admin");
        await DirectoryClient.CreateDepartmentAsync(admin, "Finance", "FIN", Token);

        var response = await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Post,
            "/api/v1/departments",
            new { name = "Facilities", code = "fin", description = (string?)null },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await DirectoryClient.ReadAsync<ProblemDto>(response, Token)).Code
            .ShouldBe("directory.duplicate_department_code");
    }

    [Fact]
    public async Task Several_departments_may_have_no_code_at_all()
    {
        using var admin = await SignedInAsync("admin");

        await DirectoryClient.CreateDepartmentAsync(admin, "Finance", null, Token);
        await DirectoryClient.CreateDepartmentAsync(admin, "Operations", null, Token);

        var list = await ListAsync(admin, "/api/v1/departments");
        list.Total.ShouldBe(2);
    }

    [Fact]
    public async Task An_update_may_keep_the_department_its_own_name()
    {
        using var admin = await SignedInAsync("admin");
        var department = await DirectoryClient.CreateDepartmentAsync(admin, "Finance", "FIN", Token);

        var response = await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"/api/v1/departments/{department.Id}",
            new { name = "Finance", code = "FIN", description = "Accounting and payroll." },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await DirectoryClient.ReadAsync<DepartmentDto>(response, Token);
        updated.Description.ShouldBe("Accounting and payroll.");
    }

    [Fact]
    public async Task An_update_onto_another_departments_name_is_refused()
    {
        using var admin = await SignedInAsync("admin");
        await DirectoryClient.CreateDepartmentAsync(admin, "Finance", null, Token);
        var operations = await DirectoryClient.CreateDepartmentAsync(admin, "Operations", null, Token);

        var response = await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Put,
            $"/api/v1/departments/{operations.Id}",
            new { name = "Finance", code = (string?)null, description = (string?)null },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// Retirement is what stands in for a delete. The row survives so existing references
    /// keep resolving; it simply stops appearing in the default list.
    /// </summary>
    [Fact]
    public async Task A_retired_department_leaves_the_default_list_but_still_exists()
    {
        using var admin = await SignedInAsync("admin");
        var department = await DirectoryClient.CreateDepartmentAsync(admin, "Typing Pool", null, Token);

        var retire = await DirectoryClient.SendAsync(
            admin, HttpMethod.Post, $"/api/v1/departments/{department.Id}/deactivate", null, Token);
        retire.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await ListAsync(admin, "/api/v1/departments")).Total.ShouldBe(0);
        (await ListAsync(admin, "/api/v1/departments?includeInactive=true")).Total.ShouldBe(1);

        var fetched = await admin.GetAsync(new Uri($"/api/v1/departments/{department.Id}", UriKind.Relative), Token);
        fetched.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await DirectoryClient.ReadAsync<DepartmentDto>(fetched, Token)).IsActive.ShouldBeFalse();

        var reinstate = await DirectoryClient.SendAsync(
            admin, HttpMethod.Post, $"/api/v1/departments/{department.Id}/reactivate", null, Token);
        reinstate.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListAsync(admin, "/api/v1/departments")).Total.ShouldBe(1);
    }

    [Fact]
    public async Task There_is_no_delete_route_for_a_department()
    {
        using var admin = await SignedInAsync("admin");
        var department = await DirectoryClient.CreateDepartmentAsync(admin, "Finance", null, Token);

        var response = await DirectoryClient.SendAsync(
            admin, HttpMethod.Delete, $"/api/v1/departments/{department.Id}", null, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Search_matches_name_and_code_and_escapes_wildcards()
    {
        using var admin = await SignedInAsync("admin");
        await DirectoryClient.CreateDepartmentAsync(admin, "Finance", "FIN", Token);
        await DirectoryClient.CreateDepartmentAsync(admin, "Operations", "OPS", Token);

        (await ListAsync(admin, "/api/v1/departments?search=nanc")).Total.ShouldBe(1);
        (await ListAsync(admin, "/api/v1/departments?search=ops")).Total.ShouldBe(1);

        // A bare % typed into the search box is a literal, not "match everything".
        (await ListAsync(admin, "/api/v1/departments?search=%25")).Total.ShouldBe(0);
    }

    [Fact]
    public async Task A_name_that_is_only_whitespace_is_a_validation_failure_not_a_conflict()
    {
        using var admin = await SignedInAsync("admin");

        var response = await DirectoryClient.SendAsync(
            admin,
            HttpMethod.Post,
            "/api/v1/departments",
            new { name = "   ", code = (string?)null, description = (string?)null },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_unknown_department_is_a_404()
    {
        using var admin = await SignedInAsync("admin");

        var response = await admin.GetAsync(new Uri($"/api/v1/departments/{Guid.CreateVersion7()}", UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await DirectoryClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("directory.department_not_found");
    }

    /// <summary>
    /// Any signed-in account may read: an end user filing their own ticket has to pick a
    /// department. Only an admin may write.
    /// </summary>
    [Theory]
    [InlineData("tech")]
    [InlineData("user")]
    public async Task A_non_admin_may_read_departments_but_not_create_one(string userName)
    {
        using var admin = await SignedInAsync("admin");
        await DirectoryClient.CreateDepartmentAsync(admin, "Finance", null, Token);

        using var caller = await SignedInAsync(userName);

        var read = await caller.GetAsync(new Uri("/api/v1/departments", UriKind.Relative), Token);
        read.StatusCode.ShouldBe(HttpStatusCode.OK);

        var write = await DirectoryClient.SendAsync(
            caller,
            HttpMethod.Post,
            "/api/v1/departments",
            new { name = "Shadow IT", code = (string?)null, description = (string?)null },
            Token);

        // 403, not a 404 disguise and not a redirect (ARCHITECTURE.md §6).
        write.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        write.Headers.Location.ShouldBeNull();
    }

    [Fact]
    public async Task An_anonymous_caller_is_challenged()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/departments", UriKind.Relative), Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// CONVENTIONS.md's security floor: a cookie-authenticated write without an
    /// antiforgery token is exactly the shape a hostile page exploits.
    /// </summary>
    [Fact]
    public async Task A_write_without_an_antiforgery_token_is_rejected()
    {
        using var admin = await SignedInAsync("admin");

        var response = await admin.PostAsJsonAsync(
            new Uri("/api/v1/departments", UriKind.Relative),
            new { name = "Finance", code = (string?)null, description = (string?)null },
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await DirectoryClient.ReadAsync<ProblemDto>(response, Token)).Code.ShouldBe("auth.antiforgery_failed");
    }

    private async Task<HttpClient> SignedInAsync(string userName)
    {
        var client = fixture.CreateClient();
        var response = await AuthClient.LoginAsync(client, userName, AuthClient.Password, Token);
        response.EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<PageDto<DepartmentDto>> ListAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(new Uri(path, UriKind.Relative), Token);
        response.EnsureSuccessStatusCode();
        return await DirectoryClient.ReadAsync<PageDto<DepartmentDto>>(response, Token);
    }
}
