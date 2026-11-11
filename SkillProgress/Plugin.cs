using BepInEx;
using HarmonyLib;

namespace SkillProgress
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.SkillProgress";
		public const string PluginName = "Skill Progress";
		public const string PluginVersion = "1.0.0";

		private readonly SkillBars bars = new SkillBars();
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
		/// Runs after the HUD has updated so the bars follow the stamina bar's final position
		/// </summary>
		private void LateUpdate()
		{
			bars.Update();
		}

		/// <summary>
		/// Removes the bars and the patches when the plugin unloads
		/// </summary>
		private void OnDestroy()
		{
			bars.Destroy();
			harmony?.UnpatchSelf();
		}
	}
}
