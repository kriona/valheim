using System;
using System.Collections.Generic;
using Kriona.Vikings;

namespace VikingVillages
{
	/// <summary>
	/// Who each living viking is, apart from its name - a GUID of its own, given by its owner the first time it loads and
	/// shown in debug mode - when it last talked to each player it knows, so it only brings up news that's new to them
	/// (see VikingNewsStore), and when it met each of them and how many times they've talked to it, so it knows its old
	/// friends
	/// </summary>
	/// <remarks>
	/// A ZDO's own ID changes as the world loads, so it can't tell one viking from another across sessions. A viking
	/// that comes back after dying is a new viking, with a GUID of its own
	/// </remarks>
	internal static class VikingIdentity
	{
		/// <summary>
		/// On a living viking, its GUID
		/// </summary>
		private static readonly int IdKey = "kriona.VikingVillages.vikingId".GetStableHashCode();

		/// <summary>
		/// On a living viking, when it last talked to each player it knows, by player ID, in ZNet time ticks - the one it
		/// talked to longest ago first - and the most it remembers
		/// </summary>
		private static readonly int TalkedKey = "kriona.VikingVillages.vikingTalked".GetStableHashCode();
		private const int TalkedMax = 64;

		/// <summary>
		/// On a living viking, when it met each player it knows and how many times they've talked to it, by player ID -
		/// the one it heard from longest ago first - and the most it remembers
		/// </summary>
		private static readonly int MetKey = "kriona.VikingVillages.vikingMet".GetStableHashCode();
		private const int MetMax = 64;

		/// <summary>
		/// The least time between two talks that each count towards how many times a player has talked to a viking, in
		/// seconds, so a few words said over and over count once
		/// </summary>
		private const float TalkCountGap = 60f;

		/// <summary>
		/// How long a viking has to have known a player, in game days, or how many more times than this they have to have
		/// talked to it, for it to call them an old friend
		/// </summary>
		private const int FriendDays = 30;
		internal const int FriendTalks = 10;

		/// <summary>
		/// A player a viking talked to, and when
		/// </summary>
		internal struct Talk
		{
			public long Player;
			public long Time;
		}

		/// <summary>
		/// A player a viking knows - when it met them, in ZNet time ticks, or 0 when it doesn't know, how many times they've
		/// talked to it, and when the last of those talks was
		/// </summary>
		internal struct Acquaintance
		{
			public long Player;
			public long Met;
			public int Talks;
			public long Counted;
		}

		/// <summary>
		/// Gets a viking's GUID
		/// </summary>
		/// <param name="zdo">The viking's ZDO</param>
		/// <returns>The GUID, or "" before its owner has given it one</returns>
		public static string Get(ZDO zdo)
		{
			return((zdo != null) ? zdo.GetString(IdKey, "") : "");
		}

		/// <summary>
		/// Gets the GUID of a viking kept out of the world (see VillageDeparted.SnapshotOf)
		/// </summary>
		/// <param name="snapshot">Its values</param>
		/// <returns>The GUID, or "" when it has none or they can't be read</returns>
		public static string OfSnapshot(byte[] snapshot)
		{
			VillageDeparted.VikingValues values = VillageDeparted.Decode(snapshot);
			int at = ((values != null) ? values.Strings.FindIndex((KeyValuePair<int, string> entry) => entry.Key == IdKey) : -1);
			return((at >= 0) ? values.Strings[at].Value : "");
		}

		/// <summary>
		/// Gives a viking a GUID when it has none, on its owner
		/// </summary>
		/// <param name="view">The viking's view</param>
		/// <returns>Its GUID, or "" when this client can't give it one</returns>
		public static string Ensure(ZNetView view)
		{
			if (view == null || !view.IsValid())
			{
				return("");
			}
			ZDO zdo = view.GetZDO();
			string id = Get(zdo);
			if (id.Length > 0 || !view.IsOwner())
			{
				return(id);
			}
			id = Guid.NewGuid().ToString("N");
			zdo.Set(IdKey, id);
			return(id);
		}

		/// <summary>
		/// Finds when a viking last talked to a player
		/// </summary>
		/// <param name="zdo">The viking's ZDO</param>
		/// <param name="player">The player's ID</param>
		/// <returns>When, in ZNet time ticks, or 0 when it never has</returns>
		public static long LastTalk(ZDO zdo, long player)
		{
			foreach (Talk talk in Decode(zdo.GetByteArray(TalkedKey)))
			{
				if (talk.Player == player)
				{
					return(talk.Time);
				}
			}
			return(0L);
		}

		/// <summary>
		/// Notes that a viking talked to a player, on its owner
		/// </summary>
		/// <param name="zdo">The viking's ZDO, owned by this client</param>
		/// <param name="player">The player's ID</param>
		/// <param name="time">When, in ZNet time ticks</param>
		public static void Talked(ZDO zdo, long player, long time)
		{
			zdo.Set(TalkedKey, Encode(Note(Decode(zdo.GetByteArray(TalkedKey)), player, time)));
		}

