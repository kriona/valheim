using HarmonyLib;

namespace TweakStats
{
	/// <summary>
	/// Drops the items a chest has outside its size on the ground when it loads, so a chest that was out of range when
	/// it was made smaller loses the same slots as one nearby
	/// </summary>
	/// <remarks>
	/// The game deletes items in columns past a chest's width as it loads them, so the inventory is made wide enough to
	/// hold them while it loads, then given its own width back. The game keeps items in rows past a chest's height by
	/// adding rows to fit them, and that's undone only for chests the config resizes or has resized before, which carry
	/// a mark in their world data. Graves rely on those added rows to hold everything a player carried, so they're never
	/// changed
	/// </remarks>
	[HarmonyPatch(typeof(Container), nameof(Container.Load))]
	public static class ContainerLoadPatch
	{
		/// <summary>
		/// Columns the inventory has while it loads, more than any chest has
		/// </summary>
		private const int LoadingWidth = 1000;

		/// <summary>
		/// Widens the inventory so every item it loads has a column
		/// </summary>
		/// <param name="__instance">The chest</param>
		/// <param name="__state">The inventory's own width, or -1 when the inventory is left alone</param>
		private static void Prefix(Container __instance, out int __state)
		{
			__state = -1;
			if (__instance.m_inventory != null && !Instances.IsGrave(__instance))
			{
				__state = __instance.m_inventory.m_width;
				__instance.m_inventory.m_width = LoadingWidth;
			}
		}

		/// <summary>
		/// Gives the inventory its own width back, and its own height when the chest keeps to it, and drops the items
		/// outside them
		/// </summary>
		/// <param name="__instance">The chest</param>
		/// <param name="__result">Whether the chest loaded, rather than finding nothing new to load</param>
		/// <param name="__state">The inventory's own width, or -1 when the inventory is left alone</param>
		private static void Postfix(Container __instance, bool __result, int __state)
		{
			if (__state < 0)
			{
				return;
			}
			__instance.m_inventory.m_width = __state;
			if (!__result)
			{
				return;
			}
			Instances.MarkResized(__instance);
			if (Instances.KeepsSize(__instance))
			{
				__instance.m_inventory.m_height = __instance.m_height;
			}
			Instances.DropItemsOutside(__instance);
		}
	}
}
