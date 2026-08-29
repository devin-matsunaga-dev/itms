using Itms.Messaging.Outbox;

namespace Itms.UnitTests.Messaging;

public sealed class RetryPolicyTests
{
    private static readonly TimeSpan Base = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Max = TimeSpan.FromMinutes(10);

    [Fact]
    public void First_retry_waits_the_base_delay()
    {
        RetryPolicy.DelayFor(attempts: 1, Base, Max).ShouldBe(Base);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(4, 40)]
    [InlineData(5, 80)]
    public void Each_attempt_doubles_the_previous_delay(int attempts, int expectedSeconds)
    {
        RetryPolicy.DelayFor(attempts, Base, Max).ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void Delay_is_capped_at_the_ceiling()
    {
        RetryPolicy.DelayFor(attempts: 20, Base, Max).ShouldBe(Max);
    }

    /// <summary>
    /// The closed form <c>base * 2^attempts</c> overflows a long well before the highest
    /// MaxAttempts the options allow, and an overflowed delay is a negative one — which
    /// makes a message retry instantly, forever.
    /// </summary>
    [Fact]
    public void A_very_high_attempt_count_stays_at_the_ceiling_rather_than_overflowing()
    {
        var delay = RetryPolicy.DelayFor(attempts: 100, Base, Max);

        delay.ShouldBe(Max);
        delay.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void A_base_delay_at_or_above_the_ceiling_collapses_to_the_ceiling()
    {
        RetryPolicy.DelayFor(attempts: 1, TimeSpan.FromMinutes(30), Max).ShouldBe(Max);
    }

    [Fact]
    public void An_attempt_count_below_one_is_rejected()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => RetryPolicy.DelayFor(attempts: 0, Base, Max));
    }
}