		/// <summary>
		/// Notes when a viking met a player, on its owner, unless it already knows when
		/// </summary>
		/// <param name="zdo">The viking's ZDO, owned by this client</param>
		/// <param name="player">The player's ID</param>
		/// <param name="time">When, in ZNet time ticks</param>
		public static void Met(ZDO zdo, long player, long time)
		{
			List<Acquaintance> known = DecodeMet(zdo.GetByteArray(MetKey));
			if (known.Exists((Acquaintance acquaintance) => acquaintance.Player == player))
			{
				return;
			}
			zdo.Set(MetKey, EncodeMet(NoteMet(known, player, time)));
		}

		/// <summary>
		/// Notes that a viking said its piece to a player, on its owner, once the line is out - that it talked to them now,
		/// so it only brings up news from after this, and when the player talked to it, that they did, for how many times
		/// they've talked
		/// </summary>
		/// <param name="view">The viking's view</param>
		/// <param name="player">The player's ID</param>
		/// <param name="talked">Whether the player talked to it, rather than it greeting them</param>
		public static void Answered(ZNetView view, long player, bool talked)
		{
			if (view == null || !view.IsValid() || !view.IsOwner())
			{
				return;
			}
			ZDO zdo = view.GetZDO();
			long now = NetTime.Now();
			if (talked)
			{
				List<Acquaintance> known = DecodeMet(zdo.GetByteArray(MetKey));
				zdo.Set(MetKey, EncodeMet(NoteTalk(known, player, now, LastTalk(zdo, player), NetTime.Ticks(TalkCountGap))));
			}
			Talked(zdo, player, now);
		}

		/// <summary>
		/// Checks whether a viking and a player are old friends - it has known them for FriendDays game days, or they've
		/// talked to it more than FriendTalks times
		/// </summary>
		/// <param name="view">The viking's view</param>
		/// <param name="player">The player's ID</param>
		/// <returns>True when they are</returns>
		public static bool OldFriends(ZNetView view, long player)
		{
			if (view == null || !view.IsValid() || EnvMan.instance == null)
			{
				return(false);
			}
			Acquaintance acquaintance = Find(DecodeMet(view.GetZDO().GetByteArray(MetKey)), player);
			return(OldFriends(acquaintance, NetTime.Now(), NetTime.Ticks(EnvMan.instance.m_dayLengthSec * FriendDays)));
		}

		/// <summary>
		/// Checks whether a viking and a player it knows are old friends
		/// </summary>
		/// <param name="acquaintance">What it remembers of the player</param>
		/// <param name="now">The time now, in ZNet time ticks</param>
		/// <param name="friendTime">How long it has to have known them, in ZNet time ticks</param>
		/// <returns>True when it has known them that long, or they've talked to it more than FriendTalks times</returns>
		internal static bool OldFriends(Acquaintance acquaintance, long now, long friendTime)
		{
			return((acquaintance.Met > 0L && now - acquaintance.Met >= friendTime) || acquaintance.Talks > FriendTalks);
		}

		/// <summary>
		/// Finds what a viking remembers of a player
		/// </summary>
		/// <param name="known">The players it knows</param>
		/// <param name="player">The player's ID</param>
		/// <returns>What it remembers, with Met 0 when it doesn't know when it met them</returns>
		internal static Acquaintance Find(List<Acquaintance> known, long player)
		{
			foreach (Acquaintance acquaintance in known)
			{
				if (acquaintance.Player == player)
				{
					return(acquaintance);
				}
			}
			Acquaintance unknown = new Acquaintance();
			unknown.Player = player;
			return(unknown);
		}

		/// <summary>
		/// Notes when a viking met a player at the end of the players it knows, unless it already knows when, and forgets
		/// the one it heard from longest ago once it remembers more than MetMax
		/// </summary>
		/// <param name="known">The players it knows, the one it heard from longest ago first</param>
		/// <param name="player">The player's ID</param>
		/// <param name="time">When it met them, in ZNet time ticks</param>
		/// <returns>The players it knows</returns>
		internal static List<Acquaintance> NoteMet(List<Acquaintance> known, long player, long time)
		{
			if (known.Exists((Acquaintance acquaintance) => acquaintance.Player == player))
			{
				return(known);
			}
			Acquaintance met = new Acquaintance();
			met.Player = player;
			met.Met = time;
			return(Keep(known, met));
		}

