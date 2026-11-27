using System.Collections.Generic;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the rings a cauldron's extensions are tried on around it
	/// </summary>
	public class VillageCookingTests
	{
		/// <summary>
		/// How much a distance can be off from float rounding, in meters
		/// </summary>
		private const float Tolerance = 0.0001f;

		/// <summary>
		/// Checks that the rings run out from the nearest a step apart, ending on the farthest when the step lands on it
		/// </summary>
		[Fact]
		public void RingsRunOutAStepApartToTheFarthest()
		{
			List<float> rings = VillageCooking.RingDistances(2.5f, 4.5f, 0.5f);
			Assert.Equal(5, rings.Count);
			for (int i = 0; i < rings.Count; i++)
			{
				Assert.Equal(2.5f + i * 0.5f, rings[i], Tolerance);
			}
		}

		/// <summary>
		/// Checks that the farthest ring is still tried when stepping out doesn't land on it
		/// </summary>
		[Fact]
		public void FarthestRingIsTriedWhenTheStepMissesIt()
		{
			List<float> rings = VillageCooking.RingDistances(2.5f, 4.2f, 0.5f);
			Assert.Equal(new List<float> { 2.5f, 3f, 3.5f, 4.2f }, rings);
		}

		/// <summary>
		/// Checks that every ring is farther than the one before and none goes past the farthest, even with a step float
		/// rounding can push over it
		/// </summary>
		[Fact]
		public void RingsNeverReachPastTheFarthest()
		{
			List<float> rings = VillageCooking.RingDistances(0.1f, 1f, 0.3f);
			Assert.Equal(1f, rings[rings.Count - 1], Tolerance);
			for (int i = 1; i < rings.Count; i++)
			{
				Assert.True(rings[i] > rings[i - 1]);
				Assert.True(rings[i] <= 1f + Tolerance);
			}
		}

		/// <summary>
		/// Checks that there's one ring when the nearest is the farthest
		/// </summary>
		[Fact]
		public void OneRingWhenNearestIsFarthest()
		{
			Assert.Equal(new List<float> { 2.5f }, VillageCooking.RingDistances(2.5f, 2.5f, 0.5f));
		}

		/// <summary>
		/// Checks that there are no rings when the farthest is nearer than the nearest
		/// </summary>
		[Fact]
		public void NoRingsWhenOutOfReach()
		{
			Assert.Empty(VillageCooking.RingDistances(2.5f, 2f, 0.5f));
		}

		/// <summary>
		/// Checks that the food on the stations is described by what's cooking, done and burnt, leaving out what there's
		/// none of, and as empty when there's nothing on them
		/// </summary>
		/// <param name="cooking">How many pieces are cooking</param>
		/// <param name="done">How many are done</param>
		/// <param name="burnt">How many are burnt</param>
		/// <param name="expected">The description</param>
		[Theory]
		[InlineData(0, 0, 0, "empty")]
		[InlineData(3, 1, 2, "3 cooking, 1 done, 2 burnt")]
		[InlineData(4, 0, 0, "4 cooking")]
		[InlineData(0, 0, 5, "5 burnt")]
		[InlineData(2, 0, 1, "2 cooking, 1 burnt")]
		public void LoadLeavesOutWhatThereIsNoneOf(int cooking, int done, int burnt, string expected)
		{
			Assert.Equal(expected, VillageCooking.DescribeLoad(cooking, done, burnt));
		}
	}
}
