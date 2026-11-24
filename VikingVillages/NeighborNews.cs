using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A thing that happened in a village, for the vikings of the villages near it to hear about (see NeighborNews) -
	/// what it was, where the village stands and its name, what a line names in it, and when it happened, in ZNet time
	/// ticks
	/// </summary>
	internal class NeighborEvent
	{
		/// <summary>
		/// The kinds of thing - a village disbanded or started, a quest come home, a death and a newcomer
		/// </summary>
		public const string Disbanded = "disbanded";
		public const string Started = "started";
		public const string QuestBack = "quest";
		public const string Death = "death";
		public const string Newcomer = "newcomer";

		public string Kind = "";
		public Vector3 Farm;
		public string Village = "";
		public string Detail = "";
		public long At;

		/// <summary>
		/// Writes the thing to a package
		/// </summary>
		/// <param name="pkg">The package</param>
		public void Write(ZPackage pkg)
		{
			pkg.Write(Kind);
			pkg.Write(Farm);
			pkg.Write(Village);
			pkg.Write(Detail);
			pkg.Write(At);
		}

		/// <summary>
		/// Reads a thing from a package
		/// </summary>
		/// <param name="pkg">The package</param>
		/// <returns>The thing</returns>
		public static NeighborEvent Read(ZPackage pkg)
		{
			NeighborEvent happened = new NeighborEvent();
			happened.Kind = pkg.ReadString();
			happened.Farm = pkg.ReadVector3();
			happened.Village = pkg.ReadString();
			happened.Detail = pkg.ReadString();
			happened.At = pkg.ReadLong();
			return(happened);
		}
	}

	/// <summary>
	/// The news the villages hear from each other - by rune stick, raven, or from travelers who came from there - kept
	/// on the server and a copy on every client, for a viking to tell a player or another viking now and then (see
	/// NeighborSpeech)
	/// </summary>
	/// <remarks>
	/// The client running a village reports what happens in it (see Report) - its being disbanded or started, a quest
	/// coming home, a death or a newcomer - to the server, which keeps the last MaxEvents for KeepDays game days in a
	/// record of its own saved with the world, a ZDO far past the edge of the world, and sends them to every client
	/// whenever they change, and to a client that joins. A village hears about what happened in villages within Reach of
	/// it in the last HearDays game days - a village being disbanded most of all
	/// </remarks>
	internal static class NeighborNews
	{
		/// <summary>
		/// The routed RPCs that report a thing to the server, send clients the news, and ask the server for it
		/// </summary>
		private const string ReportRpc = Plugin.PluginGuid + ".NeighborReport";
		private const string StateRpc = Plugin.PluginGuid + ".NeighborState";
		private const string RequestRpc = Plugin.PluginGuid + ".NeighborRequest";

		/// <summary>
		/// The prefab hash of the record the server keeps the news in - one no prefab has - where it stands, and the key
		/// the news is written to
		/// </summary>
		private const string RecordPrefab = "kriona.VikingVillages.neighborRecord";
		private static readonly Vector3 RecordPosition = new Vector3(-15000f, -1000f, -15200f);
		private static readonly int EventsKey = "kriona.VikingVillages.neighborEvents".GetStableHashCode();

		/// <summary>
		/// The most things the news keeps, how many game days it keeps them, how many game days a village hears about a
		/// thing for, and how far from a village another can be for it to hear about it, in meters
		/// </summary>
		private const int MaxEvents = 60;
		private const float KeepDays = 5f;
		internal const float HearDays = 3f;
		internal const float Reach = 3000f;

		/// <summary>
		/// How likely a village that has heard of a village being disbanded talks about that over anything else
		/// </summary>
		private const float DisbandedFirst = 0.6f;

		/// <summary>
		/// The version of how the news is written, which a reader turns down news written another way by
		/// </summary>
		private const int FormatVersion = 1;

		/// <summary>
		/// The news - the server's own, or a client's copy of it
		/// </summary>
		private static List<NeighborEvent> events = new List<NeighborEvent>();

		/// <summary>
		/// On the server, the record the news is kept in, and on a client, whether it has asked the server for the news
		/// </summary>
		private static ZDO record;
		private static bool requested;

		/// <summary>
		/// Registers the RPCs with a world's routed RPC, forgetting the news of the world being left
		/// </summary>
		public static void Register()
		{
			ZRoutedRpc.instance.Register<ZPackage>(ReportRpc, OnReport);
			ZRoutedRpc.instance.Register<ZPackage>(StateRpc, OnState);
			ZRoutedRpc.instance.Register(RequestRpc, OnRequest);
			events = new List<NeighborEvent>();
			record = null;
			requested = false;
		}

		/// <summary>
		/// Loads the news on the server once the world has, or has a client ask the server for it once its player is in
		/// the world - call it from the plugin's Update
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
			if (record == null && (ZNet.instance.IsDedicated() || Player.m_localPlayer != null))
			{
				Load();
			}
		}

		/// <summary>
		/// Reports a thing that happened in a village to the server
		/// </summary>
		/// <param name="kind">What it was (see NeighborEvent)</param>
		/// <param name="farm">Where the village stands</param>
		/// <param name="village">The village's name, or ""</param>
		/// <param name="detail">What a line names in it, or ""</param>
		public static void Report(string kind, Vector3 farm, string village, string detail)
		{
			if (ZRoutedRpc.instance == null)
			{
				return;
			}
			NeighborEvent happened = new NeighborEvent { Kind = kind, Farm = farm, Village = (village ?? ""), Detail = (detail ?? ""), At = NetTime.Now() };
			ZPackage pkg = new ZPackage();
			happened.Write(pkg);
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), ReportRpc, pkg);
		}

		/// <summary>
		/// Picks a thing a village has heard about from the villages near it - a village being disbanded most of all, and
		/// for a viking that came from one of them, a thing from there most of all, since it brought the news itself
		/// </summary>
		/// <param name="farm">Where the village hearing it stands</param>
		/// <param name="from">The name of the village the viking came from, or ""</param>
		/// <param name="brought">Set to whether the viking brought the news itself</param>
		/// <returns>The thing, or null when the village has heard of nothing</returns>
		public static NeighborEvent Heard(Vector3 farm, string from, out bool brought)
		{
			brought = false;
			if (EnvMan.instance == null)
			{
				return(null);
			}
			List<NeighborEvent> heard = Hearable(events, farm, NetTime.Now(), NetTime.Ticks(EnvMan.instance.m_dayLengthSec * HearDays));
			if (heard.Count == 0)
			{
				return(null);
			}
			List<NeighborEvent> own = heard.FindAll((NeighborEvent happened) => from.Length > 0 && happened.Village == from);
			if (own.Count > 0 && Random.value < 0.5f)
			{
				brought = true;
				return(own[Random.Range(0, own.Count)]);
			}
			List<NeighborEvent> disbanded = heard.FindAll((NeighborEvent happened) => happened.Kind == NeighborEvent.Disbanded);
			if (disbanded.Count > 0 && Random.value < DisbandedFirst)
			{
				return(disbanded[Random.Range(0, disbanded.Count)]);
			}
			return(heard[Random.Range(0, heard.Count)]);
		}

		/// <summary>
		/// Lists the things a village can hear about - in another village within Reach of it, recently enough
		/// </summary>
		/// <param name="all">Every thing in the news</param>
		/// <param name="farm">Where the village stands</param>
		/// <param name="now">The time now, in ticks</param>
		/// <param name="window">How long ago a thing can have happened, in ticks</param>
		/// <returns>The things</returns>
		internal static List<NeighborEvent> Hearable(List<NeighborEvent> all, Vector3 farm, long now, long window)
		{
			return(all.FindAll((NeighborEvent happened) =>
			{
				float distance = TravelRoad.Flat(happened.Farm, farm);
				return(distance > TravelPlanner.FarmMatch && distance <= Reach && now - happened.At <= window && happened.At <= now);
			}));
		}

		/// <summary>
		/// Adds a thing to the news, dropping the oldest past MaxEvents and those older than the news is kept
		/// </summary>
		/// <param name="all">The news, changed</param>
		/// <param name="happened">The thing</param>
		/// <param name="now">The time now, in ticks</param>
		/// <param name="keep">How long the news keeps a thing, in ticks</param>
		internal static void Add(List<NeighborEvent> all, NeighborEvent happened, long now, long keep)
		{
			all.Add(happened);
			all.RemoveAll((NeighborEvent old) => now - old.At > keep);
			if (all.Count > MaxEvents)
			{
				all.RemoveRange(0, all.Count - MaxEvents);
			}
		}

		/// <summary>
		/// Writes the news as bytes
		/// </summary>
		/// <param name="all">The news</param>
		/// <returns>The bytes</returns>
		internal static byte[] Encode(List<NeighborEvent> all)
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(FormatVersion);
			pkg.Write(all.Count);
			foreach (NeighborEvent happened in all)
			{
				happened.Write(pkg);
			}
			return(pkg.GetArray());
		}

		/// <summary>
		/// Reads the news from the bytes it's written as
		/// </summary>
		/// <param name="bytes">The bytes, or null</param>
		/// <returns>The news, none when it can't be read</returns>
		internal static List<NeighborEvent> Decode(byte[] bytes)
		{
			List<NeighborEvent> all = new List<NeighborEvent>();
			if (bytes == null || bytes.Length == 0)
			{
				return(all);
			}
			try
			{
				ZPackage pkg = new ZPackage(bytes);
				if (pkg.ReadInt() != FormatVersion)
				{
					return(all);
				}
				int count = pkg.ReadInt();
				for (int i = 0; i < count; i++)
				{
					all.Add(NeighborEvent.Read(pkg));
				}
			}
			catch (System.Exception)
			{
				return(new List<NeighborEvent>());
			}
			return(all);
		}

		/// <summary>
		/// Finds the record the news is kept in, or makes it, and reads the news
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
				if (record == null || zdo.GetByteArray(EventsKey) != null)
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
			events = Decode(record.GetByteArray(EventsKey));
		}

		/// <summary>
		/// Adds a thing a client reported to the news, on the server, saving it and sending it to every client
		/// </summary>
		/// <param name="sender">The client</param>
		/// <param name="pkg">The thing</param>
		private static void OnReport(long sender, ZPackage pkg)
		{
			if (ZNet.instance == null || !ZNet.instance.IsServer() || EnvMan.instance == null)
			{
				return;
			}
			if (record == null)
			{
				Load();
			}
			NeighborEvent happened = NeighborEvent.Read(pkg);
			Add(events, happened, NetTime.Now(), NetTime.Ticks(EnvMan.instance.m_dayLengthSec * KeepDays));
			byte[] bytes = Encode(events);
			record.Set(EventsKey, bytes);
			Plugin.Log.LogInfo("Neighbor news: " + happened.Kind + " at the village at " + happened.Farm.ToString("F0") + ((happened.Village.Length > 0) ? " (" + happened.Village + ")" : "") + ((happened.Detail.Length > 0) ? ", " + happened.Detail : ""));
			TestLog.Count("Neighbor news", happened.Kind);
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, StateRpc, new ZPackage(bytes));
		}

		/// <summary>
		/// Takes the news the server sent, on a client
		/// </summary>
		/// <param name="sender">The server</param>
		/// <param name="pkg">The news</param>
		private static void OnState(long sender, ZPackage pkg)
		{
			if (ZNet.instance == null || ZNet.instance.IsServer())
			{
				return;
			}
			events = Decode(pkg.GetArray());
		}

		/// <summary>
		/// Sends the news to a client that asked for it, on the server
		/// </summary>
		/// <param name="sender">The client</param>
		private static void OnRequest(long sender)
		{
			if (ZNet.instance == null || !ZNet.instance.IsServer())
			{
				return;
			}
			if (record == null)
			{
				Load();
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(sender, StateRpc, new ZPackage(Encode(events)));
		}

		/// <summary>
		/// Describes the news, for the test run's snapshot
		/// </summary>
		/// <returns>The description, or "" when there's none</returns>
		public static string Describe()
		{
			if (events.Count == 0)
			{
				return("");
			}
			List<string> kinds = new List<string>();
			foreach (NeighborEvent happened in events)
			{
				kinds.Add(happened.Kind + " at " + ((happened.Village.Length > 0) ? happened.Village : happened.Farm.ToString("F0")));
			}
			return(events.Count + " things the villages heard of each other: " + string.Join(", ", kinds));
		}
	}
}
