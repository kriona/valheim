using HarmonyLib;

namespace ZombieApocalypse
{
	/// <summary>
	/// Adds the terrain operation that flattens the ground for a village's fire to the game's terrain operations
	/// whenever ObjectDB lists them - as it wakes, and as it copies another's
	/// </summary>
	[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.UpdateRegisters))]
	public static class VillageLevelOpPatch
	{
		/// <summary>
		/// Adds the operation once ObjectDB has listed its own
		/// </summary>
		/// <param name="__instance">The ObjectDB</param>
		private static void Postfix(ObjectDB __instance)
		{
			VillagePaths.RegisterLevelOp(__instance);
		}
	}

	/// <summary>
	/// Places the villages' names over their fires (see VillageNameTags) once the camera has moved, so they keep up with
	/// it
	/// </summary>
	/// <remarks>
	/// Unity runs different scripts' LateUpdates in no set order, so the names are placed after the camera's
	/// </remarks>
	[HarmonyPatch(typeof(GameCamera), nameof(GameCamera.LateUpdate))]
	public static class VillageNameTagPatch
	{
		/// <summary>
		/// Places the names
		/// </summary>
		private static void Postfix()
		{
			VillageNameTags.Place();
		}
	}

	/// <summary>
	/// Shows a summary of a village's state when the player looks at its fire with debug mode on
	/// </summary>
	[HarmonyPatch(typeof(Fireplace), nameof(Fireplace.GetHoverText))]
	public static class VillageDebugHoverPatch
	{
		/// <summary>
		/// Adds the summary to the fire's hover text
		/// </summary>
		/// <param name="__instance">The fire looked at</param>
		/// <param name="__result">Its hover text</param>
		private static void Postfix(Fireplace __instance, ref string __result)
		{
			if (!Player.m_debugMode || __instance == null)
			{
				return;
			}
			Village village = VillageDebug.Of(__instance);
			if (village == null)
			{
				return;
			}
			string summary = VillageDebug.Summary(village);
			__result = (string.IsNullOrEmpty(__result) ? summary : __result + "\n" + summary);
		}
	}

	/// <summary>
	/// Keeps the game from toggling auto-pickup when the village area key is pressed at a fire it does something at -
	/// the key is auto-pickup's by default (see VillageArea.KeyTaken)
	/// </summary>
	[HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButtonDown))]
	public static class VillageAreaKeyPatch
	{
		/// <summary>
		/// Reports auto-pickup's button as not pressed while the key is taken
		/// </summary>
		/// <param name="name">The button</param>
		/// <param name="__result">Whether it was just pressed</param>
		private static void Postfix(string name, ref bool __result)
		{
			if (__result && name == "AutoPickup" && VillageArea.KeyTaken())
			{
				__result = false;
			}
		}
	}

	/// <summary>
	/// Adds the village's name and keys to the hover text of its fire, or the key for making another fire in it a
	/// gathering fire (see VillageArea) - above the fire's own name and keys, like its use wood key, and ahead of the
	/// debug summary
	/// </summary>
	[HarmonyPatch(typeof(Fireplace), nameof(Fireplace.GetHoverText))]
	[HarmonyPriority(Priority.High)]
	public static class VillageAreaHoverPatch
	{
		/// <summary>
		/// Puts the lines above the fire's own hover text
		/// </summary>
		/// <param name="__instance">The fire looked at</param>
		/// <param name="__result">Its hover text</param>
		private static void Postfix(Fireplace __instance, ref string __result)
		{
			string line = ((__instance != null) ? VillageArea.HoverLine(__instance) : null);
			if (line == null)
			{
				return;
			}
			__result = (string.IsNullOrEmpty(__result) ? line : line + "\n" + __result);
		}
	}

	/// <summary>
	/// Keeps a village's bonfire or campfire from dropping what it's built from when it breaks or is taken down, since
	/// its vikings build it back with nothing - unless a player built it, and the village took it up as its fire
	/// </summary>
	[HarmonyPatch(typeof(Piece), nameof(Piece.DropResources))]
	public static class VillageFireDropPatch
	{
		/// <summary>
		/// Skips the drop for a village's fire
		/// </summary>
		/// <param name="__instance">The piece breaking</param>
		/// <returns>False to skip the drop</returns>
		private static bool Prefix(Piece __instance)
		{
			ZNetView view = __instance.m_nview;
			return(view == null || !view.IsValid() || !view.GetZDO().GetBool(VillagePaths.BonfireKey) || view.GetZDO().GetLong(ZDOVars.s_creator, 0L) != 0L);
		}
	}

	/// <summary>
	/// Keeps a piece the mod placed or built back from dropping what it's built from when it breaks or is taken down,
	/// since the vikings build it for wood they gather for free - the pieces a village was generated with drop as usual
	/// </summary>
	[HarmonyPatch(typeof(Piece), nameof(Piece.DropResources))]
	public static class VillagePieceDropPatch
	{
		/// <summary>
		/// Skips the drop for a piece the mod built
		/// </summary>
		/// <param name="__instance">The piece breaking</param>
		/// <returns>False to skip the drop</returns>
		private static bool Prefix(Piece __instance)
		{
			ZNetView view = __instance.m_nview;
			return(view == null || !view.IsValid() || !view.GetZDO().GetBool(VillageRecords.BuiltKey));
		}
	}

	/// <summary>
	/// Pushes back the fog around a village's burning fire once the game has set the weather's fog (see VillageFog)
	/// </summary>
	[HarmonyPatch(typeof(EnvMan), nameof(EnvMan.SetEnv))]
	public static class VillageFogPatch
	{
		/// <summary>
		/// Thins the fog by how near the camera is to a village's burning fire
		/// </summary>
		/// <param name="dt">Seconds since the last update</param>
		private static void Postfix(float dt)
		{
			VillageFog.Apply(dt);
		}
	}

	/// <summary>
	/// Has a village's burning bonfire run any cooking station sitting on it, with only some of its fire check points
	/// in the flames (see VillageCooking.OnVillageFire)
	/// </summary>
	[HarmonyPatch(typeof(CookingStation), nameof(CookingStation.IsFireLit))]
	public static class VillageFireCookingPatch
	{
		/// <summary>
		/// Counts the station's fire as lit when the game found it unlit but it sits on a village's burning fire
		/// </summary>
		/// <param name="__instance">The station</param>
		/// <param name="__result">Whether its fire is lit</param>
		private static void Postfix(CookingStation __instance, ref bool __result)
		{
			if (!__result && VillageCooking.OnVillageFire(__instance))
			{
				__result = true;
			}
		}
	}
}
