using BepInEx;
using HarmonyLib;

namespace ChestPeek
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.ChestPeek";
		public const string PluginName = "Chest Peek";
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
