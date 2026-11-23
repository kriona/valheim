using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace VikingVillages
{
	/// <summary>
	/// Builds each navmesh tile through NavmeshRoofs, so roofs cost more to walk on
	/// </summary>
	[HarmonyPatch(typeof(Pathfinding), nameof(Pathfinding.BuildTile))]
	public static class NavmeshRoofPatch
	{
		/// <summary>
		/// Swaps the tile's NavMeshBuilder.UpdateNavMeshDataAsync call for NavmeshRoofs' own, which takes the same
		/// arguments, leaving the method as it is when the call isn't there
		/// </summary>
		/// <param name="instructions">The method's instructions</param>
		/// <returns>The instructions with the call swapped</returns>
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo build = AccessTools.Method(typeof(NavMeshBuilder), nameof(NavMeshBuilder.UpdateNavMeshDataAsync), new[] { typeof(NavMeshData), typeof(NavMeshBuildSettings), typeof(List<NavMeshBuildSource>), typeof(Bounds) });
			MethodInfo markedBuild = AccessTools.Method(typeof(NavmeshRoofs), nameof(NavmeshRoofs.UpdateNavMeshDataAsync));
			List<CodeInstruction> result = new List<CodeInstruction>(instructions);
			int found = 0;
			foreach (CodeInstruction instruction in result)
			{
				if (build != null && instruction.Calls(build))
				{
					found++;
				}
			}
			if (found != 1)
			{
				Plugin.Log.LogWarning("Expected one NavMeshBuilder.UpdateNavMeshDataAsync call in Pathfinding.BuildTile but found " + found + " - creatures will walk over roofs as freely as the ground");
				return(result);
			}
			foreach (CodeInstruction instruction in result)
			{
				if (instruction.Calls(build))
				{
					instruction.opcode = OpCodes.Call;
					instruction.operand = markedBuild;
				}
			}
			return(result);
		}
	}
}
