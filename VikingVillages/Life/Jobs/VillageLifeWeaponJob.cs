using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Crafting the weapon a viking is missing of the two every viking carries (see WeaponJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// The melee weapons a viking with only a bow crafts to fight up close with, in the order it prefers them - a flint axe while
		/// the village has any flint, a stone axe while there's stone to be had, or else a club, which takes only wood
		/// </summary>
		private const string FlintAxePrefab = "AxeFlint";
		private const string StoneAxePrefab = "AxeStone";
		private const string ClubPrefab = "Club";
		private const string FlintPrefab = "Flint";

		/// <summary>
		/// Finds the weapon the viking is missing of the two every viking carries, for it to craft - for one with no bow,
		/// a crude bow, once the village has the wood for it and a first batch of arrows, which its quiver then fills
		/// with (see FindArrowWork); for one with a bow and no melee weapon, a flint axe, stone axe or club (see
		/// FlintAxePrefab) - or else the job that gets what the village is short of for it, like a hunt for leather or
		/// foraging for flint (see GatherFor). One viking arms itself at a time
		/// </summary>
		/// <returns>The job, or null when it has both, or can't get what it's missing</returns>
		private Job FindSidearmWork()
		{
			if (viking == null || OtherDoing(ArmingKey))
			{
				return(null);
			}
			if (BowAmmo() != null)
			{
				if (viking.MeleeDamage() > 0f)
				{
					return(null);
				}
				List<Container> chests = UsableChests();
				List<string> picks = new List<string>();
				if (CraftStock(FlintPrefab, chests) > 0)
				{
					picks.Add(FlintAxePrefab);
				}
				if (CraftStock(StonePrefab, chests) > 0 || FindForage(StonePrefab) != null)
				{
					picks.Add(StoneAxePrefab);
				}
				picks.Add(ClubPrefab);
				foreach (string pick in picks)
				{
					Job job = CraftWeaponWork(pick, "Crafting a melee weapon", false);
					if (job != null)
					{
						return(job);
					}
				}
				return(null);
			}
			ArrowRecipe(out int _, out int arrowWood);
			if (VillageWood() < WoodToCraft(CrudeBowPrefab) + arrowWood)
			{
				return(null);
			}
			return(CraftWeaponWork(CrudeBowPrefab, "Crafting a bow", false));
		}

		/// <summary>
		/// Finds the job crafting a weapon the viking keeps to fall back on, or puts away in a chest - or the job that
		/// gets what the village is short of for it (see GatherFor)
		/// </summary>
		/// <param name="prefab">The weapon's prefab name</param>
		/// <param name="label">What the job shows as in the debug hover text</param>
		/// <param name="store">True to put it away in a chest for the village, rather than keep it</param>
		/// <returns>The job, or null when it can't get what the weapon takes</returns>
		private Job CraftWeaponWork(string prefab, string label, bool store)
		{
			ArrowPlan plan = PlanCraft(prefab, out string missing);
			if (plan == null)
			{
				return(GatherFor(missing));
			}
			bool woodLeft = (Time.time >= noWoodUntil || CarriedWood() >= plan.Wood || FindWoodChest() != null);
			if (!woodLeft || (plan.Station != null && !ClaimObject(plan.Station.GetComponent<ZNetView>())))
			{
				return(null);
			}
			return(new WeaponJob(this, plan, prefab, label, store));
		}

		/// <summary>
		/// Counts the wood the viking carries and the village's chests hold, of every kind
		/// </summary>
		/// <returns>The wood</returns>
		private int VillageWood()
		{
			int wood = CarriedWood();
			foreach (Container chest in UsableChests())
			{
				wood += ChestWood(chest);
			}
			return(wood);
		}

		/// <summary>
		/// Reads the wood the game's recipe for an item takes
		/// </summary>
		/// <param name="prefab">The item's prefab name</param>
		/// <returns>The wood, or 0 when the game has no recipe for it</returns>
		private static int WoodToCraft(string prefab)
		{
			Recipe recipe = ((ObjectDB.instance != null) ? ObjectDB.instance.m_recipes.Find((Recipe candidate) => Crafts(candidate) && candidate.m_item.gameObject.name == prefab) : null);
			int wood = 0;
			if (recipe != null)
			{
				foreach (Piece.Requirement requirement in recipe.m_resources)
				{
					if (requirement != null && requirement.m_resItem != null && WoodItems.Contains(requirement.m_resItem.gameObject.name))
					{
						wood += CraftAmount(requirement, 1);
					}
				}
			}
			return(wood);
		}

		/// <summary>
		/// Arming itself or the village by day, one viking of the village at a time - crafting the weapon it's missing of
		/// the two every viking carries, a crude bow or a melee weapon (see FindSidearmWork), at the station its recipe
		/// needs, then keeping it with the other to pick between as each fight calls for (see VikingCore.ChooseWeapon);
		/// crafting a tool for the village's chests (see FindToolStockWork) or what its goal takes (see FindGoalWork), which
		/// it then puts away; or crafting a weapon,
		/// armor or a shield better than its own from what the village has (see FindGearCraftWork), which it then takes
		/// up the way it takes up gear it finds
		/// </summary>
		private class WeaponJob : Job
		{
			private readonly ItemRun weaponRun;
			private readonly string label;
			private readonly bool store;
			private readonly bool takeUp;
			private float nextRenew;

			/// <summary>
			/// Makes the job, with the station the weapon is crafted at already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="weapon">What the weapon takes to craft</param>
			/// <param name="prefab">The weapon it crafts</param>
			/// <param name="name">What the job shows as in the debug hover text</param>
			/// <param name="forChest">True to put the weapon away in a chest for the village, rather than keep it</param>
			/// <param name="wear">True to take it up as gear better than its own, carrying what it replaces to a chest</param>
			public WeaponJob(VillageLife owner, ArrowPlan weapon, string prefab, string name, bool forChest, bool wear = false) : base(owner)
			{
				weaponRun = new ItemRun(owner, null, (ItemDrop.ItemData item) => false, prefab, weapon);
				label = name;
				store = forChest;
				takeUp = wear;
			}

			/// <summary>
			/// Notes that the viking is arming itself, so no other viking of the village starts to
			/// </summary>
			public override void Begin()
			{
				life.SetDoing(ArmingKey, true);
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name</returns>
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
				return(weaponRun.Outing());
			}

			/// <summary>
			/// Crafts the weapon, and carries one for the village's chests to put away
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has the weapon or can't get one, or it's dusk</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk())
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.SetDoing(ArmingKey, true);
					weaponRun.Renew();
				}
				if (!weaponRun.Tick(dt, out ItemDrop.ItemData weapon))
				{
					return(false);
				}
				if ((store || takeUp) && weapon != null)
				{
					weapon.m_customData[VikingItems.CarryKey] = CarryLoot;
					life.humanoid.GetInventory().Changed();
				}
				if (takeUp && weapon != null)
				{
					life.TakeUpGear();
				}
				return(true);
			}

			/// <summary>
			/// Lets go of the station and the wood it was after, carries what it carries for crafting back to a chest, and
			/// notes it's no longer arming itself
			/// </summary>
			public override void End()
			{
				weaponRun.Release();
				life.SetDoing(ArmingKey, false);
			}
		}
	}
}
