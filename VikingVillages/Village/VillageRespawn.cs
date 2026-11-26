using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Keeps each village's head count - how many living vikings it was generated with - and brings back the ones it's
	/// missing after they die, one at a time
	/// </summary>
	/// <remarks>
	/// Whoever owns a village's generator looks after it, and everything it keeps track of is in the generator's ZDO,
	/// so it carries over reloads and owner changes. A viking's death is reported to every client, and only the one
	/// that owns the generator notes it. The next viking comes back a game day after the last one did or after its
	/// death, whichever is later - three days when a player killed it - once no enemy is near the village, at a spot no
	/// player is near or can see outside the farm's fence, and walks in. A viking that went missing without its death being noted is counted as dying when
	/// it's found missing
	/// </remarks>
	internal static class VillageRespawn
	{
		/// <summary>
		/// On a farm's generator, how many living vikings its village was generated with, and that its houses were
		/// given their planned number of vikings each, so its head count can be worked out from them
		/// </summary>
		public static readonly int HeadCountKey = "kriona.VikingVillages.villageHeadCount".GetStableHashCode();
		public static readonly int PlannedKey = "kriona.VikingVillages.villagePlanned".GetStableHashCode();

		/// <summary>
		/// On a farm's generator, that its village has had every viking it starts with
		/// </summary>
		public static readonly int PopulatedKey = "kriona.VikingVillages.villagePopulated".GetStableHashCode();

		/// <summary>
		/// On a farm's generator, the deaths waiting for a viking to come back in their place - when each happened and
		/// whether a player killed it - and when the last viking came back, in ZNet time ticks
		/// </summary>
		public static readonly int DeathsKey = "kriona.VikingVillages.villageDeaths".GetStableHashCode();
		public static readonly int LastRespawnKey = "kriona.VikingVillages.villageLastRespawn".GetStableHashCode();

		/// <summary>
		/// The routed RPC that tells every client a living viking died, with where its village's farm stands, when it
		/// died in ZNet time ticks and whether a player killed it
		/// </summary>
		public const string DeathRpc = "kriona.VikingVillages.VillageDeath";

		/// <summary>
		/// The fewest and most living vikings a house is planned to hold
		/// </summary>
		private const int HouseHeadsMin = 2;
		private const int HouseHeadsMax = 2;


		/// <summary>
		/// Seconds between looks at which zones are loaded, and between looks after the villages this client owns
		/// </summary>
		private const float ZoneInterval = 1f;
		private const float CheckInterval = 5f;

		/// <summary>
		/// Seconds between looks after the villages this client owns while one is still being set up, and the fewest
		/// milliseconds a step of setting one up has to take to be written to the log (see Timed)
		/// </summary>
		private const float SetupInterval = 1f;
		private const double TimingLogMin = 10.0;

		/// <summary>
		/// Seconds a zone has to have been loaded before what's in it is judged, so its objects have all arrived
		/// </summary>
		private const float SettleTime = 15f;

		/// <summary>
		/// Seconds a village's vikings have to keep not adding up to its head count before it's put right, so a death
		/// on its way to being noted isn't counted twice
		/// </summary>
		private const float MismatchTime = 20f;

		/// <summary>
		/// How far past the village's edge an enemy keeps a viking from coming back, how far from where it would come
		/// from one keeps it from coming there, how far from every player it comes back, and how far past the edge its
		/// vikings are counted, in meters
		/// </summary>
		public const float EnemyReach = 10f;
		private const float ArrivalClear = 20f;
		private const float PlayerClearance = 20f;
		private const float StrayReach = 100f;

		/// <summary>
		/// How many game days a viking takes to come back, and how many when a player killed it
		/// </summary>
		private const float RespawnDays = 1f;
		private const float PlayerKillDays = 3f;

		/// <summary>
		/// How many open spots are tried for one out of every player's sight, and how long a village looks for one
		/// before a viking comes where a player is near or can see it, in seconds
		/// </summary>
		private const int HiddenTries = 5;
		private const float SpotPatience = 120f;

		/// <summary>
		/// Since when each village has been looking for a spot for a viking coming to it, and when it last looked, by its
		/// generator's ZDO
		/// </summary>
		private static readonly Dictionary<ZDOID, float> searchingSince = new Dictionary<ZDOID, float>();
		private static readonly Dictionary<ZDOID, float> lastSearch = new Dictionary<ZDOID, float>();

		/// <summary>
		/// Since when an enemy has been in or near each village while a viking was due to come to it, and when one was
		/// last found there, by its generator's ZDO, by Time.time on this client
		/// </summary>
		private static readonly Dictionary<ZDOID, float> enemySince = new Dictionary<ZDOID, float>();
		private static readonly Dictionary<ZDOID, float> enemyLastSeen = new Dictionary<ZDOID, float>();

		/// <summary>
		/// How long a village can go unlooked at for enemies before the wait for them to leave starts over, in seconds -
		/// a village is only looked at while a viking is due to come to it, so a longer gap means a new wait
		/// </summary>
		internal const float EnemyWaitGap = CheckInterval * 3f;

		/// <summary>
		/// How long an enemy in or near a village keeps a viking from coming to it, in seconds - after that it comes
		/// anyway, from a spot clear of enemies (see FindArrivalSpot), rather than waiting on monsters a raid left behind
		/// that may never go - unless a raid is still on
		/// </summary>
		internal const float EnemyPatience = 600f;

		/// <summary>
		/// How far from the spot a viking notes as its village the generator can stand, and how far from a house's
		/// middle a viking's noted home can be and still be that house, in meters
		/// </summary>
		private const float FarmReach = 16f;
		private const float HomeMatch = 1f;

		/// <summary>
		/// The most deaths a village keeps waiting
		/// </summary>
		private const int MaxDeaths = 64;

		/// <summary>
		/// A death waiting for a viking to come back in its place
		/// </summary>
		internal struct Death
		{
			public long Time;
			public bool ByPlayer;
		}

		/// <summary>
		/// A village's vikings not adding up to its head count - since when, and whether there were too few or too many
		/// </summary>
		private struct Mismatch
		{
			public float Since;
			public int Sign;
		}

		private static readonly int PlayerPrefabHash = "Player".GetStableHashCode();

		private static int sightMask;
		private static float nextZoneCheck;
		private static float nextCheck;

		/// <summary>
		/// When each zone around a loaded village was found loaded, by Time.time
		/// </summary>
		private static readonly Dictionary<Vector2s, float> zoneLoadedSince = new Dictionary<Vector2s, float>();

		/// <summary>
		/// The zones this client generated in the world being played (see NoteGenerated)
		/// </summary>
		private static readonly HashSet<Vector2s> generatedHere = new HashSet<Vector2s>();

		/// <summary>
		/// The villages whose vikings don't add up to their head count
		/// </summary>
		private static readonly Dictionary<ZDOID, Mismatch> mismatches = new Dictionary<ZDOID, Mismatch>();

		/// <summary>
		/// Notes which zones around the loaded villages are loaded every ZoneInterval seconds, and looks after the
		/// villages this client owns every CheckInterval seconds, or SetupInterval while one is still being set up or
		/// moved into - noting their houses, records and head counts, putting
		/// right the deaths they're missing and bringing back the vikings they're due - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (ZNetScene.instance == null || ZoneSystem.instance == null || ZNet.instance == null || ZDOMan.instance == null || !ServerCheck.Active)
			{
				return;
			}
			if (Time.time >= nextZoneCheck)
			{
				nextZoneCheck = Time.time + ZoneInterval;
				TrackZones();
			}
			if (Time.time < nextCheck)
			{
				return;
			}
			// While a village is still being set up, it's looked after every SetupInterval, so each step follows the last
			// without waiting out CheckInterval
			bool settingUp = false;
			foreach (Village village in new List<Village>(Village.Loaded()))
			{
				if (!village.IsOwner())
				{
					continue;
				}
				if (village.NeedsAllHouses())
				{
					RecordAllHouses(village);
					settingUp = true;
					continue;
				}
				// Nothing is set up and no viking comes until the bonfire is built, since a farm with no room for one stays
				// abandoned. A village a player started has no houses until its beds bring them
				if (village.Houses.Count == 0 && !village.IsFounded())
				{
					continue;
				}
				if (!VillagePaths.Laid(village.Zdo))
				{
					settingUp = true;
					continue;
				}
				VillageNames.TryAutoName(village);
				Timed("supporting pieces", village, () => FarmSupport.TrySupport(village));
				bool settled = Settled(village);
				bool noted = VillageRecords.IsNoted(village);
				settingUp |= (!settled || !noted);
				if (settled)
				{
					// Furnished first, so the signs, beds, furniture, cooking stations and sitting logs are noted among the
					// village's pieces
					Timed("putting up signs", village, () => VillageSigns.TrySwap(village));
					Timed("making beds real", village, () => VillageBeds.TryMakeReal(village));
					Timed("furnishing", village, () => VillageFurnishing.TryFurnish(village));
					Timed("placing cooking stations", village, () => VillageCooking.TryPlace(village));
					Timed("placing the cauldron", village, () => VillageCooking.TryPlaceCauldron(village));
					Timed("placing sitting logs", village, () => VillageSeats.TryPlace(village));
					Timed("moving sitting logs", village, () => VillageSeats.TryMove(village));
					Timed("noting pieces", village, () => VillageRecords.TrySnapshot(village));
					if (!noted && VillageRecords.IsNoted(village))
					{
						LogSetUp(village, "set up");
					}
					// After the pieces are noted, so the boat is never one of the village's own pieces
					if (VillageRecords.IsNoted(village))
					{
						Timed("mooring a boat", village, () => VillageShoreBoat.TryPlace(village));
					}
				}
				if (!EnsureHeadCount(village, settled) || !settled)
				{
					continue;
				}
				List<ZDO> vikings = Census(village);
				if (!Populated(village, vikings))
				{
					settingUp = true;
					continue;
				}
				VillageBeds.Update(village, vikings);
				VillageBeds.AssignBeds(village, vikings);
				VillageTravel.Arrive(village, vikings);
				CheckCount(village, vikings);
				TryRespawn(village, vikings);
			}
			nextCheck = Time.time + (settingUp ? SetupInterval : CheckInterval);
		}

		/// <summary>
		/// Runs a step of setting a village up, writing to the log how long it took when it took TimingLogMin or longer,
		/// so a step that holds up the game can be found
		/// </summary>
		/// <param name="step">What the step does, like "furnishing"</param>
		/// <param name="village">The village</param>
		/// <param name="action">The step</param>
		public static void Timed(string step, Village village, System.Action action)
		{
			System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
			action();
			watch.Stop();
			if (watch.Elapsed.TotalMilliseconds >= TimingLogMin)
			{
				Plugin.Log.LogInfo("Timing: " + step + " in the village at " + village.Farm.ToString("F0") + " took " + watch.Elapsed.TotalMilliseconds.ToString("F0") + "ms");
			}
		}

		/// <summary>
		/// Writes to the log how long after its farm was generated on this client a village reached a step of being set
		/// up, when it was generated here
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="step">The step reached, like "set up"</param>
		public static void LogSetUp(Village village, string step)
		{
			float since = VillagePaths.SinceGenerated(village.Farm);
			if (since >= 0f)
			{
				Plugin.Log.LogInfo("Timing: the village at " + village.Farm.ToString("F0") + " was " + step + " " + since.ToString("F1") + "s after its farm was generated");
			}
		}

		/// <summary>
		/// Gives a village the vikings it starts with that it hasn't had yet, once its bonfire is built - every viking of
		/// its core, since none are spawned as the farm is generated, when there may be no room for its bonfire and the
		/// houses close by its farmhouses aren't known yet. They come at once, in the village, on open ground beside its
		/// core's houses (see Village.SpawnSpot), whether or not a player can see them, rather than waiting their turn
		/// like a viking that died. The animals its pens spawned are tamed first, so the vikings never see them as wild
		/// ones to fight
		/// </summary>
		/// <param name="village">The village, whose generator this client owns, with everything across it loaded</param>
		/// <param name="vikings">Its living vikings' ZDOs, added to as they come</param>
		/// <returns>True once the village has had every viking it starts with</returns>
		private static bool Populated(Village village, List<ZDO> vikings)
		{
			ZDO zdo = village.Zdo;
			if (zdo.GetBool(PopulatedKey))
			{
				return(true);
			}
			int heads = zdo.GetInt(HeadCountKey);
			if (vikings.Count < heads && VillageRecords.StandingCore(village).Count == 0)
			{
				return(false);
			}
			if (vikings.Count < heads)
			{
				VillageLivestock.TamePens(village);
			}
			while (vikings.Count < heads)
			{
				if (!village.SpawnSpot(out Vector3 spot))
				{
					return(false);
				}
				ZDO spawned = SpawnVillager(village, vikings, spot, null);
				if (spawned == null)
				{
					return(false);
				}
				vikings.Add(spawned);
			}
			zdo.Set(PopulatedKey, true);
			return(true);
		}

		/// <summary>
		/// Describes a village's head count and the vikings it's waiting to get back, for the debug hover text of its
		/// fire - with its core houses only while some are down, and its respawns only while it's waiting for any
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="text">The text, added to a line at a time</param>
		public static void Describe(Village village, System.Text.StringBuilder text)
		{
			ZDO zdo = village.Zdo;
			text.Append("Head count: " + zdo.GetInt(HeadCountKey) + ", planned " + PlannedTotal(village) + (zdo.GetBool(PopulatedKey) ? ", populated" : (VillagePaths.Laid(zdo) ? ", not populated yet" : ", not populated until its bonfire is built")) + (Settled(village) ? "" : ", still loading") + ((VillageDeparted.Returning(village) > 0) ? ", " + VillageDeparted.Returning(village) + " that left coming back" : "") + "\n");
			int standing = VillageRecords.StandingCore(village).Count;
			if (standing < village.CoreHouses.Count)
			{
				text.Append("Core houses standing: " + standing + "/" + village.CoreHouses.Count + "\n");
			}
			List<Death> deaths = ReadDeaths(zdo);
			if (deaths.Count == 0 || EnvMan.instance == null)
			{
				return;
			}
			if (standing == 0)
			{
				text.Append("Respawns: " + deaths.Count + " waiting, none until a core house is rebuilt\n");
				return;
			}
			int byPlayers = deaths.FindAll((Death death) => death.ByPlayer).Count;
			long due = DueAt(zdo.GetLong(LastRespawnKey, 0L), deaths[0], EnvMan.instance.m_dayLengthSec);
			float hours = (float)(due - NetTime.Now()) / System.TimeSpan.TicksPerSecond / EnvMan.instance.m_dayLengthSec * 24f;
			string when = ((hours > 0f) ? "next in " + hours.ToString("F1") + " game hours" : (EnemyHoldsBack(village, village.FindEnemy(EnemyReach), false) ? "next due, waiting for enemies to leave" : "next due"));
			text.Append("Respawns: " + deaths.Count + " waiting" + ((byPlayers > 0) ? " (" + byPlayers + " killed by players)" : "") + ", " + when + "\n");
		}

		/// <summary>
		/// Registers the routed RPC that reports living vikings' deaths
		/// </summary>
		public static void RegisterRpc()
		{
			ZRoutedRpc.instance?.Register<Vector3, long, bool>(DeathRpc, OnDeathReported);
		}

		/// <summary>
		/// Works out how many living vikings a house is planned to hold, the same every time for the same house
		/// </summary>
		/// <remarks>
		/// The number comes from where the house stands and the world's seed, so the village's head count can be added
		/// up from its houses whichever order they're generated in
		/// </remarks>
		/// <param name="center">The house's middle</param>
		/// <returns>How many vikings it holds</returns>
		public static int PlannedHeads(Vector3 center)
		{
			int seed = ((WorldGenerator.instance != null) ? WorldGenerator.instance.GetSeed() : 0);
			int hash = unchecked((Mathf.RoundToInt(center.x) * 73856093) ^ (Mathf.RoundToInt(center.z) * 19349663) ^ (seed * 83492791));
			uint mixed = unchecked((uint)hash * 2654435761u);
			return(HouseHeadsMin + (int)((mixed >> 16) % (uint)(HouseHeadsMax - HouseHeadsMin + 1)));
		}

		/// <summary>
		/// Notes in a farm's generator, as it's generated, that its houses were given their planned number of vikings
		/// </summary>
		/// <param name="farm">The farm's planned spot</param>
		public static void MarkPlanned(Vector3 farm)
		{
			ZDO generator = FindGenerator(farm);
			if (generator == null)
			{
				Plugin.Log.LogWarning("Couldn't find the generator of the farm at " + farm.ToString("F0") + " to note its head count");
				return;
			}
			generator.Set(PlannedKey, true);
		}

		/// <summary>
		/// Reports a living viking's death to every client as it dies on its owner, so the owner of its village's
		/// generator can note it
		/// </summary>
		/// <param name="character">The dying creature</param>
		public static void OnVikingDeath(Character character)
		{
			if (character == null || !VikingCore.IsLivingViking(character) || !character.m_nview.IsOwner() || ZRoutedRpc.instance == null)
			{
				return;
			}
			if (!FarmOf(character, out Vector3 farm))
			{
				return;
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, DeathRpc, farm, NetTime.Now(), KilledByPlayer(character));
			NeighborNews.Report(NeighborEvent.Death, farm, VillageNames.Get(Village.ForFarm(farm)), VikingNames.Get(character));
		}

		/// <summary>
		/// Finds where the farm of a living viking's village stands - the village it notes, or else the one it's in. One
		/// leaving a disbanded village or on the road to another belongs to none, even passing through one
		/// </summary>
		/// <param name="character">The viking</param>
		/// <param name="farm">Where the farm stands</param>
		/// <returns>False when it belongs to no village</returns>
		public static bool FarmOf(Character character, out Vector3 farm)
		{
			ZDO zdo = character.m_nview.GetZDO();
			if (zdo.GetBool(VillageFounding.LeavingKey) || VillageTravel.OnRoad(zdo))
			{
				farm = Vector3.zero;
				return(false);
			}
			farm = zdo.GetVec3(Village.VillageKey, Vector3.zero);
			if (farm != Vector3.zero)
			{
				return(true);
			}
			Village village = Village.Find(character.transform.position);
			if (village == null || !village.Contains(character.transform.position))
			{
				return(false);
			}
			farm = village.Farm;
			return(true);
		}

		/// <summary>
		/// Checks whether a player dealt a creature's last hit
		/// </summary>
		/// <param name="character">The creature</param>
		/// <returns>True when a player did</returns>
		private static bool KilledByPlayer(Character character)
		{
			HitData hit = character.m_lastHit;
			if (hit == null)
			{
				return(false);
			}
			Character attacker = hit.GetAttacker();
			if (attacker != null)
			{
				return(attacker.IsPlayer());
			}
			ZDO zdo = (hit.m_attacker.IsNone() ? null : ZDOMan.instance.GetZDO(hit.m_attacker));
			return(zdo != null && zdo.GetPrefab() == PlayerPrefabHash);
		}

		/// <summary>
		/// Notes a living viking's death in its village's generator when this client owns it - or, on the server, when
		/// no one does, taking it over to note the death
		/// </summary>
		/// <param name="sender">The client the viking died on</param>
		/// <param name="farm">Where the viking's village's farm stands</param>
		/// <param name="time">When it died, in ZNet time ticks</param>
		/// <param name="byPlayer">Whether a player killed it</param>
		private static void OnDeathReported(long sender, Vector3 farm, long time, bool byPlayer)
		{
			ZDO generator = FindGenerator(farm);
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
			List<Death> deaths = ReadDeaths(generator);
			Death death = new Death();
			death.Time = time;
			death.ByPlayer = byPlayer;
			InsertByTime(deaths, death);
			WriteDeaths(generator, deaths);
			Plugin.Log.LogInfo("A living viking of the village at " + farm.ToString("F0") + " died" + (byPlayer ? " by a player's hand" : "") + ", " + deaths.Count + " waiting to come back");
		}

		/// <summary>
		/// Adds a death to a village's waiting deaths in order of when they happened, after those that happened at the
		/// same time
		/// </summary>
		/// <param name="deaths">The deaths, earliest first, added to</param>
		/// <param name="death">The death</param>
		internal static void InsertByTime(List<Death> deaths, Death death)
		{
			int index = deaths.Count;
			while (index > 0 && deaths[index - 1].Time > death.Time)
			{
				index--;
			}
			deaths.Insert(index, death);
		}

		/// <summary>
		/// Finds the generator of a farm in the ZDOs this client has - or the anchor of a village a player started -
		/// nearest to a point
		/// </summary>
		/// <param name="farm">Where the generator stands, or the farm's planned spot</param>
		/// <returns>The generator's ZDO, or null</returns>
		public static ZDO FindGenerator(Vector3 farm)
		{
			List<ZDO> zdos = new List<ZDO>();
			ZdosNear(farm, FarmReach, zdos);
			ZDO nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (ZDO zdo in zdos)
			{
				if (zdo.GetPrefab() != VillagePaths.GeneratorHash && !VillageFounding.IsAnchor(zdo.GetPrefab()))
				{
					continue;
				}
				float distance = Utils.DistanceXZ(zdo.GetPosition(), farm);
				if (distance <= FarmReach && distance < nearestDistance)
				{
					nearest = zdo;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Adds the ZDOs this client has in every zone within a distance of a point to a list
		/// </summary>
		/// <param name="center">The point</param>
		/// <param name="reach">How far from it to look, in meters</param>
		/// <param name="found">The list, added to</param>
		public static void ZdosNear(Vector3 center, float reach, List<ZDO> found)
		{
			if (ZDOMan.instance == null || ZDOMan.instance.m_objectsBySector == null)
			{
				return;
			}
			Vector2s min = ZoneSystem.GetZone(center - new Vector3(reach, 0f, reach));
			Vector2s max = ZoneSystem.GetZone(center + new Vector3(reach, 0f, reach));
			for (int x = min.x; x <= max.x; x++)
			{
				for (int y = min.y; y <= max.y; y++)
				{
					uint sector = ZoneSystem.SectorToIndex(x, y).Sector;
					if (sector < ZDOMan.instance.m_objectsBySector.Length && ZDOMan.instance.m_objectsBySector[sector] != null)
					{
						found.AddRange(ZDOMan.instance.m_objectsBySector[sector]);
					}
				}
			}
		}

		/// <summary>
		/// Notes when each zone across the loaded villages was found loaded, forgetting the zones that aren't
		/// </summary>
		private static void TrackZones()
		{
			Dictionary<Vector2s, float> loaded = new Dictionary<Vector2s, float>();
			foreach (Village village in new List<Village>(Village.Loaded()))
			{
				foreach (Vector2s zone in Zones(village))
				{
					if (loaded.ContainsKey(zone) || !ZoneSystem.instance.IsZoneLoaded(zone))
					{
						continue;
					}
					loaded[zone] = (zoneLoadedSince.TryGetValue(zone, out float since) ? since : Time.time);
				}
			}
			zoneLoadedSince.Clear();
			foreach (KeyValuePair<Vector2s, float> zone in loaded)
			{
				zoneLoadedSince[zone.Key] = zone.Value;
			}
		}

		/// <summary>
		/// Forgets the zones and head count mismatches noted in the world being left
		/// </summary>
		public static void Forget()
		{
			zoneLoadedSince.Clear();
			generatedHere.Clear();
			FarmSupport.Forget();
			VillageSnow.Forget();
			mismatches.Clear();
		}

		/// <summary>
		/// Lists the zones a village reaches across
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The zones</returns>
		private static List<Vector2s> Zones(Village village)
		{
			List<Vector2s> zones = new List<Vector2s>();
			Vector2s min = ZoneSystem.GetZone(village.Middle - new Vector3(village.Radius, 0f, village.Radius));
			Vector2s max = ZoneSystem.GetZone(village.Middle + new Vector3(village.Radius, 0f, village.Radius));
			for (int x = min.x; x <= max.x; x++)
			{
				for (int y = min.y; y <= max.y; y++)
				{
					zones.Add(new Vector2s(x, y));
				}
			}
			return(zones);
		}

		/// <summary>
		/// Checks whether the zone a point is in has been loaded long enough for everything in it to have arrived
		/// </summary>
		/// <remarks>
		/// Only the zones a loaded village reaches across are kept track of
		/// </remarks>
		/// <param name="point">The point</param>
		/// <returns>True when it has</returns>
		public static bool ZoneSettled(Vector3 point)
		{
			Vector2s zone = ZoneSystem.GetZone(point);
			if (!ZoneSystem.instance.IsZoneLoaded(zone))
			{
				return(false);
			}
			// A zone this client generated was filled in all at once, so there's nothing more to arrive
			return(generatedHere.Contains(zone) || (zoneLoadedSince.TryGetValue(zone, out float since) && Time.time - since >= SettleTime));
		}

		/// <summary>
		/// Notes a zone this client just generated, with everything in it placed at once, so it counts as settled right
		/// away (see ZoneSettled), and looks after the villages again soon, since one may have just been generated
		/// </summary>
		/// <param name="zone">The zone</param>
		public static void NoteGenerated(Vector2s zone)
		{
			generatedHere.Add(zone);
			CheckSoon();
		}

		/// <summary>
		/// Looks after the villages this client owns on the next update, rather than waiting out CheckInterval, as one
		/// moves on to its next step of being set up
		/// </summary>
		public static void CheckSoon()
		{
			nextCheck = 0f;
		}

		/// <summary>
		/// Checks whether every zone a village reaches across has been loaded long enough for everything in it to have
		/// arrived
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>True when they all have</returns>
		private static bool Settled(Village village)
		{
			foreach (Vector2s zone in Zones(village))
			{
				if (!ZoneSettled(ZoneSystem.GetZonePos(zone)))
				{
					return(false);
				}
			}
			return(true);
		}

		/// <summary>
		/// Notes every house of a village whose generator only notes its farmhouses, once the houses near it have all
		/// been generated
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		private static void RecordAllHouses(Village village)
		{
			byte[] bytes = village.Zdo.GetByteArray(VillagePaths.HousesKey);
			if (bytes == null)
			{
				return;
			}
			List<VillagePaths.House> houses = VillagePaths.ReadHouses(bytes);
			if (VillageLocations.FindOutposts(village.Zdo, village.Farm, houses))
			{
				Village.RecordHouses(village.Zdo, houses);
			}
		}

		/// <summary>
		/// Notes a village's head count in its generator when it has none yet - added up from its houses' planned
		/// numbers for a village generated with them, or else counted from the vikings it has, at least one for each
		/// house - and raises a village's head count to its houses' planned numbers when it's lower, so the vikings it's
		/// short are brought back. A village a player started only has the vikings its beds bring, none to begin with
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		/// <param name="settled">Whether everything across the village has loaded, so its vikings can be counted</param>
		/// <returns>True when the village has a head count</returns>
		private static bool EnsureHeadCount(Village village, bool settled)
		{
			if (village.IsFounded())
			{
				return(true);
			}
			ZDO zdo = village.Zdo;
			int noted = zdo.GetInt(HeadCountKey);
			if (noted > 0)
			{
				int planned = (zdo.GetBool(PlannedKey) ? PlannedTotal(village) : 0);
				if (planned > noted)
				{
					zdo.Set(HeadCountKey, planned);
					Plugin.Log.LogInfo("The village at " + village.Farm.ToString("F0") + " now has " + planned + " living vikings, up from " + noted);
				}
				return(true);
			}
			int heads;
			if (zdo.GetBool(PlannedKey))
			{
				heads = PlannedTotal(village);
			}
			else if (settled)
			{
				heads = CountedTotal(village);
			}
			else
			{
				return(false);
			}
			heads = Mathf.Max(1, heads);
			zdo.Set(HeadCountKey, heads);
			Plugin.Log.LogInfo("The village at " + village.Farm.ToString("F0") + " has " + heads + " living vikings");
			return(true);
		}

		/// <summary>
		/// Adds up the planned numbers of vikings of the houses of a village's core, where its vikings live - its
		/// farmhouses, and the houses close by them, leaving out the outposts and the houses added for players' beds,
		/// whose vikings the beds bring (see VillageBeds)
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>Its head count</returns>
		private static int PlannedTotal(Village village)
		{
			int heads = 0;
			foreach (VillagePaths.House house in village.CoreHouses)
			{
				if (!VillageBeds.IsAdded(village, house))
				{
					heads += PlannedHeads(house.Center);
				}
			}
			return(heads);
		}

		/// <summary>
		/// Counts the living vikings of a village generated without planned numbers, at least one for each house
		/// </summary>
		/// <param name="village">The village, with everything across it loaded</param>
		/// <returns>Its head count</returns>
		private static int CountedTotal(Village village)
		{
			List<ZDO> vikings = Census(village);
			int heads = 0;
			int housed = 0;
			foreach (VillagePaths.House house in village.Houses)
			{
				int claims = Claims(house, vikings);
				housed += claims;
				heads += Mathf.Max(1, claims);
			}
			return(heads + Mathf.Max(0, vikings.Count - housed));
		}

		/// <summary>
		/// Lists the living vikings this client has that belong to a village - those that note it as their village, and
		/// those inside it that note none, which are taken in when this client owns them
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The vikings' ZDOs</returns>
		private static List<ZDO> Census(Village village)
		{
			List<ZDO> zdos = new List<ZDO>();
			ZdosNear(village.Middle, village.Radius + StrayReach, zdos);
			List<ZDO> vikings = new List<ZDO>();
			List<ZDO> homeless = new List<ZDO>();
			foreach (ZDO zdo in zdos)
			{
				if (zdo.GetPrefab() != VikingCore.LivingPrefabHash || zdo.GetBool(VillageFounding.LeavingKey))
				{
					continue;
				}
				Vector3 farm = zdo.GetVec3(Village.VillageKey, Vector3.zero);
				if (farm != Vector3.zero)
				{
					if (Utils.DistanceXZ(farm, village.Farm) <= FarmReach)
					{
						vikings.Add(zdo);
					}
				}
				else if (village.Contains(zdo.GetPosition()))
				{
					homeless.Add(zdo);
				}
			}
			// One on its way to another village passes through, and one that comes before the village has every viking it
			// starts with waits outside, so it doesn't take a new viking's place
			homeless.RemoveAll((ZDO zdo) => !VillageTravel.Bound(zdo, village) || !VillageTravel.Takes(village));
			foreach (ZDO zdo in homeless)
			{
				// A village a player started has no house to give one until its beds bring them
				if (zdo.IsOwner() && village.Houses.Count > 0)
				{
					village.Adopt(zdo, FewestClaimed(village, vikings));
				}
				vikings.Add(zdo);
			}
			return(vikings);
		}

		/// <summary>
		/// Counts the vikings that call a house home
		/// </summary>
		/// <param name="house">The house</param>
		/// <param name="vikings">The vikings' ZDOs</param>
		/// <returns>How many do</returns>
		private static int Claims(VillagePaths.House house, List<ZDO> vikings)
		{
			int claims = 0;
			foreach (ZDO zdo in vikings)
			{
				Vector3 home = zdo.GetVec3(Village.HomeKey, Vector3.zero);
				if (home != Vector3.zero && Utils.DistanceXZ(home, house.Center) <= HomeMatch)
				{
					claims++;
				}
			}
			return(claims);
		}

		/// <summary>
		/// Finds the house of a village's core the fewest vikings call home, among those still standing when any are -
		/// the vikings don't live in its outposts
		/// </summary>
		/// <param name="village">The village, with at least one house</param>
		/// <param name="vikings">Its vikings' ZDOs</param>
		/// <returns>The house</returns>
		private static VillagePaths.House FewestClaimed(Village village, List<ZDO> vikings)
		{
			List<VillagePaths.House> standing = VillageRecords.StandingCore(village);
			List<VillagePaths.House> homes = ((standing.Count > 0) ? standing : ((village.CoreHouses.Count > 0) ? village.CoreHouses : village.Houses));
			VillagePaths.House best = homes[0];
			int bestClaims = int.MaxValue;
			foreach (VillagePaths.House house in homes)
			{
				int claims = Claims(house, vikings);
				if (claims < bestClaims)
				{
					best = house;
					bestClaims = claims;
				}
			}
			return(best);
		}

		/// <summary>
		/// Puts right a village whose vikings and waiting deaths have kept not adding up to its head count for
		/// MismatchTime - noting a death now, not by a player, for each viking gone missing, or forgetting the latest
		/// deaths when there are too many. Its vikings out on a quest count as its own (see VillageQuests.AwayCount)
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		/// <param name="vikings">Its living vikings' ZDOs</param>
		private static void CheckCount(Village village, List<ZDO> vikings)
		{
			ZDOID id = village.Zdo.m_uid;
			List<Death> deaths = ReadDeaths(village.Zdo);
			int gap = village.Zdo.GetInt(HeadCountKey) - vikings.Count - deaths.Count - VillageQuests.AwayCount(village, vikings);
			if (gap == 0)
			{
				mismatches.Remove(id);
				return;
			}
			int sign = ((gap > 0) ? 1 : -1);
			if (!mismatches.TryGetValue(id, out Mismatch mismatch) || mismatch.Sign != sign)
			{
				mismatch = new Mismatch();
				mismatch.Since = Time.time;
				mismatch.Sign = sign;
				mismatches[id] = mismatch;
				return;
			}
			if (Time.time - mismatch.Since < MismatchTime)
			{
				return;
			}
			mismatches.Remove(id);
			Reconcile(deaths, gap, NetTime.Now());
			if (gap > 0)
			{
				Plugin.Log.LogInfo("The village at " + village.Farm.ToString("F0") + " is missing " + gap + " living vikings");
			}
			WriteDeaths(village.Zdo, deaths);
		}

		/// <summary>
		/// Puts right a village's waiting deaths for how far its vikings and deaths fall short of its head count -
		/// noting a death now, not by a player, for each one short, or forgetting the latest deaths for each one over, as
		/// many as there are
		/// </summary>
		/// <param name="deaths">The deaths, earliest first, changed</param>
		/// <param name="gap">How many the vikings and deaths fall short of the head count, or less than 0 for how many
		/// over</param>
		/// <param name="now">The time now, in ZNet time ticks</param>
		internal static void Reconcile(List<Death> deaths, int gap, long now)
		{
			if (gap > 0)
			{
				for (int i = 0; i < gap; i++)
				{
					Death death = new Death();
					death.Time = now;
					death.ByPlayer = false;
					deaths.Add(death);
				}
			}
			else
			{
				deaths.RemoveRange(Mathf.Max(0, deaths.Count + gap), Mathf.Min(deaths.Count, -gap));
			}
		}

		/// <summary>
		/// Brings back a village's viking for its earliest waiting death once it's due, when no enemy is near and there's
		/// a spot out of every player's sight - a new viking that calls the house the fewest vikings call home its home
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		/// <param name="vikings">Its living vikings' ZDOs</param>
		private static void TryRespawn(Village village, List<ZDO> vikings)
		{
			List<Death> deaths = ReadDeaths(village.Zdo);
			if (deaths.Count == 0 || EnvMan.instance == null)
			{
				return;
			}
			// A village with none of its core standing gets no one back until a house of it is rebuilt, and its vikings out
			// on a quest are still its own
			if (vikings.Count + VillageQuests.AwayCount(village, vikings) >= village.Zdo.GetInt(HeadCountKey) || VillageRecords.StandingCore(village).Count == 0)
			{
				return;
			}
			long due = DueAt(village.Zdo.GetLong(LastRespawnKey, 0L), deaths[0], EnvMan.instance.m_dayLengthSec);
			if (NetTime.Now() < due || !ReadyToArrive(village, out Vector3 spot, out bool outside, out string _))
			{
				return;
			}
			ZDO spawned = SpawnVillager(village, vikings, spot, null);
			if (spawned == null)
			{
				return;
			}
			spawned.Set(VillageLife.ArrivingKey, outside);
			deaths.RemoveAt(0);
			WriteDeaths(village.Zdo, deaths);
			village.Zdo.Set(LastRespawnKey, NetTime.Now());
		}

		/// <summary>
		/// Works out when a viking is due back for a death - RespawnDays after the death or after the last viking came
		/// back, whichever is later, or PlayerKillDays when a player killed it
		/// </summary>
		/// <param name="lastRespawn">When the last viking came back, in ZNet time ticks, or 0 when none has</param>
		/// <param name="next">The death</param>
		/// <param name="dayLength">How long a game day is, in seconds</param>
		/// <returns>When it's due, in ZNet time ticks</returns>
		internal static long DueAt(long lastRespawn, Death next, long dayLength)
		{
			long delay = NetTime.Ticks(dayLength * (next.ByPlayer ? PlayerKillDays : RespawnDays));
			return(System.Math.Max(lastRespawn, next.Time) + delay);
		}

		/// <summary>
		/// Brings a new living viking to a village to call a house home, when no enemy is near and there's a spot out of
		/// every player's sight for it to walk in from (see FindArrivalSpot)
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		/// <param name="vikings">Its living vikings' ZDOs, added to</param>
		/// <param name="home">The house it calls home</param>
		/// <param name="why">Set to why it couldn't come yet, for the debug hover text</param>
		/// <returns>Its ZDO, or null when it couldn't come yet</returns>
		public static ZDO Invite(Village village, List<ZDO> vikings, VillagePaths.House home, out string why)
		{
			if (!ReadyToArrive(village, out Vector3 spot, out bool outside, out why))
			{
				return(null);
			}
			ZDO zdo = SpawnVillager(village, vikings, spot, home);
			if (zdo != null)
			{
				zdo.Set(VillageLife.ArrivingKey, outside);
				vikings.Add(zdo);
			}
			return(zdo);
		}

		/// <summary>
		/// Checks whether an enemy in or near a village keeps vikings from coming to it - while a raid is on, or for
		/// EnemyPatience after the wait for it to leave started (see WaitStart) - noting the wait when asked to, and
		/// forgetting it once there's no enemy
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="enemy">The enemy in the village or within EnemyReach of it, or null</param>
		/// <param name="note">Whether to note the wait, which only a viking due to come does, so describing the village
		/// leaves it as it was</param>
		/// <returns>True when it does</returns>
		private static bool EnemyHoldsBack(Village village, Character enemy, bool note)
		{
			ZDOID id = village.Zdo.m_uid;
			if (enemy == null)
			{
				if (note)
				{
					ForgetEnemyWait(id);
				}
				return(false);
			}
			float since = 0f;
			float lastSeen = 0f;
			bool known = (enemySince.TryGetValue(id, out since) && enemyLastSeen.TryGetValue(id, out lastSeen));
			since = WaitStart(known, since, lastSeen, Time.time);
			if (note)
			{
				enemySince[id] = since;
				enemyLastSeen[id] = Time.time;
			}
			return(HoldsBack(since, Time.time, VillageRaids.On(village)));
		}

		/// <summary>
		/// Works out since when vikings have waited for enemies to leave a village - since the wait already noted, when
		/// an enemy was last found there no more than EnemyWaitGap ago, or else from now, so a wait broken off by a
		/// viking coming, the village unloading or no viking being due starts over
		/// </summary>
		/// <param name="known">Whether a wait is noted for the village</param>
		/// <param name="since">When the noted wait started, by Time.time</param>
		/// <param name="lastSeen">When an enemy was last found there, by Time.time</param>
		/// <param name="now">The time now, by Time.time</param>
		/// <returns>When the wait started, by Time.time</returns>
		internal static float WaitStart(bool known, float since, float lastSeen, float now)
		{
			return((known && now - lastSeen <= EnemyWaitGap) ? since : now);
		}

		/// <summary>
		/// Checks whether an enemy keeps vikings from coming to a village - while a raid is on, or until it has held them
		/// back for EnemyPatience
		/// </summary>
		/// <param name="since">When the wait for it to leave started, by Time.time</param>
		/// <param name="now">The time now, by Time.time</param>
		/// <param name="raid">Whether a raid is on</param>
		/// <returns>True when it does</returns>
		internal static bool HoldsBack(float since, float now, bool raid)
		{
			return(raid || now - since < EnemyPatience);
		}

		/// <summary>
		/// Forgets a village's wait for enemies to leave, once there's no enemy or a viking has come
		/// </summary>
		/// <param name="id">The village's generator's ZDO</param>
		private static void ForgetEnemyWait(ZDOID id)
		{
			enemySince.Remove(id);
			enemyLastSeen.Remove(id);
		}

		/// <summary>
		/// Checks whether a viking can come to a village now - with no enemy in it or within EnemyReach of its edge, or
		/// none for longer than EnemyPatience with no raid on (see EnemyHoldsBack), and a spot to come from (see
		/// FindArrivalSpot)
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="spot">Where it would come from</param>
		/// <param name="outside">Whether that's outside the fence</param>
		/// <param name="why">Set to why it can't, for the debug hover text, or "" when it can</param>
		/// <returns>True when it can</returns>
		private static bool ReadyToArrive(Village village, out Vector3 spot, out bool outside, out string why)
		{
			return(ReadyToArrive(village, Vector3.zero, out spot, out outside, out why));
		}

		/// <summary>
		/// Checks whether a viking can come to a village now, from the side nearest a point when it can (see
		/// FindArrivalSpot)
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="toward">The point it comes from, or zero for any side</param>
		/// <param name="spot">Where it would come from</param>
		/// <param name="outside">Whether that's outside the fence</param>
		/// <param name="why">Set to why it can't, for the debug hover text, or "" when it can</param>
		/// <returns>True when it can</returns>
		private static bool ReadyToArrive(Village village, Vector3 toward, out Vector3 spot, out bool outside, out string why)
		{
			spot = Vector3.zero;
			outside = false;
			Character enemy = village.FindEnemy(EnemyReach);
			if (EnemyHoldsBack(village, enemy, true))
			{
				why = "a hostile " + Utils.GetPrefabName(enemy.gameObject) + " in the village or within " + EnemyReach.ToString("F0") + "m of it, at " + enemy.transform.position.ToString("F0");
				return(false);
			}
			if (!FindArrivalSpot(village, toward, out spot, out outside))
			{
				why = "no spot out of every player's sight and " + ArrivalClear.ToString("F0") + "m from every enemy for it to come from yet";
				return(false);
			}
			why = "";
			return(true);
		}

		/// <summary>
		/// Finds a spot out of every player's sight for a viking coming to a village to start from - outside the farm's
		/// fence, so it walks in, or beside one of the houses of its core when there's no open ground outside, like a
		/// village with nothing but water or cliffs around its fence - with no enemy within ArrivalClear of it. When
		/// there's open ground outside but a player can see every spot tried, it waits for another look - for up to
		/// SpotPatience seconds, after which any spot clear of enemies will do, near a player or in their sight. A viking
		/// coming from a point comes from the spot nearest it of those tried
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="toward">The point it comes from, or zero for any side</param>
		/// <param name="spot">The spot</param>
		/// <param name="outside">Whether it's outside the fence</param>
		/// <returns>False when none was found</returns>
		private static bool FindArrivalSpot(Village village, Vector3 toward, out Vector3 spot, out bool outside)
		{
			ZDOID id = village.Zdo.m_uid;
			// A search broken off, by the village unloading or an enemy coming near, starts over
			if (!searchingSince.TryGetValue(id, out float since) || !lastSearch.TryGetValue(id, out float last) || Time.time - last > CheckInterval * 3f)
			{
				since = Time.time;
				searchingSince[id] = since;
			}
			lastSearch[id] = Time.time;
			// A viking that has waited too long for a spot out of sight comes where a player is near or can see it
			bool anywhere = (Time.time - since >= SpotPatience);
			outside = true;
			bool open = false;
			bool found = false;
			Vector3 best = Vector3.zero;
			for (int i = 0; i < HiddenTries; i++)
			{
				if (!village.ArrivalSpot(out spot))
				{
					break;
				}
				open = true;
				if ((anywhere || Hidden(spot)) && Village.FindEnemyNear(spot, ArrivalClear) == null)
				{
					if (toward == Vector3.zero)
					{
						searchingSince.Remove(id);
						return(true);
					}
					if (!found || Utils.DistanceXZ(spot, toward) < Utils.DistanceXZ(best, toward))
					{
						best = spot;
					}
					found = true;
				}
			}
			if (found)
			{
				spot = best;
				searchingSince.Remove(id);
				return(true);
			}
			if (open)
			{
				spot = Vector3.zero;
				return(false);
			}
			outside = false;
			if (((anywhere && village.SpawnSpot(out spot)) || FindHiddenSpot(village, out spot)) && Village.FindEnemyNear(spot, ArrivalClear) == null)
			{
				searchingSince.Remove(id);
				return(true);
			}
			return(false);
		}

		/// <summary>
		/// Spawns a living viking of a village at a spot, never starred, calling a house of its core home - the one given,
		/// or else the one the fewest vikings call home - as the next viking that left a village disbanded where it
		/// stands when there's one coming back (see VillageDeparted), or else a new one, marked as an archer when the
		/// village has none, and named for its place among the village's first vikings when it's one of those it starts
		/// with (see VikingNames.FirstVikings)
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="vikings">Its living vikings' ZDOs</param>
		/// <param name="spot">Where it comes</param>
		/// <param name="chosen">The house it calls home, or null to pick one</param>
		/// <returns>Its ZDO, or null when it couldn't be spawned</returns>
		private static ZDO SpawnVillager(Village village, List<ZDO> vikings, Vector3 spot, VillagePaths.House? chosen)
		{
			GameObject prefab = ZNetScene.instance.GetPrefab(VikingCore.LivingPrefabHash);
			if (prefab == null)
			{
				return(null);
			}
			GameObject instance = Object.Instantiate(prefab, spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
			BaseAI ai = instance.GetComponent<BaseAI>();
			if (ai != null)
			{
				ai.SetPatrolPoint();
			}
			ZDO zdo = instance.GetComponent<ZNetView>().GetZDO();
			// The archer mark only gives a viking a bow as its gear is first rolled, and one coming back already has its
			// gear, so it's left to the next new viking when the village has no one with a bow
			bool returned = VillageDeparted.TakeReturning(village, zdo);
			if (!returned && !vikings.Exists((ZDO other) => VikingCore.IsArcher(other)))
			{
				zdo.Set(VikingCore.ArcherKey, true);
			}
			// The vikings a village starts with come in order, and the first of some villages' have names of their own
			if (!returned && !village.Zdo.GetBool(PopulatedKey))
			{
				VikingNames.PresetFirst(zdo, VillageNames.Typed(village), vikings.Count);
			}
			VillagePaths.House home = (chosen.HasValue ? chosen.Value : FewestClaimed(village, vikings));
			village.Adopt(zdo, home);
			ForgetEnemyWait(village.Zdo.m_uid);
			TestLog.Count("Vikings", (returned ? "came back to a village" : "came to a village"));
			Plugin.Log.LogInfo(TestLog.Name(zdo) + (returned ? " came back" : " came") + " to the village at " + village.Farm.ToString("F0") + ", calling the house at " + home.Center.ToString("F0") + " home");
			return(zdo);
		}

		/// <summary>
		/// Brings a viking that was on the road into a village as it was - walking in from the side it came from, when no
		/// enemy is near and there's a spot out of every player's sight, and calling the house the fewest vikings call home
		/// its home, the same as a stray taken in. One coming home from a quest calls its own house home again, when it
		/// still stands, and isn't new to the village - and comes back inside a village that was trapped when it left, at
		/// the spot it left from
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		/// <param name="vikings">Its living vikings' ZDOs, added to</param>
		/// <param name="snapshot">The viking's values (see VillageDeparted.SnapshotOf)</param>
		/// <param name="from">Where it comes from</param>
		/// <returns>Its ZDO, or null when it couldn't come yet</returns>
		public static ZDO SpawnArrival(Village village, List<ZDO> vikings, byte[] snapshot, Vector3 from)
		{
			if (!ArrivalSpotFor(village, snapshot, from, out Vector3 spot, out bool outside))
			{
				return(null);
			}
			return(SpawnArrivalAt(village, vikings, snapshot, spot, outside));
		}

		/// <summary>
		/// Finds where a viking that was on the road comes into a village - a spot out of every player's sight on the side
		/// it came from, when no enemy is near (see ReadyToArrive), or for one coming home from a quest of a village that
		/// was trapped when it left, the spot it left from, inside the village
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="snapshot">The viking's values (see VillageDeparted.SnapshotOf)</param>
		/// <param name="from">Where it comes from</param>
		/// <param name="spot">Set to where it comes in</param>
		/// <param name="outside">Set to whether that's outside the fence</param>
		/// <returns>False when it can't come yet</returns>
		public static bool ArrivalSpotFor(Village village, byte[] snapshot, Vector3 from, out Vector3 spot, out bool outside)
		{
			spot = Vector3.zero;
			outside = false;
			// A village a player started has no house to give one until its beds bring them
			if (village.Houses.Count == 0)
			{
				return(false);
			}
			if (VillageQuests.ComingHome(snapshot, village) && VillageQuests.TrappedSpot(village, out Vector3 inside))
			{
				spot = inside;
				return(village.FindEnemy(EnemyReach) == null);
			}
			return(ReadyToArrive(village, from, out spot, out outside, out string _));
		}

		/// <summary>
		/// Brings a viking that was on the road into a village as it was, at a spot already found for it (see
		/// ArrivalSpotFor and SpawnArrival)
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		/// <param name="vikings">Its living vikings' ZDOs, added to</param>
		/// <param name="snapshot">The viking's values (see VillageDeparted.SnapshotOf)</param>
		/// <param name="spot">Where it comes in</param>
		/// <param name="outside">Whether that's outside the fence, so it walks in</param>
		/// <returns>Its ZDO, or null when it couldn't be spawned</returns>
		public static ZDO SpawnArrivalAt(Village village, List<ZDO> vikings, byte[] snapshot, Vector3 spot, bool outside)
		{
			bool questHome = VillageQuests.ComingHome(snapshot, village);
			GameObject prefab = ZNetScene.instance.GetPrefab(VikingCore.LivingPrefabHash);
			if (prefab == null)
			{
				return(null);
			}
			GameObject instance = Object.Instantiate(prefab, spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
			BaseAI ai = instance.GetComponent<BaseAI>();
			if (ai != null)
			{
				ai.SetPatrolPoint();
			}
			ZDO zdo = instance.GetComponent<ZNetView>().GetZDO();
			if (!VillageDeparted.ApplySnapshot(snapshot, zdo))
			{
				Plugin.Log.LogWarning("Couldn't read a viking arriving from the road at the village at " + village.Farm.ToString("F0") + ", so a new one came in its place");
			}
			VillagePaths.House home = FewestClaimed(village, vikings);
			if (questHome)
			{
				// Noted as the village's already, so it isn't taken in as new to it
				zdo.Set(Village.VillageKey, village.Farm);
				Vector3 ownHome = zdo.GetVec3(VillageQuests.QuestHomeKey, Vector3.zero);
				foreach (VillagePaths.House house in VillageRecords.StandingCore(village))
				{
					if (ownHome != Vector3.zero && Utils.DistanceXZ(house.Center, ownHome) <= HomeMatch)
					{
						home = house;
					}
				}
			}
			village.Adopt(zdo, home);
			ForgetEnemyWait(village.Zdo.m_uid);
			zdo.Set(VillageLife.ArrivingKey, outside);
			vikings.Add(zdo);
			TestLog.Count("Vikings", (questHome ? "came home from a quest" : "arrived at a village from the road"));
			if (!questHome)
			{
				NeighborNews.Report(NeighborEvent.Newcomer, village.Farm, VillageNames.Get(village), VikingNames.Get(zdo));
			}
			Plugin.Log.LogInfo(TestLog.Name(zdo) + (questHome ? " came home from a quest to the village at " : " arrived from the road at the village at ") + village.Farm.ToString("F0") + ", calling the house at " + home.Center.ToString("F0") + " home");
			return(zdo);
		}

		/// <summary>
		/// Finds an open spot in a village more than PlayerClearance from every player and out of the sight of every
		/// player loaded on this client
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="spot">The spot</param>
		/// <returns>False when none was found</returns>
		private static bool FindHiddenSpot(Village village, out Vector3 spot)
		{
			for (int i = 0; i < HiddenTries; i++)
			{
				if (village.SpawnSpot(out spot) && Hidden(spot))
				{
					return(true);
				}
			}
			spot = Vector3.zero;
			return(false);
		}

		/// <summary>
		/// Checks whether a spot is more than PlayerClearance from every player - those loaded here, and on the server
		/// every client's reference position - and out of the sight of every player loaded here
		/// </summary>
		/// <param name="spot">The spot, on the ground</param>
		/// <returns>True when it is</returns>
		public static bool Hidden(Vector3 spot)
		{
			foreach (Player player in Player.GetAllPlayers())
			{
				if (player == null)
				{
					continue;
				}
				if (Vector3.Distance(player.transform.position, spot) <= PlayerClearance || Sees(player, spot))
				{
					return(false);
				}
			}
			if (ZNet.instance.IsServer())
			{
				foreach (ZNetPeer peer in ZNet.instance.GetPeers())
				{
					if (peer != null && Vector3.Distance(peer.m_refPos, spot) <= PlayerClearance)
					{
						return(false);
					}
				}
			}
			return(true);
		}

		/// <summary>
		/// Checks whether a player can see a viking standing at a spot - its legs or its head
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="spot">The spot, on the ground</param>
		/// <returns>True when nothing solid is between the player's eyes and either</returns>
		private static bool Sees(Player player, Vector3 spot)
		{
			Vector3 eye = player.GetEyePoint();
			return(!Physics.Linecast(eye, spot + Vector3.up * 0.5f, SightMask()) || !Physics.Linecast(eye, spot + Vector3.up * 1.7f, SightMask()));
		}

		/// <summary>
		/// Gets the layers a player can't see through
		/// </summary>
		/// <returns>The layer mask</returns>
		private static int SightMask()
		{
			if (sightMask == 0)
			{
				sightMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
			}
			return(sightMask);
		}

		/// <summary>
		/// Reads the deaths waiting in a village's generator, earliest first
		/// </summary>
		/// <param name="generator">The generator's ZDO</param>
		/// <returns>The deaths</returns>
		private static List<Death> ReadDeaths(ZDO generator)
		{
			byte[] bytes = generator.GetByteArray(DeathsKey);
			return((bytes != null) ? DecodeDeaths(bytes) : new List<Death>());
		}

		/// <summary>
		/// Reads the deaths waiting in a village as EncodeDeaths wrote them
		/// </summary>
		/// <param name="bytes">The bytes</param>
		/// <returns>The deaths, earliest first, or none when the bytes are cut short or don't hold deaths</returns>
		internal static List<Death> DecodeDeaths(byte[] bytes)
		{
			List<Death> deaths = new List<Death>();
			try
			{
				ZPackage pkg = new ZPackage(bytes);
				int count = pkg.ReadInt();
				for (int i = 0; i < count; i++)
				{
					Death death = new Death();
					death.Time = pkg.ReadLong();
					death.ByPlayer = pkg.ReadBool();
					deaths.Add(death);
				}
			}
			catch (System.Exception)
			{
				deaths.Clear();
			}
			return(deaths);
		}

		/// <summary>
		/// Writes the deaths waiting in a village's generator (see EncodeDeaths)
		/// </summary>
		/// <param name="generator">The generator's ZDO, owned by this client</param>
		/// <param name="deaths">The deaths, earliest first</param>
		private static void WriteDeaths(ZDO generator, List<Death> deaths)
		{
			generator.Set(DeathsKey, EncodeDeaths(deaths));
		}

		/// <summary>
		/// Writes the deaths waiting in a village, keeping the earliest MaxDeaths - their count, then when each happened
		/// and whether a player killed it
		/// </summary>
		/// <param name="deaths">The deaths, earliest first</param>
		/// <returns>The bytes</returns>
		internal static byte[] EncodeDeaths(List<Death> deaths)
		{
			int count = Mathf.Min(deaths.Count, MaxDeaths);
			ZPackage pkg = new ZPackage();
			pkg.Write(count);
			for (int i = 0; i < count; i++)
			{
				pkg.Write(deaths[i].Time);
				pkg.Write(deaths[i].ByPlayer);
			}
			return(pkg.GetArray());
		}
	}
}
