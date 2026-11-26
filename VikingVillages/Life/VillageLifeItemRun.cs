using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Getting an item a viking works or fights with, taken from a chest or crafted (see ItemRun)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Getting an item a viking works or fights with - taking it from a chest as its own, or else crafting it from
		/// the game's recipe: getting what it takes besides wood from the village's chests, gathering the wood, then
		/// working at the station with the player's crafting animation and the station's sounds, raising the viking's
		/// skill the way a player's rises. A plan for an upgrade (see PlanUpgrade) raises a piece it has a quality
		/// instead of crafting a new one
		/// </summary>
		private class ItemRun
		{
			private readonly VillageLife life;
			private readonly System.Predicate<ItemDrop.ItemData> takes;
			private readonly string prefab;
			private Container source;
			private ArrowPlan craft;
			private WoodRun run;
			private Container chest;
			private float actAt = -1f;
			private float craftUntil = -1f;

			/// <summary>
			/// Starts getting the item, with the station it's crafted at already claimed when it's crafted
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="fromChest">The chest it takes the item from, or null to craft it</param>
			/// <param name="wanted">Which items in the chest it takes</param>
			/// <param name="craftPrefab">The prefab it crafts when the chest has none</param>
			/// <param name="recipe">What it takes to craft, or null when it's taken from the chest</param>
			public ItemRun(VillageLife owner, Container fromChest, System.Predicate<ItemDrop.ItemData> wanted, string craftPrefab, ArrowPlan recipe)
			{
				life = owner;
				source = fromChest;
				takes = wanted;
				prefab = craftPrefab;
				craft = recipe;
			}

			/// <summary>
			/// Takes the item from the chest, or else crafts it
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="got">Set to the item once it has it, or null</param>
			/// <returns>True once it has it, or can't get it</returns>
			public bool Tick(float dt, out ItemDrop.ItemData got)
			{
				got = null;
				if (source != null)
				{
					if (!life.Usable(source))
					{
						FromChestFailed(false);
						return(craft == null);
					}
					if (!Reach(dt, source))
					{
						return(false);
					}
					got = life.TakeTool(source, takes);
					if (got == null)
					{
						FromChestFailed(true);
						return(craft == null);
					}
					source = null;
					return(true);
				}
				if (craft == null)
				{
					return(true);
				}
				return(Craft(dt, out got));
			}

			/// <summary>
			/// Gives up on the chest - leaving it alone for a while when it couldn't be used - and crafts the item instead
			/// when the village has what it takes
			/// </summary>
			/// <param name="avoid">True to leave the chest alone for a while</param>
			private void FromChestFailed(bool avoid)
			{
				if (avoid && source != null)
				{
					life.Avoid(source.m_nview);
				}
				source = null;
				craft = life.PlanCraft(prefab, out string _);
				if (craft != null && craft.Station != null && !life.ClaimObject(craft.Station.GetComponent<ZNetView>()))
				{
					craft = null;
				}
			}

			/// <summary>
			/// Crafts the item - getting what it takes besides wood from the village's chests and its wood, then working
			/// at the station
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="got">Set to the item once it's crafted, or null</param>
			/// <returns>True once it's crafted, or what it takes is gone, there's no wood left to get, or it can't get to
			/// the station</returns>
			private bool Craft(float dt, out ItemDrop.ItemData got)
			{
				got = null;
				CraftingStation bench = craft.Station;
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
					// A piece to upgrade that's gone, or was upgraded meanwhile, takes nothing
					if (craft.Upgrade != null && !life.CanStillUpgrade(craft))
					{
						return(true);
					}
					if (life.CarriedWood() < craft.Wood || !life.UseCraft(craft))
					{
						return(true);
					}
					life.UseWood(craft.Wood);
					got = ((craft.Upgrade != null) ? life.UpgradeGear(craft) : life.humanoid.GetInventory().AddItem(craft.Prefab, 1, 1, 0, 0L, "", false));
					if (got != null && craft.Upgrade == null)
					{
						TestLog.Count("Crafted", TestLog.ItemName(got));
					}
					life.RaiseSkill(((bench != null && bench.m_craftingSkill != Skills.SkillType.None) ? bench.m_craftingSkill : Skills.SkillType.Crafting), 1f);
					if (bench != null)
					{
						VillageSignals.SendEffect(VillageSignals.CraftDoneEffect, bench.GetComponent<ZNetView>().GetZDO().m_uid);
					}
					return(true);
				}
				if (life.MissingCraft(craft, out string ingredient, out int needed))
				{
					return(Fetch(dt, ingredient, needed));
				}
				if (!life.HaveWood(craft.Wood, ref run, dt, out bool done))
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
			/// Walks to the nearest chest holding an ingredient the item takes and takes it - giving up when no chest
			/// holds it any more
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
				if (!Reach(dt, chest))
				{
					return(false);
				}
				// A chest it took nothing from - no room to carry it, say - is left alone for a while
				if (life.TakeCraft(chest, ingredient, needed) <= 0)
				{
					life.Avoid(chest.m_nview);
				}
				chest = null;
				return(false);
			}

			/// <summary>
			/// Walks to a chest and reaches into it, the way a player opens one - leaving a chest it can't get to alone
			/// for a while
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="target">The chest</param>
			/// <returns>True once it has reached into it</returns>
			private bool Reach(float dt, Container target)
			{
				if (Utils.DistanceXZ(target.transform.position, life.transform.position) > ChestReach)
				{
					life.Go(dt, target.transform.position, ChestReach, false);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						if (target == source)
						{
							FromChestFailed(true);
						}
						else
						{
							life.Avoid(target.m_nview);
							chest = null;
						}
					}
					actAt = -1f;
					return(false);
				}
				if (actAt < 0f)
				{
					life.Face(target.transform.position);
					VikingPoses.Interact(life.humanoid);
					life.ShowChestOpen(target);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (Time.time < actAt)
				{
					return(false);
				}
				actAt = -1f;
				return(true);
			}

			/// <summary>
			/// Checks whether the wood the viking is after lies outside the village
			/// </summary>
			/// <returns>True when it does</returns>
			public bool Outing()
			{
				return(run != null && run.Outing());
			}

			/// <summary>
			/// Renews the viking's claim on the station it crafts at
			/// </summary>
			public void Renew()
			{
				if (craft != null && craft.Station != null)
				{
					life.ClaimObject(craft.Station.GetComponent<ZNetView>());
				}
			}

			/// <summary>
			/// Stops working at the station and lets go of it and the wood it was after, carrying what it carries for
			/// crafting back to a chest
			/// </summary>
			public void Release()
			{
				life.SetCrafting(0);
				if (run != null)
				{
					run.Release();
				}
				if (craft != null && craft.Station != null)
				{
					life.ReleaseObject(craft.Station.GetComponent<ZNetView>());
				}
				life.ReturnCraft();
			}
		}
	}
}
