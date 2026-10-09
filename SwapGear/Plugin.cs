using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SwapGear
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.SwapGear";
		public const string PluginName = "Swap Gear";
		public const string PluginVersion = "1.0.0";

		public static ConfigEntry<KeyboardShortcut> SwapKey;
		public static ConfigEntry<float> PickupTimeout;
		public static ConfigEntry<bool> InstantEquip;

		private Harmony harmony;
		private Coroutine swap;

		/// <summary>
		/// Binds the settings and applies the patches
		/// </summary>
		private void Awake()
		{
			SwapKey = Config.Bind("General", "SwapKey", new KeyboardShortcut(KeyCode.Y), "Key to press while looking at an armor stand to swap your gear with what's on it - a Unity KeyCode name, optionally with modifiers, e.g. Y or LeftControl + Y");
			PickupTimeout = Config.Bind("General", "PickupTimeout", 10f, "Seconds to wait for the armor stand's items to be picked up before giving up on equipping them");
			InstantEquip = Config.Bind("General", "InstantEquip", false, "Unequip and equip gear instantly instead of taking as long as it does from the inventory");
			harmony = new Harmony(PluginGuid);
			harmony.PatchAll();
			Logger.LogInfo(PluginName + " " + PluginVersion + " loaded");
		}

		/// <summary>
		/// Starts a swap when the key is pressed while looking at an armor stand with items on it
		/// </summary>
		private void Update()
		{
			if (Swap.Running || !IsSwapKeyDown())
			{
				return;
			}
			Player player = Player.m_localPlayer;
			if (player == null || !player.TakeInput())
			{
				return;
			}
			ArmorStand stand = Swap.GetHoveredStand(player);
			if (stand != null)
			{
				swap = StartCoroutine(Swap.Run(player, stand));
			}
		}

		/// <summary>
		/// Whether the swap key was pressed this frame with all of its modifiers held
		/// </summary>
		/// <returns>True when the shortcut was pressed</returns>
		private static bool IsSwapKeyDown()
		{
			KeyboardShortcut shortcut = SwapKey.Value;
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
		/// Stops a running swap and removes the patches when the plugin unloads
		/// </summary>
		private void OnDestroy()
		{
			if (swap != null)
			{
				StopCoroutine(swap);
				Swap.Running = false;
			}
			harmony?.UnpatchSelf();
		}
	}
}
