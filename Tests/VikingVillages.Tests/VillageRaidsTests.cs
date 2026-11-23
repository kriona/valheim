using UnityEngine;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests when a player counts as in a raid's area, the way the game judges
	/// </summary>
	public class VillageRaidsTests
	{
		/// <summary>
		/// Checks that a point counts as in a raid's area only when it's nearer the raid's middle than its range, seen
		/// from above
		/// </summary>
		/// <param name="x">Where the point stands across from the raid's middle</param>
		/// <param name="z">Where it stands deep from it</param>
		/// <param name="expected">Whether it's in the area</param>
		[Theory]
		[InlineData(0f, 0f, true)]
		[InlineData(49.9f, 0f, true)]
		[InlineData(50f, 0f, false)]
		[InlineData(30f, 40f, false)]
		[InlineData(30f, 39.9f, true)]
		[InlineData(-60f, 0f, false)]
		public void AreaReachesItsRange(float x, float z, bool expected)
		{
			Assert.Equal(expected, VillageRaids.InArea(new Vector3(100f + x, 20f, 200f + z), new Vector3(100f, 0f, 200f), 50f));
		}

		/// <summary>
		/// Checks that a point up to 3000 meters high counts, and one higher, out of the world in a dungeon, doesn't
		/// </summary>
		/// <param name="y">How high the point stands</param>
		/// <param name="expected">Whether it's in the area</param>
		[Theory]
		[InlineData(-50f, true)]
		[InlineData(2999f, true)]
		[InlineData(3000f, true)]
		[InlineData(3000.1f, false)]
		[InlineData(5000f, false)]
		public void PointsOutOfTheWorldAreOutside(float y, bool expected)
		{
			Assert.Equal(expected, VillageRaids.InArea(new Vector3(10f, y, 0f), Vector3.zero, 50f));
		}

		/// <summary>
		/// Checks that only the point's height counts, not the raid middle's
		/// </summary>
		[Fact]
		public void RaidsHeightIsIgnored()
		{
			Assert.True(VillageRaids.InArea(new Vector3(10f, 100f, 0f), new Vector3(0f, 5000f, 0f), 50f));
		}
	}
}
