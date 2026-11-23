using System.Collections.Generic;
using BepInEx.Configuration;
using Kriona.Shared;

namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests how a BepInEx config file's values are read from its lines to check them against the settings, the way
	/// BepInEx reads them
	/// </summary>
	public class ConfigWatcherTests
	{
		/// <summary>
		/// Checks that settings are read by their section and key, with their values trimmed
		/// </summary>
		[Fact]
		public void ReadsSettingsBySection()
		{
			Dictionary<ConfigDefinition, string> values = ConfigWatcher.ReadValues(new[]
			{
				"[Zombies]",
				"ZombieFlies = true",
				"  ZombieHazeStrength   =   50  ",
				"",
				"[Vikings]",
				"VikingVillages=false"
			});
			Assert.Equal(3, values.Count);
			Assert.Equal("true", values[new ConfigDefinition("Zombies", "ZombieFlies")]);
			Assert.Equal("50", values[new ConfigDefinition("Zombies", "ZombieHazeStrength")]);
			Assert.Equal("false", values[new ConfigDefinition("Vikings", "VikingVillages")]);
		}

		/// <summary>
		/// Checks that comments are skipped, even ones that look like settings or follow spaces
		/// </summary>
		[Fact]
		public void SkipsComments()
		{
			Dictionary<ConfigDefinition, string> values = ConfigWatcher.ReadValues(new[]
			{
				"## Settings file was created by plugin",
				"[General]",
				"# Setting type: Boolean",
				"   # Default = true",
				"Enabled = false"
			});
			Assert.Single(values);
			Assert.Equal("false", values[new ConfigDefinition("General", "Enabled")]);
		}

		/// <summary>
		/// Checks that a value holding "=" keeps everything after the first one
		/// </summary>
		[Fact]
		public void SplitsAtTheFirstEquals()
		{
			Dictionary<ConfigDefinition, string> values = ConfigWatcher.ReadValues(new[] { "[General]", "Key = a=b = c" });
			Assert.Equal("a=b = c", values[new ConfigDefinition("General", "Key")]);
		}

		/// <summary>
		/// Checks that a setting before any section header is read in the section "", and an empty value as ""
		/// </summary>
		[Fact]
		public void ReadsSettingsBeforeAnySection()
		{
			Dictionary<ConfigDefinition, string> values = ConfigWatcher.ReadValues(new[] { "Loose = 1", "Empty =", "[General]", "Loose = 2" });
			Assert.Equal(3, values.Count);
			Assert.Equal("1", values[new ConfigDefinition("", "Loose")]);
			Assert.Equal("", values[new ConfigDefinition("", "Empty")]);
			Assert.Equal("2", values[new ConfigDefinition("General", "Loose")]);
		}

		/// <summary>
		/// Checks that a header with spaces around it starts its section, lines with no "=" are skipped, and a setting
		/// written twice keeps its last value
		/// </summary>
		[Fact]
		public void TrimsHeadersAndKeepsTheLastValue()
		{
			Dictionary<ConfigDefinition, string> values = ConfigWatcher.ReadValues(new[] { "   [General]   ", "not a setting", "Count = 1", "Count = 3" });
			Assert.Single(values);
			Assert.Equal("3", values[new ConfigDefinition("General", "Count")]);
		}

		/// <summary>
		/// Checks that a file with no lines has no values
		/// </summary>
		[Fact]
		public void ReadsNothingFromNoLines()
		{
			Assert.Empty(ConfigWatcher.ReadValues(new string[0]));
		}
	}
}
