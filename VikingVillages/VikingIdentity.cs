using System;
using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// Who each living viking is, apart from its name - a GUID of its own, given by its owner the first time it loads and
	/// shown in debug mode - and when it last talked to each player it knows, so it only brings up news that's new to
	/// them (see VikingNewsStore)
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
		/// A player a viking talked to, and when
		/// </summary>
		internal struct Talk
		{
			public long Player;
			public long Time;
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
