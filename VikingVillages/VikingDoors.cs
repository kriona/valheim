using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Walks living vikings in and out of houses through their doors, opening a closed door in a viking's way and
	/// closing it behind them
	/// </summary>
	/// <remarks>
	/// A closed door's collider is on the piece layer the navmesh is built from, so it walls off the doorway and a house
	/// with its doors shut is an island of navmesh of its own. Opening a door doesn't rebuild the navmesh either - a tile
	/// is only rebuilt when something pathfinds near it and it's been 10 seconds since its last build, one tile at a
	/// time - so a path through a doorway can lag well behind the door. Steer has a viking pathfind to a point just
	/// before the door on its side, then walk straight through the doorway to a point just past it, which needs no
	/// navmesh, while Update opens the door as the viking comes up to it.
	///
	/// A house is worked out from each door on its own: either side of the door that's walled in all around, with a roof
	/// over it, is a room, and a point is in that room when it's on that side of the door and can see the point just
	/// inside it. That covers an interior door between two rooms as well as an outer door
	/// </remarks>
	internal static class VikingDoors
	{
		/// <summary>
		/// How far in front of a viking a closed door gets opened, in meters from the door's plane
		/// </summary>
		internal const float OpenReach = 1.5f;

		/// <summary>
		/// How far to either side of a doorway's middle a viking still walks through it, in meters
		/// </summary>
		internal const float DoorwayHalfWidth = 0.9f;

		/// <summary>
		/// The cosine of the widest angle between a viking's heading and straight through a door that has it open the door
		/// </summary>
		internal const float HeadingCosine = 0.5f;

		/// <summary>
		/// How far past a door a viking has to be before it closes the door, and how close a character has to be to a
		/// doorway's middle to be standing in it, in meters
		/// </summary>
		internal const float CloseClearance = 2.5f;
		private const float DoorwayRadius = 1.5f;

		/// <summary>
		/// How long a viking waits to get through a door they opened before closing it wherever they are, in seconds,
		/// unless their way still goes through the door
		/// </summary>
		private const float OpenTimeout = 30f;

		/// <summary>
		/// How long after opening or closing a door a viking waits before trying it again, in seconds, while the door
		/// swings
		/// </summary>
		private const float SwingWait = 1.5f;

		/// <summary>
		/// How many doors the doors looked through hold before the ones that are gone are dropped as another wakes, on
		/// top of twice what's left after the last time
		/// </summary>
		private const int PruneMin = 256;

		/// <summary>
		/// How far out from a door the points a viking walks between on either side stand, in meters
		/// </summary>
		internal const float StepOut = 1.2f;

		/// <summary>
		/// How close to the point before a door a viking has to come to start walking through the doorway, in meters,
		/// and the longest it walks straight for before looking again, in seconds
		/// </summary>
		internal const float CrossStart = 1.5f;
		private const float CrossTimeout = 6f;

		/// <summary>
		/// How far a door is looked for around a point, and how far from a door a point in its room can be, in meters
		/// </summary>
		private const float DoorRange = 25f;
		private const float RoomReach = 15f;

		/// <summary>
		/// How far above or below a viking a door can be and still be its door, in meters
		/// </summary>
		internal const float FloorReach = 1.5f;

		/// <summary>
		/// How long a viking's route is kept before it's worked out again, in seconds, and how far its target can move
		/// before then, in meters
		/// </summary>
		private const float RouteRefresh = 0.25f;
		private const float RouteTargetMove = 0.5f;

		/// <summary>
		/// How long what's either side of a door is kept before it's looked at again, in seconds
		/// </summary>
		private const float DoorRefresh = 30f;

		/// <summary>
		/// How far the rays that look for walls around a side of a door reach, in meters, how many there are, and the
		/// height above the floor they're cast from
		/// </summary>
		private const float WallReach = 12f;
		private const int WallRays = 8;
		private const float WallRayHeight = 1.5f;

		/// <summary>
		/// How far the ray that looks for a roof over a side of a door reaches, in meters
		/// </summary>
		private const float RoofReach = 10f;

		/// <summary>
		/// The score a side of a door needs to be a room - one for each wall ray that hits and one for a roof - and the
		/// lower score that's enough when the other side scores no more than OpenScore
		/// </summary>
		internal const int RoomScore = 8;
		internal const int PartRoomScore = 6;
		internal const int OpenScore = 3;

		/// <summary>
		/// The heights above the floor a room is looked across at, in meters - over a table, and over most else
		/// </summary>
		private static readonly float[] SightHeights = { 1.0f, 1.6f };

		/// <summary>
		/// What's either side of a door
		/// </summary>
		internal class DoorInfo
		{
			/// <summary>
			/// The floor in the middle of the doorway
			/// </summary>
			public Vector3 Floor;

			/// <summary>
			/// The door's forward direction, level
			/// </summary>
			public Vector3 Facing;

			/// <summary>
			/// Whether the side the door faces is a room, and whether the side behind it is
			/// </summary>
			public bool FrontRoom;
			public bool BackRoom;

			/// <summary>
			/// When this was worked out, in Time.time seconds
			/// </summary>
			public float Time;
		}

		/// <summary>
		/// A room beside a door - the door, what's either side of it, and the side of it the room is on, 1 for the side
		/// it faces or -1 for behind
		/// </summary>
		internal struct Room
		{
			public Door Door;
			public DoorInfo Info;
			public int Side;
		}

		/// <summary>
		/// A door a viking opened, which it closes once it's through
		/// </summary>
		private class OpenedDoor
		{
			/// <summary>
			/// The door
			/// </summary>
			public Door Door;

			/// <summary>
			/// The side of the door the viking opened it from
			/// </summary>
			public int Side;

			/// <summary>
			/// When the door times out, after which the viking closes it once their way no longer goes through it, in
			/// Time.time seconds
			/// </summary>
			public float Until;
		}

		/// <summary>
		/// What a viking is doing with doors
		/// </summary>
		private class VikingState
		{
			/// <summary>
			/// The doors it opened and hasn't closed yet
			/// </summary>
			public readonly List<OpenedDoor> Opened = new List<OpenedDoor>();

			/// <summary>
			/// The door it's walking straight through, or null, the point past it it's walking to, the side of the
			/// door that point is on, and when it stops walking straight, in Time.time seconds
			/// </summary>
			public Door CrossDoor;
			public Vector3 CrossTo;
			public int CrossSide;
			public float CrossUntil;

			/// <summary>
			/// The door its route goes through next, or null when it needs none, the side of the door it's coming
			/// from, the target the route was worked out for, and when it's worked out again, in Time.time seconds
			/// </summary>
			public Door RouteDoor;
			public int RouteSide;
			public Vector3 RouteTarget;
			public float RouteUntil;

			/// <summary>
			/// Whether it's waiting beside a door for the doorway to clear, since when, in Time.time seconds, or -1 while
			/// it isn't, the door it's waiting at, and when it last waited there - so its wait carries on while it steps
			/// about beside the door
			/// </summary>
			public bool Waiting;
			public float WaitSince = -1f;
			public Door WaitDoor;
			public float LastWaited;

			/// <summary>
			/// What it last waited for at a door, for the debug hover text, or null, and who and why, for the log
			/// </summary>
			public string Blocker;
			public int BlockerKey;

			/// <summary>
			/// Who stands in the doorway it waits at, when that's what it waits for, or null
			/// </summary>
			public Character Standing;

			/// <summary>
			/// Whether it's lined up with the doorway it's crossing, after stepping in front of it from beside it
			/// </summary>
			public bool LinedUp;

			/// <summary>
			/// When it can next open or close a door to get itself unstuck (see Unstick), in Time.time seconds
			/// </summary>
			public float UnstickAt;
		}

		/// <summary>
		/// The viking crossing each door, which the others wait for, and until when it's held
		/// </summary>
		private static readonly Dictionary<Door, KeyValuePair<Character, float>> crossings = new Dictionary<Door, KeyValuePair<Character, float>>();

		/// <summary>
		/// How far to the side of the point before a door a viking waits for the doorway to clear, so it's out of the way
		/// of whoever comes through, how far out from the door's plane counts as in the doorway, both in meters, and how
		/// long it waits before it goes through anyway, in seconds
		/// </summary>
		internal const float WaitAside = 1.3f;
		private const float DoorwayDepth = 0.6f;
		internal const float WaitMax = 8f;

		/// <summary>
		/// How far to the side of a doorway's middle a viking can start crossing straight through it from, in meters -
		/// from farther it steps in front of the doorway first
		/// </summary>
		private const float LineUpWidth = 0.4f;

		/// <summary>
		/// How long a viking can go without waiting at a door before its wait there starts over, in seconds
		/// </summary>
		internal const float WaitGap = 3f;

		/// <summary>
		/// How far from a stuck viking a door can be for it to open or close it to get itself unstuck, in meters, and how
		/// long before it does so again, in seconds, so it doesn't swing a door back and forth
		/// </summary>
		private const float UnstickReach = 2.5f;
		private const float UnstickWait = 6f;

		/// <summary>
		/// The doors that have woken in the world
		/// </summary>
		private static readonly List<Door> doors = new List<Door>();

		/// <summary>
		/// How many doors the doors looked through hold before the ones that are gone are dropped as the next wakes -
		/// FindDoors drops them too, but only runs on a client that owns a viking
		/// </summary>
		private static int nextPrune = PruneMin;

		/// <summary>
		/// What's either side of each door looked at
		/// </summary>
		private static readonly Dictionary<Door, DoorInfo> infos = new Dictionary<Door, DoorInfo>();

		/// <summary>
		/// When each door asked to open or close can be asked again, in Time.time seconds
		/// </summary>
		private static readonly Dictionary<Door, float> waits = new Dictionary<Door, float>();

		/// <summary>
		/// What each viking is doing with doors
		/// </summary>
		private static readonly ConditionalWeakTable<Character, VikingState> states = new ConditionalWeakTable<Character, VikingState>();

		/// <summary>
		/// Buffers reused while working out routes and casting rays
		/// </summary>
		private static readonly List<Door> nearby = new List<Door>();
		private static readonly List<Room> vikingRooms = new List<Room>();
		private static readonly List<Room> targetRooms = new List<Room>();
		private static readonly RaycastHit[] hits = new RaycastHit[32];

		/// <summary>
		/// The layers of building pieces, looked up on first use
		/// </summary>
		private static int pieceMask;

		/// <summary>
		/// Adds a door that woke to the doors looked through, dropping the doors that are gone once there are nextPrune
		/// </summary>
		/// <param name="door">The door</param>
		public static void Register(Door door)
		{
			if (doors.Count >= nextPrune)
			{
				for (int i = doors.Count - 1; i >= 0; i--)
				{
					if (!doors[i])
					{
						Forget(doors[i]);
						doors.RemoveAt(i);
					}
				}
				nextPrune = doors.Count * 2 + PruneMin;
			}
			doors.Add(door);
		}

		/// <summary>
		/// Forgets what's noted about a door that's gone
		/// </summary>
		/// <param name="door">The door</param>
		private static void Forget(Door door)
		{
			infos.Remove(door);
			waits.Remove(door);
			crossings.Remove(door);
		}

		/// <summary>
		/// Opens or closes a door this client owns, the way the game does for a player, leaving it be when it's already
		/// open or closed as wanted, so a door a player just closed isn't opened again by closing it
		/// </summary>
		/// <param name="door">The door</param>
		/// <param name="state">1 or -1 to open it toward either side, or 0 to close it</param>
		private static void SetDoor(Door door, int state)
		{
			if (!door || door.m_nview == null || !door.m_nview.IsValid() || !door.m_nview.IsOwner() || door.m_animator == null || !door.CanInteract())
			{
				return;
			}
			ZDO zdo = door.m_nview.GetZDO();
			if ((zdo.GetInt(ZDOVars.s_state) != 0) == (state != 0))
			{
				return;
			}
			zdo.Set(ZDOVars.s_state, Mathf.Clamp(state, -1, 1));
			door.UpdateState();
		}

		/// <summary>
		/// Works out where a viking heading for a target walks next, when getting there means going through a house's
		/// door - to the point just before the door on its side, pathfinding, then straight through the doorway to the
		/// point just past it
		/// </summary>
		/// <param name="viking">The viking, on its owner</param>
		/// <param name="target">Where it's heading</param>
		/// <param name="waypoint">Where it walks next - the target when it needs no door</param>
		/// <param name="direct">Whether it walks straight at the waypoint instead of pathfinding, while crossing a
		/// doorway</param>
		/// <returns>True when its way goes through a door</returns>
		public static bool Steer(Character viking, Vector3 target, out Vector3 waypoint, out bool direct)
		{
			waypoint = target;
			direct = false;
			if (viking == null || viking.IsDead())
			{
				return(false);
			}
			VikingState state = states.GetOrCreateValue(viking);
			Vector3 position = viking.transform.position;
			state.Waiting = false;
			if (state.CrossDoor != null)
			{
				if (StillCrossing(state, position))
				{
					// From beside the doorway it steps in front of it first, so it walks through rather than into the wall
					// beside it, and opens the door as it comes at it
					DoorInfo crossing = GetInfo(state.CrossDoor);
					if (!state.LinedUp && Mathf.Abs(DoorGeometry.Lateral(crossing, position)) > LineUpWidth && DoorGeometry.Along(crossing, -state.CrossSide, position) > 0f)
					{
						waypoint = DoorGeometry.SidePoint(crossing, -state.CrossSide);
						direct = true;
						return(true);
					}
					state.LinedUp = true;
					waypoint = state.CrossTo;
					direct = true;
					return(true);
				}
				ReleaseCrossing(state.CrossDoor, viking);
				state.CrossDoor = null;
				state.RouteUntil = 0f;
			}
			if (Time.time >= state.RouteUntil || Vector3.Distance(target, state.RouteTarget) > RouteTargetMove)
			{
				FindRoute(state, position, target);
				state.RouteTarget = target;
				state.RouteUntil = Time.time + RouteRefresh;
			}
			if (!IsUsable(state.RouteDoor))
			{
				state.RouteDoor = null;
				return(false);
			}
			DoorInfo info = GetInfo(state.RouteDoor);
			Vector3 near = DoorGeometry.SidePoint(info, state.RouteSide);
			if (DoorGeometry.ReadyToCross(info, state.RouteSide, position, near))
			{
				// One at a time - a viking waits beside the door while another crosses it or someone stands in it
				if (DoorwayTaken(state.RouteDoor, info, viking, out string blocker, out int blockerKey, out state.Standing) && !WaitedEnough(state))
				{
					if (Player.m_debugMode && blockerKey != state.BlockerKey)
					{
						Plugin.Log.LogInfo("Door: " + DebugName(viking) + " waits at " + DoorName(state.RouteDoor) + " for " + blocker);
					}
					state.Blocker = blocker;
					state.BlockerKey = blockerKey;
					state.Waiting = true;
					waypoint = DoorGeometry.WaitSpot(info, state.RouteSide, position);
					return(true);
				}
				if (Player.m_debugMode && state.Blocker != null)
				{
					Plugin.Log.LogInfo("Door: " + DebugName(viking) + " goes through " + DoorName(state.RouteDoor) + ((blocker != null) ? " after waiting " + WaitMax.ToString("F0") + "s for " + blocker : ", now clear"));
				}
				state.Blocker = null;
				state.BlockerKey = 0;
				state.WaitSince = -1f;
				state.LinedUp = false;
				state.CrossDoor = state.RouteDoor;
				state.CrossSide = -state.RouteSide;
				state.CrossTo = DoorGeometry.SidePoint(info, state.CrossSide);
				state.CrossUntil = Time.time + CrossTimeout;
				crossings[state.CrossDoor] = new KeyValuePair<Character, float>(viking, state.CrossUntil);
				waypoint = state.CrossTo;
				direct = true;
				return(true);
			}
			waypoint = near;
			return(true);
		}

		/// <summary>
		/// Checks whether a viking is waiting beside a door for its doorway to clear, which doesn't count as being stuck
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <returns>True while it waits</returns>
		public static bool IsWaiting(Character viking)
		{
			return(viking != null && states.TryGetValue(viking, out VikingState state) && state.Waiting);
		}

		/// <summary>
		/// Finds who stands in the doorway a viking is waiting at, so it can be asked to make way
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <returns>Who stands there, or null when the viking isn't waiting for someone standing in a doorway</returns>
		public static Character DoorwayBlocker(Character viking)
		{
			if (viking == null || !states.TryGetValue(viking, out VikingState state) || !state.Waiting)
			{
				return(null);
			}
			return(state.Standing);
		}

		/// <summary>
		/// Checks whether a viking is walking straight through a doorway
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <returns>True while it is</returns>
		public static bool IsCrossing(Character viking)
		{
			return(viking != null && states.TryGetValue(viking, out VikingState state) && state.CrossDoor != null);
		}

		/// <summary>
		/// Checks whether a doorway is taken - another viking is crossing it, or someone else stands in it, other than a
		/// viking waiting to go through it, which would leave them all waiting for each other
		/// </summary>
		/// <param name="door">The door</param>
		/// <param name="info">What's either side of it</param>
		/// <param name="viking">The viking that wants to cross</param>
		/// <param name="blocker">What it waits for, for the debug hover text, or null when the doorway is clear</param>
		/// <param name="blockerKey">Who it waits for and why, which changes only when that does, for the log - 0 when the
		/// doorway is clear</param>
		/// <param name="standing">Who stands in the doorway, when that's what it waits for, or null</param>
		/// <returns>True when it has to wait</returns>
		private static bool DoorwayTaken(Door door, DoorInfo info, Character viking, out string blocker, out int blockerKey, out Character standing)
		{
			blocker = null;
			blockerKey = 0;
			standing = null;
			if (crossings.TryGetValue(door, out KeyValuePair<Character, float> crossing) && crossing.Key != viking && crossing.Key != null && !crossing.Key.IsDead() && Time.time < crossing.Value)
			{
				blocker = DebugName(crossing.Key) + " crossing (" + (crossing.Value - Time.time).ToString("F0") + "s left)";
				blockerKey = crossing.Key.GetInstanceID() * 2;
				return(true);
			}
			foreach (Character character in Character.GetAllCharacters())
			{
				if (character == null || character == viking || character.IsDead() || IsWaiting(character))
				{
					continue;
				}
				Vector3 position = character.transform.position;
				if (Mathf.Abs(DoorGeometry.Along(info, 1, position)) < DoorwayDepth && Mathf.Abs(DoorGeometry.Lateral(info, position)) <= DoorwayHalfWidth && Mathf.Abs(position.y - info.Floor.y) < 2f)
				{
					blocker = DebugName(character) + " standing in the doorway" + (IsCrossing(character) ? ", crossing" : "");
					blockerKey = character.GetInstanceID() * 2 + 1;
					standing = character;
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Names a creature for the debug hover text and the log - its name and where it stands
		/// </summary>
		/// <param name="character">The creature</param>
		/// <returns>The name</returns>
		private static string DebugName(Character character)
		{
			return(Utils.GetPrefabName(character.gameObject) + " '" + character.GetHoverName() + "' at " + character.transform.position.ToString("F1"));
		}

		/// <summary>
		/// Describes what a viking is doing at doors, for the debug hover text - the door its route goes through, and
		/// whether it's walking to it, crossing it or waiting for it, and what for
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <returns>The line, ending in a line break, or nothing when it knows of no door or has none on its way</returns>
		public static string DebugLine(Character viking)
		{
			if (viking == null || !states.TryGetValue(viking, out VikingState state))
			{
				return("");
			}
			if (state.CrossDoor != null)
			{
				return("Door: crossing " + DoorName(state.CrossDoor) + " to " + state.CrossTo.ToString("F1") + ", " + (state.CrossUntil - Time.time).ToString("F0") + "s left\n");
			}
			if (state.RouteDoor == null)
			{
				return("");
			}
			if (state.Waiting)
			{
				return("Door: waiting " + (Time.time - state.WaitSince).ToString("F0") + "/" + WaitMax.ToString("F0") + "s at " + DoorName(state.RouteDoor) + " for " + (state.Blocker ?? "?") + "\n");
			}
			return("Door: heading for " + DoorName(state.RouteDoor) + " from side " + state.RouteSide + "\n");
		}

		/// <summary>
		/// Names a door for the debug hover text and the log - where it stands
		/// </summary>
		/// <param name="door">The door</param>
		/// <returns>The name</returns>
		private static string DoorName(Door door)
		{
			return((door != null) ? "the door at " + door.transform.position.ToString("F1") : "a door that's gone");
		}

		/// <summary>
		/// Notes how long a viking has waited at a door, and checks whether it's waited WaitMax, after which it goes
		/// through anyway, in case whoever's in the way isn't moving - a wait starts over at another door, or once it
		/// hasn't waited for WaitGap
		/// </summary>
		/// <param name="state">The viking's state, with the door its route goes through</param>
		/// <returns>True once it's waited long enough</returns>
		private static bool WaitedEnough(VikingState state)
		{
			bool waited = DoorGeometry.WaitedEnough(ref state.WaitSince, ref state.LastWaited, state.WaitDoor == state.RouteDoor, Time.time);
			state.WaitDoor = state.RouteDoor;
			return(waited);
		}

		/// <summary>
		/// Lets go of the door a viking was crossing, so the next can go through
		/// </summary>
		/// <param name="door">The door</param>
		/// <param name="viking">The viking</param>
		private static void ReleaseCrossing(Door door, Character viking)
		{
			if (door != null && crossings.TryGetValue(door, out KeyValuePair<Character, float> crossing) && crossing.Key == viking)
			{
				crossings.Remove(door);
			}
		}

		/// <summary>
		/// Opens a closed door right in front of a viking as it walks into it, and closes each door it opened once it's
		/// through and clear, unless someone stands in the doorway - called each AI update on the viking's owner
		/// </summary>
		/// <param name="viking">The viking</param>
		public static void Update(Character viking)
		{
			if (viking == null)
			{
				return;
			}
			VikingState state = states.GetOrCreateValue(viking);
			if (viking.IsDead())
			{
				ReleaseCrossing(state.CrossDoor, viking);
				state.CrossDoor = null;
				state.RouteDoor = null;
				CloseBehind(state, viking.transform.position, true);
				state.Opened.Clear();
				return;
			}
			Vector3 position = viking.transform.position;
			FindDoors(position, DoorwayRadius + OpenReach);
			Vector3 heading = viking.GetMoveDir();
			heading.y = 0f;
			if (heading.sqrMagnitude > 0.01f)
			{
				heading.Normalize();
				foreach (Door door in nearby)
				{
					if (IsClosed(door) && DoorGeometry.IsAhead(GetInfo(door), position, heading))
					{
						Open(door, position, state);
					}
				}
			}
			CloseBehind(state, position, false);
		}

		/// <summary>
		/// Checks whether a viking walking straight through a doorway is still on its way to the point past the door
		/// </summary>
		/// <param name="state">The viking's state, with the door it's crossing</param>
		/// <param name="position">Where the viking stands</param>
		/// <returns>True while it's short of the point and hasn't been at it too long</returns>
		private static bool StillCrossing(VikingState state, Vector3 position)
		{
			if (!state.CrossDoor || Time.time >= state.CrossUntil)
			{
				return(false);
			}
			DoorInfo info = GetInfo(state.CrossDoor);
			return(DoorGeometry.Along(info, state.CrossSide, position) < StepOut - 0.3f && Utils.DistanceXZ(position, state.CrossTo) > 0.4f);
		}

		/// <summary>
		/// Works out which door a viking's way to its target goes through next - out of the room it's in when the
		/// target isn't in it, or else into the target's room - choosing the door that makes the shortest walk
		/// </summary>
		/// <param name="state">The viking's state, given the door and the side it comes from, or no door</param>
		/// <param name="position">Where the viking stands</param>
		/// <param name="target">Where it's heading</param>
		private static void FindRoute(VikingState state, Vector3 position, Vector3 target)
		{
			state.RouteDoor = null;
			FindDoors(position, DoorRange);
			FindRooms(position, vikingRooms);
			FindDoors(target, DoorRange);
			FindRooms(target, targetRooms);
			if (vikingRooms.Count == 0 && targetRooms.Count == 0)
			{
				return;
			}
			if (vikingRooms.Count > 0 && targetRooms.Count > 0 && (SharesRoom() || InSight(position, target, null)))
			{
				return;
			}
			bool leaving = (vikingRooms.Count > 0);
			List<Room> rooms = (leaving ? vikingRooms : targetRooms);
			int pick = DoorGeometry.PickRoute(rooms, position, target, leaving, out int routeSide);
			if (pick >= 0)
			{
				state.RouteDoor = rooms[pick].Door;
				state.RouteSide = routeSide;
			}
		}

		/// <summary>
		/// Checks whether the viking and its target are in one of the same rooms
		/// </summary>
		/// <returns>True when they are</returns>
		private static bool SharesRoom()
		{
			foreach (Room vikingRoom in vikingRooms)
			{
				foreach (Room targetRoom in targetRooms)
				{
					if (vikingRoom.Door == targetRoom.Door && vikingRoom.Side == targetRoom.Side)
					{
						return(true);
					}
				}
			}
			return(false);
		}

		/// <summary>
		/// Finds the rooms a point is in among the doors found near it - each side of a door that's a room, that the
		/// point is on and can see the point just inside the door from
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="rooms">Given the rooms, cleared first</param>
		private static void FindRooms(Vector3 point, List<Room> rooms)
		{
			rooms.Clear();
			foreach (Door door in nearby)
			{
				if (!IsUsable(door))
				{
					continue;
				}
				DoorInfo info = GetInfo(door);
				if (Utils.DistanceXZ(point, info.Floor) > RoomReach)
				{
					continue;
				}
				for (int side = -1; side <= 1; side += 2)
				{
					bool room = ((side > 0) ? info.FrontRoom : info.BackRoom);
					if (room && DoorGeometry.Along(info, side, point) > 0.3f && InSight(DoorGeometry.SidePoint(info, side), point, door))
					{
						rooms.Add(new Room { Door = door, Info = info, Side = side });
					}
				}
			}
		}

		/// <summary>
		/// Checks whether two points on the floor can see each other at one of the SightHeights, past building pieces
		/// other than a door's own
		/// </summary>
		/// <param name="from">One point</param>
		/// <param name="to">The other</param>
		/// <param name="door">The door whose pieces don't block the view, or null</param>
		/// <returns>True when nothing blocks the view at some height</returns>
		private static bool InSight(Vector3 from, Vector3 to, Door door)
		{
			foreach (float height in SightHeights)
			{
				if (IsClear(from + Vector3.up * height, to + Vector3.up * height, door))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether a line runs clear of building pieces other than a door's own
		/// </summary>
		/// <param name="from">The start of the line</param>
		/// <param name="to">The end of the line</param>
		/// <param name="door">The door whose pieces don't block the line, or null</param>
		/// <returns>True when nothing blocks it</returns>
		private static bool IsClear(Vector3 from, Vector3 to, Door door)
		{
			Vector3 line = to - from;
			float length = line.magnitude;
			if (length < 0.01f)
			{
				return(true);
			}
			int count = Physics.RaycastNonAlloc(from, line / length, hits, length, PieceMask());
			for (int i = 0; i < count; i++)
			{
				if (door == null || !hits[i].collider.transform.IsChildOf(door.transform))
				{
					return(false);
				}
			}
			return(true);
		}

		/// <summary>
		/// Asks a door's owner to open it away from a viking, the way the game opens it for a player, and remembers it
		/// for closing, with the side the viking opened it from - kept up to date when the door is still remembered, as
		/// it is when a player closed it and the viking opens it again, maybe from the other side
		/// </summary>
		/// <param name="door">The door, closed</param>
		/// <param name="position">Where the viking stands</param>
		/// <param name="state">The viking's state, given the door</param>
		private static void Open(Door door, Vector3 position, VikingState state)
		{
			AskDoor(door, DoorGeometry.OpenState(door.transform.forward, door.transform.position, position));
			int side = DoorGeometry.OpenedSide(GetInfo(door), position);
			foreach (OpenedDoor opened in state.Opened)
			{
				if (opened.Door == door)
				{
					opened.Side = side;
					opened.Until = Time.time + OpenTimeout;
					return;
				}
			}
			state.Opened.Add(new OpenedDoor { Door = door, Side = side, Until = Time.time + OpenTimeout });
		}

		/// <summary>
		/// Closes each door a viking opened with nobody in the doorway - once they're on the far side and CloseClearance
		/// from the door, once the door is past its timeout and their way no longer goes through it, or at once when
		/// they're dead - and forgets doors that are gone or closed already
		/// </summary>
		/// <param name="state">The viking's state</param>
		/// <param name="position">Where the viking stands</param>
		/// <param name="dead">Whether the viking is dead, so every door they opened that can be closed is closed now</param>
		private static void CloseBehind(VikingState state, Vector3 position, bool dead)
		{
			for (int i = state.Opened.Count - 1; i >= 0; i--)
			{
				OpenedDoor opened = state.Opened[i];
				Door door = opened.Door;
				if (!door || door.m_nview == null || !door.m_nview.IsValid())
				{
					state.Opened.RemoveAt(i);
					continue;
				}
				if (IsWaiting(door))
				{
					continue;
				}
				if (door.m_nview.GetZDO().GetInt(ZDOVars.s_state) == 0)
				{
					state.Opened.RemoveAt(i);
					continue;
				}
				DoorInfo info = GetInfo(door);
				bool through = DoorGeometry.IsThrough(info, opened.Side, position);
				bool timedOut = (Time.time >= opened.Until && door != state.CrossDoor && door != state.RouteDoor);
				if ((dead || through || timedOut) && door.m_animator != null && door.CanInteract() && !IsOccupied(info))
				{
					AskDoor(door, 0);
					state.Opened.RemoveAt(i);
				}
			}
		}

		/// <summary>
		/// Takes over a door and opens or closes it, and holds off trying it again for SwingWait while it swings - setting
		/// the state on this client works whether or not the door's owner has the mod, and the door's ZDO carries it to
		/// everyone
		/// </summary>
		/// <param name="door">The door</param>
		/// <param name="state">1 or -1 to open it toward either side, the way the game opens it for a player, or 0 to
		/// close it</param>
		private static void AskDoor(Door door, int state)
		{
			if (!door.m_nview.IsOwner())
			{
				door.m_nview.ClaimOwnership();
			}
			SetDoor(door, state);
			waits[door] = Time.time + SwingWait;
		}

		/// <summary>
		/// Opens or closes the usable door nearest a viking that's stood stuck on its way somewhere, within UnstickReach
		/// of it, at most every UnstickWait seconds - closing an open one, whose door may be what stands in its way as it
		/// walks past, unless someone else stands in the doorway or its own way goes through it, or opening a closed one,
		/// which may be what it's walking into
		/// </summary>
		/// <param name="viking">The viking, on its owner</param>
		/// <returns>True when it opened or closed one</returns>
		public static bool Unstick(Character viking)
		{
			if (viking == null || viking.IsDead())
			{
				return(false);
			}
			VikingState state = states.GetOrCreateValue(viking);
			if (Time.time < state.UnstickAt)
			{
				return(false);
			}
			Vector3 position = viking.transform.position;
			FindDoors(position, UnstickReach);
			Door nearest = null;
			float best = float.MaxValue;
			foreach (Door door in nearby)
			{
				if (!IsUsable(door) || IsWaiting(door) || door.m_animator == null || !door.CanInteract())
				{
					continue;
				}
				// The door its own way goes through stays open
				bool ownWay = (door == state.CrossDoor || door == state.RouteDoor);
				if (ownWay && door.m_nview.GetZDO().GetInt(ZDOVars.s_state) != 0)
				{
					continue;
				}
				DoorInfo info = GetInfo(door);
				float distance = Utils.DistanceXZ(position, info.Floor);
				if (Mathf.Abs(position.y - info.Floor.y) <= FloorReach && distance <= UnstickReach && distance < best)
				{
					nearest = door;
					best = distance;
				}
			}
			if (nearest == null)
			{
				return(false);
			}
			bool open = (nearest.m_nview.GetZDO().GetInt(ZDOVars.s_state) != 0);
			if (open && IsOccupied(GetInfo(nearest), viking))
			{
				return(false);
			}
			state.UnstickAt = Time.time + UnstickWait;
			if (open)
			{
				AskDoor(nearest, 0);
				state.Opened.RemoveAll((OpenedDoor opened) => opened.Door == nearest);
			}
			else
			{
				Open(nearest, position, state);
			}
			if (Player.m_debugMode)
			{
				Plugin.Log.LogInfo("Door: " + DebugName(viking) + " was stuck, so it " + (open ? "closes " : "opens ") + DoorName(nearest));
			}
			return(true);
		}

		/// <summary>
		/// Checks whether a character that's alive stands in a doorway
		/// </summary>
		/// <param name="info">The door</param>
		/// <returns>True when one does</returns>
		private static bool IsOccupied(DoorInfo info)
		{
			return(IsOccupied(info, null));
		}

		/// <summary>
		/// Checks whether a character that's alive stands in a doorway, other than one left out
		/// </summary>
		/// <param name="info">The door</param>
		/// <param name="except">The character left out, or null</param>
		/// <returns>True when one does</returns>
		private static bool IsOccupied(DoorInfo info, Character except)
		{
			foreach (Character character in Character.GetAllCharacters())
			{
				if (character != null && character != except && !character.IsDead())
				{
					Vector3 position = character.transform.position;
					if (Utils.DistanceXZ(position, info.Floor) < DoorwayRadius && Mathf.Abs(position.y - info.Floor.y) < 2f)
					{
						return(true);
					}
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether a viking can use a door - one with no key, that can be closed again, and that isn't a door a
		/// player built inside an active ward
		/// </summary>
		/// <param name="door">The door</param>
		/// <returns>True when it can</returns>
		private static bool IsUsable(Door door)
		{
			if (!door || door.m_nview == null || !door.m_nview.IsValid() || door.m_keyItem != null || door.m_canNotBeClosed)
			{
				return(false);
			}
			// A ward keeps the vikings out of the doors a player built, but not the village's own, and only for a door the
			// game checks wards for
			bool playerBuilt = (door.m_nview.GetZDO().GetLong(ZDOVars.s_creator, 0L) != 0L);
			return(!(playerBuilt && door.m_checkGuardStone && VillageLife.Warded(door.transform.position)));
		}

		/// <summary>
		/// Checks whether a door a viking can use is closed and ready to open, and isn't waiting on its owner
		/// </summary>
		/// <param name="door">The door</param>
		/// <returns>True when it is</returns>
		private static bool IsClosed(Door door)
		{
			return(IsUsable(door) && !IsWaiting(door) && door.m_nview.GetZDO().GetInt(ZDOVars.s_state) == 0 && door.m_animator != null && door.CanInteract());
		}

		/// <summary>
		/// Checks whether a door was asked to open or close too recently to ask again
		/// </summary>
		/// <param name="door">The door</param>
		/// <returns>True while it was</returns>
		private static bool IsWaiting(Door door)
		{
			return(waits.TryGetValue(door, out float until) && Time.time < until);
		}

		/// <summary>
		/// Finds the doors near a point into the nearby list, dropping doors that are gone from the doors looked through
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="range">How far from the point a door can stand, in meters</param>
		private static void FindDoors(Vector3 point, float range)
		{
			nearby.Clear();
			for (int i = doors.Count - 1; i >= 0; i--)
			{
				Door door = doors[i];
				if (!door)
				{
					doors.RemoveAt(i);
					Forget(door);
					continue;
				}
				Vector3 position = door.transform.position;
				if (Utils.DistanceXZ(point, position) <= range && Mathf.Abs(point.y - position.y) <= range)
				{
					nearby.Add(door);
				}
			}
		}

		/// <summary>
		/// What's either side of a door, worked out again once it's DoorRefresh old
		/// </summary>
		/// <param name="door">The door</param>
		/// <returns>The door's floor, facing and rooms</returns>
		private static DoorInfo GetInfo(Door door)
		{
			if (infos.TryGetValue(door, out DoorInfo info) && Time.time - info.Time < DoorRefresh)
			{
				return(info);
			}
			if (info == null)
			{
				info = new DoorInfo();
				infos[door] = info;
			}
			info.Time = Time.time;
			Transform transform = door.transform;
			info.Floor = FindFloor(transform.position);
			Vector3 facing = transform.forward;
			bool upright = (Mathf.Abs(facing.y) < 0.5f);
			facing.y = 0f;
			info.Facing = ((facing.sqrMagnitude > 0.0001f) ? facing.normalized : Vector3.forward);
			info.FrontRoom = false;
			info.BackRoom = false;
			if (upright)
			{
				int front = SideScore(info, 1);
				int back = SideScore(info, -1);
				info.FrontRoom = DoorGeometry.IsRoom(front, back);
				info.BackRoom = DoorGeometry.IsRoom(back, front);
			}
			return(info);
		}

		/// <summary>
		/// Finds the floor under the middle of a doorway - the ground or floor piece under the door's position
		/// </summary>
		/// <remarks>
		/// A wooden door's position is in the middle of its doorway a meter up, inside the closed door's collider,
		/// which a ray cast from inside doesn't hit, so the ray starts just above the position rather than over the
		/// top of the door
		/// </remarks>
		/// <param name="position">The door's position, somewhere in its doorway</param>
		/// <returns>The floor, or the position when nothing is under it</returns>
		private static Vector3 FindFloor(Vector3 position)
		{
			int mask = PieceMask() | LayerMask.GetMask("terrain", "Default", "static_solid");
			if (Physics.Raycast(position + Vector3.up * 0.05f, Vector3.down, out RaycastHit hit, 3f, mask))
			{
				return(hit.point);
			}
			return(position);
		}

		/// <summary>
		/// Scores how walled in a side of a door is - one for each of WallRays level rays from the point just out
		/// from the door that hits a building piece, and one for a roof overhead
		/// </summary>
		/// <param name="info">The door, with its floor and facing</param>
		/// <param name="side">1 for the side the door faces or -1 for behind it</param>
		/// <returns>The score</returns>
		private static int SideScore(DoorInfo info, int side)
		{
			Vector3 origin = DoorGeometry.SidePoint(info, side) + Vector3.up * WallRayHeight;
			int mask = PieceMask();
			int score = 0;
			for (int i = 0; i < WallRays; i++)
			{
				Vector3 direction = Quaternion.Euler(0f, i * 360f / WallRays, 0f) * info.Facing;
				if (Physics.Raycast(origin, direction, WallReach, mask))
				{
					score++;
				}
			}
			if (Physics.Raycast(origin, Vector3.up, RoofReach, mask))
			{
				score++;
			}
			return(score);
		}

		/// <summary>
		/// The layer mask of building pieces, looked up the first time
		/// </summary>
		/// <returns>The mask</returns>
		private static int PieceMask()
		{
			if (pieceMask == 0)
			{
				pieceMask = LayerMask.GetMask("piece");
			}
			return(pieceMask);
		}
	}

	/// <summary>
	/// The rules VikingDoors works a doorway out by - where a point stands against a door, which side is a room, when a
	/// viking walks through, opens a door or is through it, and how long it waits at one
	/// </summary>
	internal static class DoorGeometry
	{
		/// <summary>
		/// The point on the floor StepOut from a door's middle on one side
		/// </summary>
		/// <param name="info">The door</param>
		/// <param name="side">1 for the side the door faces or -1 for behind it</param>
		/// <returns>The point</returns>
		internal static Vector3 SidePoint(VikingDoors.DoorInfo info, int side)
		{
			return(info.Floor + info.Facing * (side * VikingDoors.StepOut));
		}

		/// <summary>
		/// How far out from a door's plane a point is, toward one side
		/// </summary>
		/// <param name="info">The door</param>
		/// <param name="side">1 for the side the door faces or -1 for behind it</param>
		/// <param name="point">The point</param>
		/// <returns>The distance in meters, below 0 on the other side</returns>
		internal static float Along(VikingDoors.DoorInfo info, int side, Vector3 point)
		{
			Vector3 offset = point - info.Floor;
			offset.y = 0f;
			return(Vector3.Dot(offset, info.Facing) * side);
		}

		/// <summary>
		/// How far a point is to the side of a doorway's middle, along the door's plane
		/// </summary>
		/// <param name="info">The door</param>
		/// <param name="point">The point</param>
		/// <returns>The distance in meters, below 0 on one side</returns>
		internal static float Lateral(VikingDoors.DoorInfo info, Vector3 point)
		{
			Vector3 offset = point - info.Floor;
			offset.y = 0f;
			return(Vector3.Dot(offset, Vector3.Cross(Vector3.up, info.Facing)));
		}

		/// <summary>
		/// Finds where a viking waits for a doorway to clear - beside the point before the door on its side, toward the
		/// side it's on already, out of the way of whoever comes through
		/// </summary>
		/// <param name="info">The door</param>
		/// <param name="side">The side of the door the viking comes from</param>
		/// <param name="position">Where the viking stands</param>
		/// <returns>The spot</returns>
		internal static Vector3 WaitSpot(VikingDoors.DoorInfo info, int side, Vector3 position)
		{
			Vector3 across = Vector3.Cross(Vector3.up, info.Facing);
			float sign = ((Lateral(info, position) >= 0f) ? 1f : -1f);
			return(SidePoint(info, side) + across * (sign * VikingDoors.WaitAside));
		}

		/// <summary>
		/// Checks whether a side of a door is a room, from how walled in it and the other side are - RoomScore, or
		/// PartRoomScore when the other side scores no more than OpenScore
		/// </summary>
		/// <param name="score">The side's score</param>
		/// <param name="otherScore">The other side's score</param>
		/// <returns>True when it's a room</returns>
		internal static bool IsRoom(int score, int otherScore)
		{
			return(score >= VikingDoors.RoomScore || (score >= VikingDoors.PartRoomScore && otherScore <= VikingDoors.OpenScore));
		}

		/// <summary>
		/// Checks whether a viking is close enough to the point before a door, or already in the doorway, to walk
		/// straight through
		/// </summary>
		/// <param name="info">The door</param>
		/// <param name="side">The side of the door the viking comes from</param>
		/// <param name="position">Where the viking stands</param>
		/// <param name="near">The point before the door on the viking's side</param>
		/// <returns>True when it is</returns>
		internal static bool ReadyToCross(VikingDoors.DoorInfo info, int side, Vector3 position, Vector3 near)
		{
			if (Mathf.Abs(position.y - info.Floor.y) > VikingDoors.FloorReach)
			{
				return(false);
			}
			float along = Along(info, side, position);
			if (along < -0.3f)
			{
				return(false);
			}
			if (Utils.DistanceXZ(position, near) <= VikingDoors.CrossStart)
			{
				return(true);
			}
			return(along <= VikingDoors.StepOut + 0.3f && Mathf.Abs(Lateral(info, position)) <= VikingDoors.DoorwayHalfWidth);
		}

		/// <summary>
		/// Checks whether a door is in front of a viking that's walking into it - near enough, in line with the doorway
		/// and heading through it
		/// </summary>
		/// <param name="info">The door</param>
		/// <param name="position">Where the viking stands</param>
		/// <param name="heading">The way the viking walks, level and of length 1</param>
		/// <returns>True when it is</returns>
		internal static bool IsAhead(VikingDoors.DoorInfo info, Vector3 position, Vector3 heading)
		{
			if (Mathf.Abs(position.y - info.Floor.y) > VikingDoors.FloorReach)
			{
				return(false);
			}
			float along = Along(info, 1, position);
			if (Mathf.Abs(along) > VikingDoors.OpenReach || Mathf.Abs(Lateral(info, position)) > VikingDoors.DoorwayHalfWidth)
			{
				return(false);
			}
			Vector3 through = info.Facing * ((along >= 0f) ? -1f : 1f);
			return(Vector3.Dot(heading, through) >= VikingDoors.HeadingCosine);
		}

		/// <summary>
		/// Picks the door a viking's way goes through among the rooms beside doors - out of the room it's in when it's
		/// leaving, or else into its target's - choosing the one that makes the shortest walk through the points either
		/// side of the door, and the first of those that tie
		/// </summary>
		/// <param name="rooms">The rooms the viking is in when it's leaving, or else the rooms its target is in</param>
		/// <param name="position">Where the viking stands</param>
		/// <param name="target">Where it's heading</param>
		/// <param name="leaving">Whether the rooms are the viking's own</param>
		/// <param name="routeSide">Set to the side of the picked door the viking comes from - the room's side when it's
		/// leaving and the other side when it's going in - or 0 when there's no room</param>
		/// <returns>The picked room's index, or -1 when there are none</returns>
		internal static int PickRoute(List<VikingDoors.Room> rooms, Vector3 position, Vector3 target, bool leaving, out int routeSide)
		{
			int pick = -1;
			routeSide = 0;
			float best = float.MaxValue;
			for (int i = 0; i < rooms.Count; i++)
			{
				VikingDoors.Room room = rooms[i];
				Vector3 inside = SidePoint(room.Info, room.Side);
				Vector3 outside = SidePoint(room.Info, -room.Side);
				float cost = (leaving ? Vector3.Distance(position, inside) + Vector3.Distance(outside, target) : Vector3.Distance(position, outside) + Vector3.Distance(inside, target));
				if (cost < best)
				{
					best = cost;
					pick = i;
					routeSide = (leaving ? room.Side : -room.Side);
				}
			}
			return(pick);
		}

		/// <summary>
		/// Checks whether a viking is through a door it opened - on the far side from where it opened it, and
		/// CloseClearance from the doorway's middle
		/// </summary>
		/// <param name="info">The door</param>
		/// <param name="side">The side of the door the viking opened it from</param>
		/// <param name="position">Where the viking stands</param>
		/// <returns>True when it's through</returns>
		internal static bool IsThrough(VikingDoors.DoorInfo info, int side, Vector3 position)
		{
			return(Along(info, side, position) < 0f && Utils.DistanceXZ(position, info.Floor) >= VikingDoors.CloseClearance);
		}

		/// <summary>
		/// Works out which way a door opens away from someone, the way the game's Door.Open does for a player
		/// </summary>
		/// <param name="forward">The door's forward direction</param>
		/// <param name="door">The door's position</param>
		/// <param name="user">Where whoever opens it stands</param>
		/// <returns>The door's state once open - 1 when they stand behind it, or -1 in front of it or level with
		/// it</returns>
		internal static int OpenState(Vector3 forward, Vector3 door, Vector3 user)
		{
			Vector3 direction = (user - door).normalized;
			return((Vector3.Dot(forward, direction) < 0f) ? 1 : -1);
		}

		/// <summary>
		/// Works out the side of a door a viking opens it from
		/// </summary>
		/// <param name="info">The door</param>
		/// <param name="position">Where the viking stands</param>
		/// <returns>1 for the side the door faces, including its plane, or -1 for behind it</returns>
		internal static int OpenedSide(VikingDoors.DoorInfo info, Vector3 position)
		{
			return((Along(info, 1, position) >= 0f) ? 1 : -1);
		}

		/// <summary>
		/// Notes a viking waiting at a door, and checks whether it's waited WaitMax, after which it goes through anyway, in
		/// case whoever's in the way isn't moving - a wait starts over at another door, or once it hasn't waited for
		/// longer than WaitGap
		/// </summary>
		/// <param name="waitSince">When its wait started, or a negative number while it hasn't one, set to now when the
		/// wait starts over</param>
		/// <param name="lastWaited">When it last waited, set to now</param>
		/// <param name="sameDoor">Whether the door it waits at is the one its wait started at</param>
		/// <param name="now">The time, in seconds</param>
		/// <returns>True once it's waited long enough</returns>
		internal static bool WaitedEnough(ref float waitSince, ref float lastWaited, bool sameDoor, float now)
		{
			if (waitSince < 0f || !sameDoor || now - lastWaited > VikingDoors.WaitGap)
			{
				waitSince = now;
			}
			lastWaited = now;
			return(now - waitSince >= VikingDoors.WaitMax);
		}
	}
}
