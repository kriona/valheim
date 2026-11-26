using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The rules of a village's cart - which villages keep one, what becomes of it wherever it ends up, and which sides
	/// of a house it's parked beside - worked out apart from the game so they can be tested
	/// </summary>
	internal static class CartRules
	{
		/// <summary>
		/// What a village's cart is up to, as its village sees it (see VillageCarts)
		/// </summary>
		internal enum CartState
		{
			/// <summary>
			/// The village keeps no cart - a Meadows or Ashlands village
			/// </summary>
			None,

			/// <summary>
			/// It has none, or it was destroyed or has been gone too long, so the vikings build one
			/// </summary>
			Missing,

			/// <summary>
			/// It stands in the village
			/// </summary>
			Home,

			/// <summary>
			/// It stands loaded outside the village, so a viking fetches it back
			/// </summary>
			Astray,

			/// <summary>
			/// It's out of reach - somewhere not loaded - and comes back on its own or is built anew once it has been
			/// gone a game day
			/// </summary>
			Away,

			/// <summary>
			/// It went out on a quest, and comes back with it
			/// </summary>
			OnQuest
		}

		/// <summary>
		/// The weight in a cart that slows a viking pulling it as much as it's slowed at all - its "full" load, which
		/// the cart is loaded up to (see ShareWithCart) - and how much longer a trip takes pulling it full, as a share
		/// </summary>
		/// <remarks>
		/// A cart has no weight limit - the game only adds what it holds to its mass (see Vagon.UpdateMass) - so these are
		/// estimates, to be put right by the speeds the test log notes for vikings pulling carts (see CartPull)
		/// </remarks>
		internal const float FullWeight = 800f;
		internal const float FullSlowdown = 0.25f;

		/// <summary>
		/// The most a trip can be slowed by a cart, as times its length, however heavy the cart
		/// </summary>
		private const float SlowdownMost = 1.5f;

		/// <summary>
		/// How much longer a meter of rough ground counts as taking for a road a cart is pulled along, on top of its
		/// usual time (see Rough), so the road keeps off it where it can
		/// </summary>
		internal const float RoughCost = 2f;

		/// <summary>
		/// Checks whether a biome is rough ground for pulling a cart - the Swamp's mire, the Mountains' slopes and the
		/// Mistlands' cliffs
		/// </summary>
		/// <param name="biome">The biome's index (see ResourceBook)</param>
		/// <returns>True when it is</returns>
		internal static bool Rough(int biome)
		{
			return(biome == ResourceBook.Swamp || biome == ResourceBook.Mountain || biome == ResourceBook.Mistlands);
		}

		/// <summary>
		/// The weight from which an item counts as heavy for loading a cart - wood, stone, ore and metal - rather than
		/// light, like trophies, hides and feathers
		/// </summary>
		internal const float HeavyWeight = 2f;

		/// <summary>
		/// Works out how much longer a trip takes pulling a cart holding a weight
		/// </summary>
		/// <param name="load">The weight in the cart</param>
		/// <returns>How many times as long it takes, 1 for an empty cart</returns>
		internal static float Slowdown(float load)
		{
			return(Mathf.Min(SlowdownMost, 1f + FullSlowdown * Mathf.Max(0f, load) / FullWeight));
		}

		/// <summary>
		/// Works out how many of a light item go in a cart once its heavy load is in (see ShareWithCart) - a small stack,
		/// like a single trophy
		/// </summary>
		/// <param name="maxStack">How many of it make a full stack</param>
		/// <returns>How many</returns>
		internal static int SmallStack(int maxStack)
		{
			return(Mathf.Clamp(maxStack / 20, 1, 5));
		}

		/// <summary>
		/// Adds a cart's room to what a quest's vikings can carry between them, shared out evenly, for working out how
		/// much they bring home - its slots and the weight of the heavy load it takes, half its full weight
		/// </summary>
		/// <param name="spaces">Each viking's room</param>
		/// <param name="cartSlots">The cart's slots</param>
		/// <returns>Each viking's room with a share of the cart's, new copies</returns>
		internal static List<CarrySpace> Pooled(List<CarrySpace> spaces, int cartSlots)
		{
			List<CarrySpace> pooled = new List<CarrySpace>();
			int count = spaces.Count;
			for (int i = 0; i < count; i++)
			{
				pooled.Add(new CarrySpace { WeightRoom = spaces[i].WeightRoom + FullWeight / 2f / count, FreeSlots = spaces[i].FreeSlots + cartSlots / count + ((i < cartSlots % count) ? 1 : 0) });
			}
			return(pooled);
		}

		/// <summary>
		/// Shares what a quest found out among its vikings and a cart - the vikings carrying the heaviest things first,
		/// after what the quest was for, then the cart taking heavy things (see HeavyWeight) up to half its full weight,
		/// heaviest first, then light things in small stacks (see SmallStack) up to its full weight, lightest first -
		/// leaving behind what doesn't fit, so the cart is never loaded so heavy it holds the vikings back for long
		/// </summary>
		/// <param name="found">What was found, each item and how many</param>
		/// <param name="wanted">What the quest was for, which goes first, or ""</param>
		/// <param name="spaces">Each viking's room, used up as they're given things</param>
		/// <param name="cartSlots">The cart's slots</param>
		/// <param name="weightOf">Gets an item's weight</param>
		/// <param name="stackOf">Gets how many of an item make a stack</param>
		/// <param name="cartShare">Set to what goes in the cart, none when it isn't needed</param>
		/// <returns>What each viking carries home, in the order of spaces</returns>
		internal static List<Dictionary<string, int>> ShareWithCart(List<KeyValuePair<string, int>> found, string wanted, List<CarrySpace> spaces, int cartSlots, System.Func<string, float> weightOf, System.Func<string, int> stackOf, out Dictionary<string, int> cartShare)
		{
			List<Dictionary<string, int>> shares = QuestSim.Share(HeavyFirst(found, wanted, weightOf), spaces, weightOf, stackOf);
			Dictionary<string, int> left = new Dictionary<string, int>();
			foreach (KeyValuePair<string, int> entry in found)
			{
				int carried = 0;
				foreach (Dictionary<string, int> share in shares)
				{
					carried += (share.TryGetValue(entry.Key, out int count) ? count : 0);
				}
				if (entry.Value > carried)
				{
					left[entry.Key] = (left.TryGetValue(entry.Key, out int known) ? known : 0) + entry.Value - carried;
				}
			}
			cartShare = new Dictionary<string, int>();
			CarrySpace cart = new CarrySpace { WeightRoom = FullWeight / 2f, FreeSlots = cartSlots };
			foreach (KeyValuePair<string, int> entry in HeavyFirst(ToList(left), wanted, weightOf))
			{
				if (weightOf(entry.Key) >= HeavyWeight)
				{
					TakeUnits(cart, cartShare, entry.Key, weightOf(entry.Key), stackOf(entry.Key), entry.Value);
				}
			}
			cart.WeightRoom += FullWeight / 2f;
			List<KeyValuePair<string, int>> light = HeavyFirst(ToList(left), "", weightOf);
			light.Reverse();
			foreach (KeyValuePair<string, int> entry in light)
			{
				int maxStack = stackOf(entry.Key);
				if (weightOf(entry.Key) < HeavyWeight)
				{
					TakeUnits(cart, cartShare, entry.Key, weightOf(entry.Key), maxStack, Mathf.Min(entry.Value, SmallStack(maxStack)));
				}
			}
			return(shares);
		}

		/// <summary>
		/// Ranks an item by how much vikings leaving a disbanded village want it, or picking up what their broken cart
		/// dropped - food first, then wood and stone, then everything else, then trophies last
		/// </summary>
		/// <param name="food">Whether it's food</param>
		/// <param name="woodOrStone">Whether it's wood or stone</param>
		/// <param name="trophy">Whether it's a trophy</param>
		/// <returns>The rank, lowest first</returns>
		internal static int LootRank(bool food, bool woodOrStone, bool trophy)
		{
			if (food)
			{
				return(0);
			}
			if (woodOrStone)
			{
				return(1);
			}
			return(trophy ? 3 : 2);
		}

		/// <summary>
		/// Weighs up what's in a load
		/// </summary>
		/// <param name="load">Each item and how many</param>
		/// <param name="weightOf">Gets an item's weight</param>
		/// <returns>The weight</returns>
		internal static float WeightOf(Dictionary<string, int> load, System.Func<string, float> weightOf)
		{
			float weight = 0f;
			foreach (KeyValuePair<string, int> entry in load)
			{
				weight += weightOf(entry.Key) * entry.Value;
			}
			return(weight);
		}

		/// <summary>
		/// Orders items heaviest first, after what's wanted, keeping the order they came in between items that weigh the
		/// same
		/// </summary>
		/// <param name="items">Each item and how many</param>
		/// <param name="wanted">The item that goes first, or ""</param>
		/// <param name="weightOf">Gets an item's weight</param>
		/// <returns>The items, ordered, a new list leaving out those there are none of</returns>
		private static List<KeyValuePair<string, int>> HeavyFirst(List<KeyValuePair<string, int>> items, string wanted, System.Func<string, float> weightOf)
		{
			List<int> order = new List<int>();
			for (int i = 0; i < items.Count; i++)
			{
				if (items[i].Value > 0)
				{
					order.Add(i);
				}
			}
			order.Sort((int a, int b) =>
			{
				bool aWanted = (wanted.Length > 0 && items[a].Key == wanted);
				bool bWanted = (wanted.Length > 0 && items[b].Key == wanted);
				if (aWanted != bWanted)
				{
					return(aWanted ? -1 : 1);
				}
				int byWeight = weightOf(items[b].Key).CompareTo(weightOf(items[a].Key));
				return((byWeight != 0) ? byWeight : a.CompareTo(b));
			});
			return(order.ConvertAll((int i) => items[i]));
		}

		/// <summary>
		/// Lists a tally's items and how many of each
		/// </summary>
		/// <param name="tally">The tally</param>
		/// <returns>The items</returns>
		private static List<KeyValuePair<string, int>> ToList(Dictionary<string, int> tally)
		{
			return(new List<KeyValuePair<string, int>>(tally));
		}

		/// <summary>
		/// Puts as many of an item in a cart as it has room for, up to a number - on a stack of it already there, or in
		/// an empty slot
		/// </summary>
		/// <param name="space">The cart's room, used up</param>
		/// <param name="share">What it holds, added to</param>
		/// <param name="item">The item</param>
		/// <param name="weight">Its weight</param>
		/// <param name="maxStack">How many make a stack</param>
		/// <param name="most">The most to put in</param>
		/// <returns>How many went in</returns>
		private static int TakeUnits(CarrySpace space, Dictionary<string, int> share, string item, float weight, int maxStack, int most)
		{
			maxStack = Mathf.Max(1, maxStack);
			int taken = 0;
			while (taken < most && space.WeightRoom >= weight)
			{
				int held = (share.TryGetValue(item, out int known) ? known : 0);
				bool newSlot = (held % maxStack == 0);
				if (newSlot && space.FreeSlots <= 0)
				{
					break;
				}
				if (newSlot)
				{
					space.FreeSlots--;
				}
				space.WeightRoom -= weight;
				share[item] = held + 1;
				taken++;
			}
			return(taken);
		}

		/// <summary>
		/// Checks whether a village of a biome keeps a cart - not in the Meadows or the Ashlands
		/// </summary>
		/// <param name="biome">The village's biome's index (see ResourceBook)</param>
		/// <returns>True when it does</returns>
		internal static bool Allowed(int biome)
		{
			return(biome != ResourceBook.Meadows && biome != ResourceBook.AshLands);
		}

		/// <summary>
		/// Works out what a village's cart is up to from what its village can see of it
		/// </summary>
		/// <param name="allowed">Whether the village keeps a cart (see Allowed)</param>
		/// <param name="onQuest">Whether its cart went out on a quest and hasn't come back yet</param>
		/// <param name="recorded">Whether the village has noted a cart as its own</param>
		/// <param name="known">Whether this client knows the cart's ZDO</param>
		/// <param name="lastSeenLoaded">Whether where the cart was last seen is loaded here, so a cart that isn't known
		/// there was destroyed</param>
		/// <param name="loaded">Whether the cart itself is loaded here</param>
		/// <param name="inside">Whether it stands in the village</param>
		/// <param name="awaySeconds">How long it has been out of reach, in seconds, or 0 when it hasn't</param>
		/// <param name="daySeconds">How long a game day lasts, in seconds</param>
		/// <returns>What it's up to</returns>
		internal static CartState Decide(bool allowed, bool onQuest, bool recorded, bool known, bool lastSeenLoaded, bool loaded, bool inside, double awaySeconds, double daySeconds)
		{
			if (!allowed)
			{
				return(CartState.None);
			}
			if (onQuest && !recorded)
			{
				return(CartState.OnQuest);
			}
			if (!recorded || (!known && lastSeenLoaded))
			{
				return(CartState.Missing);
			}
			if (!known || !loaded)
			{
				return((awaySeconds >= daySeconds) ? CartState.Missing : CartState.Away);
			}
			return(inside ? CartState.Home : CartState.Astray);
		}

		/// <summary>
		/// Lists the sides of a house a cart is parked beside, in the order they're tried - the side opposite its door
		/// first, then the sides to its left and right, never the side its door is on. A house with no door found has
		/// all four sides tried, starting with its back
		/// </summary>
		/// <param name="front">Which way the house's door faces out from its middle, seen from above, or zero when it
		/// has none</param>
		/// <param name="leftFirst">True to try the left side before the right</param>
		/// <returns>Each side's way out from the house's middle, seen from above, one meter long</returns>
		internal static List<Vector2> Sides(Vector2 front, bool leftFirst)
		{
			bool noDoor = (front.sqrMagnitude < 0.0001f);
			Vector2 forward = (noDoor ? new Vector2(0f, 1f) : front.normalized);
			Vector2 back = -forward;
			Vector2 left = new Vector2(-forward.y, forward.x);
			Vector2 right = -left;
			List<Vector2> sides = new List<Vector2> { back };
			sides.Add(leftFirst ? left : right);
			sides.Add(leftFirst ? right : left);
			if (noDoor)
			{
				sides.Add(forward);
			}
			return(sides);
		}

		/// <summary>
		/// Works out how far a cart beside a house stands from the house's middle - past its walls, the gap kept from
		/// them and half the cart's own width
		/// </summary>
		/// <param name="halfSize">How far the house's walls are from its middle, in meters</param>
		/// <param name="wallGap">How far the cart keeps from the walls, in meters</param>
		/// <param name="halfWidth">Half the cart's width across, in meters</param>
		/// <returns>The distance, in meters</returns>
		internal static float SideDistance(float halfSize, float wallGap, float halfWidth)
		{
			return(halfSize + wallGap + halfWidth);
		}

		/// <summary>
		/// Checks whether a spot is too near a door - within some distance of any door or doorstep - for a cart to stand
		/// there, so it never blocks the way in
		/// </summary>
		/// <param name="spot">The spot, seen from above</param>
		/// <param name="doors">The doors and doorsteps, seen from above</param>
		/// <param name="clearance">How far from each the cart keeps, in meters</param>
		/// <returns>True when it's too near one</returns>
		internal static bool BlocksDoor(Vector2 spot, List<Vector2> doors, float clearance)
		{
			foreach (Vector2 door in doors)
			{
				if ((door - spot).magnitude < clearance)
				{
					return(true);
				}
			}
			return(false);
		}
	}
}
