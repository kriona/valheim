using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What a village remembers of its dead - the names of its named vikings who died in the last MemoryDays game days,
	/// which its vikings now and then talk about fondly - and the corpses of its vikings, which those near them come to
	/// mourn
	/// </summary>
	/// <remarks>
	/// A named viking's death is reported to every client, and the one that owns its village's generator notes it in the
	/// generator's ZDO, so it carries over reloads and owner changes. A PC viking's corpse notes its village and its
	/// name as its owner makes it
	/// </remarks>
	internal static class VillageFallen
	{
		/// <summary>
		/// On a farm's generator, the named vikings of its village who died - each one's name as it was typed, who gave
		/// it, and when it died in ZNet time ticks, earliest first
		/// </summary>
		private static readonly int FallenKey = "kriona.VikingVillages.villageFallen".GetStableHashCode();

		/// <summary>
		/// On a PC viking's corpse, where the farm of the village it belonged to stands
		/// </summary>
		private static readonly int CorpseFarmKey = "kriona.VikingVillages.fallenFarm".GetStableHashCode();

		/// <summary>
		/// The routed RPC that tells every client a named viking died, with where its village's farm stands, its name as
		/// it was typed, who gave it and when it died in ZNet time ticks
		/// </summary>
		public const string FallenRpc = Plugin.PluginGuid + ".VillageFallen";

		/// <summary>
		/// How many game days a village remembers a viking who died, and the most it remembers at once
		/// </summary>
		private const float MemoryDays = 7f;
		private const int MaxFallen = 32;

		/// <summary>
		/// How likely a talk between two vikings is about one of their dead, when the village remembers any, and the
		/// least time between two such talks in a village on this client, in seconds
		/// </summary>
		private const float RememberChance = 0.15f;
		private const float RememberInterval = 900f;

		/// <summary>
		/// How far from where it stood a village's farm can be noted on a corpse and still be it, in meters
		/// </summary>
		private const float FarmMatch = 1f;

		/// <summary>
		/// A viking a village remembers
		/// </summary>
		internal struct Fallen
		{
			public string Name;
			public string Author;
			public long Time;
		}

		/// <summary>
		/// When each village's vikings on this client last talked about one of their dead, by its generator's ZDO
		/// </summary>
		private static readonly Dictionary<ZDOID, float> rememberedAt = new Dictionary<ZDOID, float>();

		/// <summary>
		/// Every corpse that has woken, dropped once it's destroyed
		/// </summary>
		private static readonly List<Ragdoll> corpses = new List<Ragdoll>();

		/// <summary>
		/// Registers the RPC with a world's routed RPC, forgetting what the world being left noted
		/// </summary>
		public static void Register()
		{
			ZRoutedRpc.instance.Register<Vector3, string, string, long>(FallenRpc, OnFallen);
			rememberedAt.Clear();
		}

		/// <summary>
		/// Tells every client a named PC viking died, as it dies on its owner, so its village's generator notes its
		/// name
		/// </summary>
		/// <param name="viking">The dying creature</param>
		public static void OnVikingDeath(Character viking)
		{
			if (viking == null || ZRoutedRpc.instance == null || !VikingCore.IsPCViking(viking) || viking.m_nview == null || !viking.m_nview.IsValid() || !viking.m_nview.IsOwner())
			{
				return;
			}
			VikingNames.ReadRaw(viking.m_nview.GetZDO(), out string name, out string author);
			if (name.Length == 0 || !VillageRespawn.FarmOf(viking, out Vector3 farm))
			{
				return;
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, FallenRpc, farm, name, author, NetTime.Now());
		}

		/// <summary>
		/// Notes a named viking's death in its village's generator when this client owns it - or, on the server, when no
		/// one does, taking it over to note the death - forgetting the deaths more than MemoryDays old
		/// </summary>
		/// <param name="sender">The client the viking died on</param>
		/// <param name="farm">Where the viking's village's farm stands</param>
		/// <param name="name">Its name as it was typed</param>
		/// <param name="author">Who gave it</param>
		/// <param name="time">When it died, in ZNet time ticks</param>
		private static void OnFallen(long sender, Vector3 farm, string name, string author, long time)
		{
			ZDO generator = VillageRespawn.FindGenerator(farm);
			if (generator == null)
			{
				return;
			}
			if (!generator.IsOwner())
			{
				if (!ZNet.instance.IsServer() || generator.HasOwner())
				{
					return;
				}
				generator.SetOwner(ZDOMan.GetSessionID());
			}
			List<Fallen> fallen = Read(generator);
			Fallen death = new Fallen();
			death.Name = name;
			death.Author = author;
			death.Time = time;
			AddDeath(fallen, death);
			Write(generator, fallen);
			Plugin.Log.LogInfo("The village at " + farm.ToString("F0") + " will remember " + name + " for " + MemoryDays.ToString("F0") + " days");
		}

		/// <summary>
		/// Lists the names of a village's dead it still remembers, filtered for their authors' words, leaving out any a
		/// PC viking of the village has taken since
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The names, the latest to die last</returns>
		public static List<string> Names(Village village)
		{
			if (village == null || village.Zdo == null)
			{
				return(new List<string>());
			}
			List<string> living = new List<string>();
			foreach (Character viking in village.Vikings())
			{
				living.Add(VikingNames.Get(viking));
			}
			List<string> fallen = new List<string>();
			foreach (Fallen one in Read(village.Zdo))
			{
				fallen.Add(VikingNames.Filter(one.Name, one.Author).RemoveRichTextTags().Trim());
			}
			return(FilterNames(fallen, living));
		}

		/// <summary>
		/// Lists the names of a village's dead who died since a time, filtered for their authors' words - the ones a
		/// viking out on a quest missed
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="since">The earliest death, in ZNet time ticks</param>
		/// <returns>The names, the latest to die last</returns>
		public static List<string> DiedSince(Village village, long since)
		{
			List<string> names = new List<string>();
			byte[] bytes = ((village != null && village.Zdo != null) ? village.Zdo.GetByteArray(FallenKey) : null);
			List<Fallen> decoded = ((bytes != null) ? Decode(bytes, since) : null);
			if (decoded == null)
			{
				return(names);
			}
			foreach (Fallen one in decoded)
			{
				names.Add(VikingNames.Filter(one.Name, one.Author).RemoveRichTextTags().Trim());
			}
			return(FilterNames(names, new List<string>()));
		}

		/// <summary>
		/// Picks out the names of a village's dead worth talking about - each name once, leaving out empty names and any
		/// a PC viking of the village has, ignoring case throughout
		/// </summary>
		/// <param name="fallenNames">The names of the dead, as they're shown, the earliest to die first</param>
		/// <param name="livingNames">The names of the village's PC vikings</param>
		/// <returns>The names, in the order they came, each spelled as it first came</returns>
		internal static List<string> FilterNames(List<string> fallenNames, List<string> livingNames)
		{
			HashSet<string> living = new HashSet<string>(livingNames, System.StringComparer.OrdinalIgnoreCase);
			HashSet<string> seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
			List<string> names = new List<string>();
			foreach (string name in fallenNames)
			{
				if (name.Length > 0 && !living.Contains(name) && seen.Add(name))
				{
					names.Add(name);
				}
			}
			return(names);
		}

		/// <summary>
		/// Adds a death to the vikings a village remembers, in the place of an earlier death of a viking with the same
		/// name, ignoring case
		/// </summary>
		/// <param name="fallen">The vikings the village remembers, added to</param>
		/// <param name="death">The viking that died</param>
		internal static void AddDeath(List<Fallen> fallen, Fallen death)
		{
			fallen.RemoveAll((Fallen one) => string.Equals(one.Name, death.Name, System.StringComparison.OrdinalIgnoreCase));
			fallen.Add(death);
		}

		/// <summary>
		/// Picks one of a village's dead for two of its vikings to talk about - now and then, at RememberChance, and no
		/// sooner than RememberInterval after the last time they did on this client
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>Its name, or "" when they talk about something else</returns>
		public static string Remember(Village village)
		{
			if (village == null || village.Zdo == null || village.Zdo.GetByteArray(FallenKey) == null)
			{
				return("");
			}
			ZDOID id = village.Zdo.m_uid;
			if ((rememberedAt.TryGetValue(id, out float last) && Time.time - last < RememberInterval) || Random.value >= RememberChance)
			{
				return("");
			}
			List<string> names = Names(village);
			if (names.Count == 0)
			{
				return("");
			}
			rememberedAt[id] = Time.time;
			return(names[Random.Range(0, names.Count)]);
		}

		/// <summary>
		/// Notes a corpse as it wakes, so the corpses of the village's vikings are found without looking through every
		/// loaded object
		/// </summary>
		/// <param name="corpse">The corpse</param>
		public static void NoteCorpse(Ragdoll corpse)
		{
			corpses.Add(corpse);
		}

		/// <summary>
		/// Marks a PC viking's corpse with its village and its name, as its owner makes it
		/// </summary>
		/// <param name="viking">The viking that died</param>
		/// <param name="corpse">Its corpse, just made by this client</param>
		public static void MarkCorpse(Character viking, Ragdoll corpse)
		{
			if (viking == null || corpse == null || !VikingCore.IsPCViking(viking) || corpse.m_nview == null || !corpse.m_nview.IsValid() || !corpse.m_nview.IsOwner())
			{
				return;
			}
			if (!VillageRespawn.FarmOf(viking, out Vector3 farm))
			{
				return;
			}
			ZDO zdo = corpse.m_nview.GetZDO();
			zdo.Set(CorpseFarmKey, farm);
			VikingNames.CopyTo(viking.m_nview.GetZDO(), zdo);
		}

		/// <summary>
		/// Checks whether a corpse is a PC viking's of a village
		/// </summary>
		/// <param name="corpse">The corpse's ZDO</param>
		/// <param name="village">The village</param>
		/// <returns>True when it is</returns>
		public static bool IsOf(ZDO corpse, Village village)
		{
			Vector3 farm = ((corpse != null) ? corpse.GetVec3(CorpseFarmKey, Vector3.zero) : Vector3.zero);
			return(farm != Vector3.zero && village != null && Utils.DistanceXZ(farm, village.Farm) <= FarmMatch);
		}

		/// <summary>
		/// Finds the corpse of one of a village's vikings nearest a point that stays dead - not one about to rise, which
		/// the vikings stand ready by instead
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="point">The point</param>
		/// <param name="range">How far from it to look, in meters</param>
		/// <param name="skip">True for a corpse to leave out</param>
		/// <returns>The corpse, or null when there's none that near</returns>
		public static Ragdoll NearestCorpse(Village village, Vector3 point, float range, System.Predicate<Ragdoll> skip)
		{
			corpses.RemoveAll((Ragdoll candidate) => candidate == null);
			Ragdoll nearest = null;
			float nearestDistance = range;
			foreach (Ragdoll corpse in corpses)
			{
				ZNetView view = corpse.m_nview;
				if (view == null || !view.IsValid())
				{
					continue;
				}
				ZDO zdo = view.GetZDO();
				if (zdo.GetLong(ZombieState.RiseAtKey) != 0L || !IsOf(zdo, village) || skip(corpse))
				{
					continue;
				}
				float distance = Vector3.Distance(corpse.GetAverageBodyPosition(), point);
				if (distance <= nearestDistance)
				{
					nearest = corpse;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Reads the vikings a village's generator remembers, leaving out those that died more than MemoryDays ago
		/// </summary>
		/// <param name="generator">The generator's ZDO</param>
		/// <returns>The vikings, earliest to die first</returns>
		private static List<Fallen> Read(ZDO generator)
		{
			List<Fallen> fallen = new List<Fallen>();
			byte[] bytes = generator.GetByteArray(FallenKey);
			if (bytes == null || EnvMan.instance == null)
			{
				return(fallen);
			}
			long since = NetTime.Now() - NetTime.Ticks(EnvMan.instance.m_dayLengthSec * MemoryDays);
			List<Fallen> decoded = Decode(bytes, since);
			if (decoded == null)
			{
				Plugin.Log.LogWarning("Couldn't read the dead remembered by the village at " + generator.GetPosition().ToString("F0"));
				return(fallen);
			}
			return(decoded);
		}

		/// <summary>
		/// Writes the vikings a village's generator remembers, keeping the latest MaxFallen to die
		/// </summary>
		/// <param name="generator">The generator's ZDO, owned by this client</param>
		/// <param name="fallen">The vikings, in any order</param>
		private static void Write(ZDO generator, List<Fallen> fallen)
		{
			generator.Set(FallenKey, Encode(fallen));
		}

		/// <summary>
		/// Reads the vikings a village remembers from the bytes they're noted as, leaving out those that died before a
		/// time
		/// </summary>
		/// <param name="bytes">The bytes (see Encode)</param>
		/// <param name="since">The earliest death kept, in ZNet time ticks - a death at this time is kept</param>
		/// <returns>The vikings, earliest to die first, or null when the bytes can't be read</returns>
		internal static List<Fallen> Decode(byte[] bytes, long since)
		{
			List<Fallen> fallen = new List<Fallen>();
			try
			{
				ZPackage pkg = new ZPackage(bytes);
				int count = pkg.ReadInt();
				for (int i = 0; i < count; i++)
				{
					Fallen one = new Fallen();
					one.Name = pkg.ReadString();
					one.Author = pkg.ReadString();
					one.Time = pkg.ReadLong();
					if (one.Time >= since)
					{
						fallen.Add(one);
					}
				}
			}
			catch (System.Exception)
			{
				return(null);
			}
			return(fallen);
		}

		/// <summary>
		/// Writes the vikings a village remembers as bytes, sorted by when they died and keeping the latest MaxFallen to
		/// die
		/// </summary>
		/// <param name="fallen">The vikings, in any order, left as they are</param>
		/// <returns>The bytes</returns>
		internal static byte[] Encode(List<Fallen> fallen)
		{
			List<Fallen> sorted = new List<Fallen>(fallen);
			sorted.Sort((Fallen a, Fallen b) => a.Time.CompareTo(b.Time));
			int start = Mathf.Max(0, sorted.Count - MaxFallen);
			ZPackage pkg = new ZPackage();
			pkg.Write(sorted.Count - start);
			for (int i = start; i < sorted.Count; i++)
			{
				pkg.Write(sorted[i].Name);
				pkg.Write(sorted[i].Author);
				pkg.Write(sorted[i].Time);
			}
			return(pkg.GetArray());
		}
	}
}
