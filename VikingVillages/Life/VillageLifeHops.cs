using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Chasing an enemy over the village's fence, and hopping fences and pen walls the way a player jumps
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Notes how near the viking has got to the enemy it's after, and jumps whatever low thing is between them when
		/// it hasn't got nearer for HopAfter seconds and there's no way around - so a viking in a pen gets out to fight.
		/// A viking fighting hand to hand with a pen's fence between it and the enemy goes over the fence for up to
		/// FenceChaseTime seconds rather than swinging through it (see ChaseOverFence). One slaughtering livestock never
		/// strikes it through a fence, with a bow or not - it keeps going over for SlaughterFenceTime seconds, then gives
		/// the animal up
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="target">The enemy</param>
		/// <returns>True when it jumped, is on its way over the fence or gave up the slaughter, so the AI's own update is
		/// skipped</returns>
		private bool UpdateChase(float dt, Character target)
		{
			float distance = Utils.DistanceXZ(transform.position, target.transform.position);
			if (target != chased)
			{
				fenceSince = Time.time;
				nextFenceCheck = 0f;
			}
			HuntJob slaughter = ((job is HuntJob hunt && hunt.Slaughters() && hunt.IsPreyOf(target)) ? hunt : null);
			if ((slaughter != null || Time.time - fenceSince < FenceChaseTime) && FenceBetween(target, (slaughter != null)))
			{
				chased = target;
				if (slaughter != null && Time.time - fenceSince >= SlaughterFenceTime)
				{
					slaughter.GiveUp();
					ai.StopMoving();
					return(true);
				}
				return(ChaseOverFence(dt, target));
			}
			if (target != chased || distance < chaseBest - 0.5f || humanoid.InAttack())
			{
				chased = target;
				chaseBest = distance;
				chaseSince = Time.time;
				return(false);
			}
			if (Time.time - chaseSince < HopAfter)
			{
				return(false);
			}
			chaseSince = Time.time;
			return(TryHop(target.transform.position, false));
		}

		/// <summary>
		/// Checks whether a pen's fence stands between the viking and an enemy it would fight hand to hand - close by,
		/// with a building piece in the way at a fence's height (see PieceAcross) and one of them in a pen and the other
		/// not, or for livestock it slaughters, the animal in a pen - looking again every FenceCheckInterval seconds, and
		/// noting whether there's a way around it
		/// </summary>
		/// <remarks>
		/// A spot just outside a pen, between its fence and a house or the village's fence, can count as fenced in too,
		/// so livestock in a pen is taken to be apart from the viking whenever a piece stands between them
		/// </remarks>
		/// <param name="target">The enemy</param>
		/// <param name="slaughter">True for livestock the viking slaughters, which it looks for the fence for even when it
		/// can shoot over it</param>
		/// <returns>True when there is</returns>
		private bool FenceBetween(Character target, bool slaughter)
		{
			if (Time.time < nextFenceCheck)
			{
				return(fenceBetween);
			}
			nextFenceCheck = Time.time + FenceCheckInterval;
			fenceBetween = false;
			if (!slaughter && HasBow(humanoid))
			{
				return(false);
			}
			float length = Vector3.Distance(transform.position, target.transform.position);
			if (length < 0.1f || length > FenceReach || !PieceAcross(transform.position, target.transform.position))
			{
				return(false);
			}
			bool targetPenned = Village.FencedIn(target.transform.position);
			fenceBetween = ((slaughter && targetPenned) || targetPenned != Village.FencedIn(transform.position));
			fencePath = (fenceBetween && Pathfinding.instance != null && Pathfinding.instance.HavePath(transform.position, target.transform.position, Pathfinding.AgentType.Humanoid));
			return(fenceBetween);
		}

		/// <summary>
		/// Checks whether a building piece stands between two spots on the ground at a fence's height - looking low, in
		/// the middle and high on a fence, so the gaps between its rails don't hide it
		/// </summary>
		/// <param name="from">One spot, on the ground</param>
		/// <param name="to">The other, on the ground</param>
		/// <returns>True when one does</returns>
		private static bool PieceAcross(Vector3 from, Vector3 to)
		{
			EnsureHopMask();
			foreach (float height in FenceHeights)
			{
				Vector3 start = from + Vector3.up * height;
				Vector3 line = to + Vector3.up * height - start;
				float length = line.magnitude;
				if (length < 0.1f)
				{
					return(false);
				}
				if (Physics.Raycast(start, line / length, out RaycastHit hit, length, hopMask, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<WearNTear>() != null)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Takes the viking over a pen's fence to the enemy on the other side - by the way around when there is one, like
		/// a gate, or else straight at the fence and over it once it's right in front
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="target">The enemy</param>
		/// <returns>True, so the AI's own update is skipped</returns>
		private bool ChaseOverFence(float dt, Character target)
		{
			if (fencePath)
			{
				mover = "going around a fence";
				ai.MoveTo(dt, target.transform.position, 0f, true);
				return(true);
			}
			if (TryHop(target.transform.position, true))
			{
				return(true);
			}
			mover = "going for a fence";
			Vector3 direction = target.transform.position - transform.position;
			direction.y = 0f;
			if (direction.sqrMagnitude > 0.01f)
			{
				ai.MoveTowards(direction.normalized, true);
			}
			return(true);
		}

		/// <summary>
		/// Sets up the layers a jump looks for obstacles on, the first time it's wanted
		/// </summary>
		private static void EnsureHopMask()
		{
			if (hopMask == 0)
			{
				hopMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece");
			}
		}

		/// <summary>
		/// How many times a viking shut in a pen tries to jump out before it climbs over instead - a pen whose ground
		/// inside lies lower than its fence's foot outside can have a fence too high to jump
		/// </summary>
		private const int PenHopTries = 3;

		/// <summary>
		/// The viking's jumps out of the pen it's shut in, for ClimbOutOfPen
		/// </summary>
		private int penHops;

		/// <summary>
		/// Checks whether something in the viking's way is a building piece it jumps when there's no way around it - like
		/// a fence or a low wall, but never a crafting station or its extension, a chest, bed, chair, fire or other
		/// furniture, or a roof or beam, any of which would only leave it stuck on top
		/// </summary>
		/// <param name="collider">What's in its way</param>
		/// <returns>True when it jumps it</returns>
		private static bool Hoppable(Collider collider)
		{
			WearNTear piece = collider.GetComponentInParent<WearNTear>();
			if (piece == null)
			{
				return(false);
			}
			if (piece.GetComponentInChildren<Container>() != null || piece.GetComponentInChildren<CraftingStation>() != null || piece.GetComponentInChildren<StationExtension>() != null || piece.GetComponentInChildren<Bed>() != null || piece.GetComponentInChildren<Chair>() != null || piece.GetComponentInChildren<Fireplace>() != null)
			{
				return(false);
			}
			Piece info = piece.GetComponent<Piece>();
			if (info != null && (info.m_category == Piece.PieceCategory.Furniture || info.m_category == Piece.PieceCategory.Crafting))
			{
				return(false);
			}
			return(HoppableName(Utils.GetPrefabName(piece.gameObject)));
		}

		/// <summary>
		/// Checks whether a building piece's name leaves it one the viking jumps - anything but a roof or a beam, which
		/// slope or stand up off the ground for it to end up stuck on
		/// </summary>
		/// <param name="name">The piece's prefab name</param>
		/// <returns>True when it does</returns>
		internal static bool HoppableName(string name)
		{
			return(!name.Contains("roof") && !name.Contains("beam"));
		}

		/// <summary>
		/// Climbs the viking over a pen's fence it's shut in by (see ShutIn), once it has tried to jump out more than
		/// PenHopTries times without getting out - putting it down on the far side, where its jump would land - rather
		/// than leaving it shut in, jumping and falling back, for as long as the fence is too high for it
		/// </summary>
		/// <param name="feet">Where it stands</param>
		/// <param name="landing">Where its jump would land, past the fence, on the ground</param>
		/// <param name="obstacle">The fence in its way</param>
		/// <returns>True when it climbed over</returns>
		private bool ClimbOutOfPen(Vector3 feet, Vector3 landing, RaycastHit obstacle)
		{
			bool shutIn = ShutIn(feet, landing, out Vector3 outside);
			if (!PenHopDue(ref penHops, shutIn))
			{
				return(false);
			}
			Vector3 over = outside + Vector3.up * 0.1f;
			transform.position = over;
			humanoid.m_body.position = over;
			humanoid.m_body.linearVelocity = Vector3.zero;
			ai.StopMoving();
			TestLog.Count("Troubles", "climbed out of a pen");
			if (TestLog.On || Player.m_debugMode)
			{
				WearNTear piece = obstacle.collider.GetComponentInParent<WearNTear>();
				Plugin.Log.LogInfo("Climb: " + TestLog.Name(humanoid, "F1") + " couldn't jump the " + ((piece != null) ? Utils.GetPrefabName(piece.gameObject) : "fence") + " at " + obstacle.point.ToString("F1") + " out of a pen, so it climbs over to " + over.ToString("F1"));
			}
			return(true);
		}

		/// <summary>
		/// Counts a viking's jump out of a pen toward climbing over its fence (see ClimbOutOfPen) - counting each jump it
		/// makes while it's shut in, however far apart, and starting over once it isn't
		/// </summary>
		/// <param name="hops">Its jumps out so far, counted on, or set back to none</param>
		/// <param name="shutIn">True when it's shut in as it jumps</param>
		/// <returns>True when it has jumped more than PenHopTries times without getting out, so it climbs over</returns>
		internal static bool PenHopDue(ref int hops, bool shutIn)
		{
			if (!shutIn)
			{
				hops = 0;
				return(false);
			}
			hops++;
			if (hops <= PenHopTries)
			{
				return(false);
			}
			hops = 0;
			return(true);
		}

		/// <summary>
		/// Checks whether the viking is shut in where it stands, with its jump the way out - in its village, when it has no
		/// whole path to the village's open ground (see Village.RallyPoint) and the navmesh where its jump would land
		/// does, which tells a pen too big to see as fenced in from its far side as well as a small one, and never takes
		/// it into one. Out of its village, when it stands fenced in and its jump would land outside (see
		/// Village.FencedIn)
		/// </summary>
		/// <param name="feet">Where it stands</param>
		/// <param name="landing">Where its jump would land, past the fence, on the ground</param>
		/// <param name="outside">Where it would be put down out of it - on the navmesh near the landing, when there's
		/// that</param>
		/// <returns>True when it's shut in</returns>
		private bool ShutIn(Vector3 feet, Vector3 landing, out Vector3 outside)
		{
			outside = landing;
			if (village == null || Pathfinding.instance == null || !village.Contains(feet))
			{
				return(Village.FencedIn(feet) && !Village.FencedIn(landing));
			}
			Vector3 open = village.RallyPoint();
			if (WayToOpenGround(feet, open))
			{
				return(false);
			}
			if (!NavmeshPoints.FindValidPoint(out Vector3 ground, landing, HopLanding, Pathfinding.AgentType.Humanoid) || !WayToOpenGround(ground, open))
			{
				return(false);
			}
			outside = ground;
			return(true);
		}

		/// <summary>
		/// Checks whether a spot loot is reached from lies in a pen by the way there - in the viking's village, open to the
		/// sky, with no whole path from it to the village's open ground (see Village.RallyPoint) while there is one from
		/// the viking - which tells the ground inside a pen too big to see as fenced in from its far side (see
		/// Village.FencedIn)
		/// </summary>
		/// <param name="stand">Where the viking would stand to reach the loot, on the navmesh</param>
		/// <returns>True when it lies in a pen</returns>
		private bool PennedByPaths(Vector3 stand)
		{
			if (village == null || Pathfinding.instance == null || !village.Contains(stand) || Village.Roofed(stand))
			{
				return(false);
			}
			Vector3 open = village.RallyPoint();
			return(WayToOpenGround(transform.position, open) && !WayToOpenGround(stand, open));
		}

		/// <summary>
		/// Checks whether there's a whole path from a spot to the village's open ground, which there isn't from inside a
		/// pen, however big
		/// </summary>
		/// <param name="from">The spot</param>
		/// <param name="open">The spot on the village's open ground (see Village.RallyPoint)</param>
		/// <returns>True when there is</returns>
		private static bool WayToOpenGround(Vector3 from, Vector3 open)
		{
			return(Pathfinding.instance.HavePath(from, open, Pathfinding.AgentType.Humanoid));
		}

		/// <summary>
		/// Starts the viking's jump over something low right in front of it on its way to a spot, like a fence, when
		/// there's no way around it - backing up and running at it, then jumping with its own jump (see UpdateHop) - or
		/// climbs out of a pen it can't jump out of (see ClimbOutOfPen)
		/// </summary>
		/// <param name="toward">Where it's going</param>
		/// <param name="fighting">True when it's going over a pen's fence to an enemy, which it jumps into the pen for,
		/// knowing there's no way around</param>
		/// <returns>True when it started</returns>
		private bool TryHop(Vector3 toward, bool fighting)
		{
			if (!fighting && Pathfinding.instance != null && Pathfinding.instance.HavePath(transform.position, toward, Pathfinding.AgentType.Humanoid))
			{
				return(false);
			}
			Vector3 direction = toward - transform.position;
			direction.y = 0f;
			if (direction.sqrMagnitude < 0.01f || !humanoid.IsOnGround())
			{
				return(false);
			}
			direction.Normalize();
			EnsureHopMask();
			Vector3 feet = transform.position;
			// Only a building piece like a fence is jumped - a rock is walked around, never climbed onto
			bool low = (Physics.Raycast(feet + Vector3.up * 0.4f, direction, out RaycastHit obstacle, HopReach, hopMask, QueryTriggerInteraction.Ignore) && Hoppable(obstacle.collider));
			bool high = Physics.Raycast(feet + Vector3.up * HopHeight, direction, HopReach + 0.5f, hopMask, QueryTriggerInteraction.Ignore);
			if (!low || high)
			{
				return(false);
			}
			// It jumps out of a pen it's shut in, but only into one for an enemy or its job
			Vector3 landing = feet + direction * (HopReach + HopLanding);
			landing.y = ZoneSystem.instance.GetSolidHeight(landing);
			if (!fighting && !GoesIntoPens() && !Village.FencedIn(feet) && Village.FencedIn(landing))
			{
				return(false);
			}
			if (ClimbOutOfPen(feet, landing, obstacle))
			{
				return(true);
			}
			// How high the fence's top stands, found from above just inside its face
			Vector3 probe = obstacle.point + direction * 0.1f;
			probe.y = feet.y + HopHeight + 0.5f;
			float top = HopHeight;
			if (Physics.Raycast(probe, Vector3.down, out RaycastHit topHit, HopHeight + 0.5f, hopMask, QueryTriggerInteraction.Ignore))
			{
				top = Mathf.Clamp(topHit.point.y - feet.y, 0.4f, HopHeight);
			}
			hopDirection = direction;
			hopPoint = obstacle.point;
			hopTop = top;
			hopLeftGround = false;
			hopPiece = Utils.GetPrefabName(obstacle.collider.GetComponentInParent<WearNTear>().gameObject);
			hopFenceTop = feet.y + top;
			// It only backs up for a run-up with no fire's flames within reach behind it, since it backs up without looking -
			// beside a fire it jumps from where it stands
			bool backUp = (Ahead() < RunUpDistance && !FireSafety.FlamesNear(feet, RunUpDistance + RunUpFireMargin, out Vector3 _, out float _));
			hopPhase = (backUp ? HopPhase.BackUp : HopPhase.RunUp);
			hopPhaseUntil = Time.time + (backUp ? BackUpTime : RunUpTime);
			if (Player.m_debugMode)
			{
				Plugin.Log.LogInfo("Hop: " + TestLog.Label(humanoid) + " at " + feet.ToString("F1") + " (" + ((job != null) ? job.Describe() : "no job") + ") takes a run-up to jump " + hopPiece + ", " + top.ToString("F2") + "m high at " + obstacle.point.ToString("F1") + ", toward " + toward.ToString("F1") + ", finding no path there" + (VikingDoors.IsCrossing(humanoid) ? ", while crossing a doorway" : ""));
			}
			return(true);
		}

		/// <summary>
		/// Carries on the viking's jump over a fence (see TryHop) - backing up to RunUpDistance from it, running at it,
		/// jumping when it's as far from it as it needs to be to clear it (see JumpDistance), and keeping on over it
		/// until it lands - giving up on a step that takes too long
		/// </summary>
		/// <returns>True while it's jumping, so the AI's own update is skipped</returns>
		private bool UpdateHop()
		{
			if (hopPhase == HopPhase.None)
			{
				return(false);
			}
			if (Time.time >= hopPhaseUntil && hopPhase != HopPhase.BackUp)
			{
				LogHopEnd((hopPhase == HopPhase.Air) ? ", still off the ground after " + HopAirTime.ToString("F1") + "s" : null);
				hopPhase = HopPhase.None;
				return(false);
			}
			if (hopPhase == HopPhase.BackUp)
			{
				mover = "backing up to jump a fence";
				if (Ahead() >= RunUpDistance || Time.time >= hopPhaseUntil)
				{
					hopPhase = HopPhase.RunUp;
					hopPhaseUntil = Time.time + RunUpTime;
				}
				else
				{
					ai.MoveTowards(-hopDirection, false);
				}
				return(true);
			}
			if (hopPhase == HopPhase.RunUp)
			{
				mover = "running at a fence";
				ai.MoveTowards(hopDirection, true);
				if (humanoid.IsOnGround() && Ahead() <= JumpDistance())
				{
					// A creature's jump carries it the way it faces, so it's turned to the fence first
					transform.rotation = Quaternion.LookRotation(hopDirection);
					humanoid.m_body.rotation = transform.rotation;
					hopTakeoff = transform.position;
					hopTakeoffAhead = Ahead();
					hopPeak = hopTakeoff.y;
					humanoid.Jump(true);
					hopPhase = HopPhase.Air;
					hopPhaseUntil = Time.time + HopAirTime;
				}
				return(true);
			}
			mover = "jumping a fence";
			ai.MoveTowards(hopDirection, true);
			hopPeak = Mathf.Max(hopPeak, transform.position.y);
			if (!humanoid.IsOnGround())
			{
				hopLeftGround = true;
			}
			else if (hopLeftGround)
			{
				LogHopEnd(null);
				hopPhase = HopPhase.None;
			}
			return(true);
		}

		/// <summary>
		/// Writes to the log, in test or debug mode, how the viking's jump over a fence ended - where it landed, whether
		/// that's past the fence, on top of it or back where it jumped from (see HopResult), how high its feet got
		/// against the fence's top and how far in front of the fence it left the ground - or that its run-up ran out
		/// before it jumped, counting each outcome
		/// </summary>
		/// <param name="note">What to add about how the jump ended, like still being in the air, or null</param>
		private void LogHopEnd(string note)
		{
			if (hopPhase == HopPhase.RunUp)
			{
				TestLog.Count("Fence jumps", "never jumped");
				if (TestLog.On || Player.m_debugMode)
				{
					Plugin.Log.LogInfo("Hop: " + TestLog.Name(humanoid, "F1") + " ran at the " + hopPiece + " at " + hopPoint.ToString("F1") + " for " + RunUpTime.ToString("F0") + "s without jumping, " + Ahead().ToString("F2") + "m in front of it");
				}
				return;
			}
			if (hopPhase != HopPhase.Air)
			{
				return;
			}
			HopOutcome outcome = HopResult(hopPoint, hopDirection, transform.position, hopFenceTop);
			string how = HopOutcomeName(outcome);
			TestLog.Count("Fence jumps", how);
			if (TestLog.On || Player.m_debugMode)
			{
				float clearance = hopPeak - hopFenceTop;
				Plugin.Log.LogInfo("Hop: " + TestLog.Name(humanoid, "F1") + " jumped the " + hopPiece + " at " + hopPoint.ToString("F1") + " and " + how + " - its feet rose " + (hopPeak - hopTakeoff.y).ToString("F2") + "m from " + hopTakeoff.ToString("F1") + ", " + Mathf.Abs(clearance).ToString("F2") + "m " + ((clearance >= 0f) ? "over" : "short of") + " the fence's top at " + hopFenceTop.ToString("F2") + ", leaving the ground " + hopTakeoffAhead.ToString("F2") + "m in front of it" + note);
			}
		}

		/// <summary>
		/// Works out where a jump over a fence left the viking - past the fence once it's at least HopOverPast beyond the
		/// fence's face the way it jumped, on top of it short of that with its feet no more than HopOnTopDrop below the
		/// fence's top, and otherwise back on the side it jumped from
		/// </summary>
		/// <param name="face">Where it found the fence's face, before jumping</param>
		/// <param name="direction">The way it jumped, level</param>
		/// <param name="landed">Where its feet are once it's down</param>
		/// <param name="top">How high the fence's top stands</param>
		/// <returns>Where it ended up</returns>
		internal static HopOutcome HopResult(Vector3 face, Vector3 direction, Vector3 landed, float top)
		{
			Vector3 offset = landed - face;
			offset.y = 0f;
			float past = Vector3.Dot(offset, direction);
			if (past >= HopOverPast)
			{
				return(HopOutcome.Over);
			}
			if (landed.y >= top - HopOnTopDrop)
			{
				return(HopOutcome.OnTop);
			}
			return(HopOutcome.FellBack);
		}

		/// <summary>
		/// Names how a jump over a fence ended, for the log
		/// </summary>
		/// <param name="outcome">How it ended</param>
		/// <returns>The words for it</returns>
		internal static string HopOutcomeName(HopOutcome outcome)
		{
			if (outcome == HopOutcome.Over)
			{
				return("got over");
			}
			else if (outcome == HopOutcome.OnTop)
			{
				return("ended up on top of it");
			}
			return("fell back");
		}

		/// <summary>
		/// Checks whether the viking is backing up, running at or jumping a fence (see UpdateHop), when it runs whatever
		/// else it's doing
		/// </summary>
		/// <returns>True when it is</returns>
		public bool Hopping()
		{
			return(hopPhase != HopPhase.None);
		}

		/// <summary>
		/// Works out how far in front of the viking the face of the fence it's jumping is, from the front of its body
		/// </summary>
		/// <returns>The distance, in meters, less than 0 once it's past</returns>
		private float Ahead()
		{
			Vector3 offset = hopPoint - transform.position;
			offset.y = 0f;
			return(Vector3.Dot(offset, hopDirection) - humanoid.GetRadius());
		}

		/// <summary>
		/// Works out how far from the fence the viking jumps to clear it at the speed it's going - its own jump made
		/// stronger by its Jump skill (see VikingCore.JumpSkillBonus), against the game's gravity - far enough out to
		/// be above the fence's top by the time it gets there, and as far again as it can spare to carry its body past
		/// </summary>
		/// <returns>The distance, in meters</returns>
		private float JumpDistance()
		{
			float boost = ((viking != null) ? VikingCore.SkillBoost(viking.SkillLevel(Skills.SkillType.Jump), VikingCore.JumpSkillBonus) : 1f);
			float up = humanoid.m_jumpForce * boost;
			float gravity = Mathf.Max(1f, -Physics.gravity.y);
			float height = hopTop + HopClearance;
			Vector3 velocity = humanoid.m_body.linearVelocity;
			velocity.y = 0f;
			float speed = Mathf.Max(0f, Vector3.Dot(velocity, hopDirection)) + humanoid.m_jumpForceForward * boost;
			float past = humanoid.GetRadius() * 2f + HopDepth;
			return(JumpFrom(up, gravity, height, speed, past));
		}

		/// <summary>
		/// Works out how far from a fence a jump clears it - far enough out to be above the fence's top by the time it
		/// gets there, and half of how much farther it carries above that height than it has to go to get past
		/// </summary>
		/// <param name="up">How fast the jump goes up, in meters a second</param>
		/// <param name="gravity">How fast gravity pulls down, in meters a second each second</param>
		/// <param name="height">How high it has to get</param>
		/// <param name="speed">How fast it goes toward the fence, in meters a second</param>
		/// <param name="past">How far it has to go above that height to get past the fence, in meters</param>
		/// <returns>The distance, in meters</returns>
		internal static float JumpFrom(float up, float gravity, float height, float speed, float past)
		{
			float spare = up * up - 2f * gravity * height;
			if (spare <= 0f)
			{
				// It can't get that high, so it jumps from right in front, which is as near as it comes
				return(0.1f);
			}
			float root = Mathf.Sqrt(spare);
			float rising = (up - root) / gravity;
			float over = 2f * root / gravity;
			return(speed * rising + Mathf.Max(0f, speed * over - past) / 2f);
		}
	}
}
