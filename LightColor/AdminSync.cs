namespace LightColor
{
	/// <summary>
	/// Sends the server's AdminOnly and AdminOnlyHelmet settings to the players who join it, so only admins can change
	/// colors or use the Helmet setting when they're on
	/// </summary>
	/// <remarks>
	/// The server sends the settings once a player is in, and again whenever they change. The game sends the server's
	/// admin list to every player, so the player's own game can tell whether they're an admin. Players without the
	/// mod ignore the message, and a server without the mod sends nothing, which leaves colors open to everyone
	/// </remarks>
	public static class AdminSync
	{
		private const string SettingsRpc = Plugin.PluginGuid + ".AdminSettings";

		/// <summary>
		/// Shown when a player who isn't an admin tries to change a color
		/// </summary>
		public const string DeniedMessage = "Only admins can change colors on this server";

		/// <summary>
		/// Whether the server this game is connected to lets only admins change colors
		/// </summary>
		private static bool serverAdminOnly;

		/// <summary>
		/// Whether the server this game is connected to lets only admins use the Helmet setting
		/// </summary>
		private static bool serverAdminOnlyHelmet;

		/// <summary>
		/// Whether the local player may change colors - always in single player and when hosting, and otherwise unless
		/// the server lets only admins change them and the player isn't one
		/// </summary>
		public static bool CanChangeColors
		{
			get
			{
				return(IsAllowed(serverAdminOnly));
			}
		}

		/// <summary>
		/// Whether the local player may color their helmet with the Helmet setting - always in single player and when
		/// hosting, and otherwise unless the server lets only admins use it and the player isn't one
		/// </summary>
		public static bool CanUseHelmet
		{
			get
			{
				return(IsAllowed(serverAdminOnlyHelmet));
			}
		}

		/// <summary>
		/// Listens for the server's settings on a client's new connection, starting with everything open to everyone
		/// until the server says otherwise
		/// </summary>
		/// <param name="peer">The other end of the connection</param>
		public static void OnNewConnection(ZNetPeer peer)
		{
			if (ZNet.instance.IsServer())
			{
				return;
			}
			serverAdminOnly = false;
			serverAdminOnlyHelmet = false;
			peer.m_rpc.Register<ZPackage>(SettingsRpc, RPC_Settings);
		}

		/// <summary>
		/// Sends the server's settings to one player
		/// </summary>
		/// <param name="rpc">The player's connection</param>
		public static void Send(ZRpc rpc)
		{
			ZPackage package = new ZPackage();
			package.Write(Plugin.AdminOnly.Value);
			package.Write(Plugin.AdminOnlyHelmet.Value);
			rpc.Invoke(SettingsRpc, package);
		}

		/// <summary>
		/// Sends the server's settings to every connected player, after one of them changes
		/// </summary>
		public static void SendToAll()
		{
			ZNet net = ZNet.instance;
			if (net == null || !net.IsServer())
			{
				return;
			}
			foreach (ZNetPeer peer in net.GetPeers())
			{
				if (peer.m_rpc != null && peer.m_uid != 0)
				{
					Send(peer.m_rpc);
				}
			}
		}

		/// <summary>
		/// Whether the local player may do something the server can limit to admins
		/// </summary>
		/// <param name="adminOnly">Whether the server lets only admins do it</param>
		/// <returns>True in single player, when hosting, when the server doesn't limit it or when the player is an admin</returns>
		private static bool IsAllowed(bool adminOnly)
		{
			ZNet net = ZNet.instance;
			if (net == null || net.IsServer() || !adminOnly)
			{
				return(true);
			}
			return(net.LocalPlayerIsAdminOrHost());
		}

		/// <summary>
		/// Records the settings the server sent
		/// </summary>
		/// <param name="rpc">The server's connection</param>
		/// <param name="package">Whether only admins can change colors, then whether only admins can use the Helmet setting</param>
		private static void RPC_Settings(ZRpc rpc, ZPackage package)
		{
			serverAdminOnly = package.ReadBool();
			serverAdminOnlyHelmet = package.ReadBool();
		}
	}
}
