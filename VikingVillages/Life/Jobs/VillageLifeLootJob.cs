using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Putting loot lying around the village away (see LootJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Putting loot away - by day, or at any hour to clear up after a fight - picking up an item lying around the
		/// village, when one of its chests has room for it, and carrying it to the nearest chest with room, or putting
		/// away what it already carries - and carrying what the vikings don't use out of the village when the chests hold
		/// enough of it
		/// </summary>
		private class LootJob : Job
		{
			private readonly bool afterFight;
			private readonly bool takeOutside;
			private readonly bool cookFirst;
			private ZNetView loot;
			private ZDOID lootId;
			private Container chest;
			private float actAt = -1f;
			private float nextRoomCheck;
			private bool dumping;
			private Vector3 dumpSpot;

			/// <summary>
			/// Makes the job, with the loot already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="target">The loot to pick up, or null to put away what it carries</param>
			/// <param name="fight">True for clearing up after a fight, which goes on through dusk and night</param>
			/// <param name="outside">True when the loot was picked as something the chests hold enough of to carry out of
			/// the village, as what an enemy dropped is, so it's still wanted once the viking gets to it, whether or not
			/// it may leave the village then</param>
			/// <param name="cook">True to leave raw food it carries for a cooking station with room by day (see
			/// FindWork), false when no cooking work could be had for it, so the job doesn't end only to be picked
			/// again</param>
			public LootJob(VillageLife owner, ZNetView target, bool fight, bool outside = false, bool cook = true) : base(owner)
			{
				loot = target;
				lootId = ((target != null) ? target.GetZDO().m_uid : ZDOID.None);
				afterFight = fight;
				takeOutside = outside;
				cookFirst = cook;
			}

			/// <summary>
			/// Picks up the loot, and more lying near it as long as it can carry more without being held back by its load -
			/// part of a stack when that's all it has room for - then takes it to the chests and puts it in, putting down
			/// where it stands what no chest has room for anymore
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's all put away, gone, or it's dusk, unless it's clearing up after a fight</returns>
			public override bool Tick(float dt)
			{
				if (!afterFight && (IsNight() || IsDusk()))
				{
					return(true);
				}
				if (loot != null)
				{
					return(PickUp(dt));
				}
				// Raw meat it picked up goes straight to a cooking station with room by day, and the rest is put away
				// along with what it cooked (see FindWork)
				if (cookFirst && !dumping && !IsNight() && !IsDusk() && life.FindCookStation(true) != null)
				{
					return(true);
				}
				ItemDrop.ItemData carried = life.CarriedLoot();
				return((carried != null) ? PutAway(dt, carried) : true);
			}

			/// <summary>
			/// Goes for more loot lying near the viking after it picked some up, while it can carry more without being
			/// held back by its load (see FindLoot)
			/// </summary>
			private void PickNext()
			{
				ZNetView next = life.FindLoot();
				if (next == null || Utils.DistanceXZ(next.transform.position, life.transform.position) > LootBatchReach || !life.ClaimWork(next.GetZDO().m_uid))
				{
					return;
				}
				loot = next;
				lootId = next.GetZDO().m_uid;
				life.UpdateOuting(Outing());
			}

			/// <summary>
			/// Walks to the loot and picks it up, while a chest still has room for it - checked every RoomCheckInterval
			/// on the way and again once it's there - then goes for the next lying near it
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True when it's gone, there's no room for it, or there's no way to it, and it carries nothing to
			/// put away</returns>
			private bool PickUp(float dt)
			{
				if (loot == null || !IsLoot(loot))
				{
					Release();
					loot = null;
					return(life.CarriedLoot() == null);
				}
				if (actAt >= 0f)
				{
					if (Time.time < actAt)
					{
						return(false);
					}
					actAt = -1f;
					life.PickUpLoot(loot);
					Release();
					loot = null;
					PickNext();
					return(loot == null && life.CarriedLoot() == null);
				}
				// Out of the village with a monster in sight, it leaves the loot and heads home before it's seen
				if (!life.village.Contains(loot.transform.position) && life.DangerNear(life.transform.position))
				{
					Release();
					loot = null;
					life.Flee(FleeReason.Forage);
					return(life.CarriedLoot() == null);
				}
				bool reached = (Utils.DistanceXZ(loot.transform.position, life.transform.position) <= ItemReach);
				if (reached || Time.time >= nextRoomCheck)
				{
					nextRoomCheck = Time.time + RoomCheckInterval;
					ItemDrop.ItemData item = loot.GetComponent<ItemDrop>().m_itemData;
					if (life.CarryRoom(item, CarryLoot) <= 0 || !WantsLoot(item, life.UsableChests(), takeOutside || life.MayLeave()))
					{
						Release();
						loot = null;
						return(life.CarriedLoot() == null);
					}
				}
				if (!reached)
				{
					life.Go(dt, loot.transform.position, ItemReach, false);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.DebugAvoid(loot, "it got no nearer on its way there, " + Utils.DistanceXZ(loot.transform.position, life.transform.position).ToString("F1") + "m from it");
						life.Avoid(loot);
						Release();
						loot = null;
						return(life.CarriedLoot() == null);
					}
					return(false);
				}
				life.Face(loot.transform.position);
				VikingPoses.Interact(life.humanoid);
				RequestItem(loot);
				actAt = Time.time + ReachTime;
				return(false);
			}

			/// <summary>
			/// Walks to the nearest chest with room for what it carries, opens it and puts it in - leaving a chest it
			/// can't get to alone and trying another. Something the vikings don't use that no chest takes, because the
			/// chests hold enough of it, it carries out of the village and drops on the village's pile there instead (see
			/// FindDumpSpot) - holding on to it while there's nowhere to drop it, for DumpGiveUpTime seconds before it
			/// puts it down where it stands - and anything else no chest has room for it puts down where it stands
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="carried">The loot it carries</param>
			/// <returns>True once it's put away, put down or dropped outside, or it's holding on to it for now</returns>
			private bool PutAway(float dt, ItemDrop.ItemData carried)
			{
				if (dumping)
				{
					return(Dump(dt));
				}
				if (chest == null || !life.Usable(chest) || Time.time >= nextRoomCheck)
				{
					nextRoomCheck = Time.time + RoomCheckInterval;
					if (chest == null || !life.Usable(chest) || !HasRoom(chest, carried, life.UsableChests()))
					{
						chest = life.FindChestFor(carried);
						actAt = -1f;
					}
					if (chest == null)
					{
						life.ai.StopMoving();
						if (KindOf(carried) != StoreKind.Other)
						{
							life.PutDownLoot(false, "no chest has room for it");
							return(life.CarriedLoot() == null);
						}
						if (!life.MayLeave())
						{
							life.dumpAgainAt = Time.time + DumpRetryTime;
							return(true);
						}
						if (!life.FindDumpSpot(out dumpSpot))
						{
							if (!life.DumpLater())
							{
								life.PutDownLoot(true, "the chests hold enough of it, and there's been nowhere outside the village to drop it for " + DumpGiveUpTime.ToString("F0") + "s");
							}
							return(true);
						}
						life.dumpNowhereSince = -1f;
						dumping = true;
						life.UpdateOuting(true);
						return(false);
					}
				}
				if (actAt >= 0f)
				{
					if (Time.time < actAt)
					{
						return(false);
					}
					actAt = -1f;
					if (life.PutLoot(chest, carried))
					{
						// The rest it carries goes in the same chest when it fits
						nextRoomCheck = 0f;
						return(life.CarriedLoot() == null);
					}
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
				life.Face(chest.transform.position);
				VikingPoses.Interact(life.humanoid);
				life.ShowChestOpen(chest);
				actAt = Time.time + ReachTime;
				return(false);
			}

			/// <summary>
			/// Lets go of the loot it was after
			/// </summary>
			private void Release()
			{
				if (lootId != ZDOID.None)
				{
					life.ReleaseWork(lootId);
					lootId = ZDOID.None;
				}
			}

			/// <summary>
			/// Walks out of the village to where it drops what the chests hold enough of, and drops it there, marked so no
			/// viking stores it again - dropping it where it got to when it can't get there
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's dropped it and carries nothing else to put away</returns>
			private bool Dump(float dt)
			{
				if (Utils.DistanceXZ(dumpSpot, life.transform.position) > DumpArrive && !life.Go(dt, dumpSpot, DumpArrive, false))
				{
					return(false);
				}
				life.moveFailed = false;
				life.ai.StopMoving();
				VikingPoses.Interact(life.humanoid);
				life.PutDownLoot(true, "the chests hold enough of it");
				dumping = false;
				chest = null;
				return(life.CarriedLoot() == null);
			}

			/// <summary>
			/// Checks whether the loot the viking is after lies outside the village, or it's taking what the chests hold
			/// enough of out of the village
			/// </summary>
			/// <returns>True when it does</returns>
			public override bool Outing()
			{
				return(dumping || (loot != null && !life.village.Contains(loot.transform.position)));
			}

			/// <summary>
			/// Lets go of the loot it was after, keeping what it carries for the next time it puts loot away
			/// </summary>
			public override void End()
			{
				Release();
			}
		}
	}
}
