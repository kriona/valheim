using System.Collections.Generic;
using System.Text;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The events villages have - each day a village rolls for one of the events its biome has that are off their
	/// cooldown, or none, and the one it lands on starts at a time of day within the event's window (see EVENTS.md)
	/// </summary>
	/// <remarks>
	/// The client that runs a village rolls for it the first time it checks the village on a day, by the world's clock,
	/// and keeps the roll in memory - so a village no player is near has no events, and one left before its event
	/// starts misses it. The day an event starts is noted on the village's generator, where its cooldown is counted
	/// from, so an event that was missed doesn't count toward its cooldown. A village is only rolled for once it has
	/// living vikings loaded and isn't being disbanded
	/// </remarks>
	internal static class VillageEvents
	{
		/// <summary>
		/// Every event a village can have, in the order a roll goes through their chances
		/// </summary>
		public static readonly List<VillageEvent> All = new List<VillageEvent> { TravelerEvent.Event };

		/// <summary>
		/// How often the villages this client runs are checked, in seconds
		/// </summary>
		private const float CheckInterval = 2f;

		/// <summary>
		/// Where a village's event stands today
		/// </summary>
		private enum State
		{
			None,
			Waiting,
			Started,
			Missed
		}

		/// <summary>
		/// A village's roll for a day - the event it landed on and the time of day it starts, where it stands, and what
		/// the roll was for the debug text
		/// </summary>
		private class Plan
		{
			public long Day;
			public VillageEvent Event;
			public float At;
			public State State;
			public bool Forced;
			public string Rolled = "";
		}

		/// <summary>
		/// Each village's roll for today, by its farm, for the world they were made in
		/// </summary>
		private static readonly Dictionary<Vector3, Plan> plans = new Dictionary<Vector3, Plan>();
		private static ZNet plansWorld;

		/// <summary>
		/// When the villages are next checked, in Time.time seconds
		/// </summary>
		private static float nextCheck;

		/// <summary>
		/// Rolls for the day's event of each village this client runs and starts it when it's due - call it from the
		/// plugin's Update
		/// </summary>
		public static void Update()
		{
			if (Time.time < nextCheck || ZNet.instance == null || EnvMan.instance == null || !ServerCheck.Active)
			{
				return;
			}
			nextCheck = Time.time + CheckInterval;
			ForgetOtherWorlds();
			long today = EnvMan.instance.GetDay();
			float now = EnvMan.instance.GetDayFraction();
			foreach (Village village in Village.Loaded())
			{
				if (village.IsOwner())
				{
					Run(village, today, now);
				}
			}
		}

		/// <summary>
		/// Forgets the rolls made in a world that has been left
		/// </summary>
		private static void ForgetOtherWorlds()
		{
			if (plansWorld != ZNet.instance)
			{
				plans.Clear();
				plansWorld = ZNet.instance;
			}
		}

		/// <summary>
		/// Rolls for a village's event the first time it's checked today, and starts the event once its time has come,
		/// or notes it missed once its window has closed
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		/// <param name="today">Today, by the world's clock</param>
		/// <param name="now">The time of day, from 0 at midnight to 1</param>
		private static void Run(Village village, long today, float now)
		{
			if (!plans.TryGetValue(village.Farm, out Plan plan) || plan.Day != today)
			{
				if (!CanHost(village))
				{
					return;
				}
				plan = Roll(village, today, now);
				plans[village.Farm] = plan;
			}
			if (plan.State != State.Waiting)
			{
				return;
			}
			if (!plan.Forced && now >= plan.Event.WindowEnd)
			{
				plan.State = State.Missed;
				if (TestLog.On)
				{
					Plugin.Log.LogInfo("Event missed at " + VillageNames.Get(village) + ": " + plan.Event.Name + " couldn't start by " + EventRules.Clock(plan.Event.WindowEnd));
				}
				return;
			}
			if ((plan.Forced || now >= plan.At) && CanHost(village) && plan.Event.CanStart(village))
			{
				Begin(village, plan, today, now);
			}
		}

		/// <summary>
		/// Checks whether a village can have an event now - it has living vikings loaded, and isn't being disbanded
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>True when it can</returns>
		private static bool CanHost(Village village)
		{
			Fireplace fire = village.Fire();
			ZDO fireZdo = ((fire != null && fire.m_nview != null && fire.m_nview.IsValid()) ? fire.m_nview.GetZDO() : null);
			return(!VillageDisband.Gathers(fireZdo) && village.Vikings().Count > 0);
		}

		/// <summary>
		/// Rolls for a village's event today - among the events its biome has that are off their cooldown - and picks
		/// the time of day it starts, or none when the event's window has already closed
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="today">Today, by the world's clock</param>
		/// <param name="now">The time of day, from 0 at midnight to 1</param>
		/// <returns>The day's plan</returns>
		private static Plan Roll(Village village, long today, float now)
		{
			int biome = VillageCooking.BiomeAt(village.Farm);
			List<VillageEvent> open = All.FindAll((VillageEvent happening) => EventRules.InBiomes(biome, happening.EasiestBiome, happening.HardestBiome) && EventRules.OffCooldown(happening.LastDay(village), happening.CooldownDays, today));
			float roll = Random.value;
			int picked = EventRules.Pick(open.ConvertAll((VillageEvent happening) => happening.Chance), roll);
			Plan plan = new Plan { Day = today, State = State.None };
			plan.Rolled = ((open.Count == 0) ? "no event can happen here today" : "rolled " + roll.ToString("F2") + " against " + string.Join(", ", open.ConvertAll((VillageEvent happening) => happening.Name + " " + (happening.Chance * 100f).ToString("F0") + "%")));
			if (picked >= 0)
			{
				VillageEvent happening = open[picked];
				float at = EventRules.StartAt(now, happening.WindowStart, happening.WindowEnd, Random.value);
				if (at >= 0f)
				{
					plan.Event = happening;
					plan.At = at;
					plan.State = State.Waiting;
				}
				else
				{
					plan.Rolled += " - " + happening.Name + ", but too late in the day for it";
				}
			}
			if (TestLog.On)
			{
				Plugin.Log.LogInfo("Event roll at " + VillageNames.Get(village) + " for day " + today + ": " + ((plan.Event != null) ? plan.Event.Name + " at " + EventRules.Clock(plan.At) : "none") + " (" + plan.Rolled + ")");
			}
			return(plan);
		}

		/// <summary>
		/// Starts a village's event, noting the day on the village's generator for its cooldown
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		/// <param name="plan">Its plan for today</param>
		/// <param name="today">Today, by the world's clock</param>
		/// <param name="now">The time of day, from 0 at midnight to 1</param>
		private static void Begin(Village village, Plan plan, long today, float now)
		{
			plan.State = State.Started;
			plan.At = now;
			village.Zdo.Set(plan.Event.LastDayKey, today);
			Plugin.Log.LogInfo("Event at " + VillageNames.Get(village) + ": " + plan.Event.Name + " starts at " + EventRules.Clock(now) + " on day " + today);
			plan.Event.Start(village);
		}

		/// <summary>
		/// Sets a village's event for today from the console - an event to start at once, whatever its chance, cooldown,
		/// biome and window, or none
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		/// <param name="happening">The event, or null for none</param>
		public static void Force(Village village, VillageEvent happening)
		{
			if (ZNet.instance == null || EnvMan.instance == null)
			{
				return;
			}
			ForgetOtherWorlds();
			Plan plan = new Plan { Day = EnvMan.instance.GetDay(), Event = happening, At = EnvMan.instance.GetDayFraction(), State = ((happening != null) ? State.Waiting : State.None), Forced = (happening != null), Rolled = "set from the console" };
			plans[village.Farm] = plan;
			nextCheck = 0f;
		}

		/// <summary>
		/// Finds an event by its ID
		/// </summary>
		/// <param name="id">The ID, in any case</param>
		/// <returns>The event, or null when there's none</returns>
		public static VillageEvent ById(string id)
		{
			return(All.Find((VillageEvent happening) => string.Equals(happening.Id, id, System.StringComparison.OrdinalIgnoreCase)));
		}

		/// <summary>
		/// Describes a village's event today and the events still on their cooldown, for the debug text of its fire
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The lines, ending in a line break</returns>
		public static string Describe(Village village)
		{
			StringBuilder text = new StringBuilder();
			text.Append("Event today: " + DescribeToday(village) + "\n");
			if (EnvMan.instance == null || ZNet.instance == null)
			{
				return(text.ToString());
			}
			long today = EnvMan.instance.GetDay();
			List<string> cooling = new List<string>();
			foreach (VillageEvent happening in All)
			{
				long last = happening.LastDay(village);
				if (!EventRules.OffCooldown(last, happening.CooldownDays, today))
				{
					cooling.Add(happening.Name + " until day " + (last + happening.CooldownDays) + ", started day " + last);
				}
			}
			if (cooling.Count > 0)
			{
				text.Append("Event cooldowns: " + string.Join(", ", cooling) + " - today is day " + today + "\n");
			}
			return(text.ToString());
		}

		/// <summary>
		/// Describes a village's event today
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The description</returns>
		private static string DescribeToday(Village village)
		{
			if (!village.IsOwner())
			{
				return("rolled by the client that runs the village");
			}
			if (EnvMan.instance == null || ZNet.instance == null || plansWorld != ZNet.instance || !plans.TryGetValue(village.Farm, out Plan plan) || plan.Day != EnvMan.instance.GetDay())
			{
				return("not rolled yet - waiting for living vikings, and not while the village is being disbanded");
			}
			switch (plan.State)
			{
				case State.Waiting:
					return(plan.Event.Name + (plan.Forced ? " as soon as it can start" : " at " + EventRules.Clock(plan.At) + ", or as soon after as it can start until " + EventRules.Clock(plan.Event.WindowEnd)) + " (" + plan.Rolled + ")");
				case State.Started:
					return(plan.Event.Name + ", started at " + EventRules.Clock(plan.At) + " (" + plan.Rolled + ")");
				case State.Missed:
					return(plan.Event.Name + ", missed - it couldn't start by " + EventRules.Clock(plan.Event.WindowEnd) + " (" + plan.Rolled + ")");
				default:
					return("none (" + plan.Rolled + ")");
			}
		}
	}
}
