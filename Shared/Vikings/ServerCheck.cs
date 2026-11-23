using System.Collections.Generic;
using HarmonyLib;
using Kriona.Shared;

namespace Kriona.Vikings
{
	/// <summary>
	/// Keeps the mod out of a world whose server doesn't have it, and keeps players without the server's version of it
	/// out of a world whose server does
	/// </summary>
	/// <remarks>
	/// A client with the mod says hello with its version as soon as it connects, which reaches the server before the
	/// client's PeerInfo, and a server with the same version answers. The server refuses a joining player whose hello
	/// didn't come or came with another version, so everyone in its world plays the same version, whatever versions of
	/// the other mods they have. A client only runs the mod once the answer arrives, so on a server without the mod it
	/// stays out of the way and the player still joins. Single player, hosting and a dedicated server always count as
	/// having the mod, since the game running them is the server
	/// </remarks>
	internal static partial class ServerCheck
	{
		/// <summary>
		/// The client's hello and the server's answer, each with its version of the mod
		/// </summary>
		private static string HelloRpc
		{
			get
			{
				return(VikingMod.Guid + ".Hello");
			}
		}
		private static string WelcomeRpc
		{
			get
			{
				return(VikingMod.Guid + ".Welcome");
			}
		}

		/// <summary>
		/// Whether the server this client is connected to answered its hello
		/// </summary>
		private static bool serverHasMod;

		/// <summary>
		/// The version each connected client with the mod said hello with, by its connection, on the server
		/// </summary>
		private static readonly Dictionary<ZRpc, string> clientVersions = new Dictionary<ZRpc, string>();

		/// <summary>
		/// Whether the mod runs in the world being played
		/// </summary>
		public static bool Active
		{
			get
			{
				return(ZNet.instance != null && (ZNet.instance.IsServer() || serverHasMod));
			}
		}

		/// <summary>
		/// Sets up a new connection - a server listens for the client's hello, and a client listens for the server's
		/// answer and its settings and says hello
		/// </summary>
		/// <param name="peer">The other end of the connection</param>
		public static void OnNewConnection(ZNetPeer peer)
		{
			if (ZNet.instance.IsServer())
			{
				peer.m_rpc.Register<string>(HelloRpc, RPC_Hello);
				return;
			}
			peer.m_rpc.Register<string>(WelcomeRpc, RPC_Welcome);
			ConfigSync.Register(peer);
			peer.m_rpc.Invoke(HelloRpc, VikingMod.Version);
		}

		/// <summary>
		/// Checks a joining player before the server lets them in, refusing one without the server's version of the mod
		/// </summary>
		/// <param name="rpc">The player's connection</param>
		/// <returns>True to let them in</returns>
		public static bool Allow(ZRpc rpc)
		{
			string player = rpc.GetSocket().GetHostName();
			string required = "This server requires " + VikingMod.Name + " " + VikingMod.Version;
			if (!clientVersions.TryGetValue(rpc, out string version))
			{
				VikingMod.Log.LogWarning("Refused " + player + ", who doesn't have " + VikingMod.Name + ", which this server requires");
				ConnectRefusal.Refuse(rpc, required + ", and you don't have it");
				return(false);
			}
			if (version != VikingMod.Version)
			{
				VikingMod.Log.LogWarning("Refused " + player + ", who has " + VikingMod.Name + " " + version + ", since this server has " + VikingMod.Version);
				ConnectRefusal.Refuse(rpc, required + ", and you have " + version);
				return(false);
			}
			return(true);
		}

		/// <summary>
		/// Forgets the server, and the versions of its clients, when leaving a world, and what the mod noted in the world
		/// (see OnLeave)
		/// </summary>
		public static void Clear()
		{
			serverHasMod = false;
			clientVersions.Clear();
			OnLeave();
		}

		/// <summary>
		/// Forgets what the mod noted in the world being left, which is by the world's ZDOs or in its time
		/// </summary>
		static partial void OnLeave();

		/// <summary>
		/// Records a joining client's version and, when it's the server's, answers it and sends it the server's
		/// settings, on the server
		/// </summary>
		/// <param name="rpc">The client's connection</param>
		/// <param name="version">The client's version of the mod</param>
		private static void RPC_Hello(ZRpc rpc, string version)
		{
			ForgetDisconnected();
			clientVersions[rpc] = version;
			if (version != VikingMod.Version)
			{
				return;
			}
			rpc.Invoke(WelcomeRpc, VikingMod.Version);
			ConfigSync.AddClient(rpc);
		}

		/// <summary>
		/// Turns the mod on once the server has answered, on a client
		/// </summary>
		/// <param name="rpc">The server's connection</param>
		/// <param name="version">The server's version of the mod</param>
		private static void RPC_Welcome(ZRpc rpc, string version)
		{
			serverHasMod = true;
			VikingMod.Log.LogInfo("The server has " + VikingMod.Name + " " + version + ", so it runs in this world");
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
	}

	/// <summary>
	/// Sets up the server check on each new connection
	/// </summary>
	[HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
	internal static class ServerCheckConnectionPatch
	{
		/// <summary>
		/// Registers the check's messages after the game's own, and says hello from a client
		/// </summary>
		/// <param name="peer">The other end of the connection</param>
		private static void Postfix(ZNetPeer peer)
		{
			ServerCheck.OnNewConnection(peer);
		}
	}

	/// <summary>
	/// Refuses a joining player without the server's version of the mod
	/// </summary>
	[HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_PeerInfo))]
	internal static class ServerCheckPeerInfoPatch
	{
		/// <summary>
		/// Stops the game letting the player in when the server refuses them
		/// </summary>
		/// <param name="__instance">The network manager</param>
		/// <param name="rpc">The player's connection</param>
		/// <returns>False to skip the game's own handling</returns>
		private static bool Prefix(ZNet __instance, ZRpc rpc)
		{
			return(!__instance.IsServer() || ServerCheck.Allow(rpc));
		}
	}

	/// <summary>
	/// Forgets the server, its settings and what was noted in its world when leaving a world
	/// </summary>
	[HarmonyPatch(typeof(ZNet), nameof(ZNet.OnDestroy))]
	internal static class ServerCheckLeavePatch
	{
		/// <summary>
		/// Turns the mod off until the next server answers and goes back to this game's own settings
		/// </summary>
		private static void Postfix()
		{
			ServerCheck.Clear();
			ConfigSync.Leave();
		}
	}
}
