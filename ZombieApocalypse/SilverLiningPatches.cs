using HarmonyLib;
using TMPro;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Lists a "Silver-line" entry on a forge's Upgrade tab for each weapon the player carries that can be lined
	/// </summary>
	/// <remarks>
	/// The entries go after the game's own upgrades, which are already sorted and placed by then
	/// </remarks>
	[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRecipeList))]
	public static class SilverLiningListPatch
	{
		/// <summary>
		/// Adds the lining entries and makes room for them in the list
		/// </summary>
		/// <param name="__instance">The inventory screen</param>
		private static void Postfix(InventoryGui __instance)
		{
			Player player = Player.m_localPlayer;
			if (player == null || !ServerCheck.Active || __instance.InCraftTab() || !SilverLining.IsForge(player.GetCurrentCraftingStation()))
			{
				return;
			}
			bool free = ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost);
			foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
			{
				Recipe recipe = SilverLining.GetRecipe(item);
				if (recipe == null)
				{
					continue;
				}
				__instance.AddRecipeToList(player, recipe, item, player.HaveRequirements(recipe, false, 1) || free);
				GameObject element = __instance.m_availableRecipes[__instance.m_availableRecipes.Count - 1].InterfaceElement;
				TMP_Text name = element.transform.Find("name").GetComponent<TMP_Text>();
				name.text = "Silver-line " + name.text;
			}
			float height = Mathf.Max(__instance.m_recipeListBaseSize, __instance.m_availableRecipes.Count * __instance.m_recipeListSpace);
			__instance.m_recipeListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
		}
	}

	/// <summary>
	/// Shows a selected lining entry as the weapon will be once lined, with the lining's cost, forge level and button
	/// </summary>
	/// <remarks>
	/// The game sets up the panel for the weapon's next quality level first, which would show the wrong stats, scale
	/// the cost and forge level by the weapon's level, and refuse a weapon already at its highest level, so the panel
	/// is set up again here at quality 1
	/// </remarks>
	[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRecipe))]
	public static class SilverLiningRecipePatch
	{
		/// <summary>
		/// Sets up the panel for a selected lining entry
		/// </summary>
		/// <param name="__instance">The inventory screen</param>
		/// <param name="player">The local player</param>
		private static void Postfix(InventoryGui __instance, Player player)
		{
			Recipe recipe = __instance.m_selectedRecipe.Recipe;
			ItemDrop.ItemData item = __instance.m_selectedRecipe.ItemData;
			if (!SilverLining.IsLiningRecipe(recipe) || item == null)
			{
				return;
			}
			string name = Localization.instance.Localize(item.m_shared.m_name);
			ItemDrop.ItemData lined = item.Clone();
			SilverLining.Line(lined);
			__instance.m_recipeName.text = name;
			__instance.m_recipeDecription.text = Localization.instance.Localize(ItemDrop.ItemData.GetTooltip(lined, item.m_quality, false, item.m_worldLevel));
			__instance.m_itemCraftType.gameObject.SetActive(true);
			__instance.m_itemCraftType.text = "Silver-line " + name;
			__instance.SetupRequirementList(1, player, true, 1);
			CraftingStation station = player.GetCurrentCraftingStation();
			bool free = ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost);
			bool levelMet = (station != null && station.GetLevel() >= SilverLining.ForgeLevel);
			__instance.m_minStationLevelIcon.gameObject.SetActive(true);
			__instance.m_minStationLevelText.text = SilverLining.ForgeLevel.ToString();
			__instance.m_minStationLevelText.color = ((levelMet || free || Mathf.Sin(Time.time * 10f) <= 0f) ? __instance.m_minStationLevelBasecolor : Color.red);
			bool haveRequirements = player.HaveRequirements(recipe, false, 1);
			bool usable = (station != null && station.CheckUsable(player, false));
			__instance.m_craftButton.interactable = ((haveRequirements && usable) || player.NoCostCheat() || (free && usable));
			__instance.m_craftButton.GetComponentInChildren<TMP_Text>().text = "Silver-line";
			UITooltip tooltip = __instance.m_craftButton.GetComponent<UITooltip>();
			if (!haveRequirements && !free)
			{
				tooltip.m_text = Localization.instance.Localize("$msg_missingrequirement");
			}
			else if (!usable)
			{
				tooltip.m_text = Localization.instance.Localize("$msg_missingstation");
			}
			else
			{
				tooltip.m_text = "";
			}
		}
	}

	/// <summary>
	/// Lines the weapon when a lining entry finishes crafting, in place of the game's upgrade
	/// </summary>
	/// <remarks>
	/// The game's upgrade would replace the weapon with a new one a level higher, so the lining marks the weapon itself
	/// instead, keeping its level, durability and crafter
	/// </remarks>
	[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
	public static class SilverLiningCraftPatch
	{
		/// <summary>
		/// Takes the silver and ectoplasm and lines the weapon
		/// </summary>
		/// <param name="__instance">The inventory screen</param>
		/// <param name="player">The local player</param>
		/// <returns>False to skip the game's crafting for a lining entry</returns>
		private static bool Prefix(InventoryGui __instance, Player player)
		{
			Recipe recipe = __instance.m_craftRecipe;
			if (!SilverLining.IsLiningRecipe(recipe))
			{
				return(true);
			}
			ItemDrop.ItemData item = __instance.m_craftUpgradeItem;
			bool free = (player.NoCostCheat() || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost));
			if (item == null || !player.GetInventory().ContainsItem(item) || SilverLining.GetRecipe(item) != recipe || (!free && !player.HaveRequirements(recipe, false, 1)))
			{
				return(false);
			}
			if (!free)
			{
				player.ConsumeResources(recipe.m_resources, 1);
			}
			SilverLining.Line(item);
			player.GetInventory().Changed();
			CraftingStation station = player.GetCurrentCraftingStation();
			if (station != null)
			{
				station.m_craftItemDoneEffects.Create(player.transform.position, Quaternion.identity);
				if (station.m_craftingSkill != Skills.SkillType.None)
				{
					player.RaiseSkill(station.m_craftingSkill);
				}
			}
			player.Message(MessageHud.MessageType.Center, Localization.instance.Localize(item.m_shared.m_name) + " is silver-lined");
			__instance.UpdateCraftingPanel();
			return(false);
		}
	}

	/// <summary>
	/// Adds a silver-lined weapon's spirit damage to its damage, which its attacks, its tooltip and the check that keeps
	/// creatures killed by spirit damage from rising all read
	/// </summary>
	/// <remarks>
	/// Only in a world running the mod - elsewhere the lining stays on the weapon but does nothing
	/// </remarks>
	[HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetDamage), new[] { typeof(int), typeof(float) })]
	public static class SilverLiningDamagePatch
	{
		/// <summary>
		/// Adds the spirit damage to a silver-lined weapon's
		/// </summary>
		/// <param name="__instance">The item</param>
		/// <param name="__result">Its damage</param>
		private static void Postfix(ItemDrop.ItemData __instance, ref HitData.DamageTypes __result)
		{
			if (SilverLining.IsLined(__instance) && ServerCheck.Active)
			{
				__result.m_spirit += SilverLining.SpiritDamage(__instance);
			}
		}
	}

	/// <summary>
	/// Marks a silver-lined weapon as one in its tooltip
	/// </summary>
	[HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip), new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) })]
	public static class SilverLiningTooltipPatch
	{
		/// <summary>
		/// Adds a "Silver-lined" line to a silver-lined weapon's tooltip
		/// </summary>
		/// <param name="item">The item</param>
		/// <param name="appending">True when the tooltip is being added to another item's</param>
		/// <param name="__result">The tooltip</param>
		private static void Postfix(ItemDrop.ItemData item, bool appending, ref string __result)
		{
			if (!appending && SilverLining.IsLined(item))
			{
				__result += "\n<color=#C8D2DC>Silver-lined</color>";
			}
		}
	}

	/// <summary>
	/// Has Hugin tell the local player about silver-lining the first time they have smelted silver
	/// </summary>
	[HarmonyPatch(typeof(Player), nameof(Player.Update))]
	public static class SilverHintPatch
	{
		/// <summary>
		/// Checks for silver bars in the player's inventory
		/// </summary>
		/// <param name="__instance">The player updating</param>
		private static void Postfix(Player __instance)
		{
			SilverHint.Update(__instance);
		}
	}
}
