using System.Collections.Generic;
using Kriona.Vikings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VikingVillages
{
	/// <summary>
	/// Lays the inventory window out for a viking's inventory - the player's card on top of the player's inventory on
	/// the left, a card for the viking on top of its inventory on the right, and no crafting - and puts it back as it
	/// was once the window closes
	/// </summary>
	/// <remarks>
	/// The viking's card is a copy of the player's, showing its name, with only its skills button left, which shows the
	/// viking's skills in the game's skills dialog. Its armor and weight show in tabs on the left of its inventory, the
	/// way the player's show on the right of theirs (see PlaceTabs), with a health tab added for each of them, marked
	/// with a heart (see PlaceHealth). The panels are placed by where they stand within
	/// the window, measured as they are, so it works whatever the game's own layout
	/// </remarks>
	internal static class VikingInventoryLayout
	{
		/// <summary>
		/// The space between a card and the inventory under it, in the window's units, when the space between the
		/// player's card and the crafting panel can't be measured
		/// </summary>
		private const float Gap = 8f;

		/// <summary>
		/// The panels moved and where they stood, where each goes within the window, the window's root they're placed
		/// in, the viking's card, the player's inventory and the viking's, the viking's tabs beside its inventory with
		/// the player's tabs they mirror, the viking's armor in its tab, and whether the crafting panel was showing
		/// </summary>
		private static readonly Dictionary<Transform, Vector3> moved = new Dictionary<Transform, Vector3>();
		private static readonly List<KeyValuePair<RectTransform, Vector2>> places = new List<KeyValuePair<RectTransform, Vector2>>();
		private static Transform root;
		private static GameObject card;
		private static RectTransform skillsButton;
		private static RectTransform player;
		private static RectTransform container;
		private static readonly List<KeyValuePair<Transform, Transform>> tabs = new List<KeyValuePair<Transform, Transform>>();
		private static TMP_Text vikingArmor;

		/// <summary>
		/// The title of the viking's inventory, hidden while its card above shows its name, or null
		/// </summary>
		private static GameObject hiddenTitle;

		/// <summary>
		/// The viking's weight in its armor tab when the player's armor and weight are shown together, and the chest's
		/// weight tab hidden in its place, or null
		/// </summary>
		private static TMP_Text vikingWeight;
		private static GameObject hiddenWeight;
		private static bool craftingShown;

		/// <summary>
		/// The player's health tab added to their inventory, and its text and the viking's (see PlaceHealth), or null
		/// </summary>
		private static GameObject playerHealthTab;
		private static TMP_Text playerHealth;
		private static TMP_Text vikingHealth;

		/// <summary>
		/// The heart the game shows as a tamed animal is petted, which marks the health tabs, the effect it comes from,
		/// and the sprite made of it, made the first time it's wanted
		/// </summary>
		private const string HeartEffect = "fx_boar_pet";
		private static Sprite heart;

		/// <summary>
		/// The heart's color - a bright, full pink, since the particles' own color is only as bright as the light they're
		/// shown in
		/// </summary>
		private static readonly Color HeartColor = new Color(1f, 0.3f, 0.45f, 1f);

		/// <summary>
		/// How opaque a pixel of the heart's texture has to be to be part of the heart, which is drawn fully opaque, and
		/// anything less left out
		/// </summary>
		private const float HeartAlphaCut = 0.1f;

		/// <summary>
		/// The layout elements of the panels left out of the window's own layout, whether each was added for it, and
		/// whether it was left out before
		/// </summary>
		private struct LeftOut
		{
			public LayoutElement Element;
			public bool Added;
			public bool Ignored;
		}

		private static readonly List<LeftOut> leftOut = new List<LeftOut>();

		private static readonly Vector3[] corners = new Vector3[4];

		/// <summary>
		/// Lays the window out for a viking's inventory, once
		/// </summary>
		/// <param name="gui">The game's inventory window</param>
		/// <param name="viking">The viking</param>
		/// <param name="name">Its name, for its card</param>
		public static void Apply(InventoryGui gui, Humanoid viking, string name)
		{
			if (card != null || gui.m_info == null || gui.m_player == null || gui.m_container == null)
			{
				return;
			}
			root = gui.m_inventoryRoot;
			// Where the player's inventory, card and crafting panel stand in the game's own layout, before anything moves
			Rect info = Bounds(root, gui.m_info);
			Rect player = Bounds(root, gui.m_player);
			Rect crafting = Bounds(root, gui.m_crafting);
			PlanPlaces(info, player, crafting, out Vector2 infoAt, out Vector2 playerAt, out Vector2 cardAt, out Vector2 containerAt);
			craftingShown = gui.m_crafting.gameObject.activeSelf;
			gui.m_crafting.gameObject.SetActive(false);
			foreach (RectTransform panel in new[] { gui.m_info, gui.m_player, gui.m_container })
			{
				moved[panel] = panel.localPosition;
			}
			card = Object.Instantiate(gui.m_info.gameObject, gui.m_info.parent);
			card.name = "VikingCard";
			SetUpCard(gui, viking, name);
			RectTransform cardRect = card.transform as RectTransform;
			// The window lays out its panels itself, which would put them back, so the ones placed here are left out of it
			foreach (RectTransform panel in new[] { gui.m_info, gui.m_player, gui.m_container, cardRect, skillsButton })
			{
				if (panel != null)
				{
					LeaveOutOfLayout(panel);
				}
			}
			// The player's card goes on top of their inventory on the left, and the viking's card where the player's
			// stood, on top of the viking's inventory
			places.Clear();
			places.Add(new KeyValuePair<RectTransform, Vector2>(gui.m_info, infoAt));
			places.Add(new KeyValuePair<RectTransform, Vector2>(gui.m_player, playerAt));
			places.Add(new KeyValuePair<RectTransform, Vector2>(cardRect, cardAt));
			places.Add(new KeyValuePair<RectTransform, Vector2>(gui.m_container, containerAt));
			card.AddComponent<VikingInventoryLayoutHolder>();
			card.AddComponent<VikingInventoryLayoutEarlyHolder>();
			Hold();
			PlaceTabs(gui);
			VikingInventoryStatus.Show(gui.m_container, gui.m_player);
			// The card above the viking's inventory shows its name, so the inventory's own title is left out
			if (gui.m_containerName != null && gui.m_containerName.gameObject.activeSelf)
			{
				hiddenTitle = gui.m_containerName.gameObject;
				hiddenTitle.SetActive(false);
			}
			Update(viking);
		}

		/// <summary>
		/// Works out where the panels' top left corners go within the window - the player's card on top of their
		/// inventory, where their inventory stood, and the viking's card on top of the viking's inventory, where the
		/// player's card stood, the cards at the top of the two and each inventory as far below its card as the player's
		/// card stands above the crafting panel, or Gap when that can't be measured
		/// </summary>
		/// <param name="info">The player's card, in the window's root's space</param>
		/// <param name="player">The player's inventory</param>
		/// <param name="crafting">The crafting panel</param>
		/// <param name="infoAt">Set to where the player's card goes</param>
		/// <param name="playerAt">Set to where the player's inventory goes</param>
		/// <param name="cardAt">Set to where the viking's card goes</param>
		/// <param name="containerAt">Set to where the viking's inventory goes</param>
		internal static void PlanPlaces(Rect info, Rect player, Rect crafting, out Vector2 infoAt, out Vector2 playerAt, out Vector2 cardAt, out Vector2 containerAt)
		{
			float top = Mathf.Max(info.yMax, player.yMax);
			float gap = info.yMin - crafting.yMax;
			if (gap <= 0f || gap > info.height)
			{
				gap = Gap;
			}
			float below = top - info.height - gap;
			infoAt = new Vector2(player.xMin, top);
			playerAt = new Vector2(player.xMin, below);
			cardAt = new Vector2(info.xMin, top);
			containerAt = new Vector2(info.xMin, below);
		}

		/// <summary>
		/// Puts the viking's armor and weight in tabs on the left of its inventory, mirroring the player's on the right of
		/// theirs - copies of the player's tabs, standing behind the viking's inventory the way the player's stand behind
		/// theirs, with the chest's own weight tab hidden while they show (see MirrorTabs)
		/// </summary>
		/// <param name="gui">The game's inventory window</param>
		private static void PlaceTabs(InventoryGui gui)
		{
			player = gui.m_player;
			container = gui.m_container;
			RectTransform playerArmor = PanelChild(gui.m_player, gui.m_armor);
			RectTransform playerWeight = PanelChild(gui.m_player, gui.m_weight);
			if (playerArmor != null)
			{
				GameObject copy = CopyTab(playerArmor, "VikingArmor");
				vikingArmor = Twin(playerArmor, copy.transform, gui.m_armor);
				if (playerWeight == playerArmor)
				{
					vikingWeight = Twin(playerArmor, copy.transform, gui.m_weight);
				}
			}
			if (playerWeight != null && playerWeight != playerArmor)
			{
				GameObject copy = CopyTab(playerWeight, "VikingWeight");
				vikingWeight = Twin(playerWeight, copy.transform, gui.m_weight);
			}
			RectTransform chestWeight = PanelChild(gui.m_container, gui.m_containerWeight);
			if (vikingWeight != null && chestWeight != null && chestWeight.gameObject.activeSelf)
			{
				hiddenWeight = chestWeight.gameObject;
				hiddenWeight.SetActive(false);
			}
			PlaceHealth(gui, playerArmor, playerWeight);
			MirrorTabs();
		}

		/// <summary>
		/// Adds a health tab to the player's inventory, marked with the heart the game shows as a tamed animal is petted -
		/// a copy of their weight tab, between their armor and weight tabs when there's room, or else under the lower of
		/// them - and a copy of it on the left of the viking's inventory for the viking's health, the way its other tabs
		/// mirror the player's
		/// </summary>
		/// <param name="gui">The game's inventory window</param>
		/// <param name="playerArmor">The player's armor tab, or null</param>
		/// <param name="playerWeight">The player's weight tab, or null</param>
		private static void PlaceHealth(InventoryGui gui, RectTransform playerArmor, RectTransform playerWeight)
		{
			RectTransform template = (playerWeight ?? playerArmor);
			TMP_Text templateText = ((playerWeight != null) ? gui.m_weight : gui.m_armor);
			if (template == null)
			{
				return;
			}
			playerHealthTab = Object.Instantiate(template.gameObject, template.parent);
			playerHealthTab.name = "PlayerHealth";
			// Beside the tab it's copied from, so the inventory's background, which comes after the tabs, covers its
			// inner edge the same way
			playerHealthTab.transform.SetSiblingIndex(template.GetSiblingIndex() + 1);
			RectTransform tab = playerHealthTab.transform as RectTransform;
			LeaveOutOfLayout(tab);
			foreach (Graphic graphic in playerHealthTab.GetComponentsInChildren<Graphic>(true))
			{
				graphic.raycastTarget = false;
			}
			playerHealth = Twin(template, playerHealthTab.transform, templateText);
			// A combined armor and weight tab keeps only the text it's copied for
			foreach (TMP_Text text in playerHealthTab.GetComponentsInChildren<TMP_Text>(true))
			{
				if (text != playerHealth)
				{
					text.gameObject.SetActive(false);
				}
			}
			MarkWithHeart(playerHealthTab.transform);
			float height = tab.rect.height * tab.localScale.y;
			if (playerArmor != null && playerWeight != null && playerArmor != playerWeight)
			{
				Vector3 position = template.localPosition;
				position.y = HealthTabY(playerArmor.localPosition.y, playerArmor.rect.height * playerArmor.localScale.y, playerWeight.localPosition.y, playerWeight.rect.height * playerWeight.localScale.y, height);
				tab.localPosition = position;
			}
			else
			{
				tab.localPosition = template.localPosition - new Vector3(0f, height + Gap, 0f);
			}
			GameObject copy = CopyTab(tab, "VikingHealth");
			vikingHealth = Twin(tab, copy.transform, playerHealth);
		}

		/// <summary>
		/// Works out how high the health tab's middle stands beside separate armor and weight tabs - halfway between
		/// them when the room between their edges fits it, or else as far under the lower of them as the two stand
		/// apart
		/// </summary>
		/// <param name="armorY">The middle of the armor tab</param>
		/// <param name="armorHeight">The armor tab's height</param>
		/// <param name="weightY">The middle of the weight tab</param>
		/// <param name="weightHeight">The weight tab's height</param>
		/// <param name="height">The health tab's height</param>
		/// <returns>The health tab's middle</returns>
		internal static float HealthTabY(float armorY, float armorHeight, float weightY, float weightHeight, float height)
		{
			float upper = Mathf.Max(armorY, weightY);
			float lower = Mathf.Min(armorY, weightY);
			float room = upper - lower - (armorHeight + weightHeight) / 2f;
			return(((room >= height) ? (upper + lower) / 2f : lower - (upper - lower)));
		}

		/// <summary>
		/// Puts the heart in place of a copied tab's icon - the smallest picture on it, past its background - in the
		/// heart's own color, keeping its shape
		/// </summary>
		/// <param name="tab">The tab</param>
		private static void MarkWithHeart(Transform tab)
		{
			Sprite sprite = Heart();
			if (sprite == null)
			{
				return;
			}
			Image icon = null;
			float smallest = float.MaxValue;
			Image[] images = tab.GetComponentsInChildren<Image>(true);
			if (images.Length < 2)
			{
				return;
			}
			foreach (Image image in images)
			{
				Rect rect = image.rectTransform.rect;
				float area = Mathf.Abs(rect.width * rect.height);
				if (image.sprite != null && area < smallest)
				{
					icon = image;
					smallest = area;
				}
			}
			if (icon == null)
			{
				return;
			}
			icon.sprite = sprite;
			icon.color = HeartColor;
			// The game's UI material, rather than whatever the copied icon was drawn with, so nothing dims the heart
			icon.material = null;
			icon.preserveAspect = true;
		}

		/// <summary>
		/// Makes a sprite of the heart the game shows as a tamed animal is petted, the first time it's wanted
		/// </summary>
		/// <returns>The sprite, or null when the effect isn't loaded</returns>
		private static Sprite Heart()
		{
			if (heart != null)
			{
				return(heart);
			}
			GameObject effect = GamePrefabs.Find(HeartEffect);
			ParticleSystemRenderer renderer = ((effect != null) ? effect.GetComponentInChildren<ParticleSystemRenderer>(true) : null);
			Texture2D texture = ((renderer != null && renderer.sharedMaterial != null) ? renderer.sharedMaterial.mainTexture as Texture2D : null);
			if (texture == null)
			{
				Plugin.Log.LogWarning("Couldn't find the heart of " + HeartEffect + " to mark the health tabs with");
				return(null);
			}
			Texture2D bright = Brighten(texture);
			heart = Sprite.Create(bright, new Rect(0f, 0f, bright.width, bright.height), new Vector2(0.5f, 0.5f));
			heart.name = "VikingVillages heart";
			return(heart);
		}

		/// <summary>
		/// Makes a copy of the heart's texture with its lightest pixel white and the rest lightened as much, and every
		/// pixel of the heart fully opaque - past HeartAlphaCut - keeping its shading and its pixels sharp. The particles
		/// light it up and blend it in the world, which the inventory doesn't, so as it is it shows dark and
		/// see-through there. The texture is copied by drawing it, since the game's can't be read
		/// </summary>
		/// <param name="texture">The heart's texture</param>
		/// <returns>The copy</returns>
		private static Texture2D Brighten(Texture2D texture)
		{
			RenderTexture target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
			RenderTexture previous = RenderTexture.active;
			Graphics.Blit(texture, target);
			RenderTexture.active = target;
			Texture2D copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
			copy.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
			RenderTexture.active = previous;
			RenderTexture.ReleaseTemporary(target);
			Color[] pixels = copy.GetPixels();
			BrightenPixels(pixels);
			copy.SetPixels(pixels);
			copy.filterMode = FilterMode.Point;
			copy.Apply();
			return(copy);
		}

		/// <summary>
		/// Lightens the heart's pixels so the lightest of those past HeartAlphaCut is white and the rest as much, and makes
		/// those fully opaque and the others fully clear - leaving their color as it is when there's nothing light to go by
		/// </summary>
		/// <param name="pixels">The pixels, changed in place</param>
		internal static void BrightenPixels(Color[] pixels)
		{
			float lightest = 0f;
			foreach (Color pixel in pixels)
			{
				if (pixel.a > HeartAlphaCut)
				{
					lightest = Mathf.Max(lightest, pixel.maxColorComponent);
				}
			}
			float scale = ((lightest > 0.01f) ? 1f / lightest : 1f);
			for (int i = 0; i < pixels.Length; i++)
			{
				float alpha = ((pixels[i].a > HeartAlphaCut) ? 1f : 0f);
				pixels[i] = new Color(Mathf.Clamp01(pixels[i].r * scale), Mathf.Clamp01(pixels[i].g * scale), Mathf.Clamp01(pixels[i].b * scale), alpha);
			}
		}

		/// <summary>
		/// Writes a player's or viking's health the way the game writes their weight - what they have and the most they
		/// can have
		/// </summary>
		/// <param name="text">The tab's text, or null</param>
		/// <param name="character">The player or viking</param>
		private static void ShowHealth(TMP_Text text, Character character)
		{
			if (text == null || character == null)
			{
				return;
			}
			text.text = Mathf.CeilToInt(character.GetHealth()) + "/" + Mathf.CeilToInt(character.GetMaxHealth());
		}

		/// <summary>
		/// Copies one of the player's tabs for the viking's inventory, behind the inventory and left out of the window's
		/// layout, with its background turned around to stick out the other way (see FlipBackground), and letting the
		/// pointer through
		/// </summary>
		/// <param name="original">The player's tab</param>
		/// <param name="name">The copy's name</param>
		/// <returns>The copy</returns>
		private static GameObject CopyTab(RectTransform original, string name)
		{
			GameObject copy = Object.Instantiate(original.gameObject, container.parent);
			copy.name = name;
			copy.transform.SetSiblingIndex(container.GetSiblingIndex());
			LeaveOutOfLayout(copy.transform as RectTransform);
			FlipBackground(copy.transform);
			// It only shows the viking's numbers, so it neither takes the pointer nor shows the player's tooltip
			foreach (Graphic graphic in copy.GetComponentsInChildren<Graphic>(true))
			{
				graphic.raycastTarget = false;
			}
			tabs.Add(new KeyValuePair<Transform, Transform>(original, copy.transform));
			return(copy);
		}

		/// <summary>
		/// Turns a tab's background around, so it sticks out to the left as the player's sticks out to the right - the
		/// biggest image on it, which its icon and text stand on, past any on the tab itself, which holds them all, like
		/// the image the weight tab has under its background
		/// </summary>
		/// <param name="tab">The tab</param>
		private static void FlipBackground(Transform tab)
		{
			Image background = null;
			float biggest = 0f;
			foreach (Image image in tab.GetComponentsInChildren<Image>(true))
			{
				// Turned around, the tab's own image would turn everything on it around with it, and the background
				// would be turned back with its icon and text
				if (image.transform == tab)
				{
					continue;
				}
				Rect rect = image.rectTransform.rect;
				float area = Mathf.Abs(rect.width * rect.height * image.rectTransform.lossyScale.x * image.rectTransform.lossyScale.y);
				if (area > biggest)
				{
					background = image;
					biggest = area;
				}
			}
			if (background == null)
			{
				return;
			}
			// Turned around its middle, it stands where it stood, and MirrorTabs moves it on from there. What stands on it,
			// like its text, is turned back, so it still reads the right way round
			FlipX(background.transform);
			foreach (Transform child in background.transform)
			{
				FlipX(child);
			}
		}

		/// <summary>
		/// Turns something around from left to right
		/// </summary>
		/// <param name="part">What's turned</param>
		private static void FlipX(Transform part)
		{
			Vector3 scale = part.localScale;
			scale.x = -scale.x;
			part.localScale = scale;
		}

		/// <summary>
		/// Puts each part of the viking's tabs where its twin in the player's tabs stands, mirrored from the player's
		/// inventory to the viking's - as far left of the viking's inventory's left edge as the twin stands right of the
		/// player's right edge, and as far down from its top - called whenever the panels are put in place
		/// </summary>
		private static void MirrorTabs()
		{
			if (player == null || container == null)
			{
				return;
			}
			Rect from = Bounds(root, player);
			Rect to = Bounds(root, container);
			foreach (KeyValuePair<Transform, Transform> tab in tabs)
			{
				if (tab.Key == null || tab.Value == null)
				{
					continue;
				}
				// Parents first, since moving one moves the parts in it, which are put in place after it
				RectTransform[] originals = tab.Key.GetComponentsInChildren<RectTransform>(true);
				RectTransform[] copies = tab.Value.GetComponentsInChildren<RectTransform>(true);
				for (int i = 0; i < originals.Length && i < copies.Length; i++)
				{
					Vector2 target = MirrorPoint(Bounds(root, originals[i]).center, from, to);
					Vector2 now = Bounds(root, copies[i]).center;
					copies[i].position += root.TransformVector(new Vector3(target.x - now.x, target.y - now.y, 0f));
				}
			}
		}

		/// <summary>
		/// Mirrors a point beside the player's inventory to beside the viking's - as far left of the viking's
		/// inventory's left edge as it stands right of the player's right edge, and as far down from its top
		/// </summary>
		/// <param name="center">The point, beside the player's inventory</param>
		/// <param name="from">The player's inventory</param>
		/// <param name="to">The viking's inventory</param>
		/// <returns>The point beside the viking's inventory</returns>
		internal static Vector2 MirrorPoint(Vector2 center, Rect from, Rect to)
		{
			return(new Vector2(to.xMin - (center.x - from.xMax), to.yMax - (from.yMax - center.y)));
		}

		/// <summary>
		/// Finds the part of a panel something in it belongs to - the panel's child it's in
		/// </summary>
		/// <param name="panel">The panel</param>
		/// <param name="part">Something in it, or null</param>
		/// <returns>The panel's child, or null when it's the panel itself or not in it</returns>
		internal static RectTransform PanelChild(Transform panel, Component part)
		{
			Transform step = ((part != null) ? part.transform : null);
			while (step != null && step.parent != panel)
			{
				step = step.parent;
			}
			return(step as RectTransform);
		}

		/// <summary>
		/// Puts the panels where they go, after anything else has moved them this frame - called as the frame ends while
		/// the layout is applied
		/// </summary>
		public static void Hold()
		{
			if (root == null)
			{
				return;
			}
			// Placed twice, since one panel may hold another and move it along
			for (int pass = 0; pass < 2; pass++)
			{
				foreach (KeyValuePair<RectTransform, Vector2> place in places)
				{
					if (place.Key != null)
					{
						MoveTo(root, place.Key, place.Value);
					}
				}
			}
			// The viking's card's only button stands in the middle of it, rather than where the first of the player's does
			if (card != null && skillsButton != null)
			{
				Rect bounds = Bounds(root, card.transform as RectTransform);
				Rect button = Bounds(root, skillsButton);
				skillsButton.position += root.TransformVector(new Vector3(bounds.center.x - button.center.x, 0f, 0f));
			}
			MirrorTabs();
		}

		/// <summary>
		/// Leaves a panel out of the layout group that arranges it, noting how it was so it can be put back
		/// </summary>
		/// <param name="panel">The panel</param>
		private static void LeaveOutOfLayout(RectTransform panel)
		{
			LayoutElement element = panel.GetComponent<LayoutElement>();
			bool added = (element == null);
			if (added)
			{
				element = panel.gameObject.AddComponent<LayoutElement>();
			}
			leftOut.Add(new LeftOut { Element = element, Added = added, Ignored = element.ignoreLayout });
			element.ignoreLayout = true;
		}

		/// <summary>
		/// Keeps the viking's armor and weight tabs and its status effects under its inventory up to date
		/// </summary>
		/// <param name="viking">The viking</param>
		public static void Update(Humanoid viking)
		{
			if (card == null || viking == null)
			{
				return;
			}
			if (vikingArmor != null)
			{
				vikingArmor.text = Mathf.CeilToInt(viking.GetBodyArmor()).ToString();
			}
			if (vikingWeight != null && viking is PlayerLikeVikingBody pc)
			{
				vikingWeight.text = pc.WeightText();
			}
			ShowHealth(playerHealth, Player.m_localPlayer);
			ShowHealth(vikingHealth, viking);
			VikingInventoryStatus.Update(viking);
		}

		/// <summary>
		/// Puts the window back as it was - the panels where they stood, the crafting panel showing again - and takes the
		/// viking's card away
		/// </summary>
		/// <param name="gui">The game's inventory window, or null when it's gone</param>
		public static void Restore(InventoryGui gui)
		{
			foreach (LeftOut entry in leftOut)
			{
				if (entry.Element == null)
				{
					continue;
				}
				if (entry.Added)
				{
					Object.Destroy(entry.Element);
				}
				else
				{
					entry.Element.ignoreLayout = entry.Ignored;
				}
			}
			leftOut.Clear();
			places.Clear();
			root = null;
			foreach (KeyValuePair<Transform, Vector3> entry in moved)
			{
				if (entry.Key != null)
				{
					entry.Key.localPosition = entry.Value;
				}
			}
			moved.Clear();
			if (gui != null && gui.m_crafting != null && craftingShown)
			{
				gui.m_crafting.gameObject.SetActive(true);
			}
			if (card != null)
			{
				Object.Destroy(card);
			}
			foreach (KeyValuePair<Transform, Transform> tab in tabs)
			{
				if (tab.Value != null)
				{
					Object.Destroy(tab.Value.gameObject);
				}
			}
			tabs.Clear();
			if (playerHealthTab != null)
			{
				Object.Destroy(playerHealthTab);
			}
			playerHealthTab = null;
			playerHealth = null;
			vikingHealth = null;
			VikingInventoryStatus.Hide();
			if (hiddenWeight != null)
			{
				hiddenWeight.SetActive(true);
			}
			if (hiddenTitle != null)
			{
				hiddenTitle.SetActive(true);
			}
			card = null;
			skillsButton = null;
			player = null;
			container = null;
			vikingArmor = null;
			vikingWeight = null;
			hiddenWeight = null;
			hiddenTitle = null;
		}

		/// <summary>
		/// Makes the viking's card out of the copy of the player's - its name in place of theirs, no buttons but skills,
		/// which shows the viking's skills, and no PvP toggle
		/// </summary>
		/// <param name="gui">The game's inventory window</param>
		/// <param name="viking">The viking</param>
		/// <param name="name">Its name</param>
		private static void SetUpCard(InventoryGui gui, Humanoid viking, string name)
		{
			// The copy would join the window's gamepad navigation as a second player card
			foreach (UIGroupHandler group in card.GetComponentsInChildren<UIGroupHandler>(true))
			{
				Object.Destroy(group);
			}
			TMP_Text nameText = Twin(gui.m_info, card.transform, gui.m_playerName);
			if (nameText == null)
			{
				nameText = card.GetComponentInChildren<TMP_Text>(true);
			}
			if (nameText != null)
			{
				nameText.text = name;
			}
			Button skills = null;
			foreach (Button button in card.GetComponentsInChildren<Button>(true))
			{
				if (!Calls(button, nameof(InventoryGui.OnOpenSkills)))
				{
					button.gameObject.SetActive(false);
					continue;
				}
				skills = button;
				skillsButton = button.transform as RectTransform;
				button.onClick = new Button.ButtonClickedEvent();
				button.onClick.AddListener(() => ShowSkills(gui, viking));
			}
			foreach (Toggle toggle in card.GetComponentsInChildren<Toggle>(true))
			{
				toggle.gameObject.SetActive(false);
			}
			// Text the copy brought along that's the player's, like the time of day, is left out
			foreach (TMP_Text text in card.GetComponentsInChildren<TMP_Text>(true))
			{
				bool kept = (text == nameText || (skills != null && text.transform.IsChildOf(skills.transform)));
				if (!kept && text.gameObject != card)
				{
					text.gameObject.SetActive(false);
				}
			}
			// The card only shows things but for its skills button, so it lets the pointer through to what's under it -
			// otherwise it would take the pointer from the viking's items wherever it reaches over them, and their
			// tooltips wouldn't show
			foreach (Graphic graphic in card.GetComponentsInChildren<Graphic>(true))
			{
				if (skills == null || !graphic.transform.IsChildOf(skills.transform))
				{
					graphic.raycastTarget = false;
				}
			}
			if (card.transform.parent == gui.m_container.parent)
			{
				card.transform.SetSiblingIndex(gui.m_container.GetSiblingIndex());
			}
		}

		/// <summary>
		/// Finds the text in a copy of something of the player's - their card or armor tab - standing where one does in
		/// the original
		/// </summary>
		/// <param name="original">The original</param>
		/// <param name="copy">The copy</param>
		/// <param name="text">The text in the original, or null</param>
		/// <returns>Its twin in the copy, or null when it isn't in the original</returns>
		private static TMP_Text Twin(Transform original, Transform copy, TMP_Text text)
		{
			if (text == null)
			{
				return(null);
			}
			List<string> path = new List<string>();
			Transform step = text.transform;
			while (step != null && step != original)
			{
				path.Insert(0, step.name);
				step = step.parent;
			}
			if (step == null)
			{
				return(null);
			}
			Transform twin = ((path.Count > 0) ? copy.Find(string.Join("/", path)) : copy);
			return((twin != null) ? twin.GetComponent<TMP_Text>() : null);
		}

		/// <summary>
		/// Checks whether a button calls a method of the inventory window when clicked
		/// </summary>
		/// <param name="button">The button</param>
		/// <param name="method">The method's name</param>
		/// <returns>True when it does</returns>
		private static bool Calls(Button button, string method)
		{
			for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
			{
				if (button.onClick.GetPersistentMethodName(i) == method)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Shows the viking's skills in the game's skills dialog, the way it shows the player's - each skill the game
		/// has, with the viking's level in it and how far it is toward the next, and its total
		/// </summary>
		/// <param name="gui">The game's inventory window</param>
		/// <param name="viking">The viking</param>
		private static void ShowSkills(InventoryGui gui, Humanoid viking)
		{
			SkillsDialog dialog = gui.m_skillsDialog;
			VikingCore core = ((viking != null) ? viking.GetComponent<VikingCore>() : null);
			Player player = Player.m_localPlayer;
			if (dialog == null || core == null || player == null)
			{
				return;
			}
			dialog.gameObject.SetActive(true);
			List<Skills.SkillDef> skills = player.GetSkills().m_skills;
			while (dialog.m_elements.Count < skills.Count)
			{
				dialog.m_elements.Add(Object.Instantiate(dialog.m_elementPrefab, Vector3.zero, Quaternion.identity, dialog.m_listRoot));
			}
			float total = 0f;
			for (int i = 0; i < skills.Count; i++)
			{
				Skills.SkillDef skill = skills[i];
				float level = Mathf.Max(0f, core.ExactSkillLevel(skill.m_skill));
				total += level;
				GameObject element = dialog.m_elements[i];
				element.SetActive(true);
				RectTransform rect = element.transform as RectTransform;
				rect.anchoredPosition = new Vector2(0f, -i * dialog.m_spacing);
				element.GetComponentInChildren<UITooltip>().Set("", skill.m_description, dialog.m_tooltipAnchor, new Vector2(0f, Mathf.Min(255f, rect.localPosition.y + 10f)));
				Utils.FindChild(element.transform, "icon").GetComponent<Image>().sprite = skill.m_icon;
				Utils.FindChild(element.transform, "name").GetComponent<TMP_Text>().text = Localization.instance.Localize("$skill_" + skill.m_skill.ToString().ToLower());
				Utils.FindChild(element.transform, "leveltext").GetComponent<TMP_Text>().text = Mathf.FloorToInt(level).ToString();
				Utils.FindChild(element.transform, "bonustext").gameObject.SetActive(false);
				Utils.FindChild(element.transform, "levelbar_total").GetComponent<GuiBar>().SetValue(level / 100f);
				Utils.FindChild(element.transform, "levelbar").GetComponent<GuiBar>().SetValue(level / 100f);
				Utils.FindChild(element.transform, "currentlevel").GetComponent<GuiBar>().SetValue(level - Mathf.Floor(level));
			}
			for (int i = skills.Count; i < dialog.m_elements.Count; i++)
			{
				dialog.m_elements[i].SetActive(false);
			}
			dialog.m_listRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(dialog.m_baseListSize, skills.Count * dialog.m_spacing));
			dialog.m_totalSkillText.text = "<color=orange>" + total.ToString("0") + "</color><color=white> / </color><color=orange>" + player.GetSkills().GetTotalSkillCap().ToString("0") + "</color>";
		}

		/// <summary>
		/// Measures where a panel stands within the window
		/// </summary>
		/// <param name="root">The window's root</param>
		/// <param name="panel">The panel</param>
		/// <returns>Its bounds, in the root's space</returns>
		private static Rect Bounds(Transform root, RectTransform panel)
		{
			panel.GetWorldCorners(corners);
			Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
			Vector2 max = new Vector2(float.MinValue, float.MinValue);
			foreach (Vector3 corner in corners)
			{
				Vector3 local = root.InverseTransformPoint(corner);
				min = Vector2.Min(min, local);
				max = Vector2.Max(max, local);
			}
			return(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
		}

		/// <summary>
		/// Moves a panel so its top left corner stands at a point within the window
		/// </summary>
		/// <param name="root">The window's root</param>
		/// <param name="panel">The panel</param>
		/// <param name="topLeft">Where its top left corner goes, in the root's space</param>
		private static void MoveTo(Transform root, RectTransform panel, Vector2 topLeft)
		{
			Rect bounds = Bounds(root, panel);
			Vector3 shift = new Vector3(topLeft.x - bounds.xMin, topLeft.y - bounds.yMax, 0f);
			panel.position += root.TransformVector(shift);
		}
	}

	/// <summary>
	/// Puts the inventory window's panels where the layout for a viking's inventory has them as each frame ends, after
	/// the window's own layout and animation have had their say (see VikingInventoryLayout.Hold)
	/// </summary>
	[DefaultExecutionOrder(10000)]
	internal class VikingInventoryLayoutHolder : MonoBehaviour
	{
		/// <summary>
		/// Puts the panels where they go
		/// </summary>
		private void LateUpdate()
		{
			VikingInventoryLayout.Hold();
		}
	}

	/// <summary>
	/// Puts the inventory window's panels where the layout for a viking's inventory has them as soon as the window's
	/// animation has moved them each frame, before anything else looks at them - an item's tooltip checks the pointer is
	/// still over its slot then, and would close as it opened with the slot moved away from it
	/// </summary>
	[DefaultExecutionOrder(-10000)]
	internal class VikingInventoryLayoutEarlyHolder : MonoBehaviour
	{
		/// <summary>
		/// Puts the panels where they go
		/// </summary>
		private void LateUpdate()
		{
			VikingInventoryLayout.Hold();
		}
	}
}
