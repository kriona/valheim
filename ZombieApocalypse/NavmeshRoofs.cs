using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieApocalypse
{
	/// <summary>
	/// Makes roofs costly to walk on in the navmesh, so creatures go around a house rather than over it when they can,
	/// and the flames of fires far more so, so they go around a fire rather than through it
	/// </summary>
	/// <remarks>
	/// Each tile's roof colliders are built into a navmesh area of their own with a high cost. Roofs stay walkable, so a
	/// creature still climbs one when that's the only way to its target - except a living viking with no danger near,
	/// whose AI leaves the roof area out altogether (see StartAvoiding). Every agent but the fish walks any area, and
	/// the fish's areas leave this one out, but a creature that can't walk has its sources built as not walkable, which
	/// is left alone
	/// </remarks>
	internal static class NavmeshRoofs
	{
		/// <summary>
		/// The navmesh area roofs are built into, after the game's Default, Not Walkable, Jump and Water areas
		/// </summary>
		public const int RoofArea = 4;

		/// <summary>
		/// What a meter of roof costs to walk compared to a meter of open ground - below the game's water, which costs
		/// 100
		/// </summary>
		private const float RoofCost = 30f;

		/// <summary>
		/// The navmesh area the ground in a fire's flames is built into, what a meter of it costs to walk compared to a
		/// meter of open ground, and how far past the flames it reaches, in meters - so paths go around a fire, while a
		/// viking can still step up to it to feed it or cook
		/// </summary>
		public const int FireArea = 5;
		private const float FireCost = 500f;
		private const float FireMargin = 0.5f;
		private const float FireHeight = 4f;

		/// <summary>
		/// How far a fire's flames reach from its middle when it has none to measure, in meters
		/// </summary>
		private const float FlamesFallback = 1f;

		/// <summary>
		/// The area the game builds walkable sources into
		/// </summary>
		private const int DefaultArea = 0;

		/// <summary>
		/// The text in the prefab name of every roof piece
		/// </summary>
		private const string RoofName = "roof";

		/// <summary>
		/// Whether each piece collider seen so far is part of a roof, held as Roof or NotRoof
		/// </summary>
		private static readonly ConditionalWeakTable<Collider, object> roofColliders = new ConditionalWeakTable<Collider, object>();
		private static readonly object Roof = new object();
		private static readonly object NotRoof = new object();

		/// <summary>
		/// The layer building pieces are on, looked up on first use
		/// </summary>
		private static int pieceLayer = -1;

		/// <summary>
		/// Whether the roof area's cost has been set
		/// </summary>
		private static bool costSet;
		private static bool fireCostSet;

		/// <summary>
		/// The agents a living viking walks as (see VikingCore.UpdatePathing), and their area masks while areas are
		/// left out of them, to put back after
		/// </summary>
		private static readonly Pathfinding.AgentType[] vikingAgents = new[] { Pathfinding.AgentType.Humanoid, Pathfinding.AgentType.HumanoidAvoidWater };
		private static readonly int[] savedMasks = new int[vikingAgents.Length];
		private static bool avoiding;

		/// <summary>
		/// How far below a point the ground is looked for when checking whether it's a roof, in meters
		/// </summary>
		private const float GroundCheck = 1.5f;

		/// <summary>
		/// The layers of what can be stood on
		/// </summary>
		private static int groundMask;

		/// <summary>
		/// Leaves navmesh areas out of the paths, valid spots and path checks of every humanoid walking as a living
		/// viking does until StopAvoiding - for a living viking's AI update, so it keeps off the lava and the boiling sea
		/// (see DangerousGround) and, while there's no danger, off roofs, rather than only preferring to
		/// </summary>
		/// <remarks>
		/// The game keeps one area mask for each kind of agent, so it's changed just while the viking's AI runs and put
		/// back after, the same way a viking's ranged weapon is tuned for its AI
		/// </remarks>
		/// <param name="areas">The areas left out, as a navmesh area mask</param>
		public static void StartAvoiding(int areas)
		{
			if (avoiding || Pathfinding.instance == null)
			{
				return;
			}
			for (int i = 0; i < vikingAgents.Length; i++)
			{
				Pathfinding.AgentSettings settings = Pathfinding.instance.GetSettings(vikingAgents[i]);
				if (settings != null)
				{
					savedMasks[i] = settings.m_areaMask;
					settings.m_areaMask = savedMasks[i] & ~areas;
				}
			}
			avoiding = true;
		}

		/// <summary>
		/// Puts the areas back in the humanoids' paths after StartAvoiding
		/// </summary>
		public static void StopAvoiding()
		{
			if (!avoiding)
			{
				return;
			}
			avoiding = false;
			if (Pathfinding.instance == null)
			{
				return;
			}
			for (int i = 0; i < vikingAgents.Length; i++)
			{
				Pathfinding.AgentSettings settings = Pathfinding.instance.GetSettings(vikingAgents[i]);
				if (settings != null)
				{
					settings.m_areaMask = savedMasks[i];
				}
			}
		}

		/// <summary>
		/// Checks whether what's under a point is a roof
		/// </summary>
		/// <param name="point">The point, at or a little above where something stands</param>
		/// <returns>True when the first thing below it is part of a roof</returns>
		public static bool OnRoof(Vector3 point)
		{
			if (groundMask == 0)
			{
				groundMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
			}
			if (!Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, GroundCheck, groundMask, QueryTriggerInteraction.Ignore))
			{
				return(false);
			}
			if (pieceLayer < 0)
			{
				pieceLayer = LayerMask.NameToLayer("piece");
			}
			return(hit.collider != null && hit.collider.gameObject.layer == pieceLayer && IsRoof(hit.collider));
		}

		/// <summary>
		/// Builds a navmesh tile from its sources the way the game does, with the roof colliders among them moved into
		/// the roof area
		/// </summary>
		/// <param name="data">The tile's navmesh data</param>
		/// <param name="buildSettings">The agent's build settings</param>
		/// <param name="sources">The tile's sources</param>
		/// <param name="localBounds">The tile's bounds, around its data's position</param>
		/// <returns>The build in progress</returns>
		public static AsyncOperation UpdateNavMeshDataAsync(NavMeshData data, NavMeshBuildSettings buildSettings, List<NavMeshBuildSource> sources, Bounds localBounds)
		{
			try
			{
				MarkRoofs(sources);
			}
			catch (Exception error)
			{
				Plugin.Log.LogWarning("Couldn't mark the roofs in a navmesh tile: " + error);
			}
			try
			{
				MarkFires(sources, new Bounds(data.position + localBounds.center, localBounds.size));
			}
			catch (Exception error)
			{
				Plugin.Log.LogWarning("Couldn't mark the fires in a navmesh tile: " + error);
			}
			try
			{
				DangerousGround.Mark(sources, new Bounds(data.position + localBounds.center, localBounds.size));
			}
			catch (Exception error)
			{
				Plugin.Log.LogWarning("Couldn't mark the lava and boiling sea in a navmesh tile: " + error);
			}
			return(NavMeshBuilder.UpdateNavMeshDataAsync(data, buildSettings, sources, localBounds));
		}

		/// <summary>
		/// Puts the ground in the flames of each fire in a tile into the fire area, setting the area's cost the first
		/// time - leaving out the tiles of a creature that can't walk, which have no walkable sources
		/// </summary>
		/// <param name="sources">The tile's sources</param>
		/// <param name="bounds">The tile's bounds, in the world</param>
		private static void MarkFires(List<NavMeshBuildSource> sources, Bounds bounds)
		{
			if (ZNetScene.instance == null || !sources.Exists((NavMeshBuildSource source) => source.area == DefaultArea))
			{
				return;
			}
			if (!fireCostSet)
			{
				NavMesh.SetAreaCost(FireArea, FireCost);
				fireCostSet = true;
			}
			List<NavMeshBuildSource> fires = new List<NavMeshBuildSource>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZNetView view = instance.Value;
				if (view == null || !bounds.Contains(instance.Key.GetPosition()))
				{
					continue;
				}
				Fireplace fire = view.GetComponent<Fireplace>();
				if (fire == null)
				{
					continue;
				}
				float flames = VillageLife.AreaReach(fire, EffectArea.Type.Burning);
				float size = ((flames > 0f) ? flames : FlamesFallback) + FireMargin;
				NavMeshBuildSource box = new NavMeshBuildSource();
				box.shape = NavMeshBuildSourceShape.ModifierBox;
				box.transform = Matrix4x4.TRS(fire.transform.position, Quaternion.identity, Vector3.one);
				box.size = new Vector3(size * 2f, FireHeight, size * 2f);
				box.area = FireArea;
				fires.Add(box);
			}
			sources.AddRange(fires);
		}

		/// <summary>
		/// Moves each walkable source that's a roof's collider into the roof area, setting the area's cost the first
		/// time
		/// </summary>
		/// <param name="sources">The tile's sources</param>
		private static void MarkRoofs(List<NavMeshBuildSource> sources)
		{
			if (!costSet)
			{
				NavMesh.SetAreaCost(RoofArea, RoofCost);
				costSet = true;
			}
			if (pieceLayer < 0)
			{
				pieceLayer = LayerMask.NameToLayer("piece");
			}
			for (int i = 0; i < sources.Count; i++)
			{
				NavMeshBuildSource source = sources[i];
				if (source.area == DefaultArea && source.component is Collider collider && collider != null && collider.gameObject.layer == pieceLayer && IsRoof(collider))
				{
					source.area = RoofArea;
					sources[i] = source;
				}
			}
		}

		/// <summary>
		/// Checks whether a collider belongs to a roof building piece, remembering the answer
		/// </summary>
		/// <param name="collider">A collider on the piece layer</param>
		/// <returns>True when its piece's prefab name has roof in it</returns>
		public static bool IsRoof(Collider collider)
		{
			if (roofColliders.TryGetValue(collider, out object known))
			{
				return(known == Roof);
			}
			WearNTear piece = collider.GetComponentInParent<WearNTear>();
			bool roof = (piece != null && Utils.GetPrefabName(piece.gameObject).IndexOf(RoofName, StringComparison.OrdinalIgnoreCase) >= 0);
			roofColliders.Add(collider, (roof ? Roof : NotRoof));
			return(roof);
		}
	}
}
