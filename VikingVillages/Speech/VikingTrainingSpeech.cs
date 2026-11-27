using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What a living viking says to a player with one of the game's wooden training weapons in hand - encouraging them,
	/// glad to see them keep their skill sharp, and training too - as it calls out to them (see VikingConcernSpeech)
	/// and now and then when they talk to it. The lines are in DIALOG.md too
	/// </summary>
	internal static class VikingTrainingSpeech
	{
		/// <summary>
		/// How likely a viking talks about it when a player it knows talks to it with a training weapon in hand
		/// </summary>
		private const float ChatChance = 0.3f;

		/// <summary>
		/// The prefabs of the game's wooden training weapons
		/// </summary>
		internal static readonly HashSet<string> TrainingWeapons = new HashSet<string>
		{
			"SwordWood", "THSwordWood", "AxeWood", "BattleaxeWood", "MaceWood", "SledgeWood", "KnifeWood", "SpearWood", "AtgeirWood"
		};

		/// <summary>
		/// What a viking calls the skill a weapon trains, by the skill, as the lines say it after "your"
		/// </summary>
		internal static readonly Dictionary<Skills.SkillType, string> SkillPhrases = new Dictionary<Skills.SkillType, string>
		{
			{ Skills.SkillType.Swords, "swordplay" },
			{ Skills.SkillType.Axes, "axe work" },
			{ Skills.SkillType.Clubs, "club work" },
			{ Skills.SkillType.Knives, "knife work" },
			{ Skills.SkillType.Spears, "spear work" },
			{ Skills.SkillType.Polearms, "polearm work" }
		};

		/// <summary>
		/// What a viking calls a skill it has no phrase for
		/// </summary>
		internal const string AnySkill = "fighting";

		/// <summary>
		/// What a viking calls out to a player it sees with a training weapon in hand, with {0} for the player's name
		/// and {1} for the skill it trains (see SkillPhrases)
		/// </summary>
		internal static readonly string[] CallLines =
		{
			"It's good to keep your {1} sharp, {0}.",
			"Training, {0}? Me too, when the chores let me.",
			"Good form, {0}! Keep at it.",
			"Practicing your {1}, {0}? So am I, whenever I get the chance.",
			"That's the way, {0}. Practice makes the warrior."
		};

		/// <summary>
		/// What a viking says to a player it knows who talks to it with a training weapon in hand, with {0} for the
		/// player's name and {1} for the skill it trains
		/// </summary>
		internal static readonly string[] ChatLines =
		{
			"I saw you training, {0}. It's good to keep your {1} sharp.",
			"I'm training too, {0}. There's always more to learn.",
			"Your {1} is coming along, {0}. Keep at it.",
			"Every warrior starts with a wooden weapon, {0}, and the best keep practicing.",
			"A little practice every day, {0}. That's how I keep my {1} from going rusty."
		};

		/// <summary>
		/// Checks whether a player has a training weapon in hand
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>True when they do</returns>
		public static bool Training(Player player)
		{
			return(TrainingWeapon(player) != null);
		}

		/// <summary>
		/// Gets what a viking calls the skill the training weapon in a player's hand trains
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>The skill, like "swordplay"</returns>
		public static string SkillOf(Player player)
		{
			ItemDrop.ItemData weapon = TrainingWeapon(player);
			return(Phrase((weapon != null) ? weapon.m_shared.m_skillType : Skills.SkillType.None));
		}

		/// <summary>
		/// Gets what a viking calls a skill
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <returns>The phrase, like "swordplay", or AnySkill for one it has none for</returns>
		internal static string Phrase(Skills.SkillType skill)
		{
			return(SkillPhrases.TryGetValue(skill, out string phrase) ? phrase : AnySkill);
		}

		/// <summary>
		/// Checks whether an item is one of the game's wooden training weapons, by its prefab
		/// </summary>
		/// <param name="prefab">The item's prefab name, or null</param>
		/// <returns>True when it is</returns>
		internal static bool IsTrainingWeapon(string prefab)
		{
			return(prefab != null && TrainingWeapons.Contains(prefab));
		}

		/// <summary>
		/// Picks what a viking says to a player it knows who talks to it with a training weapon in hand, now and then
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>The line, or null when it says something else</returns>
		public static string Chat(Player player)
		{
			if (!Training(player) || Random.value >= ChatChance)
			{
				return(null);
			}
			return(string.Format(ChatLines[Random.Range(0, ChatLines.Length)], player.GetPlayerName(), SkillOf(player)));
		}

		/// <summary>
		/// Finds the training weapon in a player's hands
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>The weapon, or null when they hold none</returns>
		private static ItemDrop.ItemData TrainingWeapon(Player player)
		{
			foreach (ItemDrop.ItemData item in new[] { player.m_rightItem, player.m_leftItem })
			{
				if (item != null && item.m_dropPrefab != null && IsTrainingWeapon(item.m_dropPrefab.name))
				{
					return(item);
				}
			}
			return(null);
		}
	}
}
