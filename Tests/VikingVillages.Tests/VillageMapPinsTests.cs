using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how the village pins put down on a player's map are noted in their data - each village's key, and the
	/// note read and written
	/// </summary>
	public class VillageMapPinsTests
	{
		/// <summary>
		/// Checks that a village's key is where it stands across the ground to the meter, leaving out its height, with
		/// halves rounded to the even meter
		/// </summary>
		/// <param name="x">Where it stands east to west</param>
		/// <param name="z">Where it stands north to south</param>
		/// <param name="expected">The key</param>
		[Theory]
		[InlineData(0f, 0f, "0,0")]
		[InlineData(1.6f, -1.4f, "2,-1")]
		[InlineData(2.5f, 3.5f, "2,4")]
		[InlineData(-2.5f, -3.5f, "-2,-4")]
		[InlineData(-0.5f, 0.5f, "0,0")]
		[InlineData(-1234.7f, 5678.2f, "-1235,5678")]
		public void KeyIsWhereItStandsToTheMeter(float x, float z, string expected)
		{
			Assert.Equal(expected, VillageMapPins.Key(new Vector3(x, 250f, z)));
		}

		/// <summary>
		/// Checks that pins read back as they were written, with negative keys and hashes
		/// </summary>
		[Fact]
		public void RoundTrips()
		{
			Dictionary<string, int> pinned = new Dictionary<string, int>
			{
				{ "-120,45", -987654321 },
				{ "0,0", 0 },
				{ "3000,-2999", int.MaxValue },
				{ "-1,-1", int.MinValue }
			};
			Assert.Equal(pinned, VillageMapPins.Parse(VillageMapPins.Format(pinned)));
		}

		/// <summary>
		/// Checks that no pins are written as nothing, and nothing reads as no pins
		/// </summary>
		[Fact]
		public void NoPinsAreNothing()
		{
			Assert.Equal("", VillageMapPins.Format(new Dictionary<string, int>()));
			Assert.Empty(VillageMapPins.Parse(""));
		}

		/// <summary>
		/// Checks that entries with no key, no hash or a hash that isn't a number are left out, and the rest kept
		/// </summary>
		/// <param name="value">The note</param>
		[Theory]
		[InlineData("garbage")]
		[InlineData("1,2=")]
		[InlineData("=5")]
		[InlineData("1,2=abc")]
		[InlineData(";;")]
		public void LeavesOutBrokenEntries(string value)
		{
			Assert.Empty(VillageMapPins.Parse(value));
			Dictionary<string, int> pinned = VillageMapPins.Parse(value + ";4,5=6");
			Assert.Single(pinned);
			Assert.Equal(6, pinned["4,5"]);
		}

		/// <summary>
		/// Checks that of two entries with the same key, the last is kept
		/// </summary>
		[Fact]
		public void KeepsTheLastOfTheSameKey()
		{
			Dictionary<string, int> pinned = VillageMapPins.Parse("1,2=10;3,4=20;1,2=30");
			Assert.Equal(2, pinned.Count);
			Assert.Equal(30, pinned["1,2"]);
			Assert.Equal(20, pinned["3,4"]);
		}

		/// <summary>
		/// Checks that a key holding "=" is split at its last one, so the hash is what follows it
		/// </summary>
		[Fact]
		public void SplitsAtTheLastEquals()
		{
			Dictionary<string, int> pinned = VillageMapPins.Parse("a=b=7");
			Assert.Equal(7, pinned["a=b"]);
		}
	}
}
