using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the groups the server keeps on the road - how they're written and how far they've come
	/// </summary>
	public class TravelEntryTests
	{
		/// <summary>
		/// Makes a group with every value set
		/// </summary>
		/// <returns>The group</returns>
		private static TravelEntry Full()
		{
			TravelEntry entry = new TravelEntry();
			entry.Vikings.Add(new byte[] { 1, 2, 3 });
			entry.Vikings.Add(new byte[0]);
			entry.Destination = new Vector3(100f, 30f, -200f);
			entry.Name = "Eikvik";
			entry.Due = 638000000000000000L;
			entry.From = new Vector3(1f, 2f, 3f);
			entry.OriginFire = new Vector3(-5f, 6f, 7f);
			entry.Unloaded = 637999999999999999L;
			entry.Spent = 120.5f;
			entry.Remaining = 800.25f;
			entry.Returning = true;
			entry.FromName = "Hrafnheim";
			entry.Biome = 3;
			entry.Excluded.Add(new Vector3(9f, 0f, 9f));
			return(entry);
		}

		/// <summary>
		/// Checks that two groups hold the same values
		/// </summary>
		/// <param name="expected">One group</param>
		/// <param name="actual">The other</param>
		private static void AssertSame(TravelEntry expected, TravelEntry actual)
		{
			Assert.Equal(expected.Vikings, actual.Vikings);
			Assert.Equal(expected.Destination, actual.Destination);
			Assert.Equal(expected.Name, actual.Name);
			Assert.Equal(expected.Due, actual.Due);
			Assert.Equal(expected.From, actual.From);
			Assert.Equal(expected.OriginFire, actual.OriginFire);
			Assert.Equal(expected.Unloaded, actual.Unloaded);
			Assert.Equal(expected.Spent, actual.Spent);
			Assert.Equal(expected.Remaining, actual.Remaining);
			Assert.Equal(expected.Returning, actual.Returning);
			Assert.Equal(expected.FromName, actual.FromName);
			Assert.Equal(expected.Biome, actual.Biome);
			Assert.Equal(expected.Excluded, actual.Excluded);
		}

		[Fact]
		public void EntrySurvivesBeingWrittenAndRead()
		{
			TravelEntry entry = Full();
			AssertSame(entry, TravelEntry.Decode(entry.Encode()));
		}

		[Fact]
		public void ListSurvivesBeingWrittenAndRead()
		{
			List<TravelEntry> entries = new List<TravelEntry> { Full(), new TravelEntry() };
			List<TravelEntry> read = TravelEntry.DecodeAll(TravelEntry.EncodeAll(entries));
			Assert.Equal(2, read.Count);
			AssertSame(entries[0], read[0]);
			AssertSame(entries[1], read[1]);
		}

		[Fact]
		public void UnreadableBytesGiveNoEntries()
		{
			Assert.Empty(TravelEntry.DecodeAll(null));
			Assert.Empty(TravelEntry.DecodeAll(new byte[] { 5, 0, 0 }));
			Assert.Null(TravelEntry.Decode(new byte[] { 1 }));
		}

		[Fact]
		public void EntryWrittenAnotherWayIsSkipped()
		{
			ZPackage one = new ZPackage();
			one.Write(99);
			ZPackage all = new ZPackage();
			all.Write(2);
			all.Write(one.GetArray());
			all.Write(Full().Encode());
			List<TravelEntry> read = TravelEntry.DecodeAll(all.GetArray());
			Assert.Single(read);
			Assert.Equal("Eikvik", read[0].Name);
		}

		[Fact]
		public void ProgressAddsTheTimeSinceItWentOutOfReach()
		{
			TravelEntry entry = new TravelEntry { Unloaded = 1000L, Spent = 50f, Remaining = 500f };
			Assert.Equal(70f, entry.Progress(1000L + 20L * 10L, 10.0), 3);
		}

		[Fact]
		public void ProgressTakesNoLongerThanTheWholeRoad()
		{
			TravelEntry entry = new TravelEntry { Unloaded = 0L, Spent = 50f, Remaining = 500f };
			Assert.Equal(550f, entry.Progress(1000000L, 10.0), 3);
			Assert.Equal(50f, entry.Progress(-10L, 10.0), 3);
		}
	}
}
