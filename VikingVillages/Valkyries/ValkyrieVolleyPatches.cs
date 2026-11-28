using HarmonyLib;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Sets each feather of a valkyrie's volley flying her way as it's launched (see ValkyrieVolley)
	/// </summary>
	[HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
	internal static class ValkyrieVolleyPatch
	{
		/// <summary>
		/// Changes the velocity a feather of her volley is launched with, before the game sets it flying
		/// </summary>
		/// <param name="__instance">The projectile</param>
		/// <param name="owner">Who launched it</param>
		/// <param name="velocity">Its velocity</param>
		/// <param name="item">The attack's item</param>
		private static void Prefix(Projectile __instance, Character owner, ref Vector3 velocity, ItemDrop.ItemData item)
		{
			if (owner == null || item == null || item.m_shared == null || item.m_shared.m_name != ValkyrieCombat.VolleyName || owner.GetComponent<ValkyrieFlight>() == null)
			{
				return;
			}
			ValkyrieVolley.Launch(__instance, owner, ref velocity);
		}
	}
}
