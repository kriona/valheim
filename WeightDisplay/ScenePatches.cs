using HarmonyLib;

namespace WeightDisplay
{
	/// <summary>
	/// Reloads the config file each time a world loads
	/// </summary>
	[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
	public static class ZNetSceneAwakePatch
	{
		/// <summary>
		/// Reads the config file again as the world starts loading
		/// </summary>
		private static void Postfix()
		{
			Plugin.ReloadConfig();
		}
	}
}
