using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a village's houses are noted and read back, which of them are its core rather than outposts, and how
	/// far a point is outside it or whether it's in one of its houses
	/// </summary>
	public class VillageTests
	{
		/// <summary>
		/// Makes a house
		/// </summary>
		/// <param name="x">Where its middle is east</param>
		/// <param name="z">Where its middle is north</param>
		/// <param name="halfSize">How far its walls are from its middle</param>
		/// <returns>The house</returns>
		private static VillagePaths.House House(float x, float z, float halfSize)
		{
			return(new VillagePaths.House { Center = new Vector3(x, 0f, z), HalfSize = halfSize });
		}

		/// <summary>
		/// Makes a village with zones, a middle and a reach around them, and no generator
		/// </summary>
		/// <param name="zones">Its zones</param>
		/// <param name="middle">Its middle</param>
		/// <param name="radius">How far from its middle its zones reach</param>
		/// <returns>The village</returns>
		private static Village MakeVillage(List<Village.Zone> zones, Vector3 middle, float radius)
		{
			Village village = new Village();
			village.Zones = zones;
			village.Middle = middle;
			village.Radius = radius;
			return(village);
		}

		/// <summary>
		/// Writes houses and reads them back
		/// </summary>
		/// <param name="houses">The houses</param>
		/// <returns>The houses read back</returns>
		private static List<VillagePaths.House> RoundTrip(List<VillagePaths.House> houses)
		{
			return(VillagePaths.ReadHouses(Village.WriteHouses(houses)));
		}

		/// <summary>
		/// Checks that a village with no houses reads back with none
		/// </summary>
		[Fact]
		public void ReadsBackNoHouses()
		{
			Assert.Empty(RoundTrip(new List<VillagePaths.House>()));
		}

		/// <summary>
		/// Checks that one house reads back with its middle, height and all, and its half size
		/// </summary>
		[Fact]
		public void ReadsBackOneHouse()
		{
			List<VillagePaths.House> read = RoundTrip(new List<VillagePaths.House> { new VillagePaths.House { Center = new Vector3(12.5f, 30.25f, -7.75f), HalfSize = 6f } });
			Assert.Single(read);
			Assert.Equal(12.5f, read[0].Center.x);
			Assert.Equal(30.25f, read[0].Center.y);
			Assert.Equal(-7.75f, read[0].Center.z);
			Assert.Equal(6f, read[0].HalfSize);
		}

		/// <summary>
		/// Checks that three houses read back in the order they were written, each with its own middle and half size
		/// </summary>
		[Fact]
		public void ReadsBackThreeHousesInOrder()
		{
			List<VillagePaths.House> houses = new List<VillagePaths.House>
			{
				new VillagePaths.House { Center = new Vector3(-100f, 22f, 340.5f), HalfSize = 4.5f },
				new VillagePaths.House { Center = new Vector3(0f, 0f, 0f), HalfSize = 0f },
				new VillagePaths.House { Center = new Vector3(2048.125f, -3f, -9000f), HalfSize = 11.25f }
			};
			List<VillagePaths.House> read = RoundTrip(houses);
			Assert.Equal(3, read.Count);
			for (int i = 0; i < houses.Count; i++)
			{
				Assert.Equal(houses[i].Center.x, read[i].Center.x);
				Assert.Equal(houses[i].Center.y, read[i].Center.y);
				Assert.Equal(houses[i].Center.z, read[i].Center.z);
				Assert.Equal(houses[i].HalfSize, read[i].HalfSize);
			}
		}

		/// <summary>
		/// Checks that the gap between two houses is measured wall to wall across the ground, and is 0 when they touch
		/// or overlap
		/// </summary>
		[Fact]
		public void MeasuresTheGapWallToWall()
		{
			Assert.Equal(13f, Village.Gap(House(0f, 0f, 3f), House(20f, 0f, 4f)), 3);
			Assert.Equal(13f, Village.Gap(House(0f, 0f, 3f), new VillagePaths.House { Center = new Vector3(20f, 50f, 0f), HalfSize = 4f }), 3);
			Assert.Equal(0f, Village.Gap(House(0f, 0f, 3f), House(7f, 0f, 4f)), 3);
			Assert.Equal(0f, Village.Gap(House(0f, 0f, 5f), House(2f, 0f, 5f)), 3);
		}

		/// <summary>
		/// Checks that the core reaches out from a farmhouse along a chain of houses each within 15 meters of the last,
		/// wall to wall - the gap right at the limit counting - leaving out an outpost farther off, whatever order the
		/// houses are listed in
		/// </summary>
		[Fact]
		public void CoreChainsOutFromTheFarmhouse()
		{
			VillagePaths.House farmhouse = House(0f, 0f, 5f);
			VillagePaths.House near = House(25f, 0f, 5f);
			VillagePaths.House chained = House(50f, 0f, 5f);
			VillagePaths.House outpost = House(100f, 0f, 5f);
			List<VillagePaths.House> core = Village.Core(new List<VillagePaths.House> { farmhouse }, new List<VillagePaths.House> { outpost, chained, near, farmhouse });
			Assert.Equal(3, core.Count);
			Assert.Contains(core, (VillagePaths.House house) => house.Center.x == 0f);
			Assert.Contains(core, (VillagePaths.House house) => house.Center.x == 25f);
			Assert.Contains(core, (VillagePaths.House house) => house.Center.x == 50f);
			Assert.DoesNotContain(core, (VillagePaths.House house) => house.Center.x == 100f);
		}

		/// <summary>
		/// Checks that a house just past 15 meters from the farmhouse's walls is an outpost, as is one that's only near
		/// that outpost
		/// </summary>
		[Fact]
		public void CoreLeavesOutHousesPastTheGap()
		{
			VillagePaths.House farmhouse = House(0f, 0f, 5f);
			List<VillagePaths.House> houses = new List<VillagePaths.House> { farmhouse, House(25.5f, 0f, 5f), House(40f, 0f, 5f) };
			List<VillagePaths.House> core = Village.Core(new List<VillagePaths.House> { farmhouse }, houses);
			Assert.Single(core);
			Assert.Equal(0f, core[0].Center.x);
		}

		/// <summary>
		/// Checks that a farmhouse is matched to a house of the village within a meter of its middle
		/// </summary>
		[Fact]
		public void CoreMatchesAFarmhouseWithinAMeter()
		{
			List<VillagePaths.House> houses = new List<VillagePaths.House> { House(0f, 0f, 5f), House(100f, 0f, 5f) };
			List<VillagePaths.House> core = Village.Core(new List<VillagePaths.House> { House(0.3f, 0.4f, 5f) }, houses);
			Assert.Single(core);
			Assert.Equal(0f, core[0].Center.x);
		}

		/// <summary>
		/// Checks that every house is in the core when the village has no farmhouses, or none of them is among its
		/// houses
		/// </summary>
		[Fact]
		public void CoreIsEveryHouseWithNoFarmhouses()
		{
			List<VillagePaths.House> houses = new List<VillagePaths.House> { House(0f, 0f, 5f), House(100f, 0f, 5f), House(300f, 0f, 5f) };
			Assert.Equal(3, Village.Core(new List<VillagePaths.House>(), houses).Count);
			Assert.Equal(3, Village.Core(new List<VillagePaths.House> { House(500f, 0f, 5f) }, houses).Count);
		}

		/// <summary>
		/// Checks that a point on a zone's edge is 0 outside the village and inside it, however high it is, and a point
		/// past the edge is as far outside as it is past it
		/// </summary>
		[Fact]
		public void EdgeOfAZoneIsInside()
		{
			Village village = MakeVillage(new List<Village.Zone> { new Village.Zone { Center = new Vector3(0f, 0f, 0f), Radius = 10f } }, new Vector3(0f, 0f, 0f), 10f);
			Assert.Equal(0f, village.DistanceOutside(new Vector3(10f, 0f, 0f)), 3);
			Assert.True(village.Contains(new Vector3(10f, 0f, 0f)));
			Assert.True(village.Contains(new Vector3(6f, 50f, 8f)));
			Assert.Equal(3f, village.DistanceOutside(new Vector3(13f, 0f, 0f)), 3);
			Assert.False(village.Contains(new Vector3(13f, 0f, 0f)));
		}

		/// <summary>
		/// Checks that a point in either of two zones is inside the village, and one between them is as far outside as
		/// it is from the nearer zone's edge
		/// </summary>
		[Fact]
		public void InsideEitherOfTwoZones()
		{
			List<Village.Zone> zones = new List<Village.Zone>
			{
				new Village.Zone { Center = new Vector3(0f, 0f, 0f), Radius = 10f },
				new Village.Zone { Center = new Vector3(40f, 0f, 0f), Radius = 5f }
			};
			Village village = MakeVillage(zones, new Vector3(20f, 0f, 0f), 30f);
			Assert.True(village.Contains(new Vector3(38f, 0f, 0f)));
			Assert.Equal(0f, village.DistanceOutside(new Vector3(38f, 0f, 0f)), 3);
			Assert.True(village.Contains(new Vector3(-5f, 0f, 5f)));
			Assert.Equal(10f, village.DistanceOutside(new Vector3(25f, 0f, 0f)), 3);
			Assert.False(village.Contains(new Vector3(25f, 0f, 0f)));
			Assert.Equal(10f, village.DistanceOutside(new Vector3(20f, 0f, 0f)), 3);
		}

		/// <summary>
		/// Checks that a village with no zones measures from its middle and reach instead
		/// </summary>
		[Fact]
		public void NoZonesMeasuresFromTheMiddle()
		{
			Village village = MakeVillage(new List<Village.Zone>(), new Vector3(100f, 0f, 100f), 20f);
			Assert.Equal(10f, village.DistanceOutside(new Vector3(100f, 0f, 130f)), 3);
			Assert.False(village.Contains(new Vector3(100f, 0f, 130f)));
			Assert.Equal(0f, village.DistanceOutside(new Vector3(120f, 0f, 100f)), 3);
			Assert.True(village.Contains(new Vector3(110f, 0f, 100f)));
		}

		/// <summary>
		/// Checks that a point is in a house within its square walls and the padding past them, right at the padding
		/// counting, and the corners too
		/// </summary>
		[Fact]
		public void InHousePaddingCounts()
		{
			VillagePaths.House house = House(0f, 0f, 4f);
			Assert.True(Village.InHouse(house, new Vector3(4f, 0f, 0f), 0f));
			Assert.True(Village.InHouse(house, new Vector3(5f, 0f, 0f), 1f));
			Assert.True(Village.InHouse(house, new Vector3(-5f, 0f, 5f), 1f));
			Assert.False(Village.InHouse(house, new Vector3(5.5f, 0f, 0f), 1f));
			Assert.False(Village.InHouse(house, new Vector3(0f, 0f, -4.5f), 0f));
		}

		/// <summary>
		/// Checks that a point is in the village's houses when it's in any one of them, and in none when it has none
		/// </summary>
		[Fact]
		public void InAnyOfTheVillagesHouses()
		{
			Village village = MakeVillage(new List<Village.Zone>(), new Vector3(0f, 0f, 0f), 0f);
			Assert.False(village.InHouse(new Vector3(0f, 0f, 0f), 1f));
			village.Houses = new List<VillagePaths.House> { House(0f, 0f, 4f), House(30f, 0f, 3f) };
			Assert.True(village.InHouse(new Vector3(34f, 0f, -2f), 1f));
			Assert.False(village.InHouse(new Vector3(15f, 0f, 0f), 1f));
		}
	}
}
