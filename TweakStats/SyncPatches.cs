using HarmonyLib;

namespace TweakStats
{
	/// <summary>
	/// Sets up Tweak Stats' messages on each new connection
	/// </summary>
	[HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
	public static class ZNetNewConnectionPatch
	{
		/// <summary>
		/// Registers the messages after the game's own, and sends the client's version
		/// </summary>
		/// <param name="peer">The other end of the connection</param>
		private static void Postfix(ZNetPeer peer)
		{
			ServerSync.OnNewConnection(peer);
		}
	}

	/// <summary>
	/// Refuses a joining player without a matching version of the mod when the server requires it, and sends the
	/// server's config to a player once they're in
	/// </summary>
	[HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_PeerInfo))]
	public static class ZNetPeerInfoSyncPatch
	{
		/// <summary>
		/// Stops the game letting the player in when the server refuses them
		/// </summary>
		/// <param name="__instance">The network manager</param>
		/// <param name="rpc">The player's connection</param>
		/// <returns>False to skip the game's own handling</returns>
		private static bool Prefix(ZNet __instance, ZRpc rpc)
		{
			return(!__instance.IsServer() || ServerSync.Allow(rpc));
		}

		/// <summary>
		/// Sends the server's config once the game has let the player in
		/// </summary>
		/// <param name="__instance">The network manager</param>
		/// <param name="rpc">The player's connection</param>
		private static void Postfix(ZNet __instance, ZRpc rpc)
		{
			if (!__instance.IsServer())
			{
				return;
			}
			ZNetPeer peer = __instance.GetPeer(rpc);
			if (peer != null && peer.m_uid != 0)
			{
				ServerSync.SendConfig(rpc);
			}
		}
	}
}
