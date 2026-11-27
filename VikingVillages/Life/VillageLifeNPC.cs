using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What an NPC viking does differently in its village - it keeps out of fights, running inside a house until the
	/// danger has passed and only fighting with its knife when it's caught, shelters indoors through a raid, woken from
	/// its bed by it or the night watch's alarm, leaves
	/// fighting work to the PC vikings, and is the first to take on the village's chores, which the PC vikings leave
	/// to an NPC viking with nothing better to do
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// On an NPC viking, until when it's free to take on a chore - standing about, strolling, petting a boar or
		/// talking at home by day - in world ticks, noted for the village's PC vikings on any client (see LeavesToNPC)
		/// </summary>
		private static readonly int FreeKey = "kriona.VikingVillages.vikingFree".GetStableHashCode();

		/// <summary>
		/// How long an NPC viking's note that it's free lasts, and how often it's renewed while it stays free, in seconds
		/// </summary>
		private const float FreeHold = 12f;
		private const float FreeRenew = 5f;

		/// <summary>
		/// How long a PC viking leaves a chore to a free NPC viking before taking it on itself, in case the NPC viking
		/// can't get to it, and how long a chore has to go unseen for that wait to start over, in seconds
		/// </summary>
		internal const float DeferLimit = 15f;
		internal const float DeferGap = 10f;

		/// <summary>
		/// How long an NPC viking woken by danger stays up after the danger has passed, and how far past the village's
		/// edge an enemy keeps it up, in seconds and meters
		/// </summary>
		internal const float WokenLinger = 20f;
		private const float WokenReach = 10f;

		/// <summary>
		/// Until when an NPC viking woken by a raid or the night watch's alarm stays up inside, in Time.time seconds (see
		/// StaysUp)
		/// </summary>
		private float wokenUntil;

		/// <summary>
		/// How long an NPC viking stays inside after the danger it ran from has passed, how long it has to be hit without
		/// getting any farther away to be caught, how much farther it has to get in that time, and how long it fights back
		/// once caught after it was last hit, in seconds and meters
		/// </summary>
		internal const float ShelterLinger = 12f;
		internal const float CornerTime = 3f;
		internal const float CornerGain = 1f;
		internal const float PunchTime = 3f;

		/// <summary>
		/// How near an enemy has to be to an NPC viking that can't get away for it to fight back, in meters
		/// </summary>
		private const float StuckFightRange = 4f;

		/// <summary>
		/// When the viking last noted it's free (see UpdateFree), and whether it was free then
		/// </summary>
		private float nextFreeNote;
		private bool notedFree;

		/// <summary>
		/// For each chore the viking has left to an NPC viking, when it first and last found it waiting (see Defers)
		/// </summary>
		private readonly Dictionary<string, KeyValuePair<float, float>> deferred = new Dictionary<string, KeyValuePair<float, float>>();

		/// <summary>
		/// Whether an NPC viking is sheltering from danger, until when it stays inside once the danger has gone, where it
		/// shelters, and since when, how far from its enemy and whether it was hit while it tried to get away, and until
		/// when it fights back once caught, in Time.time seconds and meters
		/// </summary>
		private bool sheltering;
		private float shelterUntil;
		private Vector3 shelterSpot;
		private bool hasShelterSpot;
		private float escapeSince;
		private float escapeFrom;
		private float corneredUntil;

		/// <summary>
		/// What an NPC viking sheltering from danger does with its AI's update (see UpdateShelter)
		/// </summary>
		private enum ShelterMove
		{
			None,
			Handled,
			Fight
		}

		/// <summary>
		/// Checks whether the viking is an NPC viking, a peaceful villager who keeps out of fights
		/// </summary>
		/// <returns>True when it is</returns>
		public bool Peaceful()
		{
			return(humanoid != null && VikingCore.IsNPCViking(humanoid));
		}

		/// <summary>
		/// Notes on an NPC viking whether it's free to take on a chore - by day, at home in its village and not wounded,
		/// standing about, strolling, petting a boar or talking - renewed every FreeRenew seconds while it is, on its owner
		/// </summary>
		private void UpdateFree()
		{
			if (!Peaceful() || nview == null || !nview.IsValid() || !nview.IsOwner())
			{
				return;
			}
			bool idle = (job is IdleJob || job is StrollJob || job is PetJob || job is TalkJob);
			bool free = (idle && !IsNight() && !IsDusk() && village != null && village.Contains(transform.position) && !Wounded() && !sheltering);
			if (free == notedFree && (!free || Time.time < nextFreeNote))
			{
				return;
			}
			notedFree = free;
			nextFreeNote = Time.time + FreeRenew;
			nview.GetZDO().Set(FreeKey, (free ? NetTime.Now() + NetTime.Ticks(FreeHold) : 0L));
		}

		/// <summary>
		/// Checks whether any of the village's NPC vikings is free to take on a chore, on any client (see UpdateFree)
		/// </summary>
		/// <returns>True when one is</returns>
		private bool FreeNPCHome()
		{
			long now = NetTime.Now();
			return(village.Vikings().Exists((Character other) => other != humanoid && VikingCore.IsNPCViking(other) && other.m_nview != null && other.m_nview.IsValid() && other.m_nview.GetZDO().GetLong(FreeKey) > now));
		}

		/// <summary>
		/// Checks whether a PC viking leaves a chore it found to one of its village's NPC vikings - while one is free to
		/// take it on (see FreeNPCHome), for up to DeferLimit seconds after it first found the chore waiting, so a chore
		/// the NPC vikings can't get to is still done
		/// </summary>
		/// <param name="chore">The chore's name, like "Cook" (see NPCFirst)</param>
		/// <returns>True when it leaves it</returns>
		private bool LeavesToNPC(string chore)
		{
			if (Peaceful() || village == null)
			{
				return(false);
			}
			if (!FreeNPCHome())
			{
				deferred.Remove(chore);
				return(false);
			}
			float first = -1f;
			float last = -1f;
			if (deferred.TryGetValue(chore, out KeyValuePair<float, float> seen))
			{
				first = seen.Key;
				last = seen.Value;
			}
			bool defers = Defers(ref first, ref last, Time.time);
			deferred[chore] = new KeyValuePair<float, float>(first, last);
			return(defers);
		}

		/// <summary>
		/// Works out whether a PC viking still leaves a chore it found waiting to an NPC viking - for DeferLimit seconds
		/// from when it first found it, starting over once the chore has gone unseen for DeferGap
		/// </summary>
		/// <param name="first">When it first found the chore waiting, or below 0 for never, updated here</param>
		/// <param name="last">When it last found it, or below 0 for never, updated here to now</param>
		/// <param name="now">The time now, in seconds</param>
		/// <returns>True while it still leaves it</returns>
		internal static bool Defers(ref float first, ref float last, float now)
		{
			if (first < 0f || last < 0f || now - last > DeferGap)
			{
				first = now;
			}
			last = now;
			return(now - first < DeferLimit);
		}

		/// <summary>
		/// Leaves a chore a PC viking found to the village's NPC vikings while one is free (see LeavesToNPC) - letting go
		/// of what the job claimed. The chores are looting, cooking and cauldron meals, paths, repairs, clearing snow,
		/// rebuilding and stacking the fire and the village, taking down sitting logs, the cart, more chests, fueling and
		/// filling smelters and crafting for the village's goal, stocking tools and building beehives, and among the
		/// rolled jobs, stocking the chests with wood, foraging and slaughtering livestock
		/// </summary>
		/// <param name="found">The job, or null</param>
		/// <param name="chore">The chore's name</param>
		/// <returns>The job, or null when there was none or it's left to an NPC viking</returns>
		private Job NPCFirst(Job found, string chore)
		{
			if (found == null || !LeavesToNPC(chore))
			{
				return(found);
			}
			found.End();
			ReleaseWork();
			return(null);
		}

		/// <summary>
		/// Wakes an NPC viking for danger - a raid on its village, or the night watch's alarm - getting it up from its bed
		/// or seat, so it stays up inside until the danger has passed (see StaysUp)
		/// </summary>
		/// <param name="why">What woke it, for the test log</param>
		private void WakeForDanger(string why)
		{
			Interrupt(why);
			SetDulled(false);
			wokenUntil = Time.time + WokenLinger;
		}

		/// <summary>
		/// Checks whether an NPC viking woken by danger stays up - until WokenLinger seconds after the raid is over and no
		/// enemy is within WokenReach of the village
		/// </summary>
		/// <returns>True while it does</returns>
		private bool StaysUp()
		{
			if (Time.time >= wokenUntil || village == null)
			{
				return(false);
			}
			if (VillageRaids.On(village) || village.FindEnemy(WokenReach) != null)
			{
				wokenUntil = Time.time + WokenLinger;
			}
			return(true);
		}

		/// <summary>
		/// Has an NPC viking woken by danger stand inside its house, clear of the doors, rather than going back to bed
		/// </summary>
		/// <returns>The job</returns>
		private Job StayUpJob()
		{
			return(new IdleJob(this, Random.Range(IdleTimeMin, IdleTimeMax), true));
		}

		/// <summary>
		/// Picks what an NPC viking does through a raid on its village - taking done food off a cooking station, eating
		/// while it's wounded, or else standing inside its house, clear of the doors, by day or night
		/// </summary>
		/// <returns>The job</returns>
		private Job ShelterRaidJob()
		{
			Job tend = FindTendWork(true, true);
			if (tend != null)
			{
				return(tend);
			}
			if (Wounded())
			{
				Container pantry = ((Time.time >= eatAgainAt) ? FindFoodChest(false) : null);
				if (pantry != null)
				{
					return(new EatJob(this, pantry, false));
				}
			}
			return(StayUpJob());
		}

		/// <summary>
		/// Checks whether an enemy the NPC viking's AI is after is a danger to run from - not the village's livestock or an
		/// animal it's slaughtering
		/// </summary>
		/// <param name="enemy">The enemy, or null</param>
		/// <returns>True when it is</returns>
		private bool Threatens(Character enemy)
		{
			if (enemy == null || enemy.IsDead() || FarmSpawns.IsLivestock(enemy))
			{
				return(false);
			}
			return(!(job is HuntJob hunt && hunt.Allows(enemy)));
		}

		/// <summary>
		/// Has an NPC viking keep out of a fight - with an enemy after it or near, or hurt a moment ago, it drops what it's
		/// doing, shouts for help and runs inside its house, or the nearest standing house of its village, and stays there
		/// until ShelterLinger seconds after the danger has passed. One hit for CornerTime seconds without getting
		/// CornerGain meters farther from its enemy, or that can't get away with the enemy close, is caught, and fights
		/// back with its knife, or its fists without one, until PunchTime seconds after it was last hit
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="target">The enemy its AI is after, or null</param>
		/// <returns>What it does with the update</returns>
		private ShelterMove UpdateShelter(float dt, Character target)
		{
			Character enemy = (Threatens(target) ? target : null);
			bool hurt = (ai.m_timeSinceHurt < AttackedTime);
			if (enemy != null || hurt)
			{
				if (!sheltering)
				{
					StartSheltering(enemy);
				}
				shelterUntil = Time.time + ShelterLinger;
			}
			if (!sheltering)
			{
				return(ShelterMove.None);
			}
			if (Time.time >= shelterUntil)
			{
				sheltering = false;
				hasShelterSpot = false;
				return(ShelterMove.None);
			}
			if (job != null)
			{
				Interrupt("sheltering");
			}
			SetDulled(false);
			if (enemy != null && Caught(enemy))
			{
				mover = "fighting back";
				return(ShelterMove.Fight);
			}
			ShoutForHelp(enemy);
			if (!hasShelterSpot)
			{
				return(StandAndWatch(enemy));
			}
			mover = "sheltering";
			if (Go(dt, shelterSpot, 0.75f, true))
			{
				if (moveFailed)
				{
					moveFailed = false;
					hasShelterSpot = false;
				}
				return(StandAndWatch(enemy));
			}
			return(ShelterMove.Handled);
		}

		/// <summary>
		/// Starts an NPC viking sheltering from danger - picking a spot on the floor of its house, or the nearest standing
		/// house of its village, or with none standing the village's rally point, and crying out as it runs
		/// </summary>
		/// <param name="enemy">The enemy it runs from, or null when it was only hurt</param>
		private void StartSheltering(Character enemy)
		{
			sheltering = true;
			corneredUntil = 0f;
			escapeSince = Time.time;
			escapeFrom = ((enemy != null) ? Vector3.Distance(enemy.transform.position, transform.position) : 0f);
			hasShelterSpot = false;
			if (village != null && Home(out VillagePaths.House home))
			{
				shelterSpot = FloorSpot(home);
				hasShelterSpot = true;
			}
			else if (village != null)
			{
				// With no house standing it runs to the village's rally point, where the others gather to fight
				shelterSpot = village.RallyPoint();
				hasShelterSpot = true;
			}
			TestLog.Count("NPC vikings", "ran from danger");
			VikingSpeech.SayFirst(humanoid, NPCSpeech.Flee(), VikingSpeech.CallTime, VikingSpeech.CallCull);
		}

		/// <summary>
		/// Has a sheltering NPC viking stand where it is, watching the enemy it ran from
		/// </summary>
		/// <param name="enemy">The enemy, or null</param>
		/// <returns>That its update was handled</returns>
		private ShelterMove StandAndWatch(Character enemy)
		{
			ai.StopMoving();
			if (enemy != null)
			{
				ai.LookAt(enemy.GetCenterPoint());
			}
			return(ShelterMove.Handled);
		}

		/// <summary>
		/// Checks whether a sheltering NPC viking is caught by an enemy - hit while getting no farther from it, or unable
		/// to get away with it close - so it fights back (see IsCaught), keeping it caught until PunchTime seconds after it
		/// was last hit, and crying out the first time
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>True while it's caught</returns>
		private bool Caught(Character enemy)
		{
			float distance = Vector3.Distance(enemy.transform.position, transform.position);
			bool hit = (ai.m_timeSinceHurt < CornerTime);
			bool stuck = (moveFailed && distance <= StuckFightRange);
			if (Time.time < corneredUntil)
			{
				if (ai.m_timeSinceHurt < PunchTime || stuck)
				{
					corneredUntil = Mathf.Max(corneredUntil, Time.time + PunchTime - ai.m_timeSinceHurt);
				}
				return(true);
			}
			if (IsCaught(hit, stuck, distance - escapeFrom, Time.time - escapeSince))
			{
				moveFailed = false;
				corneredUntil = Time.time + PunchTime;
				TestLog.Count("NPC vikings", "caught and fought back");
				VikingSpeech.SayFirst(humanoid, NPCSpeech.Cornered(), VikingSpeech.CallTime, VikingSpeech.CallCull);
				return(true);
			}
			// Each CornerTime it starts measuring again from where it is
			if (Time.time - escapeSince >= CornerTime)
			{
				escapeSince = Time.time;
				escapeFrom = distance;
			}
			return(false);
		}

		/// <summary>
		/// Works out whether an NPC viking trying to get away from an enemy is caught - it can't get away with the enemy
		/// close, or it has been hit while it tried for CornerTime seconds without getting CornerGain meters farther away
		/// </summary>
		/// <param name="hit">Whether it was hit in the last CornerTime seconds</param>
		/// <param name="stuck">Whether it can't get away with the enemy close</param>
		/// <param name="gained">How much farther from the enemy it has got since it started measuring, in meters</param>
		/// <param name="trying">How long since it started measuring, in seconds</param>
		/// <returns>True when it's caught</returns>
		internal static bool IsCaught(bool hit, bool stuck, float gained, float trying)
		{
			if (stuck)
			{
				return(true);
			}
			return(hit && trying >= CornerTime && gained < CornerGain);
		}
	}
}
