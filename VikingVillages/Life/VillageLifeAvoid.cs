using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What a viking stays away from for a while after it couldn't get to it or work at it, what it can reach on its
	/// navmesh, and the pens it keeps out of
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Leaves a chest, loot or piece the viking couldn't get to alone for a while, and notes the failed try at
		/// anything but a chest or cooking station for the viking itself and for the village, whose vikings all leave it
		/// alone for longer once enough of them have failed (see VillageGiveUps) - unless the viking stands off its
		/// navmesh (see OffItsNavmesh)
		/// </summary>
		/// <param name="view">The chest, loot or piece</param>
		/// <param name="seconds">How long it's left alone</param>
		private void Avoid(ZNetView view, float seconds = AvoidTime)
		{
			Avoid(avoided, view, seconds);
		}

		/// <summary>
		/// Leaves a piece the viking couldn't get to to repair or clear snow off to the others for RepairAvoidTime - for
		/// that work alone (see HammerAvoided), so a chest it gave up on is still one it uses - noting the failed try for
		/// the village the way Avoid does
		/// </summary>
		/// <param name="view">The piece</param>
		private void AvoidHammerWork(ZNetView view)
		{
			Avoid(hammerAvoided, view, RepairAvoidTime);
		}

		/// <summary>
		/// Notes in a list of the things the viking leaves alone that it couldn't get to a chest, loot or piece (see
		/// Avoid)
		/// </summary>
		/// <param name="list">The list, by ZDO, with until when each is left alone</param>
		/// <param name="view">The chest, loot or piece</param>
		/// <param name="seconds">How long it's left alone</param>
		private void Avoid(Dictionary<ZDOID, float> list, ZNetView view, float seconds)
		{
			if (view == null || !view.IsValid())
			{
				return;
			}
			List<ZDOID> expired = new List<ZDOID>();
			foreach (KeyValuePair<ZDOID, float> entry in list)
			{
				if (Time.time >= entry.Value)
				{
					expired.Add(entry.Key);
				}
			}
			foreach (ZDOID id in expired)
			{
				list.Remove(id);
			}
			ZDOID thing = view.GetZDO().m_uid;
			list[thing] = Time.time + seconds;
			string name = Utils.GetPrefabName(view.gameObject);
			bool chest = (view.GetComponent<Container>() != null);
			// With no job, it's looking for one - like FindLootWork leaving loot it can't reach for the next nearest
			TestLog.Count("Couldn't reach, by job", ((job != null) ? LogJobName() : "choosing a job"));
			// A chest is counted with its place, since a village has several and the one out of reach is worth knowing
			TestLog.Count("Couldn't reach, by thing", (chest ? name + " at " + view.transform.position.ToString("F0") : name));
			if (Time.time >= nextGiveUpPrune)
			{
				nextGiveUpPrune = Time.time + GiveUpPruneInterval;
				giveUps.Prune(Time.time);
			}
			// A chest is never given up on by the whole village - without it the vikings would have nowhere to put away
			// what they find or eat from - only by the viking that couldn't get to it, for a while
			if (chest)
			{
				Container store = view.GetComponent<Container>();
				// A chest it got to but found nothing in is still within reach, and a viking off its navmesh fails to get to
				// everything, so neither leaves the chest's food out of the village's (see ChestFoodWithinReach)
				if (!AtChest(store) && !OffItsNavmesh())
				{
					NoteChestMissed(store);
				}
				LogChestMiss(store);
				return;
			}
			// Nor is a cooking station - the village's fire's stations are all it cooks at, so a few vikings that can't get
			// to them don't stop the rest cooking
			if (view.GetComponent<CookingStation>() != null)
			{
				return;
			}
			// A viking stranded where it can't walk from - up on a roof, or anywhere else off its navmesh - fails to get
			// to everything, so its try says nothing about the thing and only it leaves it alone
			if (OffItsNavmesh())
			{
				TestLog.Count("Tries not counted for the village, the viking off the navmesh", name);
				return;
			}
			ownGiveUps.Prune(Time.time);
			float own = ownGiveUps.Fail(thing, nview.GetZDO().m_uid, Time.time);
			if (own > 0f)
			{
				TestLog.Count("Given up on by a viking alone", name);
				if (TestLog.On || Player.m_debugMode)
				{
					Plugin.Log.LogInfo("Given up: " + LogName() + " (" + LogJobName() + ") leaves " + name + " at " + view.transform.position.ToString("F1") + " alone for " + (own / 60f).ToString("F0") + " minutes, after " + VillageGiveUps.Tries + " tries of its own failed to reach it");
				}
			}
			float shared = giveUps.Fail(thing, nview.GetZDO().m_uid, Time.time);
			if (shared <= 0f)
			{
				return;
			}
			TestLog.Count("Given up on by every viking", name);
			if (TestLog.On || Player.m_debugMode)
			{
				Plugin.Log.LogInfo("Given up: the vikings leave " + name + " at " + view.transform.position.ToString("F1") + " alone for " + (shared / 60f).ToString("F0") + " minutes, after " + VillageGiveUps.Tries + " tries by " + VillageGiveUps.Vikings + " or more of them failed to reach it - the last by " + LogName() + " (" + LogJobName() + ")");
			}
		}

		/// <summary>
		/// How near the viking its navmesh has to be for it to count as standing on it, in meters
		/// </summary>
		private const float OwnNavmeshReach = 1f;

		/// <summary>
		/// Checks whether the viking stands off its navmesh - on a roof, or with none within OwnNavmeshReach - where it
		/// can't walk anywhere from
		/// </summary>
		/// <returns>True when it does</returns>
		private bool OffItsNavmesh()
		{
			if (NavmeshRoofs.OnRoof(transform.position))
			{
				return(true);
			}
			return(Pathfinding.instance != null && !NavmeshPoints.FindValidPoint(out Vector3 _, transform.position, OwnNavmeshReach, Pathfinding.AgentType.Humanoid));
		}

		/// <summary>
		/// Checks whether the viking can get within reach of something lying on the ground - the nearest point it can
		/// stand at is near enough, and it has a whole way there, or a way through a door, which can't be told until
		/// it's walked
		/// </summary>
		/// <remarks>
		/// A thing tucked against a rock or a wall can be further from where the viking can stand than it reaches, so
		/// walking at it would only press it against what's in the way
		/// </remarks>
		/// <param name="point">Where it lies</param>
		/// <param name="stand">Where the viking can stand to reach it</param>
		/// <returns>False when it can't get within reach</returns>
		private bool CanReach(Vector3 point, out Vector3 stand)
		{
			return(CanReach(point, out stand, out string _, out bool _));
		}

		/// <summary>
		/// Checks whether the viking can get within reach of something lying on the ground, saying why not when it can't
		/// (see CanReach)
		/// </summary>
		/// <param name="point">Where it lies</param>
		/// <param name="stand">Where the viking can stand to reach it</param>
		/// <param name="why">Why it can't, for the log, or null when it can</param>
		/// <param name="noPath">True when there's a spot near enough to stand at, and only the whole path there is
		/// missing</param>
		/// <returns>False when it can't get within reach</returns>
		private bool CanReach(Vector3 point, out Vector3 stand, out string why, out bool noPath)
		{
			stand = point;
			why = null;
			noPath = false;
			if (Pathfinding.instance == null)
			{
				return(true);
			}
			if (!NavmeshPoints.FindValidPoint(out stand, point, PickReach, Pathfinding.AgentType.Humanoid))
			{
				why = "no navmesh within " + PickReach.ToString("F1") + "m of it";
				return(false);
			}
			if (Utils.DistanceXZ(stand, point) > PickReach)
			{
				why = "the nearest navmesh, at " + stand.ToString("F1") + ", is " + Utils.DistanceXZ(stand, point).ToString("F1") + "m from it";
				return(false);
			}
			if (VikingDoors.WayThroughDoor(humanoid, stand))
			{
				return(true);
			}
			if (!Pathfinding.instance.HavePath(transform.position, stand, Pathfinding.AgentType.Humanoid))
			{
				bool onMesh = NavmeshPoints.FindValidPoint(out Vector3 from, transform.position, 1f, Pathfinding.AgentType.Humanoid);
				why = "no whole path from the viking to " + stand.ToString("F1") + (onMesh ? "" : " - the viking isn't on the navmesh itself");
				noPath = true;
				return(false);
			}
			return(true);
		}

		/// <summary>
		/// Notes in the log, in debug mode, that the viking leaves loot alone for AvoidTime and why
		/// </summary>
		/// <param name="view">The loot</param>
		/// <param name="why">Why</param>
		private void DebugAvoid(ZNetView view, string why)
		{
			if (!Player.m_debugMode || view == null || !view.IsValid())
			{
				return;
			}
			Plugin.Log.LogInfo("Loot: " + TestLog.Name(humanoid, "F1") + " (" + ((job != null) ? job.Describe() : "no job") + ") leaves " + Utils.GetPrefabName(view.gameObject) + " at " + view.transform.position.ToString("F1") + " alone for " + AvoidTime.ToString("F0") + "s - " + why);
		}

		/// <summary>
		/// Checks whether the viking's job takes it into pens - foraging, petting a boar, slaughtering one, putting loot
		/// away, feeding a fire that stands in one, or getting wood lying in one, the only things it goes into one for
		/// </summary>
		/// <returns>True when it does</returns>
		private bool GoesIntoPens()
		{
			return(job is ForageJob || job is PetJob || job is LootJob || job is FuelJob || (job is HuntJob hunt && hunt.Slaughters()) || (job != null && inPenForWood));
		}

		/// <summary>
		/// Checks whether something lies in a pen, which the vikings keep out of - remembering the answer for
		/// PenCheckTime, since what lies on the ground doesn't move
		/// </summary>
		/// <param name="id">Its ZDO</param>
		/// <param name="point">Where it lies</param>
		/// <returns>True when it's in a pen</returns>
		private static bool InPen(ZDOID id, Vector3 point)
		{
			if (penChecks.TryGetValue(id, out KeyValuePair<bool, float> known) && Time.time < known.Value)
			{
				return(known.Key);
			}
			if (penChecks.Count > PenChecksMax)
			{
				penChecks.Clear();
			}
			bool inPen = Village.FencedIn(point);
			penChecks[id] = new KeyValuePair<bool, float>(inPen, Time.time + PenCheckTime);
			return(inPen);
		}

		/// <summary>
		/// Checks whether the viking is leaving a chest or loot alone after it couldn't get to it or gave up on it after
		/// failing again and again, or every viking is after they all gave up on it (see VillageGiveUps)
		/// </summary>
		/// <param name="id">The chest's or loot's ZDO</param>
		/// <returns>True while it is</returns>
		private bool Avoided(ZDOID id)
		{
			return((avoided.TryGetValue(id, out float until) && Time.time < until) || ownGiveUps.GivenUp(id, Time.time) || giveUps.GivenUp(id, Time.time));
		}

		/// <summary>
		/// Checks whether the viking is leaving a piece alone for repairing and clearing snow off, after it couldn't get
		/// to it for that work or it couldn't get to it at all (see Avoided)
		/// </summary>
		/// <param name="id">The piece's ZDO</param>
		/// <returns>True while it is</returns>
		private bool HammerAvoided(ZDOID id)
		{
			return((hammerAvoided.TryGetValue(id, out float until) && Time.time < until) || Avoided(id));
		}
	}
}
