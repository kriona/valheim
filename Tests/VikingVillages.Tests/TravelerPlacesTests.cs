using System.Collections.Generic;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests where a traveler comes from and goes to - the far-off villages picked for a world, and the two villages
	/// picked for a traveler
	/// </summary>
	public class TravelerPlacesTests
	{
		/// <summary>
		/// Checks that the far-off villages come from the end of the shuffled names, leaving out those taken and those to
		/// skip, up to the count
		/// </summary>
		[Fact]
		public void ExtrasComeFromTheEndLeavingOutTaken()
		{
			List<string> shuffled = new List<string> { "Alby", "Brekka", "Dalby", "Eskil", "Fjord", "Grind" };
			HashSet<string> taken = new HashSet<string> { "grind", "dalby" };
			List<string> extras = TravelerPlaces.Extras(shuffled, taken, 3, (string name) => name == "Eskil");
			Assert.Equal(new List<string> { "Fjord", "Brekka", "Alby" }, extras);
		}

		/// <summary>
		/// Checks that there are no more far-off villages than names left
		/// </summary>
		[Fact]
		public void ExtrasStopWhenNamesRunOut()
		{
			List<string> shuffled = new List<string> { "Alby", "Brekka" };
			Assert.Equal(new List<string> { "Brekka" }, TravelerPlaces.Extras(shuffled, new HashSet<string> { "alby" }, 10, null));
		}

		/// <summary>
		/// Checks that the shuffle comes out the same for the same seed, and holds every name once
		/// </summary>
		[Fact]
		public void ShuffledIsTheSameForASeed()
		{
			List<string> names = new List<string> { "Alby", "Brekka", "Dalby", "Eskil", "Fjord", "Grind", "Hamar" };
			List<string> first = VillagePlannedNames.Shuffled(names, 1234);
			Assert.Equal(first, VillagePlannedNames.Shuffled(names, 1234));
			List<string> sorted = new List<string>(first);
			sorted.Sort(System.StringComparer.Ordinal);
			Assert.Equal(names, sorted);
		}

		/// <summary>
		/// Checks that a traveler's two villages are never the same, and both come from the list
		/// </summary>
		/// <param name="fromRoll">The roll for where it comes from</param>
		/// <param name="toRoll">The roll for where it goes</param>
		/// <param name="from">Where it should come from</param>
		/// <param name="to">Where it should go</param>
		[Theory]
		[InlineData(0f, 0f, "Alby", "Brekka")]
		[InlineData(0f, 0.99f, "Alby", "Dalby")]
		[InlineData(0.5f, 0f, "Brekka", "Alby")]
		[InlineData(0.5f, 0.6f, "Brekka", "Dalby")]
		[InlineData(1f, 0f, "Dalby", "Alby")]
		[InlineData(1f, 1f, "Dalby", "Brekka")]
		public void PickPairNeverPicksOneTwice(float fromRoll, float toRoll, string from, string to)
		{
			TravelerPlaces.PickPair(new List<string> { "Alby", "Brekka", "Dalby" }, fromRoll, toRoll, out string pickedFrom, out string pickedTo);
			Assert.Equal(from, pickedFrom);
			Assert.Equal(to, pickedTo);
		}

		/// <summary>
		/// Checks that a list of one gives only where it comes from, and an empty one neither
		/// </summary>
		[Fact]
		public void PickPairWithTooFewVillages()
		{
			TravelerPlaces.PickPair(new List<string> { "Alby" }, 0.5f, 0.5f, out string from, out string to);
			Assert.Equal("Alby", from);
			Assert.Equal("", to);
			TravelerPlaces.PickPair(new List<string>(), 0.5f, 0.5f, out from, out to);
			Assert.Equal("", from);
			Assert.Equal("", to);
		}
	}
}
