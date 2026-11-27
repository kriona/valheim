using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What a living viking says to a player wearing nothing on their chest or legs - asking whether they're cold, and
	/// offering to lend them something to wear, kindly and never poking fun - as it calls out to them (see
	/// VikingConcernSpeech) and now and then when they talk to it. In the Ashlands, the lines about the cold are left
	/// out for lines about the ash. The lines are in DIALOG.md too
	/// </summary>
	internal static class VikingClothesSpeech
	{
		/// <summary>
		/// How likely a viking talks about it when a player it knows talks to it without clothes
		/// </summary>
		private const float ChatChance = 0.3f;

		/// <summary>
		/// What a viking calls out to a player it sees without clothes, with {0} for the player's name - anywhere,
		/// anywhere but the Ashlands, and only in the Ashlands
		/// </summary>
		internal static readonly string[] CallLines =
		{
			"Need something to wear, {0}? I've a spare tunic you can borrow.",
			"Lost your gear, {0}? I can lend you some clothes until you find it."
		};
		internal static readonly string[] ColdCallLines =
		{
			"Aren't you cold, {0}? I've a spare tunic if you want it.",
			"{0}, you'll catch a chill like that. Want to borrow some clothes?",
			"Not cold, {0}? You're tougher than me. Say the word if you want a cloak."
		};
		internal static readonly string[] HotCallLines =
		{
			"The ash will sting bare skin, {0}. Want to borrow something to wear?"
		};

		/// <summary>
		/// What a viking says to a player it knows without clothes who talks to it, with {0} for the player's name -
		/// anywhere, anywhere but the Ashlands, and only in the Ashlands
		/// </summary>
		internal static readonly string[] ChatLines =
		{
			"Nothing to wear, {0}? It happens to the best of us. I can lend you something.",
			"If you need clothes, {0}, I've a spare tunic. It's a bit big, but it'll do."
		};
		internal static readonly string[] ColdChatLines =
		{
			"Are you warm enough like that, {0}? I can lend you something to wear.",
			"You must be cold, {0}. Come sit by the fire, and I'll find you a tunic.",
			"Take my cloak if you're cold, {0}. I've another."
		};
		internal static readonly string[] HotChatLines =
		{
			"Bare skin and burning ash don't mix, {0}. Borrow a tunic, at least."
		};

		/// <summary>
		/// Checks whether a player is wearing no clothes - nothing on their chest or legs
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>True when they aren't</returns>
		public static bool Unclothed(Player player)
		{
			return(player.m_chestItem == null && player.m_legItem == null);
		}

		/// <summary>
		/// Picks what a viking says to a player it knows who talks to it without clothes, now and then
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>The line, or null when it says something else</returns>
		public static string Chat(Player player)
		{
			if (!Unclothed(player) || Random.value >= ChatChance)
			{
				return(null);
			}
			string[] lines = VikingConcernSpeech.Pool(ChatLines, ColdChatLines, HotChatLines, VikingConcernSpeech.InAshlands(player));
			return(string.Format(lines[Random.Range(0, lines.Length)], player.GetPlayerName()));
		}
	}
}
