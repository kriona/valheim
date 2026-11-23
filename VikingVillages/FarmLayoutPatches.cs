using HarmonyLib;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Lays out a village's farm with its farmhouses in the middle and its pens out toward its fence
	/// </summary>
	[HarmonyPatch(typeof(DungeonGenerator), nameof(DungeonGenerator.GenerateCampRadial))]
	public static class FarmLayoutPatch
	{
		/// <summary>
		/// Places a village farm's rooms in place of the generator's radial camp
		/// </summary>
		/// <param name="__instance">The generator</param>
		/// <param name="mode">How its location is being placed</param>
		/// <returns>False for a village's farm, to skip the generator's own layout</returns>
		private static bool Prefix(DungeonGenerator __instance, ZoneSystem.SpawnMode mode)
		{
			return(!FarmLayout.Generate(__instance, mode));
		}
	}

	/// <summary>
	/// Reads the ground from the world's own heights where its terrain isn't loaded while a village's farm is laid out,
	/// so the game's own placing of its fence doesn't put a section at the farm's middle height across a zone border
	/// (see FarmLayout.GroundHeight)
	/// </summary>
	[HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.GetGroundData))]
	public static class FarmGroundPatch
	{
		/// <summary>
		/// Takes the ground from the world's heights when the game's raycast found no terrain under the spot
		/// </summary>
		/// <param name="p">The spot, set to the ground's height</param>
		/// <param name="normal">The ground's slope</param>
		/// <param name="hmap">The terrain the raycast hit, or null</param>
		private static void Postfix(ref Vector3 p, ref Vector3 normal, ref Heightmap hmap)
		{
			if (FarmLayout.Generating && hmap == null)
			{
				FarmLayout.GroundFromWorld(ref p, ref normal);
			}
		}
	}
}
