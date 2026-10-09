using System.Collections.Generic;
using HarmonyLib;

namespace WhosOnline
{
	internal static class Online
	{
		/// <summary>
		/// Whether the list has been shown for the current connection
		/// </summary>
		public static bool Shown;

		/// <summary>
		/// Names of everyone in the server's player list except the local player
		/// </summary>
		/// <remarks>
		/// The local player is matched by name rather than character ID because the list the server sent may
		/// predate the local character existing. Only one matching entry is skipped so another player with the
		/// same name still appears
		/// </remarks>
		/// <param name="localName">The local player's name</param>
		/// <returns>The other players' names</returns>
		public static List<string> GetOtherNames(string localName)
		{
			List<string> names = new List<string>();
			bool skippedSelf = false;
			foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
			{
				if (!skippedSelf && info.m_name == localName)
				{
					skippedSelf = true;
					continue;
				}
				names.Add((string.IsNullOrEmpty(info.m_name) ? "(unknown)" : info.m_name));
			}
			names.Sort();
			return(names);
		}

		/// <summary>
		/// Shows the list of other players in an unlock-style popup and writes it to the chat window
		/// </summary>
		/// <param name="localName">The local player's name</param>
		public static void Show(string localName)
		{
			List<string> names = GetOtherNames(localName);
			string topic;
			string description;
			if (names.Count == 0)
			{
				topic = "No one else is online";
				description = "";
				Chat.instance.AddString("<color=yellow>" + topic + "</color>");
			}
			else
			{
				topic = "Online (" + names.Count + ")";
				description = string.Join(", ", names);
				Chat.instance.AddString("<color=yellow>" + topic + ": </color>" + description);
			}
			Chat.instance.m_hideTimer = 0f;
			QueuePopup(topic, description);
		}

		/// <summary>
		/// Queues a popup in the box the game uses for new recipes, with the map's player marker as its icon
		/// </summary>
		/// <remarks>
		/// The message goes straight into the queue rather than through QueueUnlockMsg, which would add it to
		/// the compendium log, follow it with a "new log entries" message and run player names through localization
		/// </remarks>
		/// <param name="topic">The popup's title</param>
		/// <param name="description">The text under the title</param>
		private static void QueuePopup(string topic, string description)
		{
			if (MessageHud.instance == null)
			{
				return;
			}
			MessageHud.UnlockMsg message = new MessageHud.UnlockMsg();
			message.m_icon = ((Minimap.instance != null) ? Minimap.instance.GetSprite(Minimap.PinType.Player) : null);
			message.m_topic = topic;
			message.m_description = description;
			MessageHud.instance.m_unlockMsgQueue.Enqueue(message);
		}
	}

	/// <summary>
	/// Clears the shown flag whenever a new ZNet starts, which happens on each connection to a server
	/// </summary>
	[HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
	public static class ZNetAwakePatch
	{
		/// <summary>
		/// Clears the shown flag
		/// </summary>
		private static void Postfix()
		{
			Online.Shown = false;
		}
	}

	/// <summary>
	/// Shows who is online the first time the local player spawns on a server hosted by someone else
	/// </summary>
	/// <remarks>
	/// The server sends its player list during the connection handshake, so it is already populated by the
	/// time the world has loaded and the player spawns. Respawns after death are skipped by the shown flag
	/// </remarks>
	[HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
	public static class PlayerOnSpawnedPatch
	{
		/// <summary>
		/// Shows the list when the local player spawns on another player's server and it hasn't been shown yet
		/// </summary>
		/// <param name="__instance">The player that spawned</param>
		private static void Postfix(Player __instance)
		{
			if (Online.Shown || __instance != Player.m_localPlayer)
			{
				return;
			}
			if (ZNet.instance == null || ZNet.instance.IsServer() || Chat.instance == null || Game.instance == null)
			{
				return;
			}
			Online.Shown = true;
			Online.Show(Game.instance.GetPlayerProfile().GetName());
		}
	}
}
