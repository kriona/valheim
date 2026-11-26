using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A living viking's life away from any village - lost for a while when a console command spawned it out in the
	/// wild, then on the road to the nearest village it can reach (see VillageTravel), or wandering when there's none -
	/// and the gathering at the fire of a village a player disbanded before its vikings set out together for another
	/// </summary>
	/// <remarks>
	/// On the road a viking runs from waypoint to waypoint, stays out of fights unless it can't get away - its enemy is
	/// faster than it, it's cornered, or it's hurt with its enemy right on it - or someone near it is attacked, waits for
	/// the rest of its group when they fall behind, and eats what it carries, picking food along the way when it has
	/// none. Reaching the sea it sets sail, and reaching a portal on its road it steps in, and the server takes it off
	/// the road with the rest of its group (see SetSail and EnterPortal). A wanderer fights what
	/// it can take on, keeps away from what it can't, picks up loot and looks for a village
	/// again every RecheckTime seconds. Neither uses village speech - they greet a player and pass on the news, and say
	/// their own lines (see VikingRoadSpeech)
	/// </remarks>
	internal partial class VillageLife
	{
		/// <summary>
		/// What a viking away from any village is doing
		/// </summary>
		private enum Road
		{
			None,
			Confused,
			Searching,
			Travelling,
			Waiting,
			Wandering
		}

		/// <summary>
		/// How long a viking spawned out in the wild stays lost - the least and most time, then how long it stands still
		/// at first, and the least and most time between its looks around, in seconds
		/// </summary>
		private const float ConfusedMin = 15f;
		private const float ConfusedMax = 20f;
		private const float ConfusedStill = 3f;
		private const float LookMin = 3f;
		private const float LookMax = 4f;

		/// <summary>
		/// When a lost viking says what it says - the soonest and latest after it's spawned, in seconds, and how long
		/// after the first it says another, well before it's no longer lost (see ConfusedMin)
		/// </summary>
		private const float FirstLineMin = 1f;
		private const float FirstLineMax = 2f;
		private const float SecondLineDelay = 7f;

		/// <summary>
		/// Seconds a viking waits on the server's answer before asking again, and between a wanderer's looks for a village
		/// </summary>
		private const float SearchTimeout = 60f;
		private const float RecheckTime = 180f;

		/// <summary>
		/// How near a waypoint a viking has to get to head for the next, and how near its village to have reached it, in
		/// meters, and how many waypoints in a row it can fail to reach before it plans its road again
		/// </summary>
		private const float WaypointReach = 4f;
		private const float RoadArriveReach = 40f;

		/// <summary>
		/// How many times RoadArriveReach from the village it's going to a viking can be at the end of its road and still
		/// count as there - past that, its road no longer covers where it is (see UpdateTravelling)
		/// </summary>
		private const float RoadEndSlack = 2f;
		private const int LegFailuresMax = 3;

		/// <summary>
		/// Seconds a viking waits at a village that should take it in but has no house for it before going on to the next
		/// </summary>
		private const float WaitGiveUp = 300f;

		/// <summary>
		/// How far behind the rest a viking of a group can fall before those ahead wait for it, and how far from them it
		/// can be for them to wait, in meters
		/// </summary>
		private const float GroupGap = 15f;
		private const float GroupWaitReach = 100f;

		/// <summary>
		/// How often a viking away from any village says something to itself with a player near - the least and most time
		/// between, in seconds - and how near the player has to be, in meters
		/// </summary>
		private const float RoadLineMin = 45f;
		private const float RoadLineMax = 90f;
		private const float RoadLineRange = 20f;

		/// <summary>
		/// How far a wanderer strolls each time, the least and most, in meters, the least and most it stands about between,
		/// in seconds, and how far from a spot a monster it can't take on has to be for it to go there, in meters
		/// </summary>
		private const float WanderMin = 20f;
		private const float WanderMax = 40f;
		private const float WanderPauseMin = 6f;
		private const float WanderPauseMax = 20f;
		private const float WanderClear = 30f;

		/// <summary>
		/// How a viking on the road keeps out of a fight - how far ahead it runs each step and how far from its enemy it's
		/// safe, in meters, how long it can gain no ground before it counts as cornered, how long it fights once it turns,
		/// how recently it has to have been hurt and how near its enemy to count as caught, in seconds and meters, how
		/// much faster than it an enemy has to be to outrun it, how near someone attacked has to be for it to help, in
		/// meters, and how much tougher than it a monster has to be for a wanderer to keep away from it
		/// </summary>
		private const float FleeStep = 12f;
		private const float FleeSafe = 30f;
		private const float CorneredTime = 3f;
		private const float FightBackTime = 8f;
		private const float CaughtHurtTime = 1.5f;
		private const float CaughtRange = 3f;
		private const float FasterBy = 1.05f;
		private const float HelpRange = 15f;
		private const float DifficultHealth = 1.5f;

		/// <summary>
		/// How a viking away from any village looks after itself - seconds between its looks for food or loot, how far it
		/// goes for food and loot, in meters, and how long it tries to get to it, in seconds
		/// </summary>
		private const float NeedsInterval = 10f;
		private const float RoadForageReach = 25f;
		private const float RoadLootReach = 10f;
		private const float NeedGiveUp = 30f;

		/// <summary>
		/// How often the vikings of a disbanded village say something as they gather at its fire - the least and most time
		/// between each one's looks, in seconds, and how likely it speaks at each
		/// </summary>
		private const float GatherLineMin = 8f;
		private const float GatherLineMax = 25f;
		private const float GatherLineChance = 0.5f;

		/// <summary>
		/// What the viking is doing away from any village
		/// </summary>
		private Road road = Road.None;

		/// <summary>
		/// While it's lost - until when it is, stands still and looks the way it's looking, which way that is, and when it
		/// says its first and second lines, or -1 once it has, all in Time.time seconds, and the last line it said
		/// </summary>
		private float confusedUntil;

		/// <summary>
		/// Whether the viking was lost until it started waiting to know where to go, so it says where it's going once it
		/// knows (see TakeAnswer)
		/// </summary>
		private bool wasLost;

		/// <summary>
		/// How near each other vikings with no road have to be to decide where to go as a group, in meters (see
		/// FormRoadGroup)
		/// </summary>
		private const float RoadGroupReach = 20f;

		/// <summary>
		/// For a viking deciding where to go as a group with others near it (see FormRoadGroup), the one of them who
		/// decides, or null, and the road it took and what the group can do on it, which this one takes as soon as it's
		/// no longer lost itself
		/// </summary>
		private VillageLife roadLeader;
		private TravelPlanner.Answer leaderAnswer;
		private TravelParty leaderParty;

		/// <summary>
		/// Gathers the other vikings with no village within RoadGroupReach of the viking that also have to decide where
		/// to go - still lost or waiting to know, or needing their road planned again, like after a wreck (see NeedsRoad) -
		/// those in no group or in its own, run by this client, into a group it leads, so they decide together: it alone
		/// asks for a road, for all of them (see RoadParty), and they drop their own asking and take its road once it has
		/// one (see TakeAnswer), setting out as one group, which crosses the sea together in the boats one of them builds
		/// (see Gather). It keeps the group it's in, or starts one
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True when it leads a group now, having asked for its road</returns>
		private bool FormRoadGroup(ZDO zdo)
		{
			if (roadLeader != null)
			{
				return(false);
			}
			string own = zdo.GetString(VillageTravel.GroupKey, "");
			List<VillageLife> others = new List<VillageLife>();
			foreach (VillageLife other in All)
			{
				if (other == this || other.active || other.roadLeader != null || !other.NeedsRoad() || other.nview == null || !other.nview.IsValid() || !other.nview.IsOwner() || other.humanoid == null || other.humanoid.IsDead() || Utils.DistanceXZ(other.transform.position, transform.position) > RoadGroupReach)
				{
					continue;
				}
				string theirs = other.nview.GetZDO().GetString(VillageTravel.GroupKey, "");
				if (theirs.Length == 0 || theirs == own)
				{
					others.Add(other);
				}
			}
			if (others.Count == 0)
			{
				return(false);
			}
			string group = ((own.Length > 0) ? own : "road" + zdo.m_uid.ToString());
			zdo.Set(VillageTravel.GroupKey, group);
			foreach (VillageLife other in others)
			{
				other.nview.GetZDO().Set(VillageTravel.GroupKey, group);
				other.roadLeader = this;
				other.leaderAnswer = null;
				other.leaderParty = null;
				other.ForgetSearch();
				if (other.road != Road.Confused)
				{
					other.road = Road.Searching;
				}
			}
			Plugin.Log.LogInfo(LogName() + " gathers " + others.Count + " other vikings near it with no road into a group, and decides where they go");
			TestLog.Count("Travel", "vikings deciding their road as a group");
			AskRoad(true);
			return(true);
		}

		/// <summary>
		/// Checks whether the viking has to decide where to go - lost, waiting to know, or on the road with it being
		/// planned again or past the end of it
		/// </summary>
		/// <returns>True when it has</returns>
		private bool NeedsRoad()
		{
			if (road == Road.Confused || road == Road.Searching)
			{
				return(true);
			}
			return((road == Road.Travelling || road == Road.Waiting) && (searching || roadLeg >= travelRoad.Count));
		}

		/// <summary>
		/// Plans the viking's road again from where it is, deciding as a group with the vikings near it that have to as
		/// well (see FormRoadGroup), or alone
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		private void PlanRoadAgain(ZDO zdo)
		{
			road = Road.Searching;
			if (!FormRoadGroup(zdo))
			{
				AskRoad(true);
			}
		}

		/// <summary>
		/// Stops waiting on the road the viking asked the server for, for one that goes where its group's leader decides
		/// </summary>
		private void ForgetSearch()
		{
			if (onAnswer != null)
			{
				TravelPlanner.Forget(onAnswer);
			}
			onAnswer = null;
			searching = false;
			answered = null;
		}

		/// <summary>
		/// Lists the vikings in the group the viking leads that haven't set out yet (see FormRoadGroup)
		/// </summary>
		/// <returns>Them</returns>
		private List<VillageLife> LostFollowers()
		{
			List<VillageLife> followers = new List<VillageLife>();
			foreach (VillageLife other in All)
			{
				if (other != null && other != this && other.roadLeader == this)
				{
					followers.Add(other);
				}
			}
			return(followers);
		}
		private float stillUntil;
		private float nextLookAt;
		private Vector3 lookToward;
		private float firstLineAt = -1f;
		private float secondLineAt = -1f;
		private string lastLostLine;

		/// <summary>
		/// Its search for a village - the answer once it's come, whether one is on its way, when it was asked for, what
		/// takes the answer, and the villages it found no one in
		/// </summary>
		private TravelPlanner.Answer answered;
		private bool searching;
		private float searchedAt;
		private System.Action<TravelPlanner.Answer> onAnswer;
		private readonly List<Vector3> deadEnds = new List<Vector3>();

		/// <summary>
		/// Its road - the waypoints, the one it's heading for, how many in a row it couldn't reach, and when it started
		/// waiting at its village
		/// </summary>
		private TravelRoad travelRoad = new TravelRoad();
		private int roadLeg;
		private int legFailures;
		private float waitingSince;

		/// <summary>
		/// Where the next search for a village looks - the one it gets to soonest, or one at random after a disbanded
		/// village's viking found its village gone
		/// </summary>
		private TravelPlanner.Goal roadGoal = TravelPlanner.Goal.Soonest;

		/// <summary>
		/// For a viking that left a disbanded village, its look at whether its village was started again once it reaches
		/// where it was going - the server's answer once it's come, whether it's asked and when, and the village it last
		/// looked from, so it only looks once there
		/// </summary>
		private TravelPlanner.Answer homeAnswer;
		private bool askingHome;
		private float askedHomeAt;
		private Vector3 homeChecked;

		/// <summary>
		/// When it next says something to itself, and when a wanderer next looks for a village, in Time.time seconds
		/// </summary>
		private float nextRoadLine;
		private float nextSearchAt;

		/// <summary>
		/// A wanderer's stroll - where to, whether it's on its way, and until when it stands about
		/// </summary>
		private Vector3 wanderTarget;
		private bool wanderMoving;
		private float wanderPauseUntil;

		/// <summary>
		/// Its keeping out of a fight - until when it fights rather than runs, what it runs from, the farthest it has got
		/// from it and since when it has gained no ground
		/// </summary>
		private float fightUntil;
		private Character fleeingFrom;
		private float fleeBest;
		private float fleeStuckSince;

		/// <summary>
		/// Its looking after itself - when it next looks, the food or loot it's going for, and until when it tries
		/// </summary>
		private float nextNeedsCheck;
		private Pickable roadForage;
		private ItemDrop roadLoot;
		private float needUntil;

		/// <summary>
		/// Its gathering at a disbanded village's fire - the fire, when it next looks for it, when it next might say
		/// something, whether the group has been told where they're going, and whether it has started its goodbye
		/// </summary>
		private ZDO gatherFire;
		private float nextFireLook;
		private float nextGatherLine;
		private bool announced;
		private bool goodbyeStarted;

		/// <summary>
		/// Checks whether the viking's life away from any village runs - on its owner, while it belongs to no village, or
		/// leaving a disbanded village once it's on the road - starting it when it hasn't yet
		/// </summary>
		/// <returns>True when it runs</returns>
		private bool RoadRuns()
		{
			if (!ServerCheck.Active || nview == null || !nview.IsValid() || !nview.IsOwner() || ai == null || humanoid.IsDead())
			{
				return(false);
			}
			ZDO zdo = nview.GetZDO();
			// One that belongs to a village that isn't loaded, or that it's away from, goes on as it always has, and one
			// leaving a disbanded village gathers with the rest until it's on the road (see UpdateLeaving)
			bool departing = zdo.GetBool(VillageFounding.LeavingKey);
			if ((!departing && zdo.GetVec3(Village.VillageKey, Vector3.zero) != Vector3.zero) || (departing && !VillageTravel.OnRoad(zdo)))
			{
				road = Road.None;
				return(false);
			}
			if (road == Road.None)
			{
				StartRoad(zdo);
			}
			return(true);
		}

		/// <summary>
		/// Runs before the viking's AI while it's away from any village - weighing up a fight its AI has picked, so it
		/// runs from one it shouldn't take on and keeps on its way while its enemy is far off, and turning it to a player
		/// it greets. The rest of its life away from any village runs in place of the AI's wandering (see RoadIdle), so the
		/// AI still spots its enemies
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True to skip the AI's own update</returns>
		private bool UpdateRoad(float dt)
		{
			if (!RoadRuns())
			{
				return(false);
			}
			if (UpdateBoat(dt))
			{
				return(true);
			}
			Character target = ai.m_targetCreature;
			if (target != null)
			{
				// It lets go of its cart before it fights or runs, and goes back for it after
				ReleaseRoadCart();
				if (Fights(target))
				{
					fleeingFrom = null;
					mover = "fighting away from any village";
					Scold(target);
					return(false);
				}
				if (Utils.DistanceXZ(target.transform.position, transform.position) < FleeSafe)
				{
					return(Flee(dt, target));
				}
				// Keeping an eye on an enemy far off, it keeps on its way
				fleeingFrom = null;
				return(RoadIdle(dt));
			}
			fleeingFrom = null;
			return(UpdateGreeting());
		}

		/// <summary>
		/// Runs the viking's life away from any village in place of the AI's wandering - lost, waiting to know where to
		/// go, on the road, or wandering
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True when it handled the update, so the wandering is skipped</returns>
		private bool RoadIdle(float dt)
		{
			if (!RoadRuns())
			{
				return(false);
			}
			ZDO zdo = nview.GetZDO();
			switch (road)
			{
				case Road.Confused:
					return(UpdateConfused(zdo));
				case Road.Searching:
					return(UpdateSearching(zdo));
				case Road.Travelling:
				case Road.Waiting:
					return(UpdateTravelling(dt, zdo));
				case Road.Wandering:
					return(UpdateWandering(dt, zdo));
			}
			return(false);
		}

		/// <summary>
		/// Starts the viking's life away from any village - going on along its road when it's on one, lost when a console
		/// command just spawned it, and otherwise asking the server where to go
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		private void StartRoad(ZDO zdo)
		{
			if (VillageTravel.OnRoad(zdo))
			{
				LoadRoad(zdo);
				road = Road.Travelling;
				nextRoadLine = Time.time + Random.Range(RoadLineMin, RoadLineMax) / 3f;
				return;
			}
			if (zdo.GetBool(VillageTravel.ConfusedKey))
			{
				road = Road.Confused;
				confusedUntil = Time.time + Random.Range(ConfusedMin, ConfusedMax);
				stillUntil = Time.time + ConfusedStill;
				nextLookAt = stillUntil;
				lookToward = Vector3.zero;
				firstLineAt = Time.time + Random.Range(FirstLineMin, FirstLineMax);
				secondLineAt = -1f;
				Plugin.Log.LogInfo(LogName() + " was spawned out in the wild, and is lost for " + (confusedUntil - Time.time).ToString("F0") + "s");
			}
			else
			{
				road = Road.Searching;
			}
			// Asked straight away, so the answer is in by the time it's no longer lost
			AskRoad(true);
		}

		/// <summary>
		/// Asks the server for the nearest village the viking can reach, no harder than its biome - unless it's waiting on
		/// an answer it asked for less than SearchTimeout ago
		/// </summary>
		/// <param name="force">True to ask again even then</param>
		private void AskRoad(bool force)
		{
			if (searching && !force && Time.time - searchedAt < SearchTimeout)
			{
				return;
			}
			if (onAnswer != null)
			{
				TravelPlanner.Forget(onAnswer);
			}
			searching = true;
			searchedAt = Time.time;
			answered = null;
			onAnswer = (TravelPlanner.Answer answer) =>
			{
				if (this != null)
				{
					answered = answer;
					searching = false;
					roadGoal = TravelPlanner.Goal.Soonest;
				}
			};
			TravelPlanner.Ask(transform.position, RoadParty(nview.GetZDO()), deadEnds, onAnswer, roadGoal);
		}

		/// <summary>
		/// Gets the index of the viking's biome
		/// </summary>
		/// <returns>The index into VikingGear's tables</returns>
		private int OwnBiome()
		{
			return((viking != null) ? viking.BiomeIndex() : 0);
		}

		/// <summary>
		/// Reads the viking's road from its ZDO
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		private void LoadRoad(ZDO zdo)
		{
			travelRoad = TravelRoad.Decode(zdo.GetByteArray(VillageTravel.RouteKey));
			roadLeg = Mathf.Clamp(zdo.GetInt(VillageTravel.LegKey, 1), 0, travelRoad.Count);
			legFailures = 0;
		}

		/// <summary>
		/// Gets what the viking can do on the road - its group's, once it's on the road with one, so they all keep to the
		/// same road and boat, with its own biome and what it carries added
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>The party</returns>
		private TravelParty RoadParty(ZDO zdo)
		{
			TravelParty party = new TravelParty();
			TravelPlanner.AddTo(party, humanoid, true);
			// A group's leader asks for the whole group (see FormRoadGroup)
			foreach (VillageLife other in LostFollowers())
			{
				if (other.humanoid != null)
				{
					TravelPlanner.AddTo(party, other.humanoid, false);
				}
			}
			if (VillageTravel.OnRoad(zdo))
			{
				TravelParty group = VillageTravel.PartyOf(zdo);
				party.Add(group.Easiest, group.Unteleportable, false);
				party.Hardest = Mathf.Max(party.Hardest, group.Hardest);
			}
			return(party);
		}

		/// <summary>
		/// Keeps a lost viking standing where it was spawned - still at first, then turning to look a new way every few
		/// seconds - saying where it thinks it is, until it's no longer lost and sets out
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True, as it holds the viking still</returns>
		private bool UpdateConfused(ZDO zdo)
		{
			mover = "lost";
			ai.StopMoving();
			if (firstLineAt >= 0f && Time.time >= firstLineAt)
			{
				firstLineAt = -1f;
				lastLostLine = VikingRoadSpeech.Confused(null);
				VikingSpeech.Say(humanoid, lastLostLine, VikingSpeech.TalkTime, VikingSpeech.TalkCull);
				secondLineAt = Time.time + SecondLineDelay;
			}
			if (secondLineAt >= 0f && Time.time >= secondLineAt)
			{
				secondLineAt = -1f;
				VikingSpeech.Say(humanoid, VikingRoadSpeech.Confused(lastLostLine), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
			}
			if (Time.time >= stillUntil && Time.time >= nextLookAt)
			{
				lookToward = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
				nextLookAt = Time.time + Random.Range(LookMin, LookMax);
			}
			if (lookToward != Vector3.zero)
			{
				Face(transform.position + lookToward * 5f);
			}
			if (Time.time >= confusedUntil)
			{
				zdo.Set(VillageTravel.ConfusedKey, false);
				road = Road.Searching;
				wasLost = true;
				FormRoadGroup(zdo);
			}
			return(true);
		}

		/// <summary>
		/// Has the viking stand about until the server answers where it can go, then set out - or wander when there's
		/// nowhere
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True, as it holds the viking still</returns>
		private bool UpdateSearching(ZDO zdo)
		{
			mover = "waiting to know where to go";
			ai.StopMoving();
			if (roadLeader != null && roadLeader.humanoid != null && !roadLeader.humanoid.IsDead())
			{
				mover = "waiting for its group to decide where to go";
				if (leaderAnswer != null)
				{
					TravelPlanner.Answer shared = leaderAnswer;
					leaderAnswer = null;
					TakeAnswer(zdo, shared);
				}
				return(true);
			}
			if ((object)roadLeader != null)
			{
				// Its group's leader is gone - unloaded or dead - so it decides for itself
				roadLeader = null;
				zdo.Set(VillageTravel.GroupKey, "");
				AskRoad(true);
				return(true);
			}
			if (answered != null)
			{
				TravelPlanner.Answer answer = answered;
				answered = null;
				TakeAnswer(zdo, answer);
				return(true);
			}
			AskRoad(false);
			return(true);
		}

		/// <summary>
		/// Sets the viking out for the village the server found, or has it wander when it found none - one that was lost
		/// saying where it's going, or that there's nowhere to go
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <param name="answer">The server's answer</param>
		private void TakeAnswer(ZDO zdo, TravelPlanner.Answer answer)
		{
			// A group's leader hands its road on to the rest, who take it as one group, and only it says so
			TravelParty party = null;
			bool follower = (roadLeader != null);
			if (follower)
			{
				party = leaderParty;
				roadLeader = null;
				leaderParty = null;
			}
			else
			{
				List<VillageLife> followers = LostFollowers();
				if (followers.Count > 0)
				{
					party = RoadParty(zdo);
					foreach (VillageLife other in followers)
					{
						other.leaderAnswer = answer;
						other.leaderParty = party;
					}
				}
			}
			if (wasLost)
			{
				wasLost = false;
				if (!follower)
				{
					VikingSpeech.Say(humanoid, (answer.Found ? VikingRoadSpeech.FoundWay(answer.Spoken()) : VikingRoadSpeech.Nowhere()), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
				}
				TakeRoad(zdo, answer, party);
				// Its road talk starts later, so it doesn't talk over what it just said
				nextRoadLine = Mathf.Max(nextRoadLine, Time.time + RoadLineMin);
				return;
			}
			TakeRoad(zdo, answer, party);
		}

		/// <summary>
		/// Sets the viking out for the village the server found, or has it wander when it found none
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <param name="answer">The server's answer</param>
		/// <param name="party">What its group can do on the road, or null to work it out from the viking (see
		/// RoadParty)</param>
		private void TakeRoad(ZDO zdo, TravelPlanner.Answer answer, TravelParty party)
		{
			if (!answer.Found)
			{
				if (road != Road.Wandering)
				{
					Plugin.Log.LogInfo(LogName() + " found no village it can reach, and wanders");
					nextRoadLine = Time.time + Random.Range(RoadLineMin, RoadLineMax) / 3f;
				}
				// Off the road, it's a viking with no village like any other - one of a disbanded village's group too
				VillageTravel.Clear(zdo);
				if (zdo.GetBool(VillageFounding.LeavingKey))
				{
					zdo.Set(VillageFounding.LeavingKey, false);
					zdo.Set(Village.VillageKey, Vector3.zero);
					leaving = false;
				}
				road = Road.Wandering;
				nextSearchAt = Time.time + RecheckTime;
				return;
			}
			VillageTravel.SetOut(zdo, answer, zdo.GetString(VillageTravel.GroupKey, ""), (party ?? RoadParty(zdo)));
			LoadRoad(zdo);
			road = Road.Travelling;
			nextRoadLine = Time.time + Random.Range(RoadLineMin, RoadLineMax) / 3f;
			Plugin.Log.LogInfo(LogName() + " set out for the village at " + answer.Destination.ToString("F0") + ", " + answer.Length.ToString("F0") + "m by road");
		}

		/// <summary>
		/// Runs the viking along its road, a waypoint at a time - eating and picking food on the way, waiting for its group
		/// when they fall behind, and planning its road again when it can't get through, or when it's past the end of
		/// its road far from the village it's going to
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True, as it takes the update over</returns>
		private bool UpdateTravelling(float dt, ZDO zdo)
		{
			if (answered != null)
			{
				TravelPlanner.Answer answer = answered;
				answered = null;
				TakeAnswer(zdo, answer);
				return(true);
			}
			if (searching)
			{
				// Planning its road again, it waits where it is
				ai.StopMoving();
				AskRoad(false);
				return(true);
			}
			if (travelRoad.Count == 0)
			{
				AskRoad(true);
				return(true);
			}
			Vector3 destination = zdo.GetVec3(VillageTravel.DestinationKey, Vector3.zero);
			float toVillage = Utils.DistanceXZ(transform.position, destination);
			if (roadLeg >= travelRoad.Count && toVillage > RoadArriveReach * RoadEndSlack)
			{
				// Past the end of its road far from the village - having ridden a boat somewhere else, or its road being
				// planned again when the world was last saved - it plans its road again from where it is, with the vikings
				// near it that have to as well, or out in the water, climbs onto the nearest boat or swims ashore first,
				// planning it from there (see Ashore)
				TestLog.Count("Travel", "roads planned again from past their end");
				if (OutInWater())
				{
					Plugin.Log.LogInfo(LogName() + " is past the end of its road in the water at " + transform.position.ToString("F0") + ", " + toVillage.ToString("F0") + "m from the village it's going to, and makes for the nearest boat or land");
					StartSwim(zdo, Crossing.Wrecked, Vector3.zero);
					return(true);
				}
				Plugin.Log.LogInfo(LogName() + " is past the end of its road at " + transform.position.ToString("F0") + ", " + toVillage.ToString("F0") + "m from the village it's going to, and plans its road again");
				PlanRoadAgain(zdo);
				return(true);
			}
			if (toVillage <= RoadArriveReach || roadLeg >= travelRoad.Count)
			{
				return(Reached(dt, zdo, destination));
			}
			if (travelRoad.SeaLeg(roadLeg))
			{
				// Its cart is taken down at the shore, to be built back where it comes ashore
				if (PackRoadCart(dt, zdo))
				{
					return(true);
				}
				return(UpdateCrossing(dt, zdo));
			}
			if (travelRoad.PortalLeg(roadLeg))
			{
				return(EnterPortal(dt, zdo));
			}
			if (UpdateNeeds(dt, false) || WaitForGroup(zdo) || UpdateRoadCart(dt, zdo))
			{
				return(true);
			}
			SayOnRoad(() => VikingRoadSpeech.Travelling(VillageTravel.DestinationName(zdo)));
			if (!RoadGo(dt, travelRoad.Points[roadLeg], WaypointReach))
			{
				mover = "on the road";
				return(true);
			}
			if (moveFailed)
			{
				moveFailed = false;
				legFailures++;
				if (legFailures >= LegFailuresMax)
				{
					Plugin.Log.LogInfo(LogName() + " couldn't get along its road at " + transform.position.ToString("F0") + ", and plans it again");
					AskRoad(true);
					return(true);
				}
			}
			else
			{
				legFailures = 0;
			}
			roadLeg++;
			zdo.Set(VillageTravel.LegKey, roadLeg);
			NoteBiome(zdo);
			return(true);
		}

		/// <summary>
		/// Notes the biome the viking is walking through on its trip, the first time (see TripStory)
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		private void NoteBiome(ZDO zdo)
		{
			TripStory story = TripStory.Of(zdo);
			if (story != null && WorldGenerator.instance != null && story.AddBiome(VikingGear.IndexOf(WorldGenerator.instance.GetBiome(transform.position))))
			{
				zdo.Set(TripStory.TripKey, story.Encode());
			}
		}

		/// <summary>
		/// Has a viking that reached its village join it, once the village takes it in - or go on to the next when it was
		/// a farm that stayed abandoned or a village that's gone, picked at random for a viking that left a disbanded
		/// village, or when it should take it in but has had no house for it for WaitGiveUp seconds. A viking that left a
		/// disbanded village turns around for home instead, once it's here, when its village was started again at its
		/// bonfire, and coming home to a village with no house for it, comes to its beds. Until then it waits by the
		/// village, as a farm no one has come near is set up
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="zdo">Its ZDO</param>
		/// <param name="destination">Where the village stands</param>
		/// <returns>True while it waits, false once it has joined</returns>
		private bool Reached(float dt, ZDO zdo, Vector3 destination)
		{
			ZDO generator = VillageRespawn.FindGenerator(destination);
			bool abandoned = (generator != null && generator.GetPrefab() == VillagePaths.GeneratorHash && generator.GetBool(VillagePaths.AbandonedKey));
			if (abandoned || (generator == null && VillageRespawn.ZoneSettled(destination)))
			{
				TripStory.Note(zdo, (TripStory story) => story.VillageGone = true);
				DeadEnd(destination, DisbandedViking(zdo));
				return(true);
			}
			if (CheckHome(zdo, destination))
			{
				return(true);
			}
			Village here = Village.ForFarm(destination);
			bool takes = VillageTravel.Takes(here);
			if (takes && here.NearestCoreHouse(transform.position, out VillagePaths.House house))
			{
				leaving = false;
				TripStory.Arrive(zdo);
				here.Adopt(zdo, house);
				zdo.Set(ArrivingKey, !here.Contains(transform.position));
				// A cart it brought becomes the village's, which it pulls in, and what came with it may need more chests
				ReleaseRoadCart();
				Vagon brought = RoadCarts.Arrive(zdo, here);
				if (brought != null)
				{
					zdo.Set(QuestCartKey, brought.m_nview.GetZDO().m_uid);
				}
				NoteChestCheck(here);
				village = here;
				road = Road.None;
				Plugin.Log.LogInfo(LogName() + " came off the road into the village at " + here.Farm.ToString("F0"));
				return(false);
			}
			if (road != Road.Waiting)
			{
				road = Road.Waiting;
				waitingSince = Time.time;
			}
			if (!takes)
			{
				waitingSince = Time.time;
			}
			else if (Time.time - waitingSince > WaitGiveUp)
			{
				if (ComeHomeToBeds(zdo, here))
				{
					return(true);
				}
				DeadEnd(destination, false);
				return(true);
			}
			mover = "waiting at the village it came to";
			Vector3 spot = ((here != null) ? here.RallyPoint() : destination);
			if (Utils.DistanceXZ(transform.position, spot) > RoadArriveReach / 2f)
			{
				Go(dt, spot, RoadArriveReach / 4f, false);
				moveFailed = false;
			}
			else
			{
				ai.StopMoving();
			}
			return(true);
		}

		/// <summary>
		/// Has a viking that reached the sea on its road set sail - saying so, and standing at the shore facing the water
		/// until the server takes it and the rest of its group off the road, which it does once they've all set sail or
		/// are out of every player's reach (see VillageTravel.TakeOffRoad)
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True, as it takes the update over</returns>
		private bool SetSail(ZDO zdo)
		{
			mover = "setting sail";
			if (!zdo.GetBool(VillageTravel.SailingKey))
			{
				VillageTravel.SetSail(zdo);
				VikingSpeech.Say(humanoid, VikingRoadSpeech.Sail(TravelTime.BoatName(VillageTravel.BoatBiome(zdo))), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
				Plugin.Log.LogInfo(LogName() + " reached the sea at " + transform.position.ToString("F0") + ", and sets sail in a " + TravelTime.BoatName(VillageTravel.BoatBiome(zdo)));
			}
			Face(travelRoad.Points[roadLeg]);
			return(true);
		}

		/// <summary>
		/// Has a viking that reached a portal on its road step into it - pulling its cart up to it and taking it down
		/// first (see PackRoadCart), or planning its road again when it carries what the portal won't take (see
		/// PortalRefuses) - saying so, and standing at the portal until the
		/// server takes it and the rest of its group off the road together, which it does once they've all reached it or
		/// are out of every player's reach (see VillageTravel.TakeOffRoad), so the group never splits up
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True, as it takes the update over</returns>
		private bool EnterPortal(float dt, ZDO zdo)
		{
			mover = "stepping through a portal";
			Vector3 entry = travelRoad.Points[roadLeg - 1];
			if (Utils.DistanceXZ(transform.position, entry) > WaypointReach)
			{
				RoadGo(dt, entry, WaypointReach / 2f);
				moveFailed = false;
				return(true);
			}
			ai.StopMoving();
			// What it found or loaded since its road was planned may be what a portal won't take
			if (!zdo.GetBool(VillageTravel.PortalKey) && PortalRefuses(zdo))
			{
				AskRoad(true);
				return(true);
			}
			// Its cart is taken down by the portal, to be built back on the other side
			if (!zdo.GetBool(VillageTravel.PortalKey) && PackRoadCart(dt, zdo))
			{
				return(true);
			}
			if (!zdo.GetBool(VillageTravel.PortalKey))
			{
				VillageTravel.EnterPortal(zdo);
				VikingSpeech.Say(humanoid, VikingRoadSpeech.Portal(), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
				Plugin.Log.LogInfo(LogName() + " reached a portal at " + entry.ToString("F0") + ", and steps through it to " + travelRoad.Points[roadLeg].ToString("F0"));
			}
			return(true);
		}

		/// <summary>
		/// Has the viking move on from a village it found no one in, saying so, and look for the next
		/// </summary>
		/// <param name="destination">Where the village stands</param>
		/// <param name="random">Whether to go to a village picked at random rather than the one it gets to soonest</param>
		private void DeadEnd(Vector3 destination, bool random)
		{
			VikingSpeech.Say(humanoid, VikingRoadSpeech.DeadEnd(), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
			deadEnds.Add(destination);
			road = Road.Travelling;
			roadGoal = (random ? TravelPlanner.Goal.Random : TravelPlanner.Goal.Soonest);
			Plugin.Log.LogInfo(LogName() + " found no one at the village at " + destination.ToString("F0") + ", and looks for " + (random ? "another at random" : "another"));
			TestLog.Count("Travel", "vikings that found their village gone");
			AskRoad(true);
		}

		/// <summary>
		/// Checks whether the viking left a village a player disbanded
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True when it did</returns>
		private static bool DisbandedViking(ZDO zdo)
		{
			return(VillageDeparted.FirePosition(zdo) != Vector3.zero || zdo.GetString(VillageTravel.FromNameKey, "").Length > 0);
		}

		/// <summary>
		/// Has a viking that left a disbanded village and reached where it was going ask the server whether its village was
		/// started again at its bonfire, waiting for the answer - and turn around for home when it was, or else go on into
		/// the village it reached
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <param name="destination">Where the village it reached stands</param>
		/// <returns>True while it waits or once it turns around, false to go on into the village</returns>
		private bool CheckHome(ZDO zdo, Vector3 destination)
		{
			Vector3 fire = VillageDeparted.FirePosition(zdo);
			if (fire == Vector3.zero || Utils.DistanceXZ(destination, fire) <= RoadArriveReach || homeChecked == destination)
			{
				return(false);
			}
			if (homeAnswer != null)
			{
				TravelPlanner.Answer answer = homeAnswer;
				homeAnswer = null;
				askingHome = false;
				if (!answer.Found)
				{
					homeChecked = destination;
					return(false);
				}
				VillageTravel.SetOut(zdo, answer, zdo.GetString(VillageTravel.GroupKey, ""), RoadParty(zdo));
				TripStory.Note(zdo, (TripStory story) => story.TurnedHome = true);
				LoadRoad(zdo);
				road = Road.Travelling;
				VikingSpeech.Say(humanoid, VikingRoadSpeech.HomeAgain(answer.Spoken()), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
				Plugin.Log.LogInfo(LogName() + " reached the village at " + destination.ToString("F0") + " and turns around for home at " + answer.Destination.ToString("F0") + ", started again at its bonfire, " + answer.Length.ToString("F0") + "m by " + TravelPlanner.DescribeRoad(answer.Road));
				TestLog.Count("Travel", "vikings turned around for home");
				return(true);
			}
			if (!askingHome || Time.time - askedHomeAt > SearchTimeout)
			{
				askingHome = true;
				askedHomeAt = Time.time;
				TravelPlanner.Ask(transform.position, RoadParty(zdo), new List<Vector3>(), (TravelPlanner.Answer answer) =>
				{
					if (this != null)
					{
						homeAnswer = answer;
					}
				}, TravelPlanner.Goal.Home, fire);
			}
			mover = "wondering whether home was started again";
			ai.StopMoving();
			return(true);
		}

		/// <summary>
		/// Has a viking that left a disbanded village and came home to it, started again at its bonfire, come to the
		/// village's beds when it has no house for it, the way those kept on the road do (see VillageDeparted.AddReturning)
		/// - taken out of the world, to come back as the village's beds bring vikings
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <param name="here">The village it reached</param>
		/// <returns>True when it came to the beds</returns>
		private bool ComeHomeToBeds(ZDO zdo, Village here)
		{
			Vector3 fire = VillageDeparted.FirePosition(zdo);
			if (here == null || fire == Vector3.zero || Utils.DistanceXZ(here.Farm, fire) > RoadArriveReach || !here.IsOwner())
			{
				return(false);
			}
			TripStory.Arrive(zdo);
			VillageTravel.Clear(zdo);
			VikingItems.RemoveProps(humanoid.GetInventory());
			ZPackage inventory = new ZPackage();
			humanoid.GetInventory().Save(inventory);
			zdo.Set(VikingCore.InventoryKey, inventory.GetArray());
			VillageDeparted.AddReturning(here, new List<byte[]> { VillageDeparted.SnapshotOf(zdo) });
			Plugin.Log.LogInfo(LogName() + " came home to the village started again at " + fire.ToString("F0") + ", and waits for its beds");
			TestLog.Count("Travel", "vikings home to the beds of a village started again");
			nview.Destroy();
			return(true);
		}

		/// <summary>
		/// Has the viking wait for the rest of its group when one on the same road has fallen GroupGap or more behind it,
		/// facing them
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True while it waits</returns>
		private bool WaitForGroup(ZDO zdo)
		{
			string group = zdo.GetString(VillageTravel.GroupKey, "");
			if (group.Length == 0 || travelRoad.Count == 0)
			{
				return(false);
			}
			float mine = travelRoad.Meters(roadLeg, transform.position);
			Vector3 destination = zdo.GetVec3(VillageTravel.DestinationKey, Vector3.zero);
			byte[] ownRoad = zdo.GetByteArray(VillageTravel.RouteKey);
			int roadSize = ((ownRoad != null) ? ownRoad.Length : 0);
			foreach (VillageLife other in All)
			{
				if (other == this || other.nview == null || !other.nview.IsValid() || other.humanoid.IsDead())
				{
					continue;
				}
				ZDO theirs = other.nview.GetZDO();
				if (theirs.GetString(VillageTravel.GroupKey, "") != group || Utils.DistanceXZ(other.transform.position, transform.position) > GroupWaitReach)
				{
					continue;
				}
				// One that planned a road of its own since, after finding no one at a village, goes its own way
				byte[] theirRoad = theirs.GetByteArray(VillageTravel.RouteKey);
				if (theirs.GetVec3(VillageTravel.DestinationKey, Vector3.zero) != destination || theirRoad == null || theirRoad.Length != roadSize)
				{
					continue;
				}
				if (travelRoad.Meters(theirs.GetInt(VillageTravel.LegKey, 1), other.transform.position) > mine + GroupGap)
				{
					mover = "waiting for the others";
					Face(other.transform.position);
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Has a viking with no village to go to stroll about, a short way at a time, keeping to ground no harder than its
		/// biome and away from monsters it can't take on - looking after itself, picking up loot, and looking for a
		/// village again every RecheckTime seconds
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True, as it takes the update over</returns>
		private bool UpdateWandering(float dt, ZDO zdo)
		{
			if (answered != null)
			{
				TravelPlanner.Answer answer = answered;
				answered = null;
				TakeAnswer(zdo, answer);
				if (road != Road.Wandering)
				{
					return(true);
				}
			}
			if (!searching && Time.time >= nextSearchAt)
			{
				nextSearchAt = Time.time + RecheckTime;
				AskRoad(false);
			}
			if (UpdateNeeds(dt, true))
			{
				return(true);
			}
			SayOnRoad(() => VikingRoadSpeech.Wandering());
			mover = "wandering";
			if (wanderMoving)
			{
				if (Go(dt, wanderTarget, 2f, false))
				{
					moveFailed = false;
					wanderMoving = false;
					wanderPauseUntil = Time.time + Random.Range(WanderPauseMin, WanderPauseMax);
				}
				return(true);
			}
			ai.StopMoving();
			if (Time.time < wanderPauseUntil)
			{
				return(true);
			}
			wanderMoving = PickWanderSpot(out wanderTarget);
			if (!wanderMoving)
			{
				wanderPauseUntil = Time.time + WanderPauseMin;
			}
			return(true);
		}

		/// <summary>
		/// Picks a spot for a wanderer to stroll to - on dry ground no harder than its biome, with no monster it can't take
		/// on near it
		/// </summary>
		/// <param name="spot">The spot</param>
		/// <returns>False when none was found</returns>
		private bool PickWanderSpot(out Vector3 spot)
		{
			int own = OwnBiome();
			for (int i = 0; i < 8; i++)
			{
				Vector3 direction = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
				spot = transform.position + direction * Random.Range(WanderMin, WanderMax);
				Heightmap.Biome biome = WorldGenerator.instance.GetBiome(spot);
				if (biome == Heightmap.Biome.Ocean || VikingGear.IndexOf(biome) > own)
				{
					continue;
				}
				float ground = ZoneSystem.instance.GetGroundHeight(spot);
				if (ground < ZoneSystem.instance.m_waterLevel + 0.3f || DifficultNear(spot, WanderClear))
				{
					continue;
				}
				spot.y = ground;
				return(true);
			}
			spot = transform.position;
			return(false);
		}

		/// <summary>
		/// Checks whether a monster the viking can't take on is near a spot
		/// </summary>
		/// <param name="spot">The spot</param>
		/// <param name="range">How near, in meters</param>
		/// <returns>True when one is</returns>
		private bool DifficultNear(Vector3 spot, float range)
		{
			foreach (Character character in Character.GetAllCharacters())
			{
				if (character != humanoid && !character.IsDead() && !character.IsPlayer() && BaseAI.IsEnemy(humanoid, character) && Utils.DistanceXZ(character.transform.position, spot) <= range && Difficult(character))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether a monster is more than the viking can take on - starred, or much tougher than it
		/// </summary>
		/// <param name="enemy">The monster</param>
		/// <returns>True when it is</returns>
		private bool Difficult(Character enemy)
		{
			return(enemy.GetLevel() > 1 || enemy.GetMaxHealth() > humanoid.GetMaxHealth() * DifficultHealth);
		}

		/// <summary>
		/// Checks whether the viking fights an enemy rather than running from it - a viking on the road runs unless it
		/// can't get away or someone near it needs help, and a lost or wandering viking runs only from a monster it can't
		/// take on. It fights a player who hurt it, an enemy attacking a player or living viking near it, one that's
		/// faster than it, and one that has it cornered or caught
		/// </summary>
		/// <param name="target">The enemy</param>
		/// <returns>True when it fights</returns>
		private bool Fights(Character target)
		{
			if (Time.time < fightUntil || target.IsPlayer())
			{
				return(true);
			}
			if (AttacksSomeoneNear(target) || (ai.m_timeSinceHurt < CaughtHurtTime && Utils.DistanceXZ(target.transform.position, transform.position) <= CaughtRange))
			{
				fightUntil = Time.time + FightBackTime;
				return(true);
			}
			if (target.m_runSpeed > humanoid.m_runSpeed * FasterBy)
			{
				return(true);
			}
			bool onRoad = (road == Road.Travelling || road == Road.Waiting);
			return(!onRoad && !Difficult(target));
		}

		/// <summary>
		/// Checks whether an enemy is attacking a player or another living viking within HelpRange of the viking
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>True when it is</returns>
		private bool AttacksSomeoneNear(Character enemy)
		{
			MonsterAI enemyAI = enemy.GetComponent<MonsterAI>();
			Character victim = ((enemyAI != null) ? enemyAI.m_targetCreature : null);
			if (victim == null || victim == humanoid || victim.IsDead() || Utils.DistanceXZ(victim.transform.position, transform.position) > HelpRange)
			{
				return(false);
			}
			return(victim.IsPlayer() || victim.GetComponent<VillageLife>() != null);
		}

		/// <summary>
		/// Runs from an enemy within FleeSafe of the viking - along its road when that leads away from it, or else
		/// straight away. One that gains no ground for CorneredTime turns to fight
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="target">The enemy</param>
		/// <returns>True while it runs, false once it turns to fight</returns>
		private bool Flee(float dt, Character target)
		{
			float distance = Utils.DistanceXZ(target.transform.position, transform.position);
			if (fleeingFrom != target)
			{
				fleeingFrom = target;
				fleeBest = distance;
				fleeStuckSince = Time.time;
			}
			if (distance > fleeBest + 1f)
			{
				fleeBest = distance;
				fleeStuckSince = Time.time;
			}
			else if (Time.time - fleeStuckSince > CorneredTime)
			{
				fightUntil = Time.time + FightBackTime;
				return(false);
			}
			Vector3 away = transform.position - target.transform.position;
			away.y = 0f;
			away = ((away.sqrMagnitude > 0.01f) ? away.normalized : transform.forward);
			Vector3 heading = away;
			if (road == Road.Travelling && roadLeg < travelRoad.Count)
			{
				Vector3 ahead = travelRoad.Points[roadLeg] - transform.position;
				ahead.y = 0f;
				if (ahead.sqrMagnitude > 1f && Vector3.Dot(ahead.normalized, away) > -0.2f)
				{
					heading = (ahead.normalized + away).normalized;
				}
			}
			mover = "running from " + Utils.GetPrefabName(target.gameObject);
			Go(dt, transform.position + heading * FleeStep, 1f, true);
			if (moveFailed)
			{
				moveFailed = false;
				fightUntil = Time.time + FightBackTime;
				return(false);
			}
			return(true);
		}

		/// <summary>
		/// Has a viking away from any village look after itself - eating the food it carries when it eats (see
		/// EatCarried), going for food growing near it when it carries none it eats, and for a wanderer, picking up loot lying near it out
		/// of any player's base
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="loot">Whether it picks up loot</param>
		/// <returns>True while it's going for food or loot</returns>
		private bool UpdateNeeds(float dt, bool loot)
		{
			if (roadForage == null && roadLoot == null)
			{
				if (Time.time < nextNeedsCheck)
				{
					return(false);
				}
				nextNeedsCheck = Time.time + NeedsInterval;
				if (EatCarried())
				{
					return(false);
				}
				roadForage = FindRoadForage();
				if (roadForage == null && loot)
				{
					roadLoot = FindRoadLoot();
				}
				needUntil = Time.time + NeedGiveUp;
			}
			if (roadForage != null)
			{
				if (!IsForage(roadForage) || Time.time > needUntil)
				{
					roadForage = null;
					return(false);
				}
				mover = "picking food on the road";
				if (Go(dt, roadForage.transform.position, ChestReach, false))
				{
					if (!moveFailed && PickForage(roadForage) > 0)
					{
						EatCarried();
					}
					moveFailed = false;
					roadForage = null;
				}
				return(true);
			}
			if (roadLoot != null)
			{
				ZNetView view = roadLoot.m_nview;
				if (view == null || !view.IsValid() || !IsLoot(view) || Time.time > needUntil)
				{
					roadLoot = null;
					return(false);
				}
				mover = "picking up loot on the road";
				if (Go(dt, roadLoot.transform.position, ChestReach, false))
				{
					if (!moveFailed)
					{
						PickUpLoot(view);
					}
					moveFailed = false;
					roadLoot = null;
				}
				return(true);
			}
			return(false);
		}

		/// <summary>
		/// Eats the food the viking carries that it eats now, the way it eats from a chest at home (see FoodIn) - wounded,
		/// the one that heals it fastest, and otherwise keeping its foods up on the weakest that does it good
		/// </summary>
		/// <returns>True when it ate</returns>
		private bool EatCarried()
		{
			if (body == null)
			{
				return(false);
			}
			bool topUp = !Wounded();
			float floor = body.Food.Floor();
			ItemDrop.ItemData best = null;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (!(topUp ? body.Food.CanTopUp(item) : body.Food.CanEat(item)))
				{
					continue;
				}
				if (best == null || (topUp ? EatsBefore(item, best, floor) : item.m_shared.m_foodRegen > best.m_shared.m_foodRegen))
				{
					best = item;
				}
			}
			if (best == null || !body.Food.Eat(best))
			{
				return(false);
			}
			TestLog.Count("Eaten on the road", TestLog.ItemName(best));
			humanoid.GetInventory().RemoveItem(best, 1);
			humanoid.m_consumeItemEffects.Create(transform.position, Quaternion.identity);
			humanoid.m_zanim.SetTrigger("eat");
			return(true);
		}

		/// <summary>
		/// Finds the nearest food growing within RoadForageReach that the viking eats now (see EatCarried), with no monster
		/// it can't take on near it
		/// </summary>
		/// <returns>The food, or null</returns>
		private Pickable FindRoadForage()
		{
			if (body == null)
			{
				return(null);
			}
			bool topUp = !Wounded();
			Pickable nearest = null;
			float nearestDistance = RoadForageReach;
			foreach (Pickable pickable in LoadedPickables())
			{
				if (pickable == null || !IsForage(pickable))
				{
					continue;
				}
				float distance = Utils.DistanceXZ(pickable.transform.position, transform.position);
				ItemDrop.ItemData food = ForageFood(pickable);
				if (distance < nearestDistance && (topUp ? body.Food.CanTopUp(food) : body.Food.CanEat(food)) && !DifficultNear(pickable.transform.position, WanderClear))
				{
					nearest = pickable;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Finds the nearest loot within RoadLootReach, out of any player's base, that the viking has room to carry
		/// </summary>
		/// <returns>The loot, or null</returns>
		private ItemDrop FindRoadLoot()
		{
			ItemDrop nearest = null;
			float nearestDistance = RoadLootReach;
			foreach (ItemDrop drop in LoadedDrops())
			{
				if (drop == null || drop.m_nview == null || !drop.m_nview.IsValid() || !IsLoot(drop.m_nview))
				{
					continue;
				}
				float distance = Utils.DistanceXZ(drop.transform.position, transform.position);
				if (distance >= nearestDistance || EffectArea.IsPointInsideArea(drop.transform.position, EffectArea.Type.PlayerBase) != null)
				{
					continue;
				}
				ItemDrop.ItemData item = ItemOf(drop);
				if (item == null || !humanoid.GetInventory().CanAddItem(item, 1))
				{
					continue;
				}
				nearest = drop;
				nearestDistance = distance;
			}
			return(nearest);
		}

		/// <summary>
		/// Has the viking say something to itself now and then, with a player near enough to hear it
		/// </summary>
		/// <param name="line">Picks the line</param>
		private void SayOnRoad(System.Func<string> line)
		{
			if (Time.time < nextRoadLine)
			{
				return;
			}
			nextRoadLine = Time.time + Random.Range(RoadLineMin, RoadLineMax);
			if (Player.GetClosestPlayer(transform.position, RoadLineRange) != null)
			{
				VikingSpeech.Say(humanoid, line(), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
			}
		}

		/// <summary>
		/// Finds the bonfire of the village the viking left, looked for again every couple of seconds
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>The bonfire's ZDO, or null</returns>
		private ZDO GatherFire(ZDO zdo)
		{
			if (gatherFire == null || !gatherFire.IsValid() || Time.time >= nextFireLook)
			{
				nextFireLook = Time.time + 2f;
				gatherFire = VillageDeparted.LeftFire(zdo);
			}
			return(gatherFire);
		}

		/// <summary>
		/// Gathers a viking of a disbanded village at its fire with the rest - emptying the village's own chests first, once
		/// they know where they're going (see UpdateDisbandLoot), talking about the village as they come, then
		/// wondering where to go once they're all there - until one of them says where they're going and they set out
		/// together, the ones near a player saying goodbye first, taking along the carts they loaded
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="zdo">Its ZDO</param>
		/// <param name="fire">The bonfire's ZDO</param>
		/// <returns>True while it gathers or sets out, false when there's no village to go to and it leaves the way it
		/// always has</returns>
		private bool UpdateGathering(float dt, ZDO zdo, ZDO fire)
		{
			VillageDisband.Phase phase = VillageDisband.PhaseOf(fire, out long _, out long _);
			TravelPlanner.Answer answer = VillageDisband.Destination(fire);
			Vector3 middle = fire.GetPosition();
			if (phase == VillageDisband.Phase.Gathering || phase == VillageDisband.Phase.Remaining)
			{
				if (Time.time >= nextGatherLine)
				{
					nextGatherLine = Time.time + Random.Range(GatherLineMin, GatherLineMax);
					if (Random.value < GatherLineChance)
					{
						string line = ((phase == VillageDisband.Phase.Gathering) ? VikingRoadSpeech.Disbanded(VillageNames.AtFire(fire)) : VikingRoadSpeech.WhatNow());
						VikingSpeech.Say(humanoid, line, VikingSpeech.TalkTime, VikingSpeech.TalkCull);
					}
				}
				// While they gather, they empty the village's chests, when there's a village to take it to
				if (phase == VillageDisband.Phase.Gathering && answer.Found && UpdateDisbandLoot(dt, zdo, fire))
				{
					return(true);
				}
				return(GatherAt(dt, zdo, middle));
			}
			if (!answer.Found)
			{
				return(false);
			}
			if (!disbandLootDone)
			{
				FinishDisbandLoot(zdo);
			}
			if (phase == VillageDisband.Phase.Announcing)
			{
				if (!announced)
				{
					announced = true;
					if (VillageDisband.Announces(zdo, fire))
					{
						VikingSpeech.Say(humanoid, VikingRoadSpeech.LetsGo(answer.Spoken()), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
					}
				}
				return(GatherAt(dt, zdo, middle));
			}
			if (!goodbyeStarted)
			{
				goodbyeStarted = true;
				StartGoodbye();
			}
			if (goodbyeTo != null && Time.time < runOffAt)
			{
				SayGoodbye();
				return(true);
			}
			zdo.Set(VillageTravel.FromNameKey, VillageNames.AtFire(fire));
			VillageTravel.SetOut(zdo, answer, VillageDisband.GroupName(fire), VillageDisband.PartyOf(fire, OwnBiome()));
			LoadRoad(zdo);
			road = Road.Travelling;
			nextRoadLine = Time.time + Random.Range(RoadLineMin, RoadLineMax) / 3f;
			Plugin.Log.LogInfo(LogName() + " set out from the disbanded village at " + middle.ToString("F0") + " for the village at " + answer.Destination.ToString("F0"));
			return(true);
		}

		/// <summary>
		/// Has the viking come to its spot around a fire and stand there facing it
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="zdo">Its ZDO</param>
		/// <param name="middle">Where the fire stands</param>
		/// <returns>True, as it takes the update over</returns>
		private bool GatherAt(float dt, ZDO zdo, Vector3 middle)
		{
			mover = "gathering at the fire of a disbanded village";
			Vector3 spot = middle + Quaternion.Euler(0f, zdo.m_uid.ID % 360u, 0f) * Vector3.forward * VillageDisband.StandReach;
			if (Utils.DistanceXZ(transform.position, middle) > VillageDisband.GatheredReach - 1f)
			{
				Go(dt, spot, 1.5f, true);
				moveFailed = false;
				return(true);
			}
			Face(middle);
			return(true);
		}

		/// <summary>
		/// Picks the player within GoodbyeRange the viking says goodbye to, if any, and when it says it and runs off
		/// </summary>
		private void StartGoodbye()
		{
			goodbyeTo = Player.GetClosestPlayer(transform.position, GoodbyeRange);
			goodbyeAt = Time.time + Random.Range(GoodbyeDelayMin, GoodbyeDelayMax);
			runOffAt = ((goodbyeTo != null) ? goodbyeAt + GoodbyeStand : Time.time);
		}

		/// <summary>
		/// Has the viking face the player it says goodbye to, then wave and say it
		/// </summary>
		private void SayGoodbye()
		{
			mover = "saying goodbye";
			ai.StopMoving();
			ai.LookAt(goodbyeTo.GetEyePoint());
			if (goodbyeAt >= 0f && Time.time >= goodbyeAt)
			{
				goodbyeAt = -1f;
				VikingPoses.Wave(humanoid);
				VikingSpeech.Say(humanoid, VikingSpeech.Leaving(goodbyeTo, VillageNames.AtFire(VillageDeparted.LeftFire(nview.GetZDO()))), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
			}
		}

		/// <summary>
		/// Forgets the viking's life away from any village, as it turns back to a village or joins one
		/// </summary>
		private void ForgetRoad()
		{
			road = Road.None;
			announced = false;
			goodbyeStarted = false;
			roadForage = null;
			roadLoot = null;
			wanderMoving = false;
			fleeingFrom = null;
		}

		/// <summary>
		/// Describes what the viking is doing away from any village, for the debug hover text
		/// </summary>
		/// <param name="text">The text, added to a line at a time</param>
		private void DescribeRoad(System.Text.StringBuilder text)
		{
			if (road == Road.None || nview == null || !nview.IsValid())
			{
				return;
			}
			ZDO zdo = nview.GetZDO();
			string line = "Road: " + road.ToString().ToLowerInvariant();
			if (VillageTravel.OnRoad(zdo))
			{
				string name = VillageTravel.DestinationName(zdo);
				int biome = VillageTravel.BoatBiome(zdo);
				line += " to " + ((name.Length > 0) ? name + " " : "") + "at " + zdo.GetVec3(VillageTravel.DestinationKey, Vector3.zero).ToString("F0") + ", " + travelRoad.Meters(roadLeg, transform.position).ToString("F0") + "m and " + travelRoad.Seconds(roadLeg, transform.position, TravelTime.BoatSpeed(biome)).ToString("F0") + "s left by " + TravelPlanner.DescribeRoad(travelRoad) + ", " + VillageTravel.PartyOf(zdo).Describe() + ", waypoint " + roadLeg + " of " + travelRoad.Count + (zdo.GetBool(VillageTravel.SailingKey) ? ", setting sail" : "") + (zdo.GetBool(VillageTravel.PortalKey) ? ", stepping through a portal" : "");
			}
			text.Append(line + "\n");
			string crossingLine = DescribeCrossing();
			if (crossingLine.Length > 0)
			{
				text.Append(crossingLine + "\n");
			}
		}
	}
}