		/// <summary>
		/// Notes that a player talked to a viking, moving them to the end of the players it knows - counting the talk
		/// unless the last one counted was less than countGap ago - and forgets the one it heard from longest ago once it
		/// remembers more than MetMax. A player it has no meeting for, known from before it noted them, is taken to have
		/// met it when they last talked, or now
		/// </summary>
		/// <param name="known">The players it knows, the one it heard from longest ago first</param>
		/// <param name="player">The player's ID</param>
		/// <param name="time">When they talked, in ZNet time ticks</param>
		/// <param name="lastTalk">When it last talked to them before this, in ZNet time ticks, or 0 when it never has</param>
		/// <param name="countGap">The least time between two counted talks, in ZNet time ticks</param>
		/// <returns>The players it knows</returns>
		internal static List<Acquaintance> NoteTalk(List<Acquaintance> known, long player, long time, long lastTalk, long countGap)
		{
			Acquaintance acquaintance = Find(known, player);
			known.RemoveAll((Acquaintance other) => other.Player == player);
			if (acquaintance.Met == 0L)
			{
				acquaintance.Met = ((lastTalk > 0L) ? lastTalk : time);
			}
			if (acquaintance.Talks == 0 || time - acquaintance.Counted >= countGap)
			{
				acquaintance.Talks++;
				acquaintance.Counted = time;
			}
			return(Keep(known, acquaintance));
		}

		/// <summary>
		/// Adds a player at the end of the players a viking knows, forgetting the one it heard from longest ago once it
		/// remembers more than MetMax
		/// </summary>
		/// <param name="known">The players it knows, without this one</param>
		/// <param name="acquaintance">What it remembers of the player</param>
		/// <returns>The players it knows</returns>
		private static List<Acquaintance> Keep(List<Acquaintance> known, Acquaintance acquaintance)
		{
			known.Add(acquaintance);
			while (known.Count > MetMax)
			{
				known.RemoveAt(0);
			}
			return(known);
		}

		/// <summary>
		/// Writes the players a viking knows
		/// </summary>
		/// <param name="known">The players</param>
		/// <returns>The bytes</returns>
		internal static byte[] EncodeMet(List<Acquaintance> known)
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(known.Count);
			foreach (Acquaintance acquaintance in known)
			{
				pkg.Write(acquaintance.Player);
				pkg.Write(acquaintance.Met);
				pkg.Write(acquaintance.Talks);
				pkg.Write(acquaintance.Counted);
			}
			return(pkg.GetArray());
		}

		/// <summary>
		/// Reads the players a viking knows
		/// </summary>
		/// <param name="data">The bytes, or null</param>
		/// <returns>The players, the one it heard from longest ago first</returns>
		internal static List<Acquaintance> DecodeMet(byte[] data)
		{
			List<Acquaintance> known = new List<Acquaintance>();
			if (data == null || data.Length == 0)
			{
				return(known);
			}
			ZPackage pkg = new ZPackage(data);
			int count = pkg.ReadInt();
			for (int i = 0; i < count; i++)
			{
				Acquaintance acquaintance = new Acquaintance();
				acquaintance.Player = pkg.ReadLong();
				acquaintance.Met = pkg.ReadLong();
				acquaintance.Talks = pkg.ReadInt();
				acquaintance.Counted = pkg.ReadLong();
				known.Add(acquaintance);
			}
			return(known);
		}

		/// <summary>
		/// Notes a talk in a viking's talks, moving the player to the end, and forgets the one it talked to longest ago
		/// once it remembers more than TalkedMax
		/// </summary>
		/// <param name="talks">The talks, the one longest ago first</param>
		/// <param name="player">The player's ID</param>
		/// <param name="time">When, in ZNet time ticks</param>
		/// <returns>The talks</returns>
		internal static List<Talk> Note(List<Talk> talks, long player, long time)
		{
			talks.RemoveAll((Talk talk) => talk.Player == player);
			Talk noted = new Talk();
			noted.Player = player;
			noted.Time = time;
			talks.Add(noted);
			while (talks.Count > TalkedMax)
			{
				talks.RemoveAt(0);
			}
			return(talks);
		}

		/// <summary>
		/// Writes a viking's talks
		/// </summary>
		/// <param name="talks">The talks</param>
		/// <returns>The bytes</returns>
		internal static byte[] Encode(List<Talk> talks)
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(talks.Count);
			foreach (Talk talk in talks)
			{
				pkg.Write(talk.Player);
				pkg.Write(talk.Time);
			}
			return(pkg.GetArray());
		}

		/// <summary>
		/// Reads a viking's talks
		/// </summary>
		/// <param name="data">The bytes, or null</param>
		/// <returns>The talks, the one longest ago first</returns>
		internal static List<Talk> Decode(byte[] data)
		{
			List<Talk> talks = new List<Talk>();
			if (data == null || data.Length == 0)
			{
				return(talks);
			}
			ZPackage pkg = new ZPackage(data);
			int count = pkg.ReadInt();
			for (int i = 0; i < count; i++)
			{
				Talk talk = new Talk();
				talk.Player = pkg.ReadLong();
				talk.Time = pkg.ReadLong();
				talks.Add(talk);
			}
			return(talks);
		}
	}
}
