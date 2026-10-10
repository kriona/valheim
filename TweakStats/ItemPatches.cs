using HarmonyLib;

namespace TweakStats
{
	/// <summary>
	/// Points each item in the world at its prefab's shared data, as the game itself does in the Unity editor
	/// </summary>
	/// <remarks>
	/// Unity copies the shared data into every item it instantiates, and picking an item up carries that copy into an
	/// inventory, so without this an item that was on the ground keeps the stats it had when it appeared rather than
	/// the tweaked ones
	/// </remarks>
	[HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Awake))]
	public static class ItemDropAwakePatch
	{
		/// <summary>
		/// Replaces the item's copy of the shared data with its prefab's
		/// </summary>
		/// <param name="__instance">The item</param>
		private static void Postfix(ItemDrop __instance)
		{
			if (__instance.m_itemData.m_dropPrefab == null)
			{
				return;
			}
			ItemDrop prefab = __instance.m_itemData.m_dropPrefab.GetComponent<ItemDrop>();
			if (prefab != null && prefab != __instance)
			{
				__instance.m_itemData.m_shared = prefab.m_itemData.m_shared;
			}
		}
	}
}
