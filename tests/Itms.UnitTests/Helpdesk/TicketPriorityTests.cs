using System.Reflection;
using Itms.Modules.Helpdesk.Domain;
using Itms.TestSupport;

namespace Itms.UnitTests.Helpdesk;

/// <summary>
/// The ticket-priority entity: the two identifiers, the SLA-target invariant, and the
/// immutability of the code.
/// </summary>
/// <remarks>
/// Nothing here computes an SLA. This entity holds targets; WP-1.8 is what measures
/// against them.
/// </remarks>
public sealed class TicketPriorityTests
{
    private static readonly Guid Actor = Guid.CreateVersion7();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.Zero));

    private TicketPriority Critical() =>
        TicketPriority.Create("critical", "Critical", "Service is down.", 1, 15, 240, _clock.UtcNow, Actor);

    [Fact]
    public void A_new_priority_is_active_and_stamped()
    {
        var priority = Critical();

        priority.Id.ShouldNotBe(Guid.Empty);
        priority.Code.ShouldBe("critical");
        priority.Rank.ShouldBe(1);
        priority.ResponseTargetMinutes.ShouldBe(15);
        priority.ResolutionTargetMinutes.ShouldBe(240);
        priority.IsActive.ShouldBeTrue();
        priority.CreatedAt.ShouldBe(_clock.UtcNow);
        priority.CreatedBy.ShouldBe(Actor);
    }

    [Fact]
    public void The_code_is_trimmed_and_lower_cased()
    {
        var priority = TicketPriority.Create("  CRITICAL ", "Critical", null, 1, 15, 240, _clock.UtcNow, Actor);

        priority.Code.ShouldBe("critical");
    }

    [Fact]
    public void The_name_is_trimmed_and_normalised()
    {
        var priority = TicketPriority.Create("high", "  high ", null, 2, 60, 480, _clock.UtcNow, Actor);

        priority.Name.ShouldBe("high");
        priority.NormalizedName.ShouldBe("HIGH");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1critical")]
    [InlineData("very critical")]
    [InlineData("very_critical")]
    [InlineData("Critical!")]
    public void A_malformed_code_is_refused(string code) =>
        Should.Throw<ArgumentException>(() =>
            TicketPriority.Create(code, "Critical", null, 1, 15, 240, _clock.UtcNow, Actor));

    [Theory]
    [InlineData("critical")]
    [InlineData("very-high")]
    [InlineData("p1")]
    public void A_well_formed_code_is_accepted(string code) =>
        TicketPriority.Create(code, "Critical", null, 1, 15, 240, _clock.UtcNow, Actor).Code.ShouldBe(code);

    [Fact]
    public void An_over_long_code_is_refused() =>
        Should.Throw<ArgumentException>(() => TicketPriority.Create(
            new string('a', PriorityCode.MaxLength + 1), "Critical", null, 1, 15, 240, _clock.UtcNow, Actor));

    /// <summary>
    /// The rule that keeps WP-1.8 honest: a resolution due before the response is a
    /// breach no amount of work could avoid.
    /// </summary>
    [Fact]
    public void A_resolution_target_sooner_than_the_response_target_is_refused() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            TicketPriority.Create("critical", "Critical", null, 1, 240, 15, _clock.UtcNow, Actor));

    [Fact]
    public void Equal_response_and_resolution_targets_are_allowed() =>
        TicketPriority.Create("critical", "Critical", null, 1, 60, 60, _clock.UtcNow, Actor)
            .ResolutionTargetMinutes.ShouldBe(60);

    [Theory]
    [InlineData(0, 240)]
    [InlineData(-1, 240)]
    [InlineData(15, 0)]
    [InlineData(TicketPriority.MaxTargetMinutes + 1, TicketPriority.MaxTargetMinutes + 1)]
    public void An_out_of_range_target_is_refused(int response, int resolution) =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            TicketPriority.Create("critical", "Critical", null, 1, response, resolution, _clock.UtcNow, Actor));

    [Fact]
    public void A_rename_leaves_the_code_and_the_id_alone()
    {
        var priority = Critical();
        var id = priority.Id;

        _clock.Advance(TimeSpan.FromHours(1));
        priority.Rename("Sev 1", _clock.UtcNow, Actor);

        priority.Id.ShouldBe(id);
        priority.Code.ShouldBe("critical");
        priority.Name.ShouldBe("Sev 1");
        priority.NormalizedName.ShouldBe("SEV 1");
        priority.UpdatedAt.ShouldBe(_clock.UtcNow);
    }

    /// <summary>
    /// The code is the key colours, integrations, and later rules resolve against. It is
    /// immutable by having nothing that sets it after construction, so this asserts the
    /// absence rather than a refusal.
    /// </summary>
    [Fact]
    public void Nothing_on_the_entity_can_change_the_code()
    {
        typeof(TicketPriority).GetProperty(nameof(TicketPriority.Code))!
            .SetMethod!.IsPublic.ShouldBeFalse();

        // The entity's own surface only: DeclaredOnly drops object's members (GetHashCode
        // would otherwise match), and IsSpecialName drops the property accessors, leaving
        // the methods a caller could invoke to change something.
        DeclaredMethodNames<TicketPriority>()
            .ShouldNotContain(name => name.Contains("Code", StringComparison.Ordinal));
    }

    [Fact]
    public void Setting_the_targets_moves_both_together()
    {
        var priority = Critical();

        _clock.Advance(TimeSpan.FromHours(1));
        priority.SetTargets(30, 480, _clock.UtcNow, Actor);

        priority.ResponseTargetMinutes.ShouldBe(30);
        priority.ResolutionTargetMinutes.ShouldBe(480);
        priority.UpdatedAt.ShouldBe(_clock.UtcNow);
    }

    [Fact]
    public void Setting_an_inverted_pair_of_targets_is_refused_and_changes_nothing()
    {
        var priority = Critical();

        Should.Throw<ArgumentOutOfRangeException>(() => priority.SetTargets(480, 30, _clock.UtcNow, Actor));

        priority.ResponseTargetMinutes.ShouldBe(15);
        priority.ResolutionTargetMinutes.ShouldBe(240);
    }

    [Fact]
    public void Retiring_and_reinstating_flips_the_flag()
    {
        var priority = Critical();

        priority.Deactivate(_clock.UtcNow, Actor);
        priority.IsActive.ShouldBeFalse();

        priority.Reactivate(_clock.UtcNow, Actor);
        priority.IsActive.ShouldBeTrue();
    }

    /// <summary>The same absence WP-1.1 relies on for categories.</summary>
    [Fact]
    public void The_entity_exposes_no_way_to_delete_itself() =>
        DeclaredMethodNames<TicketPriority>()
            .ShouldNotContain(name =>
                name.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Remove", StringComparison.OrdinalIgnoreCase));

    /// <summary>The names of the public instance methods a type declares itself.</summary>
    /// <typeparam name="T">The type to inspect.</typeparam>
    /// <returns>The method names, accessors and inherited members excluded.</returns>
    internal static IEnumerable<string> DeclaredMethodNames<T>() =>
        typeof(T)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name);
}
