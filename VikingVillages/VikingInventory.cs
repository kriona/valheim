using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Opens a living viking's inventory beside the player's, the way a chest opens - the player's on the left and the
	/// viking's on the right - with InventoryKey while looking at the viking, so the player can move things between them
	/// by dragging or with ctrl-click, and make the viking take off what it has equipped with right-click
	/// </summary>
	/// <remarks>
	/// The game's inventory window shows a chest's inventory, so the viking's is handed to it through a chest that's
	/// never woken - its own saving and networking never run, and the viking saves its inventory as it changes the way
	/// it always does. This client takes the viking over while it's open, so its changes are the viking's own. The
	/// viking stands facing the player and leaves its gear as it is until the window closes, which it does like a
	/// chest's when the viking gets too far away - called away to a fight, say
	/// </remarks>
	internal static class VikingInventory
	{
		/// <summary>
		/// The key that opens a viking's inventory
		/// </summary>
		public const KeyCode InventoryKey = KeyCode.I;

		/// <summary>
		/// The viking whose inventory is open, its life, and the chest that hands its inventory to the window
		/// </summary>
		private static Humanoid viking;
		private static VillageLife life;
		private static Container proxy;

		/// <summary>
		/// The weapon the open viking wields as a rule, and where it lay when last looked at
		/// </summary>
		private static ItemDrop.ItemData mainWeapon;
		private static Vector2i mainWeaponAt;

		/// <summary>
		/// The name the viking's inventory had before the window showed it under the viking's name
		/// </summary>
		private static string inventoryName;

		/// <summary>
		/// How long the window's layout for the viking stays once the window starts closing, so it animates closing as
		/// it looked, in seconds, and when it's put back, or -1 while it isn't waiting to be
		/// </summary>
		private const float RestoreDelay = 0.5f;
		private static float restoreAt = -1f;

		/// <summary>
		/// Opens the inventory of the viking the player looks at when the key is pressed, and keeps an open one going -
		/// call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (proxy != null)
			{
				UpdateOpen();
				return;
			}
			// The window keeps the viking's layout while it animates closing, and is put back once it's closed, or as soon
			// as it opens again
			if (restoreAt >= 0f && (Time.unscaledTime >= restoreAt || Opened()))
			{
				RestoreLayout();
			}
			Player player = Player.m_localPlayer;
			if (player == null || !ZInput.GetKeyDown(InventoryKey, false) || !player.TakeInput() || InventoryGui.instance == null || InventoryGui.IsVisible())
			{
				return;
			}
			Character hovered = player.GetHoverCreature();
			if (hovered != null && hovered is Humanoid humanoid && hovered.TryGetComponent(out VillageLife found) && found.CanTalkTo(player))
			{
				RestoreLayout();
				Open(humanoid, found);
			}
		}

		/// <summary>
		/// Checks whether the inventory window is open, or opening - not counting the frame after it starts closing,
		/// which InventoryGui.IsVisible still counts
		/// </summary>
		/// <returns>True when it is</returns>
		private static bool Opened()
		{
			InventoryGui gui = InventoryGui.instance;
			return(gui != null && gui.m_animator != null && gui.m_animator.GetBool("visible"));
		}

		/// <summary>
		/// Puts the window's layout back as it was, when the viking's is still waiting to be taken down
		/// </summary>
		private static void RestoreLayout()
		{
			if (restoreAt < 0f)
			{
				return;
			}
			restoreAt = -1f;
			VikingInventoryLayout.Restore(InventoryGui.instance);
		}

		/// <summary>
		/// Gets the viking whose inventory is open
		/// </summary>
		/// <returns>The viking, or null when none is</returns>
		public static Humanoid OpenViking()
		{
			return((proxy != null) ? viking : null);
		}

		/// <summary>
		/// Checks whether an inventory is the open viking's
		/// </summary>
		/// <param name="inventory">The inventory</param>
		/// <returns>True when it is</returns>
		public static bool IsVikingInventory(Inventory inventory)
		{
			return(proxy != null && viking != null && inventory != null && inventory == viking.GetInventory());
		}

		/// <summary>
		/// Checks whether the open viking wears an item - equipped, or sheathed on its back
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it does</returns>
		public static bool IsWorn(ItemDrop.ItemData item)
		{
			return(viking != null && item != null && (viking.IsItemEquiped(item) || item == viking.m_hiddenRightItem || item == viking.m_hiddenLeftItem));
		}

		/// <summary>
		/// Has the open viking take off an item it wears - equipped, or sheathed on its back - taking the item's equip time
		/// the way a player does
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it starts taking it off</returns>
		public static bool Unequip(ItemDrop.ItemData item)
		{
			LivingVikingBody body = OpenBody();
			if (!IsWorn(item) || body == null)
			{
				return(false);
			}
			body.TakeOffGear(item);
			return(true);
		}

		/// <summary>
		/// Has the open viking put on or take off an item, the way right-clicking one of the player's own does - taking
		/// off what it wears, and putting on what it doesn't, whether or not it's better than its own, each taking the
		/// item's equip time the way a player does
		/// </summary>
		/// <param name="item">The item, or null</param>
		public static void Toggle(ItemDrop.ItemData item)
		{
			LivingVikingBody body = OpenBody();
			if (item == null || body == null)
			{
				return;
			}
			if (IsWorn(item))
			{
				body.TakeOffGear(item);
			}
			else if (item.IsEquipable())
			{
				body.PutOnGear(item);
			}
		}

		/// <summary>
		/// Checks whether the open viking is about to put on or take off an item, which shows the way a player's queued
		/// gear does
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it is</returns>
		public static bool IsQueued(ItemDrop.ItemData item)
		{
			LivingVikingBody body = OpenBody();
			return(body != null && item != null && body.GearQueued(item));
		}

		/// <summary>
		/// Gets the body of the viking whose inventory is open, for its gear
		/// </summary>
		/// <returns>The body, or null when no viking's is open</returns>
		private static LivingVikingBody OpenBody()
		{
			return((life != null) ? viking as LivingVikingBody : null);
		}

		/// <summary>
		/// Checks whether the window shows the open viking's inventory
		/// </summary>
		/// <param name="gui">The game's inventory window</param>
		/// <returns>True when it does</returns>
		public static bool Showing(InventoryGui gui)
		{
			return(proxy != null && gui != null && gui.m_currentContainer == proxy);
		}

		/// <summary>
		/// Takes the viking over, so its inventory changes are its own, and shows its inventory beside the player's
		/// </summary>
		/// <param name="target">The viking</param>
		/// <param name="targetLife">Its life</param>
		private static void Open(Humanoid target, VillageLife targetLife)
		{
			ZNetView view = target.m_nview;
			if (view == null || !view.IsValid())
			{
				return;
			}
			if (!view.IsOwner())
			{
				view.ClaimOwnership();
			}
			viking = target;
			life = targetLife;
			// A viking that doesn't know the player greets them, now that this client runs it
			life.GreetIfStranger(Player.m_localPlayer);
			Inventory inventory = target.GetInventory();
			// A prop it holds for show, like a hammer, isn't its own to hand over
			VikingItems.RemoveProps(inventory);
			inventoryName = inventory.m_name;
			// Its name shows only once it knows the player
			string name = ((target is LivingVikingBody body) ? body.ShownName() : VikingNames.Get(target));
			inventory.m_name = ((name.Length > 0) ? name : "Viking");
			// The chest stays asleep, so it never makes an inventory of its own or saves one to the viking
			GameObject holder = new GameObject("VikingInventory");
			holder.SetActive(false);
			holder.transform.SetParent(target.transform, false);
			proxy = holder.AddComponent<Container>();
			proxy.m_name = inventory.m_name;
			proxy.m_nview = view;
			proxy.m_inventory = inventory;
			life.HoldForInventory(Player.m_localPlayer);
			InventoryGui.instance.Show(proxy, 1);
			VikingInventoryLayout.Apply(InventoryGui.instance, target, inventory.m_name);
		}

		/// <summary>
		/// Keeps the open viking facing the player with its gear left as it is, has it take off what's been taken out
		/// of its inventory, and closes up once the window has closed - by the key again, or the game's own keys - or
		/// moved on, or the viking is gone or no longer this client's
		/// </summary>
		private static void UpdateOpen()
		{
			InventoryGui gui = InventoryGui.instance;
			bool showing = (gui != null && InventoryGui.IsVisible() && gui.m_currentContainer == proxy);
			bool valid = (viking != null && !viking.IsDead() && viking.m_nview != null && viking.m_nview.IsValid() && viking.m_nview.IsOwner() && life != null);
			// The key closes the window again
			if (showing && ZInput.GetKeyDown(InventoryKey, false))
			{
				gui.Hide();
				showing = false;
			}
			if (!showing || !valid || Player.m_localPlayer == null)
			{
				if (valid && gui != null && gui.m_currentContainer == proxy)
				{
					gui.Hide();
				}
				Close();
				return;
			}
			life.HoldForInventory(Player.m_localPlayer);
			TakeOffMissing();
			FollowMainWeapon();
			VikingInventoryLayout.Update(viking);
		}

		/// <summary>
		/// Keeps the weapon the viking wields as a rule its own when the player moves it within the viking's inventory,
		/// since it's noted by where it lies - while taking up the one the player had it put on in its place
		/// </summary>
		private static void FollowMainWeapon()
		{
			VikingCore core = viking.GetComponent<VikingCore>();
			if (core == null)
			{
				return;
			}
			// Moved from where it lay, while still noted there, it's followed to where it lies now
			if (mainWeapon != null && viking.GetInventory().ContainsItem(mainWeapon) && mainWeapon.m_gridPos != mainWeaponAt && core.WeaponSlot() == mainWeaponAt)
			{
				core.FollowWeapon(mainWeapon);
			}
			mainWeapon = core.MainWeaponItem();
			mainWeaponAt = ((mainWeapon != null) ? mainWeapon.m_gridPos : new Vector2i(-1, -1));
		}

		/// <summary>
		/// Has the viking take off whatever it has equipped that's no longer in its inventory - moved to the player's -
		/// and forget what it shows sheathed on its back that's gone, so nothing shows as worn that it doesn't have
		/// </summary>
		private static void TakeOffMissing()
		{
			Inventory inventory = viking.GetInventory();
			ItemDrop.ItemData[] worn = { viking.m_rightItem, viking.m_leftItem, viking.m_chestItem, viking.m_legItem, viking.m_helmetItem, viking.m_shoulderItem, viking.m_utilityItem, viking.m_ammoItem, viking.m_trinketItem };
			foreach (ItemDrop.ItemData item in worn)
			{
				if (item == null)
				{
					continue;
				}
				if (!inventory.ContainsItem(item))
				{
					viking.UnequipItem(item, false);
				}
				else
				{
					// Dragging an item about clears its mark as worn, which the viking still wears
					item.m_equipped = true;
				}
			}
			// Taking off a sheathed item only forgets it on the viking's back, since it isn't equipped
			foreach (ItemDrop.ItemData hidden in new ItemDrop.ItemData[] { viking.m_hiddenRightItem, viking.m_hiddenLeftItem })
			{
				if (hidden != null && !inventory.ContainsItem(hidden))
				{
					viking.UnequipItem(hidden, false);
				}
			}
		}

		/// <summary>
		/// Lets the viking go back to what it was doing and look after its gear again, gives its inventory back its
		/// name and takes the chest away - leaving the window's layout for the viking up for RestoreDelay seconds, while
		/// the window animates closing
		/// </summary>
		private static void Close()
		{
			// The layout stays while the window animates closing, so it closes as it looked (see Update)
			restoreAt = Time.unscaledTime + RestoreDelay;
			if (viking != null)
			{
				Inventory inventory = viking.GetInventory();
				if (inventoryName != null)
				{
					inventory.m_name = inventoryName;
				}
				TakeOffMissing();
			}
			if (life != null)
			{
				life.ReleaseInventory();
			}
			if (proxy != null)
			{
				Object.Destroy(proxy.gameObject);
			}
			proxy = null;
			viking = null;
			life = null;
			inventoryName = null;
			mainWeapon = null;
		}
	}
}
