using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Listens for the requests to have a dedicated server run a village, and its answers, in each world (see
	/// ServerVillageRun)
	/// </summary>
	[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
	public static class ServerVillageRunRpcPatch
	{
		/// <summary>
		/// Registers the routed RPCs once the world's network is up
		/// </summary>
		private static void Postfix()
		{
			ServerVillageRun.RegisterRpc();
		}
	}
}
