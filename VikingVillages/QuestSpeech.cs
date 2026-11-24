using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What the vikings say about their village's quests (see VillageQuests) - the lines are in DIALOG.md too
	/// </summary>
	internal static class QuestSpeech
	{
		/// <summary>
		/// The token a line is filled in at with roughly when a quest is due back, like "tomorrow" (see
		/// TravelTime.Phrase)
		/// </summary>
		public const string WhenToken = "{when}";

		/// <summary>
		/// What one of the vikings setting out on a quest says as they go - with roughly when they'll be back, or for
		/// "now", a line without it
		/// </summary>
		internal static readonly string[] DepartureLines =
		{
			"We're off! Expect us back {when}.",
			"Keep the fire going - we'll be back {when}.",
			"Don't wait up. We should be home {when}.",
			"Right, we're away. Back {when}, if the gods allow.",
			"Off we go. Look for us {when}."
		};

		/// <summary>
		/// Picks what one of the vikings setting out on a quest says as they go
		/// </summary>
		/// <param name="when">Roughly when they're due back, like "tomorrow", or "now"</param>
		/// <returns>The line</returns>
		public static string Departure(string when)
		{
			string line = DepartureLines[Random.Range(0, DepartureLines.Length)];
			return(Fill(line, ((when == "now") ? "soon" : when)));
		}

		/// <summary>
		/// Fills a line in with roughly when a quest is due back
		/// </summary>
		/// <param name="line">The line</param>
		/// <param name="when">The phrase</param>
		/// <returns>The line filled in</returns>
		internal static string Fill(string line, string when)
		{
			return(line.Replace(WhenToken, when));
		}
	}
}
