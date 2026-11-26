using System;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests when the Yule hats show and which helmets they take the place of
	/// </summary>
	public class YuleHatsTests
	{
		/// <summary>
		/// Checks that only the 25th of December is Christmas Day, whatever the year
		/// </summary>
		/// <param name="year">The year</param>
		/// <param name="month">The month</param>
		/// <param name="day">The day</param>
		/// <param name="expected">Whether it's Christmas Day</param>
		[Theory]
		[InlineData(2026, 12, 25, true)]
		[InlineData(2031, 12, 25, true)]
		[InlineData(2026, 12, 24, false)]
		[InlineData(2026, 12, 26, false)]
		[InlineData(2026, 11, 25, false)]
		[InlineData(2027, 1, 1, false)]
		public void KnowsChristmasDay(int year, int month, int day, bool expected)
		{
			Assert.Equal(expected, YuleHats.IsChristmas(new DateTime(year, month, day, 23, 59, 0)));
		}

		/// <summary>
		/// Checks that the hats go by the date unless they're turned on or off
		/// </summary>
		/// <param name="mode">The name of the mode - whether they go by the date or are turned on or off</param>
		/// <param name="christmas">Whether it's Christmas Day</param>
		/// <param name="expected">Whether they show</param>
		[Theory]
		[InlineData("Date", true, true)]
		[InlineData("Date", false, false)]
		[InlineData("On", false, true)]
		[InlineData("On", true, true)]
		[InlineData("Off", true, false)]
		[InlineData("Off", false, false)]
		public void ShowsByModeAndDate(string mode, bool christmas, bool expected)
		{
			Assert.Equal(expected, YuleHats.Shows((YuleHats.Mode)Enum.Parse(typeof(YuleHats.Mode), mode), christmas));
		}

		/// <summary>
		/// Checks that a helmet shows as the Yule hat while the hats show, and as itself while they don't
		/// </summary>
		[Fact]
		public void SwapsHelmetsWhileShowing()
		{
			int helmet = "HelmetBronze".GetStableHashCode();
			Assert.Equal(YuleHats.YuleHash, YuleHats.HatFor(helmet, true));
			Assert.Equal(helmet, YuleHats.HatFor(helmet, false));
		}

		/// <summary>
		/// Checks that no helmet, a Dverger circlet and the Yule hat itself are shown as they are
		/// </summary>
		[Fact]
		public void LeavesNoHelmetAndCircletAlone()
		{
			Assert.Equal(0, YuleHats.HatFor(0, true));
			Assert.Equal(YuleHats.CircletHash, YuleHats.HatFor(YuleHats.CircletHash, true));
			Assert.Equal(YuleHats.YuleHash, YuleHats.HatFor(YuleHats.YuleHash, true));
		}
	}
}
