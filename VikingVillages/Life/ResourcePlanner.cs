using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// What a village has to work with - the items in its chests and carried by its vikings, by prefab name, its
	/// stations and their levels, by prefab name, its biome by VikingGear's order, its beehives, and its coins counting
	/// what its treasure would sell for
	/// </summary>
	internal class VillageHoldings
	{
		public readonly Dictionary<string, int> Stock = new Dictionary<string, int>();
		public readonly Dictionary<string, int> Stations = new Dictionary<string, int>();
		public int Biome;
		public int Hives;
		public int Coins;

		/// <summary>
		/// Gets how many of an item the village has
		/// </summary>
		/// <param name="item">The item's prefab name</param>
		/// <returns>How many</returns>
		public int Count(string item)
		{
			return(Stock.TryGetValue(item, out int count) ? count : 0);
		}

		/// <summary>
		/// Adds to how many of an item the village has
		/// </summary>
		/// <param name="item">The item's prefab name</param>
		/// <param name="count">How many more</param>
		public void Put(string item, int count)
		{
			Stock[item] = Count(item) + count;
		}

		/// <summary>
		/// Notes a station the village has, keeping the highest level of those with the same prefab
		/// </summary>
		/// <param name="prefab">The station's prefab name</param>
		/// <param name="level">Its level</param>
		public void AddStation(string prefab, int level)
		{
			if (!Stations.TryGetValue(prefab, out int known) || known < level)
			{
				Stations[prefab] = level;
			}
		}

		/// <summary>
		/// Checks whether the village has a station upgraded far enough
		/// </summary>
		/// <param name="prefab">The station's prefab name</param>
		/// <param name="level">The level it has to be at</param>
		/// <returns>True when it has</returns>
		public bool HasStation(string prefab, int level)
		{
			return(Stations.TryGetValue(prefab, out int known) && known >= level);
		}
	}

	/// <summary>
	/// What a village does next toward what it wants - nothing more, since it has it all, gather, craft or smelt an
	/// item, go on a quest for one, or give up, as it can't get it
	/// </summary>
	internal enum StepKind
	{
		Ready,
		Gather,
		Craft,
		Smelt,
		Quest,
		Impossible
	}

	/// <summary>
	/// The next thing a village does toward what it wants - what kind of step, the item and how many of it, the way it
	/// gets them, the quest for a quest, and for a village that can't get it, why
	/// </summary>
	internal class PlanStep
	{
		public StepKind Kind;
		public string Item;
		public int Amount;
		public ResourceSource Source;
		public QuestKind Quest;
		public string Reason = "";

		/// <summary>
		/// Makes a step of a kind
		/// </summary>
		/// <param name="kind">The kind of step</param>
		/// <param name="item">The item's prefab name, or null</param>
		/// <param name="amount">How many of it</param>
		/// <param name="source">The way it's got, or null</param>
		/// <returns>The step</returns>
		public static PlanStep Of(StepKind kind, string item, int amount, ResourceSource source)
		{
			return(new PlanStep { Kind = kind, Item = item, Amount = amount, Source = source, Quest = ((source != null) ? source.Quest : QuestKind.None) });
		}

		/// <summary>
		/// Makes a step for what can't be got
		/// </summary>
		/// <param name="item">The item's prefab name</param>
		/// <param name="reason">Why it can't be</param>
		/// <returns>The step</returns>
		public static PlanStep Fail(string item, string reason)
		{
			return(new PlanStep { Kind = StepKind.Impossible, Item = item, Reason = reason });
		}

		/// <summary>
		/// Describes the step for the debug display and the log
		/// </summary>
		/// <returns>The description, like "Quest MineCopper for 20 CopperOre"</returns>
		public string Describe()
		{
			switch (Kind)
			{
				case StepKind.Ready:
					return("Ready");
				case StepKind.Quest:
					return("Quest " + Quest + " for " + Amount + " " + Item);
				case StepKind.Impossible:
					return("Can't get " + Item + ": " + Reason);
				default:
					return(Kind + " " + Amount + " " + Item);
			}
		}
	}

	/// <summary>
	/// Works out the next thing a village does toward the items it wants, from what it has and every way it gets each
	/// item (see ResourceBook) - following each item it's short of back through what it's crafted or smelted from to
	/// the first one it can gather, craft, smelt or go on a quest for
	/// </summary>
	/// <remarks>
	/// What the village has is spoken for as the plan goes, so two ingredients never count the same items. The ways to
	/// get an item are tried in order, and a way that turns out impossible gives back what it spoke for before the next
	/// is tried. An item a village has some of the ore for is smelted before more ore is gone for, an ingredient isn't
	/// gone for while another ingredient of the same recipe can't be got at all, and of the ingredients a village is
	/// short of, one it has to craft, smelt or go on a quest for comes before one it gathers. A quest's tool, light and
	/// stations are planned for like any item, so wanting silver can come down to crafting a pickaxe, or to killing
	/// Eikthyr for the antler to craft it from
	/// </remarks>
	internal class ResourcePlanner
	{
		/// <summary>
		/// The deepest an item is followed back, which no chain of the game's comes near, so a loop of recipes ends
		/// </summary>
		private const int MaxDepth = 16;

		/// <summary>
		/// Every way to get each item, and what the village has
		/// </summary>
		private readonly ResourceBook book;
		private readonly VillageHoldings holdings;

		/// <summary>
		/// What the village has that the plan hasn't spoken for, and the items being followed back, so an item that comes
		/// back round to itself is given up on
		/// </summary>
		private Dictionary<string, int> left;
		private readonly HashSet<string> following = new HashSet<string>();

		/// <summary>
		/// Sets up a plan
		/// </summary>
		/// <param name="book">Every way to get each item</param>
		/// <param name="holdings">What the village has</param>
		private ResourcePlanner(ResourceBook book, VillageHoldings holdings)
		{
			this.book = book;
			this.holdings = holdings;
			left = new Dictionary<string, int>(holdings.Stock);
		}

		/// <summary>
		/// Works out the next thing a village does toward an item
		/// </summary>
		/// <param name="book">Every way to get each item</param>
		/// <param name="holdings">What the village has</param>
		/// <param name="item">The item's prefab name</param>
		/// <param name="amount">How many it wants</param>
		/// <returns>The step - Ready when it has them all</returns>
		public static PlanStep Next(ResourceBook book, VillageHoldings holdings, string item, int amount)
		{
			return(Next(book, holdings, new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>(item, amount) }));
		}

		/// <summary>
		/// Works out the next thing a village does toward a list of items, like what a recipe takes - the first item it's
		/// short of that it can get, or Impossible when one of them can't be got at all
		/// </summary>
		/// <param name="book">Every way to get each item</param>
		/// <param name="holdings">What the village has</param>
		/// <param name="needs">Each item's prefab name and how many it wants</param>
		/// <returns>The step - Ready when it has them all</returns>
		public static PlanStep Next(ResourceBook book, VillageHoldings holdings, List<KeyValuePair<string, int>> needs)
		{
			return(new ResourcePlanner(book, holdings).All(needs));
		}

		/// <summary>
		/// Follows back each of a list of items, giving the first step any of them needs - or Impossible as soon as one
		/// can't be got, as getting the rest would be no use
		/// </summary>
		/// <param name="needs">Each item's prefab name and how many</param>
		/// <returns>The step - Ready when the village has them all</returns>
		private PlanStep All(List<KeyValuePair<string, int>> needs)
		{
			PlanStep first = null;
			foreach (KeyValuePair<string, int> need in needs)
			{
				PlanStep step = Resolve(need.Key, need.Value, 0);
				if (step.Kind == StepKind.Impossible)
				{
					return(step);
				}
				first = Prefer(first, step);
			}
			return(first ?? PlanStep.Of(StepKind.Ready, null, 0, null));
		}

		/// <summary>
		/// Picks which of two steps toward the items of one list comes first - the first that isn't Ready, but a step
		/// a viking has to set out to do, like a craft, a smelt or a quest, before gathering, which the village's own
		/// jobs do anyway
		/// </summary>
		/// <param name="first">The step picked so far, or null</param>
		/// <param name="step">The next item's step</param>
		/// <returns>The step picked</returns>
		internal static PlanStep Prefer(PlanStep first, PlanStep step)
		{
			if (step.Kind == StepKind.Ready)
			{
				return(first);
			}
			if (first == null || (first.Kind == StepKind.Gather && step.Kind != StepKind.Gather))
			{
				return(step);
			}
			return(first);
		}

		/// <summary>
		/// Speaks for as many of an item as the village has, up to an amount
		/// </summary>
		/// <param name="item">The item's prefab name</param>
		/// <param name="amount">The most to speak for</param>
		/// <returns>How many it spoke for</returns>
		private int Take(string item, int amount)
		{
			int have = (left.TryGetValue(item, out int count) ? count : 0);
			int taken = System.Math.Min(have, System.Math.Max(0, amount));
			left[item] = have - taken;
			return(taken);
		}

		/// <summary>
		/// Gets how many of an item the village has that isn't spoken for
		/// </summary>
		/// <param name="item">The item's prefab name</param>
		/// <returns>How many</returns>
		private int Unclaimed(string item)
		{
			return(left.TryGetValue(item, out int count) ? count : 0);
		}

		/// <summary>
		/// Follows an item back - Ready when the village has enough, or else the first step of the first way to get the
		/// rest that isn't impossible
		/// </summary>
		/// <param name="item">The item's prefab name</param>
		/// <param name="amount">How many</param>
		/// <param name="depth">How many items it was followed back through to get here</param>
		/// <returns>The step</returns>
		private PlanStep Resolve(string item, int amount, int depth)
		{
			int missing = amount - Take(item, amount);
			if (missing <= 0)
			{
				return(PlanStep.Of(StepKind.Ready, item, 0, null));
			}
			if (depth > MaxDepth || !following.Add(item))
			{
				return(PlanStep.Fail(item, "it comes back round to itself"));
			}
			PlanStep failed = null;
			foreach (ResourceSource source in book.SourcesOf(item))
			{
				Dictionary<string, int> before = new Dictionary<string, int>(left);
				PlanStep step = Try(source, item, missing, depth);
				if (step.Kind != StepKind.Impossible)
				{
					following.Remove(item);
					return(step);
				}
				left = before;
				if (failed == null)
				{
					failed = step;
				}
			}
			following.Remove(item);
			return(failed ?? PlanStep.Fail(item, "there's no way to get it"));
		}

		/// <summary>
		/// Works out the first step of one way to get an item
		/// </summary>
		/// <param name="source">The way</param>
		/// <param name="item">The item's prefab name</param>
		/// <param name="missing">How many the village is short of</param>
		/// <param name="depth">How many items it was followed back through to get here</param>
		/// <returns>The step, or Impossible when this way can't get it</returns>
		private PlanStep Try(ResourceSource source, string item, int missing, int depth)
		{
			switch (source.Kind)
			{
				case SourceKind.Gather:
					return(PlanStep.Of(StepKind.Gather, item, missing, source));
				case SourceKind.Craft:
					return(TryCraft(source, item, missing, depth));
				case SourceKind.Smelt:
					return(TrySmelt(source, item, missing, depth));
				default:
					return(TryQuest(source, item, missing, depth));
			}
		}

		/// <summary>
		/// Works out the first step of crafting an item - Craft once the village has the station and everything the
		/// crafts take, or the first ingredient's step when it's short of one
		/// </summary>
		/// <param name="source">The recipe</param>
		/// <param name="item">The item's prefab name</param>
		/// <param name="missing">How many the village is short of</param>
		/// <param name="depth">How many items it was followed back through to get here</param>
		/// <returns>The step</returns>
		private PlanStep TryCraft(ResourceSource source, string item, int missing, int depth)
		{
			if (source.Station != null && !holdings.HasStation(source.Station, source.StationLevel))
			{
				return(PlanStep.Fail(item, "it's crafted at a " + source.Station + " of level " + source.StationLevel));
			}
			int crafts = Crafts(missing, source.Amount);
			PlanStep first = null;
			foreach (KeyValuePair<string, int> ingredient in source.Ingredients)
			{
				PlanStep step = Resolve(ingredient.Key, ingredient.Value * crafts, depth + 1);
				if (step.Kind == StepKind.Impossible)
				{
					return(step);
				}
				first = Prefer(first, step);
			}
			return(first ?? PlanStep.Of(StepKind.Craft, item, missing, source));
		}

		/// <summary>
		/// Works out how many crafts make enough of an item
		/// </summary>
		/// <param name="missing">How many are wanted</param>
		/// <param name="perCraft">How many one craft makes</param>
		/// <returns>How many crafts</returns>
		internal static int Crafts(int missing, int perCraft)
		{
			int each = System.Math.Max(1, perCraft);
			return((System.Math.Max(0, missing) + each - 1) / each);
		}

		/// <summary>
		/// Works out the first step of smelting an item - Smelt what the village has of what it's smelted from, once it
		/// has the station and the fuel, or the step to get more of it when it has none, as long as the fuel for it can
		/// be got
		/// </summary>
		/// <remarks>
		/// With nothing to smelt yet, the fuel is only checked, and what that spoke for is given back, so the fuel the
		/// ore will need doesn't keep the village from what it needs to go for the ore
		/// </remarks>
		/// <param name="source">The smelt</param>
		/// <param name="item">The item's prefab name</param>
		/// <param name="missing">How many the village is short of</param>
		/// <param name="depth">How many items it was followed back through to get here</param>
		/// <returns>The step</returns>
		private PlanStep TrySmelt(ResourceSource source, string item, int missing, int depth)
		{
			if (!holdings.HasStation(source.Station, source.StationLevel))
			{
				return(PlanStep.Fail(item, "it's smelted in a " + source.Station));
			}
			int smelt = Take(source.From, missing);
			if (source.Fuel != null && source.FuelPer > 0)
			{
				Dictionary<string, int> before = new Dictionary<string, int>(left);
				PlanStep fuel = Resolve(source.Fuel, source.FuelPer * ((smelt > 0) ? smelt : missing), depth + 1);
				if (fuel.Kind == StepKind.Impossible || (smelt > 0 && fuel.Kind != StepKind.Ready))
				{
					return(fuel);
				}
				if (smelt <= 0)
				{
					left = before;
				}
			}
			if (smelt > 0)
			{
				return(PlanStep.Of(StepKind.Smelt, item, smelt, source));
			}
			return(Resolve(source.From, missing, depth + 1));
		}

		/// <summary>
		/// Works out the first step of going on a quest for an item - Quest once the village is of a hard enough biome,
		/// has the stations, coins, light, what the quest uses up there and a tool good enough, or the step to get the
		/// light, what it uses up or the tool when it's short of one
		/// </summary>
		/// <param name="source">The quest's way to get the item</param>
		/// <param name="item">The item's prefab name</param>
		/// <param name="missing">How many the village is short of</param>
		/// <param name="depth">How many items it was followed back through to get here</param>
		/// <returns>The step</returns>
		private PlanStep TryQuest(ResourceSource source, string item, int missing, int depth)
		{
			QuestRule rule = book.RuleOf(source.Quest);
			if (rule == null)
			{
				return(PlanStep.Fail(item, "there's no quest " + source.Quest));
			}
			if (holdings.Biome < System.Math.Max(rule.MinBiome, source.MinBiome))
			{
				return(PlanStep.Fail(item, "the village's biome is too easy to go for it"));
			}
			foreach (string station in rule.Stations)
			{
				if (!holdings.HasStation(station, 1))
				{
					return(PlanStep.Fail(item, "going for it takes a " + station));
				}
			}
			if (rule.HivesBelow > 0 && holdings.Hives >= rule.HivesBelow)
			{
				return(PlanStep.Fail(item, "the village has enough beehives"));
			}
			if (rule.Coins > 0 && holdings.Coins < rule.Coins)
			{
				return(PlanStep.Fail(item, "it takes " + rule.Coins + " coins"));
			}
			if (rule.NeedsLight && Unclaimed(ResourceBook.Torch) <= 0 && Unclaimed(ResourceBook.Circlet) <= 0)
			{
				PlanStep light = Resolve(ResourceBook.Torch, 1, depth + 1);
				if (light.Kind != StepKind.Ready)
				{
					return(light);
				}
			}
			if (rule.Offering.Length > 0 && rule.OfferingCount > 0)
			{
				PlanStep offering = Resolve(rule.Offering, rule.OfferingCount, depth + 1);
				if (offering.Kind != StepKind.Ready)
				{
					return(offering);
				}
			}
			if (source.Tool != ToolKind.None && !HasTool(source.Tool, source.ToolTier))
			{
				return(ToolStep(item, source.Tool, source.ToolTier, depth));
			}
			return(PlanStep.Of(StepKind.Quest, item, missing, source));
		}

		/// <summary>
		/// Checks whether the village has a tool of a kind good enough
		/// </summary>
		/// <param name="kind">The kind of tool</param>
		/// <param name="tier">The tier it has to be at</param>
		/// <returns>True when it has</returns>
		private bool HasTool(ToolKind kind, int tier)
		{
			foreach (ToolEntry tool in book.Tools)
			{
				if (tool.Kind == kind && tool.Tier >= tier && holdings.Count(tool.Prefab) > 0)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Works out the first step of getting a tool of a kind good enough - the easiest such tool to make first, then
		/// each better one, the first that isn't impossible
		/// </summary>
		/// <param name="item">The item the tool is for</param>
		/// <param name="kind">The kind of tool</param>
		/// <param name="tier">The tier it has to be at</param>
		/// <param name="depth">How many items it was followed back through to get here</param>
		/// <returns>The step</returns>
		private PlanStep ToolStep(string item, ToolKind kind, int tier, int depth)
		{
			PlanStep failed = null;
			foreach (ToolEntry tool in book.Tools)
			{
				if (tool.Kind != kind || tool.Tier < tier)
				{
					continue;
				}
				Dictionary<string, int> before = new Dictionary<string, int>(left);
				PlanStep step = Resolve(tool.Prefab, 1, depth + 1);
				if (step.Kind != StepKind.Impossible)
				{
					return(step);
				}
				left = before;
				if (failed == null)
				{
					failed = step;
				}
			}
			return(failed ?? PlanStep.Fail(item, "there's no " + kind.ToString().ToLowerInvariant() + " good enough for it"));
		}
	}
}
