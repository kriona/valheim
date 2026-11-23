using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// One of a village's own building pieces as it first stood - its prefab, where it stood, which way it faced, and
	/// how much wood it takes to build
	/// </summary>
	internal struct PieceRecord
	{
		public int Prefab;
		public Vector3 Position;
		public Quaternion Rotation;
		public int Wood;
	}

	/// <summary>
	/// Notes each village's own building pieces once, so its vikings can tell which have been destroyed and build them
	/// back
	/// </summary>
	/// <remarks>
	/// Whoever owns a village's generator notes its pieces in the generator's ZDO, the first time everything across
	/// the village has loaded, and again when they were noted by an older version - every piece of its houses, fences
	/// and pens that no player built (see Village.OwnsPiece). A piece is told apart from the rest by its prefab and
	/// where it stands, and only pieces in zones that have finished loading are judged
	/// </remarks>
	internal static class VillageRecords
	{
		/// <summary>
		/// On a farm's generator, its village's own building pieces, compressed
		/// </summary>
		public static readonly int RecordsKey = "kriona.VikingVillages.villageRecords".GetStableHashCode();

		/// <summary>
		/// On a farm's generator, the version its pieces were noted with, so they're noted again when it's older
		/// </summary>
		public static readonly int RecordsVersionKey = "kriona.VikingVillages.villageRecordsVersion".GetStableHashCode();

		/// <summary>
		/// On a piece the mod placed or built back, so it breaks into nothing (see VillagePieceDropPatch) - the vikings
		/// build it for wood they gather for free, whatever it's made of
		/// </summary>
		public static readonly int BuiltKey = "kriona.VikingVillages.villageBuilt".GetStableHashCode();

		/// <summary>
		/// The version of how the pieces are written and which are noted, which a reader skips pieces written another
		/// way by
		/// </summary>
		private const int FormatVersion = 3;

		/// <summary>
		/// How far from where a piece stood one of its prefab can stand and still be it, in meters
		/// </summary>
		private const float MatchReach = 0.5f;

		/// <summary>
		/// How far past a house's walls a noted piece still counts as part of the house, for telling whether it's in
		/// ruins, in meters
		/// </summary>
		private const float HousePieceReach = 1f;

		/// <summary>
		/// How a piece's position is written - in centimeters from the generator, no farther away than MaxOffset meters
		/// - and its rotation, as each angle in 65536ths of a turn
		/// </summary>
		private const float PositionScale = 100f;
		private const float MaxOffset = 320f;
		private const float AngleScale = 65536f / 360f;

		/// <summary>
		/// The wood each prefab takes to build, or -1 for a prefab that isn't a building piece
		/// </summary>
		private static readonly Dictionary<int, int> woodByPrefab = new Dictionary<int, int>();

		/// <summary>
		/// Each generator's pieces as last read, with the bytes they were read from
		/// </summary>
		private static readonly Dictionary<ZDOID, KeyValuePair<byte[], List<PieceRecord>>> cache = new Dictionary<ZDOID, KeyValuePair<byte[], List<PieceRecord>>>();

		/// <summary>
		/// The share of a house's noted pieces that have to stand for it to stand, for the vikings to call it home
		/// </summary>
		private const float StandingShare = 0.5f;

		/// <summary>
		/// Seconds each village's standing houses are kept before they're worked out again
		/// </summary>
		private const float StandingInterval = 5f;

		/// <summary>
		/// Each village's standing houses and when they were worked out, by its generator's ZDO
		/// </summary>
		private static readonly Dictionary<ZDOID, KeyValuePair<float, List<VillagePaths.House>>> standingCache = new Dictionary<ZDOID, KeyValuePair<float, List<VillagePaths.House>>>();

		/// <summary>
		/// A noted piece as it's written - its prefab, where it stood, its angles in degrees and the wood it takes to
		/// build
		/// </summary>
		internal struct WrittenPiece
		{
			public int Prefab;
			public Vector3 Position;
			public Vector3 Angles;
			public int Wood;
		}

		/// <summary>
		/// Notes a village's own building pieces in its generator, when they haven't been yet
		/// </summary>
		/// <param name="village">The village, whose generator this client owns, with everything across it loaded</param>
		public static void TrySnapshot(Village village)
		{
			if (IsNoted(village))
			{
				return;
			}
			Snapshot(village);
		}

		/// <summary>
		/// Checks whether a village's own building pieces have been noted, by this version
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>True when they have</returns>
		public static bool IsNoted(Village village)
		{
			return(village.Zdo.GetByteArray(RecordsKey) != null && village.Zdo.GetInt(RecordsVersionKey) == FormatVersion);
		}

		/// <summary>
		/// Notes a village's own building pieces in its generator
		/// </summary>
		/// <param name="village">The village, whose generator this client owns, with everything across it loaded</param>
		private static void Snapshot(Village village)
		{
			List<ZDO> zdos = new List<ZDO>();
			VillageRespawn.ZdosNear(village.Middle, village.Radius + Village.PieceMargin, zdos);
			Vector3 origin = village.Farm;
			List<PieceRecord> records = new List<PieceRecord>();
			foreach (ZDO zdo in zdos)
			{
				int wood = WoodCost(zdo.GetPrefab());
				// The village's fire is noted on its own, since it's built back as the village's (see Village.FireRecord).
				// A piece just taken down, like a signpost swapped for a sign, stays among the ZDOs until the game sends
				// its removal on, and isn't noted
				if (wood < 0 || zdo.GetLong(ZDOVars.s_creator, 0L) != 0L || zdo.GetBool(VillagePaths.BonfireKey) || ZDOMan.instance.m_destroySendList.Contains(zdo.m_uid))
				{
					continue;
				}
				Vector3 position = zdo.GetPosition();
				if (!village.OwnsPiece(position) || !InReach(origin, position))
				{
					continue;
				}
				PieceRecord record = new PieceRecord();
				record.Prefab = zdo.GetPrefab();
				record.Position = position;
				record.Rotation = zdo.GetRotation();
				record.Wood = wood;
				records.Add(record);
			}
			Write(village.Zdo, origin, records);
			Plugin.Log.LogInfo("Noted " + records.Count + " building pieces of the village at " + village.Farm.ToString("F0"));
		}

		/// <summary>
		/// Notes building pieces given to a village after its pieces were noted, so its vikings build them back too -
		/// nothing is noted when its pieces haven't been, since they'll be noted along with the rest
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		/// <param name="added">The pieces</param>
		public static void Add(Village village, List<PieceRecord> added)
		{
			if (added.Count == 0 || village.Zdo.GetByteArray(RecordsKey) == null || village.Zdo.GetInt(RecordsVersionKey) != FormatVersion)
			{
				return;
			}
			List<PieceRecord> records = new List<PieceRecord>(Read(village.Zdo));
			records.AddRange(added);
			Write(village.Zdo, village.Farm, records);
		}

		/// <summary>
		/// Lists a village's noted pieces that match
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="match">Which pieces to list</param>
		/// <returns>The pieces, or none before the village's pieces are noted</returns>
		public static List<PieceRecord> Find(Village village, System.Predicate<PieceRecord> match)
		{
			return(Read(village.Zdo).FindAll(match));
		}

		/// <summary>
		/// Forgets a village's noted pieces, so its vikings no longer build them back - nothing changes when the village's
		/// pieces haven't been noted
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		/// <param name="match">Which pieces to forget</param>
		/// <returns>How many were forgotten</returns>
		public static int Remove(Village village, System.Predicate<PieceRecord> match)
		{
			if (village.Zdo.GetByteArray(RecordsKey) == null || village.Zdo.GetInt(RecordsVersionKey) != FormatVersion)
			{
				return(0);
			}
			List<PieceRecord> records = new List<PieceRecord>(Read(village.Zdo));
			int removed = records.RemoveAll(match);
			if (removed > 0)
			{
				Write(village.Zdo, village.Farm, records);
			}
			return(removed);
		}

		/// <summary>
		/// Notes a village's piece as the piece put up in its place, or notes the new piece among the village's pieces
		/// when the old one wasn't - nothing changes when the village's pieces haven't been noted yet, since the new one
		/// is noted along with the rest
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		/// <param name="from">The prefab it's noted as</param>
		/// <param name="position">Where it stood</param>
		/// <param name="swapped">The piece put up in its place</param>
		public static void Swap(Village village, int from, Vector3 position, PieceRecord swapped)
		{
			if (village.Zdo.GetByteArray(RecordsKey) == null || village.Zdo.GetInt(RecordsVersionKey) != FormatVersion)
			{
				return;
			}
			List<PieceRecord> records = new List<PieceRecord>(Read(village.Zdo));
			int index = records.FindIndex((PieceRecord record) => record.Prefab == from && Vector3.Distance(record.Position, position) <= MatchReach);
			if (index < 0)
			{
				records.Add(swapped);
			}
			else
			{
				records[index] = swapped;
			}
			Write(village.Zdo, village.Farm, records);
		}

		/// <summary>
		/// Takes the pieces of some prefabs out of a village's noted pieces, so its vikings no longer build them back
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		/// <param name="unwanted">True for a prefab whose pieces are taken out, by its hash</param>
		/// <returns>How many were taken out</returns>
		public static int RemoveAll(Village village, System.Predicate<int> unwanted)
		{
			if (village.Zdo.GetByteArray(RecordsKey) == null || village.Zdo.GetInt(RecordsVersionKey) != FormatVersion)
			{
				return(0);
			}
			List<PieceRecord> records = new List<PieceRecord>(Read(village.Zdo));
			int removed = records.RemoveAll((PieceRecord record) => unwanted(record.Prefab));
			if (removed > 0)
			{
				Write(village.Zdo, village.Farm, records);
			}
			return(removed);
		}

		/// <summary>
		/// Counts a village's noted pieces, for the debug hover text of its fire
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>How many, or -1 before they're noted</returns>
		public static int Count(Village village)
		{
			if (village.Zdo.GetByteArray(RecordsKey) == null)
			{
				return(-1);
			}
			return(Read(village.Zdo).Count);
		}

		/// <summary>
		/// Checks whether a prefab is a pile of wood - one the vikings leave gone once it's taken down or burned, rather
		/// than building it back from wood they gather for free
		/// </summary>
		/// <param name="prefab">The prefab's hash</param>
		/// <returns>True when it is</returns>
		private static bool IsWoodPile(int prefab)
		{
			return(woodPiles.Contains(prefab));
		}

		/// <summary>
		/// The piles of wood, by prefab hash (see IsWoodPile)
		/// </summary>
		private static readonly HashSet<int> woodPiles = new HashSet<int>
		{
			"wood_stack".GetStableHashCode(),
			"wood_fine_stack".GetStableHashCode(),
			"wood_core_stack".GetStableHashCode(),
			"wood_frost_stack".GetStableHashCode(),
			"wood_yggdrasil_stack".GetStableHashCode(),
			"blackwood_stack".GetStableHashCode()
		};

		/// <summary>
		/// Lists a village's noted pieces that no longer stand, among those in zones that have finished loading - leaving
		/// out the pieces of a house with none of its pieces standing, which is in ruins and built back last (see
		/// MissingInRuins), and piles of wood (see IsWoodPile)
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The missing pieces, or none before the village's pieces are noted</returns>
		public static List<PieceRecord> Missing(Village village)
		{
			List<PieceRecord> missing = new List<PieceRecord>();
			if (village == null || village.Zdo == null)
			{
				return(missing);
			}
			List<PieceRecord> records = Read(village.Zdo);
			if (records.Count == 0)
			{
				return(missing);
			}
			Dictionary<int, List<Vector3>> standing = Standing(village);
			List<VillagePaths.House> ruins = Ruins(village, records, standing);
			foreach (PieceRecord record in records)
			{
				if (!IsWoodPile(record.Prefab) && VillageRespawn.ZoneSettled(record.Position) && !Stands(standing, record) && !ruins.Exists((VillagePaths.House house) => Village.InHouse(house, record.Position, HousePieceReach)))
				{
					missing.Add(record);
				}
			}
			return(missing);
		}

		/// <summary>
		/// Lists the noted pieces of a village's houses that are in ruins - every one of them, since none stands, but piles
		/// of wood (see IsWoodPile) - for the vikings to build back once the rest of the village is
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The ruins' pieces, or none before the village's pieces are noted</returns>
		public static List<PieceRecord> MissingInRuins(Village village)
		{
			List<PieceRecord> missing = new List<PieceRecord>();
			if (village == null || village.Zdo == null)
			{
				return(missing);
			}
			List<PieceRecord> records = Read(village.Zdo);
			if (records.Count == 0)
			{
				return(missing);
			}
			List<VillagePaths.House> ruins = Ruins(village, records, Standing(village));
			foreach (PieceRecord record in records)
			{
				if (!IsWoodPile(record.Prefab) && ruins.Exists((VillagePaths.House house) => Village.InHouse(house, record.Position, HousePieceReach)))
				{
					missing.Add(record);
				}
			}
			return(missing);
		}

		/// <summary>
		/// Lists the houses of a village's core that still stand - with at least StandingShare of their noted pieces
		/// standing, or none noted, or not all of them in zones that have finished loading - which the vikings can call
		/// home, kept for StandingInterval seconds
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The standing houses</returns>
		public static List<VillagePaths.House> StandingCore(Village village)
		{
			ZDOID id = village.Zdo.m_uid;
			if (standingCache.TryGetValue(id, out KeyValuePair<float, List<VillagePaths.House>> kept) && Time.time - kept.Key < StandingInterval)
			{
				return(kept.Value);
			}
			List<VillagePaths.House> houses = new List<VillagePaths.House>();
			List<PieceRecord> records = Read(village.Zdo);
			Dictionary<int, List<Vector3>> standing = ((records.Count > 0) ? Standing(village) : null);
			foreach (VillagePaths.House house in village.CoreHouses)
			{
				int noted = 0;
				int stands = 0;
				bool settled = true;
				foreach (PieceRecord record in records)
				{
					if (!Village.InHouse(house, record.Position, HousePieceReach))
					{
						continue;
					}
					noted++;
					if (!VillageRespawn.ZoneSettled(record.Position))
					{
						settled = false;
						break;
					}
					if (Stands(standing, record))
					{
						stands++;
					}
				}
				if (HouseStands(noted, stands, settled))
				{
					houses.Add(house);
				}
			}
			standingCache[id] = new KeyValuePair<float, List<VillagePaths.House>>(Time.time, houses);
			return(houses);
		}

		/// <summary>
		/// Checks whether a house still stands - with at least StandingShare of its noted pieces standing, or none noted,
		/// or not all of them in zones that have finished loading
		/// </summary>
		/// <param name="noted">How many of the village's noted pieces are in the house</param>
		/// <param name="stands">How many of those stand</param>
		/// <param name="settled">Whether they're all in zones that have finished loading</param>
		/// <returns>True when it stands</returns>
		internal static bool HouseStands(int noted, int stands, bool settled)
		{
			return(noted == 0 || !settled || stands >= noted * StandingShare);
		}

		/// <summary>
		/// Checks whether a house is in ruins - with noted pieces, all of them in zones that have finished loading and
		/// none of them standing
		/// </summary>
		/// <param name="noted">How many of the village's noted pieces are in the house</param>
		/// <param name="stands">How many of those stand</param>
		/// <param name="settled">Whether they're all in zones that have finished loading</param>
		/// <returns>True when it's in ruins</returns>
		internal static bool IsRuin(int noted, int stands, bool settled)
		{
			return(noted > 0 && settled && stands == 0);
		}

		/// <summary>
		/// Finds where the building pieces standing across a village are, by prefab
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>Where each prefab's pieces stand</returns>
		private static Dictionary<int, List<Vector3>> Standing(Village village)
		{
			List<ZDO> zdos = new List<ZDO>();
			VillageRespawn.ZdosNear(village.Middle, village.Radius + Village.PieceMargin, zdos);
			Dictionary<int, List<Vector3>> standing = new Dictionary<int, List<Vector3>>();
			foreach (ZDO zdo in zdos)
			{
				int prefab = zdo.GetPrefab();
				if (WoodCost(prefab) < 0)
				{
					continue;
				}
				if (!standing.TryGetValue(prefab, out List<Vector3> positions))
				{
					positions = new List<Vector3>();
					standing[prefab] = positions;
				}
				positions.Add(zdo.GetPosition());
			}
			return(standing);
		}

		/// <summary>
		/// Lists a village's houses that are in ruins - with noted pieces, all of them in zones that have finished
		/// loading and none of them standing
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="records">Its noted pieces</param>
		/// <param name="standing">Where the pieces found stand, by prefab</param>
		/// <returns>The ruined houses</returns>
		private static List<VillagePaths.House> Ruins(Village village, List<PieceRecord> records, Dictionary<int, List<Vector3>> standing)
		{
			List<VillagePaths.House> ruins = new List<VillagePaths.House>();
			foreach (VillagePaths.House house in village.Houses)
			{
				int noted = 0;
				int stands = 0;
				bool settled = true;
				// One piece standing, or one in a zone still loading, is enough to tell it isn't in ruins
				foreach (PieceRecord record in records)
				{
					if (!Village.InHouse(house, record.Position, HousePieceReach))
					{
						continue;
					}
					noted++;
					if (!VillageRespawn.ZoneSettled(record.Position))
					{
						settled = false;
						break;
					}
					if (Stands(standing, record))
					{
						stands++;
						break;
					}
				}
				if (IsRuin(noted, stands, settled))
				{
					ruins.Add(house);
				}
			}
			return(ruins);
		}

		/// <summary>
		/// Builds a village's missing piece back where it stood, as a piece no player built, with its place effect
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="record">The piece</param>
		/// <returns>False when it couldn't be - its prefab is unknown, its zone hasn't finished loading, it already
		/// stands or an active ward covers it</returns>
		public static bool Rebuild(Village village, PieceRecord record)
		{
			if (village == null || ZNetScene.instance == null)
			{
				return(false);
			}
			GameObject prefab = ZNetScene.instance.GetPrefab(record.Prefab);
			// A ward put up while the viking gathered the wood keeps it from building there
			if (prefab == null || !VillageRespawn.ZoneSettled(record.Position) || StandsAt(record) || VillageLife.Warded(record.Position))
			{
				return(false);
			}
			GameObject instance = Object.Instantiate(prefab, record.Position, record.Rotation);
			MarkBuilt(instance);
			WearNTear wear = instance.GetComponent<WearNTear>();
			if (wear != null)
			{
				wear.OnPlaced();
			}
			Piece piece = instance.GetComponent<Piece>();
			if (piece != null)
			{
				piece.m_placeEffect.Create(record.Position, record.Rotation, instance.transform);
			}
			VillageSigns.Name(instance, village);
			return(true);
		}

		/// <summary>
		/// Marks a piece the mod just placed or built back, so it breaks into nothing (see VillagePieceDropPatch)
		/// </summary>
		/// <param name="instance">The piece</param>
		public static void MarkBuilt(GameObject instance)
		{
			ZNetView view = ((instance != null) ? instance.GetComponent<ZNetView>() : null);
			if (view != null && view.GetZDO() != null)
			{
				view.GetZDO().Set(BuiltKey, true);
			}
		}

		/// <summary>
		/// Forgets the pieces and standing houses read in the world being left
		/// </summary>
		public static void Forget()
		{
			cache.Clear();
			standingCache.Clear();
		}

		/// <summary>
		/// Lists the loaded building pieces in and around a village that are below full health, whoever built them - its
		/// houses, fences and pens, its fire, and what players have built there - leaving out those inside an active ward
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The damaged pieces</returns>
		public static List<WearNTear> Damaged(Village village)
		{
			List<WearNTear> damaged = new List<WearNTear>();
			if (village == null)
			{
				return(damaged);
			}
			foreach (WearNTear wear in WearNTear.GetAllInstances())
			{
				if (wear == null || wear.m_nview == null || !wear.m_nview.IsValid() || wear.m_health <= 0f)
				{
					continue;
				}
				ZDO zdo = wear.m_nview.GetZDO();
				if (!village.OwnsPiece(wear.transform.position) && !zdo.GetBool(VillagePaths.BonfireKey))
				{
					continue;
				}
				// A ward keeps the vikings from repairing what's in it, the same as it keeps out players it doesn't
				// permit
				if (VillageLife.Warded(wear.transform.position))
				{
					continue;
				}
				if (zdo.GetFloat(ZDOVars.s_health, wear.m_health) < wear.m_health * 0.999f)
				{
					damaged.Add(wear);
				}
			}
			return(damaged);
		}

		/// <summary>
		/// Checks whether a noted piece stands among the pieces found
		/// </summary>
		/// <param name="standing">Where the pieces found stand, by prefab</param>
		/// <param name="record">The piece</param>
		/// <returns>True when one of its prefab stands where it stood</returns>
		internal static bool Stands(Dictionary<int, List<Vector3>> standing, PieceRecord record)
		{
			if (!standing.TryGetValue(record.Prefab, out List<Vector3> positions))
			{
				return(false);
			}
			foreach (Vector3 position in positions)
			{
				if (Vector3.Distance(position, record.Position) <= MatchReach)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether a noted piece stands, from the ZDOs around where it stood
		/// </summary>
		/// <param name="record">The piece</param>
		/// <returns>True when one of its prefab stands where it stood</returns>
		public static bool StandsAt(PieceRecord record)
		{
			List<ZDO> zdos = new List<ZDO>();
			VillageRespawn.ZdosNear(record.Position, MatchReach, zdos);
			foreach (ZDO zdo in zdos)
			{
				if (zdo.GetPrefab() == record.Prefab && Vector3.Distance(zdo.GetPosition(), record.Position) <= MatchReach)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Works out how much wood a prefab takes to build - all of its resources, at least 1
		/// </summary>
		/// <param name="prefab">The prefab's hash</param>
		/// <returns>The wood, or -1 when it isn't a building piece</returns>
		public static int WoodCost(int prefab)
		{
			if (woodByPrefab.TryGetValue(prefab, out int wood))
			{
				return(wood);
			}
			GameObject asset = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(prefab) : null);
			wood = -1;
			if (asset != null && asset.GetComponent<WearNTear>() != null)
			{
				wood = 0;
				Piece piece = asset.GetComponent<Piece>();
				if (piece != null && piece.m_resources != null)
				{
					foreach (Piece.Requirement requirement in piece.m_resources)
					{
						wood += Mathf.Max(0, requirement.m_amount);
					}
				}
				wood = Mathf.Max(1, wood);
			}
			if (asset != null)
			{
				woodByPrefab[prefab] = wood;
			}
			return(wood);
		}

		/// <summary>
		/// Writes a village's pieces to its generator, compressed (see Encode)
		/// </summary>
		/// <param name="generator">The generator's ZDO, owned by this client</param>
		/// <param name="origin">Where the generator stands</param>
		/// <param name="records">The pieces</param>
		private static void Write(ZDO generator, Vector3 origin, List<PieceRecord> records)
		{
			List<WrittenPiece> pieces = new List<WrittenPiece>(records.Count);
			foreach (PieceRecord record in records)
			{
				WrittenPiece piece = new WrittenPiece();
				piece.Prefab = record.Prefab;
				piece.Position = record.Position;
				piece.Angles = record.Rotation.eulerAngles;
				piece.Wood = record.Wood;
				pieces.Add(piece);
			}
			generator.Set(RecordsKey, Utils.Compress(Encode(origin, pieces)));
			generator.Set(RecordsVersionKey, FormatVersion);
		}

		/// <summary>
		/// Checks whether a piece stands near enough the generator for its position to be written - no farther than
		/// MaxOffset from it on any axis
		/// </summary>
		/// <param name="origin">Where the generator stands</param>
		/// <param name="position">Where the piece stands</param>
		/// <returns>True when it does</returns>
		internal static bool InReach(Vector3 origin, Vector3 position)
		{
			Vector3 offset = position - origin;
			return(Mathf.Abs(offset.x) <= MaxOffset && Mathf.Abs(offset.y) <= MaxOffset && Mathf.Abs(offset.z) <= MaxOffset);
		}

		/// <summary>
		/// Writes a village's pieces, uncompressed - FormatVersion and the origin, each prefab and its wood once, then
		/// each piece by its prefab's place in that list, its position from the origin in centimeters and its angles in
		/// 65536ths of a turn - leaving out the pieces out of reach of the origin (see InReach), whose positions don't fit
		/// </summary>
		/// <param name="origin">Where the generator stands</param>
		/// <param name="pieces">The pieces</param>
		/// <returns>The bytes</returns>
		internal static byte[] Encode(Vector3 origin, List<WrittenPiece> pieces)
		{
			List<WrittenPiece> kept = pieces.FindAll((WrittenPiece piece) => InReach(origin, piece.Position));
			List<WrittenPiece> palette = new List<WrittenPiece>();
			Dictionary<int, int> paletteIndex = new Dictionary<int, int>();
			foreach (WrittenPiece piece in kept)
			{
				if (!paletteIndex.ContainsKey(piece.Prefab))
				{
					paletteIndex[piece.Prefab] = palette.Count;
					palette.Add(piece);
				}
			}
			ZPackage pkg = new ZPackage();
			pkg.Write(FormatVersion);
			pkg.Write(origin);
			pkg.Write(palette.Count);
			foreach (WrittenPiece entry in palette)
			{
				pkg.Write(entry.Prefab);
				pkg.Write(entry.Wood);
			}
			pkg.Write(kept.Count);
			foreach (WrittenPiece piece in kept)
			{
				Vector3 offset = (piece.Position - origin) * PositionScale;
				pkg.Write((ushort)paletteIndex[piece.Prefab]);
				pkg.Write((short)Mathf.RoundToInt(offset.x));
				pkg.Write((short)Mathf.RoundToInt(offset.y));
				pkg.Write((short)Mathf.RoundToInt(offset.z));
				pkg.Write(Angle(piece.Angles.x));
				pkg.Write(Angle(piece.Angles.y));
				pkg.Write(Angle(piece.Angles.z));
			}
			return(pkg.GetArray());
		}

		/// <summary>
		/// Converts an angle to 65536ths of a turn
		/// </summary>
		/// <param name="degrees">The angle, in degrees</param>
		/// <returns>The angle as written</returns>
		internal static ushort Angle(float degrees)
		{
			return((ushort)(Mathf.RoundToInt(Mathf.Repeat(degrees, 360f) * AngleScale) & 0xFFFF));
		}

		/// <summary>
		/// Reads a village's pieces from its generator, reusing what was read last while they haven't changed
		/// </summary>
		/// <param name="generator">The generator's ZDO</param>
		/// <returns>The pieces, or none when they haven't been noted</returns>
		private static List<PieceRecord> Read(ZDO generator)
		{
			byte[] bytes = generator.GetByteArray(RecordsKey);
			if (bytes == null)
			{
				return(new List<PieceRecord>());
			}
			if (cache.TryGetValue(generator.m_uid, out KeyValuePair<byte[], List<PieceRecord>> cached) && ReferenceEquals(cached.Key, bytes))
			{
				return(cached.Value);
			}
			List<PieceRecord> records = new List<PieceRecord>();
			try
			{
				foreach (WrittenPiece piece in Decode(Utils.Decompress(bytes)))
				{
					PieceRecord record = new PieceRecord();
					record.Prefab = piece.Prefab;
					record.Wood = piece.Wood;
					record.Position = piece.Position;
					record.Rotation = Quaternion.Euler(piece.Angles);
					records.Add(record);
				}
			}
			catch (System.Exception exception)
			{
				Plugin.Log.LogWarning("Couldn't read the building pieces noted for the village at " + generator.GetPosition().ToString("F0") + ": " + exception.Message);
				records.Clear();
			}
			cache[generator.m_uid] = new KeyValuePair<byte[], List<PieceRecord>>(bytes, records);
			return(records);
		}

		/// <summary>
		/// Reads a village's pieces as Encode wrote them, uncompressed
		/// </summary>
		/// <param name="bytes">The bytes</param>
		/// <returns>The pieces, with their angles in degrees from 0 up to a full turn, or none when they were written
		/// another way than FormatVersion</returns>
		/// <exception cref="System.Exception">The bytes are cut short or don't hold pieces</exception>
		internal static List<WrittenPiece> Decode(byte[] bytes)
		{
			List<WrittenPiece> pieces = new List<WrittenPiece>();
			ZPackage pkg = new ZPackage(bytes);
			if (pkg.ReadInt() != FormatVersion)
			{
				return(pieces);
			}
			Vector3 origin = pkg.ReadVector3();
			int paletteCount = pkg.ReadInt();
			int[] prefabs = new int[paletteCount];
			int[] woods = new int[paletteCount];
			for (int i = 0; i < paletteCount; i++)
			{
				prefabs[i] = pkg.ReadInt();
				woods[i] = pkg.ReadInt();
			}
			int count = pkg.ReadInt();
			for (int i = 0; i < count; i++)
			{
				int index = pkg.ReadUShort();
				Vector3 offset = new Vector3(pkg.ReadShort(), pkg.ReadShort(), pkg.ReadShort()) / PositionScale;
				Vector3 angles = new Vector3(pkg.ReadUShort(), pkg.ReadUShort(), pkg.ReadUShort()) / AngleScale;
				WrittenPiece piece = new WrittenPiece();
				piece.Prefab = prefabs[index];
				piece.Wood = woods[index];
				piece.Position = origin + offset;
				piece.Angles = angles;
				pieces.Add(piece);
			}
			return(pieces);
		}
	}
}
