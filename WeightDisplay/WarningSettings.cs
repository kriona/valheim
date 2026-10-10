using BepInEx.Configuration;

namespace WeightDisplay
{
	/// <summary>
	/// The config settings for when the weight and slot numbers change color and which colors they take
	/// </summary>
	/// <remarks>
	/// BepInEx writes sections in alphabetical order, so Warnings follows Display in the config file
	/// </remarks>
	public class WarningSettings
	{
		private const string ColorHint = " - a name (black, blue, green, orange, purple, red, white, yellow) or #RRGGBB, left empty for no color";

		public readonly ConfigEntry<int> WeightWarningPercent;
		public readonly ConfigEntry<string> WeightWarningColor;
		public readonly ConfigEntry<string> OverburdenedColor;
		public readonly ConfigEntry<int> SlotWarningOpenSlots;
		public readonly ConfigEntry<string> SlotWarningColor;
		public readonly ConfigEntry<string> FullSlotsColor;

		/// <summary>
		/// Binds the warning settings in the Warnings section of the config file
		/// </summary>
		/// <param name="config">The plugin's config file</param>
		public WarningSettings(ConfigFile config)
		{
			WeightWarningPercent = config.Bind("Warnings","WeightWarningPercent", 80, new ConfigDescription("Percentage of your maximum carry weight at which the weight takes WeightWarningColor", new AcceptableValueRange<int>(1, 100)));
			WeightWarningColor = config.Bind("Warnings","WeightWarningColor", "yellow", "Color of the weight at or above WeightWarningPercent" + ColorHint);
			OverburdenedColor = config.Bind("Warnings","OverburdenedColor", "red", "Color of the weight when you're overburdened" + ColorHint);
			SlotWarningOpenSlots = config.Bind("Warnings","SlotWarningOpenSlots", 3, new ConfigDescription("Number of open inventory slots at or below which the slot count takes SlotWarningColor", new AcceptableValueRange<int>(1, 100)));
			SlotWarningColor = config.Bind("Warnings","SlotWarningColor", "", "Color of the slot count when SlotWarningOpenSlots or fewer slots are open" + ColorHint);
			FullSlotsColor = config.Bind("Warnings","FullSlotsColor", "yellow", "Color of the slot count when every slot is in use" + ColorHint);
		}
	}
}
