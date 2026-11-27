using System.Collections.Generic;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a trip to Haldor buys several things at once, shares the purse out for pockets, and how many go for
	/// pockets
	/// </summary>
	public class HaldorTradeTests
	{
		/// <summary>
		/// Makes something wanted from Haldor
		/// </summary>
		/// <param name="item">What it is</param>
		/// <param name="count">How many</param>
		/// <param name="price">What each costs</param>
		/// <returns>The purchase</returns>
		private static Purchase Want(string item, int count, int price)
		{
			return(new Purchase { Item = item, Count = count, Price = price });
		}

		/// <summary>
		/// Checks that a trip buys as many of the first thing wanted as the purse pays for, then of the next with what's
		/// left
		/// </summary>
		[Fact]
		public void BuysInOrderWhileThePursePays()
		{
			List<Purchase> wanted = new List<Purchase> { Want("HelmetDverger", 3, 620), Want("BeltStrength", 2, 950) };
			List<Purchase> bought = HaldorTrade.Shopping(wanted, 3000);
			Assert.Equal(2, bought.Count);
			Assert.Equal("HelmetDverger", bought[0].Item);
			Assert.Equal(3, bought[0].Count);
			Assert.Equal("BeltStrength", bought[1].Item);
			Assert.Equal(1, bought[1].Count);
		}

		/// <summary>
		/// Checks that a trip leaves out what it can't afford any of, and buys a later, cheaper thing with what's left
		/// </summary>
		[Fact]
		public void SkipsWhatItCantAfford()
		{
			List<Purchase> wanted = new List<Purchase> { Want("BeltStrength", 1, 950), Want("HelmetDverger", 1, 620) };
			List<Purchase> bought = HaldorTrade.Shopping(wanted, 700);
			Assert.Single(bought);
			Assert.Equal("HelmetDverger", bought[0].Item);
			Assert.Empty(HaldorTrade.Shopping(wanted, 600));
		}

		/// <summary>
		/// Checks that buying brings home what's bought and the coins left over, and nothing when the purse pays for none
		/// </summary>
		[Fact]
		public void BringsHomeWhatItBoughtAndTheChange()
		{
			List<Purchase> wanted = new List<Purchase> { Want("HelmetDverger", 2, 620), Want("BeltStrength", 1, 950) };
			List<KeyValuePair<string, int>> loot = HaldorTrade.Buy(wanted, 2500, out bool spends);
			Assert.True(spends);
			Assert.Equal(new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("HelmetDverger", 2), new KeyValuePair<string, int>("BeltStrength", 1), new KeyValuePair<string, int>(ResourceBook.Coins, 310) }, loot);
			Assert.Empty(HaldorTrade.Buy(wanted, 500, out bool tooDear));
			Assert.False(tooDear);
		}

		/// <summary>
		/// Checks that the purse is shared out coins first, each buyer taking enough for their own, and a buyer whose share
		/// wouldn't pay for theirs taking nothing
		/// </summary>
		[Fact]
		public void SharesThePurseOut()
		{
			List<PurseStack> purse = new List<PurseStack>
			{
				new PurseStack { Item = "Ruby", Count = 3, Value = 20 },
				new PurseStack { Item = ResourceBook.Coins, Count = 2500, Value = 1 }
			};
			List<List<KeyValuePair<string, int>>> shares = HaldorTrade.SplitPurse(purse, 3, 1000);
			Assert.Equal(new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>(ResourceBook.Coins, 1000) }, shares[0]);
			Assert.Equal(new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>(ResourceBook.Coins, 1000) }, shares[1]);
			Assert.Empty(shares[2]);
		}

		/// <summary>
		/// Checks that treasure makes up a share coins don't cover, a piece of it whole even when it pays more than's short
		/// </summary>
		[Fact]
		public void MakesUpWithTreasure()
		{
			List<PurseStack> purse = new List<PurseStack>
			{
				new PurseStack { Item = ResourceBook.Coins, Count = 950, Value = 1 },
				new PurseStack { Item = "Ruby", Count = 2, Value = 20 },
				new PurseStack { Item = "Ruby", Count = 1, Value = 20 }
			};
			List<List<KeyValuePair<string, int>>> shares = HaldorTrade.SplitPurse(purse, 1, 1000);
			Assert.Equal(new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>(ResourceBook.Coins, 950), new KeyValuePair<string, int>("Ruby", 3) }, shares[0]);
		}

		/// <summary>
		/// Checks that with no price known, no share is handed out
		/// </summary>
		[Fact]
		public void SharesNothingWithoutAPrice()
		{
			List<PurseStack> purse = new List<PurseStack> { new PurseStack { Item = ResourceBook.Coins, Count = 5000, Value = 1 } };
			List<List<KeyValuePair<string, int>>> shares = HaldorTrade.SplitPurse(purse, 2, int.MaxValue);
			Assert.Equal(2, shares.Count);
			Assert.Empty(shares[0]);
			Assert.Empty(shares[1]);
		}

		/// <summary>
		/// Checks how many go for pockets - every buyer, and a PC viking beside them when none of them is one, but never
		/// fewer than the quest's own number or more than the village can spare
		/// </summary>
		/// <param name="ruleSize">How many go by the quest's own rule</param>
		/// <param name="buyers">How many it buys pockets for</param>
		/// <param name="pcBuys">Whether a PC viking is among them</param>
		/// <param name="available">How many are fit to go</param>
		/// <param name="village">How many the village has</param>
		/// <param name="expected">How many go</param>
		[Theory]
		[InlineData(2, 1, true, 5, 8, 2)]
		[InlineData(2, 4, true, 6, 10, 4)]
		[InlineData(2, 4, false, 6, 10, 5)]
		[InlineData(2, 6, true, 6, 7, 5)]
		[InlineData(2, 6, true, 3, 10, 3)]
		[InlineData(0, 4, true, 6, 10, 0)]
		public void SendsEveryBuyer(int ruleSize, int buyers, bool pcBuys, int available, int village, int expected)
		{
			Assert.Equal(expected, QuestRules.PocketsGroup(ruleSize, buyers, pcBuys, available, village));
		}
	}
}
