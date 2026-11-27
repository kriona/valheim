using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What PC and dvergr vikings say to a player they know about how they're doing, and to an old friend about how
	/// long they go back (see VikingIdentity.OldFriends)
	/// </summary>
	internal static class VikingFriendSpeech
	{
		/// <summary>
		/// How likely a viking greets an old friend or answers them as one, and asks a player it knows how they're doing
		/// when it doesn't
		/// </summary>
		private const float FriendChance = 0.35f;
		private const float AskChance = 0.2f;

		/// <summary>
		/// What a viking says to greet an old friend, with {0} for the player's name
		/// </summary>
		internal static readonly string[] FriendGreetings =
		{
			"{0}, old friend! Welcome back!",
			"There's my old friend! Hail, {0}!",
			"{0}! I'd know that face anywhere.",
			"Hail, {0}! It's always good to see an old friend."
		};

		/// <summary>
		/// What a viking says when an old friend talks to it, with {0} for the player's name
		/// </summary>
		internal static readonly string[] FriendChat =
		{
			"How long have we known each other now, {0}? Feels like forever.",
			"You and me, {0} - we go back a long way.",
			"There's always a seat by my fire for you, {0}. Always has been.",
			"I remember the day we met, {0}. Look at you now.",
			"Old friends like us don't need many words, {0}.",
			"When the skalds sing of you, {0}, I'll tell them I knew you first."
		};

		/// <summary>
		/// What a viking asks a player it knows about how they're doing, with {0} for the player's name
		/// </summary>
		internal static readonly string[] AskChat =
		{
			"How's the adventuring going, {0}?",
			"Been anywhere new lately, {0}?",
			"Keeping well, {0}? You look tired.",
			"Find anything worth the trouble out there, {0}?",
			"How are you holding up, {0}? Truly.",
			"Eating well, {0}? You can't fight on an empty belly.",
			"Any new scars to show me, {0}?"
		};

		/// <summary>
		/// Picks how a viking greets a player it knows when they're old friends - now and then
		/// </summary>
		/// <param name="friends">Whether they're old friends</param>
		/// <param name="player">The player's name</param>
		/// <returns>The line, or null when it greets them some other way</returns>
		public static string Greeting(bool friends, string player)
		{
			if (!friends || Random.value >= FriendChance)
			{
				return(null);
			}
			return(Fill(FriendGreetings, player));
		}

		/// <summary>
		/// Picks what a viking says when a player it knows talks to it - now and then about being old friends, when they
		/// are, or else asking how they're doing
		/// </summary>
		/// <param name="friends">Whether they're old friends</param>
		/// <param name="player">The player's name</param>
		/// <returns>The line, or null when it says something else</returns>
		public static string Chat(bool friends, string player)
		{
			if (friends && Random.value < FriendChance)
			{
				return(Fill(FriendChat, player));
			}
			if (Random.value < AskChance)
			{
				return(Fill(AskChat, player));
			}
			return(null);
		}

		/// <summary>
		/// Picks one of a set of lines at random, filling in the player's name
		/// </summary>
		/// <param name="lines">The lines, with {0} for the player's name</param>
		/// <param name="player">The player's name</param>
		/// <returns>The line</returns>
		private static string Fill(string[] lines, string player)
		{
			return(string.Format(lines[Random.Range(0, lines.Length)], player));
		}
	}
}
