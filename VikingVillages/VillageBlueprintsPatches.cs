using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Adds the kri_vv_blueprints and kri_vv_spawnvillage commands when the console sets up its commands
	/// </summary>
	[HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
	public static class VillageBlueprintsCommandPatch
	{
		/// <summary>
		/// Registers the command after the game's own
		/// </summary>
		private static void Postfix()
		{
			VillageBlueprints.RegisterCommand();
			VillageSpawnCommand.Register();
		}
	}
}
