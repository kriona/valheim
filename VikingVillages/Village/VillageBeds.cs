using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The beds players put up in and around a village for the vikings - each one left unclaimed brings a new viking to
	/// the village for as long as it stands unclaimed, and one in a building of its own grows the village to take the
	/// building in
	/// </summary>
	/// <remarks>
	/// A bed a player built in a village or within GrowReach of its edge, under a roof and covered well enough for a
	/// player to claim it (the same check the bed makes), that no player has claimed brings a new viking as soon as one
	/// can come (see VillageRespawn.Invite). A bed in none of the village's houses has the building around it
	/// measured (see Measure) and added to the village's houses, so the village reaches out to take it in, and it's in
	/// the village's core when it's close enough to it (see Village.Core). The viking calls the core house the bed is in home, or else the one
	/// nearest it. The village's head count goes up by one, and the bed is noted in the village's generator, so it
	/// brings no other. Once it's gone or a player claims it, the head count goes back down - the viking stays, but
	/// isn't brought back when it dies - and a house added for it is taken off again once no bed noted in it is left,
	/// so the village shrinks back. The vikings sleep in such a bed wherever it is in the village
	/// </remarks>
	internal static class VillageBeds
	{
		/// <summary>
		/// On a bed, that it has brought a viking to its village
		/// </summary>
		public static readonly int BroughtKey = "kriona.VikingVillages.bedBroughtViking".GetStableHashCode();

		/// <summary>
		/// On a farm's generator, the beds that have brought its village a viking and still hold it there - each bed's
		/// ZDO, where it stands, and the house added to the village for it, if one was
		/// </summary>
		private static readonly int BedsKey = "kriona.VikingVillages.villageBeds".GetStableHashCode();

		/// <summary>
		/// On a bed, the living viking that sleeps in it, and on a living viking, the bed it sleeps in - a bed is the
		/// viking's while both note each other and the viking still belongs to the bed's village
		/// </summary>
		public static readonly KeyValuePair<int, int> VikingKey = ZDO.GetHashZDOID("kriona.VikingVillages.bedViking");
		public static readonly KeyValuePair<int, int> BedKey = ZDO.GetHashZDOID("kriona.VikingVillages.vikingBed");

		/// <summary>
		/// How near a prop bed a bed has to stand for the prop to be one left behind by the bed put up in its place, in
		/// meters
		/// </summary>
		private const float LeftoverReach = 0.5f;

		/// <summary>
		/// How far from where a bed was noted a bed can stand and still be it, in meters - beds are told apart by where
		/// they stand, since the game gives every ZDO a new ID each time the world loads
		/// </summary>
		private const float BedMatch = 0.5f;

		/// <summary>
		/// Whether each prefab is a prop bed, by its hash
		/// </summary>
		private static readonly Dictionary<int, bool> propBeds = new Dictionary<int, bool>();

		/// <summary>
		/// The bed a prop bed is swapped for when the game has no bed of its own kind, like a prop_bed02's piece_bed02
		/// </summary>
		private const string PlainBed = "bed";

		/// <summary>
		/// On a farm's generator, the houses added to its village for its beds, so they're taken off once their beds
		/// are all gone - written the way the village's houses are
		/// </summary>
		private static readonly int AddedHousesKey = "kriona.VikingVillages.villageAddedHouses".GetStableHashCode();

		/// <summary>
		/// The version of how the beds are written, as a negative number ahead of them, since the first way wrote their
		/// count first
		/// </summary>
		private const int FormatVersion = -2;

		/// <summary>
		/// The least share of a bed's spawn point that has to be covered for it to bring a viking, the same as for a
		/// player to claim it
		/// </summary>
		private const float MinCover = 0.8f;

		/// <summary>
		/// How far past a village's edge a bed can stand and still grow the village to take in its building, in meters
		/// </summary>
		private const float GrowReach = 30f;

		/// <summary>
		/// How far past GrowReach the debug hover text still lists beds, as too far out, in meters
		/// </summary>
		private const float DescribeMargin = 20f;

		/// <summary>
		/// How a bed's building is measured - how many level rays are cast out from the bed, how high above its floor,
		/// how far they reach, how many of them have to find a wall for it to count as a building, the smallest and
		/// largest it can be, half its width, and how much it's given past the walls found, in meters
		/// </summary>
		private const int MeasureRays = 16;
		private const float MeasureHeight = 1f;
		private const float MeasureReach = 16f;
		private const int MeasureWalls = 10;
		private const float HalfSizeMin = 2f;
		private const float HalfSizeMax = 12f;
		private const float WallMargin = 0.5f;

		/// <summary>
		/// How near two houses' middles have to be to be the same house, in meters
		/// </summary>
		private const float HouseMatch = 1f;

		/// <summary>
		/// A bed noted in a village's generator, with the house added to the village for it, if one was - found again by
		/// where it stands (see FindBed), since its ID is only good until the world next loads
		/// </summary>
		internal struct NotedBed
		{
			public ZDOID Id;
			public Vector3 Position;
			public bool Added;
			public VillagePaths.House House;
		}

		/// <summary>
		/// Why each village's last bed couldn't bring its viking, by its generator's ZDO, for the debug hover text
		/// </summary>
		private static readonly Dictionary<ZDOID, string> lastHeldUp = new Dictionary<ZDOID, string>();

		/// <summary>
		/// Whether each prefab is a bed, by its hash
		/// </summary>
		private static readonly Dictionary<int, bool> bedPrefabs = new Dictionary<int, bool>();

		private static int wallMask;

		/// <summary>
		/// The last list of beds that couldn't be read, so it's written to the log once rather than each time it's read
		/// </summary>
		private static byte[] unreadable;

		/// <summary>
		/// Lets go of the vikings of a village's beds that are gone or claimed, taking off the houses added for them,
		/// keeps the houses added for the rest among the village's houses, and brings a viking for an unclaimed bed -
		/// one at a time
		/// </summary>
		/// <param name="village">The village, whose generator this client owns, with everything across it loaded</param>
		/// <param name="vikings">Its living vikings' ZDOs, added to</param>
		public static void Update(Village village, List<ZDO> vikings)
		{
			List<NotedBed> noted = Read(village.Zdo);
			List<NotedBed> released = noted.FindAll((NotedBed bed) => Released(bed));
			if (released.Count > 0)
			{
				noted.RemoveAll((NotedBed bed) => released.Exists((NotedBed gone) => gone.Position == bed.Position));
				// A village a player started has only the vikings its beds bring, so it can go back down to none
				int heads = Mathf.Max((village.IsFounded() ? 0 : 1), village.Zdo.GetInt(VillageRespawn.HeadCountKey) - released.Count);
				village.Zdo.Set(VillageRespawn.HeadCountKey, heads);
				Write(village.Zdo, noted);
				Plugin.Log.LogInfo("The village at " + village.Farm.ToString("F0") + " lost " + released.Count + " beds for its vikings, down to " + heads + " vikings");
			}
			KeepHouses(village, noted);
			Invite(village, vikings, noted);
		}

		/// <summary>
		/// Swaps the props in a village's houses that look like beds but can't be used - a piece with "bed" in its name
		/// and no bed in it, which no player built - for the game's bed of the same kind where it stands, or else its
		/// plain wood bed, noted among the village's pieces in the prop's place. A prop left standing by a bed put up in
		/// its place is taken away instead, and the props are taken out of the village's noted pieces, so its vikings
		/// never build them back
		/// </summary>
		/// <param name="village">The village, whose generator this client owns, with everything across it loaded</param>
		public static void TryMakeReal(Village village)
		{
			if (ZNetScene.instance == null)
			{
				return;
			}
			int unnoted = VillageRecords.RemoveAll(village, IsPropBed);
			List<ZNetView> props = new List<ZNetView>();
			List<Vector3> beds = new List<Vector3>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZDO zdo = instance.Key;
				if (instance.Value == null || zdo.GetLong(ZDOVars.s_creator, 0L) != 0L || !village.InHouse(zdo.GetPosition(), 0.5f))
				{
					continue;
				}
				if (IsBedPrefab(zdo.GetPrefab()))
				{
					beds.Add(zdo.GetPosition());
				}
				else if (IsPropBed(zdo.GetPrefab()))
				{
					props.Add(instance.Value);
				}
			}
			if (unnoted > 0)
			{
				Plugin.Log.LogInfo("Took " + unnoted + " prop beds out of the noted pieces of the village at " + village.Farm.ToString("F0"));
			}
			foreach (ZNetView prop in props)
			{
				string name = Utils.GetPrefabName(prop.gameObject);
				Vector3 at = prop.transform.position;
				if (beds.Exists((Vector3 bed) => Vector3.Distance(bed, at) <= LeftoverReach))
				{
					prop.ClaimOwnership();
					ZNetScene.instance.Destroy(prop.gameObject);
					Plugin.Log.LogInfo("Took away the prop bed " + name + " at " + at.ToString("F1") + " left standing by the bed put up in its place");
					continue;
				}
				GameObject real = RealBedFor(name);
				if (real == null)
				{
					continue;
				}
				int from = prop.GetZDO().GetPrefab();
				Vector3 position = prop.transform.position;
				Quaternion rotation = prop.transform.rotation;
				prop.ClaimOwnership();
				ZNetScene.instance.Destroy(prop.gameObject);
				VillageRecords.MarkBuilt(Object.Instantiate(real, position, rotation));
				int hash = real.name.GetStableHashCode();
				VillageRecords.Swap(village, from, position, new PieceRecord { Prefab = hash, Position = position, Rotation = rotation, Wood = VillageRecords.WoodCost(hash) });
				Plugin.Log.LogInfo("Swapped the prop bed " + name + " at " + position.ToString("F1") + " for a " + real.name + " in the village at " + village.Farm.ToString("F0"));
			}
		}

		/// <summary>
		/// Checks whether a prefab is a prop bed - a piece with "bed" in its name and no bed in it - remembering the
		/// answer
		/// </summary>
		/// <param name="prefab">The prefab's hash</param>
		/// <returns>True when it is</returns>
		private static bool IsPropBed(int prefab)
		{
			if (propBeds.TryGetValue(prefab, out bool prop))
			{
				return(prop);
			}
			GameObject asset = ZNetScene.instance.GetPrefab(prefab);
			if (asset == null)
			{
				return(false);
			}
			prop = (asset.name.IndexOf("bed", System.StringComparison.OrdinalIgnoreCase) >= 0 && asset.GetComponent<Bed>() == null);
			propBeds[prefab] = prop;
			return(prop);
		}

		/// <summary>
		/// Finds the bed a prop bed is swapped for - the game's bed of its kind, named without "prop_" or with "piece_"
		/// before it, or else the plain wood bed
		/// </summary>
		/// <param name="name">The prop's prefab name</param>
		/// <returns>The bed's prefab, or null when the game has none</returns>
		private static GameObject RealBedFor(string name)
		{
			foreach (string candidate in BedCandidates(name))
			{
				GameObject prefab = ZNetScene.instance.GetPrefab(candidate);
				if (prefab != null && prefab.GetComponent<Bed>() != null)
				{
					return(prefab);
				}
			}
			return(null);
		}

		/// <summary>
		/// Lists the names of the beds a prop bed could be swapped for, in the order they're tried - its name without
		/// "prop_" before it, in any case, then that with "piece_" before it, then the plain wood bed
		/// </summary>
		/// <param name="name">The prop's prefab name</param>
		/// <returns>The names</returns>
		internal static string[] BedCandidates(string name)
		{
			string bare = (name.StartsWith("prop_", System.StringComparison.OrdinalIgnoreCase) ? name.Substring(5) : name);
			return(new[] { bare, "piece_" + bare, PlainBed });
		}

		/// <summary>
		/// Describes the beds players built in and near a village for the debug hover text of its fire - how many
		/// brought a viking, and for the rest, why they haven't yet - when there are any
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="text">The text, added to a line at a time</param>
		public static void Describe(Village village, System.Text.StringBuilder text)
		{
			List<NotedBed> noted = Read(village.Zdo);
			int brought = noted.Count;
			Dictionary<string, int> waiting = new Dictionary<string, int>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZDO zdo = instance.Key;
				if (instance.Value == null || !IsBedPrefab(zdo.GetPrefab()) || zdo.GetLong(ZDOVars.s_creator, 0L) == 0L || (zdo.GetBool(BroughtKey) && IsNoted(noted, zdo.GetPosition())))
				{
					continue;
				}
				Vector3 position = zdo.GetPosition();
				float outside = village.DistanceOutside(position);
				if (outside > GrowReach + DescribeMargin)
				{
					continue;
				}
				string why;
				if (zdo.GetLong(ZDOVars.s_owner, 0L) != 0L)
				{
					why = "claimed";
				}
				else if (outside > GrowReach)
				{
					why = "over " + GrowReach.ToString("F0") + "m outside";
				}
				else if (Village.Find(position) != village)
				{
					why = "nearer another village";
				}
				else if (!Sheltered(instance.Value.GetComponent<Bed>()))
				{
					why = "not sheltered";
				}
				else if (!village.IsOwner())
				{
					why = "ready, for the client that runs the village to bring its viking";
				}
				else
				{
					why = "ready, " + (lastHeldUp.TryGetValue(village.Zdo.m_uid, out string heldUp) ? "held up by " + heldUp : "its viking on its way");
				}
				waiting[why] = (waiting.TryGetValue(why, out int count) ? count : 0) + 1;
			}
			List<string> parts = new List<string>();
			foreach (KeyValuePair<string, int> entry in waiting)
			{
				parts.Add(entry.Value + " " + entry.Key);
			}
			int added = AddedHouses(village).Count;
			if (brought == 0 && added == 0 && parts.Count == 0)
			{
				return;
			}
			text.Append("Player beds: " + brought + " brought a viking, " + added + " houses taken in" + ((parts.Count > 0) ? " - " + string.Join(", ", parts) : "") + "\n");
		}

		/// <summary>
		/// Finds the living viking a bed is held by - one that notes the bed as its own, as the bed notes it, and still
		/// belongs to a village - as this client knows of it
		/// </summary>
		/// <param name="bed">The bed's ZDO</param>
		/// <param name="village">The village the viking has to belong to, or null for any</param>
		/// <returns>The viking's ZDO, or null when no viking holds it</returns>
		public static ZDO Holder(ZDO bed, Village village)
		{
			ZDOID id = bed.GetZDOID(VikingKey);
			if (id == ZDOID.None || ZDOMan.instance == null)
			{
				return(null);
			}
			ZDO viking = ZDOMan.instance.GetZDO(id);
			if (viking == null || viking.GetZDOID(BedKey) != bed.m_uid)
			{
				return(null);
			}
			Vector3 farm = viking.GetVec3(Village.VillageKey, Vector3.zero);
			if (farm == Vector3.zero || (village != null && Utils.DistanceXZ(farm, village.Farm) > 1f))
			{
				return(null);
			}
			return(viking);
		}

		/// <summary>
		/// Gives each of a village's vikings this client owns that has no bed of its own a free one - in a house of its
		/// core, or one a player put up that brought a viking, that no player has claimed and no other viking holds - in
		/// its home first, then the nearest, so the village's beds show whose they are before anyone sleeps in them
		/// </summary>
		/// <param name="village">The village, whose generator this client owns, with everything across it loaded</param>
		/// <param name="vikings">Its living vikings' ZDOs</param>
		public static void AssignBeds(Village village, List<ZDO> vikings)
		{
			List<ZNetView> free = null;
			foreach (ZDO viking in vikings)
			{
				if (!viking.IsOwner() || HasBed(viking, village))
				{
					continue;
				}
				if (free == null)
				{
					free = FreeBeds(village);
				}
				if (free.Count == 0)
				{
					return;
				}
				Vector3 home = viking.GetVec3(Village.HomeKey, viking.GetPosition());
				bool hasHouse = village.NearestHouse(home, out VillagePaths.House house);
				ZNetView best = null;
				float bestScore = float.MaxValue;
				foreach (ZNetView bed in free)
				{
					float score = Utils.DistanceXZ(bed.transform.position, home) + ((hasHouse && Village.InHouse(house, bed.transform.position, 0.5f)) ? 0f : 1000f);
					if (score < bestScore)
					{
						best = bed;
						bestScore = score;
					}
				}
				Hold(viking, best);
				free.Remove(best);
			}
		}

		/// <summary>
		/// Describes a living viking's bed for its debug hover text - the bed it calls its own, if any, and the village's
		/// beds it could sleep in, counted by why it can't have them
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <param name="village">Its village</param>
		/// <returns>The line, ending in a line break</returns>
		public static string DescribeFor(ZDO viking, Village village)
		{
			string own = "none of its own";
			ZDOID id = viking.GetZDOID(BedKey);
			ZDO mine = ((id != ZDOID.None) ? ZDOMan.instance.GetZDO(id) : null);
			if (mine != null)
			{
				bool held = HasBed(viking, village);
				bool home = village.NearestHouse(viking.GetVec3(Village.HomeKey, viking.GetPosition()), out VillagePaths.House house) && Village.InHouse(house, mine.GetPosition(), 0.5f);
				own = (held ? "its own" : "noted but not its own any more") + " at " + mine.GetPosition().ToString("F0") + (home ? ", in its home" : ", outside its home");
			}
			int free = 0;
			int others = 0;
			int claimed = 0;
			int outside = 0;
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZDO zdo = instance.Key;
				if (instance.Value == null || !IsBedPrefab(zdo.GetPrefab()) || !village.Contains(zdo.GetPosition()))
				{
					continue;
				}
				ZDO holder = Holder(zdo, village);
				if (zdo.GetLong(ZDOVars.s_owner, 0L) != 0L)
				{
					claimed++;
				}
				else if (!village.InCoreHouse(zdo.GetPosition(), 0.5f) && !zdo.GetBool(BroughtKey))
				{
					outside++;
				}
				else if (holder != null && holder != viking)
				{
					others++;
				}
				else if (holder == null)
				{
					free++;
				}
			}
			return("Bed: " + own + " - village beds: " + free + " free, " + others + " other vikings', " + claimed + " claimed by players, " + outside + " outside the core\n");
		}

		/// <summary>
		/// Checks whether a living viking has a bed of its own in its village - one that notes it back, that no player
		/// has claimed
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <param name="village">Its village</param>
		/// <returns>True when it has</returns>
		private static bool HasBed(ZDO viking, Village village)
		{
			ZDOID id = viking.GetZDOID(BedKey);
			ZDO bed = ((id != ZDOID.None) ? ZDOMan.instance.GetZDO(id) : null);
			return(bed != null && bed.GetZDOID(VikingKey) == viking.m_uid && bed.GetLong(ZDOVars.s_owner, 0L) == 0L && village.Contains(bed.GetPosition()));
		}

		/// <summary>
		/// Lists the loaded beds of a village a viking could have - the ones in the houses of its core, and those a
		/// player put up that brought a viking, that no player has claimed and no viking of the village holds
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The beds</returns>
		private static List<ZNetView> FreeBeds(Village village)
		{
			List<ZNetView> free = new List<ZNetView>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZDO zdo = instance.Key;
				ZNetView view = instance.Value;
				if (view == null || !IsBedPrefab(zdo.GetPrefab()) || zdo.GetLong(ZDOVars.s_owner, 0L) != 0L)
				{
					continue;
				}
				Vector3 position = zdo.GetPosition();
				if (!village.InCoreHouse(position, 0.5f) && !(zdo.GetBool(BroughtKey) && village.Contains(position)))
				{
					continue;
				}
				Bed bed = view.GetComponent<Bed>();
				if (bed != null && bed.m_spawnPoint != null && Holder(zdo, village) == null)
				{
					free.Add(view);
				}
			}
			return(free);
		}

		/// <summary>
		/// Notes a bed as a living viking's own, and the viking as the bed's
		/// </summary>
		/// <param name="viking">The viking's ZDO, owned by this client</param>
		/// <param name="bed">The bed</param>
		public static void Hold(ZDO viking, ZNetView bed)
		{
			if (bed == null || !bed.IsValid())
			{
				return;
			}
			ZDO zdo = bed.GetZDO();
			if (zdo.GetZDOID(VikingKey) != viking.m_uid)
			{
				bed.ClaimOwnership();
				zdo.Set(VikingKey, viking.m_uid);
			}
			if (viking.GetZDOID(BedKey) != zdo.m_uid)
			{
				viking.Set(BedKey, zdo.m_uid);
			}
		}

		/// <summary>
		/// Lets go of every bed that brought a village a viking, as the village is disbanded, so each can bring a viking
		/// to a village started there again
		/// </summary>
		/// <param name="village">The village</param>
		public static void Release(Village village)
		{
			if (ZDOMan.instance == null)
			{
				return;
			}
			foreach (NotedBed bed in Read(village.Zdo))
			{
				ZDO zdo = FindBed(bed.Position);
				if (zdo == null)
				{
					continue;
				}
				if (!zdo.IsOwner())
				{
					zdo.SetOwner(ZDOMan.GetSessionID());
				}
				zdo.Set(BroughtKey, false);
				zdo.Set(VikingKey, ZDOID.None);
			}
		}

		/// <summary>
		/// Checks whether a house was added to a village for a bed, rather than being one of its own
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="house">The house</param>
		/// <returns>True when it was</returns>
		public static bool IsAdded(Village village, VillagePaths.House house)
		{
			return(Read(village.Zdo).Exists((NotedBed bed) => bed.Added && SameHouse(bed.House, house)));
		}

		/// <summary>
		/// Brings a viking for an unclaimed bed - or gives it to a viking that turned back to the village as it was started
		/// again, or one more than the village's head count, when there is one - adding the building around it to the
		/// village's houses when it's in none of them, and notes what held it up when it can't come yet
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="vikings">Its living vikings' ZDOs, added to</param>
		/// <param name="noted">The beds noted in the village, added to</param>
		private static void Invite(Village village, List<ZDO> vikings, List<NotedBed> noted)
		{
			ZNetView bedView = FindInviting(village, noted);
			if (bedView == null)
			{
				return;
			}
			Vector3 position = bedView.transform.position;
			NotedBed entry = new NotedBed { Id = bedView.GetZDO().m_uid, Position = position };
			List<VillagePaths.House> houses = new List<VillagePaths.House>(village.Houses);
			if (!village.InHouse(position, 0.5f))
			{
				VillagePaths.House measured = Measure(bedView.GetComponent<Bed>());
				// A building that runs into one of the village's houses is part of it, and isn't added again
				if (!houses.Exists((VillagePaths.House house) => Village.InHouse(house, measured.Center, 0f)))
				{
					entry.Added = true;
					entry.House = measured;
					houses.Add(measured);
				}
			}
			List<VillagePaths.House> core = Village.Core(VillagePaths.ReadHouses(village.Zdo.GetByteArray(VillagePaths.HousesKey)), houses);
			VillagePaths.House home;
			if (!NearestHouse(core, position, out home))
			{
				return;
			}
			foreach (VillagePaths.House house in core)
			{
				if (Village.InHouse(house, position, 0.5f))
				{
					home = house;
					break;
				}
			}
			// A viking that turned back to the village as it was started again takes the bed, rather than one coming to it,
			// and so does one more than the village's head count - one that stayed on when its bed was let go of, say
			ZDO turned = vikings.Find((ZDO viking) => viking.GetBool(VillageFounding.TurnedBackKey));
			if (turned == null && vikings.Count > village.Zdo.GetInt(VillageRespawn.HeadCountKey))
			{
				turned = Nearest(vikings, position);
			}
			if (turned != null)
			{
				if (!turned.IsOwner())
				{
					turned.SetOwner(ZDOMan.GetSessionID());
				}
				turned.Set(VillageFounding.TurnedBackKey, false);
				village.Adopt(turned, home);
			}
			else if (VillageRespawn.Invite(village, vikings, home, out string why) == null)
			{
				lastHeldUp[village.Zdo.m_uid] = why;
				return;
			}
			lastHeldUp.Remove(village.Zdo.m_uid);
			if (entry.Added)
			{
				Village.RecordHouses(village.Zdo, houses);
				village.ReadAgain();
				Plugin.Log.LogInfo("The village at " + village.Farm.ToString("F0") + " grew to take in the building at " + entry.House.Center.ToString("F0") + ", " + (entry.House.HalfSize * 2f).ToString("F0") + "m across" + (village.IsCore(entry.House) ? ", in its core" : ", as an outpost") + ", reaching " + village.Radius.ToString("F0") + "m");
			}
			bedView.ClaimOwnership();
			bedView.GetZDO().Set(BroughtKey, true);
			noted.Add(entry);
			Write(village.Zdo, noted);
			int count = village.Zdo.GetInt(VillageRespawn.HeadCountKey) + 1;
			village.Zdo.Set(VillageRespawn.HeadCountKey, count);
			Plugin.Log.LogInfo("A bed at " + position.ToString("F1") + " brought a viking to the village at " + village.Farm.ToString("F0") + ", up to " + count + " vikings");
		}

		/// <summary>
		/// Keeps the houses added for a village's noted beds among its houses, and only those - taking off the houses no
		/// noted bed is in any more, and putting back any that are missing
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="noted">The beds noted in it</param>
		private static void KeepHouses(Village village, List<NotedBed> noted)
		{
			List<VillagePaths.House> houses = new List<VillagePaths.House>(village.Houses);
			List<VillagePaths.House> farmhouses = VillagePaths.ReadHouses(village.Zdo.GetByteArray(VillagePaths.HousesKey));
			bool changed = false;
			foreach (NotedBed bed in noted)
			{
				if (bed.Added && !houses.Exists((VillagePaths.House house) => SameHouse(house, bed.House)))
				{
					houses.Add(bed.House);
					changed = true;
				}
			}
			List<VillagePaths.House> added = AddedHouses(village);
			foreach (VillagePaths.House house in added)
			{
				bool kept = noted.Exists((NotedBed bed) => bed.Added && SameHouse(bed.House, house));
				bool own = farmhouses.Exists((VillagePaths.House farmhouse) => SameHouse(farmhouse, house));
				if (!kept && !own && houses.RemoveAll((VillagePaths.House other) => SameHouse(other, house)) > 0)
				{
					changed = true;
					Plugin.Log.LogInfo("The village at " + village.Farm.ToString("F0") + " shrank back from the building at " + house.Center.ToString("F0") + ", which has no beds for its vikings left");
				}
			}
			SetAddedHouses(village, noted);
			if (changed)
			{
				Village.RecordHouses(village.Zdo, houses);
				village.ReadAgain();
			}
		}

		/// <summary>
		/// Lists the houses a village has had added for its beds, as last noted
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The houses</returns>
		public static List<VillagePaths.House> AddedHouses(Village village)
		{
			byte[] bytes = village.Zdo.GetByteArray(AddedHousesKey);
			return((bytes != null) ? VillagePaths.ReadHouses(bytes) : new List<VillagePaths.House>());
		}

		/// <summary>
		/// Notes the houses added to a village for the beds noted in it
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="noted">The beds</param>
		private static void SetAddedHouses(Village village, List<NotedBed> noted)
		{
			List<VillagePaths.House> added = new List<VillagePaths.House>();
			foreach (NotedBed bed in noted)
			{
				if (bed.Added && !added.Exists((VillagePaths.House house) => SameHouse(house, bed.House)))
				{
					added.Add(bed.House);
				}
			}
			ZPackage pkg = new ZPackage();
			pkg.Write(added.Count);
			foreach (VillagePaths.House house in added)
			{
				pkg.Write(house.Center);
				pkg.Write(house.HalfSize);
			}
			byte[] bytes = pkg.GetArray();
			byte[] old = village.Zdo.GetByteArray(AddedHousesKey);
			if (old == null || !System.Linq.Enumerable.SequenceEqual(old, bytes))
			{
				village.Zdo.Set(AddedHousesKey, bytes);
			}
		}

		/// <summary>
		/// Checks whether two houses are the same house, by their middles
		/// </summary>
		/// <param name="a">One house</param>
		/// <param name="b">The other</param>
		/// <returns>True when they are</returns>
		private static bool SameHouse(VillagePaths.House a, VillagePaths.House b)
		{
			return(Utils.DistanceXZ(a.Center, b.Center) <= HouseMatch);
		}

		/// <summary>
		/// Finds the house nearest a point among some houses
		/// </summary>
		/// <param name="houses">The houses</param>
		/// <param name="point">The point</param>
		/// <param name="nearest">The nearest house</param>
		/// <returns>False when there are none</returns>
		private static bool NearestHouse(List<VillagePaths.House> houses, Vector3 point, out VillagePaths.House nearest)
		{
			nearest = new VillagePaths.House();
			float best = float.MaxValue;
			foreach (VillagePaths.House house in houses)
			{
				float distance = Utils.DistanceXZ(house.Center, point);
				if (distance < best)
				{
					best = distance;
					nearest = house;
				}
			}
			return(best < float.MaxValue);
		}

		/// <summary>
		/// Measures the building a bed stands in - casting level rays out from it to the walls around it, leaving out
		/// furniture, and taking the middle of the walls found and the farthest of them, with WallMargin to spare - or
		/// a small house around the bed when too few rays find a wall
		/// </summary>
		/// <param name="bed">The bed</param>
		/// <returns>The building, as a house</returns>
		private static VillagePaths.House Measure(Bed bed)
		{
			if (wallMask == 0)
			{
				wallMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece");
			}
			Vector3 origin = ((bed.m_spawnPoint != null) ? bed.GetSpawnPoint() : bed.transform.position);
			origin.y = bed.transform.position.y + MeasureHeight;
			List<Vector3> walls = new List<Vector3>();
			for (int i = 0; i < MeasureRays; i++)
			{
				Vector3 direction = Quaternion.Euler(0f, i * 360f / MeasureRays, 0f) * Vector3.forward;
				float nearest = float.MaxValue;
				Vector3 wall = Vector3.zero;
				foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, MeasureReach, wallMask, QueryTriggerInteraction.Ignore))
				{
					Piece piece = hit.collider.GetComponentInParent<Piece>();
					if (piece != null && piece.m_category != Piece.PieceCategory.Furniture && hit.distance < nearest)
					{
						nearest = hit.distance;
						wall = hit.point;
					}
				}
				if (nearest < float.MaxValue)
				{
					walls.Add(wall);
				}
			}
			return(FitHouse(walls, origin, bed.transform.position.y));
		}

		/// <summary>
		/// Works out the house a bed's building makes from the walls found around it - centered on the middle of the walls
		/// and reaching the farthest of them, with WallMargin to spare, from HalfSizeMin to HalfSizeMax across each way -
		/// or a house of HalfSizeMin around the bed when fewer than MeasureWalls were found
		/// </summary>
		/// <param name="walls">Where the rays cast out from the bed found walls</param>
		/// <param name="origin">Where the rays were cast from</param>
		/// <param name="floorY">The height of the bed's floor, which the house's middle is put at</param>
		/// <returns>The building, as a house</returns>
		internal static VillagePaths.House FitHouse(List<Vector3> walls, Vector3 origin, float floorY)
		{
			VillagePaths.House house = new VillagePaths.House();
			if (walls.Count < MeasureWalls)
			{
				house.Center = new Vector3(origin.x, floorY, origin.z);
				house.HalfSize = HalfSizeMin;
				return(house);
			}
			Vector3 sum = Vector3.zero;
			foreach (Vector3 wall in walls)
			{
				sum += wall;
			}
			Vector3 center = sum / walls.Count;
			center.y = floorY;
			float half = 0f;
			foreach (Vector3 wall in walls)
			{
				half = Mathf.Max(half, Mathf.Abs(wall.x - center.x), Mathf.Abs(wall.z - center.z));
			}
			house.Center = center;
			house.HalfSize = Mathf.Clamp(half + WallMargin, HalfSizeMin, HalfSizeMax);
			return(house);
		}

		/// <summary>
		/// Checks whether a bed that brought a viking no longer holds it - it's gone from a zone that has finished
		/// loading, or a player has claimed it
		/// </summary>
		/// <param name="bed">The bed</param>
		/// <returns>True when it no longer does</returns>
		private static bool Released(NotedBed bed)
		{
			ZDO zdo = FindBed(bed.Position);
			if (zdo == null)
			{
				return(VillageRespawn.ZoneSettled(bed.Position));
			}
			return(zdo.GetLong(ZDOVars.s_owner, 0L) != 0L);
		}

		/// <summary>
		/// Finds the bed standing where a bed was noted - by where it stands rather than its ZDO's ID, which the game gives
		/// out afresh each time the world loads
		/// </summary>
		/// <param name="position">Where the bed was noted</param>
		/// <returns>The bed's ZDO, or null when none stands there that this client knows of</returns>
		private static ZDO FindBed(Vector3 position)
		{
			List<ZDO> zdos = new List<ZDO>();
			VillageRespawn.ZdosNear(position, BedMatch, zdos);
			return(zdos.Find((ZDO zdo) => IsBedPrefab(zdo.GetPrefab()) && Vector3.Distance(zdo.GetPosition(), position) <= BedMatch));
		}

		/// <summary>
		/// Checks whether a bed standing at a spot is one of the beds noted in a village
		/// </summary>
		/// <param name="noted">The beds noted in the village</param>
		/// <param name="position">Where the bed stands</param>
		/// <returns>True when it is</returns>
		private static bool IsNoted(List<NotedBed> noted, Vector3 position)
		{
			return(noted.Exists((NotedBed bed) => Vector3.Distance(bed.Position, position) <= BedMatch));
		}

		/// <summary>
		/// Finds the viking nearest a point
		/// </summary>
		/// <param name="vikings">The vikings' ZDOs, at least one</param>
		/// <param name="position">The point</param>
		/// <returns>The nearest one's ZDO</returns>
		private static ZDO Nearest(List<ZDO> vikings, Vector3 position)
		{
			ZDO nearest = vikings[0];
			foreach (ZDO viking in vikings)
			{
				if (Utils.DistanceXZ(viking.GetPosition(), position) < Utils.DistanceXZ(nearest.GetPosition(), position))
				{
					nearest = viking;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Finds an unclaimed bed in or near a village that hasn't brought it a viking yet - one not among its noted beds
		/// (see the remarks)
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="noted">The beds noted in it</param>
		/// <returns>The bed's view, or null when there's none</returns>
		private static ZNetView FindInviting(Village village, List<NotedBed> noted)
		{
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZDO zdo = instance.Key;
				ZNetView view = instance.Value;
				// A bed marked as having brought a viking that isn't among the village's noted beds holds no viking there,
				// so it can bring one again
				if (view == null || !IsBedPrefab(zdo.GetPrefab()) || zdo.GetLong(ZDOVars.s_creator, 0L) == 0L || (zdo.GetBool(BroughtKey) && IsNoted(noted, zdo.GetPosition())))
				{
					continue;
				}
				Vector3 position = zdo.GetPosition();
				if (zdo.GetLong(ZDOVars.s_owner, 0L) == 0L && village.DistanceOutside(position) <= GrowReach && Village.Find(position) == village && Sheltered(view.GetComponent<Bed>()))
				{
					return(view);
				}
			}
			return(null);
		}

		/// <summary>
		/// Checks whether a bed could be claimed by a player for its shelter - its spawn point under a roof and covered
		/// enough
		/// </summary>
		/// <param name="bed">The bed</param>
		/// <returns>True when it could</returns>
		public static bool Sheltered(Bed bed)
		{
			if (bed == null || bed.m_spawnPoint == null)
			{
				return(false);
			}
			Cover.GetCoverForPoint(bed.GetSpawnPoint(), out float cover, out bool underRoof);
			return(underRoof && cover >= MinCover);
		}

		/// <summary>
		/// Checks whether a prefab is a bed, remembering the answer
		/// </summary>
		/// <param name="prefab">The prefab's hash</param>
		/// <returns>True when it is</returns>
		private static bool IsBedPrefab(int prefab)
		{
			if (bedPrefabs.TryGetValue(prefab, out bool bed))
			{
				return(bed);
			}
			GameObject asset = ZNetScene.instance.GetPrefab(prefab);
			if (asset == null)
			{
				return(false);
			}
			bed = (asset.GetComponent<Bed>() != null);
			bedPrefabs[prefab] = bed;
			return(bed);
		}

		/// <summary>
		/// Reads the beds noted in a village's generator (see DecodeBeds), as none when they can't be read, writing to the
		/// log once for each unreadable list
		/// </summary>
		/// <param name="generator">The generator's ZDO</param>
		/// <returns>The beds</returns>
		private static List<NotedBed> Read(ZDO generator)
		{
			byte[] bytes = generator.GetByteArray(BedsKey);
			if (bytes == null)
			{
				return(new List<NotedBed>());
			}
			if (!TryDecodeBeds(bytes, out List<NotedBed> beds) && !ReferenceEquals(unreadable, bytes))
			{
				unreadable = bytes;
				Plugin.Log.LogWarning("Couldn't read the beds noted for the village at " + generator.GetPosition().ToString("F0"));
			}
			return(beds);
		}

		/// <summary>
		/// Reads the beds noted in a village as EncodeBeds wrote them - with FormatVersion first - or by the first way, as
		/// their count and then just their ZDOs and where they stand, with no house added for them
		/// </summary>
		/// <param name="bytes">The bytes</param>
		/// <param name="beds">The beds, or none when they can't be read</param>
		/// <returns>False when the bytes are cut short or don't hold beds</returns>
		internal static bool TryDecodeBeds(byte[] bytes, out List<NotedBed> beds)
		{
			beds = new List<NotedBed>();
			try
			{
				ZPackage pkg = new ZPackage(bytes);
				int first = pkg.ReadInt();
				bool withHouses = (first == FormatVersion);
				int count = (withHouses ? pkg.ReadInt() : first);
				for (int i = 0; i < count; i++)
				{
					NotedBed bed = new NotedBed();
					bed.Id = pkg.ReadZDOID();
					bed.Position = pkg.ReadVector3();
					if (withHouses)
					{
						bed.Added = pkg.ReadBool();
						bed.House = new VillagePaths.House { Center = pkg.ReadVector3(), HalfSize = pkg.ReadSingle() };
					}
					beds.Add(bed);
				}
				return(true);
			}
			catch (System.Exception)
			{
				beds.Clear();
				return(false);
			}
		}

		/// <summary>
		/// Writes the beds noted in a village's generator
		/// </summary>
		/// <param name="generator">The generator's ZDO, owned by this client</param>
		/// <param name="beds">The beds</param>
		private static void Write(ZDO generator, List<NotedBed> beds)
		{
			generator.Set(BedsKey, EncodeBeds(beds));
		}

		/// <summary>
		/// Writes the beds noted in a village - FormatVersion, their count, and each bed's ZDO, where it stands, whether a
		/// house was added for it and that house's middle and size
		/// </summary>
		/// <param name="beds">The beds</param>
		/// <returns>The bytes</returns>
		internal static byte[] EncodeBeds(List<NotedBed> beds)
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(FormatVersion);
			pkg.Write(beds.Count);
			foreach (NotedBed bed in beds)
			{
				pkg.Write(bed.Id);
				pkg.Write(bed.Position);
				pkg.Write(bed.Added);
				pkg.Write(bed.House.Center);
				pkg.Write(bed.House.HalfSize);
			}
			return(pkg.GetArray());
		}
	}
}
