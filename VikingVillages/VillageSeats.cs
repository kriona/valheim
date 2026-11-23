using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The sitting logs around a village's fire, each facing the fire, placed where there's room for them
	/// </summary>
	/// <remarks>
	/// Whoever owns a village's generator places them once the fire has loaded, notes it so they're only placed once,
	/// and notes them among the village's pieces so its vikings build them back. Each log sits in the ring the vikings
	/// gather in, kept within the fire's warmth, on dry, fairly flat ground with nothing in the way, outside the houses
	/// and off the paths
	/// </remarks>
	internal static class VillageSeats
	{
		/// <summary>
		/// The sitting log placed around the fire
		/// </summary>
		private const string SeatPrefab = "piece_logbench01";
		private static readonly int SeatHash = SeatPrefab.GetStableHashCode();

		/// <summary>
		/// On a farm's generator, that its village has been given its sitting logs
		/// </summary>
		private static readonly int SeatsKey = "kriona.VikingVillages.villageSeats".GetStableHashCode();

		/// <summary>
		/// On a farm's generator, where the village's fire stood before it took up another, until its sitting logs have
		/// been noted around the new one, and zero after - and where the logs its vikings are to take down stand
		/// </summary>
		private static readonly int OldFireKey = "kriona.VikingVillages.villageSeatsOldFire".GetStableHashCode();
		private static readonly int TakeDownKey = "kriona.VikingVillages.villageSeatsTakeDown".GetStableHashCode();

		/// <summary>
		/// How far from where the village's old fire stood its sitting logs can be and still be moved to its new one,
		/// and how near a spot noted to take down a log has to be to it, in meters
		/// </summary>
		private const float OldSeatReach = 8f;
		private const float TakeDownMatch = 0.5f;

		/// <summary>
		/// How many sitting logs a fire gets
		/// </summary>
		private const int Seats = 2;

		/// <summary>
		/// How far apart the spots tried around the fire are, in degrees, and the fewest degrees around the fire
		/// between two logs
		/// </summary>
		private const float AngleStep = 10f;
		private const float SeatSpread = 70f;

		/// <summary>
		/// How far inside the fire's warmth a log's seat keeps, how far a log already standing can be past the ring and
		/// still count as the fire's, how far apart two logs keep, how far above the ground the room for a log is checked
		/// from, how far into the ground a log is set so it rests on it, and the most the ground under a log can rise and
		/// fall, in meters
		/// </summary>
		private const float WarmthMargin = 0.5f;
		private const float SeatReach = 2f;
		private const float SeatGap = 0.5f;
		private const float RoomLift = 0.15f;
		private const float SeatSink = 0.05f;
		private const float SeatFlatness = 0.4f;

		/// <summary>
		/// How far above the water level the ground under a log has to be, in meters
		/// </summary>
		private const float DryHeight = 0.5f;

		private static int roomMask;

		/// <summary>
		/// Gives a village its sitting logs around its fire, once, when the fire is loaded - as many as it gets, less those
		/// already standing around it
		/// </summary>
		/// <param name="village">The village, whose generator this client owns, with everything across it loaded</param>
		public static void TryPlace(Village village)
		{
			if (village.Zdo.GetBool(SeatsKey) || ZNetScene.instance == null)
			{
				return;
			}
			Fireplace fire = village.Fire();
			if (fire == null)
			{
				return;
			}
			village.Zdo.Set(SeatsKey, true);
			List<Vector3> logs = SeatsNear(fire);
			int wanted = Seats - logs.Count;
			if (wanted <= 0)
			{
				return;
			}
			Physics.SyncTransforms();
			List<PieceRecord> built = Place(village, fire, logs, wanted);
			VillageRecords.Add(village, built);
			Plugin.Log.LogInfo("Placed " + built.Count + " sitting logs around the fire of the village at " + village.Farm.ToString("F0"));
		}

		/// <summary>
		/// Finds the sitting logs already standing around a fire
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <returns>Where they stand</returns>
		private static List<Vector3> SeatsNear(Fireplace fire)
		{
			List<Vector3> logs = new List<Vector3>();
			float reach = VillageLife.FireRing(fire, 1f) + SeatReach;
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				if (instance.Key.GetPrefab() == SeatHash && Utils.DistanceXZ(instance.Key.GetPosition(), fire.transform.position) <= reach)
				{
					logs.Add(instance.Key.GetPosition());
				}
			}
			return(logs);
		}

		/// <summary>
		/// Places sitting logs around a fire where there's room (see FindSpots)
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="fire">The fire</param>
		/// <param name="logs">Where the logs around the fire stand, added to</param>
		/// <param name="count">How many it wants</param>
		/// <returns>The logs placed, as they're noted among the village's pieces</returns>
		private static List<PieceRecord> Place(Village village, Fireplace fire, List<Vector3> logs, int count)
		{
			List<PieceRecord> built = FindSpots(village, fire, logs, count);
			GameObject prefab = ZNetScene.instance.GetPrefab(SeatPrefab);
			foreach (PieceRecord record in built)
			{
				VillageRecords.MarkBuilt(Object.Instantiate(prefab, record.Position, record.Rotation));
			}
			return(built);
		}

		/// <summary>
		/// Finds spots for sitting logs around a fire where there's room, each with its seat in the ring and facing the
		/// fire, and spread around it - starting from a side picked by the fire, so it's the same each time
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="fire">The fire</param>
		/// <param name="logs">Where the logs around the fire stand, added to</param>
		/// <param name="count">How many it wants</param>
		/// <returns>The logs that would go there, as they'd be noted among the village's pieces</returns>
		private static List<PieceRecord> FindSpots(Village village, Fireplace fire, List<Vector3> logs, int count)
		{
			List<PieceRecord> built = new List<PieceRecord>();
			GameObject prefab = ZNetScene.instance.GetPrefab(SeatPrefab);
			if (prefab == null || !VillageFurnishing.Footprint(SeatPrefab, out Bounds size))
			{
				Plugin.Log.LogWarning("Couldn't find the sitting log to put around the village's fire");
				return(built);
			}
			// Which way the seat faces and where it is, around the log's own origin
			Chair chair = prefab.GetComponentInChildren<Chair>(true);
			Vector3 seat = Vector3.zero;
			Vector3 facing = Vector3.forward;
			if (chair != null && chair.m_attachPoint != null)
			{
				seat = prefab.transform.InverseTransformPoint(chair.m_attachPoint.position);
				facing = prefab.transform.InverseTransformDirection(chair.m_attachPoint.forward);
				facing.y = 0f;
				if (facing.sqrMagnitude < 0.01f)
				{
					facing = Vector3.forward;
				}
			}
			seat.y = 0f;
			Quaternion turn = Quaternion.Inverse(Quaternion.LookRotation(facing.normalized));
			Vector3 middle = fire.transform.position;
			float warmth = VillageLife.AreaReach(fire, EffectArea.Type.Heat);
			float flames = VillageLife.AreaReach(fire, EffectArea.Type.Burning);
			float halfLength = Mathf.Max(size.extents.x, size.extents.z);
			float distance = SeatDistance(VillageLife.FireRing(fire, 0.5f), warmth, flames, halfLength);
			float apart = halfLength * 2f + SeatGap;
			Plugin.Log.LogInfo("Sitting logs go " + distance.ToString("F1") + "m from the fire at " + middle.ToString("F0") + ", its warmth reaching " + warmth.ToString("F1") + "m and its flames " + flames.ToString("F1") + "m");
			float start = StartAngle(middle);
			for (float step = 0f; step < 360f && built.Count < count; step += AngleStep)
			{
				Vector3 outward = Quaternion.Euler(0f, start + step, 0f) * Vector3.forward;
				if (logs.Exists((Vector3 log) => Crowds(log, middle, outward, distance, apart)))
				{
					continue;
				}
				Quaternion rotation = Quaternion.LookRotation(-outward) * turn;
				Vector3 spot = middle + outward * distance - rotation * seat;
				if (!HasRoom(village, spot, rotation, size, out float ground))
				{
					continue;
				}
				spot.y = ground - SeatSink;
				built.Add(new PieceRecord { Prefab = SeatHash, Position = spot, Rotation = rotation, Wood = VillageRecords.WoodCost(SeatHash) });
				logs.Add(spot);
			}
			if (built.Count < count)
			{
				Plugin.Log.LogInfo("Found room for " + built.Count + " of " + count + " sitting logs around the fire at " + middle.ToString("F0"));
			}
			return(built);
		}

		/// <summary>
		/// Works out how far from a fire its sitting logs' seats go - in the ring the vikings gather in, kept WarmthMargin
		/// inside the fire's warmth when it has any, but always far enough that the log keeps WarmthMargin clear of the
		/// flames, which wins over the warmth
		/// </summary>
		/// <param name="ring">How far from the fire the vikings gather, in meters</param>
		/// <param name="warmth">How far the fire's warmth reaches, in meters, or 0 for none</param>
		/// <param name="flames">How far the fire's flames reach, in meters, or 0 for none</param>
		/// <param name="halfLength">Half the log's longest side, in meters</param>
		/// <returns>The distance, in meters</returns>
		internal static float SeatDistance(float ring, float warmth, float flames, float halfLength)
		{
			float distance = ring;
			if (warmth > 0f)
			{
				distance = Mathf.Min(distance, warmth - WarmthMargin);
			}
			return(Mathf.Max(distance, flames + halfLength + WarmthMargin));
		}

		/// <summary>
		/// Works out the side of a fire its sitting logs are first tried on, in degrees around it - picked by where the
		/// fire stands, so it's the same each time
		/// </summary>
		/// <param name="fire">Where the fire stands</param>
		/// <returns>The angle, from 0 to 360</returns>
		internal static float StartAngle(Vector3 fire)
		{
			return(Mathf.Repeat(fire.x * 37f + fire.z * 11f, 360f));
		}

		/// <summary>
		/// Checks whether a sitting log already around a fire is in the way of one at a spot - less than SeatSpread
		/// degrees around the fire from it, or nearer the spot than the logs keep apart
		/// </summary>
		/// <param name="log">Where the log already around the fire stands</param>
		/// <param name="middle">Where the fire stands</param>
		/// <param name="outward">The direction from the fire to the spot, level and of length 1</param>
		/// <param name="distance">How far from the fire the spot is, in meters</param>
		/// <param name="apart">How far apart two logs keep, in meters</param>
		/// <returns>True when it's in the way</returns>
		internal static bool Crowds(Vector3 log, Vector3 middle, Vector3 outward, float distance, float apart)
		{
			return(Vector3.Angle(outward, Flat(log - middle)) < SeatSpread || Utils.DistanceXZ(log, middle + outward * distance) < apart);
		}

		/// <summary>
		/// Has a village's vikings move its sitting logs to the fire it took up in place of its destroyed one (see
		/// TryMove)
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		/// <param name="oldFire">Where its destroyed fire stood</param>
		public static void FireMoved(Village village, Vector3 oldFire)
		{
			if (village.Zdo.GetBool(SeatsKey))
			{
				village.Zdo.Set(OldFireKey, oldFire);
			}
		}

		/// <summary>
		/// Moves a village's sitting logs to the fire it took up in place of its destroyed one, once that fire is loaded
		/// - each log it was given around the old fire is noted at a spot around the new one instead, for its vikings to
		/// build there, and the log still standing at the old spot is left for them to take down. A log already around
		/// the new fire stays, and when a player has built a log around it, the village's logs stay where they are.
		/// Then forgets the logs to take down that no longer stand
		/// </summary>
		/// <param name="village">The village, whose generator this client owns, with everything across it loaded</param>
		public static void TryMove(Village village)
		{
			if (ZNetScene.instance == null)
			{
				return;
			}
			Vector3 oldFire = village.Zdo.GetVec3(OldFireKey, Vector3.zero);
			Fireplace fire = ((oldFire != Vector3.zero) ? village.Fire() : null);
			if (fire != null)
			{
				village.Zdo.Set(OldFireKey, Vector3.zero);
				Move(village, fire, oldFire);
			}
			List<Vector3> takeDowns = TakeDowns(village);
			if (takeDowns.RemoveAll((Vector3 spot) => VillageRespawn.ZoneSettled(spot) && StandingLog(spot) == null) > 0)
			{
				WriteTakeDowns(village, takeDowns);
			}
		}

		/// <summary>
		/// Notes a village's sitting logs around its old fire at spots around its new one, and the old ones to take down
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		/// <param name="fire">Its new fire</param>
		/// <param name="oldFire">Where its old fire stood</param>
		private static void Move(Village village, Fireplace fire, Vector3 oldFire)
		{
			float reach = VillageLife.FireRing(fire, 1f) + SeatReach;
			List<Vector3> logs = new List<Vector3>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				if (instance.Key.GetPrefab() != SeatHash || Utils.DistanceXZ(instance.Key.GetPosition(), fire.transform.position) > reach)
				{
					continue;
				}
				if (instance.Key.GetLong(ZDOVars.s_creator, 0L) != 0L)
				{
					Plugin.Log.LogInfo("Left the sitting logs of the village at " + village.Farm.ToString("F0") + " where they are, since a player built one around its new fire");
					return;
				}
				logs.Add(instance.Key.GetPosition());
			}
			List<PieceRecord> old = VillageRecords.Find(village, (PieceRecord record) => record.Prefab == SeatHash && Utils.DistanceXZ(record.Position, oldFire) <= OldSeatReach && Utils.DistanceXZ(record.Position, fire.transform.position) > reach);
			int wanted = Mathf.Min(old.Count, Seats - logs.Count);
			if (wanted <= 0)
			{
				return;
			}
			Physics.SyncTransforms();
			List<PieceRecord> spots = FindSpots(village, fire, logs, wanted);
			List<Vector3> takeDowns = TakeDowns(village);
			for (int i = 0; i < spots.Count; i++)
			{
				VillageRecords.Swap(village, SeatHash, old[i].Position, spots[i]);
				if (StandingLog(old[i].Position) != null)
				{
					takeDowns.Add(old[i].Position);
				}
			}
			WriteTakeDowns(village, takeDowns);
			Plugin.Log.LogInfo("Moving " + spots.Count + " sitting logs of the village at " + village.Farm.ToString("F0") + " to its new fire");
		}

		/// <summary>
		/// Lists where the sitting logs a village's vikings are to take down stand
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>Where they stand</returns>
		public static List<Vector3> TakeDowns(Village village)
		{
			return(VillageBlueprints.ReadPoints(village.Zdo.GetByteArray(TakeDownKey)));
		}

		/// <summary>
		/// Notes where the sitting logs a village's vikings are to take down stand
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		/// <param name="spots">Where they stand</param>
		private static void WriteTakeDowns(Village village, List<Vector3> spots)
		{
			village.Zdo.Set(TakeDownKey, VillageBlueprints.WritePoints(spots));
		}

		/// <summary>
		/// Finds the sitting log no player built standing at a spot
		/// </summary>
		/// <param name="spot">The spot</param>
		/// <returns>The log, or null when none stands there or it isn't loaded</returns>
		public static ZNetView StandingLog(Vector3 spot)
		{
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				if (instance.Key.GetPrefab() == SeatHash && instance.Value != null && instance.Value.IsValid() && instance.Key.GetLong(ZDOVars.s_creator, 0L) == 0L && Vector3.Distance(instance.Key.GetPosition(), spot) <= TakeDownMatch)
				{
					return(instance.Value);
				}
			}
			return(null);
		}

		/// <summary>
		/// Takes down a sitting log, with the effect of a piece being taken down
		/// </summary>
		/// <param name="log">The log</param>
		/// <returns>The wood it's built from</returns>
		public static int TakeDown(ZNetView log)
		{
			WearNTear wear = log.GetComponent<WearNTear>();
			if (wear != null)
			{
				wear.m_destroyedEffect.Create(log.transform.position, log.transform.rotation, log.transform);
			}
			if (!log.IsOwner())
			{
				log.ClaimOwnership();
			}
			ZNetScene.instance.Destroy(log.gameObject);
			return(VillageRecords.WoodCost(SeatHash));
		}

		/// <summary>
		/// Checks whether a sitting log has room at a spot - on dry ground that's fairly flat under it, outside the
		/// houses, off the paths and with nothing in the way
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="spot">Where the log's origin would be</param>
		/// <param name="rotation">Which way it would face</param>
		/// <param name="size">Its size, around its origin</param>
		/// <param name="ground">The ground's height at the spot</param>
		/// <returns>True when it has room</returns>
		private static bool HasRoom(Village village, Vector3 spot, Quaternion rotation, Bounds size, out float ground)
		{
			ground = ZoneSystem.instance.GetGroundHeight(spot);
			float lowest = ground;
			float highest = ground;
			for (int corner = 0; corner < 4; corner++)
			{
				Vector3 local = size.center + new Vector3(((corner & 1) != 0) ? size.extents.x : -size.extents.x, 0f, ((corner & 2) != 0) ? size.extents.z : -size.extents.z);
				Vector3 point = spot + rotation * local;
				float height = ZoneSystem.instance.GetGroundHeight(point);
				lowest = Mathf.Min(lowest, height);
				highest = Mathf.Max(highest, height);
				if (village.InHouse(point, 0.5f) || OnPath(point))
				{
					return(false);
				}
			}
			if (highest - lowest > SeatFlatness || lowest < ZoneSystem.instance.m_waterLevel + DryHeight || OnPath(spot))
			{
				return(false);
			}
			if (roomMask == 0)
			{
				roomMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece");
			}
			Vector3 center = spot + rotation * size.center;
			center.y = highest + RoomLift + size.extents.y;
			return(!Physics.CheckBox(center, size.extents, rotation, roomMask, QueryTriggerInteraction.Ignore));
		}

		/// <summary>
		/// Checks whether a point is on painted ground, like a path
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>True when it is</returns>
		private static bool OnPath(Vector3 point)
		{
			Heightmap heightmap = Heightmap.FindHeightmap(point);
			return(heightmap != null && heightmap.IsCleared(point));
		}

		/// <summary>
		/// Flattens a direction onto the ground
		/// </summary>
		/// <param name="direction">The direction</param>
		/// <returns>It, level</returns>
		private static Vector3 Flat(Vector3 direction)
		{
			direction.y = 0f;
			return(direction);
		}
	}
}
