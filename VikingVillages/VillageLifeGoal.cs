using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The one piece of gear a village is working toward at a time - the next step up for the viking with the weakest
	/// gear - and the next thing the village does toward it (see ResourcePlanner), which is what sends its vikings to
	/// smelt and on quests
	/// </summary>
	/// <remarks>
	/// The client that runs a village picks its goal every GoalInterval seconds and notes it on the village's ZDO, so it
	/// carries on from where it was when another client takes the village over. A goal is kept until no viking would
	/// take it up any more - one of them crafted or upgraded it - or it can't be got, and only the last link of the
	/// chain toward it is worked out, each time it's looked at again, so once that link is done, the next comes up
	/// </remarks>
	internal partial class VillageLife
	{
		/// <summary>
		/// On a village's ZDO, the prefab name of the piece it's working toward, or "", the quality it's to be at - 1 for a
		/// new piece, more for an upgrade - and the next thing it does toward it, as said for the debug display
		/// </summary>
		public static readonly int GoalKey = "kriona.VikingVillages.villageGoal".GetStableHashCode();
		public static readonly int GoalQualityKey = "kriona.VikingVillages.villageGoalQuality".GetStableHashCode();
		public static readonly int GoalStepKey = "kriona.VikingVillages.villageGoalStep".GetStableHashCode();

		/// <summary>
		/// Seconds between the looks at each village's goal this client runs
		/// </summary>
		private const float GoalInterval = 60f;

		/// <summary>
		/// When this client next looks at the goal of each village it runs, by the village's ZDO, in Time.time seconds
		/// </summary>
		private static readonly Dictionary<ZDOID, float> nextGoalLook = new Dictionary<ZDOID, float>();

		/// <summary>
		/// A piece a viking could work toward - the item, the quality it would be at, and what it takes, each item's
		/// prefab name and how many
		/// </summary>
		private class GoalCandidate
		{
			public string Prefab;
			public int Quality;
			public List<KeyValuePair<string, int>> Needs = new List<KeyValuePair<string, int>>();
		}

		/// <summary>
		/// Looks at the goal of each village this client runs, every GoalInterval seconds - call it from the plugin's
		/// Update
		/// </summary>
		public static void UpdateGoals()
		{
			if (ZNet.instance == null || !ServerCheck.Active)
			{
				return;
			}
			ResourceBook book = ResourceBookGame.Get();
			if (book == null)
			{
				return;
			}
			foreach (Village village in Village.Loaded())
			{
				if (village.Zdo == null || !village.IsOwner())
				{
					continue;
				}
				ZDOID id = village.Zdo.m_uid;
				if (nextGoalLook.TryGetValue(id, out float next) && Time.time < next)
				{
					continue;
				}
				nextGoalLook[id] = Time.time + GoalInterval;
				LookAtGoal(village, book);
			}
		}

		/// <summary>
		/// Forgets when each village's goal is next looked at, and the steps worked out toward each, as the world is left
		/// </summary>
		public static void ForgetGoals()
		{
			nextGoalLook.Clear();
			goalSteps.Clear();
		}

		/// <summary>
		/// Gets the next thing a village does toward its goal, worked out from what it has now
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The step, or null when it has no goal or the game hasn't loaded what's needed to plan it</returns>
		public static PlanStep GoalStep(Village village)
		{
			ResourceBook book = ResourceBookGame.Get();
			string prefab = village.Zdo.GetString(GoalKey, "");
			if (book == null || prefab.Length == 0)
			{
				return(null);
			}
			GoalCandidate goal = GoalNeeds(prefab, village.Zdo.GetInt(GoalQualityKey, 1));
			return((goal != null) ? ResourcePlanner.Next(book, HoldingsOf(village), goal.Needs) : null);
		}

		/// <summary>
		/// Keeps a village's goal while some viking would still take it up and it can still be got, or else picks
		/// another, noting it and its next step on the village's ZDO
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		/// <param name="book">Every way to get each item</param>
		private static void LookAtGoal(Village village, ResourceBook book)
		{
			VillageHoldings holdings = HoldingsOf(village);
			List<Character> vikings = village.Vikings();
			string prefab = village.Zdo.GetString(GoalKey, "");
			int quality = village.Zdo.GetInt(GoalQualityKey, 1);
			PlanStep step = null;
			if (prefab.Length > 0 && StillWanted(vikings, prefab, quality))
			{
				GoalCandidate kept = GoalNeeds(prefab, quality);
				step = ((kept != null) ? ResourcePlanner.Next(book, holdings, kept.Needs) : null);
			}
			if (step == null || step.Kind == StepKind.Impossible)
			{
				GoalCandidate picked = PickGoal(vikings, book, holdings, out step);
				prefab = ((picked != null) ? picked.Prefab : "");
				quality = ((picked != null) ? picked.Quality : 1);
			}
			string said = ((step != null) ? step.Describe() : "");
			if (prefab != village.Zdo.GetString(GoalKey, "") || quality != village.Zdo.GetInt(GoalQualityKey, 1))
			{
				Plugin.Log.LogInfo("Goal: the village at " + village.Farm.ToString("F0") + ((prefab.Length > 0) ? " works toward " + prefab + " at quality " + quality + " - " + said : " has nothing to work toward"));
			}
			village.Zdo.Set(GoalKey, prefab);
			village.Zdo.Set(GoalQualityKey, quality);
			village.Zdo.Set(GoalStepKey, said);
		}

		/// <summary>
		/// Checks whether some viking of the village would still take up a goal - a new piece over its own, or for an
		/// upgrade, a piece it has at the quality just below
		/// </summary>
		/// <param name="vikings">The village's vikings</param>
		/// <param name="prefab">The piece's prefab name</param>
		/// <param name="quality">The quality it's to be at</param>
		/// <returns>True when one would</returns>
		private static bool StillWanted(List<Character> vikings, string prefab, int quality)
		{
			ItemDrop.ItemData item = ItemOf(prefab);
			if (item == null)
			{
				return(false);
			}
			foreach (Character character in vikings)
			{
				VikingCore core = ((character != null) ? character.GetComponent<VikingCore>() : null);
				Humanoid humanoid = character as Humanoid;
				if (core == null || humanoid == null)
				{
					continue;
				}
				if (quality <= 1 && core.WouldTakeUp(item))
				{
					return(true);
				}
				if (quality > 1 && humanoid.GetInventory().GetAllItems().Exists((ItemDrop.ItemData own) => own.m_dropPrefab != null && own.m_dropPrefab.name == prefab && own.m_quality == quality - 1))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Picks a village's goal - for the viking with the weakest gear, the first it can get of an upgrade of a piece it
		/// wears, its lowest quality first, then the least of a step up of each kind of gear in the order of
		/// CraftedGearKinds, going on to the next weakest viking when the village can get none of them
		/// </summary>
		/// <param name="vikings">The village's vikings</param>
		/// <param name="book">Every way to get each item</param>
		/// <param name="holdings">What the village has</param>
		/// <param name="step">Set to the next thing the village does toward the goal picked, or null</param>
		/// <returns>The goal, or null when there's nothing the village can get</returns>
		private static GoalCandidate PickGoal(List<Character> vikings, ResourceBook book, VillageHoldings holdings, out PlanStep step)
		{
			List<KeyValuePair<float, Humanoid>> ranked = new List<KeyValuePair<float, Humanoid>>();
			foreach (Character character in vikings)
			{
				VikingCore core = ((character != null) ? character.GetComponent<VikingCore>() : null);
				Humanoid humanoid = character as Humanoid;
				if (core != null && humanoid != null)
				{
					ranked.Add(new KeyValuePair<float, Humanoid>(WornValue(core, humanoid), humanoid));
				}
			}
			ranked.Sort((KeyValuePair<float, Humanoid> a, KeyValuePair<float, Humanoid> b) => a.Key.CompareTo(b.Key));
			foreach (KeyValuePair<float, Humanoid> entry in ranked)
			{
				foreach (GoalCandidate candidate in Candidates(entry.Value))
				{
					PlanStep planned = ResourcePlanner.Next(book, holdings, candidate.Needs);
					if (planned.Kind != StepKind.Impossible)
					{
						step = planned;
						return(candidate);
					}
				}
			}
			step = null;
			return(null);
		}

		/// <summary>
		/// Adds up what a viking's equipped gear is worth to it (see VikingCore.GearValue), which ranks whose gear is
		/// weakest
		/// </summary>
		/// <param name="core">The viking</param>
		/// <param name="humanoid">Its humanoid</param>
		/// <returns>The worth</returns>
		private static float WornValue(VikingCore core, Humanoid humanoid)
		{
			float value = 0f;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (item.m_equipped && CraftedGearKind(item) != null)
				{
					value += core.GearValue(item);
				}
			}
			return(value);
		}

		/// <summary>
		/// Lists what a viking could work toward, in the order it's wanted - upgrades of the pieces it wears, lowest
		/// quality first, then of each kind of gear in the order of CraftedGearKinds, the craftable pieces it would take
		/// up over its own, least worth first
		/// </summary>
		/// <param name="humanoid">The viking</param>
		/// <returns>The candidates</returns>
		private static List<GoalCandidate> Candidates(Humanoid humanoid)
		{
			List<GoalCandidate> candidates = new List<GoalCandidate>();
			VikingCore core = humanoid.GetComponent<VikingCore>();
			List<ItemDrop.ItemData> worn = humanoid.GetInventory().GetAllItems().FindAll((ItemDrop.ItemData item) => item.m_equipped && item.m_dropPrefab != null && CraftedGearKind(item) != null && item.m_quality < item.m_shared.m_maxQuality);
			worn.Sort((ItemDrop.ItemData a, ItemDrop.ItemData b) => a.m_quality.CompareTo(b.m_quality));
			foreach (ItemDrop.ItemData item in worn)
			{
				GoalCandidate upgrade = GoalNeeds(item.m_dropPrefab.name, item.m_quality + 1);
				if (upgrade != null)
				{
					candidates.Add(upgrade);
				}
			}
			if (ObjectDB.instance == null)
			{
				return(candidates);
			}
			foreach (string kind in CraftedGearKinds)
			{
				List<KeyValuePair<float, string>> pieces = new List<KeyValuePair<float, string>>();
				foreach (Recipe recipe in ObjectDB.instance.m_recipes)
				{
					if (!Crafts(recipe))
					{
						continue;
					}
					ItemDrop.ItemData item = ItemOf(recipe.m_item);
					if (item != null && CraftedGearKind(item) == kind && core.WouldTakeUp(item))
					{
						pieces.Add(new KeyValuePair<float, string>(core.GearValue(item), item.m_dropPrefab.name));
					}
				}
				pieces.Sort((KeyValuePair<float, string> a, KeyValuePair<float, string> b) => a.Key.CompareTo(b.Key));
				foreach (KeyValuePair<float, string> piece in pieces)
				{
					GoalCandidate candidate = GoalNeeds(piece.Value, 1);
					if (candidate != null)
					{
						candidates.Add(candidate);
					}
				}
			}
			return(candidates);
		}

		/// <summary>
		/// Works out what a piece at a quality takes, from the game's recipe for it - what crafting it new or upgrading it
		/// to that quality takes (see CraftAmount), and its station, at the level that quality needs, which the planner
		/// counts as one more thing the village needs
		/// </summary>
		/// <param name="prefab">The piece's prefab name</param>
		/// <param name="quality">The quality - 1 for a new piece</param>
		/// <returns>The candidate, or null when the game has no recipe for it</returns>
		private static GoalCandidate GoalNeeds(string prefab, int quality)
		{
			Recipe recipe = ((ObjectDB.instance != null) ? ObjectDB.instance.m_recipes.Find((Recipe candidate) => Crafts(candidate) && candidate.m_item.gameObject.name == prefab) : null);
			if (recipe == null)
			{
				return(null);
			}
			GoalCandidate goal = new GoalCandidate { Prefab = prefab, Quality = quality };
			if (recipe.m_craftingStation != null)
			{
				goal.Needs.Add(new KeyValuePair<string, int>(StationNeed(recipe.m_craftingStation.gameObject.name, recipe.GetRequiredStationLevel(quality)), 1));
			}
			foreach (Piece.Requirement requirement in recipe.m_resources)
			{
				int amount = CraftAmount(requirement, quality);
				if (amount > 0)
				{
					goal.Needs.Add(new KeyValuePair<string, int>(requirement.m_resItem.gameObject.name, amount));
				}
			}
			return(goal);
		}

		/// <summary>
		/// Names a station at a level as an item the plan needs one of - one the village has when its station is
		/// upgraded far enough (see HoldingsOf), and can never get otherwise, since the vikings don't build stations
		/// </summary>
		/// <param name="prefab">The station's prefab name</param>
		/// <param name="level">The level it has to be at</param>
		/// <returns>The name, like "station:forge:2"</returns>
		internal static string StationNeed(string prefab, int level)
		{
			return("station:" + prefab + ":" + level);
		}

		/// <summary>
		/// Works out what a village has to work with - the items in its chests, carried by its vikings and lying in the
		/// village, the ore in its smelters as the bars it will be, its stations at their levels, as stations and as one
		/// of each StationNeed they meet, its biome, its beehives and its coins counting what its treasure sells for
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>What it has</returns>
		public static VillageHoldings HoldingsOf(Village village)
		{
			VillageHoldings holdings = new VillageHoldings();
			foreach (Container chest in VillageChests(village))
			{
				foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
				{
					if (item.m_dropPrefab != null && !VikingItems.IsCarried(item))
					{
						holdings.Put(item.m_dropPrefab.name, item.m_stack);
					}
				}
			}
			foreach (Character character in village.Vikings())
			{
				Humanoid humanoid = character as Humanoid;
				if (humanoid == null)
				{
					continue;
				}
				foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
				{
					if (item.m_dropPrefab != null && !VikingItems.IsProp(item))
					{
						holdings.Put(item.m_dropPrefab.name, item.m_stack);
					}
				}
			}
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				if (instance.Value == null || !village.OwnsPiece(instance.Value.transform.position))
				{
					continue;
				}
				string name = Utils.GetPrefabName(instance.Value.gameObject);
				CraftingStation station = instance.Value.GetComponent<CraftingStation>();
				Smelter smelter = instance.Value.GetComponent<Smelter>();
				ItemDrop drop = instance.Value.GetComponent<ItemDrop>();
				if (station != null)
				{
					holdings.AddStation(name, station.GetLevel());
				}
				else if (smelter != null)
				{
					holdings.AddStation(name, 1);
					AddSmelting(holdings, smelter, instance.Key);
				}
				else if (drop != null && drop.m_itemData.m_dropPrefab != null && village.Contains(drop.transform.position))
				{
					holdings.Put(drop.m_itemData.m_dropPrefab.name, drop.m_itemData.m_stack);
				}
			}
			foreach (KeyValuePair<string, int> station in holdings.Stations)
			{
				for (int level = 1; level <= station.Value; level++)
				{
					holdings.Put(StationNeed(station.Key, level), 1);
				}
			}
			holdings.Biome = VillageCooking.VillageBiome(village);
			holdings.Hives = VillageHives.Count(village);
			holdings.Coins = holdings.Count(ResourceBook.Coins);
			foreach (string treasure in ResourceBook.Treasure)
			{
				ItemDrop.ItemData item = ItemOf(treasure);
				if (item != null)
				{
					holdings.Coins += holdings.Count(treasure) * item.m_shared.m_value;
				}
			}
			return(holdings);
		}

		/// <summary>
		/// Counts what a smelter is working on as the bars it will be - the ore it holds and the bars it has made but not
		/// dropped yet
		/// </summary>
		/// <param name="holdings">What the village has, added to</param>
		/// <param name="smelter">The smelter</param>
		/// <param name="zdo">Its ZDO</param>
		private static void AddSmelting(VillageHoldings holdings, Smelter smelter, ZDO zdo)
		{
			int queued = zdo.GetInt(ZDOVars.s_queued);
			for (int i = 0; i < queued; i++)
			{
				AddSmelted(holdings, smelter, zdo.GetString("item" + i), 1);
			}
			AddSmelted(holdings, smelter, zdo.GetString(ZDOVars.s_spawnOre), zdo.GetInt(ZDOVars.s_spawnAmount));
		}

		/// <summary>
		/// Counts ore in a smelter as what it's smelted into
		/// </summary>
		/// <param name="holdings">What the village has, added to</param>
		/// <param name="smelter">The smelter</param>
		/// <param name="ore">The ore's prefab name, or ""</param>
		/// <param name="count">How much of it</param>
		private static void AddSmelted(VillageHoldings holdings, Smelter smelter, string ore, int count)
		{
			if (string.IsNullOrEmpty(ore) || count <= 0)
			{
				return;
			}
			Smelter.ItemConversion conversion = smelter.GetItemConversion(ore);
			if (conversion != null && conversion.m_to != null)
			{
				holdings.Put(conversion.m_to.gameObject.name, count);
			}
		}
	}
}
