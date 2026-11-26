using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Repairing gear at the village's workbench or forge (see GearJob), and crafting gear a viking is missing
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Finds gear for the viking to repair at the village's workbench or forge - when a piece of it the station can
		/// repair has worn down to GearWornShare and no other viking is working at the station
		/// </summary>
		/// <returns>The job, or null when there's nothing to repair</returns>
		private Job FindGearWork()
		{
			foreach (int prefab in new[] { WorkbenchHash, ForgeHash })
			{
				CraftingStation bench = FindStation(prefab);
				if (bench != null && WornGear(bench, GearWornShare) != null && ClaimObject(bench.GetComponent<ZNetView>()))
				{
					return(new GearJob(this, bench));
				}
			}
			return(null);
		}

		/// <summary>
		/// Finds a piece of the viking's own gear that a crafting station can repair and that has worn below a share of
		/// its durability
		/// </summary>
		/// <param name="bench">The station</param>
		/// <param name="below">The share of its durability it has to be below - 1 for any wear</param>
		/// <returns>The most worn piece, or null when there's none</returns>
		private ItemDrop.ItemData WornGear(CraftingStation bench, float below)
		{
			ItemDrop.ItemData worst = null;
			float worstShare = below;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (VikingItems.IsCarried(item) || VikingItems.IsProp(item) || !item.m_shared.m_useDurability || item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch)
				{
					continue;
				}
				float max = item.GetMaxDurability();
				float share = ((max > 0f) ? item.m_durability / max : 1f);
				if (share < worstShare && CanRepairAt(item, bench))
				{
					worst = item;
					worstShare = share;
				}
			}
			return(worst);
		}

		/// <summary>
		/// Checks whether a crafting station can repair an item, the way the game decides for a player - the station
		/// its recipe is crafted or repaired at, upgraded far enough
		/// </summary>
		/// <param name="item">The item</param>
		/// <param name="bench">The station</param>
		/// <returns>True when it can</returns>
		private static bool CanRepairAt(ItemDrop.ItemData item, CraftingStation bench)
		{
			if (!item.m_shared.m_canBeReparied || ObjectDB.instance == null)
			{
				return(false);
			}
			Recipe recipe = ObjectDB.instance.GetRecipe(item);
			if (recipe == null || (recipe.m_craftingStation == null && recipe.m_repairStation == null))
			{
				return(false);
			}
			bool station = ((recipe.m_repairStation != null && recipe.m_repairStation.m_name == bench.m_name) || (recipe.m_craftingStation != null && recipe.m_craftingStation.m_name == bench.m_name));
			return((station || item.m_worldLevel < Game.m_worldLevel) && Mathf.Min(bench.GetLevel(), 4) >= recipe.m_minStationLevel);
		}

		/// <summary>
		/// The kinds of gear a viking crafts for itself, in the order it crafts them - a melee weapon, a bow or crossbow, a
		/// shield, then armor for its chest, legs, head and shoulders
		/// </summary>
		private static readonly string[] CraftedGearKinds = { "melee", "ranged", "Shield", "Chest", "Legs", "Helmet", "Shoulder" };

		/// <summary>
		/// Finds a weapon, armor or a shield for the viking to craft - any the game has a recipe for that it would take up
		/// over its own (see VikingCore.WouldTakeUp), when the village has a station of the recipe's level and its chests
		/// hold everything it takes, wood included, so it never goes out gathering for it - the best of each kind it can
		/// make, in the order of CraftedGearKinds, one viking of the village arming itself at a time. It takes the piece
		/// up once it's crafted
		/// </summary>
		/// <remarks>
		/// A village seldom has what better gear takes unless players put it in its chests
		/// </remarks>
		/// <returns>The job, or null when there's nothing better it can craft</returns>
		private Job FindGearCraftWork()
		{
			if (viking == null || ObjectDB.instance == null || Time.time < nextGearCraftCheck || OtherDoing(ArmingKey))
			{
				return(null);
			}
			nextGearCraftCheck = Time.time + GearCraftCheckInterval;
			Dictionary<string, KeyValuePair<float, ArrowPlan>> picks = new Dictionary<string, KeyValuePair<float, ArrowPlan>>();
			int wood = VillageWood();
			foreach (Recipe recipe in ObjectDB.instance.m_recipes)
			{
				if (!Crafts(recipe))
				{
					continue;
				}
				ItemDrop.ItemData item = ItemOf(recipe.m_item);
				string kind = ((item != null) ? CraftedGearKind(item) : null);
				if (kind == null || !viking.WouldTakeUp(item))
				{
					continue;
				}
				float value = viking.GearValue(item);
				if (picks.TryGetValue(kind, out KeyValuePair<float, ArrowPlan> known) && known.Key >= value)
				{
					continue;
				}
				ArrowPlan plan = PlanCraft(item.m_dropPrefab.name, out string _);
				if (plan != null && plan.Wood <= wood)
				{
					picks[kind] = new KeyValuePair<float, ArrowPlan>(value, plan);
				}
			}
			foreach (string kind in CraftedGearKinds)
			{
				if (!picks.TryGetValue(kind, out KeyValuePair<float, ArrowPlan> pick))
				{
					continue;
				}
				ArrowPlan plan = pick.Value;
				if (plan.Station != null && !ClaimObject(plan.Station.GetComponent<ZNetView>()))
				{
					continue;
				}
				string name = Localization.instance.Localize(ItemOf(plan.Prefab).m_shared.m_name);
				return(new WeaponJob(this, plan, plan.Prefab, "Crafting " + name, false, true));
			}
			return(null);
		}

		/// <summary>
		/// Finds which kind of gear a viking crafts for itself an item is (see CraftedGearKinds)
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>Its kind, or null when it isn't a weapon other than a torch or a pickaxe, armor or a shield</returns>
		private static string CraftedGearKind(ItemDrop.ItemData item)
		{
			ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
			if (type == ItemDrop.ItemData.ItemType.Shield || type == ItemDrop.ItemData.ItemType.Chest || type == ItemDrop.ItemData.ItemType.Legs || type == ItemDrop.ItemData.ItemType.Helmet || type == ItemDrop.ItemData.ItemType.Shoulder)
			{
				return(type.ToString());
			}
			if (!item.IsWeapon() || type == ItemDrop.ItemData.ItemType.Torch || item.m_shared.m_damages.m_pickaxe > 0f)
			{
				return(null);
			}
			return(VikingCore.IsRanged(item) ? "ranged" : "melee");
		}

		/// <summary>
		/// Repairing its gear at the village's workbench or forge - walking to the station and working at it with the
		/// player's crafting animation, then repairing a piece the station can repair, the most worn first, with the
		/// station's repair sound, until every piece it can repair there is as good as new
		/// </summary>
		private class GearJob : Job
		{
			private readonly CraftingStation bench;
			private float workUntil = -1f;
			private float nextRenew;

			/// <summary>
			/// Makes the job, with the workbench already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="workbench">The workbench</param>
			public GearJob(VillageLife owner, CraftingStation workbench) : base(owner)
			{
				bench = workbench;
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Walks to the workbench and repairs its gear there a piece at a time
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once there's nothing left to repair, or it can't get to the workbench</returns>
			public override bool Tick(float dt)
			{
				if (bench == null)
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.ClaimObject(bench.GetComponent<ZNetView>());
				}
				ItemDrop.ItemData worn = life.WornGear(bench, 1f);
				if (workUntil >= 0f)
				{
					life.Face(bench.transform.position);
					if (Time.time < workUntil)
					{
						return(false);
					}
					workUntil = -1f;
					if (worn != null)
					{
						// The more worn it was, the more repairing it raises the skill, the same as a player's
						float max = worn.GetMaxDurability();
						life.RaiseSkill(Skills.SkillType.Crafting, ((max > 0f) ? 1f - worn.m_durability / max : 0f));
						worn.m_durability = max;
						VillageSignals.SendEffect(VillageSignals.GearRepairEffect, bench.GetComponent<ZNetView>().GetZDO().m_uid);
					}
					return(false);
				}
				if (worn == null)
				{
					return(true);
				}
				if (Utils.DistanceXZ(bench.transform.position, life.transform.position) > CraftReach)
				{
					life.Go(dt, bench.transform.position, CraftReach, false);
					return(life.moveFailed);
				}
				life.Face(bench.transform.position);
				life.SetCrafting(Mathf.Max(1, bench.m_useAnimation));
				workUntil = Time.time + GearRepairTime;
				return(false);
			}

			/// <summary>
			/// Stops working at the workbench and lets go of it
			/// </summary>
			public override void End()
			{
				life.SetCrafting(0);
				if (bench != null)
				{
					life.ReleaseObject(bench.GetComponent<ZNetView>());
				}
			}
		}
	}
}
