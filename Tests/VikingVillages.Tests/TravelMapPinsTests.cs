using System.Collections.Generic;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a group of vikings on the road or out on a quest is labeled on the map
	/// </summary>
	public class TravelMapPinsTests
	{
		/// <summary>
		/// Checks that a group's label has how many, what brings them, their names and what they're doing
		/// </summary>
		[Fact]
		public void LabelsAQuestGroupWithNamesAndWhatTheyreDoing()
		{
			VillageArrivals.Arrival arrival = new VillageArrivals.Arrival { Count = 2, What = "out on MineCopper for CopperOre", Where = "on the way home by sea", Names = new List<string> { "Astrid", "Ulf" } };
			Assert.Equal("2 vikings out on MineCopper for CopperOre (Astrid, Ulf), on the way home by sea", TravelMapPins.Label(arrival));
		}

		/// <summary>
		/// Checks that a group with no names and nothing said about what it's doing leaves both out
		/// </summary>
		[Fact]
		public void LeavesOutMissingNamesAndDoing()
		{
			VillageArrivals.Arrival arrival = new VillageArrivals.Arrival { Count = 1, What = "on the road" };
			Assert.Equal("1 viking on the road", TravelMapPins.Label(arrival));
		}
	}
}
