using BepInEx.Logging;

namespace Kriona.Vikings
{
	/// <summary>
	/// The mod the shared viking code is compiled into - Viking Villages or Zombie Apocalypse - which sets itself here
	/// first thing as it loads, so the shared code logs to its log and names its messages after it
	/// </summary>
	internal static class VikingMod
	{
		/// <summary>
		/// The mod's GUID, display name and version
		/// </summary>
		public static string Guid = "";
		public static string Name = "";
		public static string Version = "";

		/// <summary>
		/// The mod's log
		/// </summary>
		public static ManualLogSource Log;

		/// <summary>
		/// Sets the mod the shared code is running in
		/// </summary>
		/// <param name="guid">Its GUID</param>
		/// <param name="name">Its display name</param>
		/// <param name="version">Its version</param>
		/// <param name="log">Its log</param>
		public static void Init(string guid, string name, string version, ManualLogSource log)
		{
			Guid = guid;
			Name = name;
			Version = version;
			Log = log;
		}
	}
}
