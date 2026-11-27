using BepInEx.Configuration;

namespace Kriona.Vikings
{
	/// <summary>
	/// The settings the shared viking code reads, which each mod binds in its own config as it loads - Viking Villages
	/// for its PC vikings and Zombie Apocalypse for its zombie vikings, each with values of its own
	/// </summary>
	/// <remarks>
	/// A setting the mod doesn't have is left null and read as its fallback (see Get) - the zombie settings in Viking
	/// Villages, whose PC vikings are always armed, fully repaired and never starred
	/// </remarks>
	internal static class VikingSettings
	{
		/// <summary>
		/// Percent chances that a viking wearing armor also wears the set's helmet and cape, and that a viking carries a
		/// shield beside a one-handed weapon
		/// </summary>
		public static ConfigEntry<int> HelmetChance;
		public static ConfigEntry<int> CapeChance;
		public static ConfigEntry<int> ShieldChance;

		/// <summary>
		/// Percent chances that a zombie viking of the world wears armor and wields a weapon
		/// </summary>
		public static ConfigEntry<int> ZombieArmorChance;
		public static ConfigEntry<int> ZombieWeaponChance;

		/// <summary>
		/// Whether each of a zombie viking's stars upgrades its gear a level
		/// </summary>
		public static ConfigEntry<bool> ZombieStarsUpgradeGear;

		/// <summary>
		/// The least and most durability a zombie viking's gear spawns with, as a percent of full
		/// </summary>
		public static ConfigEntry<int> ZombieDurabilityMin;
		public static ConfigEntry<int> ZombieDurabilityMax;

		/// <summary>
		/// How well armor protects a zombie viking, as a percent of how well it protects a player
		/// </summary>
		public static ConfigEntry<int> ZombieArmorEffectiveness;

		/// <summary>
		/// Reads a setting, or its fallback when the mod has no such setting
		/// </summary>
		/// <param name="entry">The setting, or null</param>
		/// <param name="fallback">What it's read as when it's null</param>
		/// <returns>The value</returns>
		public static int Get(ConfigEntry<int> entry, int fallback)
		{
			return((entry != null) ? entry.Value : fallback);
		}

		/// <summary>
		/// Reads a setting, or its fallback when the mod has no such setting
		/// </summary>
		/// <param name="entry">The setting, or null</param>
		/// <param name="fallback">What it's read as when it's null</param>
		/// <returns>The value</returns>
		public static bool Get(ConfigEntry<bool> entry, bool fallback)
		{
			return((entry != null) ? entry.Value : fallback);
		}
	}
}
