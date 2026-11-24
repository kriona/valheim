using HarmonyLib;
using Kriona.Vikings;
using UnityEngine;
using UnityEngine.UI;

namespace VikingVillages
{
	/// <summary>
	/// Listens for the village's calls for help, alarms and effects in each world
	/// </summary>
	[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
	public static class VillageSignalsPatch
	{
		/// <summary>
		/// Registers the village's RPCs once the world's routed RPC is up
		/// </summary>
		private static void Postfix()
		{
			VillageSignals.Register();
		}
	}

	/// <summary>
	/// Keeps living vikings from picking fights with monsters that aren't a threat, or that are out in water too deep
	/// to fight them in - each goes after the nearest one it can fight instead, or none. A viking archer slightly
	/// prefers enemy archers (see VillageLife.NearestThreat)
	/// </summary>
	/// <remarks>
	/// A monster that attacks a viking is still fought, since the AI takes on its attacker without looking for an
	/// enemy, and so is one the vikings are called to help against
	/// </remarks>
	[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.FindEnemy))]
	public static class CalmEnemyPatch
	{
		/// <summary>
		/// Swaps an enemy that isn't a threat for the nearest one that is, and an archer's pick for the threat it
		/// prefers
		/// </summary>
		/// <param name="__instance">The AI looking for an enemy</param>
		/// <param name="__result">The enemy it found, or null</param>
		private static void Postfix(BaseAI __instance, ref Character __result)
		{
			if (__result == null || !__instance.TryGetComponent(out VillageLife life) || !life.PicksFightsWithThreats())
			{
				return;
			}
			if (!life.PrefersArchers() && life.IsThreat(__result) && !life.OutOfReach(__result))
			{
				return;
			}
			__result = life.NearestThreat();
		}
	}

	/// <summary>
	/// Has right-clicking an item in an open viking's inventory make the viking take it off when it wears it, or put it
	/// on when it doesn't, rather than the player using it (see VikingInventory.Toggle)
	/// </summary>
	[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnRightClickItem))]
	public static class VikingInventoryRightClickPatch
	{
		/// <summary>
		/// Has the viking take the item off or put it on, and skips the game's use of it, in the viking's inventory
		/// </summary>
		/// <param name="grid">The grid clicked in</param>
		/// <param name="item">The item, or null</param>
		/// <returns>False in a viking's inventory, to skip the game's use</returns>
		private static bool Prefix(InventoryGrid grid, ItemDrop.ItemData item)
		{
			if (grid == null || !VikingInventory.IsVikingInventory(grid.GetInventory()))
			{
				return(true);
			}
			VikingInventory.Toggle(item);
			return(false);
		}
	}

	/// <summary>
	/// Marks what an open viking wears in its inventory the way a player's equipped items are marked - the game only
	/// marks them in a player's own inventory
	/// </summary>
	[HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
	public static class VikingInventoryWornPatch
	{
		/// <summary>
		/// Marks the viking's worn items once the grid has drawn them
		/// </summary>
		/// <param name="__instance">The grid</param>
		private static void Postfix(InventoryGrid __instance)
		{
			Inventory inventory = __instance.m_inventory;
			if (!VikingInventory.IsVikingInventory(inventory))
			{
				return;
			}
			int width = inventory.GetWidth();
			foreach (ItemDrop.ItemData item in inventory.GetAllItems())
			{
				InventoryElement element = __instance.GetElement(item.m_gridPos.x, item.m_gridPos.y, width);
				if (element != null)
				{
					element.m_equiped.enabled = VikingInventory.IsWorn(item);
					element.m_queued.enabled = VikingInventory.IsQueued(item);
				}
			}
		}
	}

	/// <summary>
	/// Leaves out a chest's take-all and place-stacks buttons while a viking's inventory or the village's stock (see
	/// VillageStock) is open, and puts them back for a chest
	/// </summary>
	[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateContainer))]
	public static class VikingInventoryButtonsPatch
	{
		/// <summary>
		/// Hides or shows the buttons as the window shows a viking's inventory or the village's stock or not
		/// </summary>
		/// <param name="__instance">The game's inventory window</param>
		private static void Postfix(InventoryGui __instance)
		{
			bool shown = !(VikingInventory.Showing(__instance) || VillageStock.Showing(__instance));
			Show(__instance.m_takeAllButton, shown);
			Show(__instance.m_stackAllButton, shown);
		}

		/// <summary>
		/// Shows or hides a button, going by whether it shows now, since each world has a window of its own
		/// </summary>
		/// <param name="button">The button, or null</param>
		/// <param name="shown">Whether it shows</param>
		private static void Show(Button button, bool shown)
		{
			if (button != null && button.gameObject.activeSelf != shown)
			{
				button.gameObject.SetActive(shown);
			}
		}
	}

	/// <summary>
	/// Leaves out placing stacks by holding the use key while a viking's inventory or the village's stock is open, the
	/// way a chest's are placed
	/// </summary>
	[HarmonyPatch(typeof(Container), nameof(Container.StackAll))]
	public static class VikingInventoryStackPatch
	{
		/// <summary>
		/// Skips placing stacks into the viking's inventory or the village's stock
		/// </summary>
		/// <param name="__instance">The chest</param>
		/// <returns>False for either, to skip it</returns>
		private static bool Prefix(Container __instance)
		{
			return(!VikingInventory.IsVikingInventory(__instance.m_inventory) && !VillageStock.IsStock(__instance.m_inventory));
		}
	}

	/// <summary>
	/// Shows how much an open viking can carry beside what its inventory weighs, the way the player's weight shows -
	/// flashing red when it carries more
	/// </summary>
	[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateContainerWeight))]
	public static class VikingInventoryWeightPatch
	{
		/// <summary>
		/// Puts the viking's carry weight after its inventory's weight
		/// </summary>
		/// <param name="__instance">The game's inventory window</param>
		private static void Postfix(InventoryGui __instance)
		{
			if (VikingInventory.Showing(__instance) && VikingInventory.OpenViking() is LivingVikingBody viking)
			{
				__instance.m_containerWeight.text = viking.WeightText();
			}
		}
	}

	/// <summary>
	/// Shows what vikings say to each other under their health bars, keeping a speaking viking's bar showing (see
	/// VikingChatterHud)
	/// </summary>
	[HarmonyPatch(typeof(EnemyHud), nameof(EnemyHud.UpdateHuds))]
	public static class VikingChatterHudPatch
	{
		/// <summary>
		/// Keeps the bars of speaking vikings showing
		/// </summary>
		/// <param name="__instance">The game's health bars</param>
		private static void Prefix(EnemyHud __instance)
		{
			VikingChatterHud.BeforeUpdate(__instance);
		}

		/// <summary>
		/// Shows the speaking vikings' lines under their bars
		/// </summary>
		/// <param name="__instance">The game's health bars</param>
		private static void Postfix(EnemyHud __instance)
		{
			VikingChatterHud.AfterUpdate(__instance);
		}
	}

	/// <summary>
	/// Keeps a living viking's name over its health bar clear of the bar while it has its job under it (see
	/// VikingHudName)
	/// </summary>
	[HarmonyPatch(typeof(EnemyHud), nameof(EnemyHud.UpdateHuds))]
	public static class VikingHudNamePatch
	{
		/// <summary>
		/// Lifts the names once the game has set them
		/// </summary>
		/// <param name="__instance">The game's health bars</param>
		private static void Postfix(EnemyHud __instance)
		{
			VikingHudName.AfterUpdate(__instance);
		}
	}

	/// <summary>
	/// Notes the local player in the world as they spawn, so no viking takes their name
	/// </summary>
	[HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
	public static class VikingNamePlayerPatch
	{
		/// <summary>
		/// Notes the player once they've spawned
		/// </summary>
		/// <param name="__instance">The player</param>
		private static void Postfix(Player __instance)
		{
			if (__instance == Player.m_localPlayer)
			{
				VikingNames.NotePlayer(__instance);
			}
		}
	}

	/// <summary>
	/// Keeps a living viking fighting hand to hand from taking a root, or any other monster that can't move, as an
	/// enemy, so its AI never walks into the root's reach to strike it - it keeps clear and calls an archer instead
	/// (see VillageLife.UpdateRoots). The root still takes the viking as an enemy, and an archer shoots it
	/// </summary>
	[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.IsEnemy), typeof(Character), typeof(Character))]
	public static class RootEnemyPatch
	{
		/// <summary>
		/// Turns the viking's side of it down for a root
		/// </summary>
		/// <param name="a">The creature looking</param>
		/// <param name="b">The creature looked at</param>
		/// <param name="__result">Whether a takes b as an enemy</param>
		private static void Postfix(Character a, Character b, ref bool __result)
		{
			if (__result && MonsterThreat.IsRoot(b) && VikingCore.IsLivingViking(a) && !VillageLife.HasBow(a))
			{
				__result = false;
			}
		}
	}

	/// <summary>
	/// Makes a hunting viking and its prey enemies, so its AI goes for prey it would otherwise leave alone, like a deer,
	/// and keeps the living vikings and the village's livestock at peace otherwise - the livestock never takes a viking
	/// for an enemy, so it doesn't fight back when it's slaughtered, and a viking only takes the animal it's
	/// slaughtering for one
	/// </summary>
	/// <remarks>
	/// The living vikings are of the Dverger faction, which the game sets against tamed animals while it's aggravated,
	/// as a viking is after a player hurts it
	/// </remarks>
	[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.IsEnemy), typeof(Character), typeof(Character))]
	public static class HuntEnemyPatch
	{
		/// <summary>
		/// Turns the pair into enemies when one hunts the other, and keeps a living viking and livestock apart unless
		/// the viking hunts it
		/// </summary>
		/// <param name="a">One creature</param>
		/// <param name="b">The other</param>
		/// <param name="__result">Whether they're enemies</param>
		private static void Postfix(Character a, Character b, ref bool __result)
		{
			if (a == null || b == null)
			{
				return;
			}
			bool vikingAndLivestock = (VikingCore.IsLivingViking(a) && FarmSpawns.IsLivestock(b));
			bool livestockAndViking = (!vikingAndLivestock && VikingCore.IsLivingViking(b) && FarmSpawns.IsLivestock(a));
			__result = Enemies(vikingAndLivestock, livestockAndViking, VillageLife.Hunts(a, b), VillageLife.Hunts(b, a), __result);
		}

		/// <summary>
		/// Works out whether a pair are enemies - a living viking and livestock only when the viking hunts it, livestock
		/// and a living viking never, and any other pair when the game has them as enemies or one hunts the other
		/// </summary>
		/// <param name="vikingAndLivestock">Whether the first is a living viking and the other livestock</param>
		/// <param name="livestockAndViking">Whether the first is livestock and the other a living viking</param>
		/// <param name="firstHunts">Whether the first hunts the other</param>
		/// <param name="otherHunts">Whether the other hunts the first</param>
		/// <param name="enemies">Whether the game has them as enemies</param>
		/// <returns>True when they're enemies</returns>
		internal static bool Enemies(bool vikingAndLivestock, bool livestockAndViking, bool firstHunts, bool otherHunts, bool enemies)
		{
			if (vikingAndLivestock)
			{
				return(firstHunts);
			}
			if (livestockAndViking)
			{
				return(false);
			}
			return(enemies || firstHunts || otherHunts);
		}
	}

	/// <summary>
	/// Keeps the village's livestock calm when a living viking hurts it - slaughtering it, or with a stray blow - so it
	/// doesn't turn on the viking or run from it, as it would from anything else that hurts it
	/// </summary>
	/// <remarks>
	/// A hurt creature is alerted and takes what hurt it as its target here, and every way an animal runs away goes by
	/// its target, so with none it stays where it is
	/// </remarks>
	[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.OnDamaged))]
	public static class LivestockCalmPatch
	{
		/// <summary>
		/// Skips the creature's reaction when it's livestock and a living viking hurt it
		/// </summary>
		/// <param name="__instance">The AI of the creature hurt</param>
		/// <param name="attacker">Who hurt it, or null</param>
		/// <returns>False for livestock hurt by a living viking, to skip its reaction</returns>
		private static bool Prefix(MonsterAI __instance, Character attacker)
		{
			return(attacker == null || !VikingCore.IsLivingViking(attacker) || !FarmSpawns.IsLivestock(__instance));
		}
	}

	/// <summary>
	/// Calls the living vikings to help when a player or living viking is hurt by a zombie or monster
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage))]
	public static class VillageDistressPatch
	{
		/// <summary>
		/// Calls for help once the hit has landed, on the client that owns the victim
		/// </summary>
		/// <param name="__instance">The creature hit</param>
		/// <param name="hit">The hit</param>
		private static void Postfix(Character __instance, HitData hit)
		{
			if (__instance == null || hit == null || __instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner())
			{
				return;
			}
			VillageSignals.OnHurt(__instance, hit.GetAttacker());
		}
	}

	/// <summary>
	/// Wakes a sleeping monster for a living viking that comes near, the way it wakes for a player (see MonsterWake)
	/// </summary>
	[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateSleep))]
	public static class MonsterWakePatch
	{
		/// <summary>
		/// Looks for living vikings near the monster once the game has checked for players
		/// </summary>
		/// <param name="__instance">The monster's AI</param>
		private static void Postfix(MonsterAI __instance)
		{
			MonsterWake.Check(__instance);
		}
	}

	/// <summary>
	/// Calls the living vikings to help when a zombie or monster hits a building piece in a village
	/// </summary>
	[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.RPC_Damage))]
	public static class VillagePieceDistressPatch
	{
		/// <summary>
		/// Calls for help as the hit lands, on the client that owns the piece, while the piece still stands
		/// </summary>
		/// <param name="__instance">The piece hit</param>
		/// <param name="hit">The hit</param>
		private static void Prefix(WearNTear __instance, HitData hit)
		{
			if (__instance == null || hit == null || __instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner())
			{
				return;
			}
			VillageSignals.OnPieceHit(__instance, hit.GetAttacker());
		}
	}

	/// <summary>
	/// Holds a living viking still on the bed or chair it's on after the game's motion update, which turns its gravity
	/// back on
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.UpdateMotion))]
	public static class VillageSeatHoldPatch
	{
		/// <summary>
		/// Holds the viking on its seat
		/// </summary>
		/// <param name="__instance">The creature that moved</param>
		private static void Postfix(Character __instance)
		{
			if (__instance is Humanoid && __instance.TryGetComponent(out VillageLife life))
			{
				life.AfterMotion();
			}
		}
	}
}
