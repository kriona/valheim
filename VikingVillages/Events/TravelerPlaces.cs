using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Where a traveler comes from and goes to - two villages picked at random from every village in the world, those
	/// with only a name settled for them included, and ExtraCount far-off villages no village in the world goes by - and
	/// some of the vikings of each, by name, which the server finds
	/// </summary>
	/// <remarks>
	/// The far-off villages are the last of VillageNames.AutoNames shuffled by the world's seed, the same shuffle the
	/// farms' settled names take theirs from the start of, leaving out every name a village goes by or has settled for
	/// it - so they stay the same in a world, and one that comes to be a village's name is replaced by the next. A client
	/// only has the ZDOs of the places it's been near, so the server finds the vikings of the two villages, and sends
	/// their names back to the client that asked
	/// </remarks>
	internal static class TravelerPlaces
	{
		/// <summary>
		/// How many far-off villages there are, and the most vikings of a village a traveler knows by name
		/// </summary>
		internal const int ExtraCount = 10;
		internal const int FolkMax = 6;

		/// <summary>
		/// How far from a village's farm its vikings are looked for, in meters, and how far from the farm a viking's
		/// noted village can be and still be the one
		/// </summary>
		private const float FolkReach = 120f;
		private const float FarmMatch = 16f;

		/// <summary>
		/// The routed RPCs that ask the server for the vikings of a traveler's two villages, with the traveler and the two
		/// villages' names, and that send their names back, with the traveler and the names of each village's vikings,
		/// one a line
		/// </summary>
		private const string AskRpc = "kriona.VikingVillages.travelerFolkAsk";
		private const string AnswerRpc = "kriona.VikingVillages.travelerFolk";

		/// <summary>
		/// Registers the RPCs with a world's routed RPC
		/// </summary>
		public static void Register()
		{
			ZRoutedRpc.instance.Register<ZDOID, string, string>(AskRpc, OnAsk);
			ZRoutedRpc.instance.Register<ZDOID, string, string>(AnswerRpc, OnAnswer);
		}

		/// <summary>
		/// Lists the villages a traveler to a village can come from or go to - every other village in the world with a
		/// name, the names settled for farms not found yet, and the far-off villages
		/// </summary>
		/// <param name="host">The name of the village it visits, as typed, or ""</param>
		/// <returns>The names, none the same</returns>
		public static List<string> Candidates(string host)
		{
			HashSet<string> taken = new HashSet<string>();
			List<string> names = new List<string>();
			if (host.Length > 0)
			{
				taken.Add(host.ToLowerInvariant());
			}
			foreach (VillageNameList.Entry entry in VillageNameList.Others(host))
			{
				AddName(names, taken, VikingNames.Plain(entry.Name));
			}
			foreach (string name in VillagePlannedNames.Names())
			{
				AddName(names, taken, name);
			}
			int seed = ((WorldGenerator.instance != null) ? WorldGenerator.instance.GetSeed() : 0);
			List<string> shuffled = VillagePlannedNames.Shuffled(VillageNames.AutoNames, seed);
			foreach (string name in Extras(shuffled, taken, ExtraCount, VikingNames.IsPlace))
			{
				AddName(names, taken, name);
			}
			return(names);
		}

		/// <summary>
		/// Adds a name to a list when it isn't blank or taken, noting it as taken
		/// </summary>
		/// <param name="names">The list, added to</param>
		/// <param name="taken">The names taken, in lower case, added to</param>
		/// <param name="name">The name</param>
		private static void AddName(List<string> names, HashSet<string> taken, string name)
		{
			string trimmed = (name ?? "").Trim();
			if (trimmed.Length > 0 && taken.Add(trimmed.ToLowerInvariant()))
			{
				names.Add(trimmed);
			}
		}

		/// <summary>
		/// Picks the far-off villages - from the end of the shuffled names, leaving out the names taken and any to skip
		/// </summary>
		/// <param name="shuffled">The names, shuffled by the world's seed</param>
		/// <param name="taken">The names villages go by or have settled for them, in lower case</param>
		/// <param name="count">How many to pick</param>
		/// <param name="skip">Checks whether a name is left out besides, like the names of real places, or null</param>
		/// <returns>The names, last of the shuffled first</returns>
		internal static List<string> Extras(List<string> shuffled, HashSet<string> taken, int count, System.Predicate<string> skip)
		{
			List<string> extras = new List<string>();
			for (int i = shuffled.Count - 1; i >= 0 && extras.Count < count; i--)
			{
				string name = shuffled[i];
				if (!taken.Contains(name.ToLowerInvariant()) && (skip == null || !skip(name)))
				{
					extras.Add(name);
				}
			}
			return(extras);
		}

		/// <summary>
		/// Picks two different villages from a list, for where a traveler comes from and goes to
		/// </summary>
		/// <param name="names">The villages</param>
		/// <param name="fromRoll">A random number from 0 to 1 for where it comes from</param>
		/// <param name="toRoll">A random number from 0 to 1 for where it goes</param>
		/// <param name="from">Set to where it comes from, or ""</param>
		/// <param name="to">Set to where it goes, or ""</param>
		internal static void PickPair(List<string> names, float fromRoll, float toRoll, out string from, out string to)
		{
			from = "";
			to = "";
			if (names.Count == 0)
			{
				return;
			}
			int fromIndex = Mathf.Clamp(Mathf.FloorToInt(fromRoll * names.Count), 0, names.Count - 1);
			from = names[fromIndex];
			if (names.Count == 1)
			{
				return;
			}
			int toIndex = Mathf.Clamp(Mathf.FloorToInt(toRoll * (names.Count - 1)), 0, names.Count - 2);
			if (toIndex >= fromIndex)
			{
				toIndex++;
			}
			to = names[toIndex];
		}

		/// <summary>
		/// Asks the server for the vikings of a traveler's two villages, which it notes on the traveler once they come
		/// </summary>
		/// <param name="traveler">The traveler's ZDO, owned by this client</param>
		public static void AskFolk(ZDO traveler)
		{
			if (ZRoutedRpc.instance == null)
			{
				return;
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), AskRpc, traveler.m_uid, traveler.GetString(Traveler.FromKey), traveler.GetString(Traveler.ToKey));
		}

		/// <summary>
		/// Finds the vikings of a traveler's two villages on the server and sends their names to the client that asked
		/// </summary>
		/// <param name="sender">The client that asked</param>
		/// <param name="traveler">The traveler's ZDO</param>
		/// <param name="from">The village it came from</param>
		/// <param name="to">The village it goes to</param>
		private static void OnAsk(long sender, ZDOID traveler, string from, string to)
		{
			if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null)
			{
				return;
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(sender, AnswerRpc, traveler, string.Join("\n", FolkOf(from)), string.Join("\n", FolkOf(to)));
		}

		/// <summary>
		/// Notes the names of the vikings of a traveler's two villages on the traveler, on the client that owns it
		/// </summary>
		/// <param name="sender">The server</param>
		/// <param name="traveler">The traveler's ZDO</param>
		/// <param name="fromFolk">The names of the vikings of the village it came from, one a line</param>
		/// <param name="toFolk">The names of the vikings of the village it goes to, one a line</param>
		private static void OnAnswer(long sender, ZDOID traveler, string fromFolk, string toFolk)
		{
			ZDO zdo = ((ZDOMan.instance != null) ? ZDOMan.instance.GetZDO(traveler) : null);
			if (zdo == null || !zdo.IsOwner())
			{
				return;
			}
			zdo.Set(Traveler.FromFolkKey, fromFolk);
			zdo.Set(Traveler.ToFolkKey, toFolk);
		}

		/// <summary>
		/// Finds some of the vikings of a village by its name, on the server, which has every ZDO in the world
		/// </summary>
		/// <param name="name">The village's name</param>
		/// <returns>The names of up to FolkMax of its vikings at random, or none when there's no such village or it has
		/// none</returns>
		private static List<string> FolkOf(string name)
		{
			List<string> folk = new List<string>();
			if (string.IsNullOrEmpty(name) || !FindVillage(name, out Vector3 farm))
			{
				return(folk);
			}
			List<ZDO> zdos = new List<ZDO>();
			VillageRespawn.ZdosNear(farm, FolkReach, zdos);
			foreach (ZDO zdo in zdos)
			{
				if (!VikingCore.IsPlayerLikePrefab(zdo.GetPrefab()) || Traveler.Is(zdo) || Utils.DistanceXZ(zdo.GetVec3(Village.VillageKey, Vector3.zero), farm) > FarmMatch)
				{
					continue;
				}
				string viking = VikingNames.Get(zdo);
				if (viking.Length > 0 && !folk.Contains(viking))
				{
					folk.Add(viking);
				}
			}
			while (folk.Count > FolkMax)
			{
				folk.RemoveAt(Random.Range(0, folk.Count));
			}
			return(folk);
		}

		/// <summary>
		/// Finds where the farm of the village going by a name stands, by looking through every farm's generator and
		/// started village's anchor in the world
		/// </summary>
		/// <param name="name">The village's name</param>
		/// <param name="farm">Set to where its farm stands</param>
		/// <returns>False when no village goes by it</returns>
		private static bool FindVillage(string name, out Vector3 farm)
		{
			farm = Vector3.zero;
			foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
			{
				int prefab = zdo.GetPrefab();
				if (prefab != VillagePaths.GeneratorHash && !VillageFounding.IsAnchor(prefab))
				{
					continue;
				}
				if (VillageNames.At(zdo, out string named, out string _) && string.Equals(named.Trim(), name, System.StringComparison.OrdinalIgnoreCase))
				{
					farm = zdo.GetPosition();
					return(true);
				}
			}
			return(false);
		}
	}
}
