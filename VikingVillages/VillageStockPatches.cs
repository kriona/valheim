using HarmonyLib;

namespace VikingVillages
{
	/// <summary>
	/// Keeps a village's stock (see VillageStock) to look at only - clicking, dragging or splitting in it does nothing,
	/// and neither does moving one of the player's own items over to it, since its items are only copies of what's in
	/// the village's chests
	/// </summary>
	[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnSelectedItem))]
	public static class VillageStockSelectPatch
	{
		/// <summary>
		/// Skips picking up, dropping or splitting an item in the stock, and moving an item of the player's to it
		/// </summary>
		/// <param name="__instance">The game's inventory window</param>
		/// <param name="grid">The grid clicked in</param>
		/// <param name="mod">How it was clicked</param>
		/// <returns>False while the stock is open, for its grid or a move, to skip it</returns>
		private static bool Prefix(InventoryGui __instance, InventoryGrid grid, InventoryGrid.Modifier mod)
		{
			return(!VillageStock.Showing(__instance) || (grid != __instance.m_containerGrid && mod != InventoryGrid.Modifier.Move));
		}
	}

	/// <summary>
	/// Keeps a village's stock to look at only when an item is let go of over it on a touch screen
	/// </summary>
	[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnReleasedItem))]
	public static class VillageStockReleasePatch
	{
		/// <summary>
		/// Skips letting go of an item over the stock
		/// </summary>
		/// <param name="__instance">The game's inventory window</param>
		/// <param name="grid">The grid it's let go of over, or null</param>
		/// <returns>False while the stock is open, over its grid, to skip it</returns>
		private static bool Prefix(InventoryGui __instance, InventoryGrid grid)
		{
			return(!VillageStock.Showing(__instance) || grid != __instance.m_containerGrid);
		}
	}

	/// <summary>
	/// Keeps a village's stock to look at only when one of its items is right-clicked, which would use it - eating it,
	/// say
	/// </summary>
	[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnRightClickItem))]
	public static class VillageStockUsePatch
	{
		/// <summary>
		/// Skips using an item in the stock
		/// </summary>
		/// <param name="__instance">The game's inventory window</param>
		/// <param name="grid">The grid right-clicked in</param>
		/// <returns>False while the stock is open, for its grid, to skip it</returns>
		private static bool Prefix(InventoryGui __instance, InventoryGrid grid)
		{
			return(!VillageStock.Showing(__instance) || grid != __instance.m_containerGrid);
		}
	}

	/// <summary>
	/// Keeps a village's stock to look at only when the take-all button is pressed, which is hidden for it
	/// </summary>
	[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnTakeAll))]
	public static class VillageStockTakeAllPatch
	{
		/// <summary>
		/// Skips taking everything from the stock
		/// </summary>
		/// <param name="__instance">The game's inventory window</param>
		/// <returns>False while the stock is open, to skip it</returns>
		private static bool Prefix(InventoryGui __instance)
		{
			return(!VillageStock.Showing(__instance));
		}
	}

	/// <summary>
	/// Keeps a village's stock to look at only when the place-stacks button is pressed, which is hidden for it
	/// </summary>
	[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnStackAll))]
	public static class VillageStockStackAllPatch
	{
		/// <summary>
		/// Skips placing the player's stacks in the stock
		/// </summary>
		/// <param name="__instance">The game's inventory window</param>
		/// <returns>False while the stock is open, to skip it</returns>
		private static bool Prefix(InventoryGui __instance)
		{
			return(!VillageStock.Showing(__instance));
		}
	}
}
