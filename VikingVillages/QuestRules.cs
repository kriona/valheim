using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// The rules of when a village sends vikings on a quest, how many go and how long they're gone - worked out apart
	/// from the game, so they can be tested
	/// </summary>
	internal static class QuestRules
	{
		/// <summary>
		/// The fewest vikings that stay home while a quest is out
		/// </summary>
		public const int MinHome = 2;

		/// <summary>
		/// How late in the day a quest can still set out, as a share of the day - later than that, the vikings picked
		/// wait for the next morning - and how early in the day it can set out, as the vikings wake
		/// </summary>
		public const float StartCutoff = 0.6f;
		public const float StartEarliest = 0.24f;

		/// <summary>
		/// How much a quest's time is randomized either way, as a share of it
		/// </summary>
		public const float Jitter = 0.02f;

		/// <summary>
		/// How long a viking leaving on a quest has to get out of the village before it goes from wherever it is, in
		/// seconds, so one that's stuck still goes
		/// </summary>
		public const float LeaveTimeout = 90f;

		/// <summary>
		/// How long after a village's last quest came back it waits before it sends out another, as a share of a day
		/// </summary>
		public const float RestBetween = 0.25f;

		/// <summary>
		/// How long the trip to a quest takes each way when the server can't work it out, in seconds
		/// </summary>
		public const float DefaultTravel = 300f;

		/// <summary>
		/// How long the work of each quest takes once the vikings are there, the way a player would take, in seconds -
		/// mining and cutting trees are worked out from what the vikings can carry and their tools instead (see
		/// QuestSim.Gather), this being what's left for finding the ore or trees
		/// </summary>
		private static readonly Dictionary<QuestKind, float> ActiveTimes = new Dictionary<QuestKind, float>
		{
			{ QuestKind.Hunt, 900f },
			{ QuestKind.Forage, 900f },
			{ QuestKind.ChopTrees, 120f },
			{ QuestKind.MineCopper, 180f },
			{ QuestKind.MineTin, 180f },
			{ QuestKind.MineSilver, 600f },
			{ QuestKind.RaidCrypts, 1800f },
			{ QuestKind.RaidBurialChambers, 900f },
			{ QuestKind.HuntTrolls, 900f },
			{ QuestKind.HuntGoblins, 1200f },
			{ QuestKind.SellTreasure, 120f },
			{ QuestKind.BuyCirclet, 120f },
			{ QuestKind.KillEikthyr, 300f },
			{ QuestKind.FindBees, 600f },
			{ QuestKind.HuntGhosts, 900f }
		};

		/// <summary>
		/// Gets how long the work of a quest takes once the vikings are there (see ActiveTimes)
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <returns>The time, in seconds</returns>
		public static float ActiveSeconds(QuestKind kind)
		{
			return(ActiveTimes.TryGetValue(kind, out float seconds) ? seconds : 600f);
		}

		/// <summary>
		/// Checks whether a quest is worth the village talking about - any but hunting, foraging or cutting trees, which
		/// are too ordinary
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <returns>True when it's worth talking about</returns>
		public static bool WorthTalkingAbout(QuestKind kind)
		{
			return(kind != QuestKind.None && kind != QuestKind.Hunt && kind != QuestKind.Forage && kind != QuestKind.ChopTrees);
		}

		/// <summary>
		/// Checks whether a quest can set out at a time of day - after the vikings wake, and no later than StartCutoff
		/// </summary>
		/// <param name="dayFraction">How far through the day it is, from 0 at midnight</param>
		/// <returns>True when it can</returns>
		public static bool CanSetOut(float dayFraction)
		{
			return(dayFraction >= StartEarliest && dayFraction < StartCutoff);
		}

		/// <summary>
		/// Works out how long a quest is gone - there and back, and the work there - randomized by up to Jitter either way
		/// </summary>
		/// <param name="there">How long the trip there takes, in seconds</param>
		/// <param name="back">How long the trip back takes, in seconds</param>
		/// <param name="active">How long the work takes, in seconds</param>
		/// <param name="roll">A random roll from -1 to 1</param>
		/// <returns>The time, in seconds</returns>
		public static float Duration(float there, float back, float active, float roll)
		{
			float total = System.Math.Max(0f, there) + System.Math.Max(0f, back) + System.Math.Max(0f, active);
			float clamped = System.Math.Max(-1f, System.Math.Min(1f, roll));
			return(total * (1f + Jitter * clamped));
		}

		/// <summary>
		/// Works out how many vikings go on a quest - the quest's own number for the village's biome, more for one that
		/// takes a tool while the village has more of them, up to the quest's most - as long as MinHome stay home
		/// </summary>
		/// <param name="rule">The quest's rule</param>
		/// <param name="biome">The village's biome, by VikingGear's order</param>
		/// <param name="tools">How many good enough tools the village has for a quest that takes one, or 0</param>
		/// <param name="available">How many of the village's vikings are fit to go</param>
		/// <param name="village">How many vikings the village has, all told</param>
		/// <returns>How many go, or 0 when the village can't spare enough</returns>
		public static int GroupSize(QuestRule rule, int biome, int tools, int available, int village)
		{
			int wanted = rule.VikingsFor(biome);
			int size = System.Math.Min(rule.MaxVikings, System.Math.Max(wanted, tools));
			size = System.Math.Min(size, System.Math.Min(available, village - MinHome));
			return((size >= wanted) ? size : 0);
		}
	}
}
