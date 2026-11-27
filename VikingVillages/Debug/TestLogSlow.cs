using System.Diagnostics;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Times the lists of objects the game updates (see MonoUpdatersExtra) while a test run is going, and each AI and
	/// fixed update in them, writing any that takes SlowTime or longer, so a long frame names where it went (see
	/// TestLogAIListPatch and the patches beside it)
	/// </summary>
	internal static class TestLogSlow
	{
		/// <summary>
		/// How long a list or a single object's update takes to be written, in seconds
		/// </summary>
		public const double SlowTime = 1.0;

		/// <summary>
		/// Gets the time to start timing from, or 0 when no run is going, so nothing is timed
		/// </summary>
		/// <returns>The Stopwatch timestamp, or 0</returns>
		public static long Start()
		{
			return(TestLog.On ? Stopwatch.GetTimestamp() : 0L);
		}

		/// <summary>
		/// Works out whether something timed from a timestamp took SlowTime or longer
		/// </summary>
		/// <param name="start">When it started, as a Stopwatch timestamp, or 0 when it wasn't timed</param>
		/// <param name="end">When it ended, as a Stopwatch timestamp</param>
		/// <param name="frequency">Stopwatch timestamps a second</param>
		/// <param name="seconds">How long it took, in seconds</param>
		/// <returns>True when it was timed and took SlowTime or longer</returns>
		internal static bool IsSlow(long start, long end, long frequency, out double seconds)
		{
			seconds = ((start > 0L && frequency > 0L) ? (double)(end - start) / frequency : 0.0);
			return(seconds >= SlowTime);
		}

		/// <summary>
		/// Writes a list of objects the game updates that took SlowTime or longer
		/// </summary>
		/// <param name="scope">The list's profile scope, like "MonoUpdaters.FixedUpdate.Character"</param>
		/// <param name="count">How many objects the list had</param>
		/// <param name="start">When the list started, from Start</param>
		public static void EndList(string scope, int count, long start)
		{
			if (!IsSlow(start, Stopwatch.GetTimestamp(), Stopwatch.Frequency, out double seconds))
			{
				return;
			}
			TestLog.Count("Slow updates", scope);
			Plugin.Log.LogWarning("Slow: " + scope + " took " + (seconds * 1000.0).ToString("F0") + "ms for " + count + " objects");
		}

		/// <summary>
		/// Runs an AI's update in place of the game's call in MonoUpdatersExtra.UpdateAI, writing it when it takes SlowTime
		/// or longer
		/// </summary>
		/// <param name="item">The AI</param>
		/// <param name="deltaTime">Seconds since the AIs' last update</param>
		/// <returns>What the AI's update returns</returns>
		public static bool UpdateAI(IUpdateAI item, float deltaTime)
		{
			long start = Start();
			try
			{
				return(item.UpdateAI(deltaTime));
			}
			finally
			{
				EndObject("AI update", item, start);
			}
		}

		/// <summary>
		/// Runs an object's fixed update in place of the game's call in MonoUpdatersExtra.CustomFixedUpdate, writing it
		/// when it takes SlowTime or longer
		/// </summary>
		/// <param name="item">The object</param>
		/// <param name="deltaTime">Seconds since the last fixed update</param>
		public static void FixedUpdate(IMonoUpdater item, float deltaTime)
		{
			long start = Start();
			try
			{
				item.CustomFixedUpdate(deltaTime);
			}
			finally
			{
				EndObject("fixed update", item, start);
			}
		}

		/// <summary>
		/// Writes a single object's update that took SlowTime or longer, naming the object
		/// </summary>
		/// <param name="update">Which update it was, like "AI update"</param>
		/// <param name="item">The object updated</param>
		/// <param name="start">When its update started, from Start</param>
		private static void EndObject(string update, object item, long start)
		{
			if (start == 0L || !IsSlow(start, Stopwatch.GetTimestamp(), Stopwatch.Frequency, out double seconds))
			{
				return;
			}
			TestLog.Count("Slow updates", "single " + update + "s");
			Plugin.Log.LogWarning("Slow: the " + update + " of " + Describe(item) + " took " + (seconds * 1000.0).ToString("F0") + "ms");
		}

		/// <summary>
		/// Names an object the game updates, for the log - a creature by TestLog.Name, with a village viking's job, and
		/// anything else by its prefab, kind and position
		/// </summary>
		/// <param name="item">The object</param>
		/// <returns>The description</returns>
		private static string Describe(object item)
		{
			Component component = item as Component;
			if (component == null)
			{
				return((item != null) ? "a destroyed " + item.GetType().Name : "nothing");
			}
			Character character = component.GetComponent<Character>();
			if (character != null)
			{
				VillageLife life = character.GetComponent<VillageLife>();
				return(TestLog.Name(character) + ((life != null) ? " - " + life.LogJob() : ""));
			}
			return(Utils.GetPrefabName(component.gameObject) + " (" + item.GetType().Name + ") at " + component.transform.position.ToString("F0"));
		}
	}
}
