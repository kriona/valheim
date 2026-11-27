using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// What only NPC vikings say - as they run from danger, as they're caught and fight back, and as
	/// they go to shelter from a raid - and the lines and talks of the PC vikings an NPC viking never says, about
	/// fighting and adventuring, since it does neither (see VikingSpeech.Says)
	/// </summary>
	/// <remarks>
	/// Every line here is listed in the mod's DIALOG.md, which changes along with them - each row there says who says
	/// it, PC, NPC or Dvergr Vikings
	/// </remarks>
	internal static class NPCSpeech
	{
		/// <summary>
		/// What an NPC viking cries out as it runs from danger
		/// </summary>
		private static readonly string[] FleeLines =
		{
			"Run! Get inside!",
			"I'm no fighter - I'm going in!",
			"Help! Someone deal with that thing!",
			"Inside, quick! Before it sees us!",
			"Not me, not today - I'm off!",
			"Keep it away from me!"
		};

		/// <summary>
		/// What an NPC viking cries out as it's caught and fights back
		/// </summary>
		private static readonly string[] CorneredLines =
		{
			"Stay back! I'm warning you!",
			"Get away from me!",
			"I'll fight if I have to!",
			"You picked the wrong Viking to corner!",
			"Back off, or I'll... I'll hit you!"
		};

		/// <summary>
		/// What an NPC viking calls out as it notices a raid on its village by day and goes to shelter
		/// </summary>
		private static readonly string[] HideLines =
		{
			"A raid! Everyone who can't fight, get inside!",
			"Raiders! I'm going indoors!",
			"Bar the doors - they're coming!",
			"Hide! Leave this to the fighters!",
			"They're coming for {village}! Inside, quick!"
		};

		/// <summary>
		/// The lines, and the talks by their first line, an NPC viking never says or has - about fighting, keeping watch,
		/// training, its weapons and its own adventures - each the same as it's written where it's said
		/// </summary>
		private static readonly HashSet<string> PCOnlyLines = new HashSet<string>
		{
			"Weapons up - {0}!",
			"Training, {0}? Me too, when the chores let me.",
			"Practicing your {1}, {0}? So am I, whenever I get the chance.",
			"I'm training too, {0}. There's always more to learn.",
			"A little practice every day, {0}. That's how I keep my {1} from going rusty.",
			"A stone out there says killing draugr is a mercy. I try to remember that while they're shooting at me.",
			"'Slow to stalk, quick to bite,' says the fenring stone. I sleep with my axe, {0}.",
			"That other Kata killed trolls the size of towers for a glimmering reward. I'll start with a small one, {0}.",
			"The dead don't stay buried anymore.",
			"The dead keep getting back up.",
			"My axe needs a new edge.",
			"Long night ahead, {name}.",
			"That was close.",
			"You fought well out there, {name}.",
			"Did we get them all?",
			"My arm still aches from that last fight.",
			"I'm not as quick as I used to be.",
			"Do you ever go through to {linked}, {name}?"
		};

		/// <summary>
		/// Picks what an NPC viking cries out as it runs from danger
		/// </summary>
		/// <returns>The line</returns>
		public static string Flee()
		{
			return(VikingSpeech.Pick(FleeLines, ""));
		}

		/// <summary>
		/// Picks what an NPC viking cries out as it's caught and fights back
		/// </summary>
		/// <returns>The line</returns>
		public static string Cornered()
		{
			return(VikingSpeech.Pick(CorneredLines, ""));
		}

		/// <summary>
		/// Picks what an NPC viking calls out as it goes to shelter from a raid
		/// </summary>
		/// <param name="village">The village's name, or ""</param>
		/// <returns>The line</returns>
		public static string Hide(string village)
		{
			return(string.Format(VikingSpeech.Pick(HideLines, village)));
		}

		/// <summary>
		/// Checks whether a line or talk is one only PC vikings say
		/// </summary>
		/// <param name="line">The line, or the talk's first line, as it's written</param>
		/// <returns>True when it is</returns>
		internal static bool PCOnly(string line)
		{
			return(PCOnlyLines.Contains(line));
		}
	}
}
