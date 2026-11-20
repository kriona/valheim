using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Has Hugin tell a player about silver-lining the first time they have smelted silver
	/// </summary>
	/// <remarks>
	/// The tip is one of Hugin's tutorials, so a player sees it once and it's saved with their character. A character
	/// already carrying silver when the mod is installed gets it too
	/// </remarks>
	internal static class SilverHint
	{
		/// <summary>
		/// The tutorial's name, saved with the character once they've read it
		/// </summary>
		public const string TutorialName = "kriona.ZombieApocalypse.silverLining";

		/// <summary>
		/// Seconds between checks for silver in the player's inventory
		/// </summary>
		private const float CheckInterval = 1f;

		/// <summary>
		/// The silver bar's item name
		/// </summary>
		private const string SilverName = "$item_silver";

		private const string Topic = "Silver";
		private const string Text = "Silver! The dead cannot abide it. Bring silver and ectoplasm to a forge, and you can line your weapons with silver on its Upgrade tab. A creature slain by a silver-lined weapon stays dead.";

		private static float nextCheck;

		/// <summary>
		/// Adds the tip to Hugin's tutorials, once
		/// </summary>
		/// <param name="tutorial">The game's tutorials</param>
		private static void Register(Tutorial tutorial)
		{
			if (tutorial.m_texts.Exists((Tutorial.TutorialText text) => text.m_name == TutorialName))
			{
				return;
			}
			Tutorial.TutorialText tip = new Tutorial.TutorialText();
			tip.m_name = TutorialName;
			tip.m_topic = Topic;
			tip.m_label = Topic;
			tip.m_text = Text;
			tutorial.m_texts.Add(tip);
		}

		/// <summary>
		/// Shows the tip when the local player first has silver bars, checking every CheckInterval seconds
		/// </summary>
		/// <param name="player">The player updating</param>
		public static void Update(Player player)
		{
			if (player != Player.m_localPlayer || Time.time < nextCheck)
			{
				return;
			}
			nextCheck = Time.time + CheckInterval;
			if (!ServerCheck.Active || Tutorial.instance == null || player.HaveSeenTutorial(TutorialName))
			{
				return;
			}
			if (player.GetInventory().HaveItem(SilverName, false))
			{
				Register(Tutorial.instance);
				player.ShowTutorial(TutorialName);
			}
		}
	}
}
