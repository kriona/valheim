using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Kriona.Shared;
using UnityEngine;

namespace LightColor
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.LightColor";
		public const string PluginName = "Light Color";
		public const string PluginVersion = "1.2.0";

		public static ConfigEntry<KeyboardShortcut> ColorKey;
		public static ConfigEntry<string> Helmet;

		private Harmony harmony;

		/// <summary>
		/// Reads the config file again once it has been saved - settings whose value changed fire their change events,
		/// which checks a new Helmet setting so it is stored on the player
		/// </summary>
		private ConfigWatcher watcher;

		/// <summary>
		/// The Helmet setting once checked, blank when it is blank, "default" or can't be read
		/// </summary>
		private string helmetText = "";

		/// <summary>
		/// The player and text last stored, so the player's ZDO is only written when one of them changes
		/// </summary>
		private Player storedPlayer;
		private string storedText;

		/// <summary>
		/// Binds the settings, watches the config file for changes and applies the patches
		/// </summary>
		private void Awake()
		{
			ColorKey = Config.Bind("General", "ColorKey", new KeyboardShortcut(KeyCode.L), "Key to press while looking at a light to type its color - a Unity KeyCode name, optionally with modifiers, e.g. L or LeftControl + L");
			Helmet = Config.Bind("General", "Helmet", "", "Color, brightness and range for the light on your equipped helmet, like the Dverger circlet, typed the same way as on a light, e.g. blue 150% 20m - other players with the mod see it too, and blank keeps the helmet's own light");
			Helmet.SettingChanged += (sender, args) => CheckHelmetText();
			CheckHelmetText();
			watcher = new ConfigWatcher(Config.ConfigFilePath, Config.Reload, Logger);
			harmony = new Harmony(PluginGuid);
			harmony.PatchAll();
			Logger.LogInfo(PluginName + " " + PluginVersion + " loaded");
		}

		/// <summary>
		/// Reads the config file again once it has been saved, stores the Helmet setting on the local player and opens
		/// the color input for the light being looked at when the key is pressed
		/// </summary>
		private void Update()
		{
			watcher.Update();
			StoreHelmetSettings();
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
		/// Reads the Helmet setting, logging and ignoring it when it can't be read
		/// </summary>
		private void CheckHelmetText()
		{
			string text = Helmet.Value.Trim();
			if (!LightSettings.TryParse(text, out LightSettings settings, out string error))
			{
				Logger.LogWarning("Couldn't read the Helmet setting \"" + text + "\": " + error);
				text = "";
			}
			else if (settings.IsDefault())
			{
				text = "";
			}
			helmetText = text;
		}

		/// <summary>
		/// Writes the Helmet setting to the local player's ZDO when the player or the setting has changed since the last write
		/// </summary>
		private void StoreHelmetSettings()
		{
			Player player = Player.m_localPlayer;
			if (player == null || (player == storedPlayer && helmetText == storedText))
			{
				return;
			}
			ZNetView nview = player.m_nview;
			if (nview == null || !nview.IsValid())
			{
				return;
			}
			nview.GetZDO().Set(HelmetTint.SettingsHash, helmetText);
			storedPlayer = player;
			storedText = helmetText;
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
		/// Stops watching the config file and removes the patches when the plugin unloads
		/// </summary>
		private void OnDestroy()
		{
			watcher?.Dispose();
			harmony?.UnpatchSelf();
		}
	}
}
