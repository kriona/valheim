using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace CraftMore
{
	internal static class Craft
	{
		/// <summary>
		/// The game's own multi-craft amount and duration, read the first time the crafting panel updates
		/// </summary>
		private static int defaultAmount = -1;
		private static float defaultDuration;

		/// <summary>
		/// How many times the game's own multi-craft key crafts, or -1 before the crafting panel first updates
		/// </summary>
		public static int DefaultAmount => defaultAmount;

		/// <summary>
		/// Whether the craft in progress drops what doesn't fit in the inventory instead of being refused - for a Craft
		/// All, and for every craft after the first in a batch crafted one at a time
		/// </summary>
		private static bool dropExtra;

		/// <summary>
		/// Crafts left in a batch being crafted one at a time, after the one in progress
		/// </summary>
		private static int queuedCrafts;

		/// <summary>
		/// How many crafts the batch being crafted one at a time started with, or 0 when there isn't one
		/// </summary>
		private static int batchCrafts;

		private static readonly AccessTools.FieldRef<GuiBar, float> barMaxValue = AccessTools.FieldRefAccess<GuiBar, float>("m_maxValue");
		private static readonly AccessTools.FieldRef<GuiBar, float> barSmoothValue = AccessTools.FieldRefAccess<GuiBar, float>("m_smoothValue");

		/// <summary>
		/// Turns a batch that has just started into single crafts when One At A Time is on - the first craft starts
		/// now and ContinueBatch starts the rest, each taking the batch's time per craft
		/// </summary>
		/// <param name="gui">The inventory GUI that does the crafting</param>
		public static void QueueBatch(InventoryGui gui)
		{
			queuedCrafts = 0;
			batchCrafts = 0;
			// OnCraftPressed leaves the timer at -1 when it refuses to start crafting
			if (!CraftConfig.OneAtATime || gui.m_craftTimer != 0f || !gui.m_multiCrafting || gui.m_multiCraftAmount <= 1)
			{
				return;
			}
			batchCrafts = gui.m_multiCraftAmount;
			queuedCrafts = batchCrafts - 1;
			gui.m_multiCraftAmount = 1;
			gui.m_multiCraftDuration = GetSingleDuration();
		}

		/// <summary>
		/// Starts the next craft of a batch being crafted one at a time once the last one is done, dropping what doesn't
		/// fit in the inventory, or ends the batch when the player runs out of materials
		/// </summary>
		/// <param name="gui">The inventory GUI that does the crafting</param>
		/// <param name="player">The player crafting</param>
		public static void ContinueBatch(InventoryGui gui, Player player)
		{
			if (queuedCrafts <= 0 || gui.m_craftTimer >= 0f)
			{
				return;
			}
			Recipe recipe = gui.m_craftRecipe;
			if (recipe == null)
			{
				queuedCrafts = 0;
				return;
			}
			bool free = (player.NoCostCheat() || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost));
			if (!free && !player.HaveRequirements(recipe, false, 1, 1))
			{
				queuedCrafts = 0;
				return;
			}
			queuedCrafts--;
			dropExtra = true;
			gui.m_multiCrafting = true;
			gui.m_multiCraftAmount = 1;
			gui.m_multiCraftDuration = GetSingleDuration();
			gui.m_craftTimer = 0f;
			CraftingStation station = player.GetCurrentCraftingStation();
			if (recipe.m_craftingStation != null)
			{
				if (station != null)
				{
					station.m_craftItemEffects.Create(player.transform.position, Quaternion.identity);
				}
			}
			else
			{
				gui.m_craftItemEffects.Create(player.transform.position, Quaternion.identity);
			}
		}

		/// <summary>
		/// Ends a batch being crafted one at a time, for when crafting is canceled or the inventory closes
		/// </summary>
		public static void StopBatch()
		{
			queuedCrafts = 0;
			batchCrafts = 0;
		}

		/// <summary>
		/// Fills the progress bar over the whole of a batch being crafted one at a time rather than each craft
		/// </summary>
		/// <remarks>
		/// UpdateRecipe has just set the bar's maximum to the time of one craft, with the skill's speedup applied. The
		/// maximum and the smoothed fill are set directly, since SetMaxValue rescales the smoothed fill, which would
		/// make a bar that smooths its changes jump between the two scales every frame
		/// </remarks>
		/// <param name="gui">The inventory GUI that does the crafting</param>
		public static void ShowBatchProgress(InventoryGui gui)
		{
			if (gui.m_craftTimer < 0f)
			{
				batchCrafts = 0;
				return;
			}
			if (batchCrafts <= 1)
			{
				return;
			}
			GuiBar bar = gui.m_craftProgressBar;
			float craftTime = barMaxValue(bar);
			int done = (batchCrafts - queuedCrafts - 1);
			float total = (craftTime * batchCrafts);
			float value = Mathf.Min(craftTime * done + gui.m_craftTimer, total);
			barMaxValue(bar) = total;
			bar.SetValue(value);
			barSmoothValue(bar) = Mathf.Clamp01(value / total);
		}

		/// <summary>
		/// How long each craft of a batch takes - the game's batch time divided by its batch size
		/// </summary>
		/// <returns>Seconds per craft, before the crafting skill shortens it</returns>
		private static float GetSingleDuration()
		{
			return(defaultDuration / Mathf.Max(defaultAmount, 1));
		}

		/// <summary>
		/// Whether a key, or its other side for Ctrl, Shift, Alt and Command, is held
		/// </summary>
		/// <param name="key">The key to check</param>
		/// <returns>True when the key is held</returns>
		private static bool IsKeyHeld(KeyCode key)
		{
			KeyCode otherSide = GetOtherSide(key);
			return(ZInput.GetKey(key, false) || (otherSide != KeyCode.None && ZInput.GetKey(otherSide, false)));
		}

		/// <summary>
		/// The matching key on the other side of the keyboard for a left or right modifier key
		/// </summary>
		/// <param name="key">The key to match</param>
		/// <returns>The other side's key, or KeyCode.None if the key has no other side</returns>
		private static KeyCode GetOtherSide(KeyCode key)
		{
			switch (key)
			{
				case KeyCode.LeftControl:
					return(KeyCode.RightControl);
				case KeyCode.RightControl:
					return(KeyCode.LeftControl);
				case KeyCode.LeftShift:
					return(KeyCode.RightShift);
				case KeyCode.RightShift:
					return(KeyCode.LeftShift);
				case KeyCode.LeftAlt:
					return(KeyCode.RightAlt);
				case KeyCode.RightAlt:
					return(KeyCode.LeftAlt);
				case KeyCode.LeftCommand:
					return(KeyCode.RightCommand);
				case KeyCode.RightCommand:
					return(KeyCode.LeftCommand);
				default:
					return(KeyCode.None);
			}
		}

		/// <summary>
		/// The first key in the config file that is held
		/// </summary>
		/// <returns>The held key's binding, or null when none is held</returns>
		private static CraftKey GetHeldKey()
		{
			foreach (CraftKey craftKey in CraftConfig.Keys)
			{
				if (IsKeyHeld(craftKey.Key))
				{
					return(craftKey);
				}
			}
			return(null);
		}

		/// <summary>
		/// Stands in for ZInput.GetButton in the crafting code so the configured keys count as the game's multi-craft
		/// button (AltPlace, Shift by default)
		/// </summary>
		/// <param name="name">The name of the button the game asks for</param>
		/// <returns>True when the button is held, or it's the multi-craft button and a configured key is held</returns>
		public static bool GetButton(string name)
		{
			return(ZInput.GetButton(name) || (name == "AltPlace" && GetHeldKey() != null));
		}

		/// <summary>
		/// How many times a recipe has to be crafted to make a full stack of its item, at least once
		/// </summary>
		/// <param name="recipe">The recipe being crafted</param>
		/// <returns>The number of crafts that fit in one stack</returns>
		public static int GetStackAmount(Recipe recipe)
		{
			int perCraft = Mathf.Max(recipe.m_amount, 1);
			return(Mathf.Max(recipe.m_item.m_itemData.m_shared.m_maxStackSize / perCraft, 1));
		}

		/// <summary>
		/// How many times the player has the materials to craft a recipe, up to CraftConfig.MaxCount - a full stack
		/// when crafting costs nothing or the recipe is free, and 0 when they can't craft it at all
		/// </summary>
		/// <remarks>
		/// Asks HaveRequirements rather than counting materials so recipes that take any one of their ingredients
		/// are counted the way the game counts them - it finds the largest amount by doubling, then halving the gap
		/// </remarks>
		/// <param name="recipe">The recipe being crafted</param>
		/// <param name="player">The player crafting it</param>
		/// <returns>The number of crafts</returns>
		private static int GetAllAmount(Recipe recipe, Player player)
		{
			if (player.NoCostCheat() || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost) || IsFree(recipe))
			{
				return(GetStackAmount(recipe));
			}
			if (!player.HaveRequirements(recipe, false, 1, 1))
			{
				return(0);
			}
			int low = 1;
			int high = 2;
			while (high <= CraftConfig.MaxCount && player.HaveRequirements(recipe, false, 1, high))
			{
				low = high;
				high *= 2;
			}
			high = Mathf.Min(high, CraftConfig.MaxCount + 1);
			while (high - low > 1)
			{
				int middle = (low + high) / 2;
				if (player.HaveRequirements(recipe, false, 1, middle))
				{
					low = middle;
				}
				else
				{
					high = middle;
				}
			}
			return(low);
		}

		/// <summary>
		/// Whether a new item from a recipe costs nothing - every ingredient costs 0, or for a recipe that takes any
		/// one of its ingredients, one of them does - which the game's materials check passes for any number of crafts
		/// </summary>
		/// <param name="recipe">The recipe being crafted</param>
		/// <returns>True when the recipe costs nothing</returns>
		private static bool IsFree(Recipe recipe)
		{
			bool anyFree = false;
			bool allFree = true;
			foreach (Piece.Requirement requirement in recipe.m_resources)
			{
				if (requirement.m_resItem == null)
				{
					continue;
				}
				if (requirement.GetAmount(1) <= 0)
				{
					anyFree = true;
				}
				else
				{
					allFree = false;
				}
			}
			return((recipe.m_requireOnlyOneIngredient ? anyFree : allFree));
		}

		/// <summary>
		/// How many times holding a key crafts a recipe
		/// </summary>
		/// <param name="craftKey">The key's binding</param>
		/// <param name="recipe">The recipe being crafted</param>
		/// <param name="player">The player crafting it</param>
		/// <returns>The number of crafts, 0 for Craft All when the player can't craft the recipe</returns>
		public static int GetCraftCount(CraftKey craftKey, Recipe recipe, Player player)
		{
			switch (craftKey.Mode)
			{
				case CraftMode.Stack:
					return(GetStackAmount(recipe));
				case CraftMode.All:
					return(GetAllAmount(recipe, player));
				default:
					return(craftKey.Count);
			}
		}

		/// <summary>
		/// Sets the multi-craft amount for the held key, or back to the game's amount when none is held, with the
		/// craft time scaled to match so each item takes as long as it does with the game's amount
		/// </summary>
		/// <param name="gui">The inventory GUI that does the crafting</param>
		/// <returns>The held key's binding, or null when none is held</returns>
		public static CraftKey ApplyAmount(InventoryGui gui)
		{
			if (defaultAmount < 0)
			{
				defaultAmount = gui.m_multiCraftAmount;
				defaultDuration = gui.m_multiCraftDuration;
			}
			CraftKey craftKey = GetHeldKey();
			Recipe recipe = gui.m_selectedRecipe.Recipe;
			Player player = Player.m_localPlayer;
			int amount = defaultAmount;
			if (craftKey != null && recipe != null && player != null)
			{
				// Crafted once when Craft All finds nothing to craft, so the Craft button shows what's missing
				amount = Mathf.Max(GetCraftCount(craftKey, recipe, player), 1);
			}
			gui.m_multiCraftAmount = amount;
			gui.m_multiCraftDuration = (defaultDuration * amount / Mathf.Max(defaultAmount, 1));
			return(craftKey);
		}

		/// <summary>
		/// Drops what doesn't fit when Craft was pressed with a Craft All key, for crafting a new item rather than an
		/// upgrade
		/// </summary>
		/// <param name="gui">The inventory GUI that does the crafting</param>
		/// <param name="craftKey">The held key's binding, or null when none is held</param>
		public static void StartCraft(InventoryGui gui, CraftKey craftKey)
		{
			dropExtra = (craftKey != null && craftKey.Mode == CraftMode.All && gui.m_selectedRecipe.ItemData == null);
		}

		/// <summary>
		/// Stands in for Inventory.CanAddItem in the crafting code so a craft that drops what doesn't fit isn't refused
		/// when the inventory is too full to hold everything
		/// </summary>
		/// <param name="inventory">The player's inventory</param>
		/// <param name="prefab">The item being crafted</param>
		/// <param name="stack">How many are being crafted</param>
		/// <returns>True when the items fit, or when the craft drops what doesn't</returns>
		public static bool CanAddItem(Inventory inventory, GameObject prefab, int stack)
		{
			return(dropExtra || inventory.CanAddItem(prefab, stack));
		}

		/// <summary>
		/// Stands in for Inventory.AddItem in the crafting code - when the craft drops what doesn't fit, adds as many
		/// as fit and drops the rest at the player's feet
		/// </summary>
		/// <remarks>
		/// The game's own AddItem drops a single plain copy of the item for each stack that doesn't fit, losing the
		/// stack size, quality and crafter, so the rest are dropped here instead
		/// </remarks>
		/// <param name="inventory">The player's inventory</param>
		/// <param name="name">The item's prefab name</param>
		/// <param name="stack">How many were crafted</param>
		/// <param name="quality">The item's quality</param>
		/// <param name="variant">The item's variant</param>
		/// <param name="crafterID">The crafting player's ID</param>
		/// <param name="crafterName">The crafting player's name</param>
		/// <param name="position">The inventory slot to put it in, or -1, -1 for the first free one</param>
		/// <param name="cheated">Whether the item counts as cheated</param>
		/// <param name="pickedUp">Whether the item counts as picked up</param>
		/// <param name="dropIfFullInv">Whether the game drops what doesn't fit</param>
		/// <returns>The last item added or dropped, or null when nothing was</returns>
		public static ItemDrop.ItemData AddItem(Inventory inventory, string name, int stack, int quality, int variant, long crafterID, string crafterName, Vector2i position, bool cheated, bool pickedUp, bool dropIfFullInv)
		{
			GameObject prefab = (dropExtra ? ObjectDB.instance.GetItemPrefab(name) : null);
			ItemDrop itemDrop = (prefab != null ? prefab.GetComponent<ItemDrop>() : null);
			if (itemDrop == null)
			{
				return(inventory.AddItem(name, stack, quality, variant, crafterID, crafterName, position, cheated, pickedUp, dropIfFullInv));
			}
			int added = Mathf.Min(stack, GetFreeSpace(inventory, itemDrop.m_itemData.m_shared, quality));
			ItemDrop.ItemData result = null;
			if (added > 0)
			{
				result = inventory.AddItem(name, added, quality, variant, crafterID, crafterName, position, cheated, pickedUp, dropIfFullInv);
			}
			if (stack > added)
			{
				ItemDrop.ItemData dropped = Drop(itemDrop, stack - added, quality, variant, crafterID, crafterName, cheated);
				if (result == null)
				{
					result = dropped;
				}
			}
			return(result);
		}

		/// <summary>
		/// Stands in for RaiseSkill in the crafting code - with Full Experience on, raises the skill once per craft in
		/// the batch instead of once for the whole batch
		/// </summary>
		/// <remarks>
		/// Skills.Skill.Raise goes up at most one level per call and resets the progress toward the next level when it
		/// does, so the experience from a large batch in one call is mostly lost at lower levels
		/// </remarks>
		/// <param name="character">The player crafting</param>
		/// <param name="skill">The station's crafting skill</param>
		/// <param name="value">How many crafts the batch held</param>
		public static void RaiseSkill(Character character, Skills.SkillType skill, float value)
		{
			if (!CraftConfig.FullExperience || value <= 1f)
			{
				character.RaiseSkill(skill, value);
				return;
			}
			int crafts = Mathf.FloorToInt(value);
			for (int i = 0; i < crafts; i++)
			{
				character.RaiseSkill(skill, 1f);
			}
			if (value > crafts)
			{
				character.RaiseSkill(skill, value - crafts);
			}
		}

		/// <summary>
		/// How many of an item fit in the inventory - the room left in its stacks of the same quality plus its empty slots
		/// </summary>
		/// <param name="inventory">The player's inventory</param>
		/// <param name="shared">The item's shared data</param>
		/// <param name="quality">The item's quality</param>
		/// <returns>The number that fit</returns>
		private static int GetFreeSpace(Inventory inventory, ItemDrop.ItemData.SharedData shared, int quality)
		{
			int space = inventory.GetEmptySlots() * shared.m_maxStackSize;
			foreach (ItemDrop.ItemData item in inventory.GetAllItems())
			{
				if (item.m_shared.m_name == shared.m_name && item.m_quality == quality && item.m_worldLevel == Game.m_worldLevel)
				{
					space += Mathf.Max(shared.m_maxStackSize - item.m_stack, 0);
				}
			}
			return(space);
		}

		/// <summary>
		/// Drops crafted items in front of the player, in full stacks
		/// </summary>
		/// <param name="itemDrop">The item's prefab</param>
		/// <param name="amount">How many to drop</param>
		/// <param name="quality">The item's quality</param>
		/// <param name="variant">The item's variant</param>
		/// <param name="crafterID">The crafting player's ID</param>
		/// <param name="crafterName">The crafting player's name</param>
		/// <param name="cheated">Whether the item counts as cheated</param>
		/// <returns>The last stack dropped</returns>
		private static ItemDrop.ItemData Drop(ItemDrop itemDrop, int amount, int quality, int variant, long crafterID, string crafterName, bool cheated)
		{
			Transform player = Player.m_localPlayer.transform;
			Vector3 position = (player.position + player.forward + Vector3.up);
			int maxStack = Mathf.Max(itemDrop.m_itemData.m_shared.m_maxStackSize, 1);
			ItemDrop.ItemData item = null;
			while (amount > 0)
			{
				item = itemDrop.m_itemData.Clone();
				item.m_dropPrefab = itemDrop.gameObject;
				item.m_stack = Mathf.Min(amount, maxStack);
				item.m_quality = quality;
				item.m_variant = variant;
				item.m_durability = item.GetMaxDurability();
				item.m_crafterID = crafterID;
				item.m_crafterName = crafterName;
				item.m_worldLevel = (byte)Game.m_worldLevel;
				item.m_cheated = cheated;
				ItemDrop.DropItem(item, item.m_stack, position, Quaternion.identity);
				amount -= item.m_stack;
			}
			return(item);
		}

		/// <summary>
		/// Replaces every call to one method in a method's instructions with a call to another
		/// </summary>
		/// <param name="instructions">The method's instructions</param>
		/// <param name="original">The method to replace</param>
		/// <param name="replacement">A static method taking the same arguments, with the instance first for an instance method</param>
		/// <returns>The instructions with the calls replaced</returns>
		public static IEnumerable<CodeInstruction> ReplaceCalls(IEnumerable<CodeInstruction> instructions, MethodInfo original, MethodInfo replacement)
		{
			foreach (CodeInstruction instruction in instructions)
			{
				if (instruction.Calls(original))
				{
					instruction.opcode = OpCodes.Call;
					instruction.operand = replacement;
				}
				yield return instruction;
			}
		}

		/// <summary>
		/// Replaces the ZInput.GetButton calls with GetButton and the Inventory.CanAddItem calls with CanAddItem
		/// </summary>
		/// <param name="instructions">The method's instructions</param>
		/// <returns>The instructions with the calls replaced</returns>
		public static IEnumerable<CodeInstruction> ReplaceButtonAndSpace(IEnumerable<CodeInstruction> instructions)
		{
			instructions = ReplaceCalls(instructions, AccessTools.Method(typeof(ZInput), nameof(ZInput.GetButton)), AccessTools.Method(typeof(Craft), nameof(GetButton)));
			return(ReplaceCalls(instructions, AccessTools.Method(typeof(Inventory), nameof(Inventory.CanAddItem), new[] { typeof(GameObject), typeof(int) }), AccessTools.Method(typeof(Craft), nameof(CanAddItem))));
		}
	}

	/// <summary>
	/// Shows the held key's amount on the recipe, its requirements and the Craft button - the amount isn't changed
	/// while an item is being crafted, since the craft finishes in this method with the amount chosen when Craft was
	/// pressed
	/// </summary>
	[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRecipe))]
	public static class UpdateRecipePatch
	{
		/// <summary>
		/// Sets the multi-craft amount for the held key, unless an item is being crafted
		/// </summary>
		/// <param name="__instance">The inventory GUI</param>
		private static void Prefix(InventoryGui __instance)
		{
			if (__instance.m_craftTimer < 0f)
			{
				Craft.ApplyAmount(__instance);
			}
		}

		/// <summary>
		/// Updates the text under the Craft button listing the keys and what they craft
		/// </summary>
		/// <param name="__instance">The inventory GUI</param>
		/// <param name="player">The player crafting</param>
		private static void Postfix(InventoryGui __instance, Player player)
		{
			Craft.ContinueBatch(__instance, player);
			Craft.ShowBatchProgress(__instance);
			CraftHint.Update(__instance, player);
		}

		/// <summary>
		/// Lets the configured keys turn on multi-crafting
		/// </summary>
		/// <param name="instructions">The method's instructions</param>
		/// <returns>The patched instructions</returns>
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			return(Craft.ReplaceCalls(instructions, AccessTools.Method(typeof(ZInput), nameof(ZInput.GetButton)), AccessTools.Method(typeof(Craft), nameof(Craft.GetButton))));
		}
	}

	/// <summary>
	/// Starts crafting the held key's amount when Craft is pressed
	/// </summary>
	[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnCraftPressed))]
	public static class OnCraftPressedPatch
	{
		/// <summary>
		/// Sets the multi-craft amount for the held key and remembers whether it crafts all
		/// </summary>
		/// <param name="__instance">The inventory GUI</param>
		private static void Prefix(InventoryGui __instance)
		{
			Craft.StartCraft(__instance, Craft.ApplyAmount(__instance));
		}

		/// <summary>
		/// Splits the batch into single crafts when One At A Time is on
		/// </summary>
		/// <param name="__instance">The inventory GUI</param>
		private static void Postfix(InventoryGui __instance)
		{
			Craft.QueueBatch(__instance);
		}

		/// <summary>
		/// Lets the configured keys turn on multi-crafting, and a Craft All start with a full inventory
		/// </summary>
		/// <param name="instructions">The method's instructions</param>
		/// <returns>The patched instructions</returns>
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			return(Craft.ReplaceButtonAndSpace(instructions));
		}
	}

	/// <summary>
	/// Lets a Craft All, or a later craft of a batch crafted one at a time, finish with a full inventory, dropping what
	/// doesn't fit, and raises the crafting skill once
	/// per craft when Full Experience is on
	/// </summary>
	[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
	public static class DoCraftingPatch
	{
		/// <summary>
		/// Replaces the inventory space check, the calls that add the crafted item and the call that raises the skill
		/// </summary>
		/// <param name="instructions">The method's instructions</param>
		/// <returns>The patched instructions</returns>
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo addItem = AccessTools.Method(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(string), typeof(int), typeof(int), typeof(int), typeof(long), typeof(string), typeof(Vector2i), typeof(bool), typeof(bool), typeof(bool) });
			instructions = Craft.ReplaceButtonAndSpace(instructions);
			instructions = Craft.ReplaceCalls(instructions, addItem, AccessTools.Method(typeof(Craft), nameof(Craft.AddItem)));
			return(Craft.ReplaceCalls(instructions, AccessTools.Method(typeof(Character), nameof(Character.RaiseSkill)), AccessTools.Method(typeof(Craft), nameof(Craft.RaiseSkill))));
		}
	}

	/// <summary>
	/// Ends a batch being crafted one at a time when the craft is canceled
	/// </summary>
	[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnCraftCancelPressed))]
	public static class OnCraftCancelPressedPatch
	{
		/// <summary>
		/// Ends the batch
		/// </summary>
		private static void Prefix()
		{
			Craft.StopBatch();
		}
	}

	/// <summary>
	/// Ends a batch being crafted one at a time when the inventory closes, which cancels the craft in progress
	/// </summary>
	[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
	public static class HidePatch
	{
		/// <summary>
		/// Ends the batch
		/// </summary>
		private static void Prefix()
		{
			Craft.StopBatch();
		}
	}
}
