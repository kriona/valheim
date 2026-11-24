using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Fills a village's resource book (see ResourceBook) from the game once its items and prefabs are loaded - every
	/// recipe the vikings can craft from, every smelter's conversions, the pickaxes and axes with their tiers, the tool
	/// tier each rock and tree a quest goes for needs, and what Haldor asks for a Dverger circlet
	/// </summary>
	internal static class ResourceBookGame
	{
		/// <summary>
		/// The book made for the world loaded, or null until it's made
		/// </summary>
		private static ResourceBook book;

		/// <summary>
		/// Gets the book, making it the first time once the game has loaded its items and prefabs
		/// </summary>
		/// <returns>The book, or null while the game hasn't loaded them</returns>
		public static ResourceBook Get()
		{
			if (book != null)
			{
				return(book);
			}
			if (ObjectDB.instance == null || ObjectDB.instance.m_recipes.Count == 0 || ZNetScene.instance == null)
			{
				return(null);
			}
			book = Make();
			return(book);
		}

		/// <summary>
		/// Forgets the book, as the world is left, so the next is made from what that world loads
		/// </summary>
		public static void Forget()
		{
			book = null;
		}

		/// <summary>
		/// Makes the book - what the vikings gather, then the game's recipes and smelters, then the quests - so an item
		/// that can be gathered, crafted or smelted is gone on a quest for only once those can't get it
		/// </summary>
		/// <returns>The book</returns>
		private static ResourceBook Make()
		{
			ResourceBook made = new ResourceBook();
			made.AddGathered();
			AddRecipes(made);
			AddSmelters(made);
			made.AddQuests();
			AddTools(made);
			ReadToolTiers(made);
			ReadCircletPrice(made);
			Plugin.Log.LogInfo("Resource book: " + made.Sources.Count + " items, " + made.Tools.Count + " tools");
			return(made);
		}

		/// <summary>
		/// Adds every recipe that crafts its item - what it takes at its first quality, leaving out what only an upgrader
		/// station takes, the way the vikings craft (see VillageLife.CraftAmount)
		/// </summary>
		/// <param name="made">The book</param>
		private static void AddRecipes(ResourceBook made)
		{
			foreach (Recipe recipe in ObjectDB.instance.m_recipes)
			{
				if (recipe == null || !recipe.m_enabled || recipe.m_item == null || recipe.m_noCraftOnlyUpgrade)
				{
					continue;
				}
				ResourceSource source = new ResourceSource { Kind = SourceKind.Craft, Amount = Mathf.Max(1, recipe.m_amount) };
				if (recipe.m_craftingStation != null)
				{
					source.Station = recipe.m_craftingStation.gameObject.name;
					source.StationLevel = recipe.m_minStationLevel;
				}
				foreach (Piece.Requirement requirement in recipe.m_resources)
				{
					if (requirement == null || requirement.m_resItem == null || requirement.m_upgraderResource)
					{
						continue;
					}
					int amount = requirement.GetAmount(1);
					if (amount > 0)
					{
						source.Ingredients.Add(new System.Collections.Generic.KeyValuePair<string, int>(requirement.m_resItem.gameObject.name, amount));
					}
				}
				made.Add(recipe.m_item.gameObject.name, source);
			}
		}

		/// <summary>
		/// Adds each conversion of every smelter the game has - the smelter, the blast furnace, the charcoal kiln, the
		/// spinning wheel and the like - with the fuel it burns, if any
		/// </summary>
		/// <param name="made">The book</param>
		private static void AddSmelters(ResourceBook made)
		{
			foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
			{
				Smelter smelter = ((prefab != null) ? prefab.GetComponent<Smelter>() : null);
				if (smelter == null)
				{
					continue;
				}
				bool fueled = (smelter.m_fuelItem != null && smelter.m_maxFuel > 0);
				foreach (Smelter.ItemConversion conversion in smelter.m_conversion)
				{
					if (conversion == null || conversion.m_from == null || conversion.m_to == null)
					{
						continue;
					}
					ResourceSource source = new ResourceSource { Kind = SourceKind.Smelt, Station = prefab.name, StationLevel = 1, From = conversion.m_from.gameObject.name };
					if (fueled)
					{
						source.Fuel = smelter.m_fuelItem.gameObject.name;
						source.FuelPer = Mathf.Max(1, smelter.m_fuelPerProduct);
					}
					made.Add(conversion.m_to.gameObject.name, source);
				}
			}
		}

		/// <summary>
		/// Adds every pickaxe and axe the game has, with its tool tier
		/// </summary>
		/// <param name="made">The book</param>
		private static void AddTools(ResourceBook made)
		{
			foreach (GameObject prefab in ObjectDB.instance.m_items)
			{
				ItemDrop drop = ((prefab != null) ? prefab.GetComponent<ItemDrop>() : null);
				if (drop == null)
				{
					continue;
				}
				ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
				if (shared.m_skillType == Skills.SkillType.Pickaxes && shared.m_damages.m_pickaxe > 0f)
				{
					made.AddTool(prefab.name, ToolKind.Pickaxe, shared.m_toolTier);
				}
				else if (shared.m_skillType == Skills.SkillType.Axes && shared.m_damages.m_chop > 0f)
				{
					made.AddTool(prefab.name, ToolKind.Axe, shared.m_toolTier);
				}
			}
		}

		/// <summary>
		/// Reads the tool tier each quest's rock or tree needs from its prefab, leaving 0 for one the game doesn't have
		/// </summary>
		/// <param name="made">The book</param>
		private static void ReadToolTiers(ResourceBook made)
		{
			foreach (System.Collections.Generic.List<ResourceSource> sources in made.Sources.Values)
			{
				foreach (ResourceSource source in sources)
				{
					if (source.ToolTarget == null)
					{
						continue;
					}
					GameObject prefab = ZNetScene.instance.GetPrefab(source.ToolTarget);
					if (prefab == null)
					{
						Plugin.Log.LogWarning("Resource book: no prefab " + source.ToolTarget + " to read a tool tier from");
						continue;
					}
					source.ToolTier = MinToolTier(prefab);
				}
			}
		}

		/// <summary>
		/// Gets the lowest tool tier that can break a rock or cut a tree, from whichever of the game's components it has
		/// </summary>
		/// <param name="prefab">The rock's or tree's prefab</param>
		/// <returns>The tier, or 0 when it has none of them</returns>
		internal static int MinToolTier(GameObject prefab)
		{
			MineRock5 rock5 = prefab.GetComponent<MineRock5>();
			if (rock5 != null)
			{
				return(rock5.m_minToolTier);
			}
			MineRock rock = prefab.GetComponent<MineRock>();
			if (rock != null)
			{
				return(rock.m_minToolTier);
			}
			TreeBase tree = prefab.GetComponent<TreeBase>();
			if (tree != null)
			{
				return(tree.m_minToolTier);
			}
			TreeLog log = prefab.GetComponent<TreeLog>();
			if (log != null)
			{
				return(log.m_minToolTier);
			}
			Destructible destructible = prefab.GetComponent<Destructible>();
			return((destructible != null) ? destructible.m_minToolTier : 0);
		}

		/// <summary>
		/// Reads what the trader who sells the Dverger circlet - Haldor - asks for one into the quest to buy one
		/// </summary>
		/// <param name="made">The book</param>
		private static void ReadCircletPrice(ResourceBook made)
		{
			QuestRule rule = made.RuleOf(QuestKind.BuyCirclet);
			if (rule == null)
			{
				return;
			}
			foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
			{
				Trader trader = ((prefab != null) ? prefab.GetComponent<Trader>() : null);
				Trader.TradeItem item = ((trader != null) ? trader.m_items.Find((Trader.TradeItem candidate) => candidate.m_prefab != null && candidate.m_prefab.gameObject.name == ResourceBook.Circlet) : null);
				if (item != null)
				{
					rule.Coins = item.m_price;
					return;
				}
			}
			Plugin.Log.LogWarning("Resource book: no trader sells " + ResourceBook.Circlet);
		}
	}
}
