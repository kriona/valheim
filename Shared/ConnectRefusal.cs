using HarmonyLib;
using UnityEngine;

namespace Kriona.Shared
{
	/// <summary>
	/// Tells a player why a server refused them, since the game itself only says the version doesn't match
	/// </summary>
	/// <remarks>
	/// The server sends the reason just before the game's own error. Every plugin compiles its own copy of this class
	/// and registers the same message, so a client with any of the mods receives the reason - the last copy to register
	/// on a connection is the one that gets it, and only that copy adds it to the error shown back at the main menu
	/// </remarks>
	internal static class ConnectRefusal
	{
		private const string RefusedRpc = "kriona.Refused";

		/// <summary>
		/// Why the server this client last connected to refused it, or null when it wasn't refused
		/// </summary>
		private static string reason;

		/// <summary>
		/// Refuses a joining player with ErrorVersion, after sending them the reason, on the server
		/// </summary>
		/// <param name="rpc">The player's connection</param>
		/// <param name="text">Why they were refused, e.g. which mod they need</param>
		public static void Refuse(ZRpc rpc, string text)
		{
			rpc.Invoke(RefusedRpc, text);
			rpc.Invoke("Error", (int)ZNet.ConnectionStatus.ErrorVersion);
		}

		/// <summary>
		/// Forgets the last refusal and listens for the server's reason, on a client
		/// </summary>
		/// <param name="peer">The server's end of the connection</param>
		public static void OnNewConnection(ZNetPeer peer)
		{
			reason = null;
			if (ZNet.instance.IsServer())
			{
				return;
			}
			peer.m_rpc.Register<string>(RefusedRpc, RPC_Refused);
		}

		/// <summary>
		/// Adds the server's reason below the game's "incompatible version" error, then forgets it
		/// </summary>
		/// <param name="menu">The main menu</param>
		/// <param name="status">The connection status the error is shown for</param>
		public static void OnShowConnectError(FejdStartup menu, ZNet.ConnectionStatus status)
		{
			if (reason == null)
			{
				return;
			}
			string text = reason;
			reason = null;
			if (status != ZNet.ConnectionStatus.ErrorVersion)
			{
				return;
			}
			// The error is a TextMeshPro text, which the shared code can't reference, so it's reached by name
			Traverse error = Traverse.Create(menu).Field("m_connectionFailedError").Property("text");
			error.SetValue(error.GetValue<string>() + "\n\n" + text);
		}

		/// <summary>
		/// Logs the server's reason and keeps it for the error shown at the main menu, on a client
		/// </summary>
		/// <param name="rpc">The server's connection</param>
		/// <param name="text">Why the server refused this game</param>
		private static void RPC_Refused(ZRpc rpc, string text)
		{
			reason = text;
			Debug.LogWarning("The server refused this game: " + text);
		}
	}

	/// <summary>
	/// Listens for a server's reason for refusing this game on each new connection
	/// </summary>
	[HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
	internal static class ConnectRefusalConnectionPatch
	{
		/// <summary>
		/// Registers the reason's message after the game's own
		/// </summary>
		/// <param name="peer">The other end of the connection</param>
		private static void Postfix(ZNetPeer peer)
		{
			ConnectRefusal.OnNewConnection(peer);
		}
	}

	/// <summary>
	/// Shows a server's reason for refusing this game in the main menu's connection error
	/// </summary>
	[HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.ShowConnectError))]
	internal static class ConnectRefusalErrorPatch
	{
		/// <summary>
		/// Adds the reason after the game has set its own error text
		/// </summary>
		/// <param name="__instance">The main menu</param>
		/// <param name="statusOverride">The status the error is shown for, or None for the last connection's</param>
		private static void Postfix(FejdStartup __instance, ZNet.ConnectionStatus statusOverride)
		{
			ZNet.ConnectionStatus status = ((statusOverride == ZNet.ConnectionStatus.None) ? ZNet.GetConnectionStatus() : statusOverride);
			ConnectRefusal.OnShowConnectError(__instance, status);
		}
	}
}
