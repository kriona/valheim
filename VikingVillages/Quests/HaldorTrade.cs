using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// Something a trip to Haldor buys - what it is, how many and what each costs
	/// </summary>
	internal class Purchase
	{
		public string Item;
		public int Count;
		public int Price;
	}

	/// <summary>
	/// A stack a viking takes along to pay Haldor with - coins, or treasure he takes at what it's worth
	/// </summary>
	internal class PurseStack
	{
		public string Item;
		public int Count;
		public int Value;
	}

	/// <summary>
	/// How a trip to Haldor buys several things at once - what a village's coins and treasure pay for, what each viking
	/// who buys pockets for itself takes along, and what comes home
	/// </summary>
	/// <remarks>
	/// One trip buys every Dverger circlet and strength belt the village's vikings lack that its coins and treasure pay
	/// for, which the one who carries the purse hands out at home. Pockets are sewn into the buyer's own clothes, so every
	/// viking who lacks them goes along, each with a share of the purse
	/// </remarks>
	internal static class HaldorTrade
	{
		/// <summary>
		/// Works out what a trip buys of what's wanted, in the order it's wanted, as many of each as the purse pays for
		/// once what comes before is paid for
		/// </summary>
		/// <param name="wanted">What's wanted, first first</param>
		/// <param name="purse">What the coins and treasure are worth, in coins</param>
		/// <returns>What it buys, leaving out what it buys none of</returns>
		internal static List<Purchase> Shopping(List<Purchase> wanted, int purse)
		{
			List<Purchase> bought = new List<Purchase>();
			int left = purse;
			foreach (Purchase want in wanted)
			{
				if (want.Price <= 0 || want.Count <= 0)
				{
					continue;
				}
				int count = System.Math.Min(want.Count, left / want.Price);
				if (count <= 0)
				{
					continue;
				}
				bought.Add(new Purchase { Item = want.Item, Count = count, Price = want.Price });
				left -= count * want.Price;
			}
			return(bought);
		}

		/// <summary>
		/// Works out what a viking trading with Haldor brings home for what it took along - as much of what it's to buy
		/// as that pays for, in order (see Shopping), and the coins left over - or nothing at all when it pays for none
		/// </summary>
		/// <param name="wanted">What it's to buy, first first</param>
		/// <param name="purse">What its coins and treasure are worth, in coins</param>
		/// <param name="spends">Set to whether it spends what it took along</param>
		/// <returns>What it brings home, each item and how many</returns>
		internal static List<KeyValuePair<string, int>> Buy(List<Purchase> wanted, int purse, out bool spends)
		{
			List<KeyValuePair<string, int>> loot = new List<KeyValuePair<string, int>>();
			List<Purchase> bought = Shopping(wanted, purse);
			spends = (bought.Count > 0);
			if (!spends)
			{
				return(loot);
			}
			int left = purse;
			foreach (Purchase purchase in bought)
			{
				loot.Add(new KeyValuePair<string, int>(purchase.Item, purchase.Count));
				left -= purchase.Count * purchase.Price;
			}
			if (left > 0)
			{
				loot.Add(new KeyValuePair<string, int>(ResourceBook.Coins, left));
			}
			return(loot);
		}

		/// <summary>
		/// Shares a village's purse out between the vikings who each buy something for themselves, like pockets - coins
		/// first, then treasure, each viking taking until it has enough for what it buys, in turn, while there's any left.
		/// A stack is split where a share needs only some of it, but a piece of treasure is never split
		/// </summary>
		/// <param name="purse">The village's coins and treasure</param>
		/// <param name="buyers">How many buy</param>
		/// <param name="price">What each buys costs</param>
		/// <returns>Each buyer's share, each item and how many - an empty share for one the purse doesn't reach</returns>
		internal static List<List<KeyValuePair<string, int>>> SplitPurse(List<PurseStack> purse, int buyers, int price)
		{
			List<List<KeyValuePair<string, int>>> shares = new List<List<KeyValuePair<string, int>>>();
			// With no price known, no share pays for it
			if (price <= 0 || price == int.MaxValue)
			{
				for (int i = 0; i < buyers; i++)
				{
					shares.Add(new List<KeyValuePair<string, int>>());
				}
				return(shares);
			}
			// The same item's stacks are counted together, coins first, so treasure, which can't be split, makes up what's
			// short
			List<PurseStack> left = new List<PurseStack>();
			foreach (PurseStack stack in purse)
			{
				if (stack.Count <= 0 || stack.Value <= 0)
				{
					continue;
				}
				PurseStack same = left.Find((PurseStack other) => other.Item == stack.Item);
				if (same != null)
				{
					same.Count += stack.Count;
				}
				else if (stack.Item == ResourceBook.Coins)
				{
					left.Insert(0, new PurseStack { Item = stack.Item, Count = stack.Count, Value = stack.Value });
				}
				else
				{
					left.Add(new PurseStack { Item = stack.Item, Count = stack.Count, Value = stack.Value });
				}
			}
			for (int i = 0; i < buyers; i++)
			{
				List<KeyValuePair<string, int>> share = new List<KeyValuePair<string, int>>();
				int worth = 0;
				foreach (PurseStack stack in left)
				{
					if (worth >= price)
					{
						break;
					}
					if (stack.Count <= 0)
					{
						continue;
					}
					int needed = (price - worth + stack.Value - 1) / stack.Value;
					int taken = System.Math.Min(stack.Count, needed);
					stack.Count -= taken;
					worth += taken * stack.Value;
					share.Add(new KeyValuePair<string, int>(stack.Item, taken));
				}
				// A share that doesn't pay for it is put back, so the purse goes as far as it can
				if (worth < price)
				{
					foreach (KeyValuePair<string, int> taken in share)
					{
						left.Find((PurseStack stack) => stack.Item == taken.Key).Count += taken.Value;
					}
					share.Clear();
				}
				shares.Add(share);
			}
			return(shares);
		}
	}
}
