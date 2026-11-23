using System.Collections.Generic;
using System.Linq;
using Kriona.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VikingVillages
{
	/// <summary>
	/// Shows everything in a village's chests in one place - opened with StockKey while looking at the village's fire, in
	/// the game's inventory window beside the player's, the way a chest opens - with a cell for every slot of every
	/// chest, each stack in a cell of its own just as it lies in its chest, followed by the empty slots, and an arrow
	/// over the chest that holds the stack the player points at, pinned to the screen's edge for a chest that's off
	/// screen. It's to look at only - nothing can be taken out or put in (see VillageStockPatches)
	/// </summary>
	/// <remarks>
	/// The window shows a chest's inventory, so the stock is handed to it through a chest that's never woken, holding
	/// copies of the chests' items - so the chests themselves are never touched. The copies are gathered again every
	/// RefreshInterval seconds, so the stock keeps up as the vikings move things about. The window closes like a
	/// chest's when the player walks away from the fire
	/// </remarks>
	internal static class VillageStock
	{
		/// <summary>
		/// The key that opens and closes the stock, while looking at the village's fire
		/// </summary>
		public const KeyCode StockKey = KeyCode.I;

		/// <summary>
		/// How many columns the stock has, the same as a chest's, and how often its items are gathered again, in seconds
		/// </summary>
		private const int Columns = 8;
		private const float RefreshInterval = 2f;

		/// <summary>
		/// The arrows over the chests - their size on a 1080 pixel tall screen, how far in from the screen's edge a
		/// pinned one's tip sits and how far one bobs over a chest, in pixels, how far above a chest's top its tip
		/// points, in meters, how fast it bobs, their color, and their canvas's place above the game's windows
		/// </summary>
		private static readonly Vector2 ArrowSize = new Vector2(24f, 32f);
		private const float EdgeMargin = 40f;
		private const float BobHeight = 8f;
		private const float ChestLift = 0.3f;
		private const float BobSpeed = 5f;
		private static readonly Color ArrowColor = new Color(1f, 0.85f, 0f, 1f);
		private const int SortingOrder = 1000;

		/// <summary>
		/// The village whose stock is open, the chest that hands the stock to the window, and the stock
		/// </summary>
		private static Village village;
		private static Container proxy;
		private static Inventory stock;

		/// <summary>
		/// The chest each stack in the stock is copied from, how many slots the chests have between them, and when the
		/// stock is next gathered again
		/// </summary>
		private static readonly Dictionary<ItemDrop.ItemData, Container> sources = new Dictionary<ItemDrop.ItemData, Container>();
		private static int slots;
		private static float nextRefresh;

		/// <summary>
		/// The window's cells hidden past the chests' last slot, in the last row of the stock, shown again once it closes
		/// </summary>
		private static readonly HashSet<GameObject> hiddenCells = new HashSet<GameObject>();

		/// <summary>
		/// How far an arrow's label stands from its tail, in pixels on a 1080 pixel tall screen, and its size
		/// </summary>
		private const float LabelGap = 2f;
		private const float LabelFontSize = 18f;

		/// <summary>
		/// An arrow over a chest, its label, and the label's size in its own units when its text was set
		/// </summary>
		/// <remarks>
		/// The label is beside the arrow rather than on it, so it stays upright when the arrow turns at an edge
		/// </remarks>
		private class Marker
		{
			public Image Arrow;
			public TextMeshProUGUI Label;
			public Vector2 LabelSize;
		}

		/// <summary>
		/// The canvas the arrows are drawn on, the arrows, their sprite, the window's weight tab hidden while the stock
		/// is open, and the corners of the window, reused as they're measured
		/// </summary>
		private static RectTransform layer;
		private static readonly List<Marker> markers = new List<Marker>();
		private static Sprite arrowSprite;
		private static GameObject hiddenWeight;
		private static readonly Vector3[] corners = new Vector3[4];

		/// <summary>
		/// Opens the stock of the village whose fire the player looks at when the key is pressed, and keeps an open one
		/// going - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (proxy != null)
			{
				UpdateOpen();
				return;
			}
			Player player = Player.m_localPlayer;
			if (player == null || !ZInput.GetKeyDown(StockKey, false) || !player.TakeInput() || InventoryGui.instance == null || InventoryGui.IsVisible())
			{
				return;
			}
			GameObject hover = player.GetHoverObject();
			Fireplace fire = ((hover != null) ? hover.GetComponentInParent<Fireplace>() : null);
			Village target = ((fire != null) ? VillageDebug.Of(fire) : null);
			if (target != null)
			{
				Open(target, fire);
			}
		}

		/// <summary>
		/// Checks whether the window shows a village's stock
		/// </summary>
		/// <param name="gui">The game's inventory window</param>
		/// <returns>True when it does</returns>
		public static bool Showing(InventoryGui gui)
		{
			return(proxy != null && gui != null && gui.m_currentContainer == proxy);
		}

		/// <summary>
		/// Checks whether an inventory is the open stock
		/// </summary>
		/// <param name="inventory">The inventory</param>
		/// <returns>True when it is</returns>
		public static bool IsStock(Inventory inventory)
		{
			return(proxy != null && inventory != null && inventory == stock);
		}

		/// <summary>
		/// Gathers the village's stock and shows it beside the player's inventory, through a chest at the fire so the
		/// window closes when the player walks away from it
		/// </summary>
		/// <param name="target">The village</param>
		/// <param name="fire">Its fire</param>
		private static void Open(Village target, Fireplace fire)
		{
			Player player = Player.m_localPlayer;
			if (player.m_nview == null || !player.m_nview.IsValid())
			{
				return;
			}
			village = target;
			string name = VillageNames.Get(target);
			stock = new Inventory(((name.Length > 0) ? name : "Village") + " stock", player.GetInventory().GetBkg(), Columns, 1);
			Gather();
			// The chest stays asleep, so it never makes an inventory of its own or saves one, and goes by the player's
			// own network view, which this client always owns
			GameObject holder = new GameObject("VillageStock");
			holder.SetActive(false);
			holder.transform.SetParent(fire.transform, false);
			proxy = holder.AddComponent<Container>();
			proxy.m_name = stock.m_name;
			proxy.m_nview = player.m_nview;
			proxy.m_inventory = stock;
			InventoryGui.instance.Show(proxy, 1);
		}

		/// <summary>
		/// Gathers copies of everything in the village's chests into the stock - each stack as it lies in its chest,
		/// sorted by kind, then by name, then by quality and then fullest first - with an empty cell after them for each
		/// slot left free, noting the chest each comes from
		/// </summary>
		private static void Gather()
		{
			nextRefresh = Time.unscaledTime + RefreshInterval;
			sources.Clear();
			List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>();
			int found = 0;
			foreach (Container chest in VillageLife.VillageChests(village))
			{
				Inventory inventory = chest.GetInventory();
				if (inventory == null)
				{
					continue;
				}
				found += inventory.GetWidth() * inventory.GetHeight();
				foreach (ItemDrop.ItemData item in inventory.GetAllItems())
				{
					ItemDrop.ItemData copy = item.Clone();
					copy.m_equipped = false;
					items.Add(copy);
					sources[copy] = chest;
				}
			}
			// OrderBy keeps stacks that sort the same in the order they were found, so they don't swap places between
			// gatherings
			items = items.OrderBy((ItemDrop.ItemData item) => item, Comparer<ItemDrop.ItemData>.Create(CompareItems)).ToList();
			slots = Mathf.Max(found, items.Count);
			stock.m_width = Columns;
			stock.m_height = Rows(slots);
			stock.m_inventory.Clear();
			for (int i = 0; i < items.Count; i++)
			{
				items[i].m_gridPos = GridPos(i);
				stock.m_inventory.Add(items[i]);
			}
			stock.Changed();
		}

		/// <summary>
		/// Works out how many rows the stock shows for so many slots - enough for them all, and at least one so the
		/// window always has a cell to select
		/// </summary>
		/// <param name="count">How many slots the village's chests have</param>
		/// <returns>The rows</returns>
		internal static int Rows(int count)
		{
			return(Mathf.Max(1, Mathf.CeilToInt(count / (float)Columns)));
		}

		/// <summary>
		/// Hides the stock window's cells past the chests' last slot, so it shows only as many cells as the chests have
		/// slots - call it whenever a grid is drawn
		/// </summary>
		/// <param name="grid">The grid just drawn</param>
		internal static void HideExtraCells(InventoryGrid grid)
		{
			if (!IsStock(grid.m_inventory))
			{
				return;
			}
			for (int i = 0; i < grid.m_elements.Count; i++)
			{
				GameObject cell = grid.m_elements[i].gameObject;
				bool show = (i < slots);
				if (cell.activeSelf != show)
				{
					cell.SetActive(show);
					hiddenCells.Add(cell);
				}
			}
		}

		/// <summary>
		/// Shows the cells hidden past the chests' last slot again, for the next chest the window opens
		/// </summary>
		private static void ShowExtraCells()
		{
			foreach (GameObject cell in hiddenCells)
			{
				if (cell != null)
				{
					cell.SetActive(true);
				}
			}
			hiddenCells.Clear();
		}

		/// <summary>
		/// Works out where a stack stands in the stock - filling each row from the left before the next
		/// </summary>
		/// <param name="index">Its place in the sorted stock</param>
		/// <returns>Its column and row</returns>
		internal static Vector2i GridPos(int index)
		{
			return(new Vector2i(index % Columns, index / Columns));
		}

		/// <summary>
		/// Works out what makes stacks of an item the same - its prefab, quality, look and the world level it was made at
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>The key</returns>
		private static string StackKey(ItemDrop.ItemData item)
		{
			string prefab = ((item.m_dropPrefab != null) ? item.m_dropPrefab.name : item.m_shared.m_name);
			return(StackKey(prefab, item.m_quality, item.m_variant, item.m_worldLevel));
		}

		/// <summary>
		/// Makes the key that's the same for stacks of the same item, quality, look and world level
		/// </summary>
		/// <param name="prefab">The item's prefab's name</param>
		/// <param name="quality">Its quality</param>
		/// <param name="variant">Its look</param>
		/// <param name="worldLevel">The world level it was made at</param>
		/// <returns>The key</returns>
		internal static string StackKey(string prefab, int quality, int variant, int worldLevel)
		{
			return(prefab + "|" + quality + "|" + variant + "|" + worldLevel);
		}

		/// <summary>
		/// Orders items by their kind, then by name, then by quality, best first, then by how many, most first
		/// </summary>
		/// <param name="a">One item</param>
		/// <param name="b">The other</param>
		/// <returns>Which goes first</returns>
		private static int CompareItems(ItemDrop.ItemData a, ItemDrop.ItemData b)
		{
			return(CompareStock(a.m_shared.m_itemType, Localization.instance.Localize(a.m_shared.m_name), a.m_quality, a.m_stack, b.m_shared.m_itemType, Localization.instance.Localize(b.m_shared.m_name), b.m_quality, b.m_stack));
		}

		/// <summary>
		/// Orders the stock's stacks by their kind, then by name whatever its case, then by quality, best first, then by
		/// how many they hold, fullest first
		/// </summary>
		/// <param name="kindA">One stack's kind</param>
		/// <param name="nameA">Its name, localized</param>
		/// <param name="qualityA">Its quality</param>
		/// <param name="stackA">How many it holds</param>
		/// <param name="kindB">The other's kind</param>
		/// <param name="nameB">Its name, localized</param>
		/// <param name="qualityB">Its quality</param>
		/// <param name="stackB">How many it holds</param>
		/// <returns>Less than 0 when the first goes first, more than 0 when the other does, and 0 when they're the same</returns>
		internal static int CompareStock(ItemDrop.ItemData.ItemType kindA, string nameA, int qualityA, int stackA, ItemDrop.ItemData.ItemType kindB, string nameB, int qualityB, int stackB)
		{
			int kind = kindA.CompareTo(kindB);
			if (kind != 0)
			{
				return(kind);
			}
			int name = string.Compare(nameA, nameB, System.StringComparison.CurrentCultureIgnoreCase);
			if (name != 0)
			{
				return(name);
			}
			int quality = qualityB.CompareTo(qualityA);
			return((quality != 0) ? quality : stackB.CompareTo(stackA));
		}

		/// <summary>
		/// Gathers the stock again every RefreshInterval seconds, points at the chests holding the item the player points
		/// at, and closes up once the window has closed - by the key again, the game's own keys, or walking away - or the
		/// village is gone
		/// </summary>
		private static void UpdateOpen()
		{
			InventoryGui gui = InventoryGui.instance;
			bool showing = (gui != null && InventoryGui.IsVisible() && gui.m_currentContainer == proxy);
			if (showing && ZInput.GetKeyDown(StockKey, false))
			{
				gui.Hide();
				showing = false;
			}
			if (!showing || village == null || village.Zdo == null || Player.m_localPlayer == null)
			{
				if (gui != null && gui.m_currentContainer == proxy)
				{
					gui.Hide();
				}
				Close();
				return;
			}
			if (Time.unscaledTime >= nextRefresh)
			{
				Gather();
			}
			HideWeight(gui, true);
			PointAt(Hovered(gui), gui);
		}

		/// <summary>
		/// Finds the item in the stock the player points at - with the mouse, or selects with a gamepad
		/// </summary>
		/// <param name="gui">The game's inventory window</param>
		/// <returns>The item, or null when they point at none</returns>
		private static ItemDrop.ItemData Hovered(InventoryGui gui)
		{
			InventoryGrid grid = gui.m_containerGrid;
			if (grid == null || grid.GetInventory() != stock)
			{
				return(null);
			}
			if (ZInput.IsGamepadActive())
			{
				return(grid.m_uiGroup.IsActive ? grid.GetGamepadSelectedItem() : null);
			}
			InventoryElement element = grid.GetHoveredElement();
			if (element == null)
			{
				return(null);
			}
			Vector2i pos = grid.GetElementPos(element);
			return((pos.x >= 0) ? stock.GetItemAt(pos.x, pos.y) : null);
		}

		/// <summary>
		/// Puts an arrow over the chest a stack in the stock is copied from, with the item's name and how many of it the
		/// chest holds over the arrow - just above the chest and bobbing while it's on screen, pinned to the edge of the
		/// stock's window pointing in at it while the window hides it, or pinned to the screen's edge pointing its way
		/// while it's off screen - and hides the rest
		/// </summary>
		/// <param name="item">The stack, or null to hide them all</param>
		/// <param name="gui">The game's inventory window, or null</param>
		private static void PointAt(ItemDrop.ItemData item, InventoryGui gui)
		{
			Camera camera = Utils.GetMainCamera();
			Container chest = ((item != null && camera != null && sources.TryGetValue(item, out Container found)) ? found : null);
			int used = 0;
			if (chest != null)
			{
				EnsureLayer();
				Vector2 screenSize = new Vector2(Screen.width, Screen.height);
				float scale = Screen.height / 1080f;
				Vector3 player = Player.m_localPlayer.transform.position;
				bool hasCard = Card(gui, out Rect card);
				string name = Localization.instance.Localize(item.m_shared.m_name);
				Marker marker = GetMarker(0, gui);
				used = 1;
				marker.Arrow.rectTransform.localScale = Vector3.one * scale;
				marker.Label.rectTransform.localScale = Vector3.one * scale;
				Vector3 top = ChestTop(chest);
				Vector3 screen = camera.WorldToScreenPointScaled(top);
				Vector2 tip;
				Vector2 pointing;
				if (ScreenEdge.OnScreen(screen, screenSize) && hasCard && card.Contains(new Vector2(screen.x, screen.y)))
				{
					tip = ScreenEdge.PinToRect(new Vector2(screen.x, screen.y), card, out pointing);
				}
				else if (ScreenEdge.OnScreen(screen, screenSize))
				{
					float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * BobSpeed)) * BobHeight * scale;
					tip = new Vector2(screen.x, screen.y + bob);
					pointing = Vector2.down;
				}
				else
				{
					bool higher = (top.y > player.y + 1f);
					tip = ScreenEdge.Pin(screen, screenSize, EdgeMargin * scale, higher, out pointing);
				}
				marker.Arrow.rectTransform.position = new Vector3(tip.x, tip.y, 0f);
				marker.Arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ScreenEdge.Angle(pointing));
				SetLabel(marker, name, Holds(chest, item));
				Vector2 half = marker.LabelSize * scale * 0.5f;
				Vector2 middle = ScreenEdge.Behind(tip, pointing, (ArrowSize.y + LabelGap) * scale, half, screenSize);
				marker.Label.rectTransform.position = new Vector3(middle.x, middle.y, 0f);
			}
			for (int i = used; i < markers.Count; i++)
			{
				if (markers[i].Arrow != null && markers[i].Arrow.gameObject.activeSelf)
				{
					markers[i].Arrow.gameObject.SetActive(false);
					markers[i].Label.gameObject.SetActive(false);
				}
			}
		}

		/// <summary>
		/// Finds where the stock's window is on screen
		/// </summary>
		/// <param name="gui">The game's inventory window, or null</param>
		/// <param name="card">Set to the window, in pixels</param>
		/// <returns>False when it isn't showing</returns>
		private static bool Card(InventoryGui gui, out Rect card)
		{
			card = default(Rect);
			if (gui == null || gui.m_container == null || !gui.m_container.gameObject.activeInHierarchy)
			{
				return(false);
			}
			gui.m_container.GetWorldCorners(corners);
			card = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
			return(card.width > 0f && card.height > 0f);
		}

		/// <summary>
		/// Counts how many of an item a chest holds - every stack of the same item, quality and look
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="item">The item, in the stock</param>
		/// <returns>How many</returns>
		private static int Holds(Container chest, ItemDrop.ItemData item)
		{
			Inventory inventory = chest.GetInventory();
			if (inventory == null)
			{
				return(0);
			}
			string key = StackKey(item);
			int count = 0;
			foreach (ItemDrop.ItemData held in inventory.GetAllItems())
			{
				if (StackKey(held) == key)
				{
					count += held.m_stack;
				}
			}
			return(count);
		}

		/// <summary>
		/// Sets an arrow's label to an item's name, and how many a chest holds when that's more than one, sizing it to
		/// fit - only when it changes
		/// </summary>
		/// <param name="marker">The arrow</param>
		/// <param name="name">The item's name</param>
		/// <param name="count">How many the chest holds</param>
		private static void SetLabel(Marker marker, string name, int count)
		{
			string text = ((count > 1) ? name + " x" + count : name);
			if (marker.Label.text == text)
			{
				return;
			}
			marker.Label.text = text;
			marker.LabelSize = marker.Label.GetPreferredValues();
			marker.Label.rectTransform.sizeDelta = marker.LabelSize;
		}

		/// <summary>
		/// Finds the point just above a chest an arrow points at - ChestLift over the top of its colliders, or over where
		/// it stands when it has none
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>The point</returns>
		private static Vector3 ChestTop(Container chest)
		{
			Vector3 top = chest.transform.position;
			foreach (Collider collider in chest.GetComponentsInChildren<Collider>())
			{
				if (collider != null && collider.enabled && !collider.isTrigger)
				{
					top.y = Mathf.Max(top.y, collider.bounds.max.y);
				}
			}
			return(top + Vector3.up * ChestLift);
		}

		/// <summary>
		/// Gets the pooled arrow at an index, with its label, shown, making them when there aren't any yet
		/// </summary>
		/// <param name="index">Its place in the pool</param>
		/// <param name="gui">The game's inventory window, whose chest's name the label's font is copied from, or null</param>
		/// <returns>The arrow</returns>
		private static Marker GetMarker(int index, InventoryGui gui)
		{
			while (markers.Count <= index)
			{
				markers.Add(new Marker());
			}
			Marker marker = markers[index];
			if (marker.Arrow == null)
			{
				GameObject go = new GameObject("VillageStockArrow", typeof(RectTransform));
				go.transform.SetParent(layer, false);
				Image image = go.AddComponent<Image>();
				image.sprite = arrowSprite;
				image.color = ArrowColor;
				image.raycastTarget = false;
				image.rectTransform.pivot = new Vector2(0.5f, 0f);
				image.rectTransform.sizeDelta = ArrowSize;
				marker.Arrow = image;
			}
			if (marker.Label == null)
			{
				GameObject go = new GameObject("VillageStockLabel", typeof(RectTransform));
				go.transform.SetParent(layer, false);
				TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
				TMP_Text template = ((gui != null) ? gui.m_containerName : null);
				if (template != null)
				{
					label.font = template.font;
					label.fontSharedMaterial = template.fontSharedMaterial;
				}
				label.fontSize = LabelFontSize;
				label.color = ArrowColor;
				label.alignment = TextAlignmentOptions.Center;
				label.textWrappingMode = TextWrappingModes.NoWrap;
				label.raycastTarget = false;
				label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
				marker.Label = label;
			}
			if (!marker.Arrow.gameObject.activeSelf)
			{
				marker.Arrow.gameObject.SetActive(true);
				marker.Label.gameObject.SetActive(true);
			}
			return(marker);
		}

		/// <summary>
		/// Hides the stock's window's weight tab while the stock is open, which weighs nothing real, and shows it again
		/// once it's closed
		/// </summary>
		/// <param name="gui">The game's inventory window, or null</param>
		/// <param name="hide">Whether to hide it</param>
		private static void HideWeight(InventoryGui gui, bool hide)
		{
			if (!hide)
			{
				if (hiddenWeight != null)
				{
					hiddenWeight.SetActive(true);
				}
				hiddenWeight = null;
				return;
			}
			if (hiddenWeight != null || gui == null)
			{
				return;
			}
			RectTransform tab = VikingInventoryLayout.PanelChild(gui.m_container, gui.m_containerWeight);
			if (tab != null && tab.gameObject.activeSelf)
			{
				hiddenWeight = tab.gameObject;
				hiddenWeight.SetActive(false);
			}
		}

		/// <summary>
		/// Makes the canvas the arrows are drawn on, over the game's windows, and their sprite, the first time they're
		/// wanted
		/// </summary>
		private static void EnsureLayer()
		{
			if (arrowSprite == null)
			{
				arrowSprite = ArrowSprite.Create();
			}
			if (layer != null)
			{
				return;
			}
			markers.Clear();
			GameObject go = new GameObject("VillageStockArrows", typeof(RectTransform));
			Object.DontDestroyOnLoad(go);
			Canvas canvas = go.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = SortingOrder;
			layer = (RectTransform)go.transform;
		}

		/// <summary>
		/// Hides the arrows, shows the window's weight tab and hidden cells again and takes the chest and its stock away
		/// </summary>
		private static void Close()
		{
			PointAt(null, null);
			HideWeight(null, false);
			ShowExtraCells();
			if (proxy != null)
			{
				Object.Destroy(proxy.gameObject);
			}
			proxy = null;
			stock = null;
			village = null;
			sources.Clear();
		}
	}
}
