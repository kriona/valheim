using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Cooking at the village's cooking stations (see CookJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Cooking at the village's cooking stations - at the one it set out for and the others at the same fire no other
		/// viking is at, so it cooks all it can at once - putting the raw food it carries on, fetching more from the
		/// village's chests while they have room, waiting by them while it cooks, and taking off what's done, coal
		/// included, to put away - or only taking off what's done, for a viking pulled off other work
		/// </summary>
		private class CookJob : Job
		{
			private readonly List<CookingStation> stations = new List<CookingStation>();
			private Container chest;
			private CookingStation fetchFor;
			private bool chestTried;
			private float actAt = -1f;
			private float nextRenew;
			private float nextHeldTake;

			/// <summary>
			/// What it's doing at its stations, for the debug hover text
			/// </summary>
			private string doing = "starting";

			/// <summary>
			/// Whether it only takes off done food before it burns, and leaves cooking more to others - for a viking pulled
			/// off other work, or keeping guard through a raid
			/// </summary>
			private readonly bool takeOffOnly;

			/// <summary>
			/// Makes the job, with the station already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="cooker">The cooking station</param>
			/// <param name="takeOff">True to only take off done food (see takeOffOnly)</param>
			public CookJob(VillageLife owner, CookingStation cooker, bool takeOff = false) : base(owner)
			{
				stations.Add(cooker);
				nextRenew = Time.time + ClaimRenew;
				takeOffOnly = takeOff;
			}

			/// <summary>
			/// Claims the other stations at the same fire as the one it set out for (see CookTogether) that no other viking
			/// is at, to cook at them too
			/// </summary>
			public override void Begin()
			{
				CookingStation first = stations[0];
				if (first == null)
				{
					return;
				}
				foreach (CookingStation other in VillageCooking.Stations(life.village, first.transform.position))
				{
					ZNetView view = ((other != null) ? other.GetComponent<ZNetView>() : null);
					if (other == first || view == null || Utils.DistanceXZ(other.transform.position, first.transform.position) > CookTogether)
					{
						continue;
					}
					if (life.IsFree(view) && life.ClaimObject(view))
					{
						stations.Add(other);
					}
				}
			}

			/// <summary>
			/// Fetches raw food from a chest when it carries none, as much as its stations have room for, puts it on them a
			/// piece at a time, waits while it cooks and takes off what's done - done food first, before it burns
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once nothing is left to cook or take off, or it can't get to a station</returns>
			public override bool Tick(float dt)
			{
				stations.RemoveAll((CookingStation cooker) => cooker == null || cooker.m_nview == null || !cooker.m_nview.IsValid());
				if (stations.Count == 0)
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					foreach (CookingStation cooker in stations)
					{
						life.ClaimObject(cooker.m_nview);
					}
				}
				if (chest != null)
				{
					return(Fetch(dt));
				}
				CookingStation done = stations.Find((CookingStation cooker) => cooker.HaveDoneItem());
				if (takeOffOnly && done == null)
				{
					return(true);
				}
				int room = 0;
				CookingStation roomy = null;
				foreach (CookingStation cooker in stations)
				{
					int free = (VillageCooking.IsLit(cooker) ? VillageCooking.FreeSlots(cooker) : 0);
					room += free;
					if (free > 0 && roomy == null)
					{
						roomy = cooker;
					}
				}
				ItemDrop.ItemData raw = ((roomy != null) ? life.CarriedRaw(roomy) : null);
				// From dusk on it only finishes what's on the stations, rather than fetching more to cook into the night
				if (done == null && raw == null && roomy != null && !chestTried && !IsDusk() && !IsNight())
				{
					chestTried = true;
					List<Container> chests = life.UsableChests();
					chest = life.Nearest(chests.FindAll((Container candidate) => life.RawIn(candidate, roomy, chests) != null));
					if (chest != null)
					{
						fetchFor = roomy;
						actAt = -1f;
						return(false);
					}
				}
				// Food over a fire that has gone out - burned down, or put out by the rain - would never cook, so it isn't
				// waited on, and is left for when the fire is lit again (see FindTendStation)
				CookingStation cooking = stations.Find((CookingStation cooker) => cooker.HaveUncookedItem() && VillageCooking.IsLit(cooker));
				if (done == null && raw == null && cooking == null)
				{
					return(true);
				}
				CookingStation target = done ?? ((raw != null) ? roomy : cooking);
				if (!InReach(target))
				{
					FireSafety.CookSpot(target.transform.position, life.transform.position, CookReach, out Vector3 spot, out float within);
					doing = "walking to a station, " + Utils.DistanceXZ(spot, life.transform.position).ToString("F1") + "m off";
					life.Go(dt, spot, within, false);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						return(true);
					}
					return(false);
				}
				life.ai.StopMoving();
				life.Face(target.transform.position);
				if (done == null && raw == null)
				{
					// What's on the stations is still cooking
					doing = "waiting for the food to cook";
					return(false);
				}
				doing = ((done != null) ? "taking off done food" : "putting food on");
				if (actAt < 0f)
				{
					VikingPoses.Interact(life.humanoid);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (Time.time < actAt)
				{
					return(false);
				}
				actAt = -1f;
				if (done != null)
				{
					life.TakeCooked(done);
					chestTried = false;
				}
				else
				{
					VillageCooking.Add(roomy, raw.m_dropPrefab.name);
					TestLog.Count("Put on to cook", TestLog.ItemName(raw));
					life.UseCarried(raw, 1);
					life.RaiseSkill(StationSkill(roomy.m_skill), CookAddSkill);
				}
				return(false);
			}

			/// <summary>
			/// Describes the job for the debug hover text - fetching raw food from a chest, or what it's doing at its
			/// stations and how many it has
			/// </summary>
			/// <returns>The description</returns>
			public override string Describe()
			{
				if (chest != null)
				{
					return("Cook, fetching raw food from a chest " + Utils.DistanceXZ(chest.transform.position, life.transform.position).ToString("F1") + "m off");
				}
				return("Cook, " + doing + ", at " + stations.Count + ((stations.Count == 1) ? " station" : " stations"));
			}

			/// <summary>
			/// Checks whether the viking stands where it cooks at a station - near it and outside the flames of the fire
			/// under it (see FireSafety.CookSpot)
			/// </summary>
			/// <param name="cooker">The station</param>
			/// <returns>True when it does</returns>
			private bool InReach(CookingStation cooker)
			{
				FireSafety.CookSpot(cooker.transform.position, life.transform.position, CookReach, out Vector3 spot, out float within);
				return(Utils.DistanceXZ(spot, life.transform.position) <= Mathf.Max(within, ArriveReach));
			}

			/// <summary>
			/// Keeps done food on the viking's stations from burning while a player holds it - with its inventory open, or
			/// naming it - taking it off a station within its reach where it stands
			/// </summary>
			/// <returns>True when done food is on a station out of its reach, so it leaves the player to take it off</returns>
			public bool TendWhileHeld()
			{
				CookingStation done = stations.Find((CookingStation cooker) => cooker != null && cooker.m_nview != null && cooker.m_nview.IsValid() && cooker.HaveDoneItem());
				if (done == null)
				{
					return(false);
				}
				if (!InReach(done))
				{
					return(true);
				}
				if (Time.time >= nextHeldTake)
				{
					nextHeldTake = Time.time + ReachTime;
					VikingPoses.Interact(life.humanoid);
					life.TakeCooked(done);
					chestTried = false;
				}
				return(false);
			}

			/// <summary>
			/// Walks to the chest, opens it and takes as much raw food as its stations have room for - leaving a chest it
			/// can't get to alone
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>False, so the job goes on</returns>
			private bool Fetch(float dt)
			{
				if (!life.Usable(chest))
				{
					chest = null;
					return(false);
				}
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						chest = null;
					}
					return(false);
				}
				if (actAt < 0f)
				{
					life.Face(chest.transform.position);
					VikingPoses.Interact(life.humanoid);
					life.ShowChestOpen(chest);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (Time.time < actAt)
				{
					return(false);
				}
				actAt = -1f;
				int room = 0;
				foreach (CookingStation cooker in stations)
				{
					room += ((cooker != null && VillageCooking.IsLit(cooker)) ? VillageCooking.FreeSlots(cooker) : 0);
				}
				if (fetchFor != null)
				{
					life.TakeRaw(chest, fetchFor, room);
				}
				chest = null;
				return(false);
			}

			/// <summary>
			/// Lets go of its stations
			/// </summary>
			public override void End()
			{
				foreach (CookingStation cooker in stations)
				{
					if (cooker != null)
					{
						life.ReleaseObject(cooker.m_nview);
					}
				}
			}
		}
	}
}
