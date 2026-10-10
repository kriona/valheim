using HarmonyLib;
using UnityEngine;

namespace LightColor
{
	/// <summary>
	/// Colors the glow in a portal's frame, which the portal sets itself every frame as it fades between its
	/// unconnected and connected colors
	/// </summary>
	[HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.Update))]
	public static class TeleportWorldUpdatePatch
	{
		private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

		/// <summary>
		/// Replaces the glow the portal just set with the stored color at the glow's strength, then scales it by the
		/// stored brightness, leaving the portal's own glow when there are no stored settings
		/// </summary>
		/// <param name="__instance">The portal</param>
		private static void Postfix(TeleportWorld __instance)
		{
			if (__instance.m_model == null)
			{
				return;
			}
			LightTint tint = __instance.GetComponent<LightTint>();
			if (tint == null || (!tint.Settings.HasColor && tint.Settings.Brightness == 1f))
			{
				return;
			}
			Color glow = Color.Lerp(__instance.m_colorUnconnected, __instance.m_colorTargetfound, __instance.m_colorAlpha);
			Color emission = (tint.Settings.HasColor ? Recolor.Replace(glow, tint.Settings.Color) : glow);
			float alpha = emission.a;
			emission *= tint.Settings.Brightness;
			emission.a = alpha;
			__instance.m_model.material.SetColor(EmissionColorId, emission);
		}
	}

	/// <summary>
	/// Marks the color of the portal checking its connection, so the burst it spawns when it connects can be colored
	/// </summary>
	[HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.UpdatePortal))]
	public static class TeleportWorldUpdatePortalPatch
	{
		/// <summary>
		/// Whether a colored portal is checking its connection
		/// </summary>
		public static bool Active;

		/// <summary>
		/// The color of the portal checking its connection, while Active is true
		/// </summary>
		public static Color Color;

		/// <summary>
		/// Marks the portal's color when it has one
		/// </summary>
		/// <param name="__instance">The portal</param>
		private static void Prefix(TeleportWorld __instance)
		{
			LightTint tint = __instance.GetComponent<LightTint>();
			Active = (tint != null && tint.Settings.HasColor);
			Color = ((tint != null) ? tint.Settings.Color : Color.white);
		}

		/// <summary>
		/// Clears the mark so effects spawned by anything else keep their own colors
		/// </summary>
		private static void Finalizer()
		{
			Active = false;
		}
	}

	/// <summary>
	/// Colors the effects a colored portal spawns when it connects
	/// </summary>
	[HarmonyPatch(typeof(EffectList), nameof(EffectList.Create))]
	public static class EffectListCreatePatch
	{
		/// <summary>
		/// Sets the spawned effects to the portal's color while a colored portal is checking its connection
		/// </summary>
		/// <param name="__result">The spawned effect objects</param>
		private static void Postfix(GameObject[] __result)
		{
			if (!TeleportWorldUpdatePortalPatch.Active || __result == null)
			{
				return;
			}
			foreach (GameObject effect in __result)
			{
				if (effect != null)
				{
					new Tintable(effect).Apply(LightSettings.ColorOnly(TeleportWorldUpdatePortalPatch.Color));
				}
			}
		}
	}

	/// <summary>
	/// Marks the portal the player is going through and the portal at the far end, so the swirl on the loading screen
	/// can fade from one's color to the other's
	/// </summary>
	[HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.Teleport))]
	public static class TeleportWorldTeleportPatch
	{
		/// <summary>
		/// Whether the player is going through a portal
		/// </summary>
		public static bool Active;

		/// <summary>
		/// The color of the portal the player is going through, or null when it has none, while Active is true
		/// </summary>
		public static Color? From;

		/// <summary>
		/// The portal at the far end, or ZDOID.None when it isn't connected, while Active is true
		/// </summary>
		public static ZDOID Target;

		/// <summary>
		/// Marks the portal's color and the portal it is connected to, asking the server for the far portal's
		/// latest data, since a far portal's data only stays up to date while the player is near it
		/// </summary>
		/// <param name="__instance">The portal</param>
		private static void Prefix(TeleportWorld __instance)
		{
			LightTint tint = __instance.GetComponent<LightTint>();
			Active = true;
			From = ((tint != null && tint.Settings.HasColor) ? tint.Settings.Color : (Color?)null);
			Target = ZDOID.None;
			ZNetView nview = __instance.m_nview;
			if (nview == null || !nview.IsValid())
			{
				return;
			}
			Target = nview.GetZDO().GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal);
			if (Target != ZDOID.None)
			{
				ZDOMan.instance.RequestZDO(Target);
			}
		}

		/// <summary>
		/// Clears the mark so teleports started by anything else show the swirl's own colors
		/// </summary>
		private static void Finalizer()
		{
			Active = false;
		}
	}

	/// <summary>
	/// Colors the swirl on the loading screen when the local player starts a teleport
	/// </summary>
	/// <remarks>
	/// The swirl is set when the teleport starts, so it keeps its colors while the loading screen fades out after the
	/// player arrives
	/// </remarks>
	[HarmonyPatch(typeof(Player), nameof(Player.TeleportTo))]
	public static class PlayerTeleportToPatch
	{
		/// <summary>
		/// Starts the swirl fading from the color of the portal being used to the color of the far end, or shows its
		/// own colors for a teleport that isn't through a portal
		/// </summary>
		/// <param name="__instance">The player</param>
		/// <param name="__result">Whether the teleport started</param>
		private static void Postfix(Player __instance, bool __result)
		{
			if (!__result || __instance != Player.m_localPlayer)
			{
				return;
			}
			if (TeleportWorldTeleportPatch.Active)
			{
				TeleportSwirl.Begin(TeleportWorldTeleportPatch.From, TeleportWorldTeleportPatch.Target);
			}
			else
			{
				TeleportSwirl.Begin(null, ZDOID.None);
			}
		}
	}
}
