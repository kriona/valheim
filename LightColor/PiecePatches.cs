using HarmonyLib;
using UnityEngine;

namespace LightColor
{
	/// <summary>
	/// Adds a LightTint to every built piece that has lights
	/// </summary>
	[HarmonyPatch(typeof(Piece), nameof(Piece.Awake))]
	public static class PieceAwakePatch
	{
		/// <summary>
		/// Adds the tint when the piece is a networked object with at least one light, leaving out placement ghosts
		/// and pieces that already have one
		/// </summary>
		/// <param name="__instance">The piece that just woke</param>
		private static void Postfix(Piece __instance)
		{
			ZNetView nview = __instance.m_nview;
			if (nview == null || !nview.IsValid() || __instance.GetComponent<LightTint>() != null)
			{
				return;
			}
			if (__instance.GetComponentInChildren<Light>(true) == null)
			{
				return;
			}
			__instance.gameObject.AddComponent<LightTint>().Init(nview);
		}
	}

	/// <summary>
	/// Keeps the color of the item shown on an item stand, like a mounted Dverger circlet, in step with the stand's stored color
	/// </summary>
	/// <remarks>
	/// The stand calls SetVisualItem every few seconds, and it only replaces the shown item when the item has changed
	/// </remarks>
	[HarmonyPatch(typeof(ItemStand), nameof(ItemStand.SetVisualItem))]
	public static class ItemStandSetVisualItemPatch
	{
		/// <summary>
		/// Remembers the shown item so the postfix can tell whether it was replaced
		/// </summary>
		/// <param name="__instance">The item stand</param>
		/// <param name="__state">The shown item before the call</param>
		private static void Prefix(ItemStand __instance, out GameObject __state)
		{
			__state = __instance.m_visualItem;
		}

		/// <summary>
		/// Adds a tint to the stand the first time it shows an item, or records the new item's lights and colors
		/// </summary>
		/// <param name="__instance">The item stand</param>
		/// <param name="__state">The shown item before the call</param>
		private static void Postfix(ItemStand __instance, GameObject __state)
		{
			ZNetView nview = __instance.m_nview;
			if (__instance.m_visualItem == __state || nview == null || !nview.IsValid())
			{
				return;
			}
			LightTint tint = __instance.GetComponent<LightTint>();
			if (tint == null)
			{
				__instance.gameObject.AddComponent<LightTint>().Init(nview);
			}
			else
			{
				tint.Rescan();
			}
		}
	}
}
