namespace Itms.IntegrationTests;

/// <summary>
/// Names the trait that separates the volume and performance suites from the ordinary run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the split exists.</b> CONVENTIONS.md budgets the whole test run at two minutes on
/// a dev machine, and a volume test is the one shape that cannot honour it: proving a query
/// still plans correctly at fifty thousand rows means writing fifty thousand rows, and no
/// amount of care makes that fast. STATUS.md recorded the run drifting past the budget from
/// WP-2.1 onwards and named this split as the lever; WP-2.3 is the package that pulled it,
/// at the human's direction.
/// </para>
/// <para>
/// <b>Excluded is not abandoned.</b> A suite nobody runs is a suite that silently rots, so
/// CI runs this category in a step of its own after the ordinary one — it is off the
/// developer's inner loop, not off the pipeline. CONVENTIONS.md §Testing carries both
/// commands.
/// </para>
/// <para>
/// <b>What belongs here.</b> Only tests whose cost is the row count itself. A test that is
/// merely slow is a test to fix, not a test to label — moving an ordinary integration test
/// behind this trait would hide it from the run that is meant to catch it.
/// </para>
/// </remarks>
public static class VolumeTest
{
    /// <summary>The trait name. Paired with <see cref="Value"/> on a volume suite.</summary>
    public const string Name = "Category";

    /// <summary>The trait value the ordinary run excludes and the volume run selects.</summary>
    public const string Value = "Volume";
}
