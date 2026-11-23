using System.Collections.Generic;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how the names players have given villages are sent to clients, matched and told apart, and how the names
	/// given on a client are kept as taken until the server's names hold them
	/// </summary>
	public class VillageNameListTests
	{
		/// <summary>
		/// Makes a name
		/// </summary>
		/// <param name="name">The name</param>
		/// <param name="author">Who gave it</param>
		/// <returns>The name</returns>
		private static VillageNameList.Entry Entry(string name, string author)
		{
			return(new VillageNameList.Entry { Name = name, Author = author });
		}

		/// <summary>
		/// Makes a name given on this client
		/// </summary>
		/// <param name="name">The name</param>
		/// <param name="at">When it was given, in seconds</param>
		/// <returns>The name</returns>
		private static VillageNameList.Pending Given(string name, float at)
		{
			return(new VillageNameList.Pending { Entry = Entry(name, ""), At = at });
		}

		/// <summary>
		/// Writes names into a package and reads them back
		/// </summary>
		/// <param name="entries">The names</param>
		/// <returns>The names read</returns>
		private static List<VillageNameList.Entry> RoundTrip(List<VillageNameList.Entry> entries)
		{
			ZPackage pkg = new ZPackage();
			VillageNameList.Write(pkg, entries);
			return(VillageNameList.Read(new ZPackage(pkg.GetArray())));
		}

		/// <summary>
		/// Checks that no names read back as none
		/// </summary>
		[Fact]
		public void NoNamesReadBackAsNone()
		{
			Assert.Empty(RoundTrip(new List<VillageNameList.Entry>()));
		}

		/// <summary>
		/// Checks that one name reads back with its author, and one given by no player with none
		/// </summary>
		/// <param name="author">Who gave it</param>
		[Theory]
		[InlineData("76561198000000001")]
		[InlineData("host")]
		[InlineData("")]
		public void OneNameReadsBack(string author)
		{
			List<VillageNameList.Entry> read = RoundTrip(new List<VillageNameList.Entry> { Entry("Hedeby", author) });
			Assert.Single(read);
			Assert.Equal("Hedeby", read[0].Name);
			Assert.Equal(author, read[0].Author);
		}

		/// <summary>
		/// Checks that several names read back in their order, letters past ASCII and all
		/// </summary>
		[Fact]
		public void SeveralNamesReadBackInOrder()
		{
			List<VillageNameList.Entry> entries = new List<VillageNameList.Entry> { Entry("Jórvík", "host"), Entry("Tømmerby", ""), Entry("Gråbergsby", "76561198000000002") };
			Assert.True(VillageNameList.Same(entries, RoundTrip(entries)));
		}

		/// <summary>
		/// Checks that a village's own name is left out of the others whatever its case, and the rest keep their order
		/// </summary>
		[Fact]
		public void OthersLeaveOutTheOwnName()
		{
			List<VillageNameList.Entry> entries = new List<VillageNameList.Entry> { Entry("Lund", ""), Entry("Ribe", ""), Entry("Visby", "") };
			List<VillageNameList.Entry> others = VillageNameList.Others(entries, "rIBE");
			Assert.Equal(2, others.Count);
			Assert.Equal("Lund", others[0].Name);
			Assert.Equal("Visby", others[1].Name);
			Assert.Equal(3, VillageNameList.Others(entries, "").Count);
		}

		/// <summary>
		/// Checks that a name is added once, a second time in another case being the same name
		/// </summary>
		[Fact]
		public void NamesAreAddedOnce()
		{
			List<VillageNameList.Entry> entries = new List<VillageNameList.Entry>();
			Assert.True(VillageNameList.Add(entries, "Kaupang", "host"));
			Assert.False(VillageNameList.Add(entries, "KAUPANG", ""));
			Assert.True(VillageNameList.Add(entries, "Kalmar", ""));
			Assert.Equal(2, entries.Count);
			Assert.Equal("host", entries[0].Author);
			Assert.True(VillageNameList.Has(entries, "kalmar"));
			Assert.False(VillageNameList.Has(entries, "Kalma"));
		}

		/// <summary>
		/// Checks that two lists are the same only with the same names, in the same order and case, by the same players
		/// </summary>
		[Fact]
		public void SameNeedsTheSameNamesOrderAndAuthors()
		{
			List<VillageNameList.Entry> a = new List<VillageNameList.Entry> { Entry("Lund", "host"), Entry("Ribe", "") };
			Assert.True(VillageNameList.Same(a, new List<VillageNameList.Entry> { Entry("Lund", "host"), Entry("Ribe", "") }));
			Assert.False(VillageNameList.Same(a, new List<VillageNameList.Entry> { Entry("Ribe", ""), Entry("Lund", "host") }));
			Assert.False(VillageNameList.Same(a, new List<VillageNameList.Entry> { Entry("Lund", ""), Entry("Ribe", "") }));
			Assert.False(VillageNameList.Same(a, new List<VillageNameList.Entry> { Entry("lund", "host"), Entry("Ribe", "") }));
			Assert.False(VillageNameList.Same(a, new List<VillageNameList.Entry> { Entry("Lund", "host") }));
		}

		/// <summary>
		/// Checks that names given here that the server's names don't hold are kept after them, and one they hold in
		/// another case isn't put in twice
		/// </summary>
		[Fact]
		public void MergeKeepsNamesGivenHere()
		{
			List<VillageNameList.Entry> listed = new List<VillageNameList.Entry> { Entry("Lund", "host"), Entry("Ribe", "") };
			List<VillageNameList.Pending> given = new List<VillageNameList.Pending> { Given("Odense", 5f), Given("LUND", 6f) };
			List<VillageNameList.Entry> merged = VillageNameList.Merge(listed, given);
			Assert.True(VillageNameList.Same(new List<VillageNameList.Entry> { Entry("Lund", "host"), Entry("Ribe", ""), Entry("Odense", "") }, merged));
			Assert.Equal(2, listed.Count);
		}

		/// <summary>
		/// Checks that a name given here stays until the server's names hold it, whatever its case
		/// </summary>
		[Fact]
		public void ConfirmDropsNamesTheServerHolds()
		{
			List<VillageNameList.Pending> given = new List<VillageNameList.Pending> { Given("Odense", 5f), Given("Lejre", 5f) };
			VillageNameList.Confirm(given, new List<VillageNameList.Entry> { Entry("Lund", "") }, 10f, 90f);
			Assert.Equal(2, given.Count);
			VillageNameList.Confirm(given, new List<VillageNameList.Entry> { Entry("odense", "") }, 20f, 90f);
			Assert.Single(given);
			Assert.Equal("Lejre", given[0].Entry.Name);
		}

		/// <summary>
		/// Checks that a name given here the server's names never hold is dropped once it's been kept the longest it can
		/// be, and kept until then
		/// </summary>
		[Fact]
		public void ConfirmDropsNamesKeptTooLong()
		{
			List<VillageNameList.Pending> given = new List<VillageNameList.Pending> { Given("Odense", 10f), Given("Lejre", 50f) };
			List<VillageNameList.Entry> listed = new List<VillageNameList.Entry>();
			VillageNameList.Confirm(given, listed, 99.9f, 90f);
			Assert.Equal(2, given.Count);
			VillageNameList.Confirm(given, listed, 100f, 90f);
			Assert.Single(given);
			Assert.Equal("Lejre", given[0].Entry.Name);
		}

		/// <summary>
		/// Checks that a name given after a look through the world passed its village stays taken when that look's names
		/// come in without it, and isn't put in twice once a later look's names hold it
		/// </summary>
		[Fact]
		public void ANameGivenMidLookStaysTaken()
		{
			List<VillageNameList.Pending> given = new List<VillageNameList.Pending> { Given("Odense", 10f) };
			List<VillageNameList.Entry> missed = new List<VillageNameList.Entry> { Entry("Lund", "host") };
			VillageNameList.Confirm(given, missed, 12f, 90f);
			Assert.True(VillageNameList.Has(VillageNameList.Merge(missed, given), "Odense"));
			List<VillageNameList.Entry> found = new List<VillageNameList.Entry> { Entry("Odense", ""), Entry("Lund", "host") };
			VillageNameList.Confirm(given, found, 45f, 90f);
			Assert.Empty(given);
			Assert.True(VillageNameList.Same(found, VillageNameList.Merge(found, given)));
		}
	}
}
