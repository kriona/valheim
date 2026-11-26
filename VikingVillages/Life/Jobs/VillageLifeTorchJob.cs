using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Crafting a new torch (see TorchJob), and when a viking needs one
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Finds a torch for the viking to craft - when its own is half burnt or it has none, and it has no spare (see
		/// NeedsTorch) - with what the torch takes in the village's chests or carried, and wood it can get, or else the
		/// job that gets what the village is short of, like a hunt for greylings for their resin (see GatherFor)
		/// </summary>
		/// <returns>The job, or null when it needs no torch or can't get what one takes</returns>
		private Job FindTorchWork()
		{
			if (!NeedsTorch())
			{
				return(null);
			}
			ArrowPlan plan = PlanCraft(TorchPrefab, out string missing);
			if (plan == null)
			{
				return(GatherFor(missing));
			}
			bool woodLeft = (Time.time >= noWoodUntil || CarriedWood() >= plan.Wood || FindWoodChest() != null);
			if (!woodLeft || (plan.Station != null && !ClaimObject(plan.Station.GetComponent<ZNetView>())))
			{
				return(null);
			}
			return(new TorchJob(this, plan));
		}

		/// <summary>
		/// Checks whether the viking needs a new torch - it has none of its own, or its only one has burnt down to
		/// TorchReplaceShare of its strength - which it never does with a dverger circlet of its own to light its way
		/// </summary>
		/// <returns>True when it does</returns>
		private bool NeedsTorch()
		{
			if (viking != null && viking.HasCirclet())
			{
				return(false);
			}
			List<ItemDrop.ItemData> torches = humanoid.GetInventory().GetAllItems().FindAll((ItemDrop.ItemData item) => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch && !VikingItems.IsCarried(item));
			if (torches.Count != 1)
			{
				return(torches.Count == 0);
			}
			ItemDrop.ItemData torch = torches[0];
			return(torch.m_durability <= torch.GetMaxDurability() * TorchReplaceShare);
		}

		/// <summary>
		/// Checks whether the viking's torches won't see it through the dark - for the night watch, when they don't hold
		/// enough burn between them to last the rest of its shift (see WatchLeft), and for any other viking, when it has
		/// none of its own at all. A viking with a dverger circlet of its own needs no torches
		/// </summary>
		/// <returns>True when they won't</returns>
		private bool TorchShort()
		{
			if (viking != null && viking.HasCirclet())
			{
				return(false);
			}
			List<ItemDrop.ItemData> torches = humanoid.GetInventory().GetAllItems().FindAll((ItemDrop.ItemData item) => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch && !VikingItems.IsCarried(item));
			if (torches.Count == 0)
			{
				return(true);
			}
			if (!IsWatch())
			{
				return(false);
			}
			float burn = 0f;
			foreach (ItemDrop.ItemData torch in torches)
			{
				float drain = torch.m_shared.m_durabilityDrain * Game.m_durabilityRate;
				if (!torch.m_shared.m_useDurability || drain <= 0f)
				{
					return(false);
				}
				burn += torch.m_durability / drain;
			}
			return(burn < WatchLeft());
		}

		/// <summary>
		/// Finds a torch for the viking to craft at dusk or night, when its torches won't see it through the dark (see
		/// TorchShort) - only from wood and what else a torch takes that it carries or the village's chests hold, since
		/// it doesn't go out gathering in the dark - leaving it for NightTorchRetry seconds when they can't give it that
		/// </summary>
		/// <returns>The job, or null when its torches will do or it can't craft one</returns>
		private Job FindNightTorchWork()
		{
			if (Time.time < nightTorchAgainAt || !TorchShort())
			{
				return(null);
			}
			ArrowPlan plan = PlanCraft(TorchPrefab, out string _);
			if (plan == null || VillageWood() < plan.Wood || (plan.Station != null && !ClaimObject(plan.Station.GetComponent<ZNetView>())))
			{
				nightTorchAgainAt = Time.time + NightTorchRetry;
				return(null);
			}
			return(new TorchJob(this, plan, true));
		}

		/// <summary>
		/// Crafting a new torch for itself (see FindTorchWork) - getting what it takes from the village's chests and
		/// wood, then crafting it at the station its recipe needs, or where it stands when it needs none - raising the
		/// viking's crafting skill the way a player's rises. At dusk and night (see FindNightTorchWork) it takes only
		/// wood it carries or the chests hold, rather than going out to gather it in the dark
		/// </summary>
		private class TorchJob : Job
		{
			private readonly ArrowPlan plan;
			private readonly CraftingStation bench;
			private readonly bool night;
			private WoodRun run;
			private Container chest;
			private float actAt = -1f;
			private float craftUntil = -1f;
			private float nextRenew;

			/// <summary>
			/// Makes the job, with the station already claimed when the torch needs one
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="torch">What the torch takes</param>
			/// <param name="dark">True for one crafted at dusk or night, from what it carries and the chests hold</param>
			public TorchJob(VillageLife owner, ArrowPlan torch, bool dark = false) : base(owner)
			{
				plan = torch;
				bench = torch.Station;
				night = dark;
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Gets the ingredients and wood, goes to the station when there is one, works and crafts the torch
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's crafted, it no longer needs one, what it takes is gone, there's no wood left to get -
			/// carried or in the chests, in the dark - or it can't get to the station</returns>
			public override bool Tick(float dt)
			{
				if (bench != null && Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.ClaimObject(bench.GetComponent<ZNetView>());
				}
				if (craftUntil >= 0f)
				{
					if (bench != null)
					{
						life.Face(bench.transform.position);
					}
					if (Time.time < craftUntil)
					{
						return(false);
					}
					craftUntil = -1f;
					life.SetCrafting(0);
					if (life.CarriedWood() >= plan.Wood && life.UseCraft(plan))
					{
						life.UseWood(plan.Wood);
						life.humanoid.GetInventory().AddItem(plan.Prefab, plan.Batch, 1, 0, 0L, "", false);
						Skills.SkillType skill = ((bench != null && bench.m_craftingSkill != Skills.SkillType.None) ? bench.m_craftingSkill : Skills.SkillType.Crafting);
						life.RaiseSkill(skill, 1f);
						if (bench != null)
						{
							VillageSignals.SendEffect(VillageSignals.CraftDoneEffect, bench.GetComponent<ZNetView>().GetZDO().m_uid);
						}
					}
					return(true);
				}
				if (night ? !life.TorchShort() : !life.NeedsTorch())
				{
					return(true);
				}
				if (life.MissingCraft(plan, out string ingredient, out int needed))
				{
					return(Fetch(dt, ingredient, needed));
				}
				// In the dark it only takes wood it carries or the chests hold
				if (night && life.CarriedWood() < plan.Wood && life.FindWoodChest() == null)
				{
					life.nightTorchAgainAt = Time.time + NightTorchRetry;
					return(true);
				}
				if (!life.HaveWood(plan.Wood, ref run, dt, out bool done))
				{
					return(done);
				}
				if (bench != null)
				{
					if (Utils.DistanceXZ(bench.transform.position, life.transform.position) > CraftReach)
					{
						life.Go(dt, bench.transform.position, CraftReach, false);
						return(life.moveFailed);
					}
					life.Face(bench.transform.position);
					life.SetCrafting(Mathf.Max(1, bench.m_useAnimation));
					VillageSignals.SendEffect(VillageSignals.CraftEffect, bench.GetComponent<ZNetView>().GetZDO().m_uid);
				}
				else
				{
					life.ai.StopMoving();
					VikingPoses.Interact(life.humanoid);
				}
				craftUntil = Time.time + CraftTime;
				return(false);
			}

			/// <summary>
			/// Walks to the nearest chest holding an ingredient it needs and takes it - giving up when no chest holds it
			/// any more, or it can't get to the chest
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="ingredient">The ingredient's prefab name</param>
			/// <param name="needed">How many more it needs</param>
			/// <returns>True when it gives up</returns>
			private bool Fetch(float dt, string ingredient, int needed)
			{
				if (chest == null || !life.Usable(chest))
				{
					chest = life.ChestWithCraft(ingredient);
					actAt = -1f;
					if (chest == null)
					{
						return(true);
					}
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
				// A chest it took nothing from - no room to carry it, say - is left alone for a while
				if (life.TakeCraft(chest, ingredient, needed) <= 0)
				{
					life.Avoid(chest.m_nview);
				}
				chest = null;
				actAt = -1f;
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
			/// Stops working and lets go of the station and the wood it was after, carrying what it carries for crafting
			/// back to a chest
			/// </summary>
			public override void End()
			{
				life.SetCrafting(0);
				if (run != null)
				{
					run.Release();
				}
				if (bench != null)
				{
					life.ReleaseObject(bench.GetComponent<ZNetView>());
				}
				life.ReturnCraft();
			}
		}
	}
}
