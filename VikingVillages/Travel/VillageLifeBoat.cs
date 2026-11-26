using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A living viking on the road crossing the sea in the world - its group gathering at the shore, the one of the
	/// hardest biome among them building the boats, the rest getting on board, riding to the far shore and swimming in
	/// (see VikingBoat)
	/// </summary>
	/// <remarks>
	/// Reaching the sea on its road, a viking waits at the shore for the rest of its group, for up to GatherMax seconds.
	/// The one of the hardest biome among them then takes out a hammer, swings it once and builds as many boats of
	/// the best kind their biome has as they need - each with one of them at the helm - from nothing. Each tries once to
	/// jump on board from the shore, and swims to the boat's ladder when it misses; once VikingBoat.BoardTime seconds
	/// have passed, those still off it are put on board. On board it sits where it's put until the boat reaches the far
	/// shore, then jumps off and swims in, the one at the helm breaking the boat up once it's ashore. A viking in the water
	/// - wrecked, or swimming in - swims as long as its stamina lasts, by its food and Swim skill, and drowns after that
	/// the way a player does, as any viking in the water does (see LivingVikingBody.OnSwimming). One wrecked plans its
	/// road again once it's ashore, building another boat when it still has sea to cross. Where there's no water deep
	/// enough for a boat by the shore, its group sets sail out of the world (see SetSail)
	/// </remarks>
	internal partial class VillageLife
	{
		/// <summary>
		/// How far along crossing the sea a viking is - noted on its ZDO (see VikingBoat.BoardKey), so it carries on after
		/// it loads again
		/// </summary>
		private enum Crossing
		{
			None = 0,
			Gathering = 1,
			Building = 2,
			Boarding = 3,
			Landing = 4,
			Aboard = VikingBoat.Aboard,
			Wrecked = 6,
			Breaking = 7,
			Overboard = VikingBoat.Overboard
		}

		/// <summary>
		/// How often a viking overboard treading water calls out to its boat, in seconds
		/// </summary>
		private const float TreadCallInterval = 20f;

		/// <summary>
		/// When a viking overboard treading water next calls out to its boat
		/// </summary>
		private float nextTreadCall;

		/// <summary>
		/// How a viking on board shoots at a flying enemy attacking the boat (see DefendBoat) - how far off the enemy can
		/// be, how near it counts as attacking whoever it's after, how far from its seat the viking can be knocked before
		/// it's sat back down, in meters, and how long it stays standing once the last enemy is gone, in seconds
		/// </summary>
		private const float BoatShootRange = 40f;
		private const float BoatThreatNear = 15f;
		private const float BoatStandDrift = 2f;
		private const float BoatShootSettle = 3f;

		/// <summary>
		/// Whether the viking is standing at its seat to shoot from the boat, when it last saw an enemy to shoot at, and
		/// when it can loose its next arrow, in Time.time seconds
		/// </summary>
		private bool standingToShoot;
		private float lastThreatAt;
		private float nextBoatShot;

		/// <summary>
		/// How long a viking at the shore waits for the rest of its group, how long it waits for a boat before it builds
		/// its own, and how long one loaded mid-voyage waits for its boat to load before it counts as wrecked, in seconds
		/// </summary>
		private const float GatherMax = 30f;
		private const float BoatWait = 60f;
		private const float RestoreWait = 20f;

		/// <summary>
		/// How the boats are built and broken up - how many times the viking swings its hammer, once, as a player builds or
		/// takes a piece down, and how long the swing takes to land, in seconds
		/// </summary>
		private const int BuildSwings = 1;
		private const float BuildSwingGap = 1.1f;

		/// <summary>
		/// How near the water's edge the builder has to get before it swings, in meters, and the longest it walks there
		/// before it builds from where it got to, in seconds
		/// </summary>
		private const float BuildReach = 1f;
		private const float BuildWalkMost = 15f;

		/// <summary>
		/// Where boats are put in the water - how far out from the shore the water is looked for and in what steps, how
		/// deep it has to be, how far past that the boat goes, and how far apart a group's boats are, in meters
		/// </summary>
		private const float LaunchSearch = 60f;
		private const float LaunchStep = 2f;
		private const float LaunchDepth = 1.8f;
		private const float LaunchOut = 4f;
		private const float BoatSpacing = 12f;

		/// <summary>
		/// How a viking gets on board - how near the water's edge it jumps from, how fast it jumps out and up, in meters a
		/// second, how long it gives a jump to land, in seconds, and how near the ladder or its seat it has to be to climb
		/// aboard, in meters
		/// </summary>
		private const float JumpReach = 2.5f;
		private const float JumpOut = 6f;
		private const float JumpUp = 6f;
		private const float JumpLand = 1.5f;
		private const float ClimbReach = 2.5f;

		/// <summary>
		/// How far out the nearest shore is looked for, in steps of how far, and how many ways, in meters, and how long a
		/// viking has to be out of the water to count as ashore, in seconds
		/// </summary>
		private const float ShoreSearch = 400f;
		private const float ShoreStep = 10f;
		private const int ShoreWays = 16;
		private const float AshoreTime = 1.5f;

		/// <summary>
		/// How far along crossing the sea the viking is, and since when, in Time.time seconds
		/// </summary>
		private Crossing crossing = Crossing.None;
		private float crossingSince;

		/// <summary>
		/// Since when a viking loaded mid-voyage has waited for its boat to load, in Time.time seconds, or -1
		/// </summary>
		private float resumeSince = -1f;

		/// <summary>
		/// The boat it crosses in and its seat on it, the helm being 0
		/// </summary>
		private VikingBoat myBoat;
		private int mySeat;

		/// <summary>
		/// For the builder, its group's IDs, where it puts the boats in the water, how many times it has swung its hammer
		/// and when it next swings
		/// </summary>
		private readonly List<string> buildCrew = new List<string>();
		private Vector3 launchSpot;

		/// <summary>
		/// For the builder, the water's edge on the way out to where the boats go in, which it walks to before it swings
		/// </summary>
		private Vector3 buildSpot;
		private int swings;
		private float nextSwing;

		/// <summary>
		/// Its jump on board - whether it has jumped, when, and the water's edge it jumps from
		/// </summary>
		private bool jumped;
		private float jumpedAt;
		private Vector3 waterEdge;
		private bool hasEdge;

		/// <summary>
		/// Its swim ashore - where it swims to, and how long it has been out of the water - drowning on the way as any
		/// viking in the water does once its stamina runs out (see LivingVikingBody.OnSwimming)
		/// </summary>
		private Vector3 swimTo;
		private float ashoreFor;

		/// <summary>
		/// How a player walking on the boat pushes a viking sitting on its boards - how near they have to come, how far from
		/// its seat it can be pushed, in meters, and how fast it's pushed and slides back, in meters a second
		/// </summary>
		private const float PushReach = 1f;
		private const float PushMost = 1.2f;
		private const float PushSpeed = 3f;
		private const float SlideBackSpeed = 0.6f;

		/// <summary>
		/// For a viking sitting on the boards, the spot of its own it's held at, which a player can push about, or null
		/// </summary>
		private Transform pushedSpot;

		/// <summary>
		/// Notes how far along crossing the sea the viking is, on it and its ZDO
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <param name="next">How far along it is now</param>
		private void SetCrossing(ZDO zdo, Crossing next)
		{
			crossing = next;
			crossingSince = Time.time;
			if (zdo.GetInt(VikingBoat.BoardKey) != (int)next)
			{
				zdo.Set(VikingBoat.BoardKey, (int)next);
			}
		}

		/// <summary>
		/// Gets the group the viking crosses the sea with - its boat's, once it's noted on one (see VikingBoat.KeyOf), like
		/// a quest's, and otherwise its group on the road, or itself alone
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>The group's name</returns>
		private string CrossingGroup(ZDO zdo)
		{
			string boat = zdo.GetString(VikingBoat.BoatKey, "");
			int mark = boat.LastIndexOf('#');
			if (mark > 0)
			{
				return(boat.Substring(0, mark));
			}
			string group = zdo.GetString(VillageTravel.GroupKey, "");
			return((group.Length > 0) ? group : "alone" + VikingIdentity.Get(zdo));
		}

		/// <summary>
		/// Has a viking that reached the sea on its road cross it in the world - waiting for its group, building the boats
		/// or getting on board - from the shore, until it's on board (see UpdateBoat)
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True, as it takes the update over</returns>
		private bool UpdateCrossing(float dt, ZDO zdo)
		{
			if (VikingIdentity.Ensure(nview).Length == 0)
			{
				return(SetSail(zdo));
			}
			if (crossing == Crossing.None)
			{
				SetCrossing(zdo, Crossing.Gathering);
				Plugin.Log.LogInfo(LogName() + " reached the sea at " + transform.position.ToString("F0") + ", and waits for its group to cross it");
			}
			if (crossing == Crossing.Gathering || crossing == Crossing.Building)
			{
				VikingBoat boat = VikingBoat.FindFor(CrossingGroup(zdo), VikingIdentity.Get(zdo), out int seat);
				if (boat != null)
				{
					myBoat = boat;
					mySeat = seat;
					jumped = false;
					hasEdge = false;
					HoldHammer(false);
					zdo.Set(VikingBoat.BoatKey, boat.Key);
					SetCrossing(zdo, Crossing.Boarding);
				}
			}
			switch (crossing)
			{
				case Crossing.Gathering:
					return(Gather(zdo));
				case Crossing.Building:
					return(BuildBoats(dt, zdo));
				case Crossing.Boarding:
					return(Board(dt, zdo));
			}
			return(true);
		}

		/// <summary>
		/// Lists the vikings of the viking's group loaded here near it on the same road, itself included
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>The vikings</returns>
		private List<VillageLife> CrossingMembers(ZDO zdo)
		{
			List<VillageLife> members = new List<VillageLife> { this };
			string group = zdo.GetString(VillageTravel.GroupKey, "");
			if (group.Length == 0)
			{
				return(members);
			}
			Vector3 destination = zdo.GetVec3(VillageTravel.DestinationKey, Vector3.zero);
			foreach (VillageLife other in All)
			{
				if (other == this || other.nview == null || !other.nview.IsValid() || other.humanoid == null || other.humanoid.IsDead())
				{
					continue;
				}
				ZDO theirs = other.nview.GetZDO();
				if (theirs.GetString(VillageTravel.GroupKey, "") == group && theirs.GetVec3(VillageTravel.DestinationKey, Vector3.zero) == destination && Utils.DistanceXZ(other.transform.position, transform.position) <= GroupWaitReach)
				{
					members.Add(other);
				}
			}
			return(members);
		}

		/// <summary>
		/// Waits at the shore for the rest of the group, facing the water - until they're all there or GatherMax seconds
		/// have passed, when the one of the hardest biome among those there starts building the boats
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True, as it takes the update over</returns>
		private bool Gather(ZDO zdo)
		{
			mover = "waiting at the shore for the rest of its group";
			Face(travelRoad.Points[Mathf.Min(roadLeg, travelRoad.Count - 1)]);
			List<VillageLife> members = CrossingMembers(zdo);
			List<VillageLife> there = members.FindAll((VillageLife member) => member.crossing != Crossing.None);
			if (there.Count < members.Count && Time.time - crossingSince < GatherMax)
			{
				return(true);
			}
			List<string> ids = there.ConvertAll((VillageLife member) => VikingIdentity.Get(member.nview.GetZDO()));
			List<int> biomes = there.ConvertAll((VillageLife member) => member.OwnBiome());
			int builder = BoatRules.Builder(ids, biomes);
			if (builder < 0 || there[builder] != this)
			{
				// One left waiting on a builder that never came builds its own
				if (Time.time - crossingSince < BoatWait)
				{
					return(true);
				}
				ids = new List<string> { VikingIdentity.Get(zdo) };
			}
			if (!FindLaunch(out launchSpot))
			{
				Plugin.Log.LogInfo(LogName() + " found no water deep enough for a boat by the shore at " + transform.position.ToString("F0") + ", so its group sets sail out of the world");
				return(SetSail(zdo));
			}
			buildCrew.Clear();
			buildCrew.AddRange(ids);
			buildSpot = WaterEdge(transform.position, launchSpot);
			swings = 0;
			nextSwing = Time.time + 0.5f;
			SetCrossing(zdo, Crossing.Building);
			VikingSpeech.Say(humanoid, VikingRoadSpeech.Build(TravelTime.BoatName(CrossingBoat(zdo, buildCrew.Count))), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
			return(true);
		}

		/// <summary>
		/// How far apart the points along the sea ahead are looked at for water that boils, in meters - half a square of
		/// the road's map (see TravelRoute.CellSize)
		/// </summary>
		private const float BurnStep = 16f;

		/// <summary>
		/// Gets the biome the boats for the viking's crossing are picked by (see TravelTime.CrossingBoatBiome) - the best
		/// boat of its group's hardest biome, or a karve for one crossing alone, unless the sea ahead of it boils
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <param name="crew">How many cross</param>
		/// <returns>The biome, by VikingGear's order</returns>
		private int CrossingBoat(ZDO zdo, int crew)
		{
			return(TravelTime.CrossingBoatBiome(VillageTravel.BoatBiome(zdo), crew, BurningAhead()));
		}

		/// <summary>
		/// Checks whether the sea legs of the viking's road from its next waypoint on cross water that boils and burns a
		/// boat not made for it, the way the road's planning reads it (see TravelPlanner.Sample)
		/// </summary>
		/// <returns>True when they do</returns>
		private bool BurningAhead()
		{
			if (WorldGenerator.instance == null)
			{
				return(false);
			}
			BoatRules.Ashore(travelRoad, roadLeg, out int lastSea);
			Vector3 from = transform.position;
			for (int i = roadLeg; i <= lastSea && i < travelRoad.Count; i++)
			{
				Vector3 to = travelRoad.Points[i];
				int samples = Mathf.Max(1, Mathf.CeilToInt(Utils.DistanceXZ(from, to) / BurnStep));
				for (int s = 1; s <= samples; s++)
				{
					Vector3 point = Vector3.Lerp(from, to, (float)s / samples);
					if (WorldGenerator.GetAshlandsOceanGradient(point.x, point.z) > 0f)
					{
						return(true);
					}
				}
				from = to;
			}
			return(false);
		}

		/// <summary>
		/// Finds where a boat goes in the water - the first spot out from the shore toward the sea legs of the road where
		/// the water is deep enough, a little past it, on the water
		/// </summary>
		/// <param name="spot">Set to the spot</param>
		/// <returns>False when there's no water deep enough within LaunchSearch</returns>
		private bool FindLaunch(out Vector3 spot)
		{
			spot = Vector3.zero;
			if (roadLeg >= travelRoad.Count)
			{
				return(false);
			}
			return(FindLaunchToward(transform.position, travelRoad.Points[roadLeg], out spot));
		}

		/// <summary>
		/// Finds where a boat goes in the water out from a spot - the first spot toward a point where the water is deep
		/// enough, a little past it, on the water
		/// </summary>
		/// <param name="from">Where it's looked for from, at the shore</param>
		/// <param name="toward">The point it's looked for toward, out at sea</param>
		/// <param name="spot">Set to the spot</param>
		/// <returns>False when there's no water deep enough within LaunchSearch</returns>
		private static bool FindLaunchToward(Vector3 from, Vector3 toward, out Vector3 spot)
		{
			spot = Vector3.zero;
			if (ZoneSystem.instance == null)
			{
				return(false);
			}
			float water = ZoneSystem.instance.m_waterLevel;
			Vector3 way = toward - from;
			way.y = 0f;
			if (way.sqrMagnitude < 0.01f)
			{
				return(false);
			}
			way.Normalize();
			for (float out1 = LaunchStep; out1 <= LaunchSearch; out1 += LaunchStep)
			{
				Vector3 point = from + way * out1;
				if (ZoneSystem.instance.GetGroundHeight(point) < water - LaunchDepth)
				{
					spot = point + way * LaunchOut;
					spot.y = water;
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// How far out a quest's boat has to be, in meters, for its vikings to be gone with the quest once no player is
		/// within QuestSightReach of it, rather than only at BoatRules.QuestSailAway, and how often they look, in seconds
		/// </summary>
		private const float QuestSightOut = 40f;
		private const float QuestSightReach = 80f;
		private const float SailCheckInterval = 1f;

		/// <summary>
		/// When a viking on a quest's boat next looks whether it has sailed far enough to be gone (see SailedAway)
		/// </summary>
		private float nextSailCheck;

		/// <summary>
		/// Starts a village's viking getting on board the boat its quest sails in (see Board)
		/// </summary>
		/// <param name="boat">The boat</param>
		/// <param name="seat">Its seat on it, the helm being 0</param>
		private void StartQuestBoarding(VikingBoat boat, int seat)
		{
			ZDO zdo = nview.GetZDO();
			myBoat = boat;
			mySeat = seat;
			jumped = false;
			hasEdge = false;
			HoldHammer(false);
			zdo.Set(VikingBoat.BoatKey, boat.Key);
			SetCrossing(zdo, Crossing.Boarding);
			Plugin.Log.LogInfo(LogName() + " gets on board " + boat.Key + " to leave on its village's quest");
		}

		/// <summary>
		/// Checks whether a village's viking on a quest's boat setting out has sailed far enough to be gone with the quest -
		/// BoatRules.QuestSailAway from the shore it set sail from, or QuestSightOut once no player is near enough to see
		/// it, looking every SailCheckInterval seconds
		/// </summary>
		/// <returns>True when it has</returns>
		private bool SailedAway()
		{
			if (myBoat == null || !myBoat.OnQuest() || myBoat.Homeward() || myBoat.CurrentPhase != VikingBoat.Phase.Sailing || Time.time < nextSailCheck)
			{
				return(false);
			}
			nextSailCheck = Time.time + SailCheckInterval;
			float distance = Utils.DistanceXZ(myBoat.transform.position, myBoat.Launch);
			if (distance >= BoatRules.QuestSailAway)
			{
				return(true);
			}
			return(distance >= QuestSightOut && !Player.IsPlayerInRange(myBoat.transform.position, QuestSightReach));
		}

		/// <summary>
		/// Has a village's viking sail off on its quest from the boat it set out in - off its seat and gone from the world
		/// with the quest (see LeaveOnQuest), its boat following once its crew is all gone (see VikingBoat). One whose
		/// quest is no longer setting out swims for the shore instead
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		private void SailOffOnQuest(ZDO zdo)
		{
			QuestRecord record = VillageQuests.Read(village);
			string me = VikingIdentity.Get(zdo);
			StopDefending(false);
			GetOffBoard();
			if (record == null || record.Phase != QuestPhase.Leaving || !record.Members.Contains(me))
			{
				StartSwim(zdo, Crossing.Wrecked, Vector3.zero);
				return;
			}
			zdo.Set(VikingBoat.BoatKey, "");
			SetCrossing(zdo, Crossing.None);
			myBoat = null;
			LeaveOnQuest(record);
		}

		/// <summary>
		/// Walks to the water's edge, as close to where the boats go in as it can stand, then swings the hammer at the
		/// water once and builds the group's boats - as many as they need, each with its share of the group on board and
		/// the first of them at the helm. One that can't get to the edge, or hasn't in BuildWalkMost seconds, builds from
		/// where it got to
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True, as it takes the update over</returns>
		private bool BuildBoats(float dt, ZDO zdo)
		{
			if (swings == 0 && Utils.DistanceXZ(transform.position, buildSpot) > BuildReach && Time.time - crossingSince < BuildWalkMost)
			{
				mover = "walking to the water's edge to build a boat";
				nextSwing = Mathf.Max(nextSwing, Time.time + 0.3f);
				if (Go(dt, buildSpot, BuildReach / 2f, true))
				{
					// There, or as near as it can get, it builds from where it stands
					moveFailed = false;
					buildSpot = transform.position;
				}
				return(true);
			}
			mover = "building a boat";
			Face(launchSpot);
			HoldHammer(true);
			if (Time.time < nextSwing)
			{
				return(true);
			}
			if (swings < BuildSwings)
			{
				VikingPoses.Swing(humanoid);
				swings++;
				nextSwing = Time.time + BuildSwingGap;
				return(true);
			}
			HoldHammer(false);
			if (!LaunchBoats(zdo))
			{
				return(SetSail(zdo));
			}
			// Found on the next update, as every viking of the group finds its own (see UpdateCrossing)
			return(true);
		}

		/// <summary>
		/// Puts the group's boats in the water side by side, each sailing the sea legs of the road from here - the best
		/// boat of the group's hardest biome, or a karve for one crossing alone (see CrossingBoat)
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>False when the boat's prefab is missing</returns>
		private bool LaunchBoats(ZDO zdo)
		{
			int biome = CrossingBoat(zdo, buildCrew.Count);
			GameObject prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(TravelTime.BoatPrefab(biome)) : null);
			if (prefab == null)
			{
				return(false);
			}
			int ashore = BoatRules.Ashore(travelRoad, roadLeg, out int lastSea);
			TravelRoad route = new TravelRoad();
			route.Add(launchSpot, TravelLeg.Foot);
			for (int i = roadLeg; i <= lastSea && i < travelRoad.Count; i++)
			{
				route.Add(travelRoad.Points[i], TravelLeg.Sea);
			}
			Vector3 land = ((ashore < travelRoad.Count) ? travelRoad.Points[ashore] : zdo.GetVec3(VillageTravel.DestinationKey, travelRoad.Points[travelRoad.Count - 1]));
			Vector3 facing = ((route.Count > 1) ? route.Points[1] : land) - launchSpot;
			facing.y = 0f;
			Vector3 side = Vector3.Cross(Vector3.up, (facing.sqrMagnitude > 0.01f) ? facing.normalized : transform.forward);
			List<List<string>> crews = BoatRules.Crews(buildCrew, TravelTime.BoatCrew(biome));
			string group = CrossingGroup(zdo);
			for (int i = 0; i < crews.Count; i++)
			{
				// The first boat goes where the water was found, the rest to either side of it in turn
				float offset = ((i + 1) / 2) * ((i % 2 == 1) ? 1f : -1f) * BoatSpacing;
				Vector3 spot = launchSpot + side * offset;
				VikingBoat.Build(prefab, spot, facing, group, i, crews[i], route, land, roadLeg, ashore);
			}
			Plugin.Log.LogInfo(LogName() + " built " + crews.Count + " " + TravelTime.BoatName(biome) + ((crews.Count == 1) ? "" : "s") + " for " + buildCrew.Count + " vikings at " + launchSpot.ToString("F0") + ", to sail " + route.Count + " waypoints to " + land.ToString("F0"));
			TestLog.Count("Boats", "built", crews.Count);
			return(true);
		}

		/// <summary>
		/// Gets the viking on board its boat - walking to the water's edge nearest it and jumping once, swimming to its
		/// ladder when it misses, or put on board once VikingBoat.BoardTime seconds have passed
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True, as it takes the update over</returns>
		private bool Board(float dt, ZDO zdo)
		{
			if (myBoat == null)
			{
				myBoat = VikingBoat.FindFor(CrossingGroup(zdo), VikingIdentity.Get(zdo), out mySeat);
				if (myBoat == null)
				{
					// Its boat broke or unloaded before it got on
					SetCrossing(zdo, Crossing.Gathering);
					return(true);
				}
			}
			// Timed from when it came to board as well as from the boat's launch, so one coming late to a quest's boat that
			// has waited long for its crew still gets its try
			if (myBoat.CurrentPhase != VikingBoat.Phase.Boarding || Mathf.Min(myBoat.PhaseSeconds, Time.time - crossingSince) > VikingBoat.BoardTime)
			{
				GetAboard(zdo, "was put");
				return(true);
			}
			Vector3 boat = myBoat.transform.position;
			if (!jumped)
			{
				if (!hasEdge)
				{
					waterEdge = WaterEdge(transform.position, boat);
					hasEdge = true;
				}
				if (humanoid.InWater() || Utils.DistanceXZ(transform.position, waterEdge) <= JumpReach)
				{
					Vector3 toward = boat - transform.position;
					toward.y = 0f;
					Face(boat);
					humanoid.m_body.linearVelocity = toward.normalized * JumpOut + Vector3.up * JumpUp;
					jumped = true;
					jumpedAt = Time.time;
					mover = "jumping on board";
					return(true);
				}
				mover = "walking to its boat";
				Go(dt, waterEdge, JumpReach / 2f, true);
				moveFailed = false;
				return(true);
			}
			if (Time.time - jumpedAt < JumpLand)
			{
				return(true);
			}
			if (humanoid.GetStandingOnShip() == myBoat.Ship || Vector3.Distance(transform.position, myBoat.Seat(mySeat).position) < ClimbReach)
			{
				GetAboard(zdo, "jumped");
				return(true);
			}
			Vector3 ladder = myBoat.Ladder(transform.position);
			if (Vector3.Distance(transform.position, ladder) < ClimbReach)
			{
				GetAboard(zdo, "climbed");
				return(true);
			}
			mover = "swimming to its boat's ladder";
			Vector3 way = ladder - transform.position;
			way.y = 0f;
			ai.MoveTowards(way.normalized, true);
			return(true);
		}

		/// <summary>
		/// Finds the water's edge between a viking and its boat - the last dry spot on the way out to it
		/// </summary>
		/// <param name="from">Where the viking is</param>
		/// <param name="boat">Where the boat is</param>
		/// <returns>The spot, or where the viking is when it's in the water already</returns>
		private static Vector3 WaterEdge(Vector3 from, Vector3 boat)
		{
			if (ZoneSystem.instance == null)
			{
				return(from);
			}
			float water = ZoneSystem.instance.m_waterLevel;
			Vector3 toward = boat - from;
			toward.y = 0f;
			float length = toward.magnitude;
			if (length < 0.01f)
			{
				return(from);
			}
			toward /= length;
			Vector3 edge = from;
			for (float out1 = 1f; out1 < length; out1 += 1f)
			{
				Vector3 point = from + toward * out1;
				if (ZoneSystem.instance.GetGroundHeight(point) < water - 0.3f)
				{
					break;
				}
				edge = point;
			}
			return(edge);
		}

		/// <summary>
		/// Sits a viking just spawned on board a boat at once, in its seat's pose, for the kri_vv_spawnboat command - its
		/// ZDO already noting the boat (see VikingBoat.BoatKey)
		/// </summary>
		/// <param name="boat">The boat</param>
		/// <param name="seat">The index of its seat, the helm being 0</param>
		public void SeatAt(VikingBoat boat, int seat)
		{
			myBoat = boat;
			mySeat = seat;
			SitOnBoard();
			crossing = Crossing.Aboard;
			crossingSince = Time.time;
		}

		/// <summary>
		/// Sits the viking at its seat on its boat, in the seat's pose
		/// </summary>
		private void SitOnBoard()
		{
			Transform seat = myBoat.Seat(mySeat);
			string pose = myBoat.SeatPose(mySeat);
			ClearPushedSpot();
			if (pose == VikingPoses.FloorPose)
			{
				// Sitting on the boards, it sits at a spot of its own a player can push about (see UpdatePush)
				GameObject spot = new GameObject("vikingPushed");
				spot.transform.SetParent(myBoat.transform, false);
				spot.transform.position = seat.position;
				spot.transform.rotation = seat.rotation;
				pushedSpot = spot.transform;
				seat = pushedSpot;
			}
			Attach(seat, myBoat.gameObject);
			VikingPoses.SetShipPose(humanoid, pose, true);
		}

		/// <summary>
		/// Gets the viking up from its seat on its boat, or wherever it was held when its boat is gone
		/// </summary>
		private void GetOffBoard()
		{
			Detach();
			ClearPushedSpot();
			VikingPoses.SetShipPose(humanoid, "", false);
		}

		/// <summary>
		/// Takes away the spot a viking sitting on the boards was pushed about at
		/// </summary>
		private void ClearPushedSpot()
		{
			if (pushedSpot != null)
			{
				Object.Destroy(pushedSpot.gameObject);
			}
			pushedSpot = null;
		}

		/// <summary>
		/// Lets a player walking on the boat push a viking sitting on its boards out of the way - sliding it along the deck,
		/// still sitting, away from them, up to PushMost from its seat and never onto anything of the boat's or off the
		/// deck it sits on (see VikingBoat.Roomy) - and slides it slowly back to its seat once no player is that near
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		private void UpdatePush(float dt)
		{
			if (pushedSpot == null || myBoat == null)
			{
				return;
			}
			Transform boat = myBoat.transform;
			Vector3 home = boat.InverseTransformPoint(myBoat.Seat(mySeat).position);
			Vector3 now = pushedSpot.localPosition;
			Player pusher = null;
			Vector3 pusherAt = Vector3.zero;
			float nearest = PushReach;
			foreach (Player player in Player.GetAllPlayers())
			{
				if (player == null || player.IsDead() || !myBoat.Ship.IsPlayerInBoat(player))
				{
					continue;
				}
				Vector3 at = boat.InverseTransformPoint(player.transform.position);
				float distance = Flat2(at, now);
				if (distance < nearest)
				{
					nearest = distance;
					pusher = player;
					pusherAt = at;
				}
			}
			Vector3 target = home;
			float speed = SlideBackSpeed;
			if (pusher != null)
			{
				Vector3 away = now - pusherAt;
				away.y = 0f;
				if (away.sqrMagnitude < 0.0001f)
				{
					away = now - home;
					away.y = 0f;
				}
				away = ((away.sqrMagnitude > 0.0001f) ? away.normalized : Vector3.right);
				target = pusherAt + away * PushReach;
				Vector3 fromHome = target - home;
				fromHome.y = 0f;
				target = home + Vector3.ClampMagnitude(fromHome, PushMost);
				speed = PushSpeed;
			}
			Vector3 flatNow = new Vector3(now.x, 0f, now.z);
			Vector3 next = Vector3.MoveTowards(flatNow, new Vector3(target.x, 0f, target.z), speed * dt);
			if ((next - flatNow).sqrMagnitude < 0.000001f)
			{
				return;
			}
			if (myBoat.Roomy(next, home.y, out float height))
			{
				next.y = height;
			}
			else if (pusher != null)
			{
				// Something's in the way, or the deck's edge, so it stays where it was pushed to
				return;
			}
			else
			{
				next.y = home.y;
			}
			pushedSpot.localPosition = next;
		}

		/// <summary>
		/// Measures the distance between two points across a boat's deck, leaving out height
		/// </summary>
		/// <param name="a">One point, in the boat's space</param>
		/// <param name="b">The other</param>
		/// <returns>The distance, in meters</returns>
		private static float Flat2(Vector3 a, Vector3 b)
		{
			float dx = a.x - b.x;
			float dz = a.z - b.z;
			return(Mathf.Sqrt(dx * dx + dz * dz));
		}

		/// <summary>
		/// Seats the viking on board its boat, at the helm for the first of its crew
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <param name="how">How it got on, for the log</param>
		private void GetAboard(ZDO zdo, string how)
		{
			StopDefending(false);
			SitOnBoard();
			zdo.Set(VikingBoat.BoatKey, myBoat.Key);
			SetCrossing(zdo, Crossing.Aboard);
			int built = TravelTime.BiomeOfBoat(Utils.GetPrefabName(myBoat.gameObject));
			string boat = TravelTime.BoatName((built >= 0) ? built : VillageTravel.BoatBiome(zdo));
			TripStory.Note(zdo, (TripStory story) => story.Boat = boat);
			Plugin.Log.LogInfo(LogName() + " " + how + " on board " + myBoat.Key + ((mySeat == 0) ? ", at the helm" : ""));
			TestLog.Count("Boats", "vikings " + how + " on board");
		}

		/// <summary>
		/// Runs the viking's crossing once it's on board or back in the water - riding along, getting off at the far shore,
		/// swimming in and breaking the boat up - and picks up a crossing it was in the middle of as it loads again. A
		/// village's viking runs its quest's crossing here too - getting on board as it sets out, sailing off on the quest
		/// once it's far enough out (see SailedAway), and riding home from it
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True when it took the update over</returns>
		private bool UpdateBoat(float dt)
		{
			ZDO zdo = nview.GetZDO();
			if (crossing == Crossing.None && !Resume(zdo))
			{
				return(false);
			}
			switch (crossing)
			{
				case Crossing.Boarding:
					// A village's viking boards a quest's boat here, as one on the road boards in UpdateCrossing
					return(Board(dt, zdo));
				case Crossing.Aboard:
					if (active && SailedAway())
					{
						SailOffOnQuest(zdo);
						return(true);
					}
					return(RideAlong(dt, zdo));
				case Crossing.Landing:
				case Crossing.Wrecked:
					return(SwimAshore(dt, zdo));
				case Crossing.Overboard:
					return(SwimBack(zdo));
				case Crossing.Breaking:
					return(BreakBoat(zdo));
			}
			return(false);
		}

		/// <summary>
		/// Picks up the crossing a viking was in the middle of when it last unloaded - back on its seat once its boat
		/// loads, held where it is meanwhile, or swimming ashore when its boat doesn't load within RestoreWait seconds
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True when it was in the middle of one it's still on board or in the water for</returns>
		private bool Resume(ZDO zdo)
		{
			Crossing saved = (Crossing)zdo.GetInt(VikingBoat.BoardKey);
			if (saved == Crossing.None)
			{
				return(false);
			}
			if (saved == Crossing.Overboard)
			{
				myBoat = VikingBoat.FindFor(CrossingGroup(zdo), VikingIdentity.Get(zdo), out mySeat);
				if (myBoat != null)
				{
					crossing = Crossing.Overboard;
					crossingSince = Time.time;
					resumeSince = -1f;
					return(true);
				}
				resumeSince = ((resumeSince < 0f) ? Time.time : resumeSince);
				if (Time.time - resumeSince < RestoreWait)
				{
					// Treading water until its boat loads
					mover = "waiting for its boat to load";
					ai.StopMoving();
					return(true);
				}
				resumeSince = -1f;
				StartSwim(zdo, Crossing.Wrecked, Vector3.zero);
				return(true);
			}
			if (saved != Crossing.Aboard && saved != Crossing.Landing && saved != Crossing.Wrecked)
			{
				// It was still ashore, so it gathers with its group again
				zdo.Set(VikingBoat.BoardKey, (int)Crossing.None);
				zdo.Set(VikingBoat.BoatKey, "");
				return(false);
			}
			if (saved == Crossing.Aboard)
			{
				myBoat = VikingBoat.FindFor(CrossingGroup(zdo), VikingIdentity.Get(zdo), out mySeat);
				if (myBoat != null)
				{
					SitOnBoard();
					crossing = Crossing.Aboard;
					crossingSince = Time.time;
					resumeSince = -1f;
					Plugin.Log.LogInfo(LogName() + " is back on board " + myBoat.Key + " as it loads");
					return(true);
				}
				if (resumeSince < 0f)
				{
					resumeSince = Time.time;
				}
				if (Time.time - resumeSince < RestoreWait)
				{
					// Held where it was on the deck until its boat loads under it
					mover = "waiting for its boat to load";
					ai.StopMoving();
					humanoid.m_body.useGravity = false;
					humanoid.m_body.linearVelocity = Vector3.zero;
					return(true);
				}
				humanoid.m_body.useGravity = true;
				resumeSince = -1f;
			}
			StartSwim(zdo, Crossing.Wrecked, Vector3.zero);
			return(true);
		}

		/// <summary>
		/// Rides along on the boat, keeping its place on the road with the boat's - and gets off at the far shore, or swims
		/// for it when the boat is gone from under it. Knocked out of its seat, like by a fight, it sits back down while
		/// it's still on board, and in the water while the boat sails on, it's overboard and swims back to it (see
		/// SwimBack). With a flying enemy attacking the boat, it stands at its seat and shoots at it (see DefendBoat)
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True, as it takes the update over</returns>
		private bool RideAlong(float dt, ZDO zdo)
		{
			if (myBoat != null && attachPoint == null && !standingToShoot && myBoat.CurrentPhase != VikingBoat.Phase.Landing)
			{
				if (humanoid.GetStandingOnShip() == myBoat.Ship || Vector3.Distance(transform.position, myBoat.Seat(mySeat).position) < ClimbReach)
				{
					SitOnBoard();
					return(true);
				}
				GetOffBoard();
				Plugin.Log.LogInfo(LogName() + " went overboard from " + myBoat.Key + " at " + transform.position.ToString("F0"));
				TestLog.Count("Boats", "vikings overboard");
				VikingSpeech.SayFirst(humanoid, VikingRoadSpeech.HitWater(), VikingSpeech.CallTime, VikingSpeech.CallCull);
				// The helm calls out next, as the boat comes about, and only after that does it call out treading water
				nextTreadCall = Time.time + VikingSpeech.CallTime + 4f;
				SetCrossing(zdo, Crossing.Overboard);
				return(true);
			}
			if (myBoat == null)
			{
				StopDefending(false);
				GetOffBoard();
				Plugin.Log.LogInfo(LogName() + " lost its boat at sea at " + transform.position.ToString("F0") + ", and swims for the shore");
				TestLog.Count("Boats", "vikings wrecked");
				TripStory.Note(zdo, (TripStory story) => story.Wrecked = true);
				VikingSpeech.Say(humanoid, VikingRoadSpeech.Wrecked(), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
				StartSwim(zdo, Crossing.Wrecked, Vector3.zero);
				return(true);
			}
			if (myBoat.CurrentPhase == VikingBoat.Phase.Landing || !DefendBoat())
			{
				mover = ((mySeat == 0) ? "at the helm" : "on board");
				// Anything that moved it, like its AI before it was seated or its gear going on, stands it back up
				ai.StopMoving();
				string pose = myBoat.SeatPose(mySeat);
				if (!VikingPoses.InShipPose(humanoid, pose))
				{
					VikingPoses.SetShipPose(humanoid, pose, true);
				}
				UpdatePush(dt);
			}
			int leg = Mathf.Clamp(myBoat.SeaStart + myBoat.Leg - 1, 0, travelRoad.Count);
			if (leg != roadLeg)
			{
				roadLeg = leg;
				zdo.Set(VillageTravel.LegKey, roadLeg);
			}
			if (myBoat.CurrentPhase == VikingBoat.Phase.Landing)
			{
				StopDefending(false);
				GetOffBoard();
				Vector3 toward = myBoat.Land - transform.position;
				toward.y = 0f;
				humanoid.m_body.linearVelocity = toward.normalized * JumpOut + Vector3.up * JumpUp;
				StartSwim(zdo, Crossing.Landing, myBoat.Land);
			}
			return(true);
		}

		/// <summary>
		/// Shoots at a flying enemy attacking the boat - for any of its crew with a bow but the one at the helm, which
		/// steers on: standing up at its seat, its bow drawn, aiming ahead of the enemy and over it for the arrow's drop,
		/// loosing an arrow every BirdShotInterval seconds, and sitting back down BoatShootSettle seconds after the last
		/// enemy is gone. It never leaves the boat for it - knocked more than BoatStandDrift from its seat or off the deck,
		/// it's sat back down there at once
		/// </summary>
		/// <returns>True while it's standing to shoot, so it isn't held in its seat</returns>
		private bool DefendBoat()
		{
			Character threat = ((mySeat != 0 && HasBow(humanoid)) ? FlyingThreat() : null);
			if (threat != null)
			{
				lastThreatAt = Time.time;
			}
			else if (!standingToShoot || Time.time - lastThreatAt > BoatShootSettle)
			{
				StopDefending(true);
				return(false);
			}
			if (!standingToShoot)
			{
				GetOffBoard();
				standingToShoot = true;
				Plugin.Log.LogInfo(LogName() + " stands up on " + myBoat.Key + " to shoot at " + TestLog.Name(threat));
				TestLog.Count("Boats", "vikings shooting from a boat");
			}
			if (Vector3.Distance(transform.position, myBoat.Seat(mySeat).position) > BoatStandDrift || (humanoid.InWater() && humanoid.GetStandingOnShip() != myBoat.Ship))
			{
				// Kept on board, put back at its seat
				StopDefending(true);
				return(false);
			}
			aimingBow = true;
			ai.StopMoving();
			if (threat == null)
			{
				mover = "standing on deck, bow ready";
				return(true);
			}
			mover = "standing on deck shooting at " + threat.GetHoverName();
			if (viking != null && !VikingCore.IsRanged(humanoid.GetCurrentWeapon()))
			{
				viking.EquipWeapon();
			}
			Vector3 aim = BirdAim(threat.GetCenterPoint(), threat.GetVelocity());
			ai.LookAt(aim);
			if (Time.time >= nextBoatShot && ReadyToShoot(aim) && humanoid.StartAttack(null, false))
			{
				nextBoatShot = Time.time + BirdShotInterval;
				TestLog.Count("Boats", "arrows shot from a boat");
			}
			return(true);
		}

		/// <summary>
		/// Puts the bow down after shooting from the boat, sitting back down at its seat unless it's getting off the boat
		/// </summary>
		/// <param name="sit">True to sit back down</param>
		private void StopDefending(bool sit)
		{
			if (!standingToShoot)
			{
				return;
			}
			standingToShoot = false;
			aimingBow = false;
			if (sit && myBoat != null)
			{
				SitOnBoard();
			}
		}

		/// <summary>
		/// Finds the nearest flying enemy attacking the viking's boat - within BoatShootRange, after one of its crew on
		/// board or a player on it, or come within BoatThreatNear of the viking
		/// </summary>
		/// <returns>The enemy, or null when there's none</returns>
		private Character FlyingThreat()
		{
			Character nearest = null;
			float best = BoatShootRange;
			foreach (Character other in Character.GetAllCharacters())
			{
				if (other == null || other.IsDead() || !other.IsFlying() || !BaseAI.IsEnemy(humanoid, other))
				{
					continue;
				}
				float distance = Vector3.Distance(other.transform.position, transform.position);
				if (distance > best)
				{
					continue;
				}
				Character after = ((other.GetBaseAI() is MonsterAI theirs) ? theirs.GetTargetCreature() : null);
				if (distance <= BoatThreatNear || (after != null && OnMyBoat(after)))
				{
					nearest = other;
					best = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Checks whether someone is on the viking's boat - a player standing on it, or one of its crew sitting on board
		/// </summary>
		/// <param name="who">Who it is</param>
		/// <returns>True when they are</returns>
		private bool OnMyBoat(Character who)
		{
			if (myBoat == null)
			{
				return(false);
			}
			if (who is Player player)
			{
				return(myBoat.Ship.IsPlayerInBoat(player));
			}
			ZDO theirs = ((who.m_nview != null && who.m_nview.IsValid()) ? who.m_nview.GetZDO() : null);
			return(theirs != null && theirs.GetString(VikingBoat.BoatKey, "") == myBoat.Key && theirs.GetInt(VikingBoat.BoardKey) == VikingBoat.Aboard);
		}

		/// <summary>
		/// Starts the viking swimming for the shore - to a spot given, or the nearest dry ground
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <param name="why">Landing at the far shore, or wrecked</param>
		/// <param name="to">Where it swims to, or zero for the nearest dry ground</param>
		private void StartSwim(ZDO zdo, Crossing why, Vector3 to)
		{
			swimTo = ((to != Vector3.zero) ? to : NearestShore(transform.position));
			ashoreFor = 0f;
			SetCrossing(zdo, why);
		}

		/// <summary>
		/// Gets a viking overboard back on its boat, which comes about for it (see VikingBoat.UpdateSailing) - swimming
		/// for the nearest ladder when its stamina will get it there (see BoatRules.SwimsForBoat), and otherwise treading
		/// water, which takes no stamina, calling out now and then until the boat comes near enough. Back at the ladder it
		/// climbs aboard to its seat. It swims ashore once the boat reaches the far shore, and for the nearest shore when
		/// the boat is gone
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True, as it takes the update over</returns>
		private bool SwimBack(ZDO zdo)
		{
			if (myBoat == null)
			{
				Plugin.Log.LogInfo(LogName() + " lost its boat while overboard at " + transform.position.ToString("F0") + ", and swims for the shore");
				StartSwim(zdo, Crossing.Wrecked, Vector3.zero);
				return(true);
			}
			if (myBoat.CurrentPhase == VikingBoat.Phase.Landing)
			{
				StartSwim(zdo, Crossing.Landing, myBoat.Land);
				return(true);
			}
			Vector3 ladder = myBoat.Ladder(transform.position);
			float distance = Vector3.Distance(transform.position, ladder);
			if (distance < ClimbReach || humanoid.GetStandingOnShip() == myBoat.Ship)
			{
				GetAboard(zdo, "climbed back");
				VikingSpeech.Say(humanoid, VikingRoadSpeech.BackAboard(), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
				return(true);
			}
			float left = ((body != null) ? BoatRules.SwimMetersLeft(body.Stamina(), humanoid.GetSkillFactor(Skills.SkillType.Swim), Game.m_moveStaminaRate, humanoid.m_swimSpeed) : float.MaxValue);
			if (!humanoid.InWater() || BoatRules.SwimsForBoat(left, distance))
			{
				mover = "swimming back to its boat";
				Vector3 way = ladder - transform.position;
				way.y = 0f;
				ai.MoveTowards(way.normalized, true);
				return(true);
			}
			mover = "treading water for its boat to come";
			ai.StopMoving();
			Face(ladder);
			if (Time.time >= nextTreadCall)
			{
				nextTreadCall = Time.time + TreadCallInterval;
				VikingSpeech.Say(humanoid, VikingRoadSpeech.TreadWater(), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
			}
			return(true);
		}

		/// <summary>
		/// Finds the nearest dry ground to a point, from the world's plan, in rings out from it
		/// </summary>
		/// <param name="from">The point</param>
		/// <returns>The nearest dry ground, or the point when there's none within ShoreSearch</returns>
		private static Vector3 NearestShore(Vector3 from)
		{
			if (WorldGenerator.instance == null || ZoneSystem.instance == null)
			{
				return(from);
			}
			float water = ZoneSystem.instance.m_waterLevel;
			for (float radius = ShoreStep; radius <= ShoreSearch; radius += ShoreStep)
			{
				for (int i = 0; i < ShoreWays; i++)
				{
					float angle = 2f * Mathf.PI * i / ShoreWays;
					Vector3 point = from + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
					if (WorldGenerator.instance.GetHeight(point.x, point.z) > water + 0.5f)
					{
						return(point);
					}
				}
			}
			return(from);
		}

		/// <summary>
		/// Swims the viking for the shore - drowning on the way once its stamina runs out, as any viking in the water does
		/// (see LivingVikingBody.OnSwimming) - and once it has been out of the water a moment, goes on along its road from
		/// the far shore, or plans its road again from where it washed up
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True, as it takes the update over</returns>
		private bool SwimAshore(float dt, ZDO zdo)
		{
			if (humanoid.InWater())
			{
				ashoreFor = 0f;
				mover = ((body != null && body.Drowning()) ? "drowning on its way to the shore" : "swimming for the shore");
				Vector3 way = swimTo - transform.position;
				way.y = 0f;
				ai.MoveTowards(way.normalized, true);
				return(true);
			}
			ashoreFor += dt;
			if (ashoreFor < AshoreTime)
			{
				mover = "climbing ashore";
				Go(dt, swimTo, 2f, true);
				moveFailed = false;
				return(true);
			}
			Ashore(zdo);
			return(true);
		}

		/// <summary>
		/// Brings the viking ashore - on along its road from the far shore, the one at the helm breaking the boat up first,
		/// or for one wrecked, planning its road again from where it washed up. A village's viking, home from a quest by
		/// boat, goes back to its life in the village, the one at the helm breaking up a boat that isn't left for it
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		private void Ashore(ZDO zdo)
		{
			bool wrecked = (crossing == Crossing.Wrecked);
			zdo.Set(VikingBoat.BoatKey, "");
			SetCrossing(zdo, Crossing.None);
			if (active)
			{
				Plugin.Log.LogInfo(LogName() + " came ashore at " + transform.position.ToString("F0") + " by its village");
				if (!wrecked && myBoat != null && mySeat == 0 && !myBoat.Kept())
				{
					swings = 0;
					nextSwing = Time.time + 0.5f;
					SetCrossing(zdo, Crossing.Breaking);
					return;
				}
				myBoat = null;
				return;
			}
			if (wrecked || myBoat == null)
			{
				Plugin.Log.LogInfo(LogName() + " washed up at " + transform.position.ToString("F0") + ", and plans its road again");
				myBoat = null;
				AskRoad(true);
				return;
			}
			roadLeg = Mathf.Clamp(myBoat.Ashore, 0, travelRoad.Count);
			zdo.Set(VillageTravel.LegKey, roadLeg);
			Plugin.Log.LogInfo(LogName() + " came ashore at " + transform.position.ToString("F0") + " from " + myBoat.Key);
			TestLog.Count("Boats", "vikings ashore");
			// A boat left for the village they came to isn't broken up (see VikingBoat.DecideKept)
			if (mySeat == 0 && !myBoat.Kept())
			{
				swings = 0;
				nextSwing = Time.time + 0.5f;
				SetCrossing(zdo, Crossing.Breaking);
				return;
			}
			myBoat = null;
		}

		/// <summary>
		/// Has the viking that was at the helm swing its hammer at the boat once and ask for it to be broken up, once the
		/// rest of the crew is off it (see VikingBoat.UpdateLanding)
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True while it works on the boat</returns>
		private bool BreakBoat(ZDO zdo)
		{
			if (myBoat == null || myBoat.Kept())
			{
				HoldHammer(false);
				SetCrossing(zdo, Crossing.None);
				return(false);
			}
			mover = "breaking up its boat";
			Face(myBoat.transform.position);
			HoldHammer(true);
			if (Time.time < nextSwing)
			{
				return(true);
			}
			if (swings < BuildSwings)
			{
				VikingPoses.Swing(humanoid);
				swings++;
				nextSwing = Time.time + BuildSwingGap;
				return(true);
			}
			HoldHammer(false);
			myBoat.RequestBreak();
			myBoat = null;
			SetCrossing(zdo, Crossing.None);
			return(true);
		}

		/// <summary>
		/// Describes the viking's crossing for the debug hover text and the test run's snapshot
		/// </summary>
		/// <returns>The description, or "" when it isn't crossing the sea</returns>
		private string DescribeCrossing()
		{
			if (crossing == Crossing.None)
			{
				return("");
			}
			string line = "Crossing: " + crossing.ToString().ToLowerInvariant() + " for " + (Time.time - crossingSince).ToString("F0") + "s";
			if (myBoat != null)
			{
				line += ", " + myBoat.Key + " seat " + mySeat + " (" + myBoat.CurrentPhase.ToString().ToLowerInvariant() + ", " + myBoat.CrewAboard() + " of " + myBoat.Crew.Count + " on board)";
			}
			string stamina = ((body != null) ? " with " + body.Stamina().ToString("F0") + " of " + body.MaxStamina().ToString("F0") + " stamina" : "");
			if (crossing == Crossing.Landing || crossing == Crossing.Wrecked)
			{
				line += ", swimming to " + swimTo.ToString("F0") + stamina;
			}
			else if (crossing == Crossing.Overboard && myBoat != null)
			{
				line += ", " + Vector3.Distance(transform.position, myBoat.Ladder(transform.position)).ToString("F0") + "m from its ladder" + stamina;
			}
			return(line);
		}
	}
}
