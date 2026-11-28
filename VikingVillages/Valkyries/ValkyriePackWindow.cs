using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Opens a befriended valkyrie's pack beside the player's inventory, the way a chest opens, with
	/// VikingInventory.InventoryKey while looking at her - so the player can give her food to eat (see ValkyriePack)
	/// </summary>
	/// <remarks>
	/// The game's inventory window shows a chest's inventory, so her pack is handed to it through a chest that's never
	/// woken, the way a viking's inventory is (see VikingInventory). This client takes her over while it's open, so her
	/// pack's changes are written to her ZDO. A traveler's valkyrie is the traveler's, so her pack isn't opened, and it
	/// closes like a chest's when she gets too far away
	/// </remarks>
	internal static class ValkyriePackWindow
	{
		/// <summary>
		/// The valkyrie whose pack is open, and the chest that hands it to the window
		/// </summary>
		private static Character valkyrie;
		private static Container proxy;

		/// <summary>
		/// Opens the pack of the valkyrie the player looks at when the key is pressed, and keeps an open one going - call
		/// it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (proxy != null)
			{
				UpdateOpen();
				return;
			}
			Player player = Player.m_localPlayer;
			if (player == null || !ZInput.GetKeyDown(VikingInventory.InventoryKey, false) || !player.TakeInput() || InventoryGui.instance == null || InventoryGui.IsVisible())
			{
				return;
			}
			Character hovered = player.GetHoverCreature();
			// Behind her, what's looked at may be her seat rather than her body
			if (hovered == null && player.GetHoverObject() != null)
			{
				hovered = player.GetHoverObject().GetComponentInParent<Character>();
			}
			if (CanOpen(hovered, player))
			{
				Open(hovered);
			}
		}

		/// <summary>
		/// The prompt to open her pack, shown on its own line - "Inventory", as a viking's is
		/// </summary>
		/// <returns>The prompt</returns>
		public static string Prompt()
		{
			return("\n[<color=yellow><b>" + VikingInventory.InventoryKey + "</b></color>] Inventory");
		}

		/// <summary>
		/// Checks whether a player can open a valkyrie's pack - she's alive and befriended, friendly with them, and not a
		/// traveler's
		/// </summary>
		/// <param name="target">The creature</param>
		/// <param name="player">The player</param>
		/// <returns>True when they can</returns>
		public static bool CanOpen(Character target, Player player)
		{
			if (target == null || player == null || target.GetComponent<ValkyriePack>() == null || !ValkyrieSeat.CanMount(target, player))
			{
				return(false);
			}
			TravelerPet pet = TravelerPet.Of(target);
			return(pet == null || pet.Orphaned());
		}

		/// <summary>
		/// Takes her over, so her pack's changes are written to her, and shows it beside the player's inventory
		/// </summary>
		/// <param name="target">The valkyrie</param>
		private static void Open(Character target)
		{
			ZNetView view = target.m_nview;
			if (view == null || !view.IsValid() || !target.TryGetComponent(out ValkyriePack pack))
			{
				return;
			}
			if (!view.IsOwner())
			{
				view.ClaimOwnership();
			}
			valkyrie = target;
			Inventory inventory = pack.Pack();
			inventory.m_name = (target.TryGetComponent(out ValkyrieTalk talk) ? talk.ShownName() : ValkyriePrefab.DisplayName);
			// The chest stays asleep, so it never makes an inventory of its own or saves one to her
			GameObject holder = new GameObject("ValkyriePack");
			holder.SetActive(false);
			holder.transform.SetParent(target.transform, false);
			proxy = holder.AddComponent<Container>();
			proxy.m_name = inventory.m_name;
			proxy.m_nview = view;
			proxy.m_inventory = inventory;
			InventoryGui.instance.Show(proxy, 1);
		}

		/// <summary>
		/// Closes up once the window has closed - by the key again, or the game's own keys - or moved on, or she's gone,
		/// angry or no longer this client's
		/// </summary>
		private static void UpdateOpen()
		{
			InventoryGui gui = InventoryGui.instance;
			bool showing = (gui != null && InventoryGui.IsVisible() && gui.m_currentContainer == proxy);
			bool valid = (valkyrie != null && valkyrie.m_nview != null && valkyrie.m_nview.IsValid() && valkyrie.m_nview.IsOwner() && CanOpen(valkyrie, Player.m_localPlayer));
			if (showing && ZInput.GetKeyDown(VikingInventory.InventoryKey, false))
			{
				gui.Hide();
				showing = false;
			}
			if (showing && valid)
			{
				return;
			}
			if (gui != null && gui.m_currentContainer == proxy)
			{
				gui.Hide();
			}
			if (proxy != null)
			{
				Object.Destroy(proxy.gameObject);
			}
			proxy = null;
			valkyrie = null;
		}
	}
}
