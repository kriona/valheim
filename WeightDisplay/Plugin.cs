using BepInEx;
using BepInEx.Configuration;

namespace WeightDisplay
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "kriona.WeightDisplay";
		public const string PluginName = "Weight Display";
		public const string PluginVersion = "1.0.0";

		private ConfigEntry<int> fontSize;
		private ConfigEntry<int> margin;
		private readonly WeightLabel label = new WeightLabel();

		/// <summary>
		/// Binds the display settings
		/// </summary>
		private void Awake()
		{
			fontSize = Config.Bind("Display", "FontSize", 18, "Size of the weight text");
			margin = Config.Bind("Display", "Margin", 6, "Gap between the minimap (or the screen corner when there is no minimap) and the text");
			Logger.LogInfo(PluginName + " " + PluginVersion + " loaded");
		}

		/// <summary>
		/// Runs after the HUD and minimap have updated so the label follows their final positions
		/// </summary>
		private void LateUpdate()
		{
			label.Update(fontSize.Value, margin.Value);
		}

		/// <summary>
		/// Removes the label when the plugin unloads
		/// </summary>
		private void OnDestroy()
		{
			label.Destroy();
		}
	}
}
