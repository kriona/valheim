using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Picks each village's night watch - one of its living vikings each night for every VikingsPerWatch of them, or
	/// part of that, the strongest first and at random among equals (see TakeStrongest), who patrol with a torch out
	/// instead of sleeping, and wake the others when an enemy comes near
	/// </summary>
	/// <remarks>
	/// The client that owns a village's generator picks the watch as dusk falls and notes it in the generator's ZDO
	/// with the night it's for, picks another in the place of one that dies during the night, and changes the watch at
	/// midnight for vikings who haven't kept it yet that night
	/// </remarks>
	internal static class VillageWatch
	{
		/// <summary>
		/// On a farm's generator, the vikings keeping watch, the night and the shift of it they keep watch for, and
		/// every viking that has kept watch that night
		/// </summary>
		private static readonly int WatchersKey = "kriona.VikingVillages.villageWatchers".GetStableHashCode();
		private static readonly int WatchNightKey = "kriona.VikingVillages.villageWatchNight".GetStableHashCode();
		private static readonly int WatchShiftKey = "kriona.VikingVillages.villageWatchShift".GetStableHashCode();
		private static readonly int WatchedKey = "kriona.VikingVillages.villageWatched".GetStableHashCode();

		/// <summary>
		/// On a farm's generator, the viking each viking keeping watch is to wake and hand the watch over to at midnight,
		/// in the same order, ZDOID.None for none, and when the handovers began, in ZNet time ticks - and on a viking
		/// keeping watch, the night it woke its relief
		/// </summary>
		private static readonly int ReliefsKey = "kriona.VikingVillages.villageReliefs".GetStableHashCode();
		private static readonly int ReliefStartKey = "kriona.VikingVillages.villageReliefStart".GetStableHashCode();
		private static readonly int HandedOverKey = "kriona.VikingVillages.vikingHandedOver".GetStableHashCode();

		/// <summary>
		/// How long a viking keeping watch has to wake its relief before the watch is handed over anyway, in seconds
		/// </summary>
		private const float ReliefTimeout = 90f;

		/// <summary>
		/// The routed RPCs that tell every client a viking keeping watch has died, with its village's farm, where it fell
		/// and its ZDO, and that a viking who saw it die claims its place, with the farm, the dead viking's ZDO and the
		/// claimant's
		/// </summary>
		public const string DiedRpc = Plugin.PluginGuid + ".VillageWatchDied";
		public const string ClaimRpc = Plugin.PluginGuid + ".VillageWatchClaim";

		/// <summary>
		/// Registers the RPCs with a world's routed RPC
		/// </summary>
		public static void Register()
		{
			ZRoutedRpc.instance.Register<Vector3, Vector3, ZDOID>(DiedRpc, OnWatchDied);
			ZRoutedRpc.instance.Register<Vector3, ZDOID, ZDOID>(ClaimRpc, OnClaim);
		}

		/// <summary>
		/// Tells every client a viking keeping watch has died, as it dies on its owner, so a viking awake nearby who saw
		/// it can take its place
		/// </summary>
		/// <param name="viking">The dying creature</param>
		public static void OnVikingDeath(Character viking)
		{
			if (viking == null || ZRoutedRpc.instance == null || viking.m_nview == null || !viking.m_nview.IsValid() || !viking.m_nview.IsOwner() || !VikingCore.IsLivingViking(viking))
			{
				return;
			}
			Village village = Village.Find(viking.transform.position);
			ZDOID id = viking.GetZDOID();
			if (village == null || !IsWatch(village, id))
			{
				return;
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, DiedRpc, village.Farm, viking.GetCenterPoint(), id);
		}

		/// <summary>
		/// Has the nearest viking this client owns that saw a viking keeping watch die claim its place (see
		/// VillageLife.WatchWitness)
		/// </summary>
		/// <param name="sender">The client that sent it</param>
		/// <param name="farm">The village's farm</param>
		/// <param name="where">Where the viking keeping watch fell</param>
		/// <param name="dead">Its ZDO</param>
		private static void OnWatchDied(long sender, Vector3 farm, Vector3 where, ZDOID dead)
		{
			Village village = LoadedAt(farm);
			Character witness = ((village != null) ? VillageLife.WatchWitness(village, where, dead) : null);
			if (witness == null)
			{
				return;
			}
			VikingSpeech.Chatter(witness, VikingSpeech.TakeWatch(), VikingSpeech.TalkTime);
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, ClaimRpc, farm, dead, witness.GetZDOID());
		}

		/// <summary>
		/// Gives the place of a viking keeping watch that died to a viking who saw it, on the client that owns the
		/// village - the first to claim it, as long as it hasn't kept watch tonight and the watch is short of one
		/// </summary>
		/// <param name="sender">The client that sent it</param>
		/// <param name="farm">The village's farm</param>
		/// <param name="dead">The dead viking's ZDO</param>
		/// <param name="claimant">The claiming viking's ZDO</param>
		private static void OnClaim(long sender, Vector3 farm, ZDOID dead, ZDOID claimant)
		{
			Village village = LoadedAt(farm);
			if (village == null || !village.IsOwner())
			{
				return;
			}
			ZDO zdo = village.Zdo;
			long night = Night();
			if (zdo.GetLong(WatchNightKey, -1L) != night)
			{
				return;
			}
			List<ZDOID> watchers = Watchers(zdo);
			List<ZDOID> watched = ReadIds(zdo, WatchedKey);
			List<ZDOID> reliefs = ReadIds(zdo, ReliefsKey);
			if (!TryClaim(watchers, watched, reliefs, dead, claimant, WatchCount(village.Vikings().Count)))
			{
				return;
			}
			WriteIds(zdo, WatchersKey, watchers);
			WriteIds(zdo, WatchedKey, watched);
			WriteIds(zdo, ReliefsKey, reliefs);
			Plugin.Log.LogInfo(TestLog.Name(ZDOMan.instance.GetZDO(claimant)) + " of the village at " + village.Farm.ToString("F0") + " saw " + TestLog.Name(ZDOMan.instance.GetZDO(dead)) + " die keeping watch and took its place");
		}

		/// <summary>
		/// Gives a viking that saw a viking keeping watch die its place - the dead one's, keeping no relief, or else a
		/// place of its own while the watch is short of the vikings it keeps watch with - as long as it isn't keeping
		/// watch, hasn't kept it tonight and isn't to be woken to
		/// </summary>
		/// <param name="watchers">The vikings keeping watch, changed when it's given the place</param>
		/// <param name="watched">Every viking that has kept watch tonight, added to when it's given the place</param>
		/// <param name="reliefs">The viking each viking keeping watch is to wake, in the same order, ZDOID.None for none -
		/// filled out to as many as are keeping watch, and changed along with them</param>
		/// <param name="dead">The dead viking's ZDO</param>
		/// <param name="claimant">The claiming viking's ZDO</param>
		/// <param name="wanted">How many vikings keep watch for the village (see WatchCount)</param>
		/// <returns>True when it's given the place</returns>
		internal static bool TryClaim(List<ZDOID> watchers, List<ZDOID> watched, List<ZDOID> reliefs, ZDOID dead, ZDOID claimant, int wanted)
		{
			while (reliefs.Count < watchers.Count)
			{
				reliefs.Add(ZDOID.None);
			}
			if (watchers.Contains(claimant) || watched.Contains(claimant) || reliefs.Contains(claimant))
			{
				return(false);
			}
			int index = watchers.IndexOf(dead);
			if (index >= 0)
			{
				watchers[index] = claimant;
				reliefs[index] = ZDOID.None;
			}
			else if (watchers.Count < wanted)
			{
				watchers.Add(claimant);
				reliefs.Add(ZDOID.None);
			}
			else
			{
				return(false);
			}
			watched.Add(claimant);
			return(true);
		}

		/// <summary>
		/// Checks whether a viking has kept watch for its village tonight, or is to be woken to
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>True when it has</returns>
		public static bool HasWatched(Village village, ZDOID viking)
		{
			ZDO zdo = village.Zdo;
			return(zdo != null && zdo.IsValid() && zdo.GetLong(WatchNightKey, -1L) == Night() && ReadIds(zdo, WatchedKey).Contains(viking));
		}

		/// <summary>
		/// Finds the loaded village of a farm
		/// </summary>
		/// <param name="farm">The farm's spot</param>
		/// <returns>The village, or null when it isn't loaded</returns>
		private static Village LoadedAt(Vector3 farm)
		{
			foreach (Village village in Village.Loaded())
			{
				if (Utils.DistanceXZ(village.Farm, farm) < 1f)
				{
					return(village);
				}
			}
			return(null);
		}

		/// <summary>
		/// How many of a village's vikings each viking keeping watch is for - a village of 10 or fewer has one, one of
		/// 11-20 two, and so on
		/// </summary>
		private const int VikingsPerWatch = 10;

		/// <summary>
		/// Seconds between looks at the villages' watches
		/// </summary>
		private const float CheckInterval = 2f;

		/// <summary>
		/// The part of the world's day each night's number starts at - noon
		/// </summary>
		private const double NightStart = 0.5;

		private static float nextCheck;

		/// <summary>
		/// Numbers the nights, each counted from the noon before it
		/// </summary>
		/// <remarks>
		/// The number changes at noon by the world's clock, far from dusk and night, so a night keeps its number from
		/// dusk until dawn - even when the time of day is set with the tod command, which leaves the world's clock as it
		/// is
		/// </remarks>
		/// <returns>Tonight's number, or the coming night's by day</returns>
		public static long Night()
		{
			if (ZNet.instance == null || EnvMan.instance == null)
			{
				return(0L);
			}
			return(Night(ZNet.instance.GetTimeSeconds(), EnvMan.instance.m_dayLengthSec));
		}

		/// <summary>
		/// Numbers the night at a time by the world's clock, each counted from the noon before it (see Night())
		/// </summary>
		/// <param name="seconds">The time, in world seconds</param>
		/// <param name="dayLength">How long a game day is, in seconds</param>
		/// <returns>The night's number, or the coming night's by day, or 0 when the day has no length</returns>
		internal static long Night(double seconds, long dayLength)
		{
			if (dayLength <= 0)
			{
				return(0L);
			}
			double days = seconds / dayLength;
			return((long)System.Math.Floor(days - NightStart));
		}

		/// <summary>
		/// Describes a village's watch tonight, for the debug hover text of its fire
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>Each watch's name and how far it is from the middle, or "not loaded" when it's elsewhere, or
		/// "none"</returns>
		public static string Describe(Village village)
		{
			ZDO zdo = village.Zdo;
			if (zdo == null || !zdo.IsValid() || zdo.GetLong(WatchNightKey, -1L) != Night())
			{
				return("none");
			}
			List<string> watches = new List<string>();
			foreach (ZDOID id in Watchers(zdo))
			{
				GameObject found = ZNetScene.instance.FindInstance(id);
				Character watch = ((found != null) ? found.GetComponent<Character>() : null);
				watches.Add((watch == null) ? "not loaded" : watch.GetHoverName() + (watch.IsDead() ? " (dead)" : "") + " at " + Vector3.Distance(watch.transform.position, village.Farm).ToString("F0") + "m from the middle");
			}
			return((watches.Count > 0) ? string.Join(", ", watches) : "none");
		}

		/// <summary>
		/// Checks whether a viking keeps watch for its village tonight
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>True when it does</returns>
		public static bool IsWatch(Village village, ZDOID viking)
		{
			ZDO zdo = village.Zdo;
			return(zdo != null && zdo.IsValid() && zdo.GetLong(WatchNightKey, -1L) == Night() && Watchers(zdo).Contains(viking));
		}

		/// <summary>
		/// Works out how many vikings keep watch for a village - one for every VikingsPerWatch of its vikings, or part
		/// of that
		/// </summary>
		/// <param name="vikings">How many vikings it has</param>
		/// <returns>How many keep watch</returns>
		internal static int WatchCount(int vikings)
		{
			return((vikings <= 0) ? 0 : (vikings + VikingsPerWatch - 1) / VikingsPerWatch);
		}

		/// <summary>
		/// Reads the vikings keeping watch for a village
		/// </summary>
		/// <param name="zdo">The village's generator's ZDO</param>
		/// <returns>Their ZDOs</returns>
		private static List<ZDOID> Watchers(ZDO zdo)
		{
			return(ReadIds(zdo, WatchersKey));
		}

		/// <summary>
		/// Reads a list of vikings noted on a village's generator
		/// </summary>
		/// <param name="zdo">The village's generator's ZDO</param>
		/// <param name="key">The key they're noted under</param>
		/// <returns>Their ZDOs</returns>
		private static List<ZDOID> ReadIds(ZDO zdo, int key)
		{
			byte[] bytes = zdo.GetByteArray(key);
			return((bytes != null) ? DecodeIds(bytes) : new List<ZDOID>());
		}

		/// <summary>
		/// Reads a list of vikings as EncodeIds wrote it
		/// </summary>
		/// <param name="bytes">The bytes</param>
		/// <returns>Their ZDOs, in the same order, or none when the bytes are cut short</returns>
		internal static List<ZDOID> DecodeIds(byte[] bytes)
		{
			List<ZDOID> ids = new List<ZDOID>();
			try
			{
				ZPackage pkg = new ZPackage(bytes);
				int count = pkg.ReadInt();
				for (int i = 0; i < count; i++)
				{
					ids.Add(pkg.ReadZDOID());
				}
			}
			catch (System.Exception)
			{
				ids.Clear();
			}
			return(ids);
		}

		/// <summary>
		/// Notes a list of vikings on a village's generator (see EncodeIds)
		/// </summary>
		/// <param name="zdo">The village's generator's ZDO, owned by this client</param>
		/// <param name="key">The key they're noted under</param>
		/// <param name="ids">Their ZDOs</param>
		private static void WriteIds(ZDO zdo, int key, List<ZDOID> ids)
		{
			zdo.Set(key, EncodeIds(ids));
		}

		/// <summary>
		/// Writes a list of vikings - their count, then each one's ZDO
		/// </summary>
		/// <param name="ids">Their ZDOs</param>
		/// <returns>The bytes</returns>
		internal static byte[] EncodeIds(List<ZDOID> ids)
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(ids.Count);
			foreach (ZDOID id in ids)
			{
				pkg.Write(id);
			}
			return(pkg.GetArray());
		}

		/// <summary>
		/// Picks the watch of each village this client owns from dusk until dawn, when it has none for tonight or its
		/// watch has died - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (Time.time < nextCheck || ZNetScene.instance == null || EnvMan.instance == null || !ServerCheck.Active)
			{
				return;
			}
			nextCheck = Time.time + CheckInterval;
			if (!EnvMan.IsNight() && EnvMan.instance.GetDayFraction() < VillageLife.DuskStart)
			{
				return;
			}
			foreach (Village village in Village.Loaded())
			{
				EnsureWatch(village);
			}
		}

		/// <summary>
		/// Picks a village's watch for tonight when this client owns the village and it's short of the vikings it keeps
		/// watch with (see WatchCount) - none picked for tonight yet, or one has died - only before night falls, so no
		/// viking asleep is woken for it, and at midnight has each viking keeping watch wake and hand over to one who
		/// hasn't kept watch tonight, so no viking keeps watch twice in a night. One with no one left to hand over to
		/// keeps watch on, and one that dies before handing over leaves its relief asleep. Called before a viking picks
		/// what to do from dusk on, so a viking keeping watch goes straight to it rather than heading to bed first
		/// </summary>
		/// <param name="village">The village</param>
		public static void EnsureWatch(Village village)
		{
			if (village == null || !village.IsOwner())
			{
				return;
			}
			long night = Night();
			int shift = Shift();
			ZDO zdo = village.Zdo;
			bool tonight = (zdo.GetLong(WatchNightKey, -1L) == night);
			List<ZDOID> watchers = (tonight ? Watchers(zdo) : new List<ZDOID>());
			List<ZDOID> watched = (tonight ? ReadIds(zdo, WatchedKey) : new List<ZDOID>());
			List<ZDOID> reliefs = (tonight ? ReadIds(zdo, ReliefsKey) : new List<ZDOID>());
			while (reliefs.Count < watchers.Count)
			{
				reliefs.Add(ZDOID.None);
			}
			bool changed = !tonight;
			List<Character> vikings = village.Vikings();
			// At midnight each viking keeping watch is given one who hasn't kept it tonight to wake and hand it over to
			if (tonight && zdo.GetInt(WatchShiftKey) != shift)
			{
				List<Character> fresh = vikings.FindAll((Character viking) => !watchers.Contains(viking.GetZDOID()) && !watched.Contains(viking.GetZDOID()));
				reliefs.Clear();
				foreach (ZDOID _ in watchers)
				{
					ZDOID relief = ((fresh.Count > 0) ? TakeStrongest(fresh) : ZDOID.None);
					reliefs.Add(relief);
					if (relief != ZDOID.None)
					{
						watched.Add(relief);
					}
				}
				zdo.Set(ReliefStartKey, NetTime.Now());
				changed = true;
			}
			// A handover is done once the viking keeping watch has woken its relief, or has taken too long - one that dies
			// first never wakes its relief, which sleeps on
			bool overdue = (NetTime.Now() - zdo.GetLong(ReliefStartKey, 0L) > NetTime.Ticks(ReliefTimeout));
			for (int i = 0; i < watchers.Count && i < reliefs.Count; i++)
			{
				if (reliefs[i] == ZDOID.None)
				{
					continue;
				}
				if (!Keeping(reliefs[i]) || !Keeping(watchers[i]))
				{
					reliefs[i] = ZDOID.None;
					changed = true;
					continue;
				}
				ZDO outgoing = ZDOMan.instance.GetZDO(watchers[i]);
				if (overdue || (outgoing != null && outgoing.GetLong(HandedOverKey, -1L) == night))
				{
					watchers[i] = reliefs[i];
					reliefs[i] = ZDOID.None;
					changed = true;
				}
			}
			// One that died is taken out, and before night falls, while the village is still up, one who hasn't kept
			// watch tonight takes its place - once it's night, no one asleep is woken for it
			for (int i = watchers.Count - 1; i >= 0; i--)
			{
				if (!Keeping(watchers[i]))
				{
					watchers.RemoveAt(i);
					if (i < reliefs.Count)
					{
						reliefs.RemoveAt(i);
					}
					changed = true;
				}
			}
			int wanted = (EnvMan.IsNight() ? 0 : WatchCount(vikings.Count));
			List<Character> free = vikings.FindAll((Character viking) => !watchers.Contains(viking.GetZDOID()) && !watched.Contains(viking.GetZDOID()));
			while (watchers.Count < wanted && free.Count > 0)
			{
				ZDOID added = TakeStrongest(free);
				watchers.Add(added);
				reliefs.Add(ZDOID.None);
				watched.Add(added);
				changed = true;
			}
			if (!changed)
			{
				return;
			}
			foreach (ZDOID id in watchers)
			{
				if (!watched.Contains(id))
				{
					watched.Add(id);
				}
			}
			WriteIds(zdo, WatchersKey, watchers);
			WriteIds(zdo, WatchedKey, watched);
			WriteIds(zdo, ReliefsKey, reliefs);
			zdo.Set(WatchNightKey, night);
			zdo.Set(WatchShiftKey, shift);
			Plugin.Log.LogInfo("Watch of the village at " + village.Farm.ToString("F0") + ": " + watchers.Count + " keeping watch, " + reliefs.FindAll((ZDOID id) => id != ZDOID.None).Count + " to be woken to take over, " + watched.Count + " so far tonight, of " + vikings.Count + " vikings");
		}

		/// <summary>
		/// Finds the viking a viking keeping watch is to wake and hand the watch over to at midnight
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="viking">The viking keeping watch's ZDO</param>
		/// <returns>Its relief's ZDO, or ZDOID.None when it has none to wake</returns>
		public static ZDOID ReliefFor(Village village, ZDOID viking)
		{
			ZDO zdo = village.Zdo;
			if (zdo == null || !zdo.IsValid() || zdo.GetLong(WatchNightKey, -1L) != Night())
			{
				return(ZDOID.None);
			}
			int index = Watchers(zdo).IndexOf(viking);
			List<ZDOID> reliefs = ReadIds(zdo, ReliefsKey);
			return((index >= 0 && index < reliefs.Count) ? reliefs[index] : ZDOID.None);
		}

		/// <summary>
		/// Notes on a viking keeping watch that it has woken its relief, so the watch is handed over
		/// </summary>
		/// <param name="viking">The viking keeping watch, owned by this client</param>
		public static void HandedOver(Character viking)
		{
			if (viking.m_nview != null && viking.m_nview.IsValid() && viking.m_nview.IsOwner())
			{
				viking.m_nview.GetZDO().Set(HandedOverKey, Night());
			}
		}

		/// <summary>
		/// Works out which shift of the night it is - the first from dusk until midnight, the second after
		/// </summary>
		/// <returns>0 for the first shift, 1 for the second</returns>
		private static int Shift()
		{
			return(Shift(EnvMan.instance.GetDayFraction()));
		}

		/// <summary>
		/// Works out which shift of the night a time of day is in - the first from dusk until midnight, the second after
		/// </summary>
		/// <param name="dayFraction">The time of day, from 0 at midnight to 1 at the next</param>
		/// <returns>0 for the first shift, 1 for the second</returns>
		internal static int Shift(float dayFraction)
		{
			return((dayFraction < 0.5f) ? 1 : 0);
		}

		/// <summary>
		/// Takes the strongest viking out of a list - one of the hardest biome, at random among them (see Strongest) - so
		/// a viking come from a harder biome than the village's keeps watch before the rest
		/// </summary>
		/// <param name="vikings">The vikings, one fewer after</param>
		/// <returns>The viking's ZDO</returns>
		private static ZDOID TakeStrongest(List<Character> vikings)
		{
			List<int> strongest = Strongest(vikings.ConvertAll((Character viking) =>
			{
				VikingCore core = viking.GetComponent<VikingCore>();
				return((core != null) ? core.BiomeIndex() : 0);
			}));
			int pick = strongest[Random.Range(0, strongest.Count)];
			ZDOID id = vikings[pick].GetZDOID();
			vikings.RemoveAt(pick);
			return(id);
		}

		/// <summary>
		/// Finds the vikings of the hardest biome among some
		/// </summary>
		/// <param name="biomes">Each viking's biome, by its index in VikingGear's tables</param>
		/// <returns>Their places in the list, none for an empty list</returns>
		internal static List<int> Strongest(List<int> biomes)
		{
			List<int> strongest = new List<int>();
			int hardest = int.MinValue;
			for (int i = 0; i < biomes.Count; i++)
			{
				if (biomes[i] > hardest)
				{
					hardest = biomes[i];
					strongest.Clear();
				}
				if (biomes[i] == hardest)
				{
					strongest.Add(i);
				}
			}
			return(strongest);
		}

		/// <summary>
		/// Checks whether a viking picked to keep watch is still keeping it - alive, or out of this client's reach, where
		/// it can't be told
		/// </summary>
		/// <param name="id">The viking's ZDO</param>
		/// <returns>True when it is</returns>
		private static bool Keeping(ZDOID id)
		{
			if (id == ZDOID.None || ZDOMan.instance.GetZDO(id) == null)
			{
				return(false);
			}
			GameObject found = ZNetScene.instance.FindInstance(id);
			Character watch = ((found != null) ? found.GetComponent<Character>() : null);
			return(found == null || (watch != null && !watch.IsDead()));
		}
	}
}
