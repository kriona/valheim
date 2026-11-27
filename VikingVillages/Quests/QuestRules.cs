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
		/// How much longer a quest that sails from its village takes to set out, in seconds - for its vikings to build
		/// any boats they need, get on board and sail out of sight - added to when it's due back and to how long its
		/// vikings have to leave
		/// </summary>
		public const float SailOffSeconds = 90f;

		/// <summary>
		/// How long after a village's last quest came back it waits before it sends out another, as a share of a day
		/// </summary>
		public const float RestBetween = 0.25f;

		/// <summary>
		/// How long the trip to a quest takes each way when the server can't work it out, in seconds
		/// </summary>
		public const float DefaultTravel = 300f;

		/// <summary>
		/// How long a village waits for the server's answer about a quest's road before it asks again, in seconds - long
		/// enough for the server's searches waiting ahead of it to run
		/// </summary>
		public const float RouteAnswerWait = 300f;

		/// <summary>
		/// How a hunt goes - the fewest and most prey each hunter takes, how long each takes, in seconds, and how far
		/// through the day night falls, which the hunters are always home before (see EnvMan.CalculateNight)
		/// </summary>
		public const int HuntKillsLeast = 3;
		public const int HuntKillsMost = 10;
		public const float HuntKillSeconds = 90f;
		public const float NightFalls = 0.75f;

		/// <summary>
		/// Works out how long it is until night falls
		/// </summary>
		/// <param name="dayFraction">How far through the day it is, from 0 at midnight</param>
		/// <param name="dayLength">How long a game day is, in seconds</param>
		/// <returns>How long, in seconds, or 0 once it's night</returns>
		public static float SecondsToNight(float dayFraction, float dayLength)
		{
			return(System.Math.Max(0f, (NightFalls - dayFraction) * dayLength));
		}

		/// <summary>
		/// Whether a village asks the server about a quest's road - when what it said is missing or stale, and the
		/// village hasn't asked, or asked RouteAnswerWait ago or longer with no answer, as an answer can be lost
		/// </summary>
		/// <param name="fresh">Whether the village has the server's answer, and it's recent enough to go by</param>
		/// <param name="asked">Whether the village asked and hasn't heard back</param>
		/// <param name="askedAt">When it asked, in seconds</param>
		/// <param name="now">The time now, in seconds</param>
		/// <returns>Whether to ask</returns>
		public static bool AskForRoute(bool fresh, bool asked, float askedAt, float now)
		{
			return(!fresh && (!asked || now - askedAt >= RouteAnswerWait));
		}

		/// <summary>
		/// How foraging goes - the fewest and most plants each forager picks and how long each takes, in seconds - and the
		/// least time cutting trees is worth going for, past finding them, in seconds
		/// </summary>
		public const int ForagePicksLeast = 4;
		public const int ForagePicksMost = 12;
		public const float ForagePickSeconds = 75f;
		public const float TreesLeastSeconds = 60f;

		/// <summary>
		/// Works out how many plants each forager picked on a foraging quest from how long they were at it
		/// </summary>
		/// <param name="activeSeconds">How long they foraged, in seconds</param>
		/// <returns>How many plants</returns>
		public static int ForagePicks(float activeSeconds)
		{
			return(System.Math.Max(0, (int)System.Math.Round(activeSeconds / ForagePickSeconds)));
		}

		/// <summary>
		/// Checks whether a quest is always home before night - hunting, foraging and cutting trees, the everyday quests
		/// near home, which do as much as the daylight left allows
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <returns>True when it is</returns>
		public static bool HomeByNight(QuestKind kind)
		{
			return(kind == QuestKind.Hunt || kind == QuestKind.Forage || kind == QuestKind.ChopTrees);
		}

		/// <summary>
		/// Works out how long a quest can work and still be home before night - the time until night with the trip there
		/// and back taken off
		/// </summary>
		/// <param name="toNight">How long it is until night falls, in seconds</param>
		/// <param name="there">How long the trip there takes, in seconds</param>
		/// <param name="back">How long the trip back takes, in seconds</param>
		/// <returns>How long, in seconds, or 0 when there's no time at all</returns>
		public static float WorkBeforeNight(float toNight, float there, float back)
		{
			return(System.Math.Max(0f, toNight - System.Math.Max(0f, there) - System.Math.Max(0f, back)));
		}

		/// <summary>
		/// Works out the least work a quest that's home by night is worth setting out for - HuntKillsLeast prey each,
		/// ForagePicksLeast plants each, or finding the trees and TreesLeastSeconds of cutting
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <returns>How long, in seconds, or 0 for a quest that isn't home by night</returns>
		public static float LeastWork(QuestKind kind)
		{
			switch (kind)
			{
				case QuestKind.Hunt:
					return(HuntKillsLeast * HuntKillSeconds);
				case QuestKind.Forage:
					return(ForagePicksLeast * ForagePickSeconds);
				case QuestKind.ChopTrees:
					return(ActiveSeconds(kind) + TreesLeastSeconds);
				default:
					return(0f);
			}
		}

		/// <summary>
		/// Works out how many of something each viking can do in the time it has, at so long each, no more than a most
		/// </summary>
		/// <param name="work">How long it has, in seconds</param>
		/// <param name="each">How long each takes, in seconds</param>
		/// <param name="most">The most it does</param>
		/// <returns>How many</returns>
		private static int CountIn(float work, float each, int most)
		{
			double count = System.Math.Floor(work / each);
			return((int)System.Math.Max(0.0, System.Math.Min(most, count)));
		}

		/// <summary>
		/// Works out how many prey each hunter can take and still be home before night, at HuntKillSeconds a prey, no
		/// more than HuntKillsMost
		/// </summary>
		/// <param name="toNight">How long it is until night falls, in seconds</param>
		/// <param name="there">How long the trip there takes, in seconds</param>
		/// <param name="back">How long the trip back takes, in seconds</param>
		/// <returns>How many, fewer than HuntKillsLeast when there isn't time for a hunt today</returns>
		public static int HuntKillsBeforeNight(float toNight, float there, float back)
		{
			return(CountIn(WorkBeforeNight(toNight, there, back), HuntKillSeconds, HuntKillsMost));
		}

		/// <summary>
		/// Works out how many plants each forager can pick and still be home before night, at ForagePickSeconds a plant,
		/// no more than ForagePicksMost
		/// </summary>
		/// <param name="toNight">How long it is until night falls, in seconds</param>
		/// <param name="there">How long the trip there takes, in seconds</param>
		/// <param name="back">How long the trip back takes, in seconds</param>
		/// <returns>How many, fewer than ForagePicksLeast when there isn't time to forage today</returns>
		public static int ForagePicksBeforeNight(float toNight, float there, float back)
		{
			return(CountIn(WorkBeforeNight(toNight, there, back), ForagePickSeconds, ForagePicksMost));
		}

		/// <summary>
		/// How long the work of each quest takes once the vikings are there, the way a player would take, in seconds -
		/// mining and cutting trees are worked out from what the vikings can carry and their tools instead (see
		/// QuestSim.Gather), this being what's left for finding the ore or trees
		/// </summary>
		private static readonly Dictionary<QuestKind, float> ActiveTimes = new Dictionary<QuestKind, float>
		{
			{ QuestKind.Hunt, 900f },
			{ QuestKind.Forage, ForagePicksMost * ForagePickSeconds },
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
			{ QuestKind.BuyBelt, 120f },
			{ QuestKind.BuyWiderPockets, 120f },
			{ QuestKind.BuyDeeperPockets, 120f },
			{ QuestKind.KillEikthyr, 300f },
			{ QuestKind.FindBees, 600f },
			{ QuestKind.HuntGhosts, 900f },
			{ QuestKind.RaidFrostCaves, 900f }
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
		/// takes a tool while the village has more of them, or for one whose extras join whenever they can be spared, up
		/// to the quest's most - as long as MinHome stay home
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
			int most = rule.MostFor(biome);
			int size = (rule.ExtrasJoin ? most : System.Math.Min(most, System.Math.Max(wanted, tools)));
			size = System.Math.Min(size, System.Math.Min(available, village - MinHome));
			return((size >= wanted) ? size : 0);
		}
	}
}
