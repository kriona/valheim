namespace VikingVillages
{
	/// <summary>
	/// Hugin's tips about PC vikings - the first time the player's hit on one does nothing because their PvP is off,
	/// the first time the console's kill commands leave them standing because they aren't angry, and the first time the
	/// player starts a village of their own
	/// </summary>
	/// <remarks>
	/// The tips are added to the game's tutorials, so Hugin flies in with them like his own, they're kept in the
	/// compendium once read, and the player's profile remembers each was seen so it's only shown once
	/// </remarks>
	internal static class VikingPvpTip
	{
		/// <summary>
		/// The tips' names among the game's tutorials, which the player's profile remembers them by
		/// </summary>
		private const string Name = "kriona_VikingVillages_pcVikingPvp";
		private const string KillName = "kriona_VikingVillages_pcVikingKill";
		private const string FoundingName = "kriona_VikingVillages_villageFounded";

		/// <summary>
		/// The tips' title, and what Hugin says
		/// </summary>
		private const string Topic = "Living Vikings";
		private const string Text = "Not every Viking here has turned. The living ones keep to their villages, and your blows won't land on them while your PvP is off. If you truly mean them harm, turn PvP on in your inventory - but their village won't forget it soon.";
		private const string KillText = "The living Vikings still stand. Your power to strike down everything around you passes over those who bear you no anger. If you wish to slay the living Vikings as well, turn PvP on in your inventory first.";
		private const string FoundingText = "A fire is a start, but a village needs Vikings, and Vikings need somewhere to sleep. Build a bed within 30 meters of the village, under a roof with walls enough that you could sleep there yourself, and leave it unclaimed - each such bed brings a Viking from the wilds, who calls the building it stands in home. More beds, more Vikings, and the village grows to take in each new building. Give them chests and a workbench too, and they'll keep the fire stacked, repair what you build and stand together when danger comes. Claim a bed for yourself, and no one comes back in its place when its Viking falls. At the fire, press N to give your village a name, V to see how far it reaches, or Shift+V to disband it.";

		/// <summary>
		/// Has Hugin show the PvP tip to the local player, unless they've seen it
		/// </summary>
		/// <param name="player">The player whose hit did nothing</param>
		public static void Show(Player player)
		{
			Show(player, Name, Text);
		}

		/// <summary>
		/// Has Hugin show the kill commands' tip to the local player, unless they've seen it
		/// </summary>
		/// <param name="player">The player who ran the command</param>
		public static void ShowKill(Player player)
		{
			Show(player, KillName, KillText);
		}

		/// <summary>
		/// Has Hugin show the tip about the beds a village needs to the local player, unless they've seen it
		/// </summary>
		/// <param name="player">The player who started a village</param>
		public static void ShowFounding(Player player)
		{
			Show(player, FoundingName, FoundingText);
		}

		/// <summary>
		/// Has Hugin show a tip to the local player, adding it to the game's tutorials the first time, unless they've
		/// seen it
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="name">The tip's name</param>
		/// <param name="text">What Hugin says</param>
		private static void Show(Player player, string name, string text)
		{
			if (player == null || player != Player.m_localPlayer || Tutorial.instance == null || player.HaveSeenTutorial(name))
			{
				return;
			}
			if (!Tutorial.instance.m_texts.Exists((Tutorial.TutorialText tutorial) => tutorial.m_name == name))
			{
				Tutorial.TutorialText tip = new Tutorial.TutorialText();
				tip.m_name = name;
				tip.m_topic = Topic;
				tip.m_label = Topic;
				tip.m_text = text;
				Tutorial.instance.m_texts.Add(tip);
			}
			player.ShowTutorial(name);
		}
	}
}
