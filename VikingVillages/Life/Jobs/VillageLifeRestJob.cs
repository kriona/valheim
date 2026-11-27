using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Resting at night in a bed, chair or on the floor (see RestJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Resting at night - lying in a free bed in the village, or else sitting on a chair or the floor of its house -
		/// and getting up in the morning, after a moment and maybe with a stretch
		/// </summary>
		private class RestJob : Job
		{
			private Bed bed;
			private Chair chair;
			private VillagePaths.House house;
			private bool hasHouse;
			private Vector3 spot;
			private bool hasSpot;
			private bool settled;
			private Vector3 settledAt;
			private int floorTries;
			private int bedTries;
			private float retryAt;
			private float nextRenew;
			private float wakeAt = -1f;
			private float stretchUntil = -1f;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			public RestJob(VillageLife owner) : base(owner)
			{
			}

			/// <summary>
			/// Describes where the viking rests and whether it's settled down there
			/// </summary>
			/// <returns>The description</returns>
			public override string Describe()
			{
				string where = ((bed != null) ? "bed" : ((chair != null) ? "chair" : (hasSpot ? "floor" : "nowhere")));
				return("Rest on " + where + (settled ? ", settled" : ", on the way") + ((wakeAt >= 0f) ? ", waking" : ""));
			}

			/// <summary>
			/// Picks where the viking rests - a free bed, its home's first, then a chair in its home, then a spot on its
			/// home's floor, or by the fire when it has no home standing
			/// </summary>
			public override void Begin()
			{
				hasHouse = life.Home(out house);
				bed = FindBed();
				if (bed != null)
				{
					spot = bed.m_spawnPoint.position;
					hasSpot = true;
					return;
				}
				// With no house of the village standing, it rests out by the fire
				if (!hasHouse)
				{
					spot = life.village.RallyPoint();
					hasSpot = true;
					return;
				}
				chair = FindChair(house);
				if (chair != null)
				{
					spot = chair.m_attachPoint.position;
					hasSpot = true;
					return;
				}
				PickFloorSpot();
			}

			/// <summary>
			/// Picks a spot on its home's floor to rest on (see FloorSpot)
			/// </summary>
			private void PickFloorSpot()
			{
				spot = FloorSpot(house);
				hasSpot = true;
			}

			/// <summary>
			/// Checks whether the viking stands inside its home, within its walls and not on its roof
			/// </summary>
			/// <returns>True when it does</returns>
			private bool Inside()
			{
				Vector3 position = life.transform.position;
				return(hasHouse && Village.InHouse(house, position, -0.5f) && !NavmeshRoofs.OnRoof(position));
			}

			/// <summary>
			/// Checks whether the viking stands beside its bed - within BedEdgeReach of its nearest edge seen from above,
			/// level with it, and with nothing solid like a wall between them
			/// </summary>
			/// <returns>True when it does</returns>
			private bool BesideBed()
			{
				WearNTear piece = bed.GetComponent<WearNTear>();
				if (piece == null)
				{
					return(false);
				}
				Vector3 feet = life.transform.position;
				if (Mathf.Abs(feet.y - bed.transform.position.y) > BedEdgeHeight)
				{
					return(false);
				}
				Vector3 knee = feet + Vector3.up * BedEdgeKnee;
				Vector3 edge = NearestPoint(piece, knee);
				if (Utils.DistanceXZ(edge, feet) > BedEdgeReach)
				{
					return(false);
				}
				Vector3 toEdge = edge - knee;
				float distance = toEdge.magnitude;
				if (distance < 0.01f)
				{
					return(true);
				}
				EnsureHopMask();
				foreach (RaycastHit hit in Physics.RaycastAll(knee, toEdge / distance, distance, hopMask, QueryTriggerInteraction.Ignore))
				{
					if (hit.collider.GetComponentInParent<Bed>() != bed)
					{
						return(false);
					}
				}
				return(true);
			}

			/// <summary>
			/// Walks to where it rests and lies or sits down, keeps its bed claimed through the night, and gets up once
			/// the night is over - sitting on its home's floor only once it's inside, and going back to its spot when
			/// it's pushed off it
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's up in the morning</returns>
			public override bool Tick(float dt)
			{
				if (!IsNight())
				{
					return(WakeUp());
				}
				// Once the players have slept through the night, one still on its way to bed stays up (see Bedtime)
				if (!settled && SleepSkip.SleptThroughNight())
				{
					return(true);
				}
				// A viking picked as the watch after it settled down gets up to keep watch, with a word when it was asleep in
				// bed - woken by the one handing it over, most likely
				if (life.IsWatch())
				{
					if (life.inBed)
					{
						VikingSpeech.Chatter(life.humanoid, VikingSpeech.Woken(), VikingSpeech.TalkTime, true);
					}
					return(true);
				}
				if (!hasSpot || Time.time < retryAt)
				{
					life.ai.StopMoving();
					return(false);
				}
				if (!settled)
				{
					if (bed != null && bed.GetOwner() != 0L)
					{
						// A player claimed the bed
						return(true);
					}
					if (bed != null && Time.time >= nextRenew)
					{
						nextRenew = Time.time + ClaimRenew;
						if (!life.ClaimObject(bed.GetComponent<ZNetView>()))
						{
							// Another viking got there first
							return(true);
						}
					}
					// A bed or chair is lain or sat on from within reach of it, since the piece itself stands in the way, and a
					// bed from beside any edge of it
					float reach = ((bed != null || chair != null) ? 1.5f : 0.6f);
					if (bed != null && BesideBed())
					{
						life.ai.StopMoving();
						life.ResetStuck();
						life.moveFailed = false;
					}
					else if (!life.Go(dt, spot, reach, false))
					{
						return(false);
					}
					bool failed = life.moveFailed;
					life.moveFailed = false;
					// Held up on the way, like in a crowded doorway, it keeps its bed and tries again after a moment
					if (failed && bed != null && bedTries < BedRetries)
					{
						bedTries++;
						retryAt = Time.time + BedRetryWait;
						return(false);
					}
					if (failed && (bed != null || chair != null))
					{
						if (bed != null)
						{
							Plugin.Log.LogInfo(life.LogName() + " couldn't reach the bed at " + bed.transform.position.ToString("F0") + ", so it rests on the floor instead" + life.DescribeBedMiss(bed, spot));
							life.ReleaseObject(bed.GetComponent<ZNetView>());
						}
						if (chair != null)
						{
							life.ReleaseObject(chair.GetComponentInParent<ZNetView>());
						}
						bed = null;
						chair = null;
						if (!hasHouse)
						{
							hasSpot = false;
							return(false);
						}
						if (!Inside())
						{
							PickFloorSpot();
							return(false);
						}
					}
					if (bed == null && chair == null && !Inside())
					{
						// Its way in was blocked, or its spot is just past a wall from where it stands
						floorTries++;
						if (floorTries % RestFloorTries == 0)
						{
							retryAt = Time.time + RestRetryWait;
						}
						PickFloorSpot();
						return(false);
					}
					SettleDown();
					return(false);
				}
				// A player who claims the bed has it at once - the viking gets straight up and finds another
				if (bed != null && bed.GetOwner() != 0L)
				{
					return(true);
				}
				if (bed != null && Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.ClaimObject(bed.GetComponent<ZNetView>());
				}
				if (life.attachPoint == null)
				{
					life.ai.StopMoving();
					if (Utils.DistanceXZ(life.transform.position, settledAt) > PushedDistance)
					{
						StandUp();
						spot = settledAt;
					}
				}
				return(false);
			}

			/// <summary>
			/// Lies down on the bed, sits on the chair, or sits on the floor, putting away its torch
			/// </summary>
			private void SettleDown()
			{
				settled = true;
				settledAt = life.transform.position;
				nextRenew = Time.time + ClaimRenew;
				life.ai.StopMoving();
				if (life.viking != null)
				{
					life.viking.UpdateTorch();
				}
				if (bed != null)
				{
					life.Attach(bed.m_spawnPoint, bed.gameObject);
					VikingPoses.SetLying(life.humanoid, true);
				}
				else if (chair != null)
				{
					life.Attach(chair.m_attachPoint, chair.gameObject);
					VikingPoses.SetChair(life.humanoid, true);
				}
				else
				{
					VikingPoses.SetSitting(life.humanoid, true);
				}
			}

			/// <summary>
			/// Gets up a while after the night ends (see WakeDelayMin), stretching half the time
			/// </summary>
			/// <returns>True once it's up and done stretching</returns>
			private bool WakeUp()
			{
				life.ai.StopMoving();
				if (!settled)
				{
					return(true);
				}
				if (wakeAt < 0f)
				{
					wakeAt = Time.time + WakeDelayMin + SpreadDelay();
				}
				if (Time.time < wakeAt)
				{
					return(false);
				}
				if (stretchUntil < 0f)
				{
					if (bed != null)
					{
						life.body.Rest.Woke(bed.m_spawnPoint.position);
					}
					StandUp();
					if (Random.value >= StretchChance)
					{
						return(true);
					}
					VikingPoses.Stretch(life.humanoid);
					stretchUntil = Time.time + StretchTime;
				}
				return(Time.time >= stretchUntil);
			}

			/// <summary>
			/// Checks whether the viking sits for the night, on a chair or the floor
			/// </summary>
			/// <returns>True while it does</returns>
			public override bool Sitting()
			{
				return(settled && bed == null);
			}

			/// <summary>
			/// Gets off the bed, chair or floor - off a bed onto the floor spot it lay down from, while that's still beside
			/// the bed (see BedLeaveReach)
			/// </summary>
			private void StandUp()
			{
				if (!settled)
				{
					return;
				}
				settled = false;
				if (bed != null)
				{
					VikingPoses.SetLying(life.humanoid, false);
				}
				else if (chair != null)
				{
					VikingPoses.SetChair(life.humanoid, false);
				}
				else
				{
					VikingPoses.SetSitting(life.humanoid, false);
				}
				// Standing up on top of the bed, where the game puts a player getting out of one, can leave a viking on a
				// patch of navmesh on the bed with no way down to the floor
				if (bed != null && life.attachPoint != null && Utils.DistanceXZ(settledAt, bed.m_spawnPoint.position) <= BedLeaveReach)
				{
					life.DetachTo(settledAt);
					return;
				}
				life.Detach();
			}

			/// <summary>
			/// Gets up at once and lets go of its bed or chair
			/// </summary>
			public override void End()
			{
				StandUp();
				if (bed != null)
				{
					life.ReleaseObject(bed.GetComponent<ZNetView>());
				}
				if (chair != null)
				{
					life.ReleaseObject(chair.GetComponentInParent<ZNetView>());
				}
			}

			/// <summary>
			/// Checks whether the viking is lying or sitting down for the night - from settling down until it stands up in
			/// the morning, which can be a while after dawn (see WakeUp)
			/// </summary>
			/// <returns>True while it is</returns>
			public override bool Resting()
			{
				return(settled && (IsNight() || stretchUntil < 0f));
			}

			/// <summary>
			/// Finds a free bed in the village's core, or one a player put up that brought a viking (see VillageBeds) -
			/// one no player has claimed, no other viking holds tonight and no other viking of the village calls its own
			/// - its own bed first, then one in its home, then the nearest, and notes it as the viking's own. The
			/// outposts are left out otherwise, so no one sleeps out there
			/// </summary>
			/// <returns>The bed, or null</returns>
			private Bed FindBed()
			{
				life.Home(out VillagePaths.House home);
				ZDO self = life.nview.GetZDO();
				Bed best = null;
				float bestScore = float.MaxValue;
				foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
				{
					ZNetView view = instance.Value;
					if (view == null || (!life.village.InCoreHouse(view.transform.position, 0.5f) && !(instance.Key.GetBool(VillageBeds.BroughtKey) && life.village.Contains(view.transform.position))))
					{
						continue;
					}
					Bed candidate = view.GetComponent<Bed>();
					if (candidate == null || candidate.m_spawnPoint == null || candidate.GetOwner() != 0L || !life.IsFree(view))
					{
						continue;
					}
					ZDO holder = VillageBeds.Holder(instance.Key, life.village);
					if (holder != null && holder != self)
					{
						continue;
					}
					float score = Vector3.Distance(view.transform.position, life.transform.position) + (Village.InHouse(home, view.transform.position, 0.5f) ? 0f : 1000f) - ((holder == self) ? 10000f : 0f);
					if (score < bestScore)
					{
						best = candidate;
						bestScore = score;
					}
				}
				if (best == null || !life.ClaimObject(best.GetComponent<ZNetView>()))
				{
					return(null);
				}
				VillageBeds.Hold(self, best.GetComponent<ZNetView>());
				return(best);
			}

			/// <summary>
			/// Finds a chair in the viking's home that no other viking holds
			/// </summary>
			/// <param name="house">Its home</param>
			/// <returns>The chair, or null</returns>
			private Chair FindChair(VillagePaths.House house)
			{
				foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
				{
					ZNetView view = instance.Value;
					if (view == null || !Village.InHouse(house, view.transform.position, 0.5f))
					{
						continue;
					}
					Chair candidate = view.GetComponentInChildren<Chair>();
					if (candidate != null && candidate.m_attachPoint != null && !candidate.m_inShip && life.IsFree(view) && life.ClaimObject(view))
					{
						return(candidate);
					}
				}
				return(null);
			}
		}
	}
}
