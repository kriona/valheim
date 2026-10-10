using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace WeightDisplay
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.WeightDisplay";
		public const string PluginName = "Weight Display";
		public const string PluginVersion = "1.1.0";

		private ConfigEntry<int> fontSize;
		private ConfigEntry<int> margin;
		private readonly WeightLabel label = new WeightLabel();
		private Harmony harmony;

		/// <summary>
		/// The loaded plugin, for reloading its config file from a patch
		/// </summary>
		private static Plugin instance;

		/// <summary>
		/// Binds the display settings and applies the patches
		/// </summary>
		private void Awake()
		{
			instance = this;
			fontSize = Config.Bind("Display", "FontSize", 18, "Size of the weight text");
			margin = Config.Bind("Display", "Margin", 6, "Gap between the minimap (or the screen corner when there is no minimap) and the text");
			harmony = new Harmony(PluginGuid);
			harmony.PatchAll();
			Logger.LogInfo(PluginName + " " + PluginVersion + " loaded");
		}

		/// <summary>
		/// Reads the config file again, so changes made to it while the game is running take effect
		/// </summary>
		public static void ReloadConfig()
		{
			instance?.Config.Reload();
		}

		/// <summary>
		/// Runs after the HUD and minimap have updated so the label follows their final positions
		/// </summary>
		private void LateUpdate()
		{
			label.Update(fontSize.Value, margin.Value);
		}

		/// <summary>
		/// Removes the label and patches when the plugin unloads
		/// </summary>
		private void OnDestroy()
		{
			label.Destroy();
			harmony?.UnpatchSelf();
		}
	}
}
