using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests how a village's zones are laid out - around its fence, fire, houses, pieces and paths, the circles along a
	/// path's band, and leaving out circles that lie wholly inside another - and the middle and reach of its houses
	/// </summary>
	public class VillageZonesTests
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
		/// Works out the zones of a farm's village standing at the world's middle, with no pieces or paths
		/// </summary>
		/// <param name="fence">How far its fence stands, or 0 for none</param>
		/// <param name="houses">Its houses</param>
		/// <returns>The zones</returns>
		private static List<Village.Zone> FarmZones(float fence, List<VillagePaths.House> houses)
		{
			return(Village.MakeZones(Vector3.zero, fence, null, houses, new List<PieceRecord>(), new List<Village.Zone>(), out List<Village.Zone> _));
		}

		/// <summary>
		/// Checks that a village with nothing to it reaches 8 meters around its generator, with no base zones
		/// </summary>
		[Fact]
		public void NothingReachesAroundTheGenerator()
		{
			List<Village.Zone> zones = Village.MakeZones(new Vector3(50f, 3f, 50f), 0f, null, new List<VillagePaths.House>(), new List<PieceRecord>(), new List<Village.Zone>(), out List<Village.Zone> baseZones);
			Assert.Single(zones);
			Assert.Equal(new Vector3(50f, 3f, 50f), zones[0].Center);
			Assert.Equal(8f, zones[0].Radius, 3);
			Assert.Empty(baseZones);
		}

		/// <summary>
		/// Checks that a farm's fenced ground reaches 2 meters past its fence, and a house whose zone, 8 meters past its
		/// walls, just fits inside it is left out, while one a little farther out is kept
		/// </summary>
		[Fact]
		public void FenceTakesInAHouseJustInside()
		{
			List<Village.Zone> inside = FarmZones(20f, new List<VillagePaths.House> { House(10f, 0f, 4f) });
			Assert.Single(inside);
			Assert.Equal(22f, inside[0].Radius, 3);
			List<Village.Zone> past = FarmZones(20f, new List<VillagePaths.House> { House(10.5f, 0f, 4f) });
			Assert.Equal(2, past.Count);
			Assert.Equal(22f, past[0].Radius, 3);
			Assert.Equal(12f, past[1].Radius, 3);
		}

		/// <summary>
		/// Checks that two houses far apart each get a zone of their own, reaching 8 meters past their walls
		/// </summary>
		[Fact]
		public void HousesApartGetAZoneEach()
		{
			List<Village.Zone> zones = FarmZones(0f, new List<VillagePaths.House> { House(0f, 0f, 5f), House(100f, 0f, 5f) });
			Assert.Equal(2, zones.Count);
			Assert.All(zones, (Village.Zone zone) => Assert.Equal(13f, zone.Radius, 3));
			Assert.Contains(zones, (Village.Zone zone) => zone.Center.x == 0f);
			Assert.Contains(zones, (Village.Zone zone) => zone.Center.x == 100f);
		}

		/// <summary>
		/// Checks that the fire of a village a player started reaches 8 meters past its size, like a house
		/// </summary>
		[Fact]
		public void FireReachesLikeAHouse()
		{
			VillagePaths.House fire = House(5f, 5f, 3f);
			List<Village.Zone> zones = Village.MakeZones(new Vector3(5f, 0f, 5f), 0f, fire, new List<VillagePaths.House>(), new List<PieceRecord>(), new List<Village.Zone>(), out List<Village.Zone> baseZones);
			Assert.Single(zones);
			Assert.Equal(11f, zones[0].Radius, 3);
			Assert.Single(baseZones);
		}

		/// <summary>
		/// Checks that pieces within 3 meters of the first of them share its circle, grown to reach 8 meters around
		/// each, and a piece farther from it gets a circle of its own
		/// </summary>
		[Fact]
		public void PiecesCloseTogetherShareACircle()
		{
			List<PieceRecord> pieces = new List<PieceRecord>
			{
				new PieceRecord { Position = new Vector3(40f, 0f, 0f) },
				new PieceRecord { Position = new Vector3(42f, 0f, 0f) },
				new PieceRecord { Position = new Vector3(44f, 0f, 0f) }
			};
			List<Village.Zone> zones = Village.MakeZones(Vector3.zero, 0f, null, new List<VillagePaths.House>(), pieces, new List<Village.Zone>(), out List<Village.Zone> _);
			Assert.Equal(2, zones.Count);
			Assert.Equal(40f, zones[0].Center.x, 3);
			Assert.Equal(10f, zones[0].Radius, 3);
			Assert.Equal(44f, zones[1].Center.x, 3);
			Assert.Equal(8f, zones[1].Radius, 3);
		}

		/// <summary>
		/// Checks that a path's zones are among the village's zones but not its base zones, which are only around what
		/// it has built
		/// </summary>
		[Fact]
		public void PathsAreLeftOutOfTheBase()
		{
			List<Village.Zone> paths = new List<Village.Zone> { new Village.Zone { Center = new Vector3(30f, 0f, 0f), Radius = 4f } };
			List<Village.Zone> zones = Village.MakeZones(Vector3.zero, 0f, null, new List<VillagePaths.House> { House(0f, 0f, 5f) }, new List<PieceRecord>(), paths, out List<Village.Zone> baseZones);
			Assert.Equal(2, zones.Count);
			Assert.Contains(zones, (Village.Zone zone) => zone.Center.x == 30f);
			Assert.Single(baseZones);
			Assert.Equal(0f, baseZones[0].Center.x, 3);
		}

		/// <summary>
		/// Checks that with no houses the middle is the fallback and the reach is the 8 meter margin
		/// </summary>
		[Fact]
		public void ReachOfNoHousesUsesTheFallback()
		{
			Village.Reach(new List<VillagePaths.House>(), new Vector3(7f, 2f, -3f), out Vector3 middle, out float radius);
			Assert.Equal(new Vector3(7f, 2f, -3f), middle);
			Assert.Equal(8f, radius, 3);
		}

		/// <summary>
		/// Checks that the middle of houses is the average of theirs, and the reach is to the farthest wall from it with
		/// the 8 meter margin
		/// </summary>
		[Fact]
		public void ReachesTheFarthestWall()
		{
			Village.Reach(new List<VillagePaths.House> { House(0f, 0f, 2f), House(10f, 0f, 4f) }, Vector3.zero, out Vector3 middle, out float radius);
			Assert.Equal(5f, middle.x, 3);
			Assert.Equal(0f, middle.z, 3);
			Assert.Equal(17f, radius, 3);
		}

		/// <summary>
		/// Checks that the distance across the ground leaves out how high each point is
		/// </summary>
		[Fact]
		public void MeasuresAcrossTheGround()
		{
			Assert.Equal(5f, Village.FlatDistance(new Vector3(0f, 0f, 0f), new Vector3(3f, 40f, 4f)), 3);
		}

		/// <summary>
		/// Checks that a band has a circle at each end of its line and the rest evenly between, no farther apart than
		/// the step, each reaching as far as the band
		/// </summary>
		[Fact]
		public void LaysABandAlongItsLine()
		{
			List<Village.Zone> band = Village.BandZones(new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), 4f, 3f);
			Assert.Equal(5, band.Count);
			Assert.Equal(0f, band[0].Center.x, 3);
			Assert.Equal(10f, band[band.Count - 1].Center.x, 3);
			for (int i = 0; i < band.Count; i++)
			{
				Assert.Equal(4f, band[i].Radius, 3);
				if (i > 0)
				{
					Assert.True(Village.FlatDistance(band[i - 1].Center, band[i].Center) <= 3f + 0.001f);
				}
			}
		}

		/// <summary>
		/// Checks that a band with no length is still one circle's worth of ground
		/// </summary>
		[Fact]
		public void LaysABandWithNoLength()
		{
			List<Village.Zone> band = Village.BandZones(new Vector3(5f, 0f, 5f), new Vector3(5f, 0f, 5f), 4f, 3f);
			Assert.NotEmpty(band);
			Assert.All(band, (Village.Zone zone) => Assert.Equal(0f, Village.FlatDistance(zone.Center, new Vector3(5f, 0f, 5f)), 3));
		}

		/// <summary>
		/// Checks that a circle wholly inside another is left out, one only partly inside is kept, and the largest come
		/// first
		/// </summary>
		[Fact]
		public void LeavesOutCirclesInsideOthers()
		{
			List<Village.Zone> zones = new List<Village.Zone>
			{
				new Village.Zone { Center = new Vector3(5f, 0f, 0f), Radius = 4f },
				new Village.Zone { Center = new Vector3(0f, 0f, 0f), Radius = 30f },
				new Village.Zone { Center = new Vector3(28f, 0f, 0f), Radius = 8f },
				new Village.Zone { Center = new Vector3(60f, 0f, 0f), Radius = 8f }
			};
			List<Village.Zone> kept = Village.Outermost(zones);
			Assert.Equal(3, kept.Count);
			Assert.Equal(30f, kept[0].Radius, 3);
			Assert.DoesNotContain(kept, (Village.Zone zone) => zone.Radius == 4f);
			Assert.Contains(kept, (Village.Zone zone) => zone.Center.x == 28f);
			Assert.Contains(kept, (Village.Zone zone) => zone.Center.x == 60f);
		}

		/// <summary>
		/// Checks that a circle the same as another is kept only once
		/// </summary>
		[Fact]
		public void KeepsTheSameCircleOnce()
		{
			List<Village.Zone> zones = new List<Village.Zone>
			{
				new Village.Zone { Center = new Vector3(1f, 0f, 1f), Radius = 8f },
				new Village.Zone { Center = new Vector3(1f, 0f, 1f), Radius = 8f }
			};
			Assert.Single(Village.Outermost(zones));
		}
	}
}
