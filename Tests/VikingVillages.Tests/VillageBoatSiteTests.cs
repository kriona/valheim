using System;
using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests where a generated village's boat is moored, over made-up ground
	/// </summary>
	public class VillageBoatSiteTests
	{
		/// <summary>
		/// The sea's level, the height of dry ground and of deep water, how far a boat needs deep water around it and how
		/// deep
		/// </summary>
		private const float Water = 30f;
		private const float Land = 35f;
		private const float Deep = 20f;
		private const float Room = 8f;
		private const float Draft = 3f;

		/// <summary>
		/// The village's middle and how far it reaches - its edge at x = 40 toward the east
		/// </summary>
		private static readonly Vector3 Middle = new Vector3(0f, Water, 0f);
		private const float Radius = 40f;

		/// <summary>
		/// Works out how far past the round village's edge a point is
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>The distance, 0 inside it</returns>
		private static float Outside(Vector3 point)
		{
			float dx = point.x - Middle.x;
			float dz = point.z - Middle.z;
			return(Math.Max(0f, (float)Math.Sqrt(dx * dx + dz * dz) - Radius));
		}

		/// <summary>
		/// Finds the boat's spot over made-up ground
		/// </summary>
		/// <param name="height">The ground's height by x and z</param>
		/// <param name="sea">Whether a point is out at sea</param>
		/// <param name="spot">Set to the spot</param>
		/// <param name="facing">Set to which way the boat faces</param>
		/// <returns>Whether a spot was found</returns>
		private static bool Find(Func<float, float, float> height, Func<float, float, bool> sea, out Vector3 spot, out Vector3 facing)
		{
			return(VillageBoatSite.Find(Middle, Radius, Room, Draft, Water, Outside, height, sea, out spot, out facing));
		}

		[Fact]
		public void WaterTooShallowForTheBoatGetsNone()
		{
			// The sea past x = 50 only 2 meters deep, less than the boat's draft
			Assert.False(Find((float x, float z) => ((x < 50f) ? Land : Water - 2f), (float x, float z) => x > 300f, out Vector3 _, out Vector3 _));
		}

		[Fact]
		public void TheBoatIsMooredPastAShallowShelf()
		{
			// Shallow water from x = 50 to 70, deep past it
			Func<float, float, float> height = (float x, float z) => ((x < 50f) ? Land : ((x < 70f) ? Water - 1f : Deep));
			Assert.True(Find(height, (float x, float z) => x > 300f, out Vector3 spot, out Vector3 _));
			Assert.True(spot.x - Room >= 70f, "clear of the shelf: " + spot);
		}

		[Fact]
		public void AVillageOnTheCoastGetsABoatJustOffItsShore()
		{
			// Land up to x = 50, the sea past it, out at sea from x = 300
			bool found = Find((float x, float z) => ((x < 50f) ? Land : Deep), (float x, float z) => x > 300f, out Vector3 spot, out Vector3 facing);
			Assert.True(found);
			Assert.InRange(spot.x, 50f + Room, 50f + Room + VillageBoatSite.GridStep * 2f);
			Assert.Equal(Water, spot.y, 3);
			Assert.True(facing.x > 0.9f, "faces out to sea, not along the shore: " + facing);
		}

		[Fact]
		public void WaterTooFarFromTheEdgeGetsNoBoat()
		{
			// The shore 40 meters past the edge, farther than ShoreReach
			Assert.False(Find((float x, float z) => ((x < 80f) ? Land : Deep), (float x, float z) => x > 300f, out Vector3 _, out Vector3 _));
		}

		[Fact]
		public void ALakeThatNeverReachesTheSeaGetsNoBoat()
		{
			// A round lake 60 meters across just east of the village, the sea nowhere near
			Func<float, float, float> height = (float x, float z) => (((x - 80f) * (x - 80f) + z * z < 30f * 30f) ? Deep : Land);
			Assert.False(Find(height, (float x, float z) => false, out Vector3 _, out Vector3 _));
		}

		[Fact]
		public void ALakeJoinedToTheSeaByARiverGetsABoat()
		{
			// The same lake, with a river 40 meters wide running east from it to the sea at x = 1000
			Func<float, float, float> height = (float x, float z) => ((((x - 80f) * (x - 80f) + z * z < 30f * 30f) || (x > 80f && Math.Abs(z) < 20f)) ? Deep : Land);
			Assert.True(Find(height, (float x, float z) => x > 1000f, out Vector3 spot, out Vector3 _));
			Assert.True(Outside(spot) <= VillageBoatSite.SpotReach);
		}

		[Fact]
		public void WaterTooNarrowForTheBoatGetsNone()
		{
			// A stream 8 meters wide running past the village's edge to the sea
			Func<float, float, float> height = (float x, float z) => ((Math.Abs(x - 50f) < 4f) ? Deep : Land);
			Assert.False(Find(height, (float x, float z) => z > 500f, out Vector3 _, out Vector3 _));
		}

		[Fact]
		public void TheBoatFacesAlongARiver()
		{
			// A river 40 meters wide running north to south past the village's eastern edge, out to sea far north
			Func<float, float, float> height = (float x, float z) => ((x > 50f && x < 90f) ? Deep : Land);
			Assert.True(Find(height, (float x, float z) => z > 1000f, out Vector3 spot, out Vector3 facing));
			Assert.True(Math.Abs(facing.z) > 0.9f, "faces along the river: " + facing);
		}

		[Fact]
		public void AFloodNotesTheWaterItFoundLandlocked()
		{
			HashSet<long> landlocked = new HashSet<long>();
			Func<float, float, float> height = (float x, float z) => ((x * x + z * z < 50f * 50f) ? Deep : Land);
			Assert.False(VillageBoatSite.ReachesSea(Vector3.zero, Water, height, (float x, float z) => false, landlocked));
			Assert.NotEmpty(landlocked);
			Assert.True(VillageBoatSite.ReachesSea(Vector3.zero, Water, height, (float x, float z) => true, new HashSet<long>()));
		}
	}
}
