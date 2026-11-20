using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace ZombieApocalypse
{
	/// <summary>
	/// Makes clients use the server's settings while they're connected to it
	/// </summary>
	/// <remarks>
	/// A server sends its settings to each client with the mod once the client says hello (see ServerCheck), and again
	/// to all of them whenever a setting changes, from its config file or Configuration Manager. A client sets its own
	/// entries to the server's values without saving them, puts them back whenever its own file or Configuration
	/// Manager changes one, and reads its own file again when it leaves the world. Settings in LocalSettings only
	/// affect the player's own game, so each player keeps their own
	/// </remarks>
	internal static class ConfigSync
	{
		private const string ConfigRpc = Plugin.PluginGuid + ".Config";

		/// <summary>
		/// The keys of settings each player keeps their own value of
		/// </summary>
		private static readonly HashSet<string> LocalSettings = new HashSet<string> { "ZombieBuzzVolume", "ZombieHazeStrength" };

		/// <summary>
		/// The connections of the clients with the mod, on the server
		/// </summary>
		private static readonly HashSet<ZRpc> clients = new HashSet<ZRpc>();

		private static ConfigFile config;

		/// <summary>
		/// The server's values by setting, on a client that has received them, or null
		/// </summary>
		private static Dictionary<ConfigDefinition, string> serverValues;

		/// <summary>
		/// The client's SaveOnConfigSet from before it took the server's values, put back when it leaves
		/// </summary>
		private static bool savedSaveOnConfigSet;

		/// <summary>
		/// Set while entries are being set by the sync itself, so the changes don't count as the player's
		/// </summary>
		private static bool applying;

		/// <summary>
		/// Set when a setting changed, so the server sends its settings again or the client puts the server's back
		/// </summary>
		private static bool changed;

		/// <summary>
		/// Starts listening for changes to the plugin's settings
		/// </summary>
		/// <param name="pluginConfig">The plugin's BepInEx config</param>
		public static void Init(ConfigFile pluginConfig)
		{
			config = pluginConfig;
			config.SettingChanged += OnSettingChanged;
			config.ConfigReloaded += OnConfigReloaded;
		}

		/// <summary>
		/// Sends the server's settings again, or puts the server's back on a client, after a setting changed - call it
		/// from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (!changed)
			{
				return;
			}
			changed = false;
			if (ZNet.instance == null)
			{
				return;
			}
			if (ZNet.instance.IsServer())
			{
				SendToAll();
			}
			else if (serverValues != null)
			{
				Apply();
				MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, Plugin.PluginName + ": using the server's settings");
			}
		}

		/// <summary>
		/// Listens for the server's settings on a client's connection to it
		/// </summary>
		/// <param name="peer">The server</param>
		public static void Register(ZNetPeer peer)
		{
			peer.m_rpc.Register<ZPackage>(ConfigRpc, RPC_Config);
		}

		/// <summary>
		/// Sends the server's settings to a client with the mod that just said hello, and to it again whenever they
		/// change
		/// </summary>
		/// <param name="rpc">The client's connection</param>
		public static void AddClient(ZRpc rpc)
		{
			clients.Add(rpc);
			Send(rpc);
		}

		/// <summary>
		/// Forgets the clients on a server, and reads the client's own settings again on a client, when leaving a world
		/// </summary>
		public static void Leave()
		{
			clients.Clear();
			changed = false;
			if (serverValues == null)
			{
				return;
			}
			serverValues = null;
			applying = true;
			try
			{
				config.Reload();
			}
			finally
			{
				applying = false;
				config.SaveOnConfigSet = savedSaveOnConfigSet;
			}
			Plugin.Log.LogInfo("Left the server, so using this game's own settings again");
		}

		/// <summary>
		/// Notes a change to a setting that's shared, unless the sync made it
		/// </summary>
		/// <param name="sender">The plugin's config</param>
		/// <param name="args">The setting that changed</param>
		private static void OnSettingChanged(object sender, SettingChangedEventArgs args)
		{
			if (!applying && !LocalSettings.Contains(args.ChangedSetting.Definition.Key))
			{
				changed = true;
			}
		}

		/// <summary>
		/// Notes that the config file was read again, unless the sync read it
		/// </summary>
		/// <param name="sender">The plugin's config</param>
		/// <param name="args">Unused</param>
		private static void OnConfigReloaded(object sender, EventArgs args)
		{
			if (!applying)
			{
				changed = true;
			}
		}

		/// <summary>
		/// Sends the server's settings to every client with the mod, forgetting the ones that have disconnected
		/// </summary>
		private static void SendToAll()
		{
			HashSet<ZRpc> connected = new HashSet<ZRpc>();
			foreach (ZNetPeer peer in ZNet.instance.GetPeers())
			{
				if (peer.m_rpc != null && clients.Contains(peer.m_rpc))
				{
					connected.Add(peer.m_rpc);
					Send(peer.m_rpc);
				}
			}
			clients.IntersectWith(connected);
		}

		/// <summary>
		/// Sends the server's shared settings to a client
		/// </summary>
		/// <param name="rpc">The client's connection</param>
		private static void Send(ZRpc rpc)
		{
			List<ConfigEntryBase> entries = new List<ConfigEntryBase>();
			foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> pair in config)
			{
				if (!LocalSettings.Contains(pair.Key.Key))
				{
					entries.Add(pair.Value);
				}
			}
			ZPackage package = new ZPackage();
			package.Write(entries.Count);
			foreach (ConfigEntryBase entry in entries)
			{
				package.Write(entry.Definition.Section);
				package.Write(entry.Definition.Key);
				package.Write(entry.GetSerializedValue());
			}
			rpc.Invoke(ConfigRpc, package);
		}

		/// <summary>
		/// Takes the server's settings on a client, without saving them to its own file
		/// </summary>
		/// <param name="rpc">The server's connection</param>
		/// <param name="package">The settings, as a count and each one's section, key and value</param>
		private static void RPC_Config(ZRpc rpc, ZPackage package)
		{
			Dictionary<ConfigDefinition, string> values = new Dictionary<ConfigDefinition, string>();
			int count = package.ReadInt();
			for (int i = 0; i < count; i++)
			{
				string section = package.ReadString();
				string key = package.ReadString();
				values[new ConfigDefinition(section, key)] = package.ReadString();
			}
			if (serverValues == null)
			{
				savedSaveOnConfigSet = config.SaveOnConfigSet;
				config.SaveOnConfigSet = false;
				Plugin.Log.LogInfo("Using the server's settings while connected to it");
			}
			serverValues = values;
			Apply();
		}

		/// <summary>
		/// Sets the client's entries to the server's values, skipping any this version doesn't have
		/// </summary>
		private static void Apply()
		{
			applying = true;
			try
			{
				foreach (KeyValuePair<ConfigDefinition, string> value in serverValues)
				{
					if (config.ContainsKey(value.Key))
					{
						config[value.Key].SetSerializedValue(value.Value);
					}
				}
			}
			finally
			{
				applying = false;
			}
		}
	}
}
