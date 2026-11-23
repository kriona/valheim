using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests how a farm's rooms are picked, how far out its farmhouses stand, and how its rooms and blueprint houses are
	/// kept from overlapping each other and its plaza, seen from above
	/// </summary>
	public class FarmLayoutTests
	{
		/// <summary>
		/// The rooms picked in the tests - a farmhouse, a field, and one whose prefab isn't loaded
		/// </summary>
		private const string House = "house";
		private const string Field = "field";
		private const string Broken = "broken";

		/// <summary>
		/// Makes a room picker that gives some picks in turn, then the same one from then on
		/// </summary>
		/// <param name="after">The pick it gives once the others are used up</param>
		/// <param name="first">The picks it gives first, null for finding none</param>
		/// <returns>The picker</returns>
		private static System.Func<string> Picks(string after, params string[] first)
		{
			Queue<string> queue = new Queue<string>(first);
			return(() => ((queue.Count > 0) ? queue.Dequeue() : after));
		}

		/// <summary>
		/// Picks a farm's rooms from the test rooms
		/// </summary>
		/// <param name="wanted">How many rooms the farm wants</param>
		/// <param name="roomScale">How many times as many rooms the farm gets as the game would give it</param>
		/// <param name="pick">The picker</param>
		/// <returns>The rooms picked</returns>
		private static List<string> PickRooms(int wanted, float roomScale, System.Func<string> pick)
		{
			return(FarmLayout.PickRooms<string>(wanted, roomScale, pick, (string room) => room != Broken, (string room) => room == House));
		}

		/// <summary>
		/// Checks that the number of picks the farmhouses are capped after is the game's own count of rooms, rounded,
		/// and never none
		/// </summary>
		/// <param name="wanted">How many rooms the farm wants</param>
		/// <param name="roomScale">How many times as many rooms it gets as the game would give it</param>
		/// <param name="expected">The number of picks</param>
		[Theory]
		[InlineData(9, 1f, 9)]
		[InlineData(9, 3f, 3)]
		[InlineData(10, 3f, 3)]
		[InlineData(11, 3f, 4)]
		[InlineData(5, 2f, 2)]
		[InlineData(1, 3f, 1)]
		public void CapIndexIsTheGamesOwnCount(int wanted, float roomScale, int expected)
		{
			Assert.Equal(expected, FarmLayout.CapIndex(wanted, roomScale));
		}

		/// <summary>
		/// Checks that a farm the game's own size takes every farmhouse it picks
		/// </summary>
		[Fact]
		public void UnwidenedFarmTakesEveryFarmhouse()
		{
			List<string> rooms = PickRooms(5, 1f, Picks(House));
			Assert.Equal(5, rooms.Count((string room) => room == House));
		}

		/// <summary>
		/// Checks that a farm with three times the rooms gets only the farmhouses its first third of picks came to,
		/// picking again past a farmhouse after that
		/// </summary>
		[Fact]
		public void WidenedFarmCapsItsFarmhouses()
		{
			List<string> rooms = PickRooms(9, 3f, Picks(Field, House, Field, House, House, House, House, House));
			Assert.Equal(9, rooms.Count);
			Assert.Equal(2, rooms.Count((string room) => room == House));
			Assert.Equal(new[] { House, Field, House, Field }, rooms.Take(4));
		}

		/// <summary>
		/// Checks that a room is given up on when every pick for it is a farmhouse past the cap
		/// </summary>
		[Fact]
		public void FarmhousesPastTheCapAreGivenUpOn()
		{
			List<string> rooms = PickRooms(6, 2f, Picks(House));
			Assert.Equal(new[] { House, House, House }, rooms);
		}

		/// <summary>
		/// Checks that the farmhouses are still capped when the pick that reaches the game's own count finds no room
		/// </summary>
		[Fact]
		public void FailedPickAtTheCapStillCaps()
		{
			string[] first = new[] { House, House }.Concat(Enumerable.Repeat<string>(null, 10)).ToArray();
			List<string> rooms = PickRooms(9, 3f, Picks(House, first));
			Assert.Equal(new[] { House, House }, rooms);
		}

		/// <summary>
		/// Checks that a room whose pick can't be placed is given up on at once rather than picked again
		/// </summary>
		[Fact]
		public void UnusablePickGivesTheRoomUp()
		{
			List<string> rooms = PickRooms(3, 1f, Picks(Field, Broken, House));
			Assert.Equal(new[] { House, Field }, rooms);
		}

		/// <summary>
		/// Checks how far out a farm's farmhouses can stand - past the plaza however small the farm, 15 meters out where
		/// the farm reaches that far, and 45 percent of its reach on a large farm
		/// </summary>
		/// <param name="reach">How far from its middle the farm's rooms can stand, in meters</param>
		/// <param name="expected">How far out its farmhouses can stand</param>
		[Theory]
		[InlineData(0f, 8f)]
		[InlineData(5f, 8f)]
		[InlineData(10f, 10f)]
		[InlineData(15f, 15f)]
		[InlineData(20f, 15f)]
		[InlineData(40f, 18f)]
		public void HouseReachStaysPastThePlaza(float reach, float expected)
		{
			Assert.Equal(expected, FarmLayout.HouseReach(reach), 4);
		}

		/// <summary>
		/// Checks that a footprint over the farm's middle covers the plaza, whatever height it stands at
		/// </summary>
		[Fact]
		public void FootprintOverTheMiddleCoversThePlaza()
		{
			FarmLayout.Footprint footprint = Footprint(1f, -1f, 4f, 4f, 30f);
			footprint.Center.y = 25f;
			Assert.True(FarmLayout.CoversPlaza(footprint, Vector3.zero));
		}

		/// <summary>
		/// Checks that a footprint whose edge stands exactly at the plaza's radius of 7 meters doesn't cover it, and does
		/// a little nearer
		/// </summary>
		[Fact]
		public void EdgeAtThePlazaRadiusDoesNotCover()
		{
			Vector3 middle = new Vector3(100f, 0f, 200f);
			Assert.False(FarmLayout.CoversPlaza(Footprint(109f, 200f, 4f, 4f, 0f), middle));
			Assert.True(FarmLayout.CoversPlaza(Footprint(108.9f, 200f, 4f, 4f, 0f), middle));
		}

		/// <summary>
		/// Checks that a square turned 45 degrees reaches the plaza with its corner where its side wouldn't
		/// </summary>
		[Fact]
		public void TurnedCornerReachesThePlaza()
		{
			Assert.False(FarmLayout.CoversPlaza(Footprint(8.3f, 0f, 2f, 2f, 0f), Vector3.zero));
			Assert.True(FarmLayout.CoversPlaza(Footprint(8.3f, 0f, 2f, 2f, 45f), Vector3.zero));
		}

		/// <summary>
		/// Checks that a long footprint off to the side covers the plaza when it's turned to point at the middle, and not
		/// when it's turned the other way, across the line to it
		/// </summary>
		[Fact]
		public void TurnDirectionDecidesTheReach()
		{
			// Turned 45 degrees, its length runs out along the line from the middle to (6, 6), so its end comes within
			// 2.5 meters of the middle - turned -45 degrees, its side stands 8 meters off
			Assert.True(FarmLayout.CoversPlaza(Footprint(6f, 6f, 1f, 12f, 45f), Vector3.zero));
			Assert.False(FarmLayout.CoversPlaza(Footprint(6f, 6f, 1f, 12f, -45f), Vector3.zero));
		}

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
		/// Checks that two footprints in the same spot overlap
		/// </summary>
		[Fact]
		public void SameSpotOverlaps()
		{
			Assert.True(FarmLayout.Overlaps(Footprint(0f, 0f, 4f, 6f, 0f), Footprint(0f, 0f, 2f, 2f, 30f)));
		}

		/// <summary>
		/// Checks that footprints far apart don't overlap, whichever way they face
		/// </summary>
		/// <param name="yaw">Which way the second one faces, in degrees</param>
		[Theory]
		[InlineData(0f)]
		[InlineData(45f)]
		[InlineData(90f)]
		[InlineData(170f)]
		public void FarApartDoesNotOverlap(float yaw)
		{
			Assert.False(FarmLayout.Overlaps(Footprint(0f, 0f, 4f, 4f, 0f), Footprint(20f, -15f, 4f, 4f, yaw)));
		}

		/// <summary>
		/// Checks that footprints only touching edge to edge don't count as overlapping, and do once they're a little
		/// closer
		/// </summary>
		[Fact]
		public void TouchingEdgesDoNotOverlap()
		{
			FarmLayout.Footprint a = Footprint(0f, 0f, 4f, 4f, 0f);
			Assert.False(FarmLayout.Overlaps(a, Footprint(4f, 0f, 4f, 4f, 0f)));
			Assert.True(FarmLayout.Overlaps(a, Footprint(3.9f, 0f, 4f, 4f, 0f)));
		}

		/// <summary>
		/// Checks that a footprint's own size is measured along the way it faces - a long, narrow one turned a quarter
		/// turn reaches the other way
		/// </summary>
		[Fact]
		public void TurningSwapsWidthAndDepth()
		{
			FarmLayout.Footprint square = Footprint(0f, 0f, 2f, 2f, 0f);
			Assert.False(FarmLayout.Overlaps(square, Footprint(4f, 0f, 2f, 10f, 0f)));
			Assert.True(FarmLayout.Overlaps(square, Footprint(4f, 0f, 2f, 10f, 90f)));
		}

		/// <summary>
		/// Checks that a turned footprint reaches further across its corners than across its sides
		/// </summary>
		[Fact]
		public void TurnedCornerReachesFurther()
		{
			FarmLayout.Footprint square = Footprint(0f, 0f, 2f, 2f, 0f);
			Assert.False(FarmLayout.Overlaps(square, Footprint(2.2f, 0f, 2f, 2f, 0f)));
			Assert.True(FarmLayout.Overlaps(square, Footprint(2.2f, 0f, 2f, 2f, 45f)));
		}

		/// <summary>
		/// Checks that only the ground they cover counts, not how high they stand
		/// </summary>
		[Fact]
		public void HeightIsIgnored()
		{
			FarmLayout.Footprint high = Footprint(1f, 1f, 4f, 4f, 0f);
			high.Center.y = 50f;
			Assert.True(FarmLayout.Overlaps(Footprint(0f, 0f, 4f, 4f, 0f), high));
		}

		/// <summary>
		/// Checks that which footprint is checked against which makes no difference
		/// </summary>
		[Fact]
		public void OverlapIsSymmetric()
		{
			FarmLayout.Footprint a = Footprint(0f, 0f, 6f, 3f, 20f);
			FarmLayout.Footprint b = Footprint(4f, 2f, 3f, 5f, 75f);
			Assert.Equal(FarmLayout.Overlaps(a, b), FarmLayout.Overlaps(b, a));
		}
	}
}
