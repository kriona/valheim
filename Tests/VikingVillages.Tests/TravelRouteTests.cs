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
			Assert.DoesNotContain(true, route.Road.Sea);
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
			Assert.Contains(true, route.Road.Sea);
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
			Assert.DoesNotContain(true, slow.Road.Sea);
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
	}
}
