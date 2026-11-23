using UnityEngine;
using UnityEngine.AI;

namespace ZombieApocalypse
{
	/// <summary>
	/// Finds points on the navmesh for a kind of agent
	/// </summary>
	/// <remarks>
	/// The game's Pathfinding.FindValidPoint asks the navmesh about the agent by its AgentType number rather than the
	/// agent type id its navmesh is built for, so it never finds a point. This asks with the id, the way the game's
	/// own path finding does
	/// </remarks>
	internal static class NavmeshPoints
	{
		/// <summary>
		/// Finds the point on an agent's navmesh nearest a spot, keeping the navmesh there built
		/// </summary>
		/// <param name="point">The point found, or the spot when there's none</param>
		/// <param name="center">The spot</param>
		/// <param name="range">How far from the spot to look, in meters</param>
		/// <param name="agentType">The kind of agent</param>
		/// <returns>False when there's no navmesh that close</returns>
		public static bool FindValidPoint(out Vector3 point, Vector3 center, float range, Pathfinding.AgentType agentType)
		{
			point = center;
			Pathfinding pathfinding = Pathfinding.instance;
			if (pathfinding == null)
			{
				return(false);
			}
			pathfinding.PokePoint(center, agentType);
			Pathfinding.AgentSettings settings = pathfinding.GetSettings(agentType);
			if (settings == null)
			{
				return(false);
			}
			NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = settings.m_build.agentTypeID, areaMask = settings.m_areaMask };
			if (!NavMesh.SamplePosition(center, out NavMeshHit hit, range, filter))
			{
				return(false);
			}
			point = hit.position;
			return(true);
		}
	}
}
