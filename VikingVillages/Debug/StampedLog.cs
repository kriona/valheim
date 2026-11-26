using BepInEx.Logging;

namespace VikingVillages
{
	/// <summary>
	/// The plugin's log, which puts the time and the game's day in front of each line while a test run is going (see
	/// TestLog), so a long session's lines can be lined up with each other
	/// </summary>
	internal class StampedLog
	{
		/// <summary>
		/// The BepInEx log the lines go to
		/// </summary>
		private readonly ManualLogSource source;

		/// <summary>
		/// Wraps the plugin's BepInEx log
		/// </summary>
		/// <param name="source">The BepInEx log</param>
		public StampedLog(ManualLogSource source)
		{
			this.source = source;
		}

		/// <summary>
		/// Writes a line at the info level
		/// </summary>
		/// <param name="data">The line</param>
		public void LogInfo(object data)
		{
			source.LogInfo(TestLog.Stamp() + data);
		}

		/// <summary>
		/// Writes a line at the warning level
		/// </summary>
		/// <param name="data">The line</param>
		public void LogWarning(object data)
		{
			source.LogWarning(TestLog.Stamp() + data);
		}

		/// <summary>
		/// Writes a line at the error level
		/// </summary>
		/// <param name="data">The line</param>
		public void LogError(object data)
		{
			source.LogError(TestLog.Stamp() + data);
		}
	}
}
