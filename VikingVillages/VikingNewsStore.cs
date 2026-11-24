using System.Collections.Generic;
using System.Text.RegularExpressions;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Keeps the world's news (see VikingNews) on the server and a copy on every client, and has a living viking tell a
	/// player the latest news about them as it greets them or they talk to it - when there's some since it last talked
	/// to them, at TellChance, and only while more vikings are still to tell it
	/// </summary>
	/// <remarks>
	/// Clients report what happens to the server (see VikingNewsWatch), which adds it to the news, keeps the news in a
	/// record of its own saved with the world, and sends it to every client whenever it changes - and to a client that
	/// joins. A viking telling news has its owner tell the server, so the next viking says something else. The record is
	/// a ZDO far past the edge of the world, where no player ever loads it
	/// </remarks>
	internal static class VikingNewsStore
	{
		/// <summary>
		/// The routed RPCs that report an event to the server, tell it a viking told some news, send clients the news, and
		/// ask the server for it
		/// </summary>
		public const string ReportRpc = Plugin.PluginGuid + ".NewsReport";
		public const string MentionRpc = Plugin.PluginGuid + ".NewsMention";
		public const string StateRpc = Plugin.PluginGuid + ".NewsState";
		public const string RequestRpc = Plugin.PluginGuid + ".NewsRequest";

		/// <summary>
		/// The prefab hash of the record the server keeps the news in - one no prefab has - where it stands, and the key
		/// the news is written to
		/// </summary>
		private const string RecordPrefab = "kriona.VikingVillages.newsRecord";
		private static readonly Vector3 RecordPosition = new Vector3(-15000f, -1000f, -15000f);
		private static readonly int NewsKey = "kriona.VikingVillages.news".GetStableHashCode();

		/// <summary>
		/// How many game days news is told for, how likely a viking is to tell a player their news, and how many vikings
		/// tell each event, picked at random
		/// </summary>
		private const float MaxAgeDays = 10f;
		private const float TellChance = 0.9f;
		private const int MinTellers = 2;
		private const int MaxTellers = 4;

		/// <summary>
		/// How often the server drops news too old to tell, in seconds
		/// </summary>
		private const float ExpireInterval = 60f;

		/// <summary>
		/// A text tag in a creature's name, like the color of a miniboss's
		/// </summary>
		private static readonly Regex TextTag = new Regex("<[^>]*>");

		/// <summary>
		/// The news - the server's own, or a client's copy of it
		/// </summary>
		private static VikingNews news = new VikingNews();

		/// <summary>
		/// On the server, the record the news is kept in, once found or made
		/// </summary>
		private static ZDO record;

		/// <summary>
		/// On a client, whether it has asked the server for the news, and on the server, when it next drops old news, in
		/// Time.time seconds
		/// </summary>
		private static bool requested;
		private static float nextExpire;

		/// <summary>
		/// Registers the RPCs with a world's routed RPC, forgetting the news of the world being left
		/// </summary>
		public static void Register()
		{
			ZRoutedRpc.instance.Register<ZPackage>(ReportRpc, OnReport);
			ZRoutedRpc.instance.Register<int, string, int>(MentionRpc, OnMention);
			ZRoutedRpc.instance.Register<ZPackage>(StateRpc, OnState);
			ZRoutedRpc.instance.Register(RequestRpc, OnRequest);
			news = new VikingNews();
			record = null;
			requested = false;
			nextExpire = 0f;
		}

		/// <summary>
		/// Loads the news on the server once the world has, and drops what's too old every ExpireInterval seconds, or has
		/// a client ask the server for it once its player is in the world - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (ZNet.instance == null || ZRoutedRpc.instance == null || ZDOMan.instance == null)
			{
				return;
			}
			if (!ZNet.instance.IsServer())
			{
				if (!requested && Player.m_localPlayer != null)
				{
					requested = true;
					ZRoutedRpc.instance.InvokeRoutedRPC(RequestRpc);
				}
				return;
			}
			if (record == null)
			{
				if (!ZNet.instance.IsDedicated() && Player.m_localPlayer == null)
				{
					return;
				}
				Load();
			}
			if (Time.time >= nextExpire)
			{
				nextExpire = Time.time + ExpireInterval;
				if (news.Expire(NetTime.Now(), MaxAge()) > 0)
				{
					Changed();
				}
			}
		}

		/// <summary>
		/// Reports an event to the server, which adds it to the news (see VikingNews.Add)
		/// </summary>
		/// <param name="kind">The kind of event</param>
		/// <param name="subject">What it was about</param>
		/// <param name="detail">What a line names in it, like a creature's name as the game writes it, or ""</param>
		/// <param name="players">The players it happened to, by player ID</param>
		/// <param name="firstOnly">True for a first, which is only news to those who haven't had it</param>
		public static void Report(string kind, string subject, string detail, List<long> players, bool firstOnly)
		{
			if (ZRoutedRpc.instance == null || players == null || players.Count == 0)
			{
				return;
			}
			ZPackage pkg = new ZPackage();
			pkg.Write(kind);
			pkg.Write(subject ?? "");
			pkg.Write(detail ?? "");
			pkg.Write(firstOnly);
			pkg.Write(players.Count);
			foreach (long player in players)
			{
				pkg.Write(player);
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(ReportRpc, pkg);
		}

		/// <summary>
		/// Has a living or dvergr viking tell a player the latest news about them, in its kind's words, on the viking's
		/// owner, noting that it talked to them now either way - so it only ever brings up news from after it last talked
		/// to them
		/// </summary>
		/// <param name="viking">The viking, owned by this client</param>
		/// <param name="player">The player it's talking to, who it already knows</param>
		/// <param name="vikingBiome">The index of the viking's biome (see VikingGear.Biomes)</param>
		/// <returns>What it says, or null when it has no news for them</returns>
		public static string Tell(Character viking, Player player, int vikingBiome)
		{
			if (viking == null || player == null || viking.m_nview == null || !viking.m_nview.IsValid() || !viking.m_nview.IsOwner())
			{
				return(null);
			}
			ZDO zdo = viking.m_nview.GetZDO();
			string id = VikingIdentity.Ensure(viking.m_nview);
			long playerId = player.GetPlayerID();
			long now = NetTime.Now();
			long since = VikingIdentity.LastTalk(zdo, playerId);
			VikingIdentity.Talked(zdo, playerId, now);
			VikingNews.Event latest = news.NewsFor(playerId, id, since, now, MaxAge());
			if (latest == null || Random.value >= TellChance)
			{
				return(null);
			}
			string line = VikingNewsLines.Choose(latest, vikingBiome, player.GetPlayerName(), CreatureName(latest.Detail), (int count) => Random.Range(0, count), out int hash, VikingCore.IsDvergrViking(viking));
			if (line == null)
			{
				return(null);
			}
			Plugin.Log.LogInfo(TestLog.Label(viking) + " tells " + player.GetPlayerName() + " the news of " + Describe(latest));
			if (ZNet.instance.IsServer())
			{
				ApplyMention(latest.Id, id, hash);
			}
			else
			{
				news.Mention(latest.Id, id, hash);
				ZRoutedRpc.instance.InvokeRoutedRPC(MentionRpc, latest.Id, id, hash);
			}
			return(line);
		}

		/// <summary>
		/// Counts the events in the news on this client, for the debug hover text
		/// </summary>
		/// <returns>How many there are</returns>
		public static int Count()
		{
			return(news.Events.Count);
		}

		/// <summary>
		/// Gets a creature's name as this client's language writes it, without text tags
		/// </summary>
		/// <param name="detail">The name as the game writes it, or a player's name</param>
		/// <returns>The name</returns>
		private static string CreatureName(string detail)
		{
			if (string.IsNullOrEmpty(detail))
			{
				return("");
			}
			string name = ((Localization.instance != null) ? Localization.instance.Localize(detail) : detail);
			return(TextTag.Replace(name, ""));
		}

		/// <summary>
		/// Describes an event for the log
		/// </summary>
		/// <param name="news">The event</param>
		/// <returns>Its kind and subject</returns>
		private static string Describe(VikingNews.Event news)
		{
			return(news.Kind + ((news.Subject.Length > 0) ? " " + news.Subject : "") + " (event " + news.Id + ")");
		}

		/// <summary>
		/// Gets how long news is told for
		/// </summary>
		/// <returns>The ticks</returns>
		private static long MaxAge()
		{
			float day = ((EnvMan.instance != null) ? (float)EnvMan.instance.m_dayLengthSec : 1200f);
			return(NetTime.Ticks(day * MaxAgeDays));
		}

		/// <summary>
		/// Finds the record the news is kept in on the server, or makes it in a world that has none, and reads the news
		/// </summary>
		private static void Load()
		{
			List<ZDO> found = new List<ZDO>();
			int index = 0;
			while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(RecordPrefab, found, ref index))
			{
			}
			foreach (ZDO zdo in found)
			{
				if (record == null || zdo.GetByteArray(NewsKey) != null)
				{
					record = zdo;
				}
			}
			if (record == null)
			{
				int prefab = RecordPrefab.GetStableHashCode();
				record = ZDOMan.instance.CreateNewZDO(RecordPosition, prefab);
				record.SetPrefab(prefab);
				record.Persistent = true;
			}
			record.SetOwner(ZDOMan.GetSessionID());
			news = VikingNews.Decode(record.GetByteArray(NewsKey));
			Plugin.Log.LogInfo("Loaded " + news.Events.Count + " news events and " + news.Firsts.Count + " firsts");
		}

		/// <summary>
		/// Saves the news in its record on the server and sends it to every client
		/// </summary>
		private static void Changed()
		{
			if (record == null)
			{
				Load();
			}
			record.Set(NewsKey, news.Encode(true));
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, StateRpc, new ZPackage(news.Encode(false)));
		}

		/// <summary>
		/// Adds a reported event to the news on the server, told by MinTellers to MaxTellers vikings
		/// </summary>
		/// <param name="sender">The client that reported it</param>
		/// <param name="pkg">The event, as Report writes it</param>
		private static void OnReport(long sender, ZPackage pkg)
		{
			if (!ZNet.instance.IsServer())
			{
				return;
			}
			if (record == null)
			{
				Load();
			}
			string kind = pkg.ReadString();
			string subject = pkg.ReadString();
			string detail = pkg.ReadString();
			bool firstOnly = pkg.ReadBool();
			int count = pkg.ReadInt();
			List<long> players = new List<long>();
			for (int i = 0; i < count; i++)
			{
				players.Add(pkg.ReadLong());
			}
			long now = NetTime.Now();
			news.Expire(now, MaxAge());
			VikingNews.Event added = news.Add(kind, subject, detail, players, firstOnly, now, Random.Range(MinTellers, MaxTellers + 1));
			if (added == null)
			{
				return;
			}
			Plugin.Log.LogInfo("News: " + Describe(added) + " for " + added.Players.Count + " player(s), told by " + added.Remaining + " vikings");
			Changed();
		}

		/// <summary>
		/// Notes on the server that a viking told some news
		/// </summary>
		/// <param name="sender">The client the viking is on</param>
		/// <param name="id">The event's number</param>
		/// <param name="viking">The viking's ID</param>
		/// <param name="line">The stable hash of the line it said</param>
		private static void OnMention(long sender, int id, string viking, int line)
		{
			if (ZNet.instance.IsServer())
			{
				ApplyMention(id, viking, line);
			}
		}

		/// <summary>
		/// Notes that a viking told some news on the server, saving and sending the news when it changed
		/// </summary>
		/// <param name="id">The event's number</param>
		/// <param name="viking">The viking's ID</param>
		/// <param name="line">The stable hash of the line it said</param>
		private static void ApplyMention(int id, string viking, int line)
		{
			if (news.Mention(id, viking, line))
			{
				Changed();
			}
		}

		/// <summary>
		/// Takes the news the server sent, on a client
		/// </summary>
		/// <param name="sender">The server</param>
		/// <param name="pkg">The news, as VikingNews.Encode writes it</param>
		private static void OnState(long sender, ZPackage pkg)
		{
			if (ZNet.instance == null || ZNet.instance.IsServer())
			{
				return;
			}
			news = VikingNews.Decode(pkg.GetArray());
		}

		/// <summary>
		/// Sends the news to a client that asked for it, on the server
		/// </summary>
		/// <param name="sender">The client</param>
		private static void OnRequest(long sender)
		{
			if (!ZNet.instance.IsServer())
			{
				return;
			}
			if (record == null)
			{
				Load();
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(sender, StateRpc, new ZPackage(news.Encode(false)));
		}
	}
}
