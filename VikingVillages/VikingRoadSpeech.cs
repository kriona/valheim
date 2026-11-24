namespace VikingVillages
{
	/// <summary>
	/// What living vikings say away from any village - lost after being spawned out in the wild, on the road to a
	/// village, or wandering with none to go to - and as they leave a village a player disbanded (see VillageLifeTravel)
	/// </summary>
	/// <remarks>
	/// Every line is read by string.Format, with "{0}" the player's name where a line has one, and "{village}" is the
	/// village the line is about - filled in before, by VikingSpeech.Pick
	/// </remarks>
	internal static class VikingRoadSpeech
	{
		/// <summary>
		/// What a viking says to itself while it's lost, just after being spawned out in the wild
		/// </summary>
		internal static readonly string[] ConfusedLines =
		{
			"Where am I?",
			"How did I get here?",
			"What's going on?",
			"Am I in Valhalla?",
			"This isn't my bed...",
			"The last thing I remember is the mead hall...",
			"Odin's beard... what is this place?",
			"Which way is home?"
		};

		/// <summary>
		/// What a lost viking says when a player it knows talks to it
		/// </summary>
		internal static readonly string[] ConfusedAnswers =
		{
			"{0}? I... I don't know where I am.",
			"Give me a moment, {0}. My head's spinning.",
			"Is this Midgard, {0}? Tell me this is Midgard.",
			"I don't remember coming here, {0}. Any of it."
		};

		/// <summary>
		/// How a lost viking greets a player it has never met
		/// </summary>
		internal static readonly string[] ConfusedGreetings =
		{
			"You there! Do you know where this is?",
			"A living face! Tell me, where am I?",
			"Hail, stranger... is this Valhalla?",
			"Who are you? Where is everyone?"
		};

		/// <summary>
		/// How a viking out on the road greets a player it has never met, by day and by night
		/// </summary>
		internal static readonly string[] DayRoadGreetings =
		{
			"Hail, traveler. The road's lonely out here.",
			"Well met, stranger. Mind the woods.",
			"Hail! I didn't expect to meet anyone out here.",
			"A friendly face, at last. Hail, traveler."
		};
		internal static readonly string[] NightRoadGreetings =
		{
			"Hail, stranger. Bad night to be out on the road.",
			"Who goes there? ...A traveler, like me. Well met.",
			"Keep your torch close, stranger. The night's long out here."
		};

		/// <summary>
		/// How a viking out on the road greets a player it knows
		/// </summary>
		internal static readonly string[] KnownRoadGreetings =
		{
			"{0}! Good to see a friend out here.",
			"Hail, {0}. Strange meeting you on the road.",
			"{0}, well met! Walk with me a while?",
			"Ah, {0}. The road's less lonely with you on it."
		};

		/// <summary>
		/// What a viking on its way to a village says to itself now and then with a player near - with the village's name,
		/// and when it has none
		/// </summary>
		internal static readonly string[] TravelLines =
		{
			"I need to get to {village}.",
			"{village} can't be much farther.",
			"Keep moving. {village} is waiting.",
			"They say {village} has a warm fire.",
			"One more hill, and then {village}. I hope."
		};
		internal static readonly string[] UnnamedTravelLines =
		{
			"I need to get to a village.",
			"There's a village near here somewhere.",
			"Keep moving. A fire and a roof, that's all I ask.",
			"A village can't be much farther."
		};

		/// <summary>
		/// What a viking on its way to a village says when a player it knows talks to it - with the village's name, and
		/// when it has none
		/// </summary>
		internal static readonly string[] TravelAnswers =
		{
			"I'm making for {village}, {0}. Do you know the way?",
			"Can't stop long, {0}. {village} is waiting.",
			"Have you been to {village}, {0}? That's where I'm headed.",
			"Walk with me to {village}, {0}? The road's safer with two."
		};
		internal static readonly string[] UnnamedTravelAnswers =
		{
			"I'm looking for a village, {0}. Any village.",
			"There's a village ahead, {0}. I mean to sleep there tonight.",
			"Can't stop long, {0}. I need a roof over my head."
		};

		/// <summary>
		/// What a viking with no village to go to says to itself now and then with a player near, and when a player it
		/// knows talks to it
		/// </summary>
		internal static readonly string[] WanderLines =
		{
			"Nothing but wilderness...",
			"Is there no one left out here?",
			"I'll find a fire somewhere.",
			"There has to be a village somewhere."
		};
		internal static readonly string[] WanderAnswers =
		{
			"I've been wandering for days, {0}. No village anywhere.",
			"Do you know of a village near here, {0}? I can't find one.",
			"No home, no hall, {0}. Just the road.",
			"If you see a village, {0}, tell them there's one more coming."
		};

		/// <summary>
		/// What a viking on the road says as it reaches the sea and sets sail, with "{0}" the boat it sails in
		/// </summary>
		internal static readonly string[] SailLines =
		{
			"Time to find a {0}.",
			"From here it's a {0} across the water.",
			"The rest of the way is by sea.",
			"I'll take a {0} from here. Odin grant us a fair wind."
		};

		/// <summary>
		/// What a viking on the road says as it reaches a portal on its way and steps through
		/// </summary>
		internal static readonly string[] PortalLines =
		{
			"Through the portal - it's quicker than walking.",
			"Hold your breath, everyone. In we go!",
			"I'll never get used to these things.",
			"See you on the other side!",
			"Keep together - the portal won't wait."
		};

		/// <summary>
		/// What a viking says as it reaches a farm on its way that no one lives in, and moves on
		/// </summary>
		internal static readonly string[] DeadEndLines =
		{
			"Nobody lives here... on to the next one.",
			"Empty. Not a soul. I'll have to keep going.",
			"This place is abandoned. Where now?",
			"No fire, no folk. This isn't the place."
		};

		/// <summary>
		/// What the vikings of a village a player disbanded say as they gather at its fire - with the village's name, and
		/// when it has none
		/// </summary>
		internal static readonly string[] DisbandedLines =
		{
			"So that's it for {village}?",
			"I can't believe {village} is finished.",
			"All that work on {village}, for nothing.",
			"Everyone, to the fire! {village} is done.",
			"I liked it in {village}.",
			"Gather round. We need to talk about {village}."
		};
		internal static readonly string[] UnnamedDisbandedLines =
		{
			"So that's it for the village?",
			"I can't believe it's over.",
			"All that work, for nothing.",
			"Everyone, to the fire!",
			"I liked it here.",
			"Gather round. We need to talk."
		};

		/// <summary>
		/// What the vikings of a disbanded village say once they're all at the fire, before one of them says where they're
		/// going
		/// </summary>
		internal static readonly string[] WhatNowLines =
		{
			"What now?",
			"Where should we go?",
			"Is there another village near here?",
			"We can't stay here.",
			"We'll need to find a new fire.",
			"Someone must have room for us."
		};

		/// <summary>
		/// What one of the vikings of a disbanded village says to the rest as they set out for another village - with its
		/// name, and when it has none
		/// </summary>
		internal static readonly string[] LetsGoLines =
		{
			"Let's go to {village}.",
			"{village} isn't far. Let's go!",
			"We'll make for {village}. Come on!"
		};
		internal static readonly string[] UnnamedLetsGoLines =
		{
			"Let's find another village.",
			"There's another village out there. Let's go!"
		};

		/// <summary>
		/// Picks a line from a table with a village's name, or from its table for when there's none
		/// </summary>
		/// <param name="named">The lines with "{village}" in them</param>
		/// <param name="unnamed">The lines for when the village has no name</param>
		/// <param name="village">The village's name, or ""</param>
		/// <returns>The line, for string.Format</returns>
		private static string PickFor(string[] named, string[] unnamed, string village)
		{
			return((village.Length > 0) ? VikingSpeech.Pick(named, village) : VikingSpeech.Pick(unnamed, ""));
		}

		/// <summary>
		/// Picks what a lost viking says to itself, other than a line it just said
		/// </summary>
		/// <param name="last">The line it said last, or null</param>
		/// <returns>The line</returns>
		public static string Confused(string last)
		{
			string line = VikingSpeech.Pick(ConfusedLines, "");
			for (int i = 0; i < 4 && line == last; i++)
			{
				line = VikingSpeech.Pick(ConfusedLines, "");
			}
			return(string.Format(line));
		}

		/// <summary>
		/// Picks how a viking away from any village greets a player - lost or out on the road, by name for a player it
		/// knows, and with its own name for one it has never met
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="night">Whether it's night</param>
		/// <param name="known">Whether it knows them</param>
		/// <param name="self">Its name, or ""</param>
		/// <param name="confused">Whether it's lost</param>
		/// <returns>The line</returns>
		public static string Greeting(Player player, bool night, bool known, string self, bool confused)
		{
			if (known)
			{
				string[] table = (confused ? ConfusedAnswers : KnownRoadGreetings);
				return(string.Format(VikingSpeech.Pick(table, ""), player.GetPlayerName()));
			}
			string first = string.Format(VikingSpeech.Pick(confused ? ConfusedGreetings : (night ? NightRoadGreetings : DayRoadGreetings), ""));
			return(VikingSpeech.Introduce(first, self));
		}

		/// <summary>
		/// Picks what a viking away from any village says when a player it knows talks to it - lost, on its way to a
		/// village, or wandering with none to go to
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="confused">Whether it's lost</param>
		/// <param name="travelling">Whether it's on its way to a village</param>
		/// <param name="village">The village it's on its way to, or ""</param>
		/// <returns>The line</returns>
		public static string Answer(Player player, bool confused, bool travelling, string village)
		{
			string line;
			if (confused)
			{
				line = VikingSpeech.Pick(ConfusedAnswers, "");
			}
			else if (travelling)
			{
				line = PickFor(TravelAnswers, UnnamedTravelAnswers, village);
			}
			else
			{
				line = VikingSpeech.Pick(WanderAnswers, "");
			}
			return(string.Format(line, player.GetPlayerName()));
		}

		/// <summary>
		/// Picks what a viking on its way to a village says to itself
		/// </summary>
		/// <param name="village">The village's name, or ""</param>
		/// <returns>The line</returns>
		public static string Travelling(string village)
		{
			return(string.Format(PickFor(TravelLines, UnnamedTravelLines, village)));
		}

		/// <summary>
		/// Picks what a viking with no village to go to says to itself
		/// </summary>
		/// <returns>The line</returns>
		public static string Wandering()
		{
			return(string.Format(VikingSpeech.Pick(WanderLines, "")));
		}

		/// <summary>
		/// Picks what a viking on the road says as it reaches the sea and sets sail
		/// </summary>
		/// <param name="boat">The boat it sails in, like "longship"</param>
		/// <returns>The line</returns>
		public static string Sail(string boat)
		{
			return(string.Format(VikingSpeech.Pick(SailLines, ""), boat));
		}

		/// <summary>
		/// Picks what a viking on the road says as it steps through a portal
		/// </summary>
		/// <returns>The line</returns>
		public static string Portal()
		{
			return(VikingSpeech.Pick(PortalLines, ""));
		}

		/// <summary>
		/// Picks what a viking says as it reaches a farm no one lives in
		/// </summary>
		/// <returns>The line</returns>
		public static string DeadEnd()
		{
			return(string.Format(VikingSpeech.Pick(DeadEndLines, "")));
		}

		/// <summary>
		/// Picks what a viking of a disbanded village says as they gather at its fire
		/// </summary>
		/// <param name="village">The disbanded village's name, or ""</param>
		/// <returns>The line</returns>
		public static string Disbanded(string village)
		{
			return(string.Format(PickFor(DisbandedLines, UnnamedDisbandedLines, village)));
		}

		/// <summary>
		/// Picks what a viking of a disbanded village says once they're all at its fire
		/// </summary>
		/// <returns>The line</returns>
		public static string WhatNow()
		{
			return(string.Format(VikingSpeech.Pick(WhatNowLines, "")));
		}

		/// <summary>
		/// Picks what one of the vikings of a disbanded village says as they set out for another village
		/// </summary>
		/// <param name="village">The village they set out for, or ""</param>
		/// <returns>The line</returns>
		public static string LetsGo(string village)
		{
			return(string.Format(PickFor(LetsGoLines, UnnamedLetsGoLines, village)));
		}
	}
}
