using System.Collections.Generic;
using Xunit;

namespace Kriona.Vikings.Tests
{
	/// <summary>
	/// Tests which console commands are held back in a world whose server doesn't have the mod
	/// </summary>
	public class CommandGuardTests
	{
		/// <summary>
		/// The starts of the mod's commands' names, its prefabs and its raids, for the tests
		/// </summary>
		private static readonly List<string> Prefixes = new List<string> { "kri_vv_" };
		private static readonly List<string> Prefabs = new List<string> { "Viking", "VikingVillage", "ZombiePoisonCloud" };
		private static readonly List<string> Raids = new List<string> { "army_zombievikings" };

		/// <summary>
		/// Works out what a command would use
		/// </summary>
		/// <param name="command">The command's name</param>
		/// <param name="argument">Its first argument, or null</param>
		/// <returns>What it would use</returns>
		private static CommandGuard.Use Classify(string command, string argument)
		{
			return(CommandGuard.Classify(command, argument, Prefixes, Prefabs, Raids));
		}

		[Theory]
		[InlineData("kri_vv_spawnvillage", null)]
		[InlineData("kri_vv_testlog", "start")]
		[InlineData("KRI_VV_BLUEPRINTS", "reload")]
		public void TheModsOwnCommandsAreHeldBack(string command, string argument)
		{
			Assert.Equal(CommandGuard.Use.Command, Classify(command, argument));
		}

		[Theory]
		[InlineData("Viking")]
		[InlineData("viking")]
		[InlineData("ZombiePoisonCloud")]
		[InlineData("vik*")]
		[InlineData("poison*")]
		public void SpawningWhatTheModAddsIsHeldBack(string argument)
		{
			Assert.Equal(CommandGuard.Use.Spawn, Classify("spawn", argument));
		}

		[Theory]
		[InlineData("Boar")]
		[InlineData("VikingShip")]
		[InlineData("boar*")]
		[InlineData("*")]
		public void SpawningAnythingElseGoesAhead(string argument)
		{
			Assert.Equal(CommandGuard.Use.None, Classify("spawn", argument));
		}

		[Fact]
		public void StartingTheModsRaidIsHeldBack()
		{
			Assert.Equal(CommandGuard.Use.Raid, Classify("event", "army_zombievikings"));
			Assert.Equal(CommandGuard.Use.None, Classify("event", "army_eikthyr"));
		}

		[Theory]
		[InlineData("spawn", null)]
		[InlineData("spawn", "")]
		[InlineData("kill", "Viking")]
		[InlineData("", "Viking")]
		[InlineData(null, null)]
		public void OtherCommandsGoAhead(string command, string argument)
		{
			Assert.Equal(CommandGuard.Use.None, Classify(command, argument));
		}

		[Fact]
		public void RefusalNamesTheMod()
		{
			Assert.Equal("Viking Villages isn't on this server, so what it adds can't be spawned here", CommandGuard.Refusal(CommandGuard.Use.Spawn, "Viking Villages"));
			Assert.Null(CommandGuard.Refusal(CommandGuard.Use.None, "Viking Villages"));
		}
	}
}
