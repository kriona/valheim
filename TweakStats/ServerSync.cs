using System.Collections.Generic;

namespace TweakStats
{
	/// <summary>
	/// Sends a server's config to the players who join it, and refuses players without a matching version of the mod
	/// when the server requires it
	/// </summary>
	/// <remarks>
	/// A joining client sends its version as soon as it connects, which reaches the server before the client's
	/// PeerInfo, so the server can check it before letting the player in. The server sends its config once the player
	/// is in, and again whenever its config file changes
	/// </remarks>
	public static class ServerSync
	{
		private const string VersionRpc = Plugin.PluginGuid + ".Version";
		private const string ConfigRpc = Plugin.PluginGuid + ".Config";
		private const string RefusedRpc = Plugin.PluginGuid + ".Refused";

		/// <summary>
		/// The version each connected client with the mod sent, by its connection
		/// </summary>
		private static readonly Dictionary<ZRpc, string> clientVersions = new Dictionary<ZRpc, string>();

		/// <summary>
		/// Sets up a new connection - a server listens for the client's version, and a client sends it and listens for
		/// the server's config
		/// </summary>
		/// <param name="peer">The other end of the connection</param>
		public static void OnNewConnection(ZNetPeer peer)
		{
			if (ZNet.instance.IsServer())
			{
				peer.m_rpc.Register<string>(VersionRpc, RPC_Version);
				return;
			}
			peer.m_rpc.Register<ZPackage>(ConfigRpc, RPC_Config);
			peer.m_rpc.Register<string>(RefusedRpc, RPC_Refused);
			peer.m_rpc.Invoke(VersionRpc, Plugin.PluginVersion);
		}

		/// <summary>
		/// Checks a joining player before the server lets them in
		/// </summary>
		/// <param name="rpc">The player's connection</param>
		/// <returns>True to let them in, false when the server requires the mod and they don't have a matching version</returns>
		public static bool Allow(ZRpc rpc)
		{
			if (!Tweaks.RequireMod)
			{
				return(true);
			}
			if (!clientVersions.TryGetValue(rpc, out string version))
			{
				Plugin.Log.LogInfo("Refused a player without " + Plugin.PluginName + ", which this server requires");
				rpc.Invoke("Error", (int)ZNet.ConnectionStatus.ErrorVersion);
				return(false);
			}
			if (!SameMinorVersion(version, Plugin.PluginVersion))
			{
				Plugin.Log.LogInfo("Refused a player with " + Plugin.PluginName + " " + version + ", since this server has " + Plugin.PluginVersion);
				rpc.Invoke(RefusedRpc, "This server requires " + Plugin.PluginName + " " + MinorVersion(Plugin.PluginVersion) + ", and you have " + version);
				rpc.Invoke("Error", (int)ZNet.ConnectionStatus.ErrorVersion);
				return(false);
			}
			return(true);
		}

		/// <summary>
		/// Sends the server's config to a player who was just let in, when they have the mod
		/// </summary>
		/// <param name="rpc">The player's connection</param>
		public static void SendConfig(ZRpc rpc)
		{
			if (!clientVersions.ContainsKey(rpc))
			{
				return;
			}
			ZPackage package = new ZPackage();
			package.Write(Tweaks.LocalText);
			rpc.Invoke(ConfigRpc, package);
		}

		/// <summary>
		/// Sends the server's config to every connected player with the mod, after the config file changes
		/// </summary>
		public static void SendConfigToAll()
		{
			if (ZNet.instance == null)
			{
				return;
			}
			foreach (ZNetPeer peer in ZNet.instance.GetPeers())
			{
				if (peer.m_rpc != null && peer.m_uid != 0)
				{
					SendConfig(peer.m_rpc);
				}
			}
		}

		/// <summary>
		/// Forgets every connection's version when the game leaves the world
		/// </summary>
		public static void Clear()
		{
			clientVersions.Clear();
		}

		/// <summary>
		/// Records the version a joining client sent
		/// </summary>
		/// <param name="rpc">The client's connection</param>
		/// <param name="version">The client's version of the mod</param>
		private static void RPC_Version(ZRpc rpc, string version)
		{
			ForgetDisconnected();
			clientVersions[rpc] = version;
		}

		/// <summary>
		/// Removes the versions of clients that have since disconnected, so a long-running server doesn't keep them
		/// </summary>
		private static void ForgetDisconnected()
		{
			HashSet<ZRpc> connected = new HashSet<ZRpc>();
			foreach (ZNetPeer peer in ZNet.instance.GetPeers())
			{
				connected.Add(peer.m_rpc);
			}
			List<ZRpc> gone = new List<ZRpc>();
			foreach (ZRpc rpc in clientVersions.Keys)
			{
				if (!connected.Contains(rpc))
				{
					gone.Add(rpc);
				}
			}
			foreach (ZRpc rpc in gone)
			{
				clientVersions.Remove(rpc);
			}
		}

		/// <summary>
		/// Switches to the config the server sent
		/// </summary>
		/// <param name="rpc">The server's connection</param>
		/// <param name="package">The server's config file</param>
		private static void RPC_Config(ZRpc rpc, ZPackage package)
		{
			Tweaks.UseServerConfig(package.ReadString());
		}

		/// <summary>
		/// Logs why the server refused this game, since the game itself only says the version doesn't match
		/// </summary>
		/// <param name="rpc">The server's connection</param>
		/// <param name="reason">Why the server refused it</param>
		private static void RPC_Refused(ZRpc rpc, string reason)
		{
			Plugin.Log.LogWarning(reason);
		}

		/// <summary>
		/// Whether two versions share their major and minor numbers, so 1.2.0 and 1.2.3 can play together
		/// </summary>
		/// <param name="a">The first version</param>
		/// <param name="b">The second version</param>
		/// <returns>True when the major and minor numbers match</returns>
		private static bool SameMinorVersion(string a, string b)
		{
			return(MinorVersion(a) == MinorVersion(b));
		}

		/// <summary>
		/// A version's major and minor numbers
		/// </summary>
		/// <param name="version">The version, e.g. 1.2.3</param>
		/// <returns>The major and minor numbers, e.g. 1.2</returns>
		private static string MinorVersion(string version)
		{
			string[] parts = version.Split('.');
			return((parts.Length >= 2) ? parts[0] + "." + parts[1] : version);
		}
	}
}
