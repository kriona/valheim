using System.Collections.Generic;
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
		/// Finds where the viking walks to for something in one of its village's pens it has no whole path to, when its
		/// job takes it into pens - a spot outside the pen's fence it can jump in from (see FindPenWayIn) rather than the nearest it can
		/// get to, which on a slope can stand at the foot of a bank below the fence, out of reach of its top. The answer
		/// is kept for PenWayRemember seconds, and looked for again after each spot it walks to
		/// </summary>
		/// <param name="target">Where the viking is walking</param>
		/// <param name="spot">Set to the spot outside the fence, when there's one</param>
		/// <returns>True when it walks there first</returns>
		private bool PenWayIn(Vector3 target, out Vector3 spot)
		{
			spot = target;
			if (Pathfinding.instance == null || village == null || !GoesIntoPens())
			{
				return(false);
			}
			bool newTarget = (Vector3.Distance(target, penWayTarget) > 0.5f);
			if (newTarget)
			{
				penWayTarget = target;
				penWayTried.Clear();
				penWayFound = false;
				penWayUntil = 0f;
			}
			if (Time.time >= penWayUntil)
			{
				penWayUntil = Time.time + PenWayRemember;
				bool wasFound = penWayFound;
				Vector3 wasSpot = penWaySpot;
				penWayFound = false;
				if (!village.Contains(target) || !Village.FencedIn(target) || Pathfinding.instance.HavePath(transform.position, target, Pathfinding.AgentType.Humanoid))
				{
					return(false);
				}
				penWayFound = FindPenWayIn(target, out penWaySpot, out string why);
				if (newTarget || penWayFound != wasFound || (penWayFound && Utils.DistanceXZ(penWaySpot, wasSpot) > 0.5f))
				{
					TestLog.Count("Ways into pens", (penWayFound ? "found" : "none found"));
					if (TestLog.On || Player.m_debugMode)
					{
						Plugin.Log.LogInfo("Pen: " + TestLog.Name(humanoid, "F1") + " (" + ((job != null) ? job.Describe() : "no job") + ") has no whole path to " + target.ToString("F1") + " in a pen, so it " + why);
					}
				}
			}
			spot = penWaySpot;
			return(penWayFound);
		}

		/// <summary>
		/// Looks for a spot outside a pen's fence the viking can jump into the pen from, toward something in it - casting
		/// PenWayRays level rays out from it to the fence, and keeping each spot on the navmesh just past a section it
		/// jumps (see Hoppable) where its feet would be below the section's bottom at its knees and within reach of its top
		/// (see HopFrom), it hasn't tried yet, and it has a whole path to - the nearest of them
		/// </summary>
		/// <param name="target">What it's after, in the pen</param>
		/// <param name="spot">Set to the spot, when there's one</param>
		/// <param name="why">What the viking does, for the log</param>
		/// <returns>True when it found one</returns>
		private bool FindPenWayIn(Vector3 target, out Vector3 spot, out string why)
		{
			spot = target;
			EnsureHopMask();
			Vector3 origin = target + Vector3.up * PenWayHeight;
			List<KeyValuePair<float, Vector3>> spots = new List<KeyValuePair<float, Vector3>>();
			Dictionary<Vector3, string> fences = new Dictionary<Vector3, string>();
			int sections = 0;
			int high = 0;
			for (int i = 0; i < PenWayRays; i++)
			{
				Vector3 direction = Quaternion.Euler(0f, i * 360f / PenWayRays, 0f) * Vector3.forward;
				if (!Physics.Raycast(origin, direction, out RaycastHit fence, PenWayReach, hopMask, QueryTriggerInteraction.Ignore) || !Hoppable(fence.collider))
				{
					continue;
				}
				sections++;
				if (!NavmeshPoints.FindValidPoint(out Vector3 outside, fence.point + direction * PenWayOut, HopLanding, Pathfinding.AgentType.Humanoid))
				{
					continue;
				}
				Vector3 offset = outside - fence.point;
				offset.y = 0f;
				if (Vector3.Dot(offset, direction) <= 0f || offset.magnitude > HopReach - 0.1f || penWayTried.Exists((Vector3 tried) => Utils.DistanceXZ(tried, outside) < 0.5f))
				{
					continue;
				}
				// The fence's top, found from above just inside its face, and its bottom, which has to be at the viking's
				// knees from the spot for it to jump it (see TryHop)
				Vector3 probe = fence.point + direction * 0.1f;
				probe.y = Mathf.Max(outside.y, fence.point.y) + HopHeight + 2f;
				bool atKnees = (Physics.Raycast(outside + Vector3.up * 0.4f, -direction, out RaycastHit knee, HopReach, hopMask, QueryTriggerInteraction.Ignore) && Hoppable(knee.collider));
				if (!atKnees || !Physics.Raycast(probe, Vector3.down, out RaycastHit top, probe.y - outside.y + 1f, hopMask, QueryTriggerInteraction.Ignore) || top.collider.GetComponentInParent<WearNTear>() == null || !HopFrom(outside.y, top.point.y))
				{
					high++;
					continue;
				}
				spots.Add(new KeyValuePair<float, Vector3>(Utils.DistanceXZ(outside, transform.position), outside));
				fences[outside] = Utils.GetPrefabName(fence.collider.GetComponentInParent<WearNTear>().gameObject) + " at " + fence.point.ToString("F1") + ", its top " + (top.point.y - outside.y).ToString("F2") + "m above the ground there";
			}
			spots.Sort((KeyValuePair<float, Vector3> a, KeyValuePair<float, Vector3> b) => a.Key.CompareTo(b.Key));
			int noPath = 0;
			foreach (KeyValuePair<float, Vector3> entry in spots)
			{
				if (!Pathfinding.instance.HavePath(transform.position, entry.Value, Pathfinding.AgentType.Humanoid))
				{
					noPath++;
					continue;
				}
				spot = entry.Value;
				why = "walks to " + spot.ToString("F1") + " to jump into it over the " + fences[spot] + ", " + entry.Key.ToString("F1") + "m from the viking";
				return(true);
			}
			why = "finds no side of its fence to jump in from - " + sections + " fence sections around it, " + high + " too high from outside or not at the knees there, " + noPath + " with no whole path to the spot, " + penWayTried.Count + " spots tried already";
			return(false);
		}

		/// <summary>
		/// Walks the viking to a spot outside a pen's fence it can jump in from (see PenWayIn), then jumps toward what
		/// it's after in the pen from there, writing to the log why when it doesn't (see LogHopRefused) - noting the spot
		/// as tried either way, so one it can't get to or jump from is left for the next, until there's none left and it
		/// walks at what it's after as usual
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="target">What it's after, in the pen</param>
		/// <param name="wayIn">The spot outside the fence</param>
		/// <param name="run">True to run</param>
		/// <returns>False, since it's never there until it's in the pen</returns>
		private bool GoIntoPen(float dt, Vector3 target, Vector3 wayIn, bool run)
		{
			mover = "path to a way into a pen for " + ((job != null) ? job.GetType().Name : "village life");
			walkedThisUpdate = true;
			if (Utils.DistanceXZ(transform.position, wayIn) > PenWayArrive)
			{
				ai.MoveTo(dt, wayIn, 0.1f, run);
				if (CheckStuck(wayIn))
				{
					moveFailed = false;
					penWayTried.Add(wayIn);
					penWayUntil = 0f;
				}
				return(false);
			}
			ai.StopMoving();
			ResetStuck();
			penWayTried.Add(wayIn);
			penWayUntil = 0f;
			if (!TryHop(target, false))
			{
				LogHopRefused(target);
			}
			return(false);
		}

		/// <summary>
		/// Checks whether a fence's top stands low enough above the ground a viking stands on for it to jump it from
		/// there, with PenWayMargin to spare
		/// </summary>
		/// <param name="ground">How high the ground is where it stands</param>
		/// <param name="top">How high the fence's top stands</param>
		/// <returns>True when it can</returns>
		internal static bool HopFrom(float ground, float top)
		{
			return(top - ground <= HopHeight - PenWayMargin);
		}

		/// <summary>
		/// Checks whether the ground behind a viking about to jump a fence, or where it has backed up to, lies level
		/// enough for it to back up for a run-up - no more than RunUpDrop below where its feet were
		/// </summary>
		/// <param name="feet">How high its feet were</param>
		/// <param name="behind">How high the ground behind it is, or where its feet are now</param>
		/// <returns>True when it is</returns>
		internal static bool RunUpLevel(float feet, float behind)
		{
			return(behind >= feet - RunUpDrop);
		}

		/// <summary>
		/// Why a try at a jump wasn't made (see TryHop), for the log and its count - there's a whole path where it's
		/// going, it's there already, it's off the ground, there's nothing in front of its knees it jumps, what's in front
		/// of it stands too high, or the jump would take it into a pen its job doesn't take it into
		/// </summary>
		private const string HopRefusedPath = "a whole path there";
		private const string HopRefusedThere = "already where it's going";
		private const string HopRefusedAir = "off the ground";
		private const string HopRefusedKnees = "nothing it jumps at its knees";
		private const string HopRefusedHigh = "the fence's top too far above its feet";
		private const string HopRefusedPen = "a pen its job doesn't take it into";

		/// <summary>
		/// Writes to the log, in test or debug mode, why the viking's last try at a jump wasn't made (see TryHop) - with
		/// what was in front of its knees, or how high the fence's top stood above its feet - counting each reason
		/// </summary>
		/// <param name="toward">Where it was going</param>
		private void LogHopRefused(Vector3 toward)
		{
			if (hopRefused == null)
			{
				return;
			}
			TestLog.Count("Fence jumps refused", hopRefused);
			if (!TestLog.On && !Player.m_debugMode)
			{
				return;
			}
			string detail = "";
			if (hopRefused == HopRefusedKnees)
			{
				detail = ((hopRefusedThing != null) ? " - the " + ColliderName(hopRefusedThing) + " at " + hopRefusedPoint.ToString("F1") + " isn't one it jumps" : " - nothing within " + HopReach.ToString("F1") + "m");
			}
			else if (hopRefused == HopRefusedHigh && hopRefusedThing != null)
			{
				// The fence's top, found from well above just inside its face
				EnsureHopMask();
				Vector3 probe = hopRefusedPoint + hopRefusedDirection * 0.1f;
				probe.y = hopRefusedFeet.y + HopHeight + 3f;
				bool found = Physics.Raycast(probe, Vector3.down, out RaycastHit top, HopHeight + 4f, hopMask, QueryTriggerInteraction.Ignore);
				detail = " - the " + ColliderName(hopRefusedThing) + " at " + hopRefusedPoint.ToString("F1") + (found ? ", its top " + (top.point.y - hopRefusedFeet.y).ToString("F2") + "m above its feet" : ", something more than " + HopHeight.ToString("F1") + "m above its feet in its way");
			}
			Plugin.Log.LogInfo("Hop: " + TestLog.Name(humanoid, "F1") + " at " + hopRefusedFeet.ToString("F1") + " (" + ((job != null) ? job.Describe() : "no job") + ") doesn't jump toward " + toward.ToString("F1") + ", held up for " + (Time.time - stuckSince).ToString("F0") + "s - " + hopRefused + detail);
		}

		/// <summary>
		/// Names what a collider belongs to, for the log - its building piece's prefab, or its own
		/// </summary>
		/// <param name="collider">The collider</param>
		/// <returns>The name</returns>
		private static string ColliderName(Collider collider)
		{
			WearNTear piece = collider.GetComponentInParent<WearNTear>();
			return(Utils.GetPrefabName((piece != null) ? piece.gameObject : collider.gameObject));
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
			hopRefusedFeet = transform.position;
			hopRefusedThing = null;
			if (!fighting && Pathfinding.instance != null && Pathfinding.instance.HavePath(transform.position, toward, Pathfinding.AgentType.Humanoid))
			{
				hopRefused = HopRefusedPath;
				return(false);
			}
			Vector3 direction = toward - transform.position;
			direction.y = 0f;
			if (direction.sqrMagnitude < 0.01f)
			{
				hopRefused = HopRefusedThere;
				return(false);
			}
			if (!humanoid.IsOnGround())
			{
				hopRefused = HopRefusedAir;
				return(false);
			}
			direction.Normalize();
			hopRefusedDirection = direction;
			EnsureHopMask();
			Vector3 feet = transform.position;
			// Only a building piece like a fence is jumped - a rock is walked around, never climbed onto
			bool kneeHit = Physics.Raycast(feet + Vector3.up * 0.4f, direction, out RaycastHit obstacle, HopReach, hopMask, QueryTriggerInteraction.Ignore);
			if (kneeHit)
			{
				hopRefusedThing = obstacle.collider;
				hopRefusedPoint = obstacle.point;
			}
			if (!kneeHit || !Hoppable(obstacle.collider))
			{
				hopRefused = HopRefusedKnees;
				return(false);
			}
			if (Physics.Raycast(feet + Vector3.up * HopHeight, direction, HopReach + 0.5f, hopMask, QueryTriggerInteraction.Ignore))
			{
				hopRefused = HopRefusedHigh;
				return(false);
			}
			// It jumps out of a pen it's shut in, but only into one for an enemy or its job
			Vector3 landing = feet + direction * (HopReach + HopLanding);
			landing.y = ZoneSystem.instance.GetSolidHeight(landing);
			if (!fighting && !GoesIntoPens() && !Village.FencedIn(feet) && Village.FencedIn(landing))
			{
				hopRefused = HopRefusedPen;
				return(false);
			}
			hopRefused = null;
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
			hopFrom = feet.y;
			// It only backs up for a run-up with no fire's flames within reach behind it, since it backs up without looking,
			// and on ground that doesn't fall away behind it, which it would slide down - beside a fire or above a slope it
			// jumps from where it stands
			float behind = ZoneSystem.instance.GetSolidHeight(feet - direction * RunUpDistance);
			bool backUp = (Ahead() < RunUpDistance && RunUpLevel(feet.y, behind) && !FireSafety.FlamesNear(feet, RunUpDistance + RunUpFireMargin, out Vector3 _, out float _));
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
				// It stops backing up once it's far enough back, or has slid down from where it started
				if (Ahead() >= RunUpDistance || Time.time >= hopPhaseUntil || !RunUpLevel(hopFrom, transform.position.y))
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
		/// that's past the fence, around it, on top of it or back where it jumped from (see HopResult), how high its feet got
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
			HopOutcome outcome = HopResult(hopPoint, hopDirection, transform.position, hopFenceTop, hopPeak);
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
		/// fence's face the way it jumped with its feet having risen to no more than HopOnTopDrop below the fence's top,
		/// or around it when they never got that high, on top of it short of the face's HopOverPast with its feet no
		/// more than HopOnTopDrop below the top, and otherwise back on the side it jumped from
		/// </summary>
		/// <param name="face">Where it found the fence's face, before jumping</param>
		/// <param name="direction">The way it jumped, level</param>
		/// <param name="landed">Where its feet are once it's down</param>
		/// <param name="top">How high the fence's top stands</param>
		/// <param name="peak">The highest its feet got on the way</param>
		/// <returns>Where it ended up</returns>
		internal static HopOutcome HopResult(Vector3 face, Vector3 direction, Vector3 landed, float top, float peak)
		{
			Vector3 offset = landed - face;
			offset.y = 0f;
			float past = Vector3.Dot(offset, direction);
			if (past >= HopOverPast)
			{
				return((peak >= top - HopOnTopDrop) ? HopOutcome.Over : HopOutcome.Around);
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
			else if (outcome == HopOutcome.Around)
			{
				return("went around it");
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
