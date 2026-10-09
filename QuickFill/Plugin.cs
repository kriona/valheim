using BepInEx;
using HarmonyLib;

namespace QuickFill
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.QuickFill";
		public const string PluginName = "Quick Fill";
		public const string PluginVersion = "1.0.0";

		private Harmony harmony;

		/// <summary>
		/// Applies the patches
		/// </summary>
		private void Awake()
		{
			harmony = new Harmony(PluginGuid);
			harmony.PatchAll();
			Logger.LogInfo(PluginName + " " + PluginVersion + " loaded");
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
