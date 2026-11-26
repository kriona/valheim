using System.Collections.Generic;
using System.IO;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the names new vikings and villages are given - each one once, short enough to keep whole, one a viking or
	/// village would take, and listed in NAMES.md
	/// </summary>
	public class NameListsTests
	{
		/// <summary>
		/// Checks that no viking name is listed twice, for a man or a woman, whatever its case, and that each fits in
		/// MaxLength and is one a viking would take
		/// </summary>
		[Fact]
		public void VikingNamesAreUniqueAndTaken()
		{
			List<string> names = new List<string>(VikingNames.MaleNames);
			names.AddRange(VikingNames.FemaleNames);
			HashSet<string> seen = new HashSet<string>();
			foreach (string name in names)
			{
				Assert.True(seen.Add(name.ToLowerInvariant()), name + " is listed twice");
				Assert.True(name.Length <= VikingNames.MaxLength, name + " is too long");
				Assert.Equal(VikingNames.Verdict.Taken, VikingNames.JudgeText(name, "Zombie"));
			}
		}

		/// <summary>
		/// Checks that no village name is listed twice, whatever its case, and that each fits in MaxLength and is one a
		/// village would take
		/// </summary>
		[Fact]
		public void VillageNamesAreUniqueAndTaken()
		{
			HashSet<string> seen = new HashSet<string>();
			foreach (string name in VillageNames.AutoNames)
			{
				Assert.True(seen.Add(name.ToLowerInvariant()), name + " is listed twice");
				Assert.True(name.Length <= VillageNames.MaxLength, name + " is too long");
				Assert.False(VikingNames.IsPlace(name), name + " is a place's name");
				Assert.False(VikingNames.IsMarkup(name), name + " has markup");
			}
		}

		/// <summary>
		/// Checks that Arendelle's first four vikings are Elsa, Anna, Kristoff and Olaf, in that order, whatever the
		/// village's case, and that the vikings after them and other villages' are left to be named as any other
		/// </summary>
		[Fact]
		public void ArendellesFirstVikings()
		{
			Assert.Equal("Elsa", VikingNames.FirstVikingName("Arendelle", 0));
			Assert.Equal("Anna", VikingNames.FirstVikingName("arendelle", 1));
			Assert.Equal("Kristoff", VikingNames.FirstVikingName(" ARENDELLE ", 2));
			Assert.Equal("Olaf", VikingNames.FirstVikingName("Arendelle", 3));
			Assert.Null(VikingNames.FirstVikingName("Arendelle", 4));
			Assert.Null(VikingNames.FirstVikingName("Arendelle", -1));
			Assert.Null(VikingNames.FirstVikingName("Northuldra", 0));
			Assert.Null(VikingNames.FirstVikingName("", 0));
			Assert.Null(VikingNames.FirstVikingName(null, 0));
			Assert.Contains("Arendelle", VillageNames.AutoNames);
			Assert.Contains("Northuldra", VillageNames.AutoNames);
		}

		/// <summary>
		/// Checks that every village's first vikings have names of the lists, so each is rolled as a man or a woman to
		/// suit - Elsa and Anna as women, Kristoff and Olaf as men
		/// </summary>
		[Fact]
		public void FirstVikingsSuitTheirNames()
		{
			foreach (KeyValuePair<string, string[]> village in VikingNames.FirstVikings)
			{
				foreach (string name in village.Value)
				{
					Assert.True(VikingNames.ModelFor(name) >= 0, name + " of " + village.Key + " isn't a man's or a woman's name");
				}
			}
			Assert.Equal(1, VikingNames.ModelFor("Elsa"));
			Assert.Equal(1, VikingNames.ModelFor("anna"));
			Assert.Equal(0, VikingNames.ModelFor("Kristoff"));
			Assert.Equal(0, VikingNames.ModelFor("Olaf"));
			Assert.Equal(-1, VikingNames.ModelFor("Sven the Reindeer"));
		}

		/// <summary>
		/// Checks that NAMES.md lists exactly the names in the code, under the heading for each kind
		/// </summary>
		[Fact]
		public void NamesFileListsEveryName()
		{
			Dictionary<string, List<string>> sections = ReadNamesFile();
			List<string> bynames = new List<string>(VikingNames.LeaderBynames);
			bynames.AddRange(VikingNames.Bynames);
			AssertSameNames(sections, "Male Names", VikingNames.MaleNames);
			AssertSameNames(sections, "Female Names", VikingNames.FemaleNames);
			AssertSameNames(sections, "Bynames", bynames);
			AssertSameNames(sections, "Village Names", VillageNames.AutoNames);
		}

		/// <summary>
		/// Reads the names NAMES.md lists under each of its "## " headings - the comma-separated lines below it, its "### "
		/// headings and blank lines left out
		/// </summary>
		/// <returns>The names, by heading</returns>
		private static Dictionary<string, List<string>> ReadNamesFile()
		{
			string path = Path.Combine(VillageBlueprintsTests.RepositoryFolder(), "VikingVillages", "NAMES.md");
			Dictionary<string, List<string>> sections = new Dictionary<string, List<string>>();
			List<string> current = null;
			foreach (string raw in File.ReadAllLines(path))
			{
				string line = raw.Trim();
				if (line.StartsWith("## "))
				{
					current = new List<string>();
					sections[line.Substring(3)] = current;
					continue;
				}
				if (current == null || line.Length == 0 || line.StartsWith("#"))
				{
					continue;
				}
				foreach (string name in line.Split(','))
				{
					if (name.Trim().Length > 0)
					{
						current.Add(name.Trim());
					}
				}
			}
			return(sections);
		}

		/// <summary>
		/// Checks that a heading of NAMES.md lists the names in the code, each once, and no others
		/// </summary>
		/// <param name="sections">The names NAMES.md lists, by heading</param>
		/// <param name="heading">The heading</param>
		/// <param name="expected">The names in the code</param>
		private static void AssertSameNames(Dictionary<string, List<string>> sections, string heading, IEnumerable<string> expected)
		{
			Assert.True(sections.TryGetValue(heading, out List<string> listed), "NAMES.md has no " + heading + " heading");
			HashSet<string> extra = new HashSet<string>(listed);
			List<string> missing = new List<string>();
			foreach (string name in expected)
			{
				if (!extra.Remove(name))
				{
					missing.Add(name);
				}
			}
			Assert.True(missing.Count == 0, "NAMES.md's " + heading + " leaves out " + string.Join(", ", missing));
			Assert.True(extra.Count == 0, "NAMES.md's " + heading + " has names the code doesn't: " + string.Join(", ", extra));
			Assert.Equal(listed.Count, new HashSet<string>(listed).Count);
		}
	}
}
