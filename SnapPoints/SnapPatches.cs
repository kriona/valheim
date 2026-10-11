using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SnapPoints
{
	internal static class Snapping
	{
		/// <summary>
		/// The tag Piece.GetSnapPoints looks for on a piece's direct children
		/// </summary>
		private const string SnapPointTag = "snappoint";

		/// <summary>
		/// Names of the snap points added to a piece, in the order of the corners from GetCorners - the game shows the
		/// name when cycling through snap points by hand, and they mark the snap points as this mod's
		/// </summary>
		private static readonly string[] CornerNames = { "bottom corner 1", "bottom corner 2", "bottom corner 3", "bottom corner 4", "top corner 1", "top corner 2", "top corner 3", "top corner 4" };

		/// <summary>
		/// Names of the snap points added at the corners of a piece's inner part, in the order from GetInnerCorners
		/// </summary>
		private static readonly string[] InnerNames = { "inner corner 1", "inner corner 2", "inner corner 3", "inner corner 4" };

		/// <summary>
		/// Names of the snap points added on the square around a piece's center, in the order from GetOuterCorners
		/// </summary>
		private static readonly string[] OuterNames = { "outer corner 1", "outer corner 2", "outer corner 3", "outer corner 4" };

		/// <summary>
		/// Pieces that hold something wider than their colliders, by prefab name prefix, with half the width of the
		/// square of snap points around their center - an armor stand's collider is only its body, so stands snapped by
		/// it stand shoulder to shoulder
		/// </summary>
		private static readonly Dictionary<string, float> outerSquares = new Dictionary<string, float>
		{
			{ "ArmorStand", 0.5f },
		};

		/// <summary>
		/// Pieces with a flat part that has no collider but should still line up, by prefab name prefix, with the name of
		/// that part's child - a banner's collider is only its wooden beam, which is wider than the cloth hanging from it
		/// </summary>
		private static readonly Dictionary<string, string> innerParts = new Dictionary<string, string>
		{
			{ "piece_banner", "default" },
		};

		/// <summary>
		/// Thin pieces that sit flat on a wall or floor, by prefab name prefix - when they snap to a wall or floor they
		/// stay on the surface they are aimed at, since a wall's snap points run through its middle and a floor's sit
		/// below the top of its planks, which would bury them inside it
		/// </summary>
		private static readonly string[] surfacePieces = { "itemstand", "rug_", "jute_carpet" };

		/// <summary>
		/// Corner positions in each piece's local space by prefab name, worked out from the prefab once
		/// </summary>
		private static readonly Dictionary<string, Vector3[]> cornersByPrefab = new Dictionary<string, Vector3[]>();

		/// <summary>
		/// Inner part corner positions in each piece's local space by prefab name, null for pieces without an inner part
		/// </summary>
		private static readonly Dictionary<string, Vector3[]> innerByPrefab = new Dictionary<string, Vector3[]>();

		/// <summary>
		/// Outer square corner positions in each piece's local space by prefab name, null for pieces without one
		/// </summary>
		private static readonly Dictionary<string, Vector3[]> outerByPrefab = new Dictionary<string, Vector3[]>();

		/// <summary>
		/// Adds or removes corner snap points on every piece prefab, the pieces already in the world and the placement
		/// ghost so the pieces set to true in the config file have them
		/// </summary>
		public static void ApplyAll()
		{
			if (ZNetScene.instance == null)
			{
				return;
			}
			foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
			{
				if (prefab != null && prefab.GetComponent<Piece>() != null)
				{
					Apply(prefab.transform, prefab.name, (PieceConfig.GetState(prefab.name) == true), true);
				}
			}
			foreach (Piece piece in Piece.s_allPieces)
			{
				if (piece != null)
				{
					ApplyToInstance(piece.gameObject);
				}
			}
			if (Player.m_localPlayer != null && Player.m_localPlayer.m_placementGhost != null)
			{
				ApplyToInstance(Player.m_localPlayer.m_placementGhost);
			}
		}

		/// <summary>
		/// Matches a piece in the world to its prefab's snap points
		/// </summary>
		/// <param name="instance">A placed piece or placement ghost</param>
		private static void ApplyToInstance(GameObject instance)
		{
			string name = Utils.GetPrefabName(instance);
			GameObject prefab = ZNetScene.instance.GetPrefab(name);
			if (prefab != null)
			{
				Apply(instance.transform, name, HasOwnSnapPoints(prefab.transform), false);
			}
		}

		/// <summary>
		/// Adds snap points at the corners of a piece's collider box, or removes the ones added before
		/// </summary>
		/// <param name="root">The piece's root transform</param>
		/// <param name="prefabName">The piece's prefab name</param>
		/// <param name="want">Whether the piece should have the snap points</param>
		/// <param name="isPrefab">Whether the piece is the prefab asset rather than an instance in the world</param>
		private static void Apply(Transform root, string prefabName, bool want, bool isPrefab)
		{
			bool has = HasOwnSnapPoints(root);
			if (want == has)
			{
				return;
			}
			if (!want)
			{
				for (int i = root.childCount - 1; i >= 0; i--)
				{
					Transform child = root.GetChild(i);
					if (!IsOwnSnapPoint(child))
					{
						continue;
					}
					if (isPrefab)
					{
						UnityEngine.Object.DestroyImmediate(child.gameObject, true);
					}
					else
					{
						// Detached first so GetSnapPoints stops finding it before Destroy runs at the end of the frame
						child.SetParent(null, false);
						UnityEngine.Object.Destroy(child.gameObject);
					}
				}
				return;
			}
			if (!cornersByPrefab.TryGetValue(prefabName, out Vector3[] corners))
			{
				GameObject prefab = (isPrefab ? root.gameObject : ZNetScene.instance.GetPrefab(prefabName));
				corners = GetCorners(prefab.transform);
				cornersByPrefab[prefabName] = corners;
				innerByPrefab[prefabName] = GetInnerCorners(prefab.transform, prefabName);
				outerByPrefab[prefabName] = GetOuterCorners(corners, prefabName);
				if (corners == null)
				{
					Plugin.Log.LogWarning(prefabName + " has no collider to place snap points around");
				}
			}
			if (corners == null)
			{
				return;
			}
			AddSnapPoints(root, CornerNames, corners);
			Vector3[] inner = innerByPrefab[prefabName];
			if (inner != null)
			{
				AddSnapPoints(root, InnerNames, inner);
			}
			Vector3[] outer = outerByPrefab[prefabName];
			if (outer != null)
			{
				AddSnapPoints(root, OuterNames, outer);
			}
		}

		/// <summary>
		/// The four corners of a square around a piece's center at the bottom of its collider box, for pieces that hold
		/// something wider than their colliders
		/// </summary>
		/// <param name="corners">The collider box's corners from GetCorners, or null</param>
		/// <param name="prefabName">The piece's prefab name</param>
		/// <returns>The corners, or null when the piece has no outer square or no collider</returns>
		private static Vector3[] GetOuterCorners(Vector3[] corners, string prefabName)
		{
			if (corners == null)
			{
				return(null);
			}
			float half = 0f;
			foreach (KeyValuePair<string, float> square in outerSquares)
			{
				if (prefabName.StartsWith(square.Key, StringComparison.OrdinalIgnoreCase))
				{
					half = square.Value;
					break;
				}
			}
			if (half <= 0f)
			{
				return(null);
			}
			// corners[0] is the low corner and corners[6] the high one
			Vector3 center = (corners[0] + corners[6]) * 0.5f;
			float bottom = corners[0].y;
			return(new Vector3[]
			{
				new Vector3(center.x - half, bottom, center.z - half),
				new Vector3(center.x + half, bottom, center.z - half),
				new Vector3(center.x + half, bottom, center.z + half),
				new Vector3(center.x - half, bottom, center.z + half)
			});
		}

		/// <summary>
		/// Adds a tagged child to a piece at each position
		/// </summary>
		/// <param name="root">The piece's root transform</param>
		/// <param name="names">The snap points' names, one per position</param>
		/// <param name="positions">Positions in the piece's local space</param>
		private static void AddSnapPoints(Transform root, string[] names, Vector3[] positions)
		{
			for (int i = 0; i < positions.Length; i++)
			{
				GameObject point = new GameObject(names[i]);
				point.tag = SnapPointTag;
				point.transform.SetParent(root, false);
				point.transform.localPosition = positions[i];
			}
		}

		/// <summary>
		/// The four corners of a piece's flat inner part, like a banner's cloth, in the piece's local space
		/// </summary>
		/// <remarks>
		/// The corners lie across the part's two widest directions, halfway through its thinnest
		/// </remarks>
		/// <param name="root">The piece prefab's root transform</param>
		/// <param name="prefabName">The piece's prefab name</param>
		/// <returns>The corners, or null when the piece has no inner part</returns>
		private static Vector3[] GetInnerCorners(Transform root, string prefabName)
		{
			string partName = null;
			foreach (KeyValuePair<string, string> part in innerParts)
			{
				if (prefabName.StartsWith(part.Key, StringComparison.OrdinalIgnoreCase))
				{
					partName = part.Value;
					break;
				}
			}
			Transform child = (partName == null ? null : root.Find(partName));
			MeshFilter filter = (child == null ? null : child.GetComponent<MeshFilter>());
			if (filter == null || filter.sharedMesh == null)
			{
				return(null);
			}
			bool any = false;
			Bounds bounds = new Bounds();
			Encapsulate(ref bounds, ref any, filter.sharedMesh.bounds, root.worldToLocalMatrix * child.localToWorldMatrix);
			Vector3 size = bounds.size;
			int thin = 0;
			for (int axis = 1; axis < 3; axis++)
			{
				if (size[axis] < size[thin])
				{
					thin = axis;
				}
			}
			int first = ((thin == 0) ? 1 : 0);
			int second = ((thin == 2) ? 1 : 2);
			Vector3[] corners = new Vector3[4];
			for (int i = 0; i < 4; i++)
			{
				Vector3 corner = bounds.center;
				corner[first] = (((i == 1) || (i == 2)) ? bounds.max[first] : bounds.min[first]);
				corner[second] = ((i >= 2) ? bounds.max[second] : bounds.min[second]);
				corners[i] = corner;
			}
			return(corners);
		}

		/// <summary>
		/// Grows a box to hold another box after moving that box into the piece's space
		/// </summary>
		/// <param name="bounds">The box being grown</param>
		/// <param name="any">Whether bounds holds anything yet, set to true</param>
		/// <param name="local">The box to add, in its own transform's space</param>
		/// <param name="toRoot">Moves points from the box's space into the piece's space</param>
		private static void Encapsulate(ref Bounds bounds, ref bool any, Bounds local, Matrix4x4 toRoot)
		{
			Vector3 min = local.min;
			Vector3 max = local.max;
			for (int i = 0; i < 8; i++)
			{
				Vector3 corner = new Vector3(((i & 1) == 0 ? min.x : max.x), ((i & 2) == 0 ? min.y : max.y), ((i & 4) == 0 ? min.z : max.z));
				Vector3 point = toRoot.MultiplyPoint3x4(corner);
				if (!any)
				{
					bounds = new Bounds(point, Vector3.zero);
					any = true;
				}
				else
				{
					bounds.Encapsulate(point);
				}
			}
		}

		/// <summary>
		/// The corners of the box around a piece's solid colliders, in the piece's local space
		/// </summary>
		/// <remarks>
		/// Collider.bounds is empty on a prefab that isn't in the scene, so each collider's own shape is transformed
		/// into the piece's space instead
		/// </remarks>
		/// <param name="root">The piece prefab's root transform</param>
		/// <returns>The bottom four corners followed by the top four, or null when the piece has no solid collider</returns>
		private static Vector3[] GetCorners(Transform root)
		{
			bool any = false;
			Bounds bounds = new Bounds();
			foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
			{
				if (collider.isTrigger || !IsActiveUnder(collider.transform, root) || !TryGetLocalBounds(collider, out Bounds local))
				{
					continue;
				}
				Encapsulate(ref bounds, ref any, local, root.worldToLocalMatrix * collider.transform.localToWorldMatrix);
			}
			if (!any)
			{
				return(null);
			}
			Vector3 low = bounds.min;
			Vector3 high = bounds.max;
			return(new Vector3[]
			{
				new Vector3(low.x, low.y, low.z),
				new Vector3(high.x, low.y, low.z),
				new Vector3(high.x, low.y, high.z),
				new Vector3(low.x, low.y, high.z),
				new Vector3(low.x, high.y, low.z),
				new Vector3(high.x, high.y, low.z),
				new Vector3(high.x, high.y, high.z),
				new Vector3(low.x, high.y, high.z)
			});
		}

		/// <summary>
		/// The box around a collider's shape in its own transform's space
		/// </summary>
		/// <param name="collider">The collider to measure</param>
		/// <param name="bounds">Set to the collider's box</param>
		/// <returns>False for a collider type that isn't measured or a mesh collider with no mesh</returns>
		private static bool TryGetLocalBounds(Collider collider, out Bounds bounds)
		{
			bounds = new Bounds();
			if (collider is BoxCollider box)
			{
				bounds = new Bounds(box.center, box.size);
			}
			else if (collider is SphereCollider sphere)
			{
				bounds = new Bounds(sphere.center, Vector3.one * (sphere.radius * 2f));
			}
			else if (collider is CapsuleCollider capsule)
			{
				Vector3 size = Vector3.one * (capsule.radius * 2f);
				size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2f);
				bounds = new Bounds(capsule.center, size);
			}
			else if (collider is MeshCollider mesh && mesh.sharedMesh != null)
			{
				bounds = mesh.sharedMesh.bounds;
			}
			else
			{
				return(false);
			}
			return(true);
		}

		/// <summary>
		/// Whether a transform and its parents up to the root are active, which works on a prefab that isn't in the
		/// scene where activeInHierarchy doesn't
		/// </summary>
		/// <param name="transform">The transform to check</param>
		/// <param name="root">The piece's root, which isn't checked</param>
		/// <returns>True when nothing between them is inactive</returns>
		private static bool IsActiveUnder(Transform transform, Transform root)
		{
			for (Transform current = transform; current != null && current != root; current = current.parent)
			{
				if (!current.gameObject.activeSelf)
				{
					return(false);
				}
			}
			return(true);
		}

		/// <summary>
		/// Whether a child is a snap point this mod added
		/// </summary>
		/// <param name="child">A direct child of a piece</param>
		/// <returns>True for a snap point with one of the corner, inner corner or outer corner names</returns>
		public static bool IsOwnSnapPoint(Transform child)
		{
			if (!child.CompareTag(SnapPointTag))
			{
				return(false);
			}
			string name = child.name;
			return(Array.IndexOf(CornerNames, name) >= 0 || Array.IndexOf(InnerNames, name) >= 0 || Array.IndexOf(OuterNames, name) >= 0);
		}

		/// <summary>
		/// Whether a child is one of the top corner snap points this mod added
		/// </summary>
		/// <param name="child">A direct child of a piece</param>
		/// <returns>True for a snap point with one of the top corner names</returns>
		public static bool IsOwnTopCorner(Transform child)
		{
			return(child.CompareTag(SnapPointTag) && Array.IndexOf(CornerNames, child.name) >= 4);
		}

		/// <summary>
		/// Whether a piece sits flat on a surface and stays on it when it snaps to the piece it sits on
		/// </summary>
		/// <param name="prefabName">The piece's prefab name</param>
		/// <returns>True for a surface piece</returns>
		public static bool IsSurfacePiece(string prefabName)
		{
			foreach (string prefix in surfacePieces)
			{
				if (prefabName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Whether other pieces can be built on top of a piece
		/// </summary>
		/// <param name="piece">A piece, prefab or placement ghost</param>
		/// <returns>False when the piece's WearNTear doesn't support what's built on it</returns>
		public static bool Supports(GameObject piece)
		{
			WearNTear wearNTear = piece.GetComponent<WearNTear>();
			return(wearNTear == null || wearNTear.m_supports);
		}

		/// <summary>
		/// Whether a piece has the snap points this mod adds
		/// </summary>
		/// <param name="root">The piece's root transform</param>
		/// <returns>True when any child is one of this mod's snap points</returns>
		private static bool HasOwnSnapPoints(Transform root)
		{
			for (int i = 0; i < root.childCount; i++)
			{
				if (IsOwnSnapPoint(root.GetChild(i)))
				{
					return(true);
				}
			}
			return(false);
		}
	}

	/// <summary>
	/// Reads the config file and gives the pieces their snap points once the prefabs are registered, before any piece
	/// is placed or loaded into the world
	/// </summary>
	[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
	public static class ZNetSceneAwakePatch
	{
		/// <summary>
		/// Passes the scene's prefabs to the build piece list, has the watcher take the file it writes as already read
		/// so it isn't reported as a reload, then adds the snap points to the piece prefabs
		/// </summary>
		/// <param name="__instance">The scene that just started</param>
		private static void Postfix(ZNetScene __instance)
		{
			PieceConfig.Load(__instance);
			Plugin.Watcher?.Remember();
			Snapping.ApplyAll();
		}
	}

	/// <summary>
	/// Takes away every snap point of a piece set to false, both on the piece being placed and on the pieces it could
	/// snap to
	/// </summary>
	[HarmonyPatch(typeof(Piece), nameof(Piece.GetSnapPoints), new Type[] { typeof(List<Transform>) })]
	public static class PieceGetOwnSnapPointsPatch
	{
		/// <summary>
		/// Notes how many snap points the list held before this piece's were added
		/// </summary>
		/// <param name="points">The list the piece's snap points are added to</param>
		/// <param name="__state">Set to the list's count before</param>
		private static void Prefix(List<Transform> points, out int __state)
		{
			__state = points.Count;
		}

		/// <summary>
		/// Removes the snap points this piece added when it is set to false
		/// </summary>
		/// <param name="__instance">The piece</param>
		/// <param name="points">The list the piece's snap points were added to</param>
		/// <param name="__state">The list's count before</param>
		private static void Postfix(Piece __instance, List<Transform> points, int __state)
		{
			if (!PieceConfig.AnyOff || points.Count <= __state)
			{
				return;
			}
			if (PieceConfig.GetState(Utils.GetPrefabName(__instance.gameObject)) == false)
			{
				points.RemoveRange(__state, points.Count - __state);
			}
		}
	}

	/// <summary>
	/// Marks the snap point search for the placement ghost, so only that search leaves out the top corners, and keeps
	/// a surface piece on the surface it is aimed at when it snaps to a wall or floor
	/// </summary>
	[HarmonyPatch(typeof(Player), nameof(Player.FindClosestSnapPoints))]
	public static class PlayerFindClosestSnapPointsPatch
	{
		/// <summary>
		/// Set during the search when the piece being placed doesn't support what's built on it
		/// </summary>
		public static bool SkipUnsupportedTops;

		/// <summary>
		/// Stands in for a wall's or floor's snap point, moved out to its surface - the game reads its parent, so it has one
		/// </summary>
		private static Transform surfacePoint;

		/// <summary>
		/// Starts leaving out top corners when the piece being placed can't be built on either
		/// </summary>
		/// <param name="ghost">The placement ghost's transform</param>
		private static void Prefix(Transform ghost)
		{
			SkipUnsupportedTops = !Snapping.Supports(ghost.gameObject);
		}

		/// <summary>
		/// Moves the matched snap point of a wall or floor out to the surface the piece is aimed at when a surface piece
		/// snaps to it, so the snap only lines the piece up along that surface
		/// </summary>
		/// <remarks>
		/// A wall's snap points run through its middle and a floor's sit below the top of its planks, so a thin piece
		/// that snapped to them would be buried inside. Before snapping, the game puts the ghost where the aim hits the
		/// surface, so keeping the ghost's distance along the surface's normal keeps it there. This mod's own snap
		/// points are already on the surface of the piece that has them, so they are used as they are
		/// </remarks>
		/// <param name="ghost">The placement ghost's transform</param>
		/// <param name="a">The ghost's matched snap point</param>
		/// <param name="b">The matched snap point near the ghost, replaced by the moved one</param>
		/// <param name="__result">Whether a pair was found</param>
		private static void Postfix(Transform ghost, ref Transform a, ref Transform b, bool __result)
		{
			if (!__result || b == null || Snapping.IsOwnSnapPoint(b) || !Snapping.IsSurfacePiece(Utils.GetPrefabName(ghost.gameObject)))
			{
				return;
			}
			Vector3 normal = PlayerPieceRayTestPatch.LastNormal;
			Vector3 snapped = b.position - (a.position - ghost.position);
			Vector3 position = b.position + (normal * Vector3.Dot(ghost.position - snapped, normal));
			if (surfacePoint == null)
			{
				GameObject holder = new GameObject(Plugin.PluginName + " surface point");
				UnityEngine.Object.DontDestroyOnLoad(holder);
				surfacePoint = new GameObject("surface point").transform;
				surfacePoint.SetParent(holder.transform, false);
			}
			surfacePoint.position = position;
			b = surfacePoint;
		}

		/// <summary>
		/// Stops leaving out top corners once the search is done, even when it throws
		/// </summary>
		private static void Finalizer()
		{
			SkipUnsupportedTops = false;
		}
	}

	/// <summary>
	/// Records the normal of the surface the placement aim hits, so a surface piece that snaps to a wall or floor can
	/// be kept on that surface
	/// </summary>
	[HarmonyPatch(typeof(Player), nameof(Player.PieceRayTest))]
	public static class PlayerPieceRayTestPatch
	{
		/// <summary>
		/// The normal of the surface the aim last hit
		/// </summary>
		public static Vector3 LastNormal = Vector3.forward;

		/// <summary>
		/// Keeps the normal when the aim hit something
		/// </summary>
		/// <param name="normal">The hit surface's normal</param>
		/// <param name="__result">Whether the aim hit something in range</param>
		private static void Postfix(ref Vector3 normal, bool __result)
		{
			if (__result)
			{
				LastNormal = normal;
			}
		}
	}

	/// <summary>
	/// Leaves the top corners of pieces that can't be built on out of the snap points near the placement ghost when
	/// the ghost can't be built on either
	/// </summary>
	/// <remarks>
	/// The game rejects placing a piece on one that doesn't support, so snapping a chest onto a chest only offers a
	/// spot it can't be built. A floor or wall can still be placed there, so those still snap to the top corners
	/// </remarks>
	[HarmonyPatch(typeof(Piece), nameof(Piece.GetSnapPoints), new Type[] { typeof(Vector3), typeof(float), typeof(List<Transform>), typeof(List<Piece>) })]
	public static class PieceGetSnapPointsPatch
	{
		/// <summary>
		/// Removes the top corners of pieces that can't be built on during the placement ghost's search
		/// </summary>
		/// <param name="points">The snap points found near the ghost</param>
		private static void Postfix(List<Transform> points)
		{
			if (!PlayerFindClosestSnapPointsPatch.SkipUnsupportedTops)
			{
				return;
			}
			points.RemoveAll(point => Snapping.IsOwnTopCorner(point) && !Snapping.Supports(point.parent.gameObject));
		}
	}
}
