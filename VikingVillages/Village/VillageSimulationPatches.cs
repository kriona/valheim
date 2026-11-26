using HarmonyLib;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Notes which player the game is deciding who runs what around, so the village they're in counts as in their area
	/// while it decides (see VillageSimulation)
	/// </summary>
	[HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.ReleaseNearbyZDOS))]
	public static class VillageSimulationDecidePatch
	{
		/// <summary>
		/// Notes the player before the game decides
		/// </summary>
		/// <param name="refPosition">The player's reference position</param>
		/// <param name="uid">The player's session</param>
		private static void Prefix(Vector3 refPosition, long uid)
		{
			VillageSimulation.BeginDeciding(refPosition, uid);
		}

		/// <summary>
		/// Forgets the player once the game has decided, even when it threw
		/// </summary>
		private static void Finalizer()
		{
			VillageSimulation.EndDeciding();
		}
	}

	/// <summary>
	/// Counts a point in the village a player is in as in their area while the game decides who runs what around them
	/// </summary>
	[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.InActiveArea), new[] { typeof(Vector3), typeof(Vector2s) })]
	public static class VillageSimulationAreaPatch
	{
		/// <summary>
		/// Counts the point as in the area when it's in the village
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="centerZone">The zone the area is around</param>
		/// <param name="__result">Whether it's in the area</param>
		private static void Postfix(Vector3 point, Vector2s centerZone, ref bool __result)
		{
			if (!__result && VillageSimulation.ExtendsDeciding(point, centerZone))
			{
				__result = true;
			}
		}
	}

	/// <summary>
	/// Counts a point in the village another player is in as in their area too, so the game leaves what they run in it
	/// with them rather than taking it over
	/// </summary>
	[HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.IsInPeerActiveArea))]
	public static class VillageSimulationPeerAreaPatch
	{
		/// <summary>
		/// Counts the point as in the player's area when it's in the village they're in
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="uid">The player's session</param>
		/// <param name="__result">Whether it's in their area</param>
		private static void Postfix(Vector3 point, long uid, ref bool __result)
		{
			if (__result || ZNet.instance == null)
			{
				return;
			}
			Vector3 reference;
			if (uid == ZDOMan.GetSessionID())
			{
				reference = ZNet.instance.GetReferencePosition();
			}
			else
			{
				ZNetPeer peer = ZNet.instance.GetPeer(uid);
				if (peer == null)
				{
					return;
				}
				reference = peer.GetRefPos();
			}
			__result = VillageSimulation.Covers(point, reference, uid);
		}
	}
}
