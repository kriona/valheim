using HarmonyLib;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Has birds count living and dvergr vikings as danger, as they do players
	/// </summary>
	[HarmonyPatch(typeof(RandomFlyingBird), nameof(RandomFlyingBird.DangerNearby))]
	public static class BirdScarePatch
	{
		/// <summary>
		/// Counts a viking within the bird's danger distance as danger, when no player is
		/// </summary>
		/// <param name="__instance">The bird</param>
		/// <param name="p">The spot it checks</param>
		/// <param name="__result">Whether there's danger, set when a viking is near</param>
		private static void Postfix(RandomFlyingBird __instance, Vector3 p, ref bool __result)
		{
			if (!__result)
			{
				__result = BirdScare.VikingNear(p, __instance.m_avoidDangerDistance);
			}
		}
	}
}
