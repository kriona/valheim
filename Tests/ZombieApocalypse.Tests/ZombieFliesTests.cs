namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests the arithmetic used to scale the flies copied onto zombies, and how a crowd of zombies thins each one's
	/// haze
	/// </summary>
	public class ZombieFliesTests
	{
		/// <summary>
		/// Checks that a divisor of a usable size divides as normal, either sign
		/// </summary>
		/// <param name="value">The numerator</param>
		/// <param name="divisor">The divisor</param>
		/// <param name="expected">The quotient</param>
		[Theory]
		[InlineData(6f, 2f, 3f)]
		[InlineData(6f, -2f, -3f)]
		[InlineData(1f, 0.5f, 2f)]
		[InlineData(0f, 4f, 0f)]
		public void SafeDivideDivides(float value, float divisor, float expected)
		{
			Assert.Equal(expected, ZombieFlies.SafeDivide(value, divisor), 5);
		}

		/// <summary>
		/// Checks that a divisor too small to divide by gives the numerator back rather than a huge or infinite number
		/// </summary>
		/// <param name="divisor">The divisor</param>
		[Theory]
		[InlineData(0f)]
		[InlineData(0.00005f)]
		[InlineData(-0.00005f)]
		public void SafeDivideKeepsTheValueForTinyDivisors(float divisor)
		{
			Assert.Equal(6f, ZombieFlies.SafeDivide(6f, divisor));
		}

		/// <summary>
		/// Checks that a zombie gives off its whole haze with up to three zombies near, itself included, and that a bigger
		/// crowd shares three zombies' worth
		/// </summary>
		/// <param name="percent">How strong a lone zombie's haze is, in percent of a draugr's</param>
		/// <param name="crowd">How many zombies are near, itself included</param>
		/// <param name="expected">The strength, as a share of a draugr's haze</param>
		[Theory]
		[InlineData(50, 0, 0.5f)]
		[InlineData(50, 1, 0.5f)]
		[InlineData(50, 3, 0.5f)]
		[InlineData(50, 6, 0.25f)]
		[InlineData(200, 4, 1.5f)]
		[InlineData(0, 6, 0f)]
		public void HazeThinsInACrowd(int percent, int crowd, float expected)
		{
			Assert.Equal(expected, ZombieFlies.HazeStrength(percent, crowd), 5);
		}

		/// <summary>
		/// Checks that the effects are sized by how wide the creature is next to a draugr, kept from 0.6 to 3 times the
		/// draugr's
		/// </summary>
		/// <param name="radius">How wide the creature is, in meters</param>
		/// <param name="sourceRadius">How wide the draugr is, in meters</param>
		/// <param name="expected">The scale</param>
		[Theory]
		[InlineData(0.5f, 0.5f, 1f)]
		[InlineData(0.75f, 0.5f, 1.5f)]
		[InlineData(0.1f, 0.5f, 0.6f)]
		[InlineData(5f, 0.5f, 3f)]
		[InlineData(1.5f, 0.5f, 3f)]
		[InlineData(0.3f, 0.5f, 0.6f)]
		public void EffectsAreSizedToTheCreature(float radius, float sourceRadius, float expected)
		{
			Assert.Equal(expected, ZombieFlies.EffectSize(radius, sourceRadius), 5);
		}

		/// <summary>
		/// Checks that a draugr too small to measure against counts as 1 meter wide, rather than sizing the effects
		/// past every limit
		/// </summary>
		/// <param name="radius">How wide the creature is, in meters</param>
		/// <param name="expected">The scale</param>
		[Theory]
		[InlineData(0f, 0.6f)]
		[InlineData(2f, 2f)]
		[InlineData(10f, 3f)]
		public void EffectsSizeWithNoDraugrRadius(float radius, float expected)
		{
			Assert.Equal(expected, ZombieFlies.EffectSize(radius, 0f), 5);
		}
	}
}
