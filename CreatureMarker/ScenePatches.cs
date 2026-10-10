using HarmonyLib;

namespace CreatureMarker
{
	/// <summary>
	/// Loads the creature list once the scene's prefab list is ready
	/// </summary>
	[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
	public static class ZNetSceneAwakePatch
	{
		/// <summary>
		/// Passes the scene's prefabs to the creature list, then has the watcher take the file it writes as already read
		/// so it isn't reported as a reload
		/// </summary>
		/// <param name="__instance">The scene that just started</param>
		private static void Postfix(ZNetScene __instance)
		{
			CreatureConfig.Load(__instance);
			Plugin.Watcher?.Remember();
		}
	}
}
