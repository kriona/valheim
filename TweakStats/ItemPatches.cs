using System;
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

	/// <summary>
	/// Keeps an item's durability inside what the game can save
	/// </summary>
	/// <remarks>
	/// The game saves durability as a whole number of hundredths, which overflows above about 21 million and loads back
	/// as a broken item. A tweaked maxDurability or durabilityPerLevel can reach that at a high enough upgrade level, so
	/// the durability saved is held below it
	/// </remarks>
	[HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.Save), new Type[] { typeof(ZPackage) })]
	public static class ItemDataSavePatch
	{
		/// <summary>
		/// The most durability an item can have when it's saved
		/// </summary>
		private const float MaxSavedDurability = 21000000f;

		/// <summary>
		/// Lowers the item's durability to what can be saved, when it's above it
		/// </summary>
		/// <param name="__instance">The item</param>
		private static void Prefix(ItemDrop.ItemData __instance)
		{
			if (__instance.m_durability > MaxSavedDurability)
			{
				__instance.m_durability = MaxSavedDurability;
			}
		}
	}
}
