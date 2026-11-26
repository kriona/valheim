using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Crafting - planning what a viking crafts from the game's recipes, getting what it takes from the village's
	/// chests or gathering it, and the station it crafts at
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Finds the job that gets something the viking is short of to craft with - foraging for it when it's picked,
		/// like flint or stone, or else hunting only the prey or birds that drop it, like greylings for resin, boars for
		/// leather or birds for feathers - from the Plains on, as a party for prey that isn't only for archers (see
		/// HuntParty) - or with nothing wild about that drops it, slaughtering one of the village's livestock that does -
		/// one viking at a time for each, and none while it's badly hurt
		/// </summary>
		/// <remarks>
		/// What drops what comes from the game's own drop tables (see Drops), so a recipe's ingredients are gathered
		/// wherever the game has them come from
		/// </remarks>
		/// <param name="item">The ingredient's prefab name, or null</param>
		/// <returns>The job, or null when there's nothing to gather it from</returns>
		private Job GatherFor(string item)
		{
			if (string.IsNullOrEmpty(item) || Wounded())
			{
				return(null);
			}
			if (!OtherDoing(ForagingKey) && FindForage(item) != null)
			{
				return(new ForageJob(this, item));
			}
			bool archer = HasBow(humanoid);
			if (OtherDoing(HuntingKey))
			{
				return(null);
			}
			if (FindPrey(archer, item, false) != null || (archer && FindBird(item) != null))
			{
				return(new HuntJob(this, archer, item, false));
			}
			// From the Plains on, prey that isn't only for archers is hunted by a party (see HuntParty)
			if (CanLeadParty(item))
			{
				Job led = LeadParty(item);
				if (led != null)
				{
					return(led);
				}
			}
			// With nothing wild about that drops it, it slaughters one of the village's livestock, which its pen brings
			// back (see VikingFarmRespawnPatch)
			if (FindPrey(archer, item, true) != null)
			{
				return(new HuntJob(this, archer, item, true));
			}
			return(null);
		}

		/// <summary>
		/// Checks whether a creature or bird drops something when it's killed, from the game's drop tables for its prefab,
		/// remembering what each prefab drops
		/// </summary>
		/// <param name="creature">The creature or bird</param>
		/// <param name="item">The item's prefab name</param>
		/// <returns>True when it drops it</returns>
		private static bool Drops(GameObject creature, string item)
		{
			string name = Utils.GetPrefabName(creature);
			if (!dropsByPrefab.TryGetValue(name, out HashSet<string> drops))
			{
				drops = new HashSet<string>();
				GameObject prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(name) : null);
				CharacterDrop characterDrop = ((prefab != null) ? prefab.GetComponent<CharacterDrop>() : null);
				if (characterDrop != null)
				{
					foreach (CharacterDrop.Drop drop in characterDrop.m_drops)
					{
						if (drop != null && drop.m_prefab != null)
						{
							drops.Add(drop.m_prefab.name);
						}
					}
				}
				DropOnDestroyed destroyed = ((prefab != null) ? prefab.GetComponent<DropOnDestroyed>() : null);
				if (destroyed != null && destroyed.m_dropWhenDestroyed != null)
				{
					foreach (DropTable.DropData drop in destroyed.m_dropWhenDestroyed.m_drops)
					{
						if (drop.m_item != null)
						{
							drops.Add(drop.m_item.name);
						}
					}
				}
				dropsByPrefab[name] = drops;
			}
			return(drops.Contains(item));
		}

		/// <summary>
		/// Works out what an item the viking crafts for itself takes, like its torch or a hoe, from the game's recipe for
		/// it - the wood, everything else, which has to be carried for crafting or in the village's chests, and the
		/// station it's crafted at, if any, which the village has to have at the recipe's level
		/// </summary>
		/// <param name="prefab">The item's prefab name</param>
		/// <param name="missing">Set to the first ingredient the village is short of, or null</param>
		/// <returns>The item, or null when the game has no recipe for it, the village has no station for it, or it's
		/// short of an ingredient</returns>
		private ArrowPlan PlanCraft(string prefab, out string missing)
		{
			missing = null;
			Recipe recipe = ((ObjectDB.instance != null) ? ObjectDB.instance.m_recipes.Find((Recipe candidate) => Crafts(candidate) && candidate.m_item.gameObject.name == prefab) : null);
			if (recipe == null)
			{
				return(null);
			}
			CraftingStation station = null;
			if (recipe.m_craftingStation != null)
			{
				station = FindStation(recipe.m_craftingStation.gameObject.name.GetStableHashCode());
				if (station == null || station.GetLevel() < recipe.m_minStationLevel)
				{
					return(null);
				}
			}
			ArrowPlan plan = new ArrowPlan { Prefab = prefab, Batch = Mathf.Max(1, recipe.m_amount), Station = station };
			List<Container> chests = UsableChests();
			foreach (Piece.Requirement requirement in recipe.m_resources)
			{
				int amount = CraftAmount(requirement, 1);
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
					missing = name;
					return(null);
				}
			}
			return(plan);
		}

		/// <summary>
		/// Checks whether a recipe crafts its item, rather than only upgrading one a player already has
		/// </summary>
		/// <param name="recipe">The recipe, or null</param>
		/// <returns>True when it does</returns>
		private static bool Crafts(Recipe recipe)
		{
			return(recipe != null && recipe.m_enabled && recipe.m_item != null && !recipe.m_noCraftOnlyUpgrade);
		}

		/// <summary>
		/// Gets how much of an ingredient crafting or upgrading an item at a workbench or forge takes - none of one only
		/// an upgrader station takes, which the game leaves out at any other station, the way the vikings craft
		/// </summary>
		/// <param name="requirement">The ingredient, or null</param>
		/// <param name="quality">The quality crafted, 1 for a new item</param>
		/// <returns>How much it takes, or 0</returns>
		private static int CraftAmount(Piece.Requirement requirement, int quality)
		{
			if (requirement == null || requirement.m_resItem == null || requirement.m_upgraderResource)
			{
				return(0);
			}
			return(requirement.GetAmount(quality));
		}

		/// <summary>
		/// Arrows, a torch or a hoe a viking can craft, or a piece of its gear it can upgrade - what it crafts, how many a
		/// craft makes, the wood it takes, what else it takes and how much, the station it's crafted at, or null for one
		/// crafted anywhere, how much an arrow deals, whether they're silver arrows (see FindSilverArrowWork), and for an
		/// upgrade, the piece and the quality it's raised to, or for a weapon lined with silver (see FindLiningWork), the
		/// weapon at its own quality
		/// </summary>
		private class ArrowPlan
		{
			public string Prefab;
			public int Batch;
			public int Wood;
			public readonly List<KeyValuePair<string, int>> Others = new List<KeyValuePair<string, int>>();
			public CraftingStation Station;
			public float Damage;
			public bool Silver;
			public ItemDrop.ItemData Upgrade;
			public int Quality;
			public bool Lines;
		}

		/// <summary>
		/// Picks a material the vikings build or craft with that's in the village's chests, at random, for a talk (see
		/// VikingSpeech.IsMaterial)
		/// </summary>
		/// <returns>Its name, in lowercase, or "" when the chests hold none</returns>
		private string VillageMaterial()
		{
			List<string> names = new List<string>();
			if (village != null)
			{
				foreach (Container chest in UsableChests())
				{
					foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
					{
						if (VikingSpeech.IsMaterial(item))
						{
							string name = VikingSpeech.MaterialName(item);
							if (!names.Contains(name))
							{
								names.Add(name);
							}
						}
					}
				}
			}
			return((names.Count > 0) ? names[Random.Range(0, names.Count)] : "");
		}

		/// <summary>
		/// Counts how many of an ingredient the viking carries for crafting and the village's chests hold
		/// </summary>
		/// <param name="name">The ingredient's prefab name</param>
		/// <param name="chests">The village's chests the viking may use</param>
		/// <returns>How many</returns>
		private int CraftStock(string name, List<Container> chests)
		{
			int count = CarriedCraft(name);
			foreach (Container chest in chests)
			{
				foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
				{
					if (item.m_dropPrefab != null && item.m_dropPrefab.name == name && !VikingItems.IsCarried(item))
					{
						count += item.m_stack;
					}
				}
			}
			return(count);
		}

		/// <summary>
		/// Counts how many of an ingredient the viking carries for crafting
		/// </summary>
		/// <param name="name">The ingredient's prefab name</param>
		/// <returns>How many</returns>
		private int CarriedCraft(string name)
		{
			int count = 0;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (IsCarriedFor(item, CarryCraft) && item.m_dropPrefab != null && item.m_dropPrefab.name == name)
				{
					count += item.m_stack;
				}
			}
			return(count);
		}

		/// <summary>
		/// Finds an ingredient other than wood the viking still needs for a batch of arrows
		/// </summary>
		/// <param name="plan">The arrows</param>
		/// <param name="name">The ingredient's prefab name</param>
		/// <param name="needed">How many more it needs</param>
		/// <returns>False when it carries everything</returns>
		private bool MissingCraft(ArrowPlan plan, out string name, out int needed)
		{
			foreach (KeyValuePair<string, int> other in plan.Others)
			{
				int shortBy = other.Value - CarriedCraft(other.Key);
				if (shortBy > 0)
				{
					name = other.Key;
					needed = shortBy;
					return(true);
				}
			}
			name = null;
			needed = 0;
			return(false);
		}

		/// <summary>
		/// Finds the nearest of the village's chests holding an ingredient
		/// </summary>
		/// <param name="name">The ingredient's prefab name</param>
		/// <returns>The chest, or null when none holds any</returns>
		private Container ChestWithCraft(string name)
		{
			return(Nearest(UsableChests().FindAll((Container chest) => !Avoided(chest.m_nview.GetZDO().m_uid) && chest.GetInventory().GetAllItems().Exists((ItemDrop.ItemData item) => item.m_dropPrefab != null && item.m_dropPrefab.name == name && !VikingItems.IsCarried(item)))));
		}

		/// <summary>
		/// Takes an ingredient out of a chest the viking has reached, as many as it needs and can carry, to carry for
		/// crafting
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="name">The ingredient's prefab name</param>
		/// <param name="wanted">How many it needs</param>
		/// <returns>How many it took</returns>
		private int TakeCraft(Container chest, string name, int wanted)
		{
			if (wanted <= 0 || !OpenChest(chest))
			{
				return(0);
			}
			Inventory store = chest.GetInventory();
			int taken = 0;
			foreach (ItemDrop.ItemData item in store.m_inventory.FindAll((ItemDrop.ItemData stored) => stored.m_dropPrefab != null && stored.m_dropPrefab.name == name && !VikingItems.IsCarried(stored)))
			{
				if (taken >= wanted)
				{
					break;
				}
				int took = Carry(item, Mathf.Min(wanted - taken, item.m_stack), CarryCraft);
				if (took <= 0)
				{
					break;
				}
				store.RemoveItem(item, took);
				taken += took;
			}
			return(taken);
		}

		/// <summary>
		/// Uses up the ingredients other than wood a batch of arrows takes from what the viking carries for crafting
		/// </summary>
		/// <param name="plan">The arrows</param>
		/// <returns>False when it didn't carry them all, and nothing was used</returns>
		private bool UseCraft(ArrowPlan plan)
		{
			if (MissingCraft(plan, out string _, out int _))
			{
				return(false);
			}
			Inventory inventory = humanoid.GetInventory();
			foreach (KeyValuePair<string, int> other in plan.Others)
			{
				int left = other.Value;
				foreach (ItemDrop.ItemData item in inventory.m_inventory.FindAll((ItemDrop.ItemData carried) => IsCarriedFor(carried, CarryCraft) && carried.m_dropPrefab != null && carried.m_dropPrefab.name == other.Key))
				{
					if (left <= 0)
					{
						break;
					}
					int used = Mathf.Min(left, item.m_stack);
					inventory.RemoveItem(item, used);
					left -= used;
				}
			}
			return(true);
		}

		/// <summary>
		/// Has the viking carry what it carries for crafting back to a chest as loot, once it's done crafting
		/// </summary>
		private void ReturnCraft()
		{
			Inventory inventory = humanoid.GetInventory();
			bool changed = false;
			foreach (ItemDrop.ItemData item in inventory.m_inventory.FindAll((ItemDrop.ItemData carried) => IsCarriedFor(carried, CarryCraft)))
			{
				item.m_customData[VikingItems.CarryKey] = CarryLoot;
				LogCraftLeftover(item);
				changed = true;
			}
			if (changed)
			{
				inventory.Changed();
			}
		}

		/// <summary>
		/// Finds the nearest crafting station of a kind in the village that a viking can use (see StationUsable)
		/// </summary>
		/// <param name="prefab">The station's prefab hash</param>
		/// <returns>The station, or null when the village has none loaded it can use</returns>
		private CraftingStation FindStation(int prefab)
		{
			CraftingStation nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				if (instance.Key.GetPrefab() != prefab || instance.Value == null || !village.OwnsPiece(instance.Value.transform.position))
				{
					continue;
				}
				CraftingStation bench = instance.Value.GetComponent<CraftingStation>();
				float distance = Vector3.Distance(instance.Value.transform.position, transform.position);
				if (bench != null && distance < nearestDistance && StationUsable(bench, instance.Key.m_uid))
				{
					nearest = bench;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Checks whether a viking can use a crafting station, the same as a player - under a roof and covered enough
		/// when the station needs one, like a workbench or forge, and with a fire going when it needs one - remembering
		/// the answer for StationCheckTime, since the roof check casts many rays
		/// </summary>
		/// <param name="station">The station</param>
		/// <param name="id">Its ZDO</param>
		/// <returns>True when it can</returns>
		private static bool StationUsable(CraftingStation station, ZDOID id)
		{
			if (stationChecks.TryGetValue(id, out KeyValuePair<float, bool> known) && Time.time < known.Key)
			{
				return(known.Value);
			}
			if (stationChecks.Count > StationChecksMax)
			{
				stationChecks.Clear();
			}
			bool usable = true;
			if (station.m_craftRequireRoof && station.m_roofCheckPoint != null)
			{
				Cover.GetCoverForPoint(station.m_roofCheckPoint.position, out float cover, out bool underRoof);
				usable = (underRoof && cover >= StationMinCover);
			}
			if (station.m_craftRequireFire && !station.m_haveFire)
			{
				usable = false;
			}
			stationChecks[id] = new KeyValuePair<float, bool>(Time.time + StationCheckTime, usable);
			return(usable);
		}

		/// <summary>
		/// Sets the viking's crafting animation - the station's, or 0 to stop
		/// </summary>
		/// <param name="animation">The animation</param>
		private void SetCrafting(int animation)
		{
			if (humanoid != null && humanoid.m_zanim != null)
			{
				humanoid.m_zanim.SetInt(VikingPoses.CraftingInt, animation);
			}
		}
	}
}
