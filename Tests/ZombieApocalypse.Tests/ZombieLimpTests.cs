namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests how a zombie slows down as it's hurt
	/// </summary>
	public class ZombieLimpTests
	{
		/// <summary>
		/// Checks that a zombie moves at full speed down to half health, then slows steadily to the wounded speed as its
		/// health runs out, its health counting as none below none and as full above full
		/// </summary>
		/// <param name="health">Its health</param>
		/// <param name="woundedSpeed">How fast it moves as its health runs out, as a percent of its full speed</param>
		/// <param name="expected">How fast it moves, as a fraction of its full speed</param>
		[Theory]
		[InlineData(100f, 66, 1f)]
		[InlineData(150f, 66, 1f)]
		[InlineData(75f, 66, 1f)]
		[InlineData(50f, 66, 1f)]
		[InlineData(25f, 66, 0.83f)]
		[InlineData(40f, 50, 0.9f)]
		[InlineData(0f, 66, 0.66f)]
		[InlineData(-10f, 66, 0.66f)]
		[InlineData(0f, 100, 1f)]
		[InlineData(0f, 10, 0.1f)]
		public void SlowsBelowHalfHealth(float health, int woundedSpeed, float expected)
		{
			Assert.Equal(expected, ZombieLimp.SpeedScale(health, 100f, woundedSpeed), 4);
		}

		/// <summary>
		/// Checks that a zombie with no health to lose moves at full speed
		/// </summary>
		/// <param name="maxHealth">Its health when unhurt</param>
		[Theory]
		[InlineData(0f)]
		[InlineData(-5f)]
		public void NoMaxHealthIsFullSpeed(float maxHealth)
		{
			Assert.Equal(1f, ZombieLimp.SpeedScale(0f, maxHealth, 66));
		}

		/// <summary>
		/// Checks that the slowing goes by the share of its health a zombie has left, not how much it has
		/// </summary>
		[Fact]
		public void SlowingGoesByShareOfHealth()
		{
			Assert.Equal(ZombieLimp.SpeedScale(25f, 100f, 66), ZombieLimp.SpeedScale(250f, 1000f, 66), 4);
		}
	}
}
