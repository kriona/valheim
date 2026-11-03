using HarmonyLib;

namespace LightColor
{
	/// <summary>
	/// Listens for the server's AdminOnly and AdminOnlyHelmet settings on each new connection
	/// </summary>
	[HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
	public static class ZNetNewConnectionPatch
	{
		/// <summary>
		/// Registers the message after the game's own
		/// </summary>
		/// <param name="peer">The other end of the connection</param>
		private static void Postfix(ZNetPeer peer)
		{
			AdminSync.OnNewConnection(peer);
		}
	}

	/// <summary>
	/// Sends the server's AdminOnly and AdminOnlyHelmet settings to a player once they're in
	/// </summary>
	[HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_PeerInfo))]
	public static class ZNetPeerInfoPatch
	{
		/// <summary>
		/// Sends the settings once the game has let the player in
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
				AdminSync.Send(rpc);
			}
		}
	}
}
