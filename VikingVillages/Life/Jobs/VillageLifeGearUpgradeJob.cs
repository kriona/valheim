using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Upgrading a piece of a viking's own gear (see GearUpgradeJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Finds a piece of the viking's own gear to upgrade (see PlanUpgrade) when the village has everything it takes -
		/// its lowest quality pieces first, and of those, what it has equipped - one viking of the village at a time
		/// </summary>
		/// <returns>The job, or null when there's nothing it can upgrade</returns>
		private Job FindUpgradeWork()
		{
			if (OtherDoing(UpgradingKey) || ObjectDB.instance == null)
			{
				return(null);
			}
			List<ItemDrop.ItemData> gear = humanoid.GetInventory().GetAllItems().FindAll((ItemDrop.ItemData item) => !VikingItems.IsCarried(item) && !VikingItems.IsProp(item) && item.IsEquipable() && item.m_quality < item.m_shared.m_maxQuality);
			gear.Sort((ItemDrop.ItemData a, ItemDrop.ItemData b) => ((a.m_quality != b.m_quality) ? a.m_quality.CompareTo(b.m_quality) : b.m_equipped.CompareTo(a.m_equipped)));
			foreach (ItemDrop.ItemData item in gear)
			{
				ArrowPlan plan = PlanUpgrade(item);
				if (plan != null && ClaimObject(plan.Station.GetComponent<ZNetView>()))
				{
					return(new GearUpgradeJob(this, plan));
				}
			}
			return(null);
		}

		/// <summary>
		/// Works out what upgrading a piece of the viking's gear to its next quality takes, the way it does for a player -
		/// the station its recipe is crafted at, upgraded far enough for that quality, and the recipe's ingredients for
		/// it, everything but wood in the village's chests or carried for crafting, and the wood in the village too
		/// </summary>
		/// <param name="item">The piece</param>
		/// <returns>The upgrade, or null when the piece can't be upgraded, the village has no station for it, or it's
		/// short of anything it takes</returns>
		private ArrowPlan PlanUpgrade(ItemDrop.ItemData item)
		{
			Recipe recipe = ObjectDB.instance.GetRecipe(item);
			if (recipe == null || recipe.m_craftingStation == null || item.m_dropPrefab == null || item.m_quality >= item.m_shared.m_maxQuality)
			{
				return(null);
			}
			int quality = item.m_quality + 1;
			CraftingStation station = FindStation(recipe.m_craftingStation.gameObject.name.GetStableHashCode());
			if (station == null || station.GetLevel() < recipe.GetRequiredStationLevel(quality))
			{
				return(null);
			}
			ArrowPlan plan = new ArrowPlan { Prefab = item.m_dropPrefab.name, Batch = 1, Station = station, Upgrade = item, Quality = quality };
			List<Container> chests = UsableChests();
			foreach (Piece.Requirement requirement in recipe.m_resources)
			{
				int amount = CraftAmount(requirement, quality);
				if (amount <= 0)
				{
					continue;
				}
				string name = requirement.m_resItem.gameObject.name;
				if (WoodItems.Contains(name))
				{
					plan.Wood += amount;
				}
				else if (CraftStock(name, chests) >= amount)
				{
					plan.Others.Add(new KeyValuePair<string, int>(name, amount));
				}
				else
				{
					return(null);
				}
			}
			return((plan.Wood <= VillageWood()) ? plan : null);
		}

		/// <summary>
		/// Checks whether the piece an upgrade is for is still the viking's, at the quality it was to be raised from - or
		/// for a lining, still the viking's and not yet lined
		/// </summary>
		/// <param name="plan">The upgrade</param>
		/// <returns>True when it is</returns>
		private bool CanStillUpgrade(ArrowPlan plan)
		{
			if (!humanoid.GetInventory().ContainsItem(plan.Upgrade))
			{
				return(false);
			}
			return(plan.Lines ? ZombieLink.LiningRecipe(plan.Upgrade) != null : plan.Upgrade.m_quality == plan.Quality - 1);
		}

		/// <summary>
		/// Raises a piece of the viking's gear to the quality its upgrade is for, at full durability, as an upgrade a
		/// player makes leaves it - or lines a weapon with silver, leaving it as it is otherwise, as lining it at the
		/// forge does a player's
		/// </summary>
		/// <param name="plan">The upgrade</param>
		/// <returns>The piece</returns>
		private ItemDrop.ItemData UpgradeGear(ArrowPlan plan)
		{
			ItemDrop.ItemData item = plan.Upgrade;
			if (plan.Lines)
			{
				ZombieLink.Line(item);
				humanoid.GetInventory().Changed();
				TestLog.Count("Gear silver-lined", TestLog.ItemName(item));
				Plugin.Log.LogInfo(LogName() + " lined its " + Localization.instance.Localize(item.m_shared.m_name) + " with silver");
				return(item);
			}
			item.m_quality = plan.Quality;
			item.m_durability = item.GetMaxDurability();
			item.m_worldLevel = (byte)Game.m_worldLevel;
			humanoid.GetInventory().Changed();
			TestLog.Count("Gear upgraded", TestLog.ItemName(item));
			Plugin.Log.LogInfo(LogName() + " upgraded its " + Localization.instance.Localize(item.m_shared.m_name) + " to quality " + item.m_quality);
			return(item);
		}

		/// <summary>
		/// Upgrading a piece of its own gear by day, one viking of the village at a time, when the village has everything
		/// it takes (see FindUpgradeWork) - getting what it takes besides wood from the village's chests and its wood,
		/// then working at the station its recipe is crafted at, the way it crafts (see ItemRun), and raising the piece a
		/// quality - or lining a weapon with silver at the forge (see FindLiningWork). Not to be mixed up with UpgradeJob,
		/// which takes up better gear from a chest
		/// </summary>
		private class GearUpgradeJob : Job
		{
			private readonly ItemRun upgradeRun;
			private readonly string label;
			private float nextRenew;

			/// <summary>
			/// Makes the job, with the station the piece is upgraded at already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="upgrade">What upgrading the piece takes</param>
			public GearUpgradeJob(VillageLife owner, ArrowPlan upgrade) : base(owner)
			{
				upgradeRun = new ItemRun(owner, null, (ItemDrop.ItemData item) => false, upgrade.Prefab, upgrade);
				string name = Localization.instance.Localize(upgrade.Upgrade.m_shared.m_name);
				label = (upgrade.Lines ? "Lining " + name + " with silver" : "Upgrading " + name + " to quality " + upgrade.Quality);
			}

			/// <summary>
			/// Notes that the viking is upgrading its gear, so no other viking of the village starts to
			/// </summary>
			public override void Begin()
			{
				life.SetDoing(UpgradingKey, true);
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>What it's upgrading</returns>
			public override string Describe()
			{
				return(label);
			}

			/// <summary>
			/// Checks whether the wood the viking is after lies outside the village
			/// </summary>
			/// <returns>True when it does</returns>
			public override bool Outing()
			{
				return(upgradeRun.Outing());
			}

			/// <summary>
			/// Gets what the upgrade takes and works at the station
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once the piece is upgraded or it can't get what it takes, or it's dusk</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk())
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.SetDoing(UpgradingKey, true);
					upgradeRun.Renew();
				}
				return(upgradeRun.Tick(dt, out ItemDrop.ItemData _));
			}

			/// <summary>
			/// Lets go of the station and the wood it was after, carries what it carries for crafting back to a chest, and
			/// notes it's no longer upgrading
			/// </summary>
			public override void End()
			{
				upgradeRun.Release();
				life.SetDoing(UpgradingKey, false);
			}
		}
	}
}
