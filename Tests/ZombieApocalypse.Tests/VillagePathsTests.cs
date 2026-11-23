using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests how a village's paths are planned - which houses they join, which of two neighboring villages lays the path
	/// between them, how its noted houses are read, where along a path it's painted and how many floors its bridges take
	/// </summary>
	public class VillagePathsTests
	{
		/// <summary>
		/// How far apart a path's painted spots are in the tests, in meters - the village's own
		/// </summary>
		private const float Spacing = 1.5f;

		/// <summary>
		/// Makes houses with their middles at some points
		/// </summary>
		/// <param name="middles">The houses' middles</param>
		/// <returns>The houses</returns>
		private static List<VillagePaths.House> Houses(params Vector3[] middles)
		{
			List<VillagePaths.House> houses = new List<VillagePaths.House>();
			foreach (Vector3 middle in middles)
			{
				houses.Add(new VillagePaths.House { Center = middle, HalfSize = 4f });
			}
			return(houses);
		}

		/// <summary>
		/// Checks whether some lines join every house to the first, one way or another
		/// </summary>
		/// <param name="edges">The lines, by the houses' indices</param>
		/// <param name="count">How many houses there are</param>
		/// <returns>True when they do</returns>
		private static bool ReachesAll(List<KeyValuePair<int, int>> edges, int count)
		{
			HashSet<int> reached = new HashSet<int> { 0 };
			bool grew = true;
			while (grew)
			{
				grew = false;
				foreach (KeyValuePair<int, int> edge in edges)
				{
					if (reached.Contains(edge.Key) != reached.Contains(edge.Value))
					{
						reached.Add(edge.Key);
						reached.Add(edge.Value);
						grew = true;
					}
				}
			}
			return(reached.Count == count);
		}

		/// <summary>
		/// Makes a straight path east along the ground, one point a meter
		/// </summary>
		/// <param name="heights">Each point's height, from west to east</param>
		/// <returns>The path</returns>
		private static List<Vector3> East(params float[] heights)
		{
			List<Vector3> path = new List<Vector3>();
			for (int i = 0; i < heights.Length; i++)
			{
				path.Add(new Vector3(i, heights[i], 0f));
			}
			return(path);
		}

		/// <summary>
		/// Checks that no houses, or a single house, need no paths
		/// </summary>
		[Fact]
		public void JoinsNoHousesOrOneWithNothing()
		{
			Assert.Empty(VillagePaths.SpanningTree(Houses()));
			Assert.Empty(VillagePaths.SpanningTree(Houses(new Vector3(3f, 0f, 3f))));
		}

		/// <summary>
		/// Checks that three houses in a line are joined by the two short paths between neighbors, not the long one
		/// from end to end
		/// </summary>
		[Fact]
		public void JoinsHousesInALineToTheirNeighbors()
		{
			List<KeyValuePair<int, int>> edges = VillagePaths.SpanningTree(Houses(new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(4f, 0f, 0f)));
			Assert.Equal(new List<KeyValuePair<int, int>> { new KeyValuePair<int, int>(0, 2), new KeyValuePair<int, int>(2, 1) }, edges);
		}

		/// <summary>
		/// Checks that four houses on a square's corners are joined by three of its sides, never across it
		/// </summary>
		[Fact]
		public void JoinsASquareAlongThreeSides()
		{
			List<VillagePaths.House> houses = Houses(new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f), new Vector3(0f, 0f, 10f));
			List<KeyValuePair<int, int>> edges = VillagePaths.SpanningTree(houses);
			Assert.Equal(3, edges.Count);
			Assert.All(edges, (KeyValuePair<int, int> edge) => Assert.Equal(10f, Village.FlatDistance(houses[edge.Key].Center, houses[edge.Value].Center), 3));
			Assert.True(ReachesAll(edges, houses.Count));
		}

		/// <summary>
		/// Checks that how far apart houses are is measured across the ground, so a house high on a hill is still
		/// the nearest
		/// </summary>
		[Fact]
		public void MeasuresAcrossTheGround()
		{
			List<KeyValuePair<int, int>> edges = VillagePaths.SpanningTree(Houses(new Vector3(0f, 0f, 0f), new Vector3(5f, 100f, 0f), new Vector3(8f, 0f, 0f)));
			Assert.Equal(new List<KeyValuePair<int, int>> { new KeyValuePair<int, int>(0, 1), new KeyValuePair<int, int>(1, 2) }, edges);
		}

		/// <summary>
		/// Checks that many houses are all joined, by one fewer path than there are houses, each house reached once
		/// </summary>
		[Fact]
		public void JoinsEveryHouseOnce()
		{
			List<Vector3> middles = new List<Vector3>();
			for (int i = 0; i < 12; i++)
			{
				middles.Add(new Vector3((i * 7) % 31, 0f, (i * 13) % 17));
			}
			List<VillagePaths.House> houses = Houses(middles.ToArray());
			List<KeyValuePair<int, int>> edges = VillagePaths.SpanningTree(houses);
			Assert.Equal(houses.Count - 1, edges.Count);
			Assert.True(ReachesAll(edges, houses.Count));
			HashSet<int> reached = new HashSet<int>();
			foreach (KeyValuePair<int, int> edge in edges)
			{
				Assert.NotEqual(0, edge.Value);
				Assert.True(reached.Add(edge.Value), "House " + edge.Value + " reached twice");
			}
		}

		/// <summary>
		/// Checks that a house whose middle isn't a number is left out rather than joined by a path to no house
		/// </summary>
		[Fact]
		public void LeavesOutAHouseWithNoMiddle()
		{
			List<KeyValuePair<int, int>> edges = VillagePaths.SpanningTree(Houses(new Vector3(0f, 0f, 0f), new Vector3(float.NaN, 0f, 0f), new Vector3(5f, 0f, 0f)));
			Assert.Equal(new List<KeyValuePair<int, int>> { new KeyValuePair<int, int>(0, 2) }, edges);
			Assert.Empty(VillagePaths.SpanningTree(Houses(new Vector3(float.NaN, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(5f, 0f, 0f))));
		}

		/// <summary>
		/// Checks which of two villages comes first - the one farther west, or when they're within half a meter east
		/// to west, the one farther south
		/// </summary>
		/// <param name="farmX">The first village's place east</param>
		/// <param name="farmZ">Its place north</param>
		/// <param name="otherX">The other village's place east</param>
		/// <param name="otherZ">Its place north</param>
		/// <param name="expected">Whether the first comes first</param>
		[Theory]
		[InlineData(0f, 0f, 10f, 0f, true)]
		[InlineData(10f, 0f, 0f, 0f, false)]
		[InlineData(0f, 100f, 10f, -100f, true)]
		[InlineData(0.4f, 0f, 0f, 5f, true)]
		[InlineData(0f, 5f, 0.4f, 0f, false)]
		[InlineData(0.5f, 0f, 0f, 5f, true)]
		[InlineData(0.6f, 0f, 0f, 5f, false)]
		[InlineData(3f, 7f, 3f, 7f, false)]
		public void PutsTheWesternVillageFirst(float farmX, float farmZ, float otherX, float otherZ, bool expected)
		{
			Assert.Equal(expected, VillagePaths.ComesFirst(new Vector3(farmX, 0f, farmZ), new Vector3(otherX, 0f, otherZ)));
		}

		/// <summary>
		/// Checks that two villages never both come first, so the path between them is laid once, and one always does
		/// unless they're within half a meter east to west at the same place north
		/// </summary>
		[Fact]
		public void PutsOneOfTwoVillagesFirst()
		{
			Vector3 farm = new Vector3(100f, 30f, 200f);
			float[] eastOffsets = new[] { -2f, -0.6f, -0.5f, -0.3f, 0f, 0.3f, 0.5f, 0.6f, 2f };
			float[] northOffsets = new[] { -3f, 0f, 3f };
			foreach (float dx in eastOffsets)
			{
				foreach (float dz in northOffsets)
				{
					Vector3 other = farm + new Vector3(dx, 5f, dz);
					bool first = VillagePaths.ComesFirst(farm, other);
					bool second = VillagePaths.ComesFirst(other, farm);
					Assert.False(first && second, "Both come first at " + dx + ", " + dz);
					if (Mathf.Abs(dx) > 0.5f || dz != 0f)
					{
						Assert.True(first || second, "Neither comes first at " + dx + ", " + dz);
					}
				}
			}
		}

		/// <summary>
		/// Checks that nothing noted reads as no houses, rather than failing
		/// </summary>
		[Fact]
		public void ReadsNoHousesFromNothing()
		{
			Assert.Empty(VillagePaths.ReadHouses(null));
			Assert.Empty(VillagePaths.ReadHouses(new byte[0]));
			Assert.Empty(VillagePaths.ReadHouses(new byte[] { 1, 0, 0 }));
		}

		/// <summary>
		/// Checks that what's noted for a village a player started, with no farmhouses, reads as no houses
		/// </summary>
		[Fact]
		public void ReadsAFoundedVillageAsNoHouses()
		{
			Assert.Empty(VillagePaths.ReadHouses(Village.WriteHouses(new List<VillagePaths.House>())));
			ZPackage pkg = new ZPackage();
			pkg.Write(0);
			Assert.Empty(VillagePaths.ReadHouses(pkg.GetArray()));
		}

		/// <summary>
		/// Checks that houses written by hand - a count, then each one's middle and half size - read back in order,
		/// with their doors not found yet
		/// </summary>
		[Fact]
		public void ReadsNotedHouses()
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(2);
			pkg.Write(new Vector3(1f, 2f, 3f));
			pkg.Write(4f);
			pkg.Write(new Vector3(-5f, 0f, 7.5f));
			pkg.Write(6.5f);
			List<VillagePaths.House> houses = VillagePaths.ReadHouses(pkg.GetArray());
			Assert.Equal(2, houses.Count);
			Assert.Equal(new Vector3(1f, 2f, 3f), houses[0].Center);
			Assert.Equal(4f, houses[0].HalfSize);
			Assert.Equal(new Vector3(-5f, 0f, 7.5f), houses[1].Center);
			Assert.Equal(6.5f, houses[1].HalfSize);
			Assert.Null(houses[0].Doors);
		}

		/// <summary>
		/// Checks that a list cut short reads only the houses fully written, and a count below zero none
		/// </summary>
		[Fact]
		public void ReadsOnlyTheHousesFullyWritten()
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(3);
			pkg.Write(new Vector3(1f, 2f, 3f));
			pkg.Write(4f);
			pkg.Write(new Vector3(9f, 9f, 9f));
			List<VillagePaths.House> houses = VillagePaths.ReadHouses(pkg.GetArray());
			Assert.Single(houses);
			Assert.Equal(new Vector3(1f, 2f, 3f), houses[0].Center);
			ZPackage negative = new ZPackage();
			negative.Write(-1);
			negative.Write(new Vector3(1f, 2f, 3f));
			negative.Write(4f);
			Assert.Empty(VillagePaths.ReadHouses(negative.GetArray()));
		}

		/// <summary>
		/// Checks that an empty path is painted nowhere, and a path of one point at it
		/// </summary>
		[Fact]
		public void PaintsAShortPathAtItsPoints()
		{
			Assert.Empty(VillagePaths.PaintSteps(new List<Vector3>(), Spacing, 0f));
			Assert.Equal(new List<int> { 0 }, VillagePaths.PaintSteps(East(1f), Spacing, 0f));
		}

		/// <summary>
		/// Checks that a path is painted at its first point, then each point at least the spacing on from the last
		/// painted, and at its last point however near the one before
		/// </summary>
		[Fact]
		public void PaintsAPathSpacingApart()
		{
			Assert.Equal(new List<int> { 0, 2, 4, 6 }, VillagePaths.PaintSteps(East(1f, 1f, 1f, 1f, 1f, 1f, 1f), Spacing, 0f));
			Assert.Equal(new List<int> { 0, 2, 4, 5 }, VillagePaths.PaintSteps(East(1f, 1f, 1f, 1f, 1f, 1f), Spacing, 0f));
		}

		/// <summary>
		/// Checks that the spacing counts uneven steps along the path, and only across the ground, so a climb adds
		/// nothing to it
		/// </summary>
		[Fact]
		public void SpacesPaintAcrossTheGround()
		{
			List<Vector3> uneven = new List<Vector3> { new Vector3(0f, 1f, 0f), new Vector3(0.5f, 1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(1.6f, 1f, 0f), new Vector3(3f, 1f, 0f) };
			Assert.Equal(new List<int> { 0, 3, 4 }, VillagePaths.PaintSteps(uneven, Spacing, 0f));
			Assert.Equal(new List<int> { 0, 2, 3 }, VillagePaths.PaintSteps(East(1f, 5f, 1f, 1f), Spacing, 0f));
		}

		/// <summary>
		/// Checks that the points under water are left unpainted, the distance across them still counting, so the
		/// first point out of the water is painted - and a last point under water isn't
		/// </summary>
		[Fact]
		public void LeavesTheWaterUnpainted()
		{
			Assert.Equal(new List<int> { 0, 4, 6 }, VillagePaths.PaintSteps(East(1f, 1f, -1f, -1f, 1f, 1f, 1f), Spacing, 0f));
			Assert.Equal(new List<int> { 0, 2 }, VillagePaths.PaintSteps(East(1f, 1f, 1f, -1f), Spacing, 0f));
			Assert.Equal(new List<int> { 2, 4 }, VillagePaths.PaintSteps(East(-1f, -1f, 1f, 1f, 1f), Spacing, 0f));
			Assert.Empty(VillagePaths.PaintSteps(East(-1f, -1f), Spacing, 0f));
		}

		/// <summary>
		/// Checks how many floors a bridge is laid of - none when it's too short to build, at least one on each bank,
		/// and one more for each 1.9 meters it reaches
		/// </summary>
		/// <param name="length">How far the bridge reaches, in meters</param>
		/// <param name="expected">The floors</param>
		[Theory]
		[InlineData(0f, 0)]
		[InlineData(0.05f, 0)]
		[InlineData(0.1f, 2)]
		[InlineData(1.9f, 2)]
		[InlineData(2f, 3)]
		[InlineData(3.8f, 3)]
		[InlineData(3.9f, 4)]
		[InlineData(14f, 9)]
		public void LaysABridgeOfFloors(float length, int expected)
		{
			Assert.Equal(expected, VillagePaths.BridgeFloors(length));
		}

		/// <summary>
		/// Checks that a longer bridge never takes fewer floors
		/// </summary>
		[Fact]
		public void LaysMoreFloorsForLongerBridges()
		{
			int last = 0;
			for (float length = 0f; length <= 30f; length += 0.05f)
			{
				int floors = VillagePaths.BridgeFloors(length);
				Assert.True(floors >= last, "Fewer floors at " + length);
				last = floors;
			}
		}
	}
}
