using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Keeps a vikings' cart from dropping what it's built of when it breaks or is taken down, since they built it from
	/// nothing - what it holds still spills (see VillageCarts.IsVikingCart)
	/// </summary>
	[HarmonyPatch(typeof(Piece), nameof(Piece.DropResources))]
	public static class VikingCartDropPatch
	{
		/// <summary>
		/// Skips the drop for a vikings' cart
		/// </summary>
		/// <param name="__instance">The piece</param>
		/// <returns>False to skip it</returns>
		private static bool Prefix(Piece __instance)
		{
			return(!VillageCarts.IsVikingCart(__instance.GetComponent<Vagon>()));
		}
	}

	/// <summary>
	/// Keeps a player from hooking up a vikings' cart
	/// </summary>
	[HarmonyPatch(typeof(Vagon), nameof(Vagon.Interact))]
	public static class VikingCartPullPatch
	{
		/// <summary>
		/// Turns the player away from a vikings' cart
		/// </summary>
		/// <param name="__instance">The cart</param>
		/// <param name="character">Who's using it</param>
		/// <param name="__result">Whether it was used</param>
		/// <returns>False to skip the game's own for a vikings' cart</returns>
		private static bool Prefix(Vagon __instance, Humanoid character, ref bool __result)
		{
			return(VikingCartUse.Allow(__instance, character, ref __result));
		}
	}

	/// <summary>
	/// Keeps a player from opening a vikings' cart
	/// </summary>
	[HarmonyPatch(typeof(Container), nameof(Container.Interact))]
	public static class VikingCartOpenPatch
	{
		/// <summary>
		/// Turns the player away from a vikings' cart
		/// </summary>
		/// <param name="__instance">The container</param>
		/// <param name="character">Who's using it</param>
		/// <param name="__result">Whether it was used</param>
		/// <returns>False to skip the game's own for a vikings' cart</returns>
		private static bool Prefix(Container __instance, Humanoid character, ref bool __result)
		{
			return(VikingCartUse.Allow(__instance.m_wagon, character, ref __result));
		}
	}

	/// <summary>
	/// Leaves the use prompt off a vikings' cart, showing only its name
	/// </summary>
	[HarmonyPatch(typeof(Vagon), nameof(Vagon.GetHoverText))]
	public static class VikingCartHoverPatch
	{
		/// <summary>
		/// Shows only the name of a vikings' cart
		/// </summary>
		/// <param name="__instance">The cart</param>
		/// <param name="__result">Its hover text</param>
		private static void Postfix(Vagon __instance, ref string __result)
		{
			if (VillageCarts.IsVikingCart(__instance))
			{
				__result = Localization.instance.Localize(__instance.m_name);
			}
		}
	}

	/// <summary>
	/// Leaves the open prompt off a vikings' cart's box, showing only its name
	/// </summary>
	[HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
	public static class VikingCartBoxHoverPatch
	{
		/// <summary>
		/// Shows only the name of a vikings' cart's box
		/// </summary>
		/// <param name="__instance">The container</param>
		/// <param name="__result">Its hover text</param>
		private static void Postfix(Container __instance, ref string __result)
		{
			if (VillageCarts.IsVikingCart(__instance.m_wagon))
			{
				__result = Localization.instance.Localize(__instance.m_name);
			}
		}
	}

	/// <summary>
	/// Turns players away from a vikings' cart, for the patches above
	/// </summary>
	internal static class VikingCartUse
	{
		/// <summary>
		/// Lets a player use a cart unless the vikings put it up, telling them why when they did
		/// </summary>
		/// <param name="cart">The cart, or null for a container that isn't a cart's</param>
		/// <param name="character">Who's using it</param>
		/// <param name="result">Set to whether it was used when it's refused</param>
		/// <returns>True to let the game's own run</returns>
		public static bool Allow(Vagon cart, Humanoid character, ref bool result)
		{
			if (!VillageCarts.IsVikingCart(cart))
			{
				return(true);
			}
			if (character != null)
			{
				character.Message(MessageHud.MessageType.Center, "Only the Vikings use this cart");
			}
			result = false;
			return(false);
		}
	}
}
