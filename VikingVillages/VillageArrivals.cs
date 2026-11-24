using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The groups of vikings on their way to a village - those the server keeps out of the world on the road or out on a
	/// quest (see VillageTravel), and those walking the road in the world - with what brings each, where it is and when
	/// it's due, for the village's debug summary (see VillageDebug)
	/// </summary>
	/// <remarks>
	/// Only the server keeps the groups out of the world and knows where every viking on the road is, so a client asks it
	/// for a village's groups every AskInterval seconds while it describes the village, and counts down to when each is
	/// due in between. The groups the server has handed to this client to bring in are added here
	/// </remarks>
	internal static class VillageArrivals
	{
		/// <summary>
		/// A group on its way to a village - how many vikings, the names of those that have one, the quest it's out on or
		/// "" for a group on the road, what brings it, where it is, and when it's due, in ZNet time ticks, or long.MaxValue
		/// while it's looking for a village to go to
		/// </summary>
		internal class Arrival
		{
			public int Count;
			public List<string> Names = new List<string>();
			public string QuestId = "";
			public string What = "";
			public string Where = "";
			public long Due;

			/// <summary>
			/// Writes the group to a package
			/// </summary>
			/// <param name="pkg">The package</param>
			public void Write(ZPackage pkg)
			{
				pkg.Write(Count);
				pkg.Write(Names.Count);
				foreach (string name in Names)
				{
					pkg.Write(name);
				}
				pkg.Write(QuestId);
				pkg.Write(What);
				pkg.Write(Where);
				pkg.Write(Due);
			}

			/// <summary>
			/// Reads a group from a package
			/// </summary>
			/// <param name="pkg">The package</param>
			/// <returns>The group</returns>
			public static Arrival Read(ZPackage pkg)
			{
				Arrival arrival = new Arrival();
				arrival.Count = pkg.ReadInt();
				int names = pkg.ReadInt();
				for (int i = 0; i < names; i++)
				{
					arrival.Names.Add(pkg.ReadString());
				}
				arrival.QuestId = pkg.ReadString();
				arrival.What = pkg.ReadString();
				arrival.Where = pkg.ReadString();
				arrival.Due = pkg.ReadLong();
				return(arrival);
			}
		}

		/// <summary>
		/// The routed RPCs - a client asking the server for the groups on their way to the village at a spot, and the
		/// server's answer
		/// </summary>
		private const string RequestRpc = Plugin.PluginGuid + ".ArrivalsRequest";
		private const string AnswerRpc = Plugin.PluginGuid + ".ArrivalsAnswer";

		/// <summary>
		/// How often a client asks the server again for the groups on their way to a village it describes, in seconds
		/// </summary>
		private const float AskInterval = 3f;

		/// <summary>
		/// The server's last answer for a village, on a client - where the village stands, its groups, or null until the
		/// server has answered, and when to ask again, in Time.time seconds
		/// </summary>
		private class Answer
		{
			public Vector3 Farm;
			public List<Arrival> Arrivals;
			public float NextAsk;
		}

		/// <summary>
		/// The server's last answers, on a client
		/// </summary>
		private static readonly List<Answer> answers = new List<Answer>();

		/// <summary>
		/// Registers the RPCs with a world's routed RPC, forgetting the answers of the world being left
		/// </summary>
		public static void Register()
		{
			ZRoutedRpc.instance.Register<Vector3>(RequestRpc, OnRequest);
			ZRoutedRpc.instance.Register<ZPackage>(AnswerRpc, OnAnswer);
			answers.Clear();
		}

		/// <summary>
		/// Gets the groups on their way to a village - worked out here on the server, or the server's last answer on a
		/// client, which asks again once it's AskInterval old - with those handed to this client to bring in
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The groups, or null on a client until the server has answered</returns>
		public static List<Arrival> Of(Village village)
		{
			if (ZNet.instance == null || ZRoutedRpc.instance == null)
			{
				return(new List<Arrival>());
			}
			List<Arrival> arrivals = (ZNet.instance.IsServer() ? VillageTravel.ArrivalsAt(village.Farm) : Asked(village.Farm));
			if (arrivals != null)
			{
				arrivals.AddRange(VillageTravel.HeldAt(village.Farm));
			}
			return(arrivals);
		}

		/// <summary>
		/// Gets the server's last answer for a village on a client, asking again once it's AskInterval old
		/// </summary>
		/// <param name="farm">Where the village stands - its farm's planned spot or its anchor</param>
		/// <returns>A copy of the groups, or null until the server has answered</returns>
		private static List<Arrival> Asked(Vector3 farm)
		{
			Answer answer = Find(farm);
			if (answer == null)
			{
				answer = new Answer { Farm = farm };
				answers.Add(answer);
			}
			if (Time.time >= answer.NextAsk)
			{
				answer.NextAsk = Time.time + AskInterval;
				ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), RequestRpc, farm);
			}
			return((answer.Arrivals != null) ? new List<Arrival>(answer.Arrivals) : null);
		}

		/// <summary>
		/// Finds the server's last answer for a village
		/// </summary>
		/// <param name="farm">Where the village stands</param>
		/// <returns>The answer, or null when it was never asked</returns>
		private static Answer Find(Vector3 farm)
		{
			return(answers.Find((Answer answer) => Utils.DistanceXZ(answer.Farm, farm) <= TravelPlanner.FarmMatch));
		}

		/// <summary>
		/// Answers a client on the server with the groups on their way to a village
		/// </summary>
		/// <param name="sender">The client</param>
		/// <param name="farm">Where the village stands</param>
		private static void OnRequest(long sender, Vector3 farm)
		{
			if (ZNet.instance == null || !ZNet.instance.IsServer())
			{
				return;
			}
			List<Arrival> arrivals = VillageTravel.ArrivalsAt(farm);
			ZPackage pkg = new ZPackage();
			pkg.Write(farm);
			pkg.Write(arrivals.Count);
			foreach (Arrival arrival in arrivals)
			{
				arrival.Write(pkg);
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(sender, AnswerRpc, pkg);
		}

		/// <summary>
		/// Keeps the server's answer on a client
		/// </summary>
		/// <param name="sender">The server</param>
		/// <param name="pkg">The village's spot and its groups</param>
		private static void OnAnswer(long sender, ZPackage pkg)
		{
			Vector3 farm = pkg.ReadVector3();
			int count = pkg.ReadInt();
			List<Arrival> arrivals = new List<Arrival>();
			for (int i = 0; i < count; i++)
			{
				arrivals.Add(Arrival.Read(pkg));
			}
			Answer answer = Find(farm);
			if (answer == null)
			{
				answer = new Answer { Farm = farm, NextAsk = Time.time + AskInterval };
				answers.Add(answer);
			}
			answer.Arrivals = arrivals;
		}

		/// <summary>
		/// Describes a group the server keeps out of the world, or has handed to this client to bring in
		/// </summary>
		/// <param name="entry">The group</param>
		/// <param name="where">Where it is (see Where)</param>
		/// <returns>The group</returns>
		internal static Arrival FromEntry(TravelEntry entry, string where)
		{
			Arrival arrival = new Arrival { Count = entry.Vikings.Count, QuestId = entry.QuestId, What = What(entry), Where = where, Due = entry.Due };
			foreach (byte[] snapshot in entry.Vikings)
			{
				VillageDeparted.VikingValues values = VillageDeparted.Decode(snapshot);
				if (values == null)
				{
					continue;
				}
				VikingNames.ReadRaw(values, out string name, out string author);
				string shown = VikingNames.Filter(name, author).RemoveRichTextTags().Trim();
				if (shown.Length > 0)
				{
					arrival.Names.Add(shown);
				}
			}
			return(arrival);
		}

		/// <summary>
		/// Says what brings a group the server keeps out of the world - a quest, coming back to the village started again
		/// at the bonfire they left, or the road
		/// </summary>
		/// <param name="entry">The group</param>
		/// <returns>The phrase, like "out on MineCopper for CopperOre"</returns>
		internal static string What(TravelEntry entry)
		{
			if (entry.OnQuest)
			{
				return("out on " + (QuestKind)entry.QuestKind + ((entry.QuestItem.Length > 0) ? " for " + entry.QuestItem : ""));
			}
			if (entry.Returning)
			{
				return("coming back to the village started again at their bonfire");
			}
			return(OnTheRoad(entry.FromName));
		}

		/// <summary>
		/// Says what brings a group on the road - with the village it left, when a player disbanded it
		/// </summary>
		/// <param name="fromName">The name of the village it left, or ""</param>
		/// <returns>The phrase, like "on the road from Eikvik"</returns>
		internal static string OnTheRoad(string fromName)
		{
			return("on the road" + ((fromName.Length > 0) ? " from " + fromName : ""));
		}

		/// <summary>
		/// Says where a group the server keeps out of the world is now on its trip - and for a quest, whether it's on its
		/// way there, at work or on its way home
		/// </summary>
		/// <param name="entry">The group</param>
		/// <param name="now">The time now, in ZNet time ticks</param>
		/// <param name="ticksPerSecond">How many ticks a second has</param>
		/// <returns>The phrase, like "out of the world, at work at about (100, 30, -200)"</returns>
		internal static string Where(TravelEntry entry, long now, double ticksPerSecond)
		{
			if (entry.Due == long.MaxValue)
			{
				return("out of the world, looking for a village to go to");
			}
			if (entry.Journey == null || entry.Journey.Stages.Count == 0)
			{
				return("out of the world");
			}
			TravelJourney.Place place = entry.Journey.At(entry.Elapsed(now, ticksPerSecond), entry.From);
			if (place.Stage >= entry.Journey.Stages.Count)
			{
				return("out of the world, there and waiting to be brought in");
			}
			TravelJourney.Stage stage = entry.Journey.Stages[place.Stage];
			string way = (entry.OnQuest ? (stage.Homeward ? "on the way home " : "on the way there ") : "");
			string doing = ((stage.Road == null) ? "at work" : way + Leg(place.Leg));
			return("out of the world, " + doing + " at about " + place.Position.ToString("F0"));
		}

		/// <summary>
		/// Says how a leg of a road is crossed
		/// </summary>
		/// <param name="leg">The leg</param>
		/// <returns>The phrase, like "by sea"</returns>
		internal static string Leg(TravelLeg leg)
		{
			switch (leg)
			{
				case TravelLeg.Sea:
					return("by sea");
				case TravelLeg.Portal:
					return("through a portal");
			}
			return("on foot");
		}

		/// <summary>
		/// Writes the groups on their way to a village for its debug summary, a line each, soonest due first - leaving out
		/// the group out on the village's own quest, which its quest's lines cover (see VillageQuests.Describe), and
		/// writing nothing when there are no others
		/// </summary>
		/// <param name="arrivals">The groups (see Of), or null while the server hasn't answered</param>
		/// <param name="questId">The ID of the village's quest, or "" when it has none</param>
		/// <param name="now">The time now, in ZNet time ticks</param>
		/// <param name="text">The summary, written to</param>
		public static void Describe(List<Arrival> arrivals, string questId, long now, StringBuilder text)
		{
			if (arrivals == null)
			{
				text.Append("On the road here: asking the server\n");
				return;
			}
			List<Arrival> others = arrivals.FindAll((Arrival arrival) => arrival.QuestId.Length == 0 || arrival.QuestId != questId);
			if (others.Count == 0)
			{
				return;
			}
			others.Sort((Arrival a, Arrival b) => a.Due.CompareTo(b.Due));
			int vikings = 0;
			foreach (Arrival arrival in others)
			{
				vikings += arrival.Count;
			}
			text.Append("On the road here: " + Vikings(vikings) + ((others.Count > 1) ? " in " + others.Count + " groups" : "") + "\n");
			foreach (Arrival arrival in others)
			{
				text.Append("  " + Line(arrival, now) + "\n");
			}
		}

		/// <summary>
		/// Describes a group on its way to a village on a line of its own - how many, what brings them, their names, where
		/// they are and when they're due, counting down
		/// </summary>
		/// <param name="arrival">The group</param>
		/// <param name="now">The time now, in ZNet time ticks</param>
		/// <returns>The line</returns>
		internal static string Line(Arrival arrival, long now)
		{
			string names = ((arrival.Names.Count > 0) ? " (" + string.Join(", ", arrival.Names) + ")" : "");
			string where = ((arrival.Where.Length > 0) ? ", " + arrival.Where : "");
			string due = ((arrival.Due == long.MaxValue) ? "not due until it finds a village" : "due " + Countdown(arrival.Due, now));
			return(Vikings(arrival.Count) + " " + arrival.What + names + where + ", " + due);
		}

		/// <summary>
		/// Counts vikings
		/// </summary>
		/// <param name="count">How many</param>
		/// <returns>The phrase, like "1 viking" or "3 vikings"</returns>
		internal static string Vikings(int count)
		{
			return(count + ((count == 1) ? " viking" : " vikings"));
		}

		/// <summary>
		/// Says how long until something is due, or how long ago it was
		/// </summary>
		/// <param name="due">When it's due, in ZNet time ticks</param>
		/// <param name="now">The time now, in ZNet time ticks</param>
		/// <returns>The phrase, like "in 6m 32s", "now" or "12s ago"</returns>
		internal static string Countdown(long due, long now)
		{
			double seconds = (due - now) / (double)System.TimeSpan.TicksPerSecond;
			if (System.Math.Abs(seconds) < 0.5)
			{
				return("now");
			}
			return((seconds > 0.0) ? "in " + Span(seconds) : Span(-seconds) + " ago");
		}

		/// <summary>
		/// Writes a length of time in hours, minutes and seconds, leaving out the larger parts that are 0
		/// </summary>
		/// <param name="seconds">The time, in seconds</param>
		/// <returns>The phrase, like "45s", "6m 32s" or "1h 5m 12s"</returns>
		internal static string Span(double seconds)
		{
			long total = (long)System.Math.Round(System.Math.Max(0.0, seconds));
			long hours = total / 3600L;
			long minutes = (total / 60L) % 60L;
			long rest = total % 60L;
			if (hours > 0L)
			{
				return(hours + "h " + minutes + "m " + rest + "s");
			}
			return((minutes > 0L) ? minutes + "m " + rest + "s" : rest + "s");
		}
	}
}
