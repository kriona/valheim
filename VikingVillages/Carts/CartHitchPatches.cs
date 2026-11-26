using HarmonyLib;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Notes who a cart is being hooked up to while the game hooks it up, so only that puller's other carts come loose
	/// (see CartDetachAllPatch)
	/// </summary>
	[HarmonyPatch(typeof(Vagon), nameof(Vagon.AttachTo))]
	public static class CartAttachToPatch
	{
		/// <summary>
		/// Notes the puller
		/// </summary>
		/// <param name="go">Who the cart is hooked up to</param>
		private static void Prefix(GameObject go)
		{
			CartHitch.Attaching = go;
		}

		/// <summary>
		/// Forgets the puller once the cart is hooked up, even when hooking it up failed
		/// </summary>
		private static void Finalizer()
		{
			CartHitch.Attaching = null;
		}
	}

	/// <summary>
	/// Unhooks only the carts hooked to the puller a cart is being hooked up to, rather than every cart on the client,
	/// so each viking - and the player - keeps pulling its own
	/// </summary>
	[HarmonyPatch(typeof(Vagon), nameof(Vagon.DetachAll))]
	public static class CartDetachAllPatch
	{
		/// <summary>
		/// Unhooks the puller's own carts in place of every cart
		/// </summary>
		/// <returns>False when a puller is known, so the game's own doesn't run</returns>
		private static bool Prefix()
		{
			GameObject puller = CartHitch.Attaching;
			if (puller == null)
			{
				return(true);
			}
			foreach (Vagon cart in Vagon.m_instances)
			{
				if (cart != null && cart.m_attachedObject == puller)
				{
					cart.Detach();
				}
			}
			return(false);
		}
	}
}
