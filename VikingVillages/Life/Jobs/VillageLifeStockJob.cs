using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Stocking the village's chests with wood (see StockJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Stocking the village's chests with wood by day - gathering branches and wood from the ground until it carries
		/// as much as the chest it picked has room for, or as much as it can carry, then putting it in that chest or the
		/// nearest other with room
		/// </summary>
		private class StockJob : Job
		{
			private Container chest;
			private WoodRun run;
			private bool loaded;
			private float actAt = -1f;
			private float nextRenew;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="store">The chest to stock, with room for wood</param>
			public StockJob(VillageLife owner, Container store) : base(owner)
			{
				chest = store;
			}

			/// <summary>
			/// Notes that the viking is stocking the chests, so no other viking of the village starts to
			/// </summary>
			public override void Begin()
			{
				life.SetDoing(StockingKey, true);
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Gathers wood, then takes it to the chest
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's put the wood away, there's no wood to gather, no chest has room, or it's dusk</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk())
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					life.SetDoing(StockingKey, true);
					nextRenew = Time.time + ClaimRenew;
				}
				if (!loaded)
				{
					return(Gather(dt));
				}
				return(PutAway(dt));
			}

			/// <summary>
			/// Gathers wood from the ground until it carries enough
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True when there was no wood to gather and it carries none</returns>
			private bool Gather(float dt)
			{
				ItemDrop.ItemData plain = ItemOf("Wood");
				if (plain == null || chest == null)
				{
					return(true);
				}
				int wanted = Mathf.Min(CarryMax, ChestRoom(chest, plain, null));
				if (life.CarriedWood() >= wanted || !life.CanCarryWood())
				{
					loaded = true;
					return(false);
				}
				if (run == null)
				{
					run = new WoodRun(life, wanted, false);
				}
				if (run.Tick(dt))
				{
					run = null;
					return(false);
				}
				if (run.None)
				{
					run.Release();
					run = null;
					if (life.CarriedWood() <= 0)
					{
						return(true);
					}
					loaded = true;
				}
				return(false);
			}

			/// <summary>
			/// Walks to the chest, or the nearest other with room when it's full or can't be used, and puts the wood in -
			/// keeping the wood for its work when no chest has room, and leaving a chest it can't get to alone
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's put the wood in, or no chest has room</returns>
			private bool PutAway(float dt)
			{
				if (life.CarriedWood() <= 0)
				{
					return(true);
				}
				ItemDrop.ItemData plain = ItemOf("Wood");
				if (chest == null || !life.Usable(chest) || (plain != null && ChestRoom(chest, plain, null) <= 0))
				{
					chest = life.FindStockChest();
					actAt = -1f;
					if (chest == null)
					{
						return(true);
					}
				}
				if (actAt >= 0f)
				{
					if (Time.time < actAt)
					{
						return(false);
					}
					actAt = -1f;
					if (life.PutWood(chest) > 0)
					{
						return(true);
					}
					// It was opened meanwhile, or has no room for the kinds of wood it carries
					life.Avoid(chest.m_nview);
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
			/// Checks whether the wood the viking is after lies outside the village
			/// </summary>
			/// <returns>True when it does</returns>
			public override bool Outing()
			{
				return(run != null && run.Outing());
			}

			/// <summary>
			/// Lets go of the wood it was after, and notes it's no longer stocking the chests
			/// </summary>
			public override void End()
			{
				if (run != null)
				{
					run.Release();
				}
				life.SetDoing(StockingKey, false);
			}
		}
	}
}
