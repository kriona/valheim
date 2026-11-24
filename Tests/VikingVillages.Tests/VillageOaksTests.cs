using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how far a farm's houses and other rooms keep from the oaks around it, seen from above
	/// </summary>
	public class VillageOaksTests
	{
		/// <summary>
		/// Makes a footprint turned about the vertical axis
		/// </summary>
		/// <param name="x">Where its middle is across</param>
		/// <param name="z">Where its middle is deep</param>
		/// <param name="width">Its full size across</param>
		/// <param name="depth">Its full size deep</param>
		/// <param name="yaw">Which way it faces, in degrees</param>
		/// <returns>The footprint</returns>
		private static FarmLayout.Footprint Footprint(float x, float z, float width, float depth, float yaw)
		{
			// Built by hand, since Quaternion.Euler and AngleAxis call into Unity's engine
			float half = yaw * Mathf.Deg2Rad / 2f;
			Quaternion rotation = new Quaternion(0f, Mathf.Sin(half), 0f, Mathf.Cos(half));
			return(new FarmLayout.Footprint { Center = new Vector3(x, 0f, z), Rotation = rotation, Half = new Vector2(width / 2f, depth / 2f) });
		}

		/// <summary>
		/// Makes a list of one oak
		/// </summary>
		/// <param name="x">Where it stands across</param>
		/// <param name="z">Where it stands deep</param>
		/// <param name="scale">How many times its prefab's size it grew to</param>
		/// <returns>The list</returns>
		private static List<VillageOaks.Oak> OneOak(float x, float z, float scale)
		{
			return(new List<VillageOaks.Oak> { new VillageOaks.Oak { Position = new Vector3(x, 0f, z), Scale = scale } });
		}

		/// <summary>
		/// Checks that a point inside a footprint is within any distance of it, whatever height it's at
		/// </summary>
		[Fact]
		public void PointInsideIsWithin()
		{
			Assert.True(FarmLayout.Within(Footprint(0f, 0f, 8f, 8f, 30f), new Vector3(1f, 40f, -1f), 0.1f));
		}

		/// <summary>
		/// Checks that a point exactly a distance from a footprint's edge isn't within it, and is a little nearer
		/// </summary>
		[Fact]
		public void EdgeAtTheDistanceIsNotWithin()
		{
			Assert.False(FarmLayout.Within(Footprint(0f, 0f, 8f, 8f, 0f), new Vector3(10f, 0f, 0f), 6f));
			Assert.True(FarmLayout.Within(Footprint(0f, 0f, 8f, 8f, 0f), new Vector3(9.9f, 0f, 0f), 6f));
		}

		/// <summary>
		/// Checks that a turned footprint reaches a point with its corner where its side wouldn't
		/// </summary>
		[Fact]
		public void TurnedCornerReachesThePoint()
		{
			Assert.False(FarmLayout.Within(Footprint(0f, 0f, 4f, 4f, 0f), new Vector3(4.5f, 0f, 0f), 2.4f));
			Assert.True(FarmLayout.Within(Footprint(0f, 0f, 4f, 4f, 45f), new Vector3(4.5f, 0f, 0f), 2.4f));
		}

		/// <summary>
		/// Checks that a house keeps farther from an oak than a low room does - a house 6 meters from an oak's trunk
		/// crowds it, while a field there doesn't
		/// </summary>
		[Fact]
		public void HousesKeepFartherThanLowRooms()
		{
			FarmLayout.Footprint footprint = Footprint(0f, 0f, 8f, 8f, 0f);
			List<VillageOaks.Oak> oaks = OneOak(10f, 0f, 1f);
			Assert.True(VillageOaks.Crowds(footprint, VillageOaks.HouseClearance, oaks));
			Assert.False(VillageOaks.Crowds(footprint, VillageOaks.RoomClearance, oaks));
		}

		/// <summary>
		/// Checks that an oak's clearance grows and shrinks with the oak
		/// </summary>
		[Fact]
		public void ClearanceGrowsWithTheOak()
		{
			FarmLayout.Footprint footprint = Footprint(0f, 0f, 8f, 8f, 0f);
			float edge = 4f + VillageOaks.HouseClearance + 1f;
			Assert.False(VillageOaks.Crowds(footprint, VillageOaks.HouseClearance, OneOak(edge, 0f, 1f)));
			Assert.True(VillageOaks.Crowds(footprint, VillageOaks.HouseClearance, OneOak(edge, 0f, 1.5f)));
			Assert.False(VillageOaks.Crowds(footprint, VillageOaks.HouseClearance, OneOak(4f + VillageOaks.HouseClearance - 1f, 0f, 0.8f)));
		}

		/// <summary>
		/// Checks that a footprint crowds no oaks when there are none, and crowds them when any one is too close
		/// </summary>
		[Fact]
		public void AnyOakTooCloseCrowds()
		{
			FarmLayout.Footprint footprint = Footprint(0f, 0f, 8f, 8f, 0f);
			Assert.False(VillageOaks.Crowds(footprint, VillageOaks.HouseClearance, new List<VillageOaks.Oak>()));
			List<VillageOaks.Oak> oaks = OneOak(50f, 0f, 1f);
			oaks.AddRange(OneOak(0f, -6f, 1f));
			Assert.True(VillageOaks.Crowds(footprint, VillageOaks.HouseClearance, oaks));
		}
	}
}
