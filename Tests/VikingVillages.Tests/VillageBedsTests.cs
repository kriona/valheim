using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how the beds that brought a village its vikings are written and read, how the building a bed stands in is
	/// worked out from the walls around it, and which beds a prop bed is swapped for
	/// </summary>
	/// <remarks>
	/// Runs apart from the other tests that make ZDOIDs, since ZDOID keeps a table of user IDs that isn't safe to add to
	/// from tests running at once
	/// </remarks>
	[Collection("ZDOID")]
	public class VillageBedsTests
	{
		/// <summary>
		/// Checks that beds read back with everything they were written with, in the order they were written
		/// </summary>
		[Fact]
		public void BedsReadBackWhole()
		{
			List<VillageBeds.NotedBed> beds = TwoBeds();
			Assert.True(VillageBeds.TryDecodeBeds(VillageBeds.EncodeBeds(beds), out List<VillageBeds.NotedBed> read));
			Assert.Equal(2, read.Count);
			for (int i = 0; i < beds.Count; i++)
			{
				Assert.Equal(beds[i].Id, read[i].Id);
				Assert.Equal(beds[i].Position, read[i].Position);
				Assert.Equal(beds[i].Added, read[i].Added);
				Assert.Equal(beds[i].House.Center, read[i].House.Center);
				Assert.Equal(beds[i].House.HalfSize, read[i].House.HalfSize);
			}
		}

		/// <summary>
		/// Checks that beds written the first way - their count, then each one's ZDO and where it stands - read back
		/// with no house added for them
		/// </summary>
		[Fact]
		public void FirstWayBedsReadWithNoHouse()
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(2);
			pkg.Write(new ZDOID(5L, 11u));
			pkg.Write(new Vector3(10.5f, 31.25f, -7.75f));
			pkg.Write(new ZDOID(7L, 3u));
			pkg.Write(new Vector3(-100f, 0f, 250f));
			Assert.True(VillageBeds.TryDecodeBeds(pkg.GetArray(), out List<VillageBeds.NotedBed> read));
			Assert.Equal(2, read.Count);
			Assert.Equal(new ZDOID(5L, 11u), read[0].Id);
			Assert.Equal(new Vector3(10.5f, 31.25f, -7.75f), read[0].Position);
			Assert.Equal(new ZDOID(7L, 3u), read[1].Id);
			Assert.Equal(new Vector3(-100f, 0f, 250f), read[1].Position);
			foreach (VillageBeds.NotedBed bed in read)
			{
				Assert.False(bed.Added);
				Assert.Equal(Vector3.zero, bed.House.Center);
				Assert.Equal(0f, bed.House.HalfSize);
			}
		}

		/// <summary>
		/// Checks that no beds read back as none
		/// </summary>
		[Fact]
		public void NoBedsReadBackAsNone()
		{
			Assert.True(VillageBeds.TryDecodeBeds(VillageBeds.EncodeBeds(new List<VillageBeds.NotedBed>()), out List<VillageBeds.NotedBed> read));
			Assert.Empty(read);
		}

		/// <summary>
		/// Checks that no beds written the first way, as a count of 0, read as none
		/// </summary>
		[Fact]
		public void FirstWayNoBedsReadAsNone()
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(0);
			Assert.True(VillageBeds.TryDecodeBeds(pkg.GetArray(), out List<VillageBeds.NotedBed> read));
			Assert.Empty(read);
		}

		/// <summary>
		/// Checks that beds cut short read as none, rather than as some of them or failing
		/// </summary>
		/// <param name="cut">How many bytes are cut off the end</param>
		[Theory]
		[InlineData(1)]
		[InlineData(4)]
		[InlineData(30)]
		public void BedsCutShortReadAsNone(int cut)
		{
			byte[] bytes = VillageBeds.EncodeBeds(TwoBeds());
			byte[] shortened = new byte[bytes.Length - cut];
			System.Array.Copy(bytes, shortened, shortened.Length);
			Assert.False(VillageBeds.TryDecodeBeds(shortened, out List<VillageBeds.NotedBed> read));
			Assert.Empty(read);
		}

		/// <summary>
		/// Checks that a list with too few bytes for its first number reads as none
		/// </summary>
		[Fact]
		public void TooFewBytesReadAsNone()
		{
			Assert.False(VillageBeds.TryDecodeBeds(new byte[] { 1, 2 }, out List<VillageBeds.NotedBed> read));
			Assert.Empty(read);
		}

		/// <summary>
		/// Checks that a bed with fewer walls found around it than it takes to be a building gets the smallest house,
		/// around where the rays were cast from, at the height of its floor
		/// </summary>
		[Fact]
		public void TooFewWallsGiveTheSmallestHouse()
		{
			List<Vector3> walls = Square(new Vector3(100f, 6f, 200f), 6f);
			walls.RemoveAt(walls.Count - 1);
			VillagePaths.House house = VillageBeds.FitHouse(walls, new Vector3(101f, 6f, 199f), 5f);
			Assert.Equal(new Vector3(101f, 5f, 199f), house.Center);
			Assert.Equal(2f, house.HalfSize);
		}

		/// <summary>
		/// Checks that a bed with walls found all round it gets a house centered on them, at the height of its floor,
		/// reaching half a meter past the farthest of them
		/// </summary>
		[Fact]
		public void WallsAllRoundGiveAHouseAroundThem()
		{
			List<Vector3> walls = Square(new Vector3(100f, 6f, 200f), 4f);
			VillagePaths.House house = VillageBeds.FitHouse(walls, new Vector3(101f, 6f, 199f), 5f);
			Assert.Equal(new Vector3(100f, 5f, 200f), house.Center);
			Assert.Equal(4.5f, house.HalfSize);
		}

		/// <summary>
		/// Checks that a house is kept from 2 to 12 meters across each way from its middle, however near or far its walls
		/// are
		/// </summary>
		/// <param name="half">How far the walls are from the middle each way, in meters</param>
		/// <param name="expected">How far the house reaches from its middle</param>
		[Theory]
		[InlineData(1f, 2f)]
		[InlineData(1.5f, 2f)]
		[InlineData(4f, 4.5f)]
		[InlineData(11.5f, 12f)]
		[InlineData(15f, 12f)]
		public void HouseSizeIsClamped(float half, float expected)
		{
			VillagePaths.House house = VillageBeds.FitHouse(Square(Vector3.zero, half), Vector3.zero, 0f);
			Assert.Equal(expected, house.HalfSize);
		}

		/// <summary>
		/// Checks that more walls found on one side of a bed than the other pull the house's middle toward them, and the
		/// house reaches the walls on the far side
		/// </summary>
		[Fact]
		public void UnevenWallsShiftTheMiddle()
		{
			List<Vector3> walls = new List<Vector3>();
			foreach (float z in new[] { -2f, -1f, 0f, 0f, 1f, 2f })
			{
				walls.Add(new Vector3(104f, 6f, 200f + z));
			}
			foreach (float z in new[] { -1f, -1f, 1f, 1f })
			{
				walls.Add(new Vector3(96f, 6f, 200f + z));
			}
			VillagePaths.House house = VillageBeds.FitHouse(walls, new Vector3(100f, 6f, 200f), 5f);
			// The middle is (6 * 104 + 4 * 96) / 10 = 100.8 east, 4.8 from the far walls
			Assert.Equal(100.8f, house.Center.x, 0.001f);
			Assert.Equal(5f, house.Center.y);
			Assert.Equal(200f, house.Center.z, 0.001f);
			Assert.Equal(5.3f, house.HalfSize, 0.001f);
		}

		/// <summary>
		/// Checks that a prop bed is swapped for its own kind named without "prop_" in any case, then with "piece_"
		/// before that, then the plain wood bed
		/// </summary>
		/// <param name="name">The prop's prefab name</param>
		/// <param name="bare">Its name without "prop_"</param>
		[Theory]
		[InlineData("prop_bed02", "bed02")]
		[InlineData("PROP_Bed", "Bed")]
		[InlineData("Prop_bed_rug", "bed_rug")]
		[InlineData("bed_rug", "bed_rug")]
		[InlineData("my_prop_bed", "my_prop_bed")]
		public void CandidatesDropThePropPrefix(string name, string bare)
		{
			Assert.Equal(new[] { bare, "piece_" + bare, "bed" }, VillageBeds.BedCandidates(name));
		}

		/// <summary>
		/// Makes two beds - one with a house added for it, and one without
		/// </summary>
		/// <returns>The beds</returns>
		private static List<VillageBeds.NotedBed> TwoBeds()
		{
			return(new List<VillageBeds.NotedBed>
			{
				new VillageBeds.NotedBed
				{
					Id = new ZDOID(5L, 11u),
					Position = new Vector3(10.5f, 31.25f, -7.75f),
					Added = true,
					House = new VillagePaths.House { Center = new Vector3(12f, 31f, -6f), HalfSize = 4.5f }
				},
				new VillageBeds.NotedBed
				{
					Id = new ZDOID(7L, 3u),
					Position = new Vector3(-100f, 0f, 250f),
					Added = false
				}
			});
		}

		/// <summary>
		/// Makes the ten walls a bed's rays find in a square room - one in the middle of each side, one in each corner and
		/// one more on each of two opposite sides, evenly around its middle
		/// </summary>
		/// <param name="middle">The room's middle</param>
		/// <param name="half">How far the walls are from the middle each way, in meters</param>
		/// <returns>The walls</returns>
		private static List<Vector3> Square(Vector3 middle, float half)
		{
			List<Vector3> walls = new List<Vector3>();
			float[,] offsets = { { 1f, 0f }, { -1f, 0f }, { 0f, 1f }, { 0f, -1f }, { 1f, 1f }, { 1f, -1f }, { -1f, 1f }, { -1f, -1f }, { 1f, 0.5f }, { -1f, -0.5f } };
			for (int i = 0; i < offsets.GetLength(0); i++)
			{
				walls.Add(middle + new Vector3(offsets[i, 0] * half, 0f, offsets[i, 1] * half));
			}
			return(walls);
		}
	}
}
