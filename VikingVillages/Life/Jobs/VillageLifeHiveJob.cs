using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Building a beehive (see HiveJob), and taking the honey from the village's hives
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Finds a beehive for the viking to build - when the village has fewer than VillageHives.HivesMax, no other
		/// viking is building one, there's a queen bee it carries or can take from a chest, wood it can get, and a spot
		/// for one
		/// </summary>
		/// <returns>The job, or null when there's no hive to build</returns>
		private Job FindHiveWork()
		{
			if (OtherDoing(HiveKey) || VillageHives.Count(village) >= VillageHives.HivesMax)
			{
				return(null);
			}
			int wood = VillageHives.WoodCost();
			ItemDrop.ItemData queen = ItemOf(VillageHives.QueenBeePrefab);
			if (wood < 0 || queen == null || (CarriedLootOf(queen) <= 0 && FindQueenChest() == null))
			{
				return(null);
			}
			if (Time.time < noWoodUntil && CarriedWood() < wood && FindWoodChest() == null)
			{
				return(null);
			}
			if (!VillageHives.FindSpot(village, out Vector3 spot, out Quaternion rotation))
			{
				return(null);
			}
			return(new HiveJob(this, spot, rotation, wood));
		}

		/// <summary>
		/// Finds the nearest of the village's chests with a queen bee in it, leaving out those the viking couldn't get to
		/// </summary>
		/// <returns>The chest, or null when there's none</returns>
		private Container FindQueenChest()
		{
			Container nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (Container chest in UsableChests())
			{
				if (Avoided(chest.m_nview.GetZDO().m_uid) || QueenIn(chest) == null)
				{
					continue;
				}
				float distance = Vector3.Distance(chest.transform.position, transform.position);
				if (distance < nearestDistance)
				{
					nearest = chest;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Finds a queen bee in a chest
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>The queen bee's stack, or null when there's none</returns>
		private static ItemDrop.ItemData QueenIn(Container chest)
		{
			return(chest.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => item.m_dropPrefab != null && item.m_dropPrefab.name == VillageHives.QueenBeePrefab));
		}

		/// <summary>
		/// Takes a queen bee out of a chest the viking has reached, to carry
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>False when there was none, or it couldn't be used or carried</returns>
		private bool TakeQueen(Container chest)
		{
			if (!OpenChest(chest))
			{
				return(false);
			}
			ItemDrop.ItemData queen = QueenIn(chest);
			if (queen == null || Carry(queen, 1, CarryLoot) <= 0)
			{
				return(false);
			}
			chest.GetInventory().RemoveItem(queen, 1);
			chest.m_closeEffects.Create(chest.transform.position, chest.transform.rotation);
			return(true);
		}

		/// <summary>
		/// Uses up a queen bee the viking carries
		/// </summary>
		/// <returns>False when it carries none</returns>
		private bool UseQueen()
		{
			ItemDrop.ItemData carried = humanoid.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => IsCarriedFor(item, CarryLoot) && item.m_dropPrefab != null && item.m_dropPrefab.name == VillageHives.QueenBeePrefab);
			if (carried == null)
			{
				return(false);
			}
			UseCarried(carried, 1);
			return(true);
		}

		/// <summary>
		/// Building a beehive by day - taking a queen bee from a chest when it carries none, getting the wood, then
		/// walking to the hive's spot and hammering it together, using up the queen bee and the wood. One viking builds a
		/// hive at a time
		/// </summary>
		private class HiveJob : Job
		{
			private readonly Vector3 spot;
			private readonly Quaternion rotation;
			private readonly int wood;
			private Container chest;
			private float takeAt = -1f;
			private WoodRun run;
			private HammerWork work;
			private float nextRenew;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="where">Where the hive goes, on the ground</param>
			/// <param name="facing">Which way it faces</param>
			/// <param name="cost">The wood it takes</param>
			public HiveJob(VillageLife owner, Vector3 where, Quaternion facing, int cost) : base(owner)
			{
				spot = where;
				rotation = facing;
				wood = cost;
				work = new HammerWork(owner, RebuildSwings);
			}

			/// <summary>
			/// Marks the viking as building a hive, so no other viking starts one
			/// </summary>
			public override void Begin()
			{
				life.SetDoing(HiveKey, true);
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>What it's doing</returns>
			public override string Describe()
			{
				return("Build beehive at " + spot.ToString("F0"));
			}

			/// <summary>
			/// Gets the queen bee and the wood, then builds the hive
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's built, or it can't be - there's no queen bee, wood or room left, or it's dusk</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk())
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.SetDoing(HiveKey, true);
				}
				ItemDrop.ItemData queen = ItemOf(VillageHives.QueenBeePrefab);
				if (queen == null)
				{
					return(true);
				}
				if (life.CarriedLootOf(queen) <= 0)
				{
					return(FetchQueen(dt));
				}
				if (!life.HaveWood(wood, ref run, dt, out bool done))
				{
					return(done);
				}
				if (!work.Tick(dt, spot))
				{
					if (life.moveFailed)
					{
						life.moveFailed = false;
						return(true);
					}
					return(false);
				}
				// Someone may have built or dropped something there while it was getting the wood
				if (VillageHives.HasRoom(life.village, spot, rotation) && life.CarriedWood() >= wood && life.UseQueen())
				{
					life.UseWood(wood);
					VillageHives.Build(life.village, spot, rotation);
				}
				return(true);
			}

			/// <summary>
			/// Walks to the nearest chest with a queen bee and takes one
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True when there's no queen bee to be had, which ends the job</returns>
			private bool FetchQueen(float dt)
			{
				if (chest == null || !life.Usable(chest) || QueenIn(chest) == null)
				{
					chest = life.FindQueenChest();
					takeAt = -1f;
					if (chest == null)
					{
						return(true);
					}
				}
				if (takeAt >= 0f)
				{
					if (Time.time < takeAt)
					{
						return(false);
					}
					takeAt = -1f;
					if (!life.TakeQueen(chest))
					{
						chest = null;
					}
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
				chest.m_openEffects.Create(chest.transform.position, chest.transform.rotation);
				takeAt = Time.time + ReachTime;
				return(false);
			}

			/// <summary>
			/// Checks whether the wood the viking is after lies outside the village
			/// </summary>
			/// <returns>True when it does</returns>
			public override bool Outing()
			{
				return(run != null && run.Outing());
			}

			/// <summary>
			/// Lets go of the wood it was after and its mark as the one building a hive
			/// </summary>
			public override void End()
			{
				if (run != null)
				{
					run.Release();
				}
				life.SetDoing(HiveKey, false);
			}
		}

		/// <summary>
		/// Finds the nearest beehive built in the village with honey in it that no other viking is after, that the
		/// viking has room to carry the honey of and one of the village's chests has room for
		/// </summary>
		/// <returns>The beehive, or null when there's none</returns>
		private Beehive FindHive()
		{
			List<Container> chests = UsableChests();
			if (chests.Count == 0)
			{
				return(null);
			}
			Beehive nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (Beehive hive in LoadedHives())
			{
				ZNetView view = ((hive != null) ? hive.m_nview : null);
				if (view == null || !view.IsValid() || !village.OwnsPiece(view.transform.position))
				{
					continue;
				}
				ZDOID id = view.GetZDO().m_uid;
				if (Avoided(id) || hive.m_honeyItem == null || hive.GetHoneyLevel() <= 0)
				{
					continue;
				}
				if (workClaims.TryGetValue(id, out VillageLife holder) && holder != this && holder != null && holder.job != null)
				{
					continue;
				}
				float distance = Vector3.Distance(view.transform.position, transform.position);
				if (distance >= nearestDistance)
				{
					continue;
				}
				ItemDrop.ItemData honey = ItemOf(hive.m_honeyItem.gameObject.name);
				if (honey == null)
				{
					continue;
				}
				int amount = HoneyIn(hive, honey);
				honey.m_stack = CarriedLootOf(honey) + amount;
				if (CarryRoom(honey, CarryLoot) >= amount && chests.Exists((Container chest) => HasRoom(chest, honey, chests)))
				{
					nearest = hive;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Works out how much honey a beehive gives, the way it gives a player - one for each of its honey, scaled by the
		/// world's resource rate
		/// </summary>
		/// <param name="hive">The beehive</param>
		/// <param name="honey">Its honey</param>
		/// <returns>How much</returns>
		private static int HoneyIn(Beehive hive, ItemDrop.ItemData honey)
		{
			int amount = 0;
			for (int i = hive.GetHoneyLevel(); i > 0; i--)
			{
				amount += ((Game.instance != null) ? Game.instance.ScaleDrops(honey, 1) : 1);
			}
			return(amount);
		}

		/// <summary>
		/// Takes the honey out of a beehive the viking has reached, the same as a player, carried for a chest rather than
		/// dropped
		/// </summary>
		/// <param name="hive">The beehive</param>
		/// <returns>How much it took</returns>
		private int TakeHoney(Beehive hive)
		{
			if (hive == null || hive.m_nview == null || !hive.m_nview.IsValid() || hive.m_honeyItem == null)
			{
				return(0);
			}
			if (!hive.m_nview.IsOwner())
			{
				hive.m_nview.ClaimOwnership();
			}
			ItemDrop.ItemData honey = ItemOf(hive.m_honeyItem.gameObject.name);
			int amount = ((honey != null) ? HoneyIn(hive, honey) : 0);
			if (amount <= 0 || CarryRoom(honey, CarryLoot) < amount)
			{
				return(0);
			}
			int taken = Carry(honey, amount, CarryLoot);
			if (taken > 0)
			{
				TestLog.Count("Gathered", TestLog.ItemName(honey), taken);
				NoteFoodFound();
				hive.ResetLevel();
				hive.m_spawnEffect.Create(hive.m_spawnPoint.position, Quaternion.identity);
			}
			return(taken);
		}
	}
}
