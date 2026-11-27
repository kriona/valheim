using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// How a viking gets about - walking and running to a spot, steering around the others, noticing when it's stuck
	/// or twitching in place and freeing itself when it's trapped, climbing down from where it's stranded
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Where the viking has stood since when, for the debug hover text to tell how long it's been standing still -
		/// it's still while it keeps within StillReach of there, in meters
		/// </summary>
		private Vector3 stillAt;
		private float stillSince;
		private const float StillReach = 1f;

		/// <summary>
		/// When the viking's stillness was last checked, and how long it can go unchecked before it starts over, in
		/// seconds - longer means its life wasn't running here
		/// </summary>
		private float stillCheckedAt;
		private const float StillGap = 5f;

		/// <summary>
		/// How long the viking has to stand still for the debug hover text to say so, and to be logged as stuck at a job
		/// that shouldn't keep it still that long (see LogIfStuck), in seconds
		/// </summary>
		private const float StillShowTime = 10f;
		private const float StuckLogTime = 180f;

		/// <summary>
		/// The job the viking was last logged as stuck at, so each is only logged once
		/// </summary>
		private Job loggedStuck;

		/// <summary>
		/// When the viking was last resting, sitting, lying, in bed, on something or gathering at a fire, which keep it
		/// still without it being stuck (see LogIfStuck)
		/// </summary>
		private float stillRestedAt;

		/// <summary>
		/// Whether the viking tried to walk somewhere this AI update (see Go), and for how many seconds it has tried
		/// while standing still, for FreeIfTrapped
		/// </summary>
		private bool walkedThisUpdate;
		private float walkingStill;

		/// <summary>
		/// How many of the viking's moves were stopped short of a ledge or dangerous ground (see VikingDangerStepPatch)
		/// since it last moved from where it stands, for the log when it's freed
		/// </summary>
		private int stepsStopped;

		/// <summary>
		/// How many seconds a viking tries to walk somewhere without getting anywhere before it's freed (see
		/// FreeIfTrapped), and how far around it a spot to put it is looked for, in steps of FreeStep, in meters, with
		/// FreeSides spots at each step
		/// </summary>
		private const float FreeAfter = 90f;
		private const float FreeReach = 9f;
		private const float FreeStep = 1.5f;
		private const int FreeSides = 8;

		/// <summary>
		/// Writes to the log when the viking has stood still for StuckLogTime at a job that shouldn't keep it still that
		/// long - anything but resting, sitting, lying, in bed or sitting on something, or gathering at a fire, counting
		/// only from when it last stopped doing those, so a night in bed isn't taken for being stuck the morning after -
		/// with what the job says it's doing and what moved the viking last, once for each job, so a job that never
		/// finishes can be found
		/// </summary>
		private void LogIfStuck()
		{
			if (job == null)
			{
				return;
			}
			if (attachPoint != null || inBed || job.Resting() || job.Sitting() || job is GatherJob)
			{
				stillRestedAt = Time.time;
				return;
			}
			float still = Time.time - Mathf.Max(stillSince, stillRestedAt);
			if (job == loggedStuck || still < StuckLogTime)
			{
				return;
			}
			loggedStuck = job;
			TestLog.Count("Troubles", "stuck at a job");
			TestLog.Count("Stuck, by job", LogJobName());
			Plugin.Log.LogWarning("Stuck: " + LogName() + " of the village at " + ((village != null) ? village.Farm.ToString("F0") : "none") + " has stood still for " + still.ToString("F0") + "s at its job: " + job.Describe() + ", for " + (Time.time - job.StartedAt).ToString("F0") + "s, moved by " + mover + (VillageRaids.On(village) ? ", with a raid on" : "") + (IsNight() ? ", at night" : ""));
		}

		/// <summary>
		/// Frees a viking in its village that has tried to walk somewhere for FreeAfter seconds without getting
		/// anywhere - shut in beside its bed by the furniture, at the far side of a pen too big to tell from open ground,
		/// or anywhere else it has no way out of - putting it at the nearest spot with a way to the village's open ground
		/// (see FreeSpot), rather than leaving it there to go hungry. When the spot it stands on has a way there already,
		/// what holds it is something the navmesh doesn't show, which the spots around it share, so it's put at the
		/// open ground itself
		/// </summary>
		private void FreeIfTrapped()
		{
			if (walkingStill < FreeAfter)
			{
				return;
			}
			walkingStill = 0f;
			if (village == null || attachPoint != null || inBed || road != Road.None || !village.Contains(transform.position) || Pathfinding.instance == null)
			{
				return;
			}
			Vector3 from = transform.position;
			Vector3 open = village.RallyPoint();
			bool wayOut = WayToOpenGround(from, open, out string own);
			// What it was doing is written down before it's moved, which starts its path and its moves over
			string report = ((TestLog.On || Player.m_debugMode) ? FreeReport(own) : null);
			Vector3 spot;
			bool found = (wayOut ? NavmeshPoints.FindValidPoint(out spot, open, FreeStep, Pathfinding.AgentType.Humanoid) : FreeSpot(from, open, FreeReach, out spot));
			if (!found)
			{
				return;
			}
			Vector3 freed = spot + Vector3.up * 0.1f;
			transform.position = freed;
			humanoid.m_body.position = freed;
			humanoid.m_body.linearVelocity = Vector3.zero;
			ai.StopMoving();
			ResetStuck();
			TestLog.Count("Troubles", "freed after walking without getting anywhere");
			TestLog.Count("Freed, by where to", (wayOut ? "the open ground, its own spot having a way there" : "the nearest spot with a way to the open ground"));
			if (report != null)
			{
				Plugin.Log.LogInfo("Freed: " + TestLog.Label(humanoid) + " at " + from.ToString("F1") + " tried to walk for " + FreeAfter.ToString("F0") + "s without getting anywhere, moved by " + mover + ", at its job: " + ((job != null) ? job.Describe() : "none") + ", so it's put at " + freed.ToString("F1") + ", " + Utils.DistanceXZ(from, freed).ToString("F1") + "m away" + (wayOut ? ", on the village's open ground" : "") + "\n" + report);
			}
		}

		/// <summary>
		/// Checks whether the spot a viking stands on has a whole way to the village's open ground - on the navmesh
		/// within FreeStep of it, out of any pen and off the roofs, the way FreeSpot checks the spots around it
		/// </summary>
		/// <param name="from">Where the viking stands</param>
		/// <param name="open">The spot on the village's open ground (see Village.RallyPoint)</param>
		/// <param name="state">What was found, for the log</param>
		/// <returns>True when it has</returns>
		private static bool WayToOpenGround(Vector3 from, Vector3 open, out string state)
		{
			if (!NavmeshPoints.FindValidPoint(out Vector3 own, from, FreeStep, Pathfinding.AgentType.Humanoid))
			{
				state = "no navmesh within " + FreeStep.ToString("F1") + "m";
				return(false);
			}
			string at = "its navmesh at " + own.ToString("F1") + ", " + Utils.DistanceXZ(from, own).ToString("F1") + "m away";
			if (Village.FencedIn(own))
			{
				state = at + ", in a pen";
				return(false);
			}
			if (NavmeshRoofs.OnRoof(own))
			{
				state = at + ", on a roof";
				return(false);
			}
			if (!Pathfinding.instance.HavePath(own, open, Pathfinding.AgentType.Humanoid))
			{
				state = at + ", with no whole way to the open ground at " + open.ToString("F1");
				return(false);
			}
			state = at + ", with a whole way to the open ground at " + open.ToString("F1");
			return(true);
		}

		/// <summary>
		/// Describes what a viking about to be freed was doing, for the log - the spot it stands on, the path its AI
		/// follows, whether it was trying to move and how fast it went, how many of its moves stopped short of a ledge
		/// or dangerous ground, and its last moves
		/// </summary>
		/// <param name="own">What WayToOpenGround found for the spot it stands on</param>
		/// <returns>The report's lines</returns>
		private string FreeReport(string own)
		{
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			text.Append("  Own spot: " + own + "\n");
			text.Append("  Path: " + PathText() + ", the last search " + ((ai != null && ai.m_lastFindPathResult) ? "found a way" : "found none") + "\n");
			Vector3 move = humanoid.GetMoveDir();
			Vector3 velocity = humanoid.m_body.linearVelocity;
			text.Append("  Moving: " + ((new Vector3(move.x, 0f, move.z).sqrMagnitude > 0.01f) ? "trying to" : "not trying to") + ", at " + new Vector3(velocity.x, 0f, velocity.z).magnitude.ToString("F2") + "m/s\n");
			text.Append("  Steps stopped at a ledge or dangerous ground: " + stepsStopped + " since it last got anywhere\n");
			text.Append("  Trace (M moving, . still, then facing): " + MoveTrace());
			return(text.ToString());
		}

		/// <summary>
		/// Finds the nearest spot around a trapped viking it can walk to the village's open ground from - on the navmesh,
		/// out of any pen, off the roofs and clear of fires, looking further out a FreeStep at a time, up to a reach -
		/// or else the open ground itself
		/// </summary>
		/// <param name="from">Where the viking stands</param>
		/// <param name="open">The spot on the village's open ground (see Village.RallyPoint)</param>
		/// <param name="reach">How far out it looks, in meters</param>
		/// <param name="spot">The spot found</param>
		/// <returns>False when the open ground isn't on the navmesh either</returns>
		private static bool FreeSpot(Vector3 from, Vector3 open, float reach, out Vector3 spot)
		{
			for (float step = FreeStep; step <= reach; step += FreeStep)
			{
				for (int i = 0; i < FreeSides; i++)
				{
					Vector3 point = from + Quaternion.Euler(0f, i * 360f / FreeSides, 0f) * Vector3.forward * step;
					if (!NavmeshPoints.FindValidPoint(out Vector3 valid, point, FreeStep, Pathfinding.AgentType.Humanoid))
					{
						continue;
					}
					if (Village.FencedIn(valid) || NavmeshRoofs.OnRoof(valid) || Village.NearFlames(valid))
					{
						continue;
					}
					if (Pathfinding.instance.HavePath(valid, open, Pathfinding.AgentType.Humanoid))
					{
						spot = valid;
						return(true);
					}
				}
			}
			return(NavmeshPoints.FindValidPoint(out spot, open, FreeStep, Pathfinding.AgentType.Humanoid));
		}

		/// <summary>
		/// Checks whether a point is in the village's core, its vikings' home
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>True when it is</returns>
		private bool AtHome(Vector3 point)
		{
			return(Utils.DistanceXZ(point, village.CoreMiddle) <= village.CoreRadius);
		}

		/// <summary>
		/// Bends the way the viking's AI is about to move around the creatures and players in its way rather than into
		/// them - to the side of each one ahead of it, harder the nearer it is. The enemy it's after and who or what its
		/// job is about are left out, so it still walks right up to them
		/// </summary>
		/// <remarks>
		/// The navmesh only knows the ground and what's built on it, not creatures, so the AI's way runs straight
		/// through anyone standing on it. The bend goes into the AI's own move, before it turns, since the AI only moves
		/// once it faces its way - bending the move after it would turn the viking off its way, so the AI would stop to
		/// turn back, again and again. The side it passes each one on is kept for AvoidMemory seconds, so one straight
		/// ahead doesn't have it weaving from side to side
		/// </remarks>
		/// <param name="dir">The way the AI is moving, normalized</param>
		/// <returns>The way to move</returns>
		public Vector3 Steer(Vector3 dir)
		{
			// Walking straight through a doorway, any steering would only push it into the door frame, and a jump goes
			// where it was aimed
			if (humanoid == null || nview == null || !nview.IsValid() || !nview.IsOwner() || humanoid.IsDead() || attachPoint != null || humanoid.InAttack() || !humanoid.IsOnGround() || hopPhase != HopPhase.None || VikingDoors.IsCrossing(humanoid))
			{
				return(dir);
			}
			Vector3 heading = new Vector3(dir.x, 0f, dir.z);
			if (heading.sqrMagnitude < 0.01f)
			{
				return(dir);
			}
			heading.Normalize();
			Vector3 side = Vector3.Cross(Vector3.up, heading);
			Vector3 steer = Vector3.zero;
			avoidNear.Clear();
			Character.GetCharactersInRange(transform.position, AvoidRange, avoidNear);
			Character target = ((ai != null) ? ai.m_targetCreature : null);
			Character focus = ((job != null) ? job.Focus() : null);
			steeredAround = null;
			foreach (Character other in avoidNear)
			{
				if (other == null || other == humanoid || other == target || other == focus || other.IsDead())
				{
					continue;
				}
				Vector3 toOther = other.transform.position - transform.position;
				toOther.y = 0f;
				float ahead = Vector3.Dot(toOther, heading);
				float across = Vector3.Dot(toOther, side);
				float clearance = humanoid.GetRadius() + other.GetRadius() + AvoidClearance;
				if (ahead <= 0f || Mathf.Abs(across) > clearance)
				{
					continue;
				}
				// One on the other side of a wall or fence isn't in the way
				if (WallBetween(other))
				{
					continue;
				}
				// It passes on the side it picked for this one before, or else the side the other is already off to
				float pass = ((across >= 0f) ? -1f : 1f);
				if (avoidSides.TryGetValue(other, out KeyValuePair<float, float> kept) && Time.time - kept.Key < AvoidMemory)
				{
					pass = kept.Value;
				}
				avoidSides[other] = new KeyValuePair<float, float>(Time.time, pass);
				float push = (1f - Mathf.Clamp01(ahead / AvoidRange)) * (1f - Mathf.Abs(across) / clearance);
				steer += side * pass * Mathf.Max(push, AvoidMinPush) * AvoidStrength;
				steeredAround = other;
				steeredAt = Time.time;
			}
			if (avoidSides.Count > AvoidSidesMax)
			{
				avoidSides.Clear();
			}
			if (steer == Vector3.zero)
			{
				return(dir);
			}
			Vector3 bent = (heading + steer).normalized;
			return(new Vector3(bent.x, dir.y, bent.z));
		}

		/// <summary>
		/// How far above the next point of its path a twitching viking has to stand to count as stuck on top of
		/// something, and how near that point has to be, seen from above, for it to climb down to it, in meters
		/// </summary>
		private const float StrandHeight = 1.5f;
		private const float StrandReach = 3f;

		/// <summary>
		/// Climbs a twitching viking down from on top of something it's stuck on - a roof, or furniture it got onto - to
		/// the next point of its path, when that's well below it and close by, rather than leaving it turning back and
		/// forth at the edge, unable to step down to the way it's going
		/// </summary>
		/// <returns>True when it climbed down</returns>
		private bool ClimbDown()
		{
			if (ai == null || ai.m_path == null || ai.m_path.Count == 0 || attachPoint != null || !humanoid.IsOnGround())
			{
				return(false);
			}
			Vector3 next = ai.m_path[0];
			if (!Stranded(transform.position, next))
			{
				return(false);
			}
			Vector3 down = next + Vector3.up * 0.1f;
			TestLog.Count("Troubles", "climbed down from on top of something");
			if (TestLog.On || Player.m_debugMode)
			{
				Plugin.Log.LogInfo("Climb: " + TestLog.Name(humanoid, "F1") + " was stuck " + (transform.position.y - next.y).ToString("F1") + "m above its path, so it climbs down to " + down.ToString("F1"));
			}
			transform.position = down;
			humanoid.m_body.position = down;
			humanoid.m_body.linearVelocity = Vector3.zero;
			return(true);
		}

		/// <summary>
		/// Checks whether a viking stands stuck above the next point of its path - at least StrandHeight above it, and
		/// within StrandReach of it seen from above
		/// </summary>
		/// <param name="at">Where it stands</param>
		/// <param name="next">The next point of its path</param>
		/// <returns>True when it does</returns>
		internal static bool Stranded(Vector3 at, Vector3 next)
		{
			float dx = at.x - next.x;
			float dz = at.z - next.z;
			return(at.y - next.y >= StrandHeight && dx * dx + dz * dz <= StrandReach * StrandReach);
		}

		/// <summary>
		/// Counts the times in the last TwitchWindow seconds the viking started moving after standing still, and turned
		/// back the other way after turning one way
		/// </summary>
		/// <param name="starts">How many times it started moving</param>
		/// <param name="reversals">How many times it turned back</param>
		private void CountTwitches(out int starts, out int reversals)
		{
			starts = 0;
			reversals = 0;
			bool first = true;
			bool wasMoving = false;
			float lastYaw = 0f;
			float lastTurn = 0f;
			foreach (MoveSample sample in moveSamples)
			{
				if (!first)
				{
					if (sample.Moving && !wasMoving)
					{
						starts++;
					}
					float turn = Mathf.DeltaAngle(lastYaw, sample.Yaw);
					if (Mathf.Abs(turn) >= TwitchTurn)
					{
						if (lastTurn != 0f && Mathf.Sign(turn) != Mathf.Sign(lastTurn))
						{
							reversals++;
						}
						lastTurn = turn;
					}
				}
				first = false;
				wasMoving = sample.Moving;
				lastYaw = sample.Yaw;
			}
		}

		/// <summary>
		/// Describes the viking's twitching and what it was doing, for the log
		/// </summary>
		/// <param name="starts">How many times it started moving in the last TwitchWindow seconds</param>
		/// <param name="reversals">How many times it turned back</param>
		/// <returns>The report</returns>
		private string TwitchReport(int starts, int reversals)
		{
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			text.Append("Twitching: " + TestLog.Name(humanoid, "F1") + " started moving " + starts + " times and turned back " + reversals + " times in " + TwitchWindow.ToString("F0") + "s\n");
			text.Append("  Job: " + ((job != null) ? job.Describe() : "none") + "\n");
			Character target = ((ai != null) ? ai.m_targetCreature : null);
			text.Append("  Target: " + ((target != null) ? TestLog.Label(target) + " at " + Vector3.Distance(target.transform.position, transform.position).ToString("F1") + "m" : "none") + ", alerted " + (ai != null && ai.IsAlerted()) + "\n");
			text.Append("  Path: " + PathText() + "\n");
			text.Append("  Stuck: " + ((stuckSince > 0f) ? (Time.time - stuckSince).ToString("F1") + "s without progress" : "no") + ", hops " + hops + ", door " + (VikingDoors.IsWaiting(humanoid) ? "waiting" : (VikingDoors.IsCrossing(humanoid) ? "crossing" : "none")) + "\n");
			text.Append("  Steering around: " + ((steeredAround != null && Time.time - steeredAt < SteeredShowTime) ? TestLog.Label(steeredAround) + " at " + Vector3.Distance(steeredAround.transform.position, transform.position).ToString("F1") + "m" : "no one") + "\n");
			Dictionary<string, int> movers = new Dictionary<string, int>();
			foreach (MoveSample sample in moveSamples)
			{
				movers[sample.Mover] = (movers.TryGetValue(sample.Mover, out int count) ? count : 0) + 1;
			}
			List<string> moverText = new List<string>();
			foreach (KeyValuePair<string, int> entry in movers)
			{
				moverText.Add(entry.Key + " x" + entry.Value);
			}
			text.Append("  Moved by: " + string.Join(", ", moverText) + "\n");
			text.Append("  Trace (M moving, . still, then facing): " + MoveTrace());
			return(text.ToString());
		}

		/// <summary>
		/// Describes the path the viking's AI is following, for the log
		/// </summary>
		/// <returns>How many points it has left and where the next is, or "none"</returns>
		private string PathText()
		{
			if (ai != null && ai.m_path != null && ai.m_path.Count > 0)
			{
				return(ai.m_path.Count + " points left, next " + Utils.DistanceXZ(ai.m_path[0], transform.position).ToString("F1") + "m away at " + ai.m_path[0].ToString("F1"));
			}
			return("none");
		}

		/// <summary>
		/// Lists the viking's moves over the last TwitchWindow seconds, for the log - M when it was moving or . when it
		/// was still, then the way it faced
		/// </summary>
		/// <returns>The moves, oldest first</returns>
		private string MoveTrace()
		{
			System.Text.StringBuilder trace = new System.Text.StringBuilder();
			foreach (MoveSample sample in moveSamples)
			{
				trace.Append((sample.Moving ? "M" : ".") + Mathf.RoundToInt(sample.Yaw) + " ");
			}
			return(trace.ToString().TrimEnd());
		}

		/// <summary>
		/// Notes that the viking's move was stopped short of a ledge or dangerous ground (see VikingDangerStepPatch), for
		/// the log when it's freed
		/// </summary>
		public void NoteStepStopped()
		{
			stepsStopped++;
		}

		/// <summary>
		/// Checks whether a wall, fence or other solid thing stands between the viking and a creature, at knee height
		/// </summary>
		/// <param name="other">The creature</param>
		/// <returns>True when one does</returns>
		private bool WallBetween(Character other)
		{
			if (hopMask == 0)
			{
				hopMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece");
			}
			Vector3 knee = Vector3.up * 0.5f;
			return(Physics.Linecast(transform.position + knee, other.transform.position + knee, hopMask, QueryTriggerInteraction.Ignore));
		}

		/// <summary>
		/// Walks the viking toward a spot, through the doors in its way - running when it asks to, when it's away from
		/// home or on a trip to another village (see AwayFromHome), or when it's climbing a slope
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="target">The spot</param>
		/// <param name="reach">How near it has to get</param>
		/// <param name="run">True to run</param>
		/// <returns>True once it's there, or when it gave up, which sets moveFailed</returns>
		private bool Go(float dt, Vector3 target, float reach, bool run)
		{
			if (WaitForPoison(target, out bool poisonGaveUp))
			{
				return(poisonGaveUp);
			}
			run = (run || AwayFromHome() || RunsInRain() || (viking != null && viking.Climbing()));
			// Something in a pen with no whole path to it is walked to by a side of its fence low enough to jump, rather
			// than the nearest spot outside, which on a slope can stand at the foot of a bank below the fence
			if (Utils.DistanceXZ(transform.position, target) > Mathf.Max(reach, ArriveReach) && PenWayIn(target, out Vector3 wayIn))
			{
				return(GoIntoPen(dt, target, wayIn, run));
			}
			bool viaDoor = VikingDoors.Steer(humanoid, target, out Vector3 waypoint, out bool direct);
			mover = (direct ? "straight through a door" : (viaDoor ? "path to a door" : "path")) + " for " + ((job != null) ? job.GetType().Name : "village life");
			if (direct)
			{
				walkedThisUpdate = true;
				ai.MoveTowards(waypoint - transform.position, run);
				bool gaveUp = CheckStuck(target);
				AskWayWhenHeldUp(waypoint);
				return(gaveUp);
			}
			// MoveTo also stops when it finds no way there, which CheckStuck gives up on once no progress is made
			ai.MoveTo(dt, waypoint, (viaDoor ? 0.5f : reach), run);
			if (!viaDoor && Utils.DistanceXZ(transform.position, target) <= Mathf.Max(reach, ArriveReach))
			{
				ai.StopMoving();
				ResetStuck();
				return(true);
			}
			walkedThisUpdate = true;
			// Waiting its turn at a door isn't being stuck, though it asks whoever stands about in the doorway to make way
			if (viaDoor && VikingDoors.IsWaiting(humanoid))
			{
				ResetStuck();
				Character standing = VikingDoors.DoorwayBlocker(humanoid);
				if (standing != null && Time.time >= nextAskWay && standing.TryGetComponent(out VillageLife other))
				{
					nextAskWay = Time.time + MakeWayAfter;
					other.MakeWay(transform.position, standing.transform.position - transform.position);
				}
				return(false);
			}
			bool stuck = CheckStuck(target);
			AskWayWhenHeldUp(waypoint);
			return(stuck);
		}

		/// <summary>
		/// Holds the viking back from a spot within PoisonAvoid of poison once it's within PoisonWaitReach of where it
		/// keeps clear of it, standing there until the poison is gone - a cloud lasts moments - and giving up on the spot
		/// after PoisonWaitTime seconds, the way it gives up on a spot it can't reach, so its job moves on
		/// </summary>
		/// <param name="target">Where the viking is walking</param>
		/// <param name="gaveUp">Set to true when it gives up on the spot, which sets moveFailed</param>
		/// <returns>True while it holds back, so it doesn't walk on</returns>
		private bool WaitForPoison(Vector3 target, out bool gaveUp)
		{
			gaveUp = false;
			if (!VillageHazards.NearestPoison(target, PoisonAvoid, out Vector3 poison) || Utils.DistanceXZ(transform.position, poison) > PoisonAvoid + PoisonWaitReach)
			{
				poisonWaitSince = -1f;
				return(false);
			}
			mover = "waiting for poison to go for " + ((job != null) ? job.GetType().Name : "village life");
			ai.StopMoving();
			ResetStuck();
			// A wait broken off for longer than PoisonWaitBreak - for a fight, or a job that doesn't walk - starts over
			if (poisonWaitSince < 0f || Time.time - poisonWaitLast > PoisonWaitBreak)
			{
				poisonWaitSince = Time.time;
			}
			poisonWaitLast = Time.time;
			if (Time.time - poisonWaitSince >= PoisonWaitTime)
			{
				poisonWaitSince = -1f;
				moveFailed = true;
				gaveUp = true;
				TestLog.Count("Walks given up for poison, by job", ((job != null) ? LogJobName() : "village life"));
			}
			return(true);
		}

		/// <summary>
		/// Asks the vikings standing about in front of the viking to make way, once it has got no nearer where it's
		/// going for MakeWayAfter seconds, and no more often than that
		/// </summary>
		/// <param name="toward">Where it's walking next</param>
		private void AskWayWhenHeldUp(Vector3 toward)
		{
			if (Time.time - stuckSince < MakeWayAfter || Time.time < nextAskWay)
			{
				return;
			}
			nextAskWay = Time.time + MakeWayAfter;
			Vector3 heading = toward - transform.position;
			heading.y = 0f;
			if (heading.sqrMagnitude < 0.01f)
			{
				return;
			}
			heading.Normalize();
			foreach (VillageLife other in All)
			{
				if (other == this || !other.active)
				{
					continue;
				}
				Vector3 offset = other.transform.position - transform.position;
				offset.y = 0f;
				float ahead = Vector3.Dot(offset, heading);
				if (ahead < 0f || ahead > MakeWayReach || Mathf.Abs(heading.x * offset.z - heading.z * offset.x) > MakeWayWidth)
				{
					continue;
				}
				other.MakeWay(transform.position, heading);
			}
		}

		/// <summary>
		/// Has the viking step aside for another walking past, when it's only standing about, strolling, talking,
		/// petting a boar or standing in the ring at the fire - out to the side of the other's way, to the side it's on
		/// already, onto open ground
		/// </summary>
		/// <param name="from">Where the other viking is</param>
		/// <param name="heading">The way the other is walking</param>
		public void MakeWay(Vector3 from, Vector3 heading)
		{
			if (!active || attachPoint != null || ai.GetTargetCreature() != null || Time.time < makeWayUntil)
			{
				return;
			}
			bool idle = (job is IdleJob || job is StrollJob || job is TalkJob || job is PetJob || (job is GatherJob gather && !gather.Sitting()));
			if (!idle)
			{
				return;
			}
			heading.y = 0f;
			if (heading.sqrMagnitude < 0.01f)
			{
				return;
			}
			heading.Normalize();
			Vector3 offset = transform.position - from;
			Vector3 side = new Vector3(-heading.z, 0f, heading.x);
			if (Vector3.Dot(offset, side) < 0f)
			{
				side = -side;
			}
			// To its own side of the other's way first, then the other side, then on ahead out of the way
			Vector3[] tries = new[] { transform.position + side * MakeWayStep, transform.position - side * MakeWayStep, transform.position + heading * MakeWayStep * 1.5f };
			foreach (Vector3 point in tries)
			{
				if (NavmeshPoints.FindValidPoint(out Vector3 spot, point, 1f, Pathfinding.AgentType.Humanoid) && Utils.DistanceXZ(spot, from) > Utils.DistanceXZ(transform.position, from) - 0.5f)
				{
					makeWaySpot = spot;
					makeWayUntil = Time.time + MakeWayTime;
					return;
				}
			}
		}

		/// <summary>
		/// Walks the viking aside while it's making way for another (see MakeWay), putting its job off
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True while it's stepping aside, so the AI's own update is skipped</returns>
		private bool UpdateMakeWay(float dt)
		{
			if (Time.time >= makeWayUntil)
			{
				return(false);
			}
			if (Go(dt, makeWaySpot, 0.4f, false))
			{
				makeWayUntil = 0f;
				moveFailed = false;
				ai.StopMoving();
				return(false);
			}
			return(true);
		}

		/// <summary>
		/// Notes whether the viking is getting anywhere on its way to a spot - nearer it, or at least moving, since the
		/// way can lead away from the spot first, like around a house to its door - jumping whatever low thing is in its
		/// way when it has stood still for HopAfter seconds and there's no way around - writing to the log why it doesn't
		/// jump, once a walk (see LogHopRefused) - and giving up when it has for StuckTime seconds
		/// </summary>
		/// <param name="target">Where it's walking</param>
		/// <returns>True when it gave up</returns>
		private bool CheckStuck(Vector3 target)
		{
			float distance = Utils.DistanceXZ(transform.position, target);
			if (Vector3.Distance(target, stuckTarget) > 0.5f)
			{
				hops = 0;
				hopRefusedLogged = false;
			}
			bool moved = (Utils.DistanceXZ(transform.position, stuckFrom) >= StuckMove);
			if (Vector3.Distance(target, stuckTarget) > 0.5f || distance < stuckBest - 0.5f || moved)
			{
				stuckTarget = target;
				stuckBest = distance;
				stuckFrom = transform.position;
				stuckSince = Time.time;
				doorTried = false;
				return(false);
			}
			// A door beside it, standing open across its way or shut in front of it, is opened or closed once before it
			// tries jumping anything
			if (!doorTried && Time.time - stuckSince >= HopAfter && VikingDoors.Unstick(humanoid))
			{
				doorTried = true;
				stuckSince = Time.time;
				return(false);
			}
			if (Time.time - stuckSince >= HopAfter && hops < HopsMax)
			{
				if (TryHop(target, false))
				{
					hops++;
					stuckSince = Time.time;
					return(false);
				}
				// Why it doesn't jump is written once for each walk it's held up on
				if (!hopRefusedLogged)
				{
					hopRefusedLogged = true;
					LogHopRefused(target);
				}
			}
			if (Time.time - stuckSince < StuckTime)
			{
				return(false);
			}
			ai.StopMoving();
			moveFailed = true;
			TestLog.Count("Walks given up, by job", LogJobName());
			ResetStuck();
			return(true);
		}

		/// <summary>
		/// Forgets how near the viking got to where it was walking
		/// </summary>
		private void ResetStuck()
		{
			stuckTarget = Vector3.positiveInfinity;
			stuckBest = float.MaxValue;
			stuckFrom = transform.position;
			stuckSince = Time.time;
			hops = 0;
			doorTried = false;
			hopRefusedLogged = false;
		}
	}
}
