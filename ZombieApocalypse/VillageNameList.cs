using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// The names players have given villages anywhere in the world, for a village's signs to point to (see
	/// VillageSigns.Name)
	/// </summary>
	/// <remarks>
	/// A client only has the ZDOs of the places it's been near, so the server looks through every farm's generator and
	/// started village's anchor a few sectors at a time, and sends the names to every client whenever they change. A
	/// client asks the server for them as it joins. A name given here is kept as taken until the names the server
	/// sends, or on the server the look through the world, hold it - or for NoteKeep seconds, should the village be
	/// renamed first - so a look that passed its sector before it was given doesn't drop it
	/// </remarks>
	internal static class VillageNameList
	{
		/// <summary>
		/// A village's name as it was typed, and who gave it, by platform user ID or "host"
		/// </summary>
		public struct Entry
		{
			public string Name;
			public string Author;
		}

		/// <summary>
		/// A name given on this client that the names from the server, or the server's look through the world, don't
		/// hold yet, and when it was given, in Time.time seconds
		/// </summary>
		public struct Pending
		{
			public Entry Entry;
			public float At;
		}

		/// <summary>
		/// The routed RPCs that send the names to clients, with a package of them, and that ask the server for them
		/// </summary>
		private const string ListRpc = "kriona.ZombieApocalypse.villageNameList";
		private const string AskRpc = "kriona.ZombieApocalypse.villageNameAsk";

		/// <summary>
		/// How many of the world's sectors the server looks through each update, and seconds between its looks through
		/// all of them
		/// </summary>
		private const int SectorsPerUpdate = 2000;
		private const float ScanInterval = 30f;

		/// <summary>
		/// Seconds between a client's asks for the names, until the server answers
		/// </summary>
		private const float AskInterval = 10f;

		/// <summary>
		/// The most seconds a name given on this client is kept as taken without the server's names holding it - long
		/// enough for a look through the world started after it was given to find it
		/// </summary>
		private const float NoteKeep = ScanInterval * 3f;

		/// <summary>
		/// The names as last looked through or sent with the ones given here since, the ones found so far in the look
		/// going on, the ones given here that the server's names don't hold yet, and the next sector to look at, or -1
		/// between looks
		/// </summary>
		private static readonly List<Entry> names = new List<Entry>();
		private static readonly List<Entry> found = new List<Entry>();
		private static readonly List<Pending> pending = new List<Pending>();
		private static int sector = -1;

		/// <summary>
		/// When the server next looks through the world, whether this client has had the names from the server, and when
		/// it next asks for them
		/// </summary>
		private static float nextScan;
		private static bool received;
		private static float nextAsk;

		/// <summary>
		/// Registers the RPCs with a world's routed RPC, starting the world with no names
		/// </summary>
		public static void Register()
		{
			ZRoutedRpc.instance.Register<ZPackage>(ListRpc, OnList);
			ZRoutedRpc.instance.Register(AskRpc, OnAsk);
			names.Clear();
			found.Clear();
			pending.Clear();
			sector = -1;
			nextScan = 0f;
			received = false;
			nextAsk = 0f;
		}

		/// <summary>
		/// Gets the names players have given villages in the world, but for one
		/// </summary>
		/// <param name="own">The name to leave out, as it was typed - the village's own - or ""</param>
		/// <returns>The names</returns>
		public static List<Entry> Others(string own)
		{
			return(Others(names, own));
		}

		/// <summary>
		/// Gets the names in a list but for one, matched without regard to case
		/// </summary>
		/// <param name="entries">The names</param>
		/// <param name="own">The name to leave out, or ""</param>
		/// <returns>The other names, in their order</returns>
		internal static List<Entry> Others(List<Entry> entries, string own)
		{
			return(entries.FindAll((Entry entry) => !string.Equals(entry.Name, own, System.StringComparison.OrdinalIgnoreCase)));
		}

		/// <summary>
		/// Checks whether a list holds a name, matched without regard to case
		/// </summary>
		/// <param name="entries">The names</param>
		/// <param name="name">The name</param>
		/// <returns>True when it does</returns>
		internal static bool Has(List<Entry> entries, string name)
		{
			return(entries.Exists((Entry entry) => string.Equals(entry.Name, name, System.StringComparison.OrdinalIgnoreCase)));
		}

		/// <summary>
		/// Adds a name to a list unless it already holds it, matched without regard to case
		/// </summary>
		/// <param name="entries">The names, added to</param>
		/// <param name="name">The name</param>
		/// <param name="author">Who gave it</param>
		/// <returns>True when it was added</returns>
		internal static bool Add(List<Entry> entries, string name, string author)
		{
			if (Has(entries, name))
			{
				return(false);
			}
			entries.Add(new Entry { Name = name, Author = author });
			return(true);
		}

		/// <summary>
		/// Adds a name this client just gave a village, so it counts as taken here before the server's names hold it
		/// </summary>
		/// <param name="name">The name, as it was typed</param>
		/// <param name="author">Who gave it, by platform user ID or "host", or "" for no player</param>
		public static void Note(string name, string author)
		{
			if (Add(names, name, author))
			{
				pending.Add(new Pending { Entry = new Entry { Name = name, Author = author }, At = Time.time });
			}
		}

		/// <summary>
		/// Drops the names given here that a list from the server or a look through the world holds, and the ones given
		/// keep seconds ago or more
		/// </summary>
		/// <param name="given">The names given here, dropped from</param>
		/// <param name="listed">The names the server holds</param>
		/// <param name="now">The time, in the seconds the names given here were given in</param>
		/// <param name="keep">The most seconds a name given here is kept</param>
		internal static void Confirm(List<Pending> given, List<Entry> listed, float now, float keep)
		{
			given.RemoveAll((Pending note) => Has(listed, note.Entry.Name) || now - note.At >= keep);
		}

		/// <summary>
		/// Puts the names given here that a list from the server or a look through the world doesn't hold after the
		/// ones it does
		/// </summary>
		/// <param name="listed">The names the server holds</param>
		/// <param name="given">The names given here</param>
		/// <returns>The names in the list in their order, then the others given here in theirs</returns>
		internal static List<Entry> Merge(List<Entry> listed, List<Pending> given)
		{
			List<Entry> merged = new List<Entry>(listed);
			foreach (Pending note in given)
			{
				Add(merged, note.Entry.Name, note.Entry.Author);
			}
			return(merged);
		}

		/// <summary>
		/// Sets the names to a list from the server or a look through the world, keeping the ones given here that it
		/// doesn't hold yet
		/// </summary>
		/// <param name="listed">The names the server holds</param>
		/// <returns>True when the names changed</returns>
		private static bool Take(List<Entry> listed)
		{
			Confirm(pending, listed, Time.time, NoteKeep);
			List<Entry> merged = Merge(listed, pending);
			if (Same(merged, names))
			{
				return(false);
			}
			names.Clear();
			names.AddRange(merged);
			return(true);
		}

		/// <summary>
		/// Looks through the world a few sectors at a time on the server, or asks the server for the names on a client
		/// that hasn't had them - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (ZNet.instance == null || ZDOMan.instance == null || ZRoutedRpc.instance == null || !ServerCheck.Active)
			{
				return;
			}
			if (!ZNet.instance.IsServer())
			{
				if (!received && Time.time >= nextAsk)
				{
					nextAsk = Time.time + AskInterval;
					ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), AskRpc);
				}
				return;
			}
			if (sector < 0)
			{
				if (Time.time < nextScan)
				{
					return;
				}
				sector = 0;
				found.Clear();
			}
			List<ZDO>[] sectors = ZDOMan.instance.m_objectsBySector;
			int end = Mathf.Min(sector + SectorsPerUpdate, sectors.Length);
			for (; sector < end; sector++)
			{
				if (sectors[sector] == null)
				{
					continue;
				}
				foreach (ZDO zdo in sectors[sector])
				{
					int prefab = zdo.GetPrefab();
					if ((prefab == VillagePaths.GeneratorHash || VillageFounding.IsAnchor(prefab)) && VillageNames.At(zdo, out string name, out string author))
					{
						Add(found, name, author);
					}
				}
			}
			if (sector < sectors.Length)
			{
				return;
			}
			sector = -1;
			nextScan = Time.time + ScanInterval;
			received = true;
			if (Take(found))
			{
				Send(ZRoutedRpc.Everybody);
			}
		}

		/// <summary>
		/// Whether this client knows the world's names yet - had them from the server, or on the server looked through
		/// the whole world once
		/// </summary>
		public static bool Ready
		{
			get
			{
				return(received);
			}
		}

		/// <summary>
		/// Checks whether two lists of names hold the same names in the same order, given by the same players
		/// </summary>
		/// <param name="a">One list</param>
		/// <param name="b">The other</param>
		/// <returns>True when they do</returns>
		internal static bool Same(List<Entry> a, List<Entry> b)
		{
			if (a.Count != b.Count)
			{
				return(false);
			}
			for (int i = 0; i < a.Count; i++)
			{
				if (a[i].Name != b[i].Name || a[i].Author != b[i].Author)
				{
					return(false);
				}
			}
			return(true);
		}

		/// <summary>
		/// Sends the names, from the server
		/// </summary>
		/// <param name="target">The peer to send them to, or ZRoutedRpc.Everybody</param>
		private static void Send(long target)
		{
			ZPackage pkg = new ZPackage();
			Write(pkg, names);
			ZRoutedRpc.instance.InvokeRoutedRPC(target, ListRpc, pkg);
		}

		/// <summary>
		/// Writes names into a package - their count, then each one's name and author
		/// </summary>
		/// <param name="pkg">The package, written to</param>
		/// <param name="entries">The names</param>
		internal static void Write(ZPackage pkg, List<Entry> entries)
		{
			pkg.Write(entries.Count);
			foreach (Entry entry in entries)
			{
				pkg.Write(entry.Name);
				pkg.Write(entry.Author);
			}
		}

		/// <summary>
		/// Reads names written by Write from a package
		/// </summary>
		/// <param name="pkg">The package</param>
		/// <returns>The names, in the order they were written</returns>
		internal static List<Entry> Read(ZPackage pkg)
		{
			List<Entry> entries = new List<Entry>();
			int count = pkg.ReadInt();
			for (int i = 0; i < count; i++)
			{
				entries.Add(new Entry { Name = pkg.ReadString(), Author = pkg.ReadString() });
			}
			return(entries);
		}

		/// <summary>
		/// Answers a client's ask for the names, on the server
		/// </summary>
		/// <param name="sender">The client that asked</param>
		private static void OnAsk(long sender)
		{
			if (ZNet.instance != null && ZNet.instance.IsServer())
			{
				Send(sender);
			}
		}

		/// <summary>
		/// Takes the names the server sent, on a client, keeping the ones given here that they don't hold yet
		/// </summary>
		/// <param name="sender">The server</param>
		/// <param name="pkg">The names</param>
		private static void OnList(long sender, ZPackage pkg)
		{
			if (ZNet.instance == null || ZNet.instance.IsServer())
			{
				return;
			}
			Take(Read(pkg));
			received = true;
		}
	}
}
