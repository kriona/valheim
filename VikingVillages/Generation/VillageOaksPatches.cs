using HarmonyLib;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Keeps the rooms of a village's farm being generated - its farmhouses, fields, pens and fence sections - clear of
	/// the oaks around it, by counting coming too close to one as running into another room (see VillageOaks)
	/// </summary>
	/// <remarks>
	/// The generator tests every room and fence section it places here, and so do FarmLayout and VillageFarmhouses for
	/// theirs. Between farms there are no oaks noted, so other dungeons are left as they are
	/// </remarks>
	[HarmonyPatch(typeof(DungeonGenerator), nameof(DungeonGenerator.TestCollision))]
	public static class FarmOakPatch
	{
		/// <summary>
		/// Counts a room as colliding when it would come too close to one of the farm's oaks
		/// </summary>
		/// <param name="room">The room</param>
		/// <param name="pos">Where its middle would go</param>
		/// <param name="rot">Which way it would face</param>
		/// <param name="__result">Whether it collides</param>
		private static void Postfix(Room room, Vector3 pos, Quaternion rot, ref bool __result)
		{
			if (!__result && room != null && VillageOaks.Crowds(room, pos, rot))
			{
				__result = true;
			}
		}
	}
}
