using BepInEx;
using HarmonyLib;

namespace CreatureMarker
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.CreatureMarker";
		public const string PluginName = "Creature Marker";
		public const string PluginVersion = "1.0.0";

		private readonly MarkerHud markers = new MarkerHud();
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
		/// Runs after the camera and creatures have moved so the markers track their final positions
		/// </summary>
		private void LateUpdate()
		{
			markers.Update();
		}

		/// <summary>
		/// Removes the markers and patches when the plugin unloads
		/// </summary>
		private void OnDestroy()
		{
			markers.Destroy();
			harmony?.UnpatchSelf();
		}
	}
}
