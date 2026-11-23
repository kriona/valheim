using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests how names are simplified before vikings' spawns are matched to them, how a farm finds the farms its village
	/// is joined to, and where a farm's beehive stands
	/// </summary>
	public class VikingSpawnsTests
	{
		/// <summary>
		/// How far from its planned spot a farm's generator can stand in the tests, in meters
		/// </summary>
		private const float Reach = 16f;

		/// <summary>
		/// Makes a farm the world plans
		/// </summary>
		/// <param name="x">Where it stands across</param>
		/// <param name="z">Where it stands deep</param>
		/// <param name="placed">Whether its zone has been generated</param>
		/// <returns>The farm</returns>
		private static VikingSpawns.PlannedFarm Farm(float x, float z, bool placed = true)
		{
			return(new VikingSpawns.PlannedFarm { Position = new Vector3(x, 0f, z), Placed = placed });
		}

		/// <summary>
		/// Makes a farmhouse
		/// </summary>
		/// <param name="x">Where its middle stands across</param>
		/// <param name="y">How high its middle stands</param>
		/// <param name="z">Where its middle stands deep</param>
		/// <param name="halfSize">How far its walls reach from its middle, in meters</param>
		/// <returns>The farmhouse</returns>
		private static VikingSpawns.Farmhouse House(float x, float y, float z, float halfSize)
		{
			return(new VikingSpawns.Farmhouse { Center = new Vector3(x, y, z), HalfSize = halfSize });
		}

		/// <summary>
		/// Checks that spaces and underscores are taken out and the rest is lowercased
		/// </summary>
		/// <param name="name">The name</param>
		/// <param name="expected">The simplified name</param>
		[Theory]
		[InlineData("Dark Meadows", "darkmeadows")]
		[InlineData("Ruin_BlackForest", "ruinblackforest")]
		[InlineData("  Mixed _Case_ ", "mixedcase")]
		[InlineData("plains", "plains")]
		[InlineData("", "")]
		public void SimplifyLowercasesAndJoins(string name, string expected)
		{
			Assert.Equal(expected, VikingSpawns.Simplify(name));
		}

		/// <summary>
		/// Checks that a missing name simplifies to an empty one
		/// </summary>
		[Fact]
		public void SimplifyTreatsNullAsEmpty()
		{
			Assert.Equal("", VikingSpawns.Simplify(null));
		}

		/// <summary>
		/// Checks that a farm with no others planned near it is its own planned spot where its generator stands, with no
		/// farms to join
		/// </summary>
		[Fact]
		public void NoFarmsNearLeavesTheGeneratorsSpot()
		{
			Vector3 generator = new Vector3(5f, 30f, 5f);
			Assert.True(VikingSpawns.NearbyFarms(generator, Reach, new List<VikingSpawns.PlannedFarm>(), out Vector3 self, out List<Vector3> farms));
			Assert.Equal(generator, self);
			Assert.Empty(farms);
		}

		/// <summary>
		/// Checks that the farm nearest the generator is its own, and the others near it are the ones to join
		/// </summary>
		[Fact]
		public void NearestFarmIsItsOwn()
		{
			List<VikingSpawns.PlannedFarm> planned = new List<VikingSpawns.PlannedFarm> { Farm(10f, 0f), Farm(3f, 4f), Farm(80f, 0f) };
			Assert.True(VikingSpawns.NearbyFarms(Vector3.zero, Reach, planned, out Vector3 self, out List<Vector3> farms));
			Assert.Equal(new Vector3(3f, 0f, 4f), self);
			Assert.Equal(new List<Vector3> { new Vector3(10f, 0f, 0f), new Vector3(80f, 0f, 0f) }, farms);
		}

		/// <summary>
		/// Checks that a farm exactly 100 meters from the farm's own is joined to it, whatever its height, and one a
		/// little farther isn't
		/// </summary>
		[Fact]
		public void FarmAtTheLinkRangeIsJoined()
		{
			VikingSpawns.PlannedFarm high = Farm(100f, 0f);
			high.Position.y = 50f;
			List<VikingSpawns.PlannedFarm> planned = new List<VikingSpawns.PlannedFarm> { Farm(0f, 0f), high, Farm(0f, 100.1f) };
			Assert.True(VikingSpawns.NearbyFarms(Vector3.zero, Reach, planned, out Vector3 self, out List<Vector3> farms));
			Assert.Equal(Vector3.zero, self);
			Assert.Equal(new List<Vector3> { high.Position }, farms);
		}

		/// <summary>
		/// Checks that a farm whose zone hasn't been generated yet holds the village up while it's near enough to look at
		/// </summary>
		[Fact]
		public void UnplacedFarmNearHoldsUp()
		{
			List<VikingSpawns.PlannedFarm> planned = new List<VikingSpawns.PlannedFarm> { Farm(5f, 0f), Farm(60f, 0f, false) };
			Assert.False(VikingSpawns.NearbyFarms(Vector3.zero, Reach, planned, out Vector3 _, out List<Vector3> _));
		}

		/// <summary>
		/// Checks that farms beyond the link range and the generator's reach of it aren't looked at - not joined, even
		/// within the link range of the farm's own, and not holding the village up when they haven't been generated
		/// </summary>
		[Fact]
		public void FarmsBeyondTheGeneratorsReachAreNotLookedAt()
		{
			List<VikingSpawns.PlannedFarm> planned = new List<VikingSpawns.PlannedFarm> { Farm(50f, 0f), Farm(130f, 0f), Farm(-200f, 0f, false) };
			Assert.True(VikingSpawns.NearbyFarms(Vector3.zero, Reach, planned, out Vector3 self, out List<Vector3> farms));
			Assert.Equal(new Vector3(50f, 0f, 0f), self);
			Assert.Empty(farms);
		}

		/// <summary>
		/// Checks that a beehive stands outside the farmhouse nearest the wild one, on its side and at its middle's height
		/// </summary>
		[Fact]
		public void HiveStandsOutsideTheNearestHouse()
		{
			List<VikingSpawns.Farmhouse> houses = new List<VikingSpawns.Farmhouse> { House(0f, 0f, 0f, 4f), House(20f, 1f, 0f, 5f) };
			Vector3 spot = VikingSpawns.HiveSpot(new Vector3(18f, 3f, 0f), houses, out Vector3 outward);
			Assert.Equal(13f, spot.x, 4);
			Assert.Equal(1f, spot.y, 4);
			Assert.Equal(0f, spot.z, 4);
			Assert.Equal(-1f, outward.x, 4);
			Assert.Equal(0f, outward.y, 4);
		}

		/// <summary>
		/// Checks that a beehive stands 2 meters past the farmhouse's walls, however high the wild one hung
		/// </summary>
		[Fact]
		public void HiveStandsClearOfTheWalls()
		{
			List<VikingSpawns.Farmhouse> houses = new List<VikingSpawns.Farmhouse> { House(0f, 0f, 0f, 4f) };
			Vector3 spot = VikingSpawns.HiveSpot(new Vector3(3f, 10f, 4f), houses, out Vector3 _);
			Assert.Equal(6f, Utils.DistanceXZ(spot, Vector3.zero), 4);
			Assert.Equal(3.6f, spot.x, 4);
			Assert.Equal(4.8f, spot.z, 4);
			Assert.Equal(0f, spot.y, 4);
		}

		/// <summary>
		/// Checks that a wild beehive hanging right over the farmhouse's middle has its beehive stand out along the
		/// world's forward axis
		/// </summary>
		/// <param name="y">How high the wild beehive hung</param>
		/// <param name="z">How far off the middle it hung, too little to tell a side</param>
		[Theory]
		[InlineData(5f, 0f)]
		[InlineData(2.5f, 0.05f)]
		public void HiveOverTheMiddleFallsBackOnForward(float y, float z)
		{
			List<VikingSpawns.Farmhouse> houses = new List<VikingSpawns.Farmhouse> { House(10f, 0f, 10f, 3f) };
			Vector3 spot = VikingSpawns.HiveSpot(new Vector3(10f, y, 10f + z), houses, out Vector3 outward);
			Assert.Equal(Vector3.forward, outward);
			Assert.Equal(10f, spot.x, 4);
			Assert.Equal(15f, spot.z, 4);
		}
	}
}
