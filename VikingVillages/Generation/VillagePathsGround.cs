using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Reshaping the ground around a village - flattening it with the game's terrain operations, moving what stands in
	/// the way, and finding and leveling the spot for its fire
	/// </summary>
	internal static partial class VillagePaths
	{
		/// <summary>
		/// Reshapes the ground around a village to the grid's flattened heights, the way a level operation does, and
		/// moves what stands on the ground - trees, rocks, bushes and the rest - up or down with it
		/// </summary>
		/// <remarks>
		/// Each zone's terrain is changed through its own terrain compiler, which this client takes over so it can save
		/// the change for everyone. Building pieces stand where the ground isn't changed, so they're left alone
		/// </remarks>
		/// <param name="grid">The village's ground, flattened</param>
		/// <param name="min">The area's lowest corner</param>
		/// <param name="max">The area's highest corner</param>
		private static void Reshape(Grid grid, Vector3 min, Vector3 max)
		{
			Vector3 center = (min + max) / 2f;
			float radius = Utils.DistanceXZ(min, max) / 2f;
			List<Heightmap> maps = new List<Heightmap>();
			Heightmap.FindHeightmap(center, radius, maps);
			foreach (Heightmap map in maps)
			{
				TerrainComp terrain = map.GetAndCreateTerrainCompiler();
				if (terrain == null || !terrain.m_initialized || terrain.m_nview == null || !terrain.m_nview.IsValid())
				{
					continue;
				}
				terrain.m_nview.ClaimOwnership();
				int size = terrain.m_width + 1;
				int half = map.m_width / 2;
				bool changed = false;
				for (int i = 0; i < size; i++)
				{
					for (int j = 0; j < size; j++)
					{
						Vector3 vertex = map.transform.position + new Vector3((j - half) * map.m_scale, 0f, (i - half) * map.m_scale);
						if (!grid.TryGetChange(vertex, out float change) || Mathf.Abs(change) < MinFlatten)
						{
							continue;
						}
						// The same as a level operation - the smoothing is folded into the level, which takes the change
						int index = i * size + j;
						terrain.m_levelDelta[index] = Mathf.Clamp(terrain.m_levelDelta[index] + change + terrain.m_smoothDelta[index], -8f, 8f);
						terrain.m_smoothDelta[index] = 0f;
						terrain.m_modifiedHeight[index] = true;
						changed = true;
					}
				}
				if (changed)
				{
					terrain.Save();
					map.Poke(1, false);
				}
			}
			if (ClutterSystem.instance != null)
			{
				ClutterSystem.instance.ResetGrass(center, radius);
			}
			MoveObjects(grid, min, max);
		}

		/// <summary>
		/// Moves the objects standing on the ground around a village up or down with the ground under them, leaving
		/// building pieces, which stand where it isn't changed, and creatures and dropped items, which find the ground
		/// on their own
		/// </summary>
		/// <param name="grid">The village's ground, flattened</param>
		/// <param name="min">The area's lowest corner</param>
		/// <param name="max">The area's highest corner</param>
		private static void MoveObjects(Grid grid, Vector3 min, Vector3 max)
		{
			List<ZNetView> moving = new List<ZNetView>();
			foreach (ZNetView view in ZNetScene.instance.m_instances.Values)
			{
				if (view == null)
				{
					continue;
				}
				Vector3 position = view.transform.position;
				if (position.x < min.x || position.x > max.x || position.z < min.z || position.z > max.z)
				{
					continue;
				}
				GameObject target = view.gameObject;
				if (target.GetComponent<WearNTear>() != null || target.GetComponent<Character>() != null || target.GetComponent<ItemDrop>() != null || target.GetComponent<Ragdoll>() != null || target.GetComponent<TerrainComp>() != null || target.GetComponent<DungeonGenerator>() != null || target.GetComponent<LocationProxy>() != null)
				{
					continue;
				}
				moving.Add(view);
			}
			List<ZNetView> moved = new List<ZNetView>();
			foreach (ZNetView view in moving)
			{
				Vector3 position = view.transform.position;
				if (!grid.TryGetChange(position, out float change) || Mathf.Abs(change) < MinMove)
				{
					continue;
				}
				view.ClaimOwnership();
				position.y += change;
				view.transform.position = position;
				view.GetZDO().SetPosition(position);
				moved.Add(view);
			}
			// Objects that stand still don't follow their ZDO's position, so the other clients with them loaded are told
			// where they went
			if (moved.Count == 0 || ZRoutedRpc.instance == null)
			{
				return;
			}
			ZPackage pkg = new ZPackage();
			pkg.Write(moved.Count);
			foreach (ZNetView view in moved)
			{
				pkg.Write(view.GetZDO().m_uid);
				pkg.Write(view.transform.position);
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, MovedRpc, pkg);
		}

		/// <summary>
		/// Moves the objects another client moved with the ground around a village to where they went, on the clients
		/// that have them loaded
		/// </summary>
		/// <param name="sender">The client that moved them</param>
		/// <param name="pkg">Each object's ZDO and where it went</param>
		private static void OnMoved(long sender, ZPackage pkg)
		{
			if (ZNetScene.instance == null)
			{
				return;
			}
			int count = pkg.ReadInt();
			for (int i = 0; i < count; i++)
			{
				ZDOID id = pkg.ReadZDOID();
				Vector3 position = pkg.ReadVector3();
				GameObject found = ZNetScene.instance.FindInstance(id);
				ZNetView view = ((found != null) ? found.GetComponent<ZNetView>() : null);
				if (view != null && view.IsValid() && !view.IsOwner())
				{
					view.transform.position = position;
				}
			}
		}

		/// <summary>
		/// Finds each house's doors among the objects around the village - the doors within DoorReach of its walls
		/// </summary>
		/// <param name="houses">The houses, given their doors</param>
		/// <param name="blockers">The objects around the village</param>
		private static void FindDoors(List<House> houses, List<Blocker> blockers)
		{
			for (int i = 0; i < houses.Count; i++)
			{
				House house = houses[i];
				house.Doors = new List<Vector3>();
				foreach (Blocker blocker in blockers)
				{
					if (blocker.View != null && blocker.View.GetComponent<Door>() != null && Utils.DistanceXZ(blocker.View.transform.position, house.Center) <= house.HalfSize + DoorReach)
					{
						house.Doors.Add(blocker.View.transform.position);
					}
				}
				houses[i] = house;
			}
		}

		/// <summary>
		/// Finds the middle of some houses
		/// </summary>
		/// <param name="houses">The houses</param>
		/// <returns>The average of their middles</returns>
		private static Vector3 Middle(List<House> houses)
		{
			Vector3 sum = Vector3.zero;
			foreach (House house in houses)
			{
				sum += house.Center;
			}
			return((houses.Count > 0) ? sum / houses.Count : sum);
		}

		/// <summary>
		/// Finds the open ground for a bonfire nearest the middle of a farm, which its layout keeps clear of rooms, clear
		/// of its buildings and paths - flat ground if there's any, or else gently sloping ground to flatten for it
		/// </summary>
		/// <param name="grid">The village's ground, with its paths marked</param>
		/// <param name="middle">The middle of the farm</param>
		/// <param name="spot">Where the bonfire goes - at the height the ground is flattened to, when it is</param>
		/// <param name="flatten">Whether the ground there is flattened first</param>
		/// <returns>False when there's no ground for it</returns>
		private static bool FindFireSpot(Grid grid, Vector3 middle, out Vector3 spot, out bool flatten)
		{
			return(FindLevelSpot(grid, middle, BonfireRadius, 0f, BonfireSearch, out spot, out flatten));
		}

		/// <summary>
		/// Finds the open ground nearest a point with room all around it, clear of buildings and paths, the way a fire's
		/// is found - flat ground if there's any, or else gently sloping ground to flatten for it (see Flatten)
		/// </summary>
		/// <param name="grid">The village's ground, with its paths marked</param>
		/// <param name="middle">The point</param>
		/// <param name="radius">How much room it needs around it, no more than FlattenReach, in meters</param>
		/// <param name="least">How far from the point it stands at the least, in meters</param>
		/// <param name="search">How far from the point to look, in meters</param>
		/// <param name="spot">The spot - at the height the ground is flattened to, when it is</param>
		/// <param name="flatten">Whether the ground there is flattened first</param>
		/// <returns>False when there's no ground for it</returns>
		internal static bool FindLevelSpot(Grid grid, Vector3 middle, float radius, float least, float search, out Vector3 spot, out bool flatten)
		{
			flatten = false;
			if (grid.FindOpenSpot(middle, radius, least, search, FireFlatness, out spot, out float level))
			{
				return(true);
			}
			if (levelOp == null || !grid.FindOpenSpot(middle, radius, least, search, FlattenLimit, out spot, out level))
			{
				return(false);
			}
			// Levels the ground halfway between its highest and lowest points, so it's cut and filled evenly
			spot.y = level;
			flatten = true;
			return(true);
		}

		/// <summary>
		/// How far from a spot flattening it levels the ground (see Flatten), in meters
		/// </summary>
		public const float FlattenReach = LevelRadius;

		/// <summary>
		/// Levels the ground within FlattenReach of a spot to the spot's height, smoothing it into the ground around, the
		/// way the ground under a village's fire is
		/// </summary>
		/// <param name="spot">The spot, at the height to level to</param>
		/// <returns>False when the terrain operation isn't set up</returns>
		public static bool Flatten(Vector3 spot)
		{
			if (levelOp == null)
			{
				return(false);
			}
			Object.Instantiate(levelOp, spot, Quaternion.identity);
			return(true);
		}

		/// <summary>
		/// Builds a village's bonfire at the spot found for it, flattening the ground there first when it needs it
		/// </summary>
		/// <param name="spot">Where it goes (see FindFireSpot)</param>
		/// <param name="flatten">Whether the ground there is flattened first</param>
		private static void PlaceBonfire(Vector3 spot, bool flatten)
		{
			GameObject prefab = ZNetScene.instance.GetPrefab(BonfirePrefab);
			if (prefab == null)
			{
				Plugin.Log.LogWarning("Couldn't find the " + BonfirePrefab + " prefab to build a village's fire at " + spot.ToString("F0"));
				return;
			}
			if (flatten)
			{
				Object.Instantiate(levelOp, spot, Quaternion.identity);
			}
			// A fire is a building piece, which breaks without support unless it touches the ground
			spot.y -= FireSink;
			GameObject instance = Object.Instantiate(prefab, spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
			VillageRecords.MarkBuilt(instance);
			ZNetView view = instance.GetComponent<ZNetView>();
			if (view != null && view.GetZDO() != null)
			{
				view.GetZDO().Set(BonfireKey, true);
				// The vikings stack it with as much wood as it holds
				Fireplace fire = instance.GetComponent<Fireplace>();
				if (fire != null)
				{
					view.GetZDO().Set(ZDOVars.s_fuel, fire.m_maxFuel);
				}
			}
			Plugin.Log.LogInfo("Built a " + BonfirePrefab + " at " + spot.ToString("F1") + (flatten ? " on ground flattened for it" : ""));
		}

		/// <summary>
		/// Adds the terrain operations that flatten the ground for a fire and paint the ground under a house to the
		/// game's terrain operations, building them the first time
		/// </summary>
		/// <remarks>
		/// Whoever owns a piece of ground applies the operations on it, finding each by its name, so the operations have
		/// to be known on every client
		/// </remarks>
		/// <param name="db">The ObjectDB, as it wakes</param>
		public static void RegisterLevelOp(ObjectDB db)
		{
			TerrainOp path = db.m_terrainOps.Find((TerrainOp op) => op != null && op.name == PathPiece);
			if (levelOp == null)
			{
				if (path == null)
				{
					return;
				}
				levelOp = CopyOp(path, LevelOpName);
				TerrainOp level = levelOp.GetComponent<TerrainOp>();
				TerrainOp.Settings settings = new TerrainOp.Settings();
				settings.m_level = true;
				settings.m_levelRadius = LevelRadius;
				settings.m_square = false;
				settings.m_smooth = true;
				settings.m_smoothRadius = LevelSmoothRadius;
				settings.m_smoothPower = LevelSmoothPower;
				settings.m_paintCleared = true;
				settings.m_paintType = TerrainModifier.PaintType.Dirt;
				settings.m_paintRadius = LevelRadius;
				settings.m_paintCurve = path.m_settings.m_paintCurve;
				level.m_settings = settings;
				level.m_spawnOnPlaced = null;
			}
			if (floorOp == null && path != null)
			{
				floorOp = CopyOp(path, FloorOpName);
				TerrainOp floor = floorOp.GetComponent<TerrainOp>();
				TerrainOp.Settings settings = new TerrainOp.Settings();
				settings.m_level = false;
				settings.m_raise = false;
				settings.m_smooth = false;
				settings.m_paintCleared = true;
				settings.m_paintType = path.m_settings.m_paintType;
				settings.m_paintRadius = FloorPaintRadius;
				settings.m_paintCurve = path.m_settings.m_paintCurve;
				floor.m_settings = settings;
				floor.m_spawnOnPlaced = null;
			}
			if (snowOp == null && path != null)
			{
				snowOp = CopyOp(path, SnowOpName);
				TerrainOp snow = snowOp.GetComponent<TerrainOp>();
				TerrainOp.Settings settings = new TerrainOp.Settings();
				settings.m_level = false;
				settings.m_raise = false;
				settings.m_smooth = false;
				settings.m_paintCleared = true;
				settings.m_paintType = TerrainModifier.PaintType.Reset;
				settings.m_paintRadius = SnowPaintRadius;
				settings.m_paintCurve = path.m_settings.m_paintCurve;
				snow.m_settings = settings;
				snow.m_spawnOnPlaced = null;
			}
			Register(db, levelOp, LevelOpName);
			Register(db, floorOp, FloorOpName);
			Register(db, snowOp, SnowOpName);
		}

		/// <summary>
		/// Copies the hoe's path as a terrain operation of the mod's own, kept inactive and without the path's piece
		/// </summary>
		/// <param name="path">The hoe's path</param>
		/// <param name="name">The copy's name</param>
		/// <returns>The copy, whose settings are the caller's to set</returns>
		private static GameObject CopyOp(TerrainOp path, string name)
		{
			if (opHolder == null)
			{
				opHolder = new GameObject("VikingVillages terrain ops");
				opHolder.SetActive(false);
				Object.DontDestroyOnLoad(opHolder);
			}
			GameObject copy = Object.Instantiate(path.gameObject, opHolder.transform, false);
			copy.name = name;
			Piece piece = copy.GetComponent<Piece>();
			if (piece != null)
			{
				Object.DestroyImmediate(piece);
			}
			return(copy);
		}

		/// <summary>
		/// Adds one of the mod's terrain operations to the game's, by its name
		/// </summary>
		/// <param name="db">The ObjectDB</param>
		/// <param name="holder">The operation's object, or null when it couldn't be built</param>
		/// <param name="name">Its name</param>
		private static void Register(ObjectDB db, GameObject holder, string name)
		{
			TerrainOp op = ((holder != null) ? holder.GetComponent<TerrainOp>() : null);
			if (op == null)
			{
				return;
			}
			if (!db.m_terrainOps.Contains(op))
			{
				db.m_terrainOps.Add(op);
			}
			db.m_terrainOpsByHash[name.GetStableHashCode()] = op;
		}
	}
}
