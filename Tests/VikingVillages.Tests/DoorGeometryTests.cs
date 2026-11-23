using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a living viking works out where it stands against a door, which side of it is a room, and when it
	/// walks through, opens, waits at and is through a door
	/// </summary>
	public class DoorGeometryTests
	{
		/// <summary>
		/// Builds a door on the floor at a point, facing a level direction
		/// </summary>
		/// <param name="floor">The floor in the middle of the doorway</param>
		/// <param name="facing">The door's forward direction, level and of length 1</param>
		/// <returns>The door</returns>
		private static VikingDoors.DoorInfo MakeDoor(Vector3 floor, Vector3 facing)
		{
			return(new VikingDoors.DoorInfo { Floor = floor, Facing = facing });
		}

		/// <summary>
		/// Builds a door at the world's middle facing north, so its front is +z and its plane runs along x with +x to
		/// the right as it's seen from behind
		/// </summary>
		/// <returns>The door</returns>
		private static VikingDoors.DoorInfo NorthDoor()
		{
			return(MakeDoor(Vector3.zero, new Vector3(0f, 0f, 1f)));
		}

		/// <summary>
		/// Checks that two points are the same to within float rounding
		/// </summary>
		/// <param name="expected">The point it should be</param>
		/// <param name="actual">The point it is</param>
		private static void AssertNear(Vector3 expected, Vector3 actual)
		{
			Assert.Equal(expected.x, actual.x, 4);
			Assert.Equal(expected.y, actual.y, 4);
			Assert.Equal(expected.z, actual.z, 4);
		}

		/// <summary>
		/// Checks that a point in front of a door is out from it toward the front and short of it toward the back, by
		/// its level distance from the door's plane
		/// </summary>
		[Fact]
		public void AlongIsPositiveTowardTheSideThePointIsOn()
		{
			VikingDoors.DoorInfo door = NorthDoor();
			Vector3 point = new Vector3(0.3f, 5f, 2f);
			Assert.Equal(2f, DoorGeometry.Along(door, 1, point), 4);
			Assert.Equal(-2f, DoorGeometry.Along(door, -1, point), 4);
		}

		/// <summary>
		/// Checks that how far to the side of a doorway's middle a point is changes sign across the middle
		/// </summary>
		[Fact]
		public void LateralChangesSignAcrossTheMiddle()
		{
			VikingDoors.DoorInfo door = NorthDoor();
			Assert.Equal(-0.5f, DoorGeometry.Lateral(door, new Vector3(-0.5f, 0f, 1f)), 4);
			Assert.Equal(0.5f, DoorGeometry.Lateral(door, new Vector3(0.5f, 0f, 1f)), 4);
			Assert.Equal(0f, DoorGeometry.Lateral(door, new Vector3(0f, 2f, 3f)), 4);
		}

		/// <summary>
		/// Checks the distances from a door away from the world's middle facing east, ignoring height
		/// </summary>
		[Fact]
		public void DistancesFollowTheDoor()
		{
			VikingDoors.DoorInfo door = MakeDoor(new Vector3(10f, 2f, -4f), new Vector3(1f, 0f, 0f));
			Vector3 point = new Vector3(11.5f, 3f, -3f);
			Assert.Equal(1.5f, DoorGeometry.Along(door, 1, point), 4);
			Assert.Equal(-1f, DoorGeometry.Lateral(door, point), 4);
		}

		/// <summary>
		/// Checks that the points either side of a door stand 1.2 meters out from its floor, behind it for side -1
		/// </summary>
		[Fact]
		public void SidePointsStandOutFromTheFloor()
		{
			VikingDoors.DoorInfo door = MakeDoor(new Vector3(10f, 2f, -4f), new Vector3(0f, 0f, 1f));
			AssertNear(new Vector3(10f, 2f, -5.2f), DoorGeometry.SidePoint(door, -1));
			AssertNear(new Vector3(10f, 2f, -2.8f), DoorGeometry.SidePoint(door, 1));
			Assert.Equal(1.2f, DoorGeometry.Along(door, -1, DoorGeometry.SidePoint(door, -1)), 4);
		}

		/// <summary>
		/// Checks that a viking waits 1.3 meters to the side of the point before the door, on the side of the doorway it
		/// stands on already, and to the right when it stands in line with the middle
		/// </summary>
		/// <param name="x">How far to the right of the doorway's middle the viking stands, in meters</param>
		/// <param name="expectedX">How far to the right the spot is</param>
		[Theory]
		[InlineData(0.2f, 1.3f)]
		[InlineData(-0.2f, -1.3f)]
		[InlineData(3f, 1.3f)]
		[InlineData(0f, 1.3f)]
		public void WaitSpotStaysOnTheVikingsSide(float x, float expectedX)
		{
			Vector3 spot = DoorGeometry.WaitSpot(NorthDoor(), -1, new Vector3(x, 0f, -2f));
			AssertNear(new Vector3(expectedX, 0f, -1.2f), spot);
		}

		/// <summary>
		/// Checks which scores make a side of a door a room - 8 or more, or 6 or more when the other side scores 3 or
		/// less
		/// </summary>
		/// <param name="score">The side's score</param>
		/// <param name="otherScore">The other side's score</param>
		/// <param name="expected">Whether it's a room</param>
		[Theory]
		[InlineData(8, 9, true)]
		[InlineData(9, 0, true)]
		[InlineData(6, 3, true)]
		[InlineData(6, 0, true)]
		[InlineData(6, 4, false)]
		[InlineData(7, 7, false)]
		[InlineData(5, 0, false)]
		[InlineData(0, 0, false)]
		public void IsRoomNeedsWallsAndARoof(int score, int otherScore, bool expected)
		{
			Assert.Equal(expected, DoorGeometry.IsRoom(score, otherScore));
		}

		/// <summary>
		/// Checks when a viking coming from behind a door is ready to walk straight through it - within 1.5 meters
		/// height of its floor, no more than 0.3 meters past its plane, and within 1.5 meters of the point before it or
		/// in the doorway, within 1.5 meters out and 0.9 meters to the side
		/// </summary>
		/// <param name="x">Where the viking stands, to the right</param>
		/// <param name="y">Where it stands, up</param>
		/// <param name="z">Where it stands, toward the door's front</param>
		/// <param name="expected">Whether it's ready</param>
		[Theory]
		[InlineData(0f, 0f, -1.2f, true)]
		[InlineData(0f, 1.5f, -1.2f, true)]
		[InlineData(0f, 1.6f, -1.2f, false)]
		[InlineData(0f, -1.6f, -1.2f, false)]
		[InlineData(0f, 0f, 0.25f, true)]
		[InlineData(0f, 0f, 0.35f, false)]
		[InlineData(1.4f, 0f, -1.2f, true)]
		[InlineData(1.6f, 0f, -1.2f, false)]
		[InlineData(0.9f, 0f, 0.2f, true)]
		[InlineData(-0.9f, 0f, 0.2f, true)]
		[InlineData(0.95f, 0f, 0.2f, false)]
		[InlineData(0f, 0f, -2.8f, false)]
		public void ReadyToCrossNearTheDoor(float x, float y, float z, bool expected)
		{
			VikingDoors.DoorInfo door = NorthDoor();
			Assert.Equal(expected, DoorGeometry.ReadyToCross(door, -1, new Vector3(x, y, z), DoorGeometry.SidePoint(door, -1)));
		}

		/// <summary>
		/// Checks when a door is ahead of a viking walking into it - within 1.5 meters of its plane and its floor's
		/// height, 0.9 meters of the doorway's middle, and heading through it from either side within 60 degrees
		/// </summary>
		/// <param name="x">Where the viking stands, to the right</param>
		/// <param name="y">Where it stands, up</param>
		/// <param name="z">Where it stands, toward the door's front</param>
		/// <param name="headingX">The way it walks, to the right</param>
		/// <param name="headingZ">The way it walks, toward the door's front</param>
		/// <param name="expected">Whether the door is ahead</param>
		[Theory]
		[InlineData(0f, 0f, 1f, 0f, -1f, true)]
		[InlineData(0f, 0f, 1f, 0f, 1f, false)]
		[InlineData(0f, 0f, -1f, 0f, 1f, true)]
		[InlineData(0f, 0f, -1f, 0f, -1f, false)]
		[InlineData(0f, 0f, 1f, 0.866f, -0.5f, true)]
		[InlineData(0f, 0f, 1f, 0.88f, -0.475f, false)]
		[InlineData(0f, 0f, 1f, 1f, 0f, false)]
		[InlineData(0f, 0f, 1.5f, 0f, -1f, true)]
		[InlineData(0f, 0f, 1.6f, 0f, -1f, false)]
		[InlineData(0f, 0f, -1.6f, 0f, 1f, false)]
		[InlineData(0.95f, 0f, 1f, 0f, -1f, false)]
		[InlineData(0f, 1.6f, 1f, 0f, -1f, false)]
		public void IsAheadWhenHeadingThrough(float x, float y, float z, float headingX, float headingZ, bool expected)
		{
			Assert.Equal(expected, DoorGeometry.IsAhead(NorthDoor(), new Vector3(x, y, z), new Vector3(headingX, 0f, headingZ)));
		}

		/// <summary>
		/// Builds the rooms behind two doors facing north, at the world's middle and 10 meters east of it
		/// </summary>
		/// <param name="eastFirst">Whether the east door's room comes first</param>
		/// <returns>The rooms</returns>
		private static List<VikingDoors.Room> TwoRooms(bool eastFirst)
		{
			VikingDoors.Room west = new VikingDoors.Room { Info = NorthDoor(), Side = -1 };
			VikingDoors.Room east = new VikingDoors.Room { Info = MakeDoor(new Vector3(10f, 0f, 0f), new Vector3(0f, 0f, 1f)), Side = -1 };
			return((eastFirst ? new List<VikingDoors.Room> { east, west } : new List<VikingDoors.Room> { west, east }));
		}

		/// <summary>
		/// Checks that a viking leaving a room for a target out to the northeast goes out of the east door, the
		/// shorter walk, from the room's side, whichever order the doors come in
		/// </summary>
		/// <param name="eastFirst">Whether the east door's room comes first</param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void PickRouteLeavesByTheShortestWalk(bool eastFirst)
		{
			List<VikingDoors.Room> rooms = TwoRooms(eastFirst);
			int pick = DoorGeometry.PickRoute(rooms, new Vector3(2f, 0f, -3f), new Vector3(12f, 0f, 5f), true, out int routeSide);
			Assert.Equal(10f, rooms[pick].Info.Floor.x);
			Assert.Equal(-1, routeSide);
		}

		/// <summary>
		/// Checks that a viking leaving a room for a target out to the northwest goes out of the west door
		/// </summary>
		[Fact]
		public void PickRouteLeavesByTheNearerDoor()
		{
			List<VikingDoors.Room> rooms = TwoRooms(false);
			int pick = DoorGeometry.PickRoute(rooms, new Vector3(1f, 0f, -2f), new Vector3(-2f, 0f, 6f), true, out int routeSide);
			Assert.Equal(0, pick);
			Assert.Equal(-1, routeSide);
		}

		/// <summary>
		/// Checks that a viking going into a room for a target inside near the east door goes in by the east door,
		/// from the side away from the room
		/// </summary>
		/// <param name="eastFirst">Whether the east door's room comes first</param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void PickRouteEntersFromTheOtherSide(bool eastFirst)
		{
			List<VikingDoors.Room> rooms = TwoRooms(eastFirst);
			int pick = DoorGeometry.PickRoute(rooms, new Vector3(2f, 0f, 5f), new Vector3(11f, 0f, -3f), false, out int routeSide);
			Assert.Equal(10f, rooms[pick].Info.Floor.x);
			Assert.Equal(1, routeSide);
		}

		/// <summary>
		/// Checks that no rooms pick no door
		/// </summary>
		[Fact]
		public void PickRouteWithNoRoomsPicksNone()
		{
			Assert.Equal(-1, DoorGeometry.PickRoute(new List<VikingDoors.Room>(), Vector3.zero, Vector3.one, true, out int routeSide));
			Assert.Equal(0, routeSide);
		}

		/// <summary>
		/// Checks when a viking that opened a door from its front is through it - behind its plane and 2.5 meters or
		/// more from the doorway's middle
		/// </summary>
		/// <param name="x">Where the viking stands, to the right</param>
		/// <param name="z">Where it stands, toward the door's front</param>
		/// <param name="expected">Whether it's through</param>
		[Theory]
		[InlineData(0f, -2.5f, true)]
		[InlineData(0f, -2.4f, false)]
		[InlineData(1.5f, -2f, true)]
		[InlineData(0f, 2.5f, false)]
		[InlineData(0f, 6f, false)]
		[InlineData(3f, 0f, false)]
		public void IsThroughPastTheClearance(float x, float z, bool expected)
		{
			Assert.Equal(expected, DoorGeometry.IsThrough(NorthDoor(), 1, new Vector3(x, 0f, z)));
		}

		/// <summary>
		/// Checks that a viking that opened a door from behind is through once it's 2.5 meters out in front
		/// </summary>
		[Fact]
		public void IsThroughFromBehind()
		{
			Assert.True(DoorGeometry.IsThrough(NorthDoor(), -1, new Vector3(0f, 0f, 2.5f)));
			Assert.False(DoorGeometry.IsThrough(NorthDoor(), -1, new Vector3(0f, 0f, -2.5f)));
		}

		/// <summary>
		/// Checks that a door opens to state -1 for someone in front of it or level with it and 1 for someone behind it,
		/// as the game's Door.Open does
		/// </summary>
		/// <param name="forwardX">The door's forward direction, east</param>
		/// <param name="forwardZ">The door's forward direction, north</param>
		/// <param name="x">Where the user stands, east</param>
		/// <param name="z">Where the user stands, north</param>
		/// <param name="expected">The door's state once open</param>
		[Theory]
		[InlineData(0f, 1f, 5f, 7f, -1)]
		[InlineData(0f, 1f, 5f, 3f, 1)]
		[InlineData(0f, 1f, 7f, 5f, -1)]
		[InlineData(1f, 0f, 6f, 5f, -1)]
		[InlineData(1f, 0f, 4f, 5f, 1)]
		public void OpenStateOpensAwayFromTheUser(float forwardX, float forwardZ, float x, float z, int expected)
		{
			Assert.Equal(expected, DoorGeometry.OpenState(new Vector3(forwardX, 0f, forwardZ), new Vector3(5f, 1f, 5f), new Vector3(x, 0f, z)));
		}

		/// <summary>
		/// Checks the side a door is opened from - its front, counting its plane, or behind it
		/// </summary>
		/// <param name="x">Where the viking stands, to the right</param>
		/// <param name="z">Where it stands, toward the door's front</param>
		/// <param name="expected">1 for the front or -1 for behind</param>
		[Theory]
		[InlineData(0f, 0.5f, 1)]
		[InlineData(0f, -0.5f, -1)]
		[InlineData(0.4f, 0f, 1)]
		[InlineData(-2f, -3f, -1)]
		public void OpenedSideIsTheVikingsSide(float x, float z, int expected)
		{
			Assert.Equal(expected, DoorGeometry.OpenedSide(NorthDoor(), new Vector3(x, 0f, z)));
		}

		/// <summary>
		/// Checks that a door a viking opens again from behind counts it through once it's out in front, which it
		/// wouldn't by the side it was first opened from
		/// </summary>
		[Fact]
		public void ReopenedFromBehindIsThroughInFront()
		{
			VikingDoors.DoorInfo door = NorthDoor();
			Vector3 outInFront = new Vector3(0f, 0f, 3f);
			int side = DoorGeometry.OpenedSide(door, new Vector3(0f, 0f, -1f));
			Assert.Equal(-1, side);
			Assert.True(DoorGeometry.IsThrough(door, side, outInFront));
			Assert.False(DoorGeometry.IsThrough(door, 1, outInFront));
		}

		/// <summary>
		/// Checks that a viking waiting at a door goes through once it's waited 8 seconds since its wait started
		/// </summary>
		[Fact]
		public void WaitedEnoughAfterTheFullWait()
		{
			float since = -1f;
			float last = 0f;
			Assert.False(DoorGeometry.WaitedEnough(ref since, ref last, true, 10f));
			Assert.Equal(10f, since);
			Assert.False(DoorGeometry.WaitedEnough(ref since, ref last, true, 12f));
			Assert.False(DoorGeometry.WaitedEnough(ref since, ref last, true, 14f));
			Assert.False(DoorGeometry.WaitedEnough(ref since, ref last, true, 16f));
			Assert.False(DoorGeometry.WaitedEnough(ref since, ref last, true, 17.5f));
			Assert.True(DoorGeometry.WaitedEnough(ref since, ref last, true, 18f));
			Assert.Equal(10f, since);
			Assert.Equal(18f, last);
		}

		/// <summary>
		/// Checks that a wait starts over after more than 3 seconds without waiting, but not after exactly 3
		/// </summary>
		[Fact]
		public void WaitedEnoughStartsOverAfterAGap()
		{
			float since = -1f;
			float last = 0f;
			DoorGeometry.WaitedEnough(ref since, ref last, true, 10f);
			DoorGeometry.WaitedEnough(ref since, ref last, true, 13f);
			Assert.Equal(10f, since);
			Assert.False(DoorGeometry.WaitedEnough(ref since, ref last, true, 16.5f));
			Assert.Equal(16.5f, since);
			Assert.False(DoorGeometry.WaitedEnough(ref since, ref last, true, 18f));
		}

		/// <summary>
		/// Checks that a wait starts over at another door
		/// </summary>
		[Fact]
		public void WaitedEnoughStartsOverAtAnotherDoor()
		{
			float since = -1f;
			float last = 0f;
			DoorGeometry.WaitedEnough(ref since, ref last, true, 10f);
			DoorGeometry.WaitedEnough(ref since, ref last, true, 12f);
			Assert.False(DoorGeometry.WaitedEnough(ref since, ref last, false, 14f));
			Assert.Equal(14f, since);
			DoorGeometry.WaitedEnough(ref since, ref last, true, 16f);
			Assert.False(DoorGeometry.WaitedEnough(ref since, ref last, true, 18f));
			DoorGeometry.WaitedEnough(ref since, ref last, true, 20f);
			Assert.True(DoorGeometry.WaitedEnough(ref since, ref last, true, 22f));
		}
	}
}
