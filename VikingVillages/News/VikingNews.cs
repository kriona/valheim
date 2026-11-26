using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// The news living vikings pass on to players - what each player did or what happened to them, from killing a boss
	/// to dying - and the firsts each character has had in the world, so a first is only news once
	/// </summary>
	/// <remarks>
	/// Only the server changes the news, which it keeps and sends to every client (see VikingNewsStore). Each event
	/// lists the players it's news to, how many more vikings will tell it, which vikings have and which of the lines
	/// they said, so no two say the same line
	/// </remarks>
	internal class VikingNews
	{
		/// <summary>
		/// The kinds of event - a boss, a miniboss, a trader met, a dungeon or biome found, an animal tamed, a skill or
		/// every skill mastered, an achievement, a leviathan sailed by, a monster killed with a tree, a sea serpent
		/// killed, a death and sailing off the edge of the world
		/// </summary>
		public const string Boss = "boss";
		public const string Miniboss = "miniboss";
		public const string Trader = "trader";
		public const string Dungeon = "dungeon";
		public const string Biome = "biome";
		public const string Tame = "tame";
		public const string Skill = "skill";
		public const string AllSkills = "allskills";
		public const string Achievement = "achievement";
		public const string Leviathan = "leviathan";
		public const string Tree = "tree";
		public const string Serpent = "serpent";
		public const string Death = "death";
		public const string Edge = "edge";

		/// <summary>
		/// The most events kept at once - the oldest go first
		/// </summary>
		public const int MaxEvents = 64;

		/// <summary>
		/// The version of how the news is written, which a reader skips news written another way by
		/// </summary>
		private const int FormatVersion = 1;

		/// <summary>
		/// Something that happened, which vikings tell the players it happened to
		/// </summary>
		internal class Event
		{
			/// <summary>
			/// The event's number, unique in its world
			/// </summary>
			public int Id;

			/// <summary>
			/// What kind of event it is (see the kinds above), what it was about - a boss's prefab, a biome, a skill, a
			/// cause of death - and what a line names in it, like the creature that killed a player
			/// </summary>
			public string Kind = "";
			public string Subject = "";
			public string Detail = "";

			/// <summary>
			/// When it happened, in ZNet time ticks
			/// </summary>
			public long Time;

			/// <summary>
			/// How many more vikings will tell it
			/// </summary>
			public int Remaining;

			/// <summary>
			/// The players it's news to, by player ID
			/// </summary>
			public List<long> Players = new List<long>();

			/// <summary>
			/// The vikings who told it, by their IDs (see VikingIdentity), and the lines they said, by each line's stable
			/// hash
			/// </summary>
			public List<string> Mentioned = new List<string>();
			public List<int> UsedLines = new List<int>();
		}

		/// <summary>
		/// The events, oldest first
		/// </summary>
		public readonly List<Event> Events = new List<Event>();

		/// <summary>
		/// The firsts each character has had in the world (see FirstKey)
		/// </summary>
		public readonly HashSet<string> Firsts = new HashSet<string>();

		/// <summary>
		/// The number the next event gets
		/// </summary>
		public int NextId = 1;

		/// <summary>
		/// Checks whether an event of a kind can happen to a player again and again - a death, sailing off the edge,
		/// sailing by a leviathan, killing a sea serpent or a monster with a tree - so a new one takes the place of the
		/// player's last rather than adding to it
		/// </summary>
		/// <param name="kind">The kind of event</param>
		/// <returns>True when it can</returns>
		public static bool Repeats(string kind)
		{
			return(kind == Death || kind == Edge || kind == Leviathan || kind == Serpent || kind == Tree);
		}

		/// <summary>
		/// Makes the key a character's first of something is noted by
		/// </summary>
		/// <param name="player">The player's ID</param>
		/// <param name="kind">The kind of event</param>
		/// <param name="subject">What it was about</param>
		/// <returns>The key</returns>
		public static string FirstKey(long player, string kind, string subject)
		{
			return(player.ToString() + "/" + kind + "/" + subject);
		}

		/// <summary>
		/// Adds an event that just happened to some players. A first only goes to the players who haven't had it
		/// before, noting it for them. A boss killed again takes the place of the last news of it, keeping every player
		/// in either, and any other event takes each of its players out of the news of the same kind and subject - of
		/// the same kind for one that happens again and again (see Repeats) - dropping news nobody is left in. The
		/// oldest events go once there are more than MaxEvents
		/// </summary>
		/// <param name="kind">The kind of event</param>
		/// <param name="subject">What it was about</param>
		/// <param name="detail">What a line names in it, or ""</param>
		/// <param name="players">The players it happened to, by player ID</param>
		/// <param name="firstOnly">True for a first, which is only news to those who haven't had it</param>
		/// <param name="time">When it happened, in ZNet time ticks</param>
		/// <param name="remaining">How many vikings will tell it</param>
		/// <returns>The event added, or null when it's news to nobody</returns>
		public Event Add(string kind, string subject, string detail, List<long> players, bool firstOnly, long time, int remaining)
		{
			List<long> involved = new List<long>();
			foreach (long player in players)
			{
				if (player == 0L || involved.Contains(player))
				{
					continue;
				}
				if (firstOnly && !Firsts.Add(FirstKey(player, kind, subject)))
				{
					continue;
				}
				involved.Add(player);
			}
			if (involved.Count == 0)
			{
				return(null);
			}
			for (int i = Events.Count - 1; i >= 0; i--)
			{
				Event old = Events[i];
				if (old.Kind != kind)
				{
					continue;
				}
				if (kind == Boss)
				{
					if (old.Subject == subject)
					{
						foreach (long player in old.Players)
						{
							if (!involved.Contains(player))
							{
								involved.Add(player);
							}
						}
						Events.RemoveAt(i);
					}
					continue;
				}
				if (!Repeats(kind) && old.Subject != subject)
				{
					continue;
				}
				old.Players.RemoveAll(involved.Contains);
				if (old.Players.Count == 0)
				{
					Events.RemoveAt(i);
				}
			}
			Event added = new Event();
			added.Id = NextId++;
			added.Kind = kind;
			added.Subject = (subject ?? "");
			added.Detail = (detail ?? "");
			added.Time = time;
			added.Remaining = remaining;
			added.Players = involved;
			Events.Add(added);
			while (Events.Count > MaxEvents)
			{
				Events.RemoveAt(0);
			}
			return(added);
		}

		/// <summary>
		/// Finds an event by its number
		/// </summary>
		/// <param name="id">The event's number</param>
		/// <returns>The event, or null when it's gone</returns>
		public Event Find(int id)
		{
			foreach (Event news in Events)
			{
				if (news.Id == id)
				{
					return(news);
				}
			}
			return(null);
		}

		/// <summary>
		/// Notes that a viking told an event with a line - one viking fewer left to tell it, dropping it once none are
		/// </summary>
		/// <param name="id">The event's number</param>
		/// <param name="viking">The viking's ID</param>
		/// <param name="line">The line's stable hash</param>
		/// <returns>True when it was noted, false when the event is gone or the viking already told it</returns>
		public bool Mention(int id, string viking, int line)
		{
			Event news = Find(id);
			if (news == null || news.Mentioned.Contains(viking))
			{
				return(false);
			}
			news.Mentioned.Add(viking);
			news.UsedLines.Add(line);
			news.Remaining--;
			if (news.Remaining <= 0)
			{
				Events.Remove(news);
			}
			return(true);
		}

		/// <summary>
		/// Drops the events older than a given age
		/// </summary>
		/// <param name="now">The time now, in ZNet time ticks</param>
		/// <param name="maxAge">The most ticks an event is kept for</param>
		/// <returns>How many were dropped</returns>
		public int Expire(long now, long maxAge)
		{
			return(Events.RemoveAll((Event news) => news.Time < now - maxAge));
		}

		/// <summary>
		/// Finds the news a viking has for a player - the latest event the player was in, since the viking last talked
		/// to them and no older than maxAge, that the viking hasn't told and more vikings are still to
		/// </summary>
		/// <param name="player">The player's ID</param>
		/// <param name="viking">The viking's ID</param>
		/// <param name="since">When the viking last talked to the player, in ZNet time ticks</param>
		/// <param name="now">The time now, in ZNet time ticks</param>
		/// <param name="maxAge">The most ticks an event is told for</param>
		/// <returns>The event, or null when there's none</returns>
		public Event NewsFor(long player, string viking, long since, long now, long maxAge)
		{
			Event best = null;
			foreach (Event news in Events)
			{
				if (news.Time <= since || news.Time < now - maxAge || news.Remaining <= 0 || !news.Players.Contains(player) || news.Mentioned.Contains(viking))
				{
					continue;
				}
				if (best == null || news.Time > best.Time || (news.Time == best.Time && news.Id > best.Id))
				{
					best = news;
				}
			}
			return(best);
		}

		/// <summary>
		/// Writes the news - with the firsts for keeping it, and without them for sending it to clients, which only
		/// tell it
		/// </summary>
		/// <param name="firsts">True to write the firsts too</param>
		/// <returns>The bytes</returns>
		public byte[] Encode(bool firsts)
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(FormatVersion);
			pkg.Write(NextId);
			pkg.Write(Events.Count);
			foreach (Event news in Events)
			{
				pkg.Write(news.Id);
				pkg.Write(news.Kind);
				pkg.Write(news.Subject);
				pkg.Write(news.Detail);
				pkg.Write(news.Time);
				pkg.Write(news.Remaining);
				pkg.Write(news.Players.Count);
				foreach (long player in news.Players)
				{
					pkg.Write(player);
				}
				pkg.Write(news.Mentioned.Count);
				foreach (string viking in news.Mentioned)
				{
					pkg.Write(viking);
				}
				pkg.Write(news.UsedLines.Count);
				foreach (int line in news.UsedLines)
				{
					pkg.Write(line);
				}
			}
			if (!firsts)
			{
				pkg.Write(0);
				return(pkg.GetArray());
			}
			pkg.Write(Firsts.Count);
			foreach (string first in Firsts)
			{
				pkg.Write(first);
			}
			return(pkg.GetArray());
		}

		/// <summary>
		/// Reads news written by Encode
		/// </summary>
		/// <param name="data">The bytes, or null</param>
		/// <returns>The news - empty when there's nothing to read, or it was written another way</returns>
		public static VikingNews Decode(byte[] data)
		{
			VikingNews read = new VikingNews();
			if (data == null || data.Length == 0)
			{
				return(read);
			}
			ZPackage pkg = new ZPackage(data);
			if (pkg.ReadInt() != FormatVersion)
			{
				return(read);
			}
			read.NextId = pkg.ReadInt();
			int count = pkg.ReadInt();
			for (int i = 0; i < count; i++)
			{
				Event news = new Event();
				news.Id = pkg.ReadInt();
				news.Kind = pkg.ReadString();
				news.Subject = pkg.ReadString();
				news.Detail = pkg.ReadString();
				news.Time = pkg.ReadLong();
				news.Remaining = pkg.ReadInt();
				int players = pkg.ReadInt();
				for (int j = 0; j < players; j++)
				{
					news.Players.Add(pkg.ReadLong());
				}
				int mentioned = pkg.ReadInt();
				for (int j = 0; j < mentioned; j++)
				{
					news.Mentioned.Add(pkg.ReadString());
				}
				int lines = pkg.ReadInt();
				for (int j = 0; j < lines; j++)
				{
					news.UsedLines.Add(pkg.ReadInt());
				}
				read.Events.Add(news);
			}
			int firsts = pkg.ReadInt();
			for (int i = 0; i < firsts; i++)
			{
				read.Firsts.Add(pkg.ReadString());
			}
			return(read);
		}
	}
}
