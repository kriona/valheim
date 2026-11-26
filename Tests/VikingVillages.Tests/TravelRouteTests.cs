using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a viking's road to the village it can get to soonest is planned over the world's map
	/// </summary>
	public class TravelRouteTests
	{
		/// <summary>
		/// A raft's speed, for the tests, in meters a second
		/// </summary>
		private const float Raft = 3f;

		/// <summary>
		/// Runs a search to the end
		/// </summary>
		/// <param name="from">Where the road starts</param>
		/// <param name="own">The viking's biome</param>
		/// <param name="boat">How fast its boat goes</param>
		/// <param name="villages">Where the villages stand</param>
		/// <param name="map">The map</param>
		/// <returns>The finished search</returns>
		private static TravelRoute Run(Vector3 from, int own, float boat, List<Vector3> villages, System.Func<float, float, TravelRoute.Cell> map)
		{
			TravelRoute route = new TravelRoute(from, own, boat, villages, map);
			int steps = 0;
			while (!route.Step(1000))
			{
				steps++;
				Assert.True(steps < 2000, "The search never ended");
			}
			return(route);
		}

		/// <summary>
		/// Reads a map of open Meadows everywhere
		/// </summary>
		/// <param name="x">The point's x</param>
		/// <param name="z">The point's z</param>
		/// <returns>The square</returns>
		private static TravelRoute.Cell Meadows(float x, float z)
		{
			return(new TravelRoute.Cell { Open = true, Biome = 0 });
		}

		/// <summary>
		/// Reads a map with a strait of sea at x 300-400, which ends to the north at a z
		/// </summary>
		/// <param name="x">The point's x</param>
		/// <param name="z">The point's z</param>
		/// <param name="end">Where the strait ends to the north</param>
		/// <returns>The square</returns>
		private static TravelRoute.Cell Strait(float x, float z, float end)
		{
			bool sea = (x > 300f && x < 400f && z < end);
			return(new TravelRoute.Cell { Open = true, Sea = sea, Biome = 0 });
		}

		/// <summary>
		/// Checks that each leg of a road is no longer than a road's legs can be
		/// </summary>
		/// <param name="road">The road</param>
		private static void AssertShortLegs(TravelRoad road)
		{
			for (int i = 1; i < road.Count; i++)
			{
				Assert.True(Vector3.Distance(road.Points[i - 1], road.Points[i]) <= 60.5f, "A leg of the road is too long");
			}
		}

		[Fact]
		public void OpenGroundRoadIsNearlyStraight()
		{
			Vector3 from = new Vector3(10f, 0f, 10f);
			Vector3 village = new Vector3(810f, 0f, 10f);
			TravelRoute route = Run(from, 0, Raft, new List<Vector3> { village }, Meadows);
			Assert.Equal(0, route.Found);
			Assert.Equal(from, route.Road.Points[0]);
			Assert.Equal(village, route.Road.Points[route.Road.Count - 1]);
			Assert.InRange(route.Length, 799f, 830f);
			Assert.Equal(route.Length / TravelTime.Speed, route.Seconds, 2);
			Assert.DoesNotContain(TravelLeg.Sea, route.Road.Legs);
			AssertShortLegs(route.Road);
		}

		[Fact]
		public void FarVillagesAreFound()
		{
			TravelRoute route = Run(Vector3.zero, 0, Raft, new List<Vector3> { new Vector3(7000f, 0f, 0f) }, Meadows);
			Assert.Equal(0, route.Found);
		}

		[Fact]
		public void SoonestByRoadIsFoundOverNearestInAStraightLine()
		{
			// A wall of lava at x 300-400 with a gap only far to the north, so the village across it takes longer to reach
			System.Func<float, float, TravelRoute.Cell> map = (float x, float z) => new TravelRoute.Cell { Open = (x < 300f || x > 400f || z > 2000f), Biome = 0 };
			Vector3 from = new Vector3(100f, 0f, 0f);
			Vector3 across = new Vector3(500f, 0f, 0f);
			Vector3 inland = new Vector3(100f, 0f, -1200f);
			TravelRoute route = Run(from, 0, Raft, new List<Vector3> { across, inland }, map);
			Assert.Equal(1, route.Found);
		}

		[Fact]
		public void RoadSailsAcrossTheSea()
		{
			TravelRoute route = Run(new Vector3(100f, 0f, 0f), 0, Raft, new List<Vector3> { new Vector3(600f, 0f, 0f) }, (float x, float z) => Strait(x, z, 100000f));
			Assert.Equal(0, route.Found);
			Assert.Contains(TravelLeg.Sea, route.Road.Legs);
			Assert.True(route.Length < 700f);
			for (int i = 1; i < route.Road.Count; i++)
			{
				float x = route.Road.Points[i].x;
				if (x > 320f && x < 380f)
				{
					Assert.True(route.Road.SeaLeg(i), "The leg to " + route.Road.Points[i] + " should be by sea");
				}
			}
			AssertShortLegs(route.Road);
		}

		[Fact]
		public void SeaLegsTakeTheBoatsTime()
		{
			TravelRoute route = Run(new Vector3(100f, 0f, 0f), 0, Raft, new List<Vector3> { new Vector3(600f, 0f, 0f) }, (float x, float z) => Strait(x, z, 100000f));
			float sea = 0f;
			float land = 0f;
			for (int i = 1; i < route.Road.Count; i++)
			{
				float leg = Vector3.Distance(route.Road.Points[i - 1], route.Road.Points[i]);
				if (route.Road.SeaLeg(i))
				{
					sea += leg;
				}
				else
				{
					land += leg;
				}
			}
			Assert.Equal(land / TravelTime.Speed + sea / Raft, route.Seconds, 1);
		}

		[Fact]
		public void RoadWalksAroundAShortStraitRatherThanSailSlowly()
		{
			// The strait ends just north of the road, so walking around is quicker than a slow raft across
			System.Func<float, float, TravelRoute.Cell> map = (float x, float z) => Strait(x, z, 40f);
			TravelRoute slow = Run(new Vector3(100f, 0f, 0f), 0, 0.5f, new List<Vector3> { new Vector3(600f, 0f, 0f) }, map);
			Assert.DoesNotContain(TravelLeg.Sea, slow.Road.Legs);
		}

		[Fact]
		public void LavaIsNeverCrossed()
		{
			System.Func<float, float, TravelRoute.Cell> map = (float x, float z) => new TravelRoute.Cell { Open = (x < 300f || x > 400f), Biome = 0 };
			TravelRoute route = Run(new Vector3(100f, 0f, 0f), 0, Raft, new List<Vector3> { new Vector3(600f, 0f, 0f) }, (float x, float z) => ((x * x + z * z > 3000f * 3000f) ? new TravelRoute.Cell { Open = false } : map(x, z)));
			Assert.Equal(-1, route.Found);
			Assert.Equal(0, route.Road.Count);
		}

		[Fact]
		public void RoadKeepsOutOfAHarderBiomeWhenThereIsAWayAround()
		{
			// A band of Black Forest at x 300-400, which ends at z 300
			System.Func<float, float, TravelRoute.Cell> map = (float x, float z) => new TravelRoute.Cell { Open = true, Biome = ((x > 300f && x < 400f && z < 300f) ? 1 : 0) };
			TravelRoute route = Run(new Vector3(100f, 0f, 0f), 0, Raft, new List<Vector3> { new Vector3(600f, 0f, 0f) }, map);
			Assert.Equal(0, route.Found);
			foreach (Vector3 point in route.Road.Points)
			{
				Assert.False(point.x > 300f && point.x < 400f && point.z < 300f, "The road crosses the harder biome at " + point);
			}
		}

		[Fact]
		public void RoadCrossesAHarderBiomeWhenThereIsNoWayAround()
		{
			System.Func<float, float, TravelRoute.Cell> map = (float x, float z) => new TravelRoute.Cell { Open = ((x * x + z * z) < 3000f * 3000f), Biome = ((x > 300f && x < 400f) ? 2 : 0) };
			TravelRoute route = Run(new Vector3(100f, 0f, 0f), 0, Raft, new List<Vector3> { new Vector3(600f, 0f, 0f) }, map);
			Assert.Equal(0, route.Found);
		}

		[Fact]
		public void VillageOnTheShoreIsReached()
		{
			// The village's own square is lava, which a road can still end in
			Vector3 village = new Vector3(500f, 0f, 16f);
			System.Func<float, float, TravelRoute.Cell> map = (float x, float z) => new TravelRoute.Cell { Open = (x < 480f), Biome = 0 };
			TravelRoute route = Run(new Vector3(100f, 0f, 16f), 0, Raft, new List<Vector3> { village }, map);
			Assert.Equal(0, route.Found);
		}

		[Fact]
		public void NoVillagesEndsAtOnce()
		{
			TravelRoute route = new TravelRoute(Vector3.zero, 0, Raft, new List<Vector3>(), Meadows);
			Assert.True(route.Done);
			Assert.Equal(-1, route.Found);
		}

		/// <summary>
		/// Runs a search to the end with a boat that may or may not hold up in a burning sea, and portals
		/// </summary>
		/// <param name="from">Where the road starts</param>
		/// <param name="fireproof">Whether the boat holds up in a burning sea</param>
		/// <param name="places">Where the places stand</param>
		/// <param name="map">The map</param>
		/// <param name="portals">The portals, or null</param>
		/// <returns>The finished search</returns>
		private static TravelRoute RunWith(Vector3 from, bool fireproof, List<Vector3> places, System.Func<float, float, TravelRoute.Cell> map, List<TravelRoute.Portal> portals)
		{
			TravelRoute route = new TravelRoute(from, 0, Raft, fireproof, 0f, places, map, portals);
			int steps = 0;
			while (!route.Step(1000))
			{
				steps++;
				Assert.True(steps < 2000, "The search never ended");
			}
			return(route);
		}

		/// <summary>
		/// Reads a map with a wall of lava at x 300-400 and nothing past 3000 m from the middle, so the far side can only be
		/// reached through a portal
		/// </summary>
		/// <param name="x">The point's x</param>
		/// <param name="z">The point's z</param>
		/// <returns>The square</returns>
		private static TravelRoute.Cell Walled(float x, float z)
		{
			return(new TravelRoute.Cell { Open = ((x < 300f || x > 400f) && x * x + z * z < 3000f * 3000f), Biome = 0 });
		}

		[Fact]
		public void RoadGoesThroughAPortalPastWhatCantBeCrossed()
		{
			TravelRoute.Portal portal = new TravelRoute.Portal { Entry = new Vector3(150f, 0f, 0f), Exit = new Vector3(550f, 0f, 0f) };
			TravelRoute route = RunWith(new Vector3(100f, 0f, 0f), false, new List<Vector3> { new Vector3(700f, 0f, 0f) }, Walled, new List<TravelRoute.Portal> { portal });
			Assert.Equal(0, route.Found);
			Assert.Equal(1, route.Road.PortalCount());
			int leg = route.Road.Legs.IndexOf(TravelLeg.Portal);
			Assert.Equal(portal.Entry, route.Road.Points[leg - 1]);
			Assert.Equal(portal.Exit, route.Road.Points[leg]);
			Assert.True(route.Seconds < (50f + 150f) / TravelTime.Speed + TravelTime.PortalSeconds + 10f);
		}

		[Fact]
		public void NoPortalNoRoadPastAWall()
		{
			TravelRoute route = RunWith(new Vector3(100f, 0f, 0f), false, new List<Vector3> { new Vector3(700f, 0f, 0f) }, Walled, null);
			Assert.Equal(-1, route.Found);
		}

		[Fact]
		public void APortalOutOfTheWayIsLeftAlone()
		{
			// Out to the portal and back from its far end takes longer than walking straight there
			TravelRoute.Portal portal = new TravelRoute.Portal { Entry = new Vector3(-600f, 0f, 0f), Exit = new Vector3(600f, 0f, 1200f) };
			TravelRoute route = RunWith(new Vector3(0f, 0f, 0f), false, new List<Vector3> { new Vector3(600f, 0f, 0f) }, Meadows, new List<TravelRoute.Portal> { portal });
			Assert.Equal(0, route.Found);
			Assert.Equal(0, route.Road.PortalCount());
		}

		[Fact]
		public void RoadWalksBetweenTwoPortalsThatStandTogether()
		{
			// The first portal's far end stands right by the second's near end, as at a hub of portals
			List<TravelRoute.Portal> portals = new List<TravelRoute.Portal>
			{
				new TravelRoute.Portal { Entry = new Vector3(150f, 0f, 0f), Exit = new Vector3(-1000f, 0f, 1000f) },
				new TravelRoute.Portal { Entry = new Vector3(-1003f, 0f, 1002f), Exit = new Vector3(550f, 0f, 0f) }
			};
			System.Func<float, float, TravelRoute.Cell> map = (float x, float z) => ((x < -900f) ? new TravelRoute.Cell { Open = true, Biome = 0 } : Walled(x, z));
			// The near side is walled off from the hub too, at z 500
			System.Func<float, float, TravelRoute.Cell> cut = (float x, float z) => ((z > 400f && z < 500f) ? new TravelRoute.Cell { Open = false } : map(x, z));
			TravelRoute route = RunWith(new Vector3(100f, 0f, 0f), false, new List<Vector3> { new Vector3(700f, 0f, 0f) }, cut, portals);
			Assert.Equal(0, route.Found);
			Assert.Equal(2, route.Road.PortalCount());
			for (int i = 1; i < route.Road.Count; i++)
			{
				if (route.Road.PortalLeg(i))
				{
					Assert.False(route.Road.PortalLeg(i - 1), "Two portal legs in a row skip the walk between them");
				}
			}
		}

		/// <summary>
		/// Reads a map with a strait of burning sea at x 300-400, the only way across
		/// </summary>
		/// <param name="x">The point's x</param>
		/// <param name="z">The point's z</param>
		/// <returns>The square</returns>
		private static TravelRoute.Cell BurningStrait(float x, float z)
		{
			bool sea = (x > 300f && x < 400f);
			return(new TravelRoute.Cell { Open = (x * x + z * z < 3000f * 3000f), Sea = sea, Burning = sea, Biome = 0 });
		}

		[Fact]
		public void ABurningSeaIsNeverCrossedInABoatItWouldBurn()
		{
			TravelRoute route = RunWith(new Vector3(100f, 0f, 0f), false, new List<Vector3> { new Vector3(600f, 0f, 0f) }, BurningStrait, null);
			Assert.Equal(-1, route.Found);
		}

		[Fact]
		public void ABurningSeaIsCrossedInABoatMadeForIt()
		{
			TravelRoute route = RunWith(new Vector3(100f, 0f, 0f), true, new List<Vector3> { new Vector3(600f, 0f, 0f) }, BurningStrait, null);
			Assert.Equal(0, route.Found);
			Assert.Contains(TravelLeg.Sea, route.Road.Legs);
		}

		/// <summary>
		/// Runs a search to the end for a viking that swims a distance
		/// </summary>
		/// <param name="swimMeters">How far it swims, in meters</param>
		/// <param name="map">The map</param>
		/// <returns>The finished search</returns>
		private static TravelRoute RunSwimming(float swimMeters, System.Func<float, float, TravelRoute.Cell> map)
		{
			TravelRoute route = new TravelRoute(new Vector3(100f, 0f, 16f), 0, Raft, false, swimMeters, new List<Vector3> { new Vector3(600f, 0f, 16f) }, map, null);
			int steps = 0;
			while (!route.Step(1000))
			{
				steps++;
				Assert.True(steps < 2000, "The search never ended");
			}
			return(route);
		}

		/// <summary>
		/// Reads a map with a lake from x 320 to a far edge, south of a z, and nothing past 3000 m from the middle
		/// </summary>
		/// <param name="x">The point's x</param>
		/// <param name="z">The point's z</param>
		/// <param name="far">The lake's far edge along x</param>
		/// <param name="end">Where the lake ends to the north</param>
		/// <returns>The square</returns>
		private static TravelRoute.Cell Lake(float x, float z, float far, float end)
		{
			return(new TravelRoute.Cell { Open = (x * x + z * z < 3000f * 3000f), Water = (x >= 320f && x < far && z < end), Biome = 0 });
		}

		/// <summary>
		/// Checks whether a point of a road is in a lake
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="far">The lake's far edge along x</param>
		/// <param name="end">Where the lake ends to the north</param>
		/// <returns>True when it is</returns>
		private static bool InLake(Vector3 point, float far, float end)
		{
			return(point.x >= 320f && point.x < far && point.z < end);
		}

		[Theory]
		[InlineData(0f, 1)]
		[InlineData(31f, 1)]
		[InlineData(64f, 2)]
		[InlineData(100f, 3)]
		public void VikingsSwimAsManySquaresAsTheirSwimCovers(float meters, int expected)
		{
			Assert.Equal(expected, TravelRoute.SwimSquares(meters));
		}

		[Fact]
		public void ARiverIsSwumAcrossWhenThereIsNoWayAround()
		{
			TravelRoute route = RunSwimming(0f, (float x, float z) => Lake(x, z, 352f, 100000f));
			Assert.Equal(0, route.Found);
			Assert.Contains(route.Road.Points, (Vector3 point) => InLake(point, 352f, 100000f));
		}

		[Fact]
		public void ALakeWiderThanTheVikingSwimsIsWalkedAround()
		{
			TravelRoute route = RunSwimming(40f, (float x, float z) => Lake(x, z, 416f, 200f));
			Assert.Equal(0, route.Found);
			Assert.DoesNotContain(route.Road.Points, (Vector3 point) => InLake(point, 416f, 200f));
		}

		[Fact]
		public void ALakeWiderThanTheVikingSwimsWithNoWayAroundCantBeCrossed()
		{
			Assert.Equal(-1, RunSwimming(40f, (float x, float z) => Lake(x, z, 416f, 100000f)).Found);
		}

		[Fact]
		public void AStrongSwimmerSwimsAcrossAWideLake()
		{
			TravelRoute route = RunSwimming(100f, (float x, float z) => Lake(x, z, 416f, 100000f));
			Assert.Equal(0, route.Found);
			Assert.Contains(route.Road.Points, (Vector3 point) => InLake(point, 416f, 100000f));
		}

		/// <summary>
		/// Reads a map with deep water off the shore from x 320 to a far edge, as the world's plan leaves it between an
		/// island and the mainland - too deep to wade and deep enough for a boat, but not the sea - and nothing past
		/// 3000 m from the middle
		/// </summary>
		/// <param name="x">The point's x</param>
		/// <param name="z">The point's z</param>
		/// <param name="far">The water's far edge along x</param>
		/// <returns>The square</returns>
		private static TravelRoute.Cell DeepWater(float x, float z, float far)
		{
			bool water = (x >= 320f && x < far);
			return(new TravelRoute.Cell { Open = (x * x + z * z < 3000f * 3000f), Water = water, Deep = water, Biome = 0 });
		}

		[Fact]
		public void DeepWaterTooWideToSwimIsSailed()
		{
			// 160 meters of it, five squares, with no way around, for a viking who swims one
			TravelRoute route = RunSwimming(40f, (float x, float z) => DeepWater(x, z, 480f));
			Assert.Equal(0, route.Found);
			Assert.Contains(TravelLeg.Sea, route.Road.Legs);
		}

		[Fact]
		public void DeepWaterNarrowEnoughToSwimIsSwum()
		{
			// One square of it, rather than getting into a boat
			TravelRoute route = RunSwimming(40f, (float x, float z) => DeepWater(x, z, 352f));
			Assert.Equal(0, route.Found);
			Assert.DoesNotContain(TravelLeg.Sea, route.Road.Legs);
			Assert.Contains(route.Road.Points, (Vector3 point) => point.x >= 320f && point.x < 352f);
		}

		[Fact]
		public void SwimmingTakesLongerThanRunning()
		{
			// A lake right across the road that ends a little way north, so walking around beats swimming straight across
			TravelRoute route = RunSwimming(1000f, (float x, float z) => Lake(x, z, 416f, 96f));
			Assert.Equal(0, route.Found);
			Assert.DoesNotContain(route.Road.Points, (Vector3 point) => InLake(point, 416f, 96f));
		}
	}
}
