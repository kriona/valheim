using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Registers the anchor of the villages players start in each world's prefabs (see VillageFounding)
	/// </summary>
	[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
	public static class VillageFoundingPrefabPatch
	{
		/// <summary>
		/// Registers the prefab once the game's own are in
		/// </summary>
		/// <param name="__instance">The world's ZNetScene</param>
		private static void Postfix(ZNetScene __instance)
		{
			VillageFounding.Register(__instance);
		}
	}
}
