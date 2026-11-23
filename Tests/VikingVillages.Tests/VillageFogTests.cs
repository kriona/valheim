using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how much of the weather's fog is left by how near the nearest burning village fire is - none within 15
	/// meters, all of it from 45 meters on, and easing between
	/// </summary>
	public class VillageFogTests
	{
		/// <summary>
		/// Checks that all the fog is left with no fires burning
		/// </summary>
		[Fact]
		public void AllTheFogIsLeftWithNoFires()
		{
			Assert.Equal(1f, VillageFog.Target(new Vector3(10f, 0f, -20f), new List<Vector3>()), 5);
		}

		/// <summary>
		/// Checks how much fog is left by how far across the ground the spot is from a fire - none at it or up to 15
		/// meters, half halfway out, and all of it at 45 meters and past
		/// </summary>
		/// <param name="distance">How far east of the fire the spot is, in meters</param>
		/// <param name="expected">The share of the fog left</param>
		[Theory]
		[InlineData(0f, 0f)]
		[InlineData(10f, 0f)]
		[InlineData(15f, 0f)]
		[InlineData(30f, 0.5f)]
		[InlineData(45f, 1f)]
		[InlineData(200f, 1f)]
		public void FogThinsNearAFire(float distance, float expected)
		{
			List<Vector3> fires = new List<Vector3> { new Vector3(100f, 30f, -50f) };
			Assert.Equal(expected, VillageFog.Target(new Vector3(100f + distance, 30f, -50f), fires), 4);
		}

		/// <summary>
		/// Checks that the fog eases in rather than thickening evenly - less than a quarter of it left a quarter of the
		/// way out, and more than three quarters three quarters of the way
		/// </summary>
		[Fact]
		public void FogEasesBetween()
		{
			List<Vector3> fires = new List<Vector3> { Vector3.zero };
			Assert.Equal(0.15625f, VillageFog.Target(new Vector3(22.5f, 0f, 0f), fires), 4);
			Assert.Equal(0.84375f, VillageFog.Target(new Vector3(37.5f, 0f, 0f), fires), 4);
		}

		/// <summary>
		/// Checks that how high the camera is above a fire makes no difference
		/// </summary>
		[Fact]
		public void HeightIsIgnored()
		{
			List<Vector3> fires = new List<Vector3> { Vector3.zero };
			Assert.Equal(0f, VillageFog.Target(new Vector3(0f, 80f, 0f), fires), 4);
		}

		/// <summary>
		/// Checks that the nearest of two fires sets the fog, whichever comes first
		/// </summary>
		[Fact]
		public void NearestFireWins()
		{
			Vector3 near = new Vector3(0f, 0f, 30f);
			Vector3 far = new Vector3(0f, 0f, -100f);
			Assert.Equal(0.5f, VillageFog.Target(Vector3.zero, new List<Vector3> { near, far }), 4);
			Assert.Equal(0.5f, VillageFog.Target(Vector3.zero, new List<Vector3> { far, near }), 4);
			Assert.Equal(0f, VillageFog.Target(new Vector3(0f, 0f, -95f), new List<Vector3> { near, far }), 4);
		}
	}
}
