using System.Collections.Generic;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how the test log adds up its totals and writes them, a line per category
	/// </summary>
	public class TestTotalsTests
	{
		/// <summary>
		/// Checks that amounts for the same thing add up, and that something never counted is 0
		/// </summary>
		[Fact]
		public void AmountsAddUp()
		{
			TestTotals totals = new TestTotals();
			totals.Add("Looted", "Wood", 3);
			totals.Add("Looted", "Wood", 2);
			totals.Add("Looted", "Stone", 1);
			Assert.Equal(5.0, totals.Get("Looted", "Wood"));
			Assert.Equal(1.0, totals.Get("Looted", "Stone"));
			Assert.Equal(0.0, totals.Get("Looted", "Flint"));
			Assert.Equal(0.0, totals.Get("Eaten", "Wood"));
		}

		/// <summary>
		/// Checks that each category's line has its sum, then the largest first and alphabetically among equals, with
		/// the categories in the order they were first counted
		/// </summary>
		[Fact]
		public void LinesSortLargestFirst()
		{
			TestTotals totals = new TestTotals();
			totals.Add("Jobs started", "Stroll", 2);
			totals.Add("Eaten", "Honey", 1);
			totals.Add("Jobs started", "Fuel", 5);
			totals.Add("Jobs started", "Cook", 2);
			List<string> lines = totals.Lines();
			Assert.Equal(2, lines.Count);
			Assert.Equal("Jobs started: 9 - Fuel 5, Cook 2, Stroll 2", lines[0]);
			Assert.Equal("Eaten: 1 - Honey 1", lines[1]);
		}

		/// <summary>
		/// Checks that past ListedMax names the rest are summed up as one
		/// </summary>
		[Fact]
		public void LinesSumUpTheRest()
		{
			TestTotals totals = new TestTotals();
			for (int i = 0; i < TestTotals.ListedMax + 2; i++)
			{
				totals.Add("Crafted", "Item" + i.ToString("00"), 100 - i);
			}
			string line = totals.Lines()[0];
			Assert.EndsWith(", 2 more " + TestTotals.Format(100 - TestTotals.ListedMax + 100 - TestTotals.ListedMax - 1), line);
			Assert.DoesNotContain("Item" + TestTotals.ListedMax.ToString("00"), line);
		}

		/// <summary>
		/// Checks that whole amounts are written without decimals and the rest to one place
		/// </summary>
		[Fact]
		public void FormatsAmounts()
		{
			Assert.Equal("12", TestTotals.Format(12.0));
			Assert.Equal("12.3", TestTotals.Format(12.34));
			Assert.Equal("13", TestTotals.Format(12.96));
			Assert.Equal("0.5", TestTotals.Format(0.5));
		}
	}
}
