using System.Collections.Generic;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests what a village remembers of its dead - how the deaths are noted and read back, and which names its vikings
	/// talk about
	/// </summary>
	public class VillageFallenTests
	{
		/// <summary>
		/// Makes a death
		/// </summary>
		/// <param name="name">The viking's name</param>
		/// <param name="time">When it died, in ticks</param>
		/// <returns>The death</returns>
		private static VillageFallen.Fallen Death(string name, long time)
		{
			VillageFallen.Fallen death = new VillageFallen.Fallen();
			death.Name = name;
			death.Author = "author of " + name;
			death.Time = time;
			return(death);
		}

		/// <summary>
		/// Checks that deaths read back as they were written, with their names, authors and times
		/// </summary>
		[Fact]
		public void RoundTrips()
		{
			List<VillageFallen.Fallen> fallen = new List<VillageFallen.Fallen> { Death("Bjorn", 100L), Death("Astrid", 200L) };
			List<VillageFallen.Fallen> read = VillageFallen.Decode(VillageFallen.Encode(fallen), 0L);
			Assert.Equal(2, read.Count);
			Assert.Equal("Bjorn", read[0].Name);
			Assert.Equal("author of Bjorn", read[0].Author);
			Assert.Equal(100L, read[0].Time);
			Assert.Equal("Astrid", read[1].Name);
			Assert.Equal("author of Astrid", read[1].Author);
			Assert.Equal(200L, read[1].Time);
		}

		/// <summary>
		/// Checks that of 40 deaths written out of order, the latest 32 are kept, earliest first
		/// </summary>
		[Fact]
		public void KeepsTheLatestSorted()
		{
			List<VillageFallen.Fallen> fallen = new List<VillageFallen.Fallen>();
			for (int i = 0; i < 40; i++)
			{
				// 17 and 40 share no factor, so this visits every time from 0 to 39 once, out of order
				int time = (i * 17) % 40;
				fallen.Add(Death("Viking " + time, time));
			}
			List<VillageFallen.Fallen> read = VillageFallen.Decode(VillageFallen.Encode(fallen), 0L);
			Assert.Equal(32, read.Count);
			for (int i = 0; i < read.Count; i++)
			{
				Assert.Equal(i + 8L, read[i].Time);
				Assert.Equal("Viking " + (i + 8), read[i].Name);
			}
		}

		/// <summary>
		/// Checks that writing the deaths leaves the list it was given as it was
		/// </summary>
		[Fact]
		public void EncodeLeavesTheListAlone()
		{
			List<VillageFallen.Fallen> fallen = new List<VillageFallen.Fallen> { Death("Late", 300L), Death("Early", 100L) };
			VillageFallen.Encode(fallen);
			Assert.Equal("Late", fallen[0].Name);
			Assert.Equal("Early", fallen[1].Name);
		}

		/// <summary>
		/// Checks that a death before the earliest time kept is forgotten, while one exactly at it is remembered
		/// </summary>
		[Fact]
		public void ForgetsDeathsBeforeSince()
		{
			List<VillageFallen.Fallen> fallen = new List<VillageFallen.Fallen> { Death("Before", 999L), Death("At", 1000L), Death("After", 1001L) };
			List<VillageFallen.Fallen> read = VillageFallen.Decode(VillageFallen.Encode(fallen), 1000L);
			Assert.Equal(2, read.Count);
			Assert.Equal("At", read[0].Name);
			Assert.Equal("After", read[1].Name);
		}

		/// <summary>
		/// Checks that no deaths read back as none
		/// </summary>
		[Fact]
		public void RoundTripsNone()
		{
			Assert.Empty(VillageFallen.Decode(VillageFallen.Encode(new List<VillageFallen.Fallen>()), 0L));
		}

		/// <summary>
		/// Checks that bytes too short to hold a count, or cut off partway through a death, can't be read
		/// </summary>
		[Fact]
		public void CorruptBytesReadAsNothing()
		{
			Assert.Null(VillageFallen.Decode(new byte[] { 1, 2, 3 }, 0L));
			byte[] bytes = VillageFallen.Encode(new List<VillageFallen.Fallen> { Death("Bjorn", 100L) });
			Assert.Null(VillageFallen.Decode(bytes[..(bytes.Length - 4)], 0L));
		}

		/// <summary>
		/// Checks that a death replaces an earlier one of a viking with the same name, whatever its case, and leaves the
		/// others
		/// </summary>
		[Fact]
		public void ADeathReplacesTheSameName()
		{
			List<VillageFallen.Fallen> fallen = new List<VillageFallen.Fallen> { Death("Bjorn", 100L), Death("Astrid", 200L) };
			VillageFallen.AddDeath(fallen, Death("bjorn", 300L));
			Assert.Equal(2, fallen.Count);
			Assert.Equal("Astrid", fallen[0].Name);
			Assert.Equal("bjorn", fallen[1].Name);
			Assert.Equal(300L, fallen[1].Time);
		}

		/// <summary>
		/// Checks that each name is talked about once, whatever its case, in the order they died
		/// </summary>
		[Fact]
		public void NamesEachOnceIgnoringCase()
		{
			List<string> names = VillageFallen.FilterNames(new List<string> { "Bjorn", "Astrid", "bjorn", "BJORN" }, new List<string>());
			Assert.Equal(new List<string> { "Bjorn", "Astrid" }, names);
		}

		/// <summary>
		/// Checks that a name a PC viking has taken since is left out, whatever its case, as are empty names
		/// </summary>
		[Fact]
		public void LeavesOutNamesOfTheLivingAndEmptyNames()
		{
			List<string> names = VillageFallen.FilterNames(new List<string> { "Bjorn", "", "Astrid", "Sigrid" }, new List<string> { "ASTRID", "" });
			Assert.Equal(new List<string> { "Bjorn", "Sigrid" }, names);
		}
	}
}
