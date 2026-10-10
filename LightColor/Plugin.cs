using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace LightColor
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.LightColor";
		public const string PluginName = "Light Color";
		public const string PluginVersion = "1.0.0";

		public static ConfigEntry<KeyboardShortcut> ColorKey;

		private Harmony harmony;

		/// <summary>
		/// Binds the settings and applies the patches
		/// </summary>
		private void Awake()
		{
			ColorKey = Config.Bind("General", "ColorKey", new KeyboardShortcut(KeyCode.L), "Key to press while looking at a light to type its color - a Unity KeyCode name, optionally with modifiers, e.g. L or LeftControl + L");
			harmony = new Harmony(PluginGuid);
			harmony.PatchAll();
			Logger.LogInfo(PluginName + " " + PluginVersion + " loaded");
		}

		/// <summary>
		/// Opens the color input for the light being looked at when the key is pressed
		/// </summary>
		private void Update()
		{
			if (!IsColorKeyDown())
			{
				return;
			}
			Player player = Player.m_localPlayer;
			if (player == null || !player.TakeInput())
			{
				return;
			}
			GameObject hover = player.GetHoverObject();
			if (hover == null)
			{
				return;
			}
			LightTint tint = hover.GetComponentInParent<LightTint>();
			if (tint != null && tint.HasEffects && PrivateArea.CheckAccess(tint.transform.position))
			{
				tint.RequestColor();
			}
		}

		/// <summary>
		/// Whether the color key was pressed this frame with all of its modifiers held
		/// </summary>
		/// <returns>True when the shortcut was pressed</returns>
		private static bool IsColorKeyDown()
		{
			KeyboardShortcut shortcut = ColorKey.Value;
			if (shortcut.MainKey == KeyCode.None || !ZInput.GetKeyDown(shortcut.MainKey, false))
			{
				return(false);
			}
			foreach (KeyCode modifier in shortcut.Modifiers)
			{
				if (!ZInput.GetKey(modifier, false))
				{
					return(false);
				}
			}
			return(true);
		}

		/// <summary>
		/// Removes the patches when the plugin unloads
		/// </summary>
		private void OnDestroy()
		{
			harmony?.UnpatchSelf();
		}
	}
}
