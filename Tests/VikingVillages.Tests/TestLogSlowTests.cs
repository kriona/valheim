using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how the test log tells a slow update from a quick one
	/// </summary>
	public class TestLogSlowTests
	{
		/// <summary>
		/// Checks that an update just under SlowTime isn't slow, and one at it or over is, with its length in seconds
		/// </summary>
		[Fact]
		public void SlowFromSlowTimeOn()
		{
			Assert.False(TestLogSlow.IsSlow(1000L, 1999L, 1000L, out double quick));
			Assert.Equal(0.999, quick, 6);
			Assert.True(TestLogSlow.IsSlow(1000L, 2000L, 1000L, out double atLimit));
			Assert.Equal(1.0, atLimit, 6);
			Assert.True(TestLogSlow.IsSlow(1000L, 119000L, 1000L, out double stall));
			Assert.Equal(118.0, stall, 6);
		}

		/// <summary>
		/// Checks that an update that wasn't timed, started at 0 while no run was going, is never slow
		/// </summary>
		[Fact]
		public void UntimedNeverSlow()
		{
			Assert.False(TestLogSlow.IsSlow(0L, 5000000L, 1000L, out double seconds));
			Assert.Equal(0.0, seconds);
		}
	}
}
