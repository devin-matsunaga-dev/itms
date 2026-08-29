using Itms.Contracts.Events;
using NetArchTest.Rules;

namespace Itms.ArchitectureTests;

/// <summary>
/// Rules about the shape of the public contract surface. The boundary rules stop a
/// module reaching into another one; these stop the escape hatch — the contracts
/// assembly — from drifting into a dumping ground.
/// </summary>
public sealed class ContractShapeTests
{
    /// <summary>
    /// Every event is a fact about something that already happened, so every event is
    /// sealed and derives from <see cref="DomainEvent"/> — which is what carries the
    /// <c>EventId</c> the outbox and its idempotent consumers key on.
    /// </summary>
    [Fact]
    public void Every_domain_event_derives_from_DomainEvent_and_is_sealed()
    {
        var result = Types.InAssembly(SolutionLayout.Contracts)
            .That().ResideInNamespace("Itms.Contracts.Events")
            .And().DoNotHaveName(nameof(DomainEvent))
            .Should().Inherit(typeof(DomainEvent))
            .And().BeSealed()
            .GetResult();

        result.FailingTypeNames.ShouldBeNull();
    }

    /// <summary>
    /// Events are named for what happened. A present-tense name in this namespace is a
    /// command wearing an event's clothes, and it is the point at which modules start
    /// giving each other orders.
    /// </summary>
    [Fact]
    public void Every_domain_event_is_named_in_the_past_tense()
    {
        var presentTense = SolutionLayout.Contracts.GetTypes()
            .Where(t => t.Namespace == "Itms.Contracts.Events" && t != typeof(DomainEvent))
            .Where(t => !t.Name.EndsWith("ed", StringComparison.Ordinal)
                     && !t.Name.EndsWith("Offline", StringComparison.Ordinal))
            .Select(t => t.Name)
            .ToArray();

        presentTense.ShouldBeEmpty();
    }

    /// <summary>The eleven events ARCHITECTURE.md §5 names all exist and none has been quietly dropped.</summary>
    [Fact]
    public void The_events_named_in_the_architecture_all_exist()
    {
        string[] required =
        [
            nameof(TicketCreated), nameof(TicketAssigned), nameof(TicketStatusChanged), nameof(TicketResolved),
            nameof(AssetAssigned), nameof(AssetStatusChanged),
            nameof(DeviceWentOffline), nameof(DeviceRecovered),
            nameof(AlertRaised), nameof(AlertResolved),
            nameof(UserDeactivated),
        ];

        var declared = SolutionLayout.Contracts.GetTypes()
            .Where(t => t.Namespace == "Itms.Contracts.Events")
            .Select(t => t.Name)
            .ToArray();

        declared.ShouldBe([.. required, nameof(DomainEvent)], ignoreOrder: true);
    }

    /// <summary>
    /// A lookup is the read side of a module boundary. Naming them consistently is what
    /// makes an accidental command interface in this namespace obvious in review.
    /// </summary>
    [Fact]
    public void Every_lookup_contract_is_an_interface_named_Lookup()
    {
        var result = Types.InAssembly(SolutionLayout.Contracts)
            .That().ResideInNamespace("Itms.Contracts.Lookups")
            .And().AreInterfaces()
            .Should().HaveNameEndingWith("Lookup")
            .GetResult();

        result.FailingTypeNames.ShouldBeNull();

        SolutionLayout.Contracts.GetTypes()
            .Where(t => t.Namespace == "Itms.Contracts.Lookups" && t.IsInterface)
            .ShouldNotBeEmpty();
    }

    /// <summary>
    /// The contracts assembly exists to be referenced by every module, so everything in
    /// it is public. An internal type here is invisible to the modules that need it.
    /// </summary>
    [Fact]
    public void Every_contract_type_is_public()
    {
        var nonPublic = SolutionLayout.Contracts.GetTypes()
            .Where(t => !t.IsNested && !t.IsPublic && !IsCompilerGenerated(t))
            .Select(t => t.FullName)
            .ToArray();

        nonPublic.ShouldBeEmpty();
    }

    private static bool IsCompilerGenerated(Type type) =>
        type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false);
}
