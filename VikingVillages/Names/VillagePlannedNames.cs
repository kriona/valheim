using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The names settled for the world's farms in advance, before they're found - each farm the world was planned with
	/// that has no name of its own yet gets one of VillageNames.AutoNames, none of them the same, which its village
	/// takes when it's set up (see VillageNames.TryAutoName)
	/// </summary>
	/// <remarks>
	/// Only the server has the world's plan of its locations, so it works the names out - the farms in order of where
	/// they stand, and the names shuffled by the world's seed, so they come out the same each time - leaving out the
	/// names villages already go by. It does so again every ReplanInterval seconds, as villages are named, and sends the
	/// names to every client whenever they change. A client asks the server for them as it joins
	/// </remarks>
	internal static class VillagePlannedNames
	{
		/// <summary>
		/// The routed RPCs that send the planned names to clients, with a package of them, and that ask the server for
		/// them
		/// </summary>
		private const string PlanRpc = "kriona.VikingVillages.villagePlan";
		private const string AskRpc = "kriona.VikingVillages.villagePlanAsk";

		/// <summary>
		/// Seconds between the server's working out of the names, and between a client's asks for them until the server
		/// answers
		/// </summary>
		private const float ReplanInterval = 30f;
		private const float AskInterval = 10f;

		/// <summary>
		/// How far from a farm's planned spot its village's generator can stand and still be its, in meters
		/// </summary>
		private const float FarmMatch = 16f;

		/// <summary>
		/// The farms' planned spots and the names settled for them, whether they're known yet - worked out on the server,
		/// or had from it on a client - and when the server next works them out or a client next asks for them
		/// </summary>
		private static readonly List<KeyValuePair<Vector3, string>> plan = new List<KeyValuePair<Vector3, string>>();
		private static bool ready;
		private static float nextPlan;
		private static float nextAsk;

		/// <summary>
		/// Whether the names are known on this client yet, so a village waits to be named until they are
		/// </summary>
		public static bool Ready
		{
			get
			{
				return(ready);
			}
		}

		/// <summary>
		/// Registers the RPCs with a world's routed RPC, starting the world with no names
		/// </summary>
		public static void Register()
		{
			ZRoutedRpc.instance.Register<ZPackage>(PlanRpc, OnPlan);
			ZRoutedRpc.instance.Register(AskRpc, OnAsk);
			plan.Clear();
			ready = false;
			nextPlan = 0f;
			nextAsk = 0f;
		}

		/// <summary>
		/// Works the names out every ReplanInterval seconds on the server, sending them on when they change, or asks the
		/// server for them on a client that hasn't had them - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (ZNet.instance == null || ZoneSystem.instance == null || ZRoutedRpc.instance == null || !ServerCheck.Active)
			{
				return;
			}
			if (!ZNet.instance.IsServer())
			{
				if (!ready && Time.time >= nextAsk)
				{
					nextAsk = Time.time + AskInterval;
					ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), AskRpc);
				}
				return;
			}
			// The names villages already go by are left out, so they're looked through first
			if (Time.time < nextPlan || WorldGenerator.instance == null || !VillageNameList.Ready)
			{
				return;
			}
			nextPlan = Time.time + ReplanInterval;
			List<KeyValuePair<Vector3, string>> planned = Plan();
			bool changed = (!ready || !Same(planned, plan));
			plan.Clear();
			plan.AddRange(planned);
			ready = true;
			if (changed)
			{
				Plugin.Log.LogInfo("Settled the names of " + plan.Count + " of the world's " + FarmSpawns.PlannedFarms().Count + " farms in advance");
				Send(ZRoutedRpc.Everybody);
			}
		}

		/// <summary>
		/// Gets the name settled for the farm a village stands at
		/// </summary>
		/// <param name="farm">Where the village's farm stands</param>
		/// <returns>The name, or null when it has none settled</returns>
		public static string For(Vector3 farm)
		{
			foreach (KeyValuePair<Vector3, string> entry in plan)
			{
				if (Utils.DistanceXZ(entry.Key, farm) <= FarmMatch)
				{
					return(entry.Value);
				}
			}
			return(null);
		}

		/// <summary>
		/// Lists the names settled for the world's farms
		/// </summary>
		/// <returns>The names, in the order of the farms</returns>
		public static List<string> Names()
		{
			return(plan.ConvertAll((KeyValuePair<Vector3, string> entry) => entry.Value));
		}

		/// <summary>
		/// Shuffles names by a seed, so they come out in the same order for the same seed
		/// </summary>
		/// <param name="names">The names</param>
		/// <param name="seed">The seed - the world's, for the world's farms</param>
		/// <returns>A shuffled copy of the names</returns>
		internal static List<string> Shuffled(IList<string> names, int seed)
		{
			List<string> shuffled = new List<string>(names);
			System.Random rolls = new System.Random(seed);
			for (int i = shuffled.Count - 1; i > 0; i--)
			{
				int j = rolls.Next(i + 1);
				string swap = shuffled[i];
				shuffled[i] = shuffled[j];
				shuffled[j] = swap;
			}
			return(shuffled);
		}

		/// <summary>
		/// Checks whether a name is settled for a farm, so a village started by a player doesn't take it
		/// </summary>
		/// <param name="name">The name</param>
		/// <returns>True when it is</returns>
		public static bool IsSettled(string name)
		{
			return(plan.Exists((KeyValuePair<Vector3, string> entry) => string.Equals(entry.Value, name, System.StringComparison.OrdinalIgnoreCase)));
		}

		/// <summary>
		/// Works out the names on the server - for each of the world's planned farms in order of where they stand whose
		/// village has never been named, the next of VillageNames.AutoNames, shuffled by the world's seed, that no village
		/// goes by and no farm before it got
		/// </summary>
		/// <returns>The farms' planned spots and their names</returns>
		private static List<KeyValuePair<Vector3, string>> Plan()
		{
			List<Vector3> farms = FarmSpawns.PlannedFarms();
			farms.Sort((Vector3 a, Vector3 b) => ((a.x != b.x) ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z)));
			List<string> names = Shuffled(VillageNames.AutoNames, WorldGenerator.instance.GetSeed());
			HashSet<string> taken = new HashSet<string>();
			foreach (VillageNameList.Entry entry in VillageNameList.Others(""))
			{
				taken.Add(entry.Name.ToLowerInvariant());
			}
			List<KeyValuePair<Vector3, string>> planned = new List<KeyValuePair<Vector3, string>>();
			int next = 0;
			foreach (Vector3 farm in farms)
			{
				// A farm already generated keeps the name it was given, or none when a player took it away
				ZDO generator = VillageRespawn.FindGenerator(farm);
				if (generator != null && Utils.DistanceXZ(generator.GetPosition(), farm) <= FarmMatch && VillageNames.EverNamed(generator))
				{
					continue;
				}
				while (next < names.Count && (taken.Contains(names[next].ToLowerInvariant()) || VikingNames.IsPlace(names[next])))
				{
					next++;
				}
				if (next >= names.Count)
				{
					break;
				}
				planned.Add(new KeyValuePair<Vector3, string>(farm, names[next]));
				next++;
			}
			return(planned);
		}

		/// <summary>
		/// Checks whether two plans hold the same farms and names in the same order
		/// </summary>
		/// <param name="a">One plan</param>
		/// <param name="b">The other</param>
		/// <returns>True when they do</returns>
		private static bool Same(List<KeyValuePair<Vector3, string>> a, List<KeyValuePair<Vector3, string>> b)
		{
			if (a.Count != b.Count)
			{
				return(false);
			}
			for (int i = 0; i < a.Count; i++)
			{
				if (a[i].Key != b[i].Key || a[i].Value != b[i].Value)
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
			pkg.Write(plan.Count);
			foreach (KeyValuePair<Vector3, string> entry in plan)
			{
				pkg.Write(entry.Key);
				pkg.Write(entry.Value);
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(target, PlanRpc, pkg);
		}

		/// <summary>
		/// Answers a client's ask for the names, on the server, once they're worked out
		/// </summary>
		/// <param name="sender">The client that asked</param>
		private static void OnAsk(long sender)
		{
			if (ZNet.instance != null && ZNet.instance.IsServer() && ready)
			{
				Send(sender);
			}
		}

		/// <summary>
		/// Takes the names the server sent, on a client
		/// </summary>
		/// <param name="sender">The server</param>
		/// <param name="pkg">The names</param>
		private static void OnPlan(long sender, ZPackage pkg)
		{
			if (ZNet.instance == null || ZNet.instance.IsServer())
			{
				return;
			}
			plan.Clear();
			int count = pkg.ReadInt();
			for (int i = 0; i < count; i++)
			{
				plan.Add(new KeyValuePair<Vector3, string>(pkg.ReadVector3(), pkg.ReadString()));
			}
			ready = true;
		}
	}
}
