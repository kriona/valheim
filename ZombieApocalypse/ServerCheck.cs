using HarmonyLib;

namespace ZombieApocalypse
{
	/// <summary>
	/// Keeps the mod from raising zombies on a server that doesn't have it
	/// </summary>
	/// <remarks>
	/// A client with the mod says hello as soon as it connects, and a server with the mod answers. The client only
	/// raises zombies once that answer arrives, so on a server without the mod it stays out of the way. Single player,
	/// hosting and a dedicated server always count as having the mod, since the game running them is the server
	/// </remarks>
	internal static class ServerCheck
	{
		private const string HelloRpc = Plugin.PluginGuid + ".Hello";
		private const string WelcomeRpc = Plugin.PluginGuid + ".Welcome";

		/// <summary>
		/// Whether the server this client is connected to answered its hello
		/// </summary>
		private static bool serverHasMod;

		/// <summary>
		/// Whether the mod may raise zombies in the world being played
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
		/// answer and says hello
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
			peer.m_rpc.Invoke(HelloRpc, Plugin.PluginVersion);
		}

		/// <summary>
		/// Forgets the server when leaving a world
		/// </summary>
		public static void Clear()
		{
			serverHasMod = false;
		}

		/// <summary>
		/// Answers a client with the mod and sends it the server's settings, on the server
		/// </summary>
		/// <param name="rpc">The client's connection</param>
		/// <param name="version">The client's version of the mod</param>
		private static void RPC_Hello(ZRpc rpc, string version)
		{
			rpc.Invoke(WelcomeRpc, Plugin.PluginVersion);
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
			Plugin.Log.LogInfo("The server has " + Plugin.PluginName + " " + version + ", so zombies can rise");
		}
	}

	/// <summary>
	/// Sets up the server check on each new connection
	/// </summary>
	[HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
	public static class ServerCheckConnectionPatch
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
	/// Forgets the server and its settings when leaving a world
	/// </summary>
	[HarmonyPatch(typeof(ZNet), nameof(ZNet.OnDestroy))]
	public static class ServerCheckLeavePatch
	{
		/// <summary>
		/// Turns the mod off until the next server answers, and goes back to this game's own settings
		/// </summary>
		private static void Postfix()
		{
			ServerCheck.Clear();
			ConfigSync.Leave();
		}
	}
}
