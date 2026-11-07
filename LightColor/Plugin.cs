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
		public const string PluginVersion = "1.5.1";

		public static ConfigEntry<KeyboardShortcut> ColorKey;
		public static ConfigEntry<string> Helmet;
		public static ConfigEntry<bool> AdminOnly;
		public static ConfigEntry<bool> AdminOnlyHelmet;

		private Harmony harmony;

		/// <summary>
		/// Whether the game is a dedicated server, which has no player or screen and only sends its settings to players
		/// </summary>
		private bool dedicated;

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
		/// Whether the Helmet setting was left out of the last write because only admins can use it, so the player is
		/// only told once
		/// </summary>
		private bool helmetDenied;

		/// <summary>
		/// Binds the settings, watches the config file for changes and applies the patches
		/// </summary>
		private void Awake()
		{
			ColorKey = Config.Bind("General", "ColorKey", new KeyboardShortcut(KeyCode.L), "Key to press while looking at a light to type its color - a Unity KeyCode name, optionally with modifiers, e.g. L or LeftControl + L");
			Helmet = Config.Bind("General", "Helmet", "", "Color, brightness and range for the light on your equipped helmet, like the Dverger circlet, typed the same way as on a light, e.g. blue 150% 20m - other players with the mod see it too, and blank keeps the helmet's own light");
			Helmet.SettingChanged += (sender, args) => CheckHelmetText();
			CheckHelmetText();
			AdminOnly = Config.Bind("Server", "AdminOnly", false, "Only admins in the server's adminlist.txt can change colors and text glow");
			AdminOnly.SettingChanged += (sender, args) => AdminSync.SendToAll();
			AdminOnlyHelmet = Config.Bind("Server", "AdminOnlyHelmet", false, "Only admins in the server's adminlist.txt can color their equipped helmet with the Helmet setting");
			AdminOnlyHelmet.SettingChanged += (sender, args) => AdminSync.SendToAll();
			watcher = new ConfigWatcher(Config, Logger, PluginName);
			harmony = new Harmony(PluginGuid);
			dedicated = Application.isBatchMode;
			if (dedicated)
			{
				harmony.CreateClassProcessor(typeof(ZNetPeerInfoPatch)).Patch();
			}
			else
			{
				harmony.PatchAll();
			}
			Logger.LogInfo(PluginName + " " + PluginVersion + " loaded");
		}

		/// <summary>
		/// Reads the config file again once it has been saved, stores the Helmet setting on the local player and opens
		/// the color input for the light being looked at when the key is pressed
		/// </summary>
		private void Update()
		{
			watcher.Update();
			if (dedicated)
			{
				return;
			}
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
			if (tint == null || !tint.HasEffects)
			{
				return;
			}
			if (!AdminSync.CanChangeColors)
			{
				player.Message(MessageHud.MessageType.Center, AdminSync.DeniedMessage);
				return;
			}
			if (PrivateArea.CheckAccess(tint.transform.position))
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
			else if (settings.Styles != TextStyle.None)
			{
				Logger.LogWarning("Halo, lit and outline in the Helmet setting \"" + text + "\" only work on signs, so they're ignored");
			}
			else if (settings.IsDefault())
			{
				text = "";
			}
			helmetText = text;
		}

		/// <summary>
		/// Writes the Helmet setting to the local player's ZDO when the player or the setting has changed since the last
		/// write, writing blank instead when the server lets only admins use it and the player isn't one
		/// </summary>
		private void StoreHelmetSettings()
		{
			Player player = Player.m_localPlayer;
			if (player == null)
			{
				return;
			}
			bool denied = (helmetText.Length > 0 && !AdminSync.CanUseHelmet);
			string text = (denied ? "" : helmetText);
			if (player == storedPlayer && text == storedText)
			{
				return;
			}
			ZNetView nview = player.m_nview;
			if (nview == null || !nview.IsValid())
			{
				return;
			}
			nview.GetZDO().Set(HelmetTint.SettingsHash, text);
			storedPlayer = player;
			storedText = text;
			if (denied && !helmetDenied)
			{
				player.Message(MessageHud.MessageType.TopLeft, "Only admins can use the Helmet setting on this server");
			}
			helmetDenied = denied;
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
