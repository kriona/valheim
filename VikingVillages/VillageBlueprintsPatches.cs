using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Adds the kri_vv_blueprints, kri_vv_spawnvillage, kri_vv_testlog, kri_vv_serverrun and kri_vv_travel commands when
	/// the console sets up its commands
	/// </summary>
	[HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
	public static class VillageBlueprintsCommandPatch
	{
		/// <summary>
		/// Registers the commands after the game's own
		/// </summary>
		private static void Postfix()
		{
			VillageBlueprints.RegisterCommand();
			VillageSpawnCommand.Register();
			TestLogCommand.Register();
			ServerVillageRun.RegisterCommand();
			TravelCommand.Register();
		}
	}
}
