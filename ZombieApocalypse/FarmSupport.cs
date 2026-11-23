using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Keeps a farm's pieces from breaking for want of support where they were generated over uneven ground - a fence
	/// section left floating is lowered onto the ground, or taken away when the ground falls away too far under it, and
	/// a house's floor, wall, stair or pole that reaches neither the ground nor a piece under it gets a column of stone
	/// under it, down into the ground
	/// </summary>
	/// <remarks>
	/// A piece is held up when it touches the ground or rests on one that is, and breaks within seconds when it isn't.
	/// The pieces are put right on the client that owns the village, once its paths are laid and the ground they reshape
	/// has settled (see SupportDelay), so until then a farm's own pieces are kept from wearing at all, for HoldMax seconds
	/// at most. Each farm is put right once, noted in its generator's ZDO
	/// </remarks>
	internal static class FarmSupport
	{
		/// <summary>
		/// On a farm's generator, that its pieces have been put right
		/// </summary>
		private static readonly int SupportedKey = "kriona.ZombieApocalypse.villageSupported".GetStableHashCode();

		/// <summary>
		/// The start of the names of the game's farm locations
		/// </summary>
		private const string FarmPrefix = "WoodFarm";

		/// <summary>
		/// How far from a farm's location its pieces can stand to be kept from wearing until it's put right - its widest
		/// fence and the empty houses near it it takes in - and the most seconds they're kept from it, in case it never is
		/// </summary>
		private const float HoldReach = 110f;
		private const float HoldMax = 120f;

		/// <summary>
		/// Seconds after a village's paths are laid before its pieces are put right, so the ground they reshaped is in
		/// place
		/// </summary>
		private const float SupportDelay = 2f;

		/// <summary>
		/// How far past its houses' walls a piece still counts as one of them, and past the widest fence the fence
		/// stands, in meters
		/// </summary>
		private const float HouseMargin = 2f;
		private const float FenceMargin = 12f;

		/// <summary>
		/// How deep under a piece's bottom what holds it up is looked for, how far a fence section can be lowered before
		/// it's taken away instead, how high a stone column can stand, and how far it's sunk into the ground, in meters
		/// </summary>
		private const float UnderDepth = 0.15f;
		private const float FenceDropMax = 2f;
		private const float ColumnMax = 4f;
		private const float ColumnSink = 0.3f;

		/// <summary>
		/// The stone piece a column is stacked of, a meter on each side with its pivot in its middle
		/// </summary>
		private const string ColumnPiece = "stone_wall_1x1";

		/// <summary>
		/// The pieces kept from wearing, with the farm each belongs to and when it started being kept, whether each farm
		/// loaded is put right yet, by its location, and when each village's paths were laid on this client
		/// </summary>
		private struct Held
		{
			public Location Farm;
			public float Since;
		}
		private static readonly Dictionary<WearNTear, Held> held = new Dictionary<WearNTear, Held>();
		private static readonly Dictionary<Location, bool> supported = new Dictionary<Location, bool>();
		private static readonly Dictionary<ZDOID, float> laidAt = new Dictionary<ZDOID, float>();

		private static int solidMask;

		/// <summary>
		/// Keeps a piece no player built from wearing as it wakes, when it stands at a farm that hasn't been put right yet
		/// </summary>
		/// <param name="piece">The piece</param>
		public static void OnAwake(WearNTear piece)
		{
			if (!Plugin.VikingVillages.Value || piece == null || piece.m_nview == null || !piece.m_nview.IsValid() || piece.m_nview.GetZDO().GetLong(ZDOVars.s_creator, 0L) != 0L)
			{
				return;
			}
			Location farm = FarmNear(piece.transform.position);
			if (farm != null && !IsSupported(farm))
			{
				held[piece] = new Held { Farm = farm, Since = Time.time };
			}
		}

		/// <summary>
		/// Checks whether a piece is kept from wearing - letting it go once it has been for HoldMax seconds
		/// </summary>
		/// <param name="piece">The piece</param>
		/// <returns>True when it is</returns>
		public static bool Holds(WearNTear piece)
		{
			if (held.Count == 0 || !held.TryGetValue(piece, out Held hold))
			{
				return(false);
			}
			if (Time.time - hold.Since < HoldMax)
			{
				return(true);
			}
			held.Remove(piece);
			return(false);
		}

		/// <summary>
		/// Notes that a village's paths were just laid, so its pieces are put right SupportDelay seconds later
		/// </summary>
		/// <param name="generator">The farm's generator</param>
		public static void NoteLaid(ZDO generator)
		{
			laidAt[generator.m_uid] = Time.time;
		}

		/// <summary>
		/// Forgets the pieces and farms of the world being left
		/// </summary>
		public static void Forget()
		{
			held.Clear();
			supported.Clear();
			laidAt.Clear();
		}

		/// <summary>
		/// Puts a village's pieces right once, on the client that owns it, once its paths are laid and SupportDelay has
		/// passed since - then lets its pieces wear again
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		public static void TrySupport(Village village)
		{
			ZDO zdo = village.Zdo;
			if (zdo == null || zdo.GetBool(SupportedKey) || zdo.GetPrefab() != VillagePaths.GeneratorHash || !VillagePaths.Laid(zdo))
			{
				return;
			}
			if (laidAt.TryGetValue(zdo.m_uid, out float laid) && Time.time - laid < SupportDelay)
			{
				return;
			}
			zdo.Set(SupportedKey, true);
			if (solidMask == 0)
			{
				solidMask = LayerMask.GetMask("piece", "Default", "static_solid", "Default_small", "terrain");
			}
			GameObject column = ZNetScene.instance.GetPrefab(ColumnPiece);
			float fence = zdo.GetFloat(VillageFarmhouses.FenceKey, VillageFarmhouses.FenceRadiusMax) + FenceMargin;
			List<WearNTear> pieces = new List<WearNTear>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZNetView view = instance.Value;
				WearNTear piece = ((view != null) ? view.GetComponent<WearNTear>() : null);
				if (piece == null || instance.Key.GetLong(ZDOVars.s_creator, 0L) != 0L)
				{
					continue;
				}
				Vector3 position = view.transform.position;
				if (Utils.DistanceXZ(position, village.Farm) <= fence || village.Houses.Exists((VillagePaths.House house) => Village.InHouse(house, position, HouseMargin)))
				{
					pieces.Add(piece);
				}
			}
			int lowered = 0;
			int removed = 0;
			int columns = 0;
			int columned = 0;
			int unsnowed = 0;
			foreach (WearNTear piece in pieces)
			{
				if (piece != null && ClearSnow(piece))
				{
					unsnowed++;
				}
			}
			foreach (WearNTear piece in pieces)
			{
				if (piece == null || !Bounds(piece, out Bounds bounds) || Underneath(piece, bounds))
				{
					continue;
				}
				string name = Utils.GetPrefabName(piece.gameObject).ToLowerInvariant();
				if (name.Contains("fence"))
				{
					if (Lower(piece, bounds))
					{
						lowered++;
					}
					else
					{
						piece.m_nview.ClaimOwnership();
						ZNetScene.instance.Destroy(piece.gameObject);
						removed++;
					}
					continue;
				}
				if (column != null && Foundational(piece, name))
				{
					int stacked = Column(bounds, column);
					if (stacked > 0)
					{
						columns += stacked;
						columned++;
					}
				}
			}
			Physics.SyncTransforms();
			// The stone blocks are noted among the village's pieces with the rest as it's set up, so they're built back too
			Release(village.Farm);
			Plugin.Log.LogInfo("Supported the pieces of the village at " + village.Farm.ToString("F0") + ": " + pieces.Count + " looked at, " + lowered + " fence sections lowered onto the ground, " + removed + " taken away over ground that falls too far, " + columns + " stone blocks under " + columned + " house pieces, and the snow cleared off " + unsnowed + " pieces in the Deep North");
		}

		/// <summary>
		/// Clears the snow a farm's piece in the Deep North starts with - the game has every piece of a generated room
		/// start fully snowed over - so its houses don't start out buried. Snow builds up on it again from there as it
		/// does on any piece out in the open, which the vikings clear off (see VillageSnow)
		/// </summary>
		/// <param name="piece">The piece</param>
		/// <returns>True when it was in the Deep North and had snow to clear</returns>
		private static bool ClearSnow(WearNTear piece)
		{
			Vector3 position = piece.transform.position;
			if (!WorldGenerator.IsDeepnorth(position.x, position.z))
			{
				return(false);
			}
			ZDO zdo = piece.m_nview.GetZDO();
			bool snowed = (piece.m_addPreSnow || zdo.GetBool(ZDOVars.s_preSnow) || zdo.GetFloat(ZDOVars.s_snow) > 0f);
			if (!snowed)
			{
				return(false);
			}
			piece.m_addPreSnow = false;
			zdo.Set(ZDOVars.s_preSnow, false);
			piece.ChangeSnow(-1f);
			return(true);
		}

		/// <summary>
		/// Lets go of the pieces kept from wearing at the farm a village stands at, and notes the farm as put right
		/// </summary>
		/// <param name="farm">Where the village's farm stands</param>
		private static void Release(Vector3 farm)
		{
			List<WearNTear> gone = new List<WearNTear>();
			foreach (KeyValuePair<WearNTear, Held> entry in held)
			{
				if (entry.Key == null || entry.Value.Farm == null || Utils.DistanceXZ(entry.Value.Farm.transform.position, farm) <= HoldReach)
				{
					gone.Add(entry.Key);
					if (entry.Value.Farm != null)
					{
						supported[entry.Value.Farm] = true;
					}
				}
			}
			foreach (WearNTear piece in gone)
			{
				held.Remove(piece);
			}
			Location location = FarmNear(farm);
			if (location != null)
			{
				supported[location] = true;
			}
		}

		/// <summary>
		/// Finds the farm location a point stands near enough for its pieces to be the farm's
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>The farm, or null when there's none that near</returns>
		private static Location FarmNear(Vector3 point)
		{
			foreach (Location location in Location.s_allLocations)
			{
				if (location != null && Utils.DistanceXZ(location.transform.position, point) <= HoldReach && Utils.GetPrefabName(location.gameObject).StartsWith(FarmPrefix, System.StringComparison.OrdinalIgnoreCase))
				{
					return(location);
				}
			}
			return(null);
		}

		/// <summary>
		/// Checks whether a farm has been put right - or never needs to be, as one whose paths were laid before farms
		/// were put right - by its generator, once it's there
		/// </summary>
		/// <param name="farm">The farm's location</param>
		/// <returns>True when it has</returns>
		private static bool IsSupported(Location farm)
		{
			if (supported.TryGetValue(farm, out bool known))
			{
				return(known);
			}
			ZDO generator = VillageRespawn.FindGenerator(farm.transform.position);
			if (generator == null)
			{
				return(false);
			}
			bool done = (generator.GetBool(SupportedKey) || (VillagePaths.Laid(generator) && !laidAt.ContainsKey(generator.m_uid)));
			supported[farm] = done;
			return(done);
		}

		/// <summary>
		/// Measures a piece's solid colliders, in the world
		/// </summary>
		/// <param name="piece">The piece</param>
		/// <param name="bounds">Set to the space they take</param>
		/// <returns>False when it has none</returns>
		private static bool Bounds(WearNTear piece, out Bounds bounds)
		{
			bounds = new Bounds();
			bool any = false;
			foreach (Collider collider in piece.GetComponentsInChildren<Collider>())
			{
				if (collider == null || collider.isTrigger || !collider.enabled)
				{
					continue;
				}
				if (any)
				{
					bounds.Encapsulate(collider.bounds);
				}
				else
				{
					bounds = collider.bounds;
					any = true;
				}
			}
			return(any);
		}

		/// <summary>
		/// Checks whether anything holds a piece up from underneath - the ground, a building piece or anything else solid
		/// just under its bottom, the way the game looks for what supports it
		/// </summary>
		/// <param name="piece">The piece</param>
		/// <param name="bounds">The space it takes</param>
		/// <returns>True when something does</returns>
		private static bool Underneath(WearNTear piece, Bounds bounds)
		{
			Vector3 center = new Vector3(bounds.center.x, bounds.min.y - UnderDepth / 2f, bounds.center.z);
			Vector3 half = new Vector3(Mathf.Max(0.05f, bounds.extents.x * 0.9f), UnderDepth, Mathf.Max(0.05f, bounds.extents.z * 0.9f));
			foreach (Collider hit in Physics.OverlapBox(center, half, Quaternion.identity, solidMask, QueryTriggerInteraction.Ignore))
			{
				if (hit != null && hit.attachedRigidbody == null && hit.GetComponentInParent<WearNTear>() != piece)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Lowers a fence section left floating onto the ground - until the highest ground under it meets its bottom - so
		/// it touches it, unless that's more than FenceDropMax down
		/// </summary>
		/// <param name="piece">The fence section</param>
		/// <param name="bounds">The space it takes</param>
		/// <returns>False when it would have to go too far down, so it's taken away instead</returns>
		private static bool Lower(WearNTear piece, Bounds bounds)
		{
			float ground = float.MinValue;
			foreach (Vector3 point in new[] { bounds.center, new Vector3(bounds.min.x, 0f, bounds.min.z), new Vector3(bounds.min.x, 0f, bounds.max.z), new Vector3(bounds.max.x, 0f, bounds.min.z), new Vector3(bounds.max.x, 0f, bounds.max.z) })
			{
				ground = Mathf.Max(ground, ZoneSystem.instance.GetGroundHeight(point));
			}
			float drop = bounds.min.y - ground + 0.05f;
			if (drop > FenceDropMax)
			{
				return(false);
			}
			piece.m_nview.ClaimOwnership();
			piece.transform.position -= new Vector3(0f, drop, 0f);
			piece.m_nview.GetZDO().SetPosition(piece.transform.position);
			// It works out what holds it up again from where it stands now
			piece.m_colliders = null;
			return(true);
		}

		/// <summary>
		/// Checks whether a piece is part of a house's frame that a column holds up - a floor, wall, stair or pole, not a
		/// roof, decoration or piece of furniture
		/// </summary>
		/// <param name="piece">The piece</param>
		/// <param name="name">Its prefab name, in lowercase</param>
		/// <returns>True when it is</returns>
		private static bool Foundational(WearNTear piece, string name)
		{
			Piece part = piece.GetComponent<Piece>();
			if (part == null || (part.m_category != Piece.PieceCategory.BuildingWorkbench && part.m_category != Piece.PieceCategory.BuildingStonecutter) || name.Contains("roof"))
			{
				return(false);
			}
			return(name.Contains("floor") || name.Contains("wall") || name.Contains("stair") || name.Contains("pole"));
		}

		/// <summary>
		/// Stacks stone blocks under the middle of a piece from its bottom down into the ground, when the ground under it
		/// is no more than ColumnMax down, as pieces no player built
		/// </summary>
		/// <param name="bounds">The space the piece takes</param>
		/// <param name="column">The stone block</param>
		/// <returns>How many blocks were stacked, or 0 when the ground is too far down</returns>
		private static int Column(Bounds bounds, GameObject column)
		{
			Vector3 under = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
			float ground = ZoneSystem.instance.GetGroundHeight(under);
			if (bounds.min.y - ground > ColumnMax)
			{
				return(0);
			}
			int stacked = 0;
			for (float top = bounds.min.y; top > ground - ColumnSink; top -= 1f)
			{
				GameObject block = Object.Instantiate(column, new Vector3(under.x, top - 0.5f, under.z), Quaternion.identity);
				VillageRecords.MarkBuilt(block);
				WearNTear wear = block.GetComponent<WearNTear>();
				if (wear != null)
				{
					wear.OnPlaced();
				}
				stacked++;
			}
			return(stacked);
		}
	}
}
