using System.Collections.Generic;
using Kriona.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreatureMarker
{
	/// <summary>
	/// Colored arrows above marked creatures, pinned to the screen edge and pointing at them when they are off-screen,
	/// with an optional name label behind each arrow, and a red ! beside the arrow of a creature that leaves players alone
	/// until provoked and has been
	/// </summary>
	/// <remarks>
	/// The markers live in a canvas of their own under the enemy HUD root, so they hide along with the health bars,
	/// but sort above the HUD canvas so an arrow pinned to the screen edge isn't hidden behind the minimap
	/// </remarks>
	public class MarkerHud
	{
		/// <summary>
		/// Above the HUD canvas holding the minimap (400) and below the top left messages (500), the inventory and
		/// every other screen opened over the game
		/// </summary>
		private const int SortingOrder = 450;
		private const float HeadOffset = 0.4f;
		private const float EdgeMargin = 40f;
		private const float BobSpeed = 5f;
		private const float LabelGap = 2f;
		private const float LabelFontSize = 16f;
		private const float GameHudGap = 4f;

		/// <summary>
		/// The space left between markers stacked over each other on screen, in screen pixels
		/// </summary>
		private const float StackGap = 2f;

		/// <summary>
		/// How many meters higher than the player a target has to be for its arrow to be pinned to the top half of the
		/// screen, so uneven ground doesn't count as higher
		/// </summary>
		private const float HigherMargin = 1f;
		private static readonly Vector2 ArrowSize = new Vector2(24f, 32f);
		internal static readonly Color OutlineColor = ArrowSprite.OutlineColor;

		/// <summary>
		/// The red ! beside the arrow of a creature that's been provoked - its text, size, the box it's centered in and
		/// how far it stands from the arrow, in canvas units, and its color
		/// </summary>
		private const string AlarmText = "!";
		private const float AlarmFontSize = 26f;
		private static readonly Vector2 AlarmSize = new Vector2(12f, 30f);
		private const float AlarmGap = 2f;
		private static readonly Color AlarmColor = new Color(1f, 0.15f, 0.15f, 1f);

		/// <summary>
		/// One creature's, fish's or bird's arrow and name label
		/// </summary>
		/// <remarks>
		/// The label is a sibling of the arrow so it stays upright when the arrow rotates at the screen edge
		/// </remarks>
		private class Marker
		{
			public Image arrow;
			public TextMeshProUGUI label;

			/// <summary>
			/// The red ! beside the arrow, shown while the creature has been provoked
			/// </summary>
			public TextMeshProUGUI alarm;
			public Object source;

			/// <summary>
			/// The name token the name was looked up from, since the traders and pets seen through one portal share it as
			/// their source
			/// </summary>
			public string nameToken;

			/// <summary>
			/// The target's localized name, followed by the stack size for a stack of items, looked up when the marker is
			/// given a new target or the stack size changes
			/// </summary>
			public string name;

			/// <summary>
			/// The stack size in the name
			/// </summary>
			public int count;

			/// <summary>
			/// The name, level, star color and distance in the label's text, so the text is only rebuilt when one of
			/// them changes
			/// </summary>
			/// <remarks>
			/// The name is null when names are hidden, the level is 0 when levels are hidden and the distance is -1
			/// when distances are hidden, so a distance of -2 means the text hasn't been set
			/// </remarks>
			public string shownName;
			public int shownLevel;
			public bool shownFish;
			public Color shownColor;
			public int shownDistance = -2;

			/// <summary>
			/// The label's text measured in canvas units, when the text is set, since TMP lays the text out again each
			/// time it is measured
			/// </summary>
			public Vector2 labelSize;
		}

		private readonly List<Marker> markers = new List<Marker>();

		/// <summary>
		/// The mask cutting the markers seen through each portal to its opening, by portal
		/// </summary>
		private readonly Dictionary<Object, OpeningMask> masks = new Dictionary<Object, OpeningMask>();

		/// <summary>
		/// The masks shaped this frame, so the others can be hidden
		/// </summary>
		private readonly HashSet<OpeningMask> masksShaped = new HashSet<OpeningMask>();

		/// <summary>
		/// Reused for the portals whose masks are removed once the portal is gone
		/// </summary>
		private readonly List<Object> masksGone = new List<Object>();

		/// <summary>
		/// Reused for an opening's outline on screen
		/// </summary>
		private readonly List<Vector2> openingPoints = new List<Vector2>();
		private GameObject root;

		/// <summary>
		/// The markers' canvas under the enemy HUD root
		/// </summary>
		private RectTransform layer;
		private Sprite sprite;

		/// <summary>
		/// The star shown for each of a creature's stars, set up when the first label is created
		/// </summary>
		private readonly LevelStars stars = new LevelStars();

		/// <summary>
		/// Reused when measuring the game's own HUD above a creature
		/// </summary>
		private readonly List<Graphic> graphics = new List<Graphic>();
		private readonly Vector3[] corners = new Vector3[4];

		/// <summary>
		/// An on-screen marker to stack, with how far its target is from the player, squared, and its scale
		/// </summary>
		private struct Stacked
		{
			public Marker marker;
			public float distanceSquared;
			public float size;
		}

		/// <summary>
		/// Reused for the on-screen markers placed this frame, and the screen space each takes once stacked
		/// </summary>
		private readonly List<Stacked> stacking = new List<Stacked>();
		private readonly List<Rect> stacked = new List<Rect>();

		/// <summary>
		/// Shows an arrow for each marked creature, fish and bird when Show On Screen is on and hides the spare ones,
		/// showing none while the large map or the loading screen is shown since they would be drawn over it
		/// </summary>
		/// <param name="marked">The living marked creatures and the marked fish and birds within range</param>
		/// <param name="player">The local player</param>
		public void Update(List<Target> marked, Player player)
		{
			EnemyHud enemyHud = EnemyHud.instance;
			Camera camera = Utils.GetMainCamera();
			if (enemyHud == null || camera == null)
			{
				return;
			}
			if (root != enemyHud.m_hudRoot)
			{
				// The markers were destroyed along with the previous HUD root
				root = enemyHud.m_hudRoot;
				markers.Clear();
				masks.Clear();
				layer = CreateLayer();
			}
			int used = 0;
			bool largeMap = (Minimap.instance != null && Minimap.instance.m_mode == Minimap.MapMode.Large);
			if (CreatureConfig.ShowOnScreen && !largeMap && !IsLoadingScreenShown())
			{
				bool showName = CreatureConfig.ShowName;
				bool showDistance = CreatureConfig.ShowDistance;
				bool showLevel = CreatureConfig.ShowLevel;
				bool showLabel = (showName || showDistance || showLevel);
				float farDistanceSquared = CreatureConfig.FarDistance * CreatureConfig.FarDistance;
				Vector3 farScale = Vector3.one * CreatureConfig.FarScale;
				Vector3 playerPosition = player.transform.position;
				foreach (Target target in marked)
				{
					Marker marker = GetMarker(used, enemyHud);
					used++;
					Transform parent = layer;
					if (target.throughPortal)
					{
						OpeningMask mask = GetOpeningMask(target.source, camera);
						if (mask == null)
						{
							SetActive(marker.arrow.gameObject, false);
							SetActive(marker.label.gameObject, false);
							SetActive(marker.alarm.gameObject, false);
							continue;
						}
						parent = mask.transform;
					}
					else if (PortalNpcs.IsHiddenByView(target.top, camera.transform.position, target.source))
					{
						SetActive(marker.arrow.gameObject, false);
						SetActive(marker.label.gameObject, false);
						SetActive(marker.alarm.gameObject, false);
						continue;
					}
					if (marker.arrow.transform.parent != parent)
					{
						marker.arrow.transform.SetParent(parent, false);
						marker.label.transform.SetParent(parent, false);
						marker.alarm.transform.SetParent(parent, false);
					}
					float distanceSquared = Vector3.SqrMagnitude(target.position - playerPosition);
					Vector3 scale = ((distanceSquared > farDistanceSquared) ? farScale : Vector3.one);
					// The label and the ! are scaled along with the arrow, so placing them can go by the arrow's scale alone
					marker.arrow.rectTransform.localScale = scale;
					marker.label.rectTransform.localScale = scale;
					marker.alarm.rectTransform.localScale = scale;
					SetActive(marker.alarm.gameObject, target.provoked);
					marker.arrow.color = target.color;
					// While the game's HUD shows the name and level above an on-screen creature, the arrow sits above it and
					// the label leaves them out - an arrow pinned to the screen edge is away from the HUD, so it keeps them
					GameObject chatText = (target.chatText ? target.source as GameObject : null);
					float gameHudHeight = (target.chatText ? GetHeightAbove(chatText) : GetGameHudHeight(enemyHud, target.source));
					Rect? dialog = GetDialogRect(target.source);
					bool higher = (target.position.y > playerPosition.y + HigherMargin);
					Vector2 pointing;
					bool onScreen;
					if (gameHudHeight >= 0f)
					{
						pointing = Place(marker.arrow.rectTransform, camera, target.top, gameHudHeight + GameHudGap, dialog, scale.x, higher, out onScreen);
					}
					else
					{
						pointing = Place(marker.arrow.rectTransform, camera, target.top + Vector3.up * HeadOffset, 0f, dialog, scale.x, higher, out onScreen);
					}
					// A ping's or shout's text is moved behind the arrow when it's pinned to the screen edge, so it always
					// shows the sender
					float textBehind = 0f;
					if (target.chatText && !onScreen)
					{
						textBehind = PlaceChatText(marker, chatText, pointing);
					}
					bool underGameHud = ((gameHudHeight >= 0f && onScreen) || target.chatText);
					bool showText = (underGameHud ? showDistance : showLabel);
					SetActive(marker.label.gameObject, showText);
					if (showText)
					{
						if (marker.source != target.source || marker.nameToken != target.nameToken || marker.count != target.count)
						{
							marker.source = target.source;
							marker.nameToken = target.nameToken;
							marker.count = target.count;
							marker.name = Localization.instance.Localize(target.nameToken);
							if (target.count > 1)
							{
								// The game shows a stack's size the same way when it lies on the ground
								marker.name += " x" + target.count;
							}
						}
						int distance = (showDistance ? Mathf.RoundToInt(Mathf.Sqrt(distanceSquared)) : -1);
						// A speech bubble that already names the creature, like a Viking Villages Viking's, leaves the name out too
						bool named = (dialog.HasValue && BubbleNames(target.source, marker.name));
						string name = ((showName && !underGameHud && !named) ? marker.name : null);
						int level = ((showLevel && !underGameHud) ? target.level : 0);
						SetLabelText(marker, name, level, target.fish, target.color, distance);
						marker.label.color = target.color;
						PlaceLabel(marker, pointing, textBehind);
					}
					// Markers seen through a portal stay where they are, inside its opening
					if (onScreen && !target.throughPortal)
					{
						stacking.Add(new Stacked { marker = marker, distanceSquared = distanceSquared, size = scale.x });
					}
				}
				Stack();
				for (int i = 0; i < used; i++)
				{
					if (markers[i].alarm.gameObject.activeSelf)
					{
						PlaceAlarm(markers[i]);
					}
				}
			}
			stacking.Clear();
			for (int i = used; i < markers.Count; i++)
			{
				markers[i].source = null;
				markers[i].shownName = null;
				markers[i].shownDistance = -2;
				SetActive(markers[i].arrow.gameObject, false);
				SetActive(markers[i].label.gameObject, false);
				SetActive(markers[i].alarm.gameObject, false);
			}
			HideUnusedMasks();
		}

		/// <summary>
		/// The mask cutting the markers seen through a portal to its opening, shaped to where the opening is on screen
		/// the first time it is asked for in a frame
		/// </summary>
		/// <param name="portal">The portal</param>
		/// <param name="camera">The main camera</param>
		/// <returns>The mask, or null when the opening can't be placed on screen</returns>
		private OpeningMask GetOpeningMask(Object portal, Camera camera)
		{
			if (masks.TryGetValue(portal, out OpeningMask mask) && masksShaped.Contains(mask))
			{
				return(mask);
			}
			if (!PortalNpcs.GetOpeningOnScreen(portal as Transform, camera, openingPoints, out Vector2 center))
			{
				return(null);
			}
			if (mask == null)
			{
				mask = OpeningMask.Create(layer);
				masks[portal] = mask;
			}
			SetActive(mask.gameObject, true);
			mask.SetShape(openingPoints, center);
			masksShaped.Add(mask);
			return(mask);
		}

		/// <summary>
		/// Hides the masks of portals with no markers this frame and removes those of portals that are gone
		/// </summary>
		private void HideUnusedMasks()
		{
			foreach (KeyValuePair<Object, OpeningMask> entry in masks)
			{
				if (entry.Key == null)
				{
					masksGone.Add(entry.Key);
				}
				else if (!masksShaped.Contains(entry.Value))
				{
					SetActive(entry.Value.gameObject, false);
				}
			}
			foreach (Object portal in masksGone)
			{
				OpeningMask mask = masks[portal];
				// Markers left under the mask are out of use, and go back to the canvas so they aren't destroyed with it
				foreach (Marker marker in markers)
				{
					if (marker.arrow.transform.parent == mask.transform)
					{
						marker.arrow.transform.SetParent(layer, false);
						marker.label.transform.SetParent(layer, false);
						marker.alarm.transform.SetParent(layer, false);
					}
				}
				Object.Destroy(mask.gameObject);
				masks.Remove(portal);
			}
			masksGone.Clear();
			masksShaped.Clear();
		}

		/// <summary>
		/// Whether the game's black loading screen is shown, as it is while teleporting, sleeping or dead
		/// </summary>
		/// <remarks>
		/// The loading screen is part of the HUD canvas, which the markers' canvas sorts above
		/// </remarks>
		/// <returns>True when the loading screen is shown, even while it is fading in or out</returns>
		private static bool IsLoadingScreenShown()
		{
			Hud hud = Hud.instance;
			return(hud != null && hud.m_loadingScreen != null && hud.m_loadingScreen.gameObject.activeSelf);
		}

		/// <summary>
		/// Removes the markers' canvas along with the markers, the generated sprite and the star sprite asset
		/// </summary>
		public void Destroy()
		{
			stars.Destroy();
			if (layer != null)
			{
				Object.Destroy(layer.gameObject);
			}
			markers.Clear();
			masks.Clear();
			if (sprite != null)
			{
				Object.Destroy(sprite.texture);
				Object.Destroy(sprite);
			}
		}

		/// <summary>
		/// Puts the arrow's tip just above the target, or on the screen edge in the target's direction when it is off-screen
		/// </summary>
		/// <param name="arrow">The arrow to move</param>
		/// <param name="camera">The main camera</param>
		/// <param name="target">World point the arrow points at</param>
		/// <param name="lift">How many screen pixels above the target an on-screen arrow's tip sits</param>
		/// <param name="dialog">The screen rectangle of the target's speech bubble, which an on-screen arrow sits above
		/// when the bubble would cover it, or null when the target isn't talking</param>
		/// <param name="size">The marker's scale, which shrinks the bob along with the arrow</param>
		/// <param name="higher">Whether the target is higher than the player, which lets an off-screen arrow be pinned to
		/// the top half of the screen - otherwise it is mirrored into the bottom half, on the same side</param>
		/// <param name="onScreen">Set to whether the target is on screen, rather than the arrow pinned to the edge</param>
		/// <returns>The screen direction the arrow points in, as a unit vector</returns>
		private static Vector2 Place(RectTransform arrow, Camera camera, Vector3 target, float lift, Rect? dialog, float size, bool higher, out bool onScreen)
		{
			Vector3 screen = camera.WorldToScreenPointScaled(target);
			Vector2 screenSize = new Vector2(Screen.width, Screen.height);
			onScreen = ScreenEdge.OnScreen(screen, screenSize);
			if (onScreen)
			{
				float tip = screen.y + lift;
				if (dialog.HasValue)
				{
					Rect bubble = dialog.Value;
					float halfArrow = ArrowSize.x * 0.5f * arrow.lossyScale.x;
					if (bubble.yMax > tip && screen.x + halfArrow > bubble.xMin && screen.x - halfArrow < bubble.xMax)
					{
						tip = bubble.yMax + GameHudGap;
					}
				}
				arrow.position = new Vector3(screen.x, tip + Bob(size), 0f);
				arrow.localRotation = Quaternion.identity;
				return(Vector2.down);
			}
			Vector2 edge = ScreenEdge.Pin(screen, screenSize, EdgeMargin, higher, out Vector2 direction);
			arrow.position = new Vector3(edge.x, edge.y, 0f);
			arrow.localRotation = Quaternion.Euler(0f, 0f, ScreenEdge.Angle(direction));
			return(direction);
		}

		/// <summary>
		/// How far an on-screen arrow is bobbed up right now
		/// </summary>
		/// <param name="size">The marker's scale, which shrinks the bob along with the arrow</param>
		/// <returns>The height, in screen pixels</returns>
		private static float Bob(float size)
		{
			return(Mathf.Abs(Mathf.Sin(Time.time * BobSpeed)) * CreatureConfig.BobHeight * size);
		}

		/// <summary>
		/// Stacks the on-screen markers placed this frame that would overlap - the nearest target's keeps its place, and
		/// each farther one is moved up, arrow and label together, until it's clear of those below it
		/// </summary>
		/// <remarks>
		/// The space each takes is measured without the bob, which every arrow shares, so markers that only touch at the
		/// top of the bob don't stack and unstack as they bob
		/// </remarks>
		private void Stack()
		{
			if (stacking.Count < 2)
			{
				return;
			}
			stacking.Sort((Stacked a, Stacked b) => a.distanceSquared.CompareTo(b.distanceSquared));
			stacked.Clear();
			foreach (Stacked entry in stacking)
			{
				Rect space = Space(entry.marker);
				space.y -= Bob(entry.size);
				float shift = 0f;
				bool moved = true;
				for (int tries = 0; moved && tries <= stacked.Count; tries++)
				{
					moved = false;
					foreach (Rect below in stacked)
					{
						Rect shifted = space;
						shifted.y += shift;
						if (shifted.Overlaps(below))
						{
							shift = below.yMax + StackGap - space.yMin;
							moved = true;
						}
					}
				}
				if (shift > 0f)
				{
					Vector3 up = new Vector3(0f, shift, 0f);
					entry.marker.arrow.rectTransform.position += up;
					entry.marker.label.rectTransform.position += up;
					space.y += shift;
				}
				stacked.Add(space);
			}
		}

		/// <summary>
		/// Measures the screen space a marker takes - its arrow, and its label when it's shown
		/// </summary>
		/// <param name="marker">The marker, placed on screen</param>
		/// <returns>The space, in screen pixels</returns>
		private static Rect Space(Marker marker)
		{
			RectTransform arrow = marker.arrow.rectTransform;
			float scale = arrow.lossyScale.x;
			Vector3 tip = arrow.position;
			Rect space = new Rect(tip.x - ArrowSize.x * 0.5f * scale, tip.y, ArrowSize.x * scale, ArrowSize.y * scale);
			if (marker.label.gameObject.activeSelf)
			{
				Vector2 size = marker.labelSize * scale;
				Vector3 center = marker.label.rectTransform.position;
				Rect label = new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
				space = Rect.MinMaxRect(Mathf.Min(space.xMin, label.xMin), Mathf.Min(space.yMin, label.yMin), Mathf.Max(space.xMax, label.xMax), Mathf.Max(space.yMax, label.yMax));
			}
			return(space);
		}

		/// <summary>
		/// How far the top of the game's own name, stars and health bar sits above the creature's top point, in screen
		/// pixels, while the game shows them above the creature
		/// </summary>
		/// <remarks>
		/// The game places its HUD at the creature's top point, so measuring from the HUD's own position gives a height
		/// that holds whether the game has moved it yet this frame or not. Bosses are left out since their bar sits at
		/// the top of the screen
		/// </remarks>
		/// <param name="enemyHud">The enemy HUD</param>
		/// <param name="source">The marked Character, Trader, Petable, Fish, RandomFlyingBird, Leviathan, Odin, Raven, Valkyrie, ItemDrop, TeleportWorld or TombStone</param>
		/// <returns>The height, or -1 when the game isn't showing a name above the target</returns>
		private float GetGameHudHeight(EnemyHud enemyHud, Object source)
		{
			Character character = source as Character;
			if (character == null || character.IsBoss() || !enemyHud.m_huds.TryGetValue(character, out EnemyHud.HudData data))
			{
				return(-1f);
			}
			if (data.m_isMount || data.m_gui == null || !data.m_gui.activeSelf || data.m_name == null || !data.m_name.gameObject.activeInHierarchy)
			{
				return(-1f);
			}
			return(GetHeightAbove(data.m_gui));
		}

		/// <summary>
		/// How far the top of a piece of the game's screen space UI, like a creature's health bar or a ping's text, sits
		/// above the UI object's own position, which the game puts at the world point it shows the UI above
		/// </summary>
		/// <param name="gui">The UI object, or null</param>
		/// <returns>The height in screen pixels, or -1 when the UI object is missing or hidden</returns>
		private float GetHeightAbove(GameObject gui)
		{
			if (gui == null || !gui.activeInHierarchy)
			{
				return(-1f);
			}
			float bottom = gui.transform.position.y;
			float top = bottom;
			if (GetGraphicsBounds(gui, out Rect bounds))
			{
				top = Mathf.Max(top, bounds.yMax);
			}
			return(top - bottom);
		}

		/// <summary>
		/// The screen rectangle of the speech bubble the game shows over a target that is talking, like a trader
		/// greeting the player
		/// </summary>
		/// <param name="source">The marked Character, Trader, Petable, Fish, RandomFlyingBird, Leviathan, Odin, Raven, Valkyrie, ItemDrop, TeleportWorld or TombStone</param>
		/// <returns>The bubble's rectangle in screen pixels, or null when the target has no bubble showing</returns>
		private Rect? GetDialogRect(Object source)
		{
			Chat chat = Chat.instance;
			Component component = source as Component;
			if (chat == null || component == null || chat.m_npcTexts.Count == 0)
			{
				return(null);
			}
			Chat.NpcText npcText = chat.FindNpcText(component.gameObject);
			if (npcText == null || npcText.m_gui == null || !npcText.m_gui.activeInHierarchy || !npcText.IsVisible())
			{
				return(null);
			}
			if (!GetGraphicsBounds(npcText.m_gui, out Rect bounds))
			{
				return(null);
			}
			return(bounds);
		}

		/// <summary>
		/// Checks whether a creature's speech bubble already names it - in its topic, as a trader's does, or on the
		/// first line of its text, as a Viking Villages Viking's does while its health bar doesn't show
		/// </summary>
		/// <param name="source">The creature</param>
		/// <param name="name">The name its label would show</param>
		/// <returns>True when the bubble names it</returns>
		private static bool BubbleNames(Object source, string name)
		{
			Chat chat = Chat.instance;
			Component component = source as Component;
			if (chat == null || component == null || string.IsNullOrEmpty(name))
			{
				return(false);
			}
			Chat.NpcText npcText = chat.FindNpcText(component.gameObject);
			if (npcText == null)
			{
				return(false);
			}
			bool topic = (npcText.m_topicField != null && npcText.m_topicField.gameObject.activeInHierarchy && NamesSpeaker(npcText.m_topicField.text, name));
			return(topic || (npcText.m_textField != null && NamesSpeaker(npcText.m_textField.text, name)));
		}

		/// <summary>
		/// Checks whether a bubble's text starts with a speaker's name on a line of its own, its rich text tags left out
		/// </summary>
		/// <param name="text">The text, or null</param>
		/// <param name="name">The name</param>
		/// <returns>True when its first line is the name</returns>
		internal static bool NamesSpeaker(string text, string name)
		{
			if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(name))
			{
				return(false);
			}
			string plain = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", "");
			return(FirstLine(plain, out string _).Trim() == FirstLine(name, out string _).Trim());
		}

		/// <summary>
		/// Measures the visible graphics under a screen space UI object, going by the letters themselves for text
		/// </summary>
		/// <param name="root">The UI object to measure</param>
		/// <param name="bounds">Set to the rectangle around the graphics in screen pixels</param>
		/// <returns>False when nothing under the object is visible</returns>
		private bool GetGraphicsBounds(GameObject root, out Rect bounds)
		{
			Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
			Vector2 max = new Vector2(float.MinValue, float.MinValue);
			bool found = false;
			root.GetComponentsInChildren(false, graphics);
			foreach (Graphic graphic in graphics)
			{
				if (graphic.color.a <= 0f)
				{
					continue;
				}
				if (graphic is TMP_Text text)
				{
					// The text's rect can be taller than the letters, so measure the letters themselves
					if (string.IsNullOrEmpty(text.text))
					{
						continue;
					}
					Bounds letters = text.textBounds;
					Vector2 lower = text.rectTransform.TransformPoint(letters.min);
					Vector2 upper = text.rectTransform.TransformPoint(letters.max);
					min = Vector2.Min(min, lower);
					max = Vector2.Max(max, upper);
				}
				else
				{
					graphic.rectTransform.GetWorldCorners(corners);
					min = Vector2.Min(min, corners[0]);
					max = Vector2.Max(max, corners[2]);
				}
				found = true;
			}
			graphics.Clear();
			bounds = (found ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : default(Rect));
			return(found);
		}

		/// <summary>
		/// Sets the label to the name, then the level, then the distance in brackets when there is text before it -
		/// like "Greydwarf ⭐⭐ (5m)", "Perch[3]" or "5m" - rebuilding the text only when one of them has changed. A name
		/// of more than one line, like a creature's from another mod with more under its name, gets the level and the
		/// distance after its first line
		/// </summary>
		/// <param name="marker">The marker whose label to set</param>
		/// <param name="name">The name to show, or null to leave it out</param>
		/// <param name="level">The level to show, or 0 to leave it out</param>
		/// <param name="fish">Whether the level is a fish's, shown as a number instead of stars</param>
		/// <param name="color">The marker color, which fills the stars</param>
		/// <param name="distance">The distance in meters to show, or -1 to leave it out</param>
		private void SetLabelText(Marker marker, string name, int level, bool fish, Color color, int distance)
		{
			if (name == marker.shownName && level == marker.shownLevel && fish == marker.shownFish && color == marker.shownColor && distance == marker.shownDistance)
			{
				return;
			}
			marker.shownName = name;
			marker.shownLevel = level;
			marker.shownFish = fish;
			marker.shownColor = color;
			marker.shownDistance = distance;
			string text = FirstLine(name ?? "", out string rest);
			if (level > 1)
			{
				if (fish)
				{
					// The game shows a fish's level the same way when it lies on the ground
					text += "[" + level + "]";
				}
				else
				{
					if (text.Length > 0)
					{
						text += " ";
					}
					string star = stars.GetStar(color);
					for (int i = 1; i < level; i++)
					{
						text += star;
					}
				}
			}
			if (distance >= 0)
			{
				text = ((text.Length > 0) ? text + " (" + distance + "m)" : distance + "m");
			}
			marker.label.text = text + rest;
			marker.labelSize = marker.label.GetPreferredValues();
			marker.label.rectTransform.sizeDelta = marker.labelSize;
		}

		/// <summary>
		/// Splits a name at the end of its first line
		/// </summary>
		/// <param name="name">The name</param>
		/// <param name="rest">Set to the rest of it from the line break on, or "" for a name of one line</param>
		/// <returns>Its first line</returns>
		internal static string FirstLine(string name, out string rest)
		{
			int lineEnd = name.IndexOf('\n');
			if (lineEnd < 0)
			{
				rest = "";
				return(name);
			}
			rest = name.Substring(lineEnd);
			return(name.Substring(0, lineEnd));
		}

		/// <summary>
		/// Centers the label just behind the arrow's tail, or behind the game's text when it was moved there first, kept
		/// inside the screen
		/// </summary>
		/// <param name="marker">The marker whose arrow has already been placed</param>
		/// <param name="pointing">The screen direction the arrow points in, as a unit vector</param>
		/// <param name="textBehind">How far the game's text placed behind the arrow reaches back from it, gap included,
		/// in screen pixels, or 0 when there is none</param>
		private static void PlaceLabel(Marker marker, Vector2 pointing, float textBehind)
		{
			RectTransform arrow = marker.arrow.rectTransform;
			RectTransform label = marker.label.rectTransform;
			// Sizes are in canvas units, positions in screen pixels
			float scale = arrow.lossyScale.x;
			Vector2 halfSize = marker.labelSize * scale * 0.5f;
			Vector2 center = ScreenEdge.Behind(arrow.position, pointing, (ArrowSize.y + LabelGap) * scale + textBehind, halfSize, new Vector2(Screen.width, Screen.height));
			label.position = new Vector3(center.x, center.y, 0f);
		}

		/// <summary>
		/// Moves the game's text over a ping or shout to just behind its arrow pinned to the screen edge, where the
		/// marker's own label would go, and holds it there for the rest of the frame (see ChatTexts)
		/// </summary>
		/// <param name="marker">The marker whose arrow has already been placed</param>
		/// <param name="text">The game's text, or null</param>
		/// <param name="pointing">The screen direction the arrow points in, as a unit vector</param>
		/// <returns>How far the text reaches back from the arrow, along with the gap after it, in screen pixels, or 0 when
		/// the text isn't shown</returns>
		private float PlaceChatText(Marker marker, GameObject text, Vector2 pointing)
		{
			if (text == null || !text.activeInHierarchy || !GetGraphicsBounds(text, out Rect bounds))
			{
				return(0f);
			}
			RectTransform arrow = marker.arrow.rectTransform;
			float scale = arrow.lossyScale.x;
			Vector2 center = ScreenEdge.Behind(arrow.position, pointing, (ArrowSize.y + LabelGap) * scale, bounds.size * 0.5f, new Vector2(Screen.width, Screen.height));
			// The letters sit off the text's own position, so the move is by how far their middle has to go
			Vector2 shift = center - bounds.center;
			Vector3 position = text.transform.position + new Vector3(shift.x, shift.y, 0f);
			text.transform.position = position;
			ChatTexts.Hold(text, position);
			return(Mathf.Abs(pointing.x) * bounds.width + Mathf.Abs(pointing.y) * bounds.height + LabelGap);
		}

		/// <summary>
		/// Returns the pooled marker at the index, creating it if needed, with its arrow active
		/// </summary>
		/// <param name="index">Pool index</param>
		/// <param name="enemyHud">The enemy HUD, for the label font</param>
		/// <returns>The marker</returns>
		private Marker GetMarker(int index, EnemyHud enemyHud)
		{
			if (index >= markers.Count)
			{
				Marker created = new Marker();
				created.arrow = CreateArrow();
				created.label = CreateLabel(enemyHud);
				created.alarm = CreateAlarm(enemyHud);
				markers.Add(created);
			}
			Marker marker = markers[index];
			SetActive(marker.arrow.gameObject, true);
			return(marker);
		}

		/// <summary>
		/// Sets a GameObject's active state when it differs
		/// </summary>
		/// <param name="go">The object</param>
		/// <param name="active">Whether it should be active</param>
		internal static void SetActive(GameObject go, bool active)
		{
			if (go.activeSelf != active)
			{
				go.SetActive(active);
			}
		}

		/// <summary>
		/// Creates the markers' canvas under the enemy HUD root, filling it and sorted above the HUD
		/// </summary>
		/// <returns>The canvas's transform</returns>
		private RectTransform CreateLayer()
		{
			GameObject go = new GameObject("CreatureMarkers", typeof(RectTransform));
			go.transform.SetParent(root.transform, false);
			RectTransform rect = (RectTransform)go.transform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			Canvas canvas = go.AddComponent<Canvas>();
			canvas.overrideSorting = true;
			canvas.sortingOrder = SortingOrder;
			return(rect);
		}

		/// <summary>
		/// Creates a centered name label in the markers' canvas, styled like the health bars' names
		/// </summary>
		/// <param name="enemyHud">The enemy HUD whose name text supplies the font</param>
		/// <returns>The label</returns>
		private TextMeshProUGUI CreateLabel(EnemyHud enemyHud)
		{
			GameObject go = new GameObject("CreatureMarkerLabel", typeof(RectTransform));
			go.transform.SetParent(layer, false);
			go.SetActive(false);
			TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
			TMP_Text template = GetNameTemplate(enemyHud);
			if (template != null)
			{
				label.font = template.font;
				label.fontSharedMaterial = template.fontSharedMaterial;
			}
			stars.Create(enemyHud, label.font, LabelFontSize);
			if (stars.Asset != null)
			{
				label.spriteAsset = stars.Asset;
			}
			label.fontSize = LabelFontSize;
			label.alignment = TextAlignmentOptions.Center;
			label.textWrappingMode = TextWrappingModes.NoWrap;
			label.raycastTarget = false;
			label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
			return(label);
		}

		/// <summary>
		/// Creates the red ! shown beside a provoked creature's arrow in the markers' canvas, hidden, in the health bars'
		/// font
		/// </summary>
		/// <param name="enemyHud">The enemy HUD whose name text supplies the font</param>
		/// <returns>The !</returns>
		private TextMeshProUGUI CreateAlarm(EnemyHud enemyHud)
		{
			GameObject go = new GameObject("CreatureMarkerAlarm", typeof(RectTransform));
			go.transform.SetParent(layer, false);
			go.SetActive(false);
			TextMeshProUGUI alarm = go.AddComponent<TextMeshProUGUI>();
			TMP_Text template = GetNameTemplate(enemyHud);
			if (template != null)
			{
				alarm.font = template.font;
				alarm.fontSharedMaterial = template.fontSharedMaterial;
			}
			alarm.text = AlarmText;
			alarm.fontSize = AlarmFontSize;
			alarm.fontStyle = FontStyles.Bold;
			alarm.color = AlarmColor;
			alarm.alignment = TextAlignmentOptions.Center;
			alarm.textWrappingMode = TextWrappingModes.NoWrap;
			alarm.raycastTarget = false;
			alarm.rectTransform.pivot = new Vector2(0.5f, 0.5f);
			alarm.rectTransform.sizeDelta = AlarmSize;
			return(alarm);
		}

		/// <summary>
		/// Puts a marker's ! beside its arrow, wherever the arrow ended up (see AlarmSpot)
		/// </summary>
		/// <param name="marker">The marker, its arrow placed and stacked</param>
		private void PlaceAlarm(Marker marker)
		{
			RectTransform arrow = marker.arrow.rectTransform;
			arrow.GetWorldCorners(corners);
			Vector2 min = corners[0];
			Vector2 max = corners[0];
			for (int i = 1; i < corners.Length; i++)
			{
				min = Vector2.Min(min, corners[i]);
				max = Vector2.Max(max, corners[i]);
			}
			float scale = arrow.lossyScale.x;
			Vector2 spot = AlarmSpot(Rect.MinMaxRect(min.x, min.y, max.x, max.y), AlarmSize * scale * 0.5f, AlarmGap * scale, Screen.width);
			marker.alarm.rectTransform.position = new Vector3(spot.x, spot.y, 0f);
		}

		/// <summary>
		/// Works out where the ! goes beside an arrow - just after it on the right, level with its middle, or on its left
		/// when there's no room on the screen to its right, as there isn't for an arrow pinned to the right edge
		/// </summary>
		/// <param name="arrow">The arrow's box on screen, however it's turned, in screen pixels</param>
		/// <param name="half">Half the !'s size, in screen pixels</param>
		/// <param name="gap">How far the ! stands from the arrow, in screen pixels</param>
		/// <param name="screenWidth">The screen's width, in pixels</param>
		/// <returns>The !'s middle, in screen pixels</returns>
		internal static Vector2 AlarmSpot(Rect arrow, Vector2 half, float gap, float screenWidth)
		{
			float x = arrow.xMax + gap + half.x;
			if (x + half.x > screenWidth)
			{
				x = arrow.xMin - gap - half.x;
			}
			return(new Vector2(x, arrow.center.y));
		}

		/// <summary>
		/// The name text on the enemy HUD's health bar template, or the player HUD's health text when it can't be found
		/// </summary>
		/// <param name="enemyHud">The enemy HUD</param>
		/// <returns>The text to copy the font from, or null</returns>
		private static TMP_Text GetNameTemplate(EnemyHud enemyHud)
		{
			Transform name = enemyHud.m_baseHud.transform.Find("Name");
			if (name != null && name.TryGetComponent(out TMP_Text text))
			{
				return(text);
			}
			if (Hud.instance != null)
			{
				return(Hud.instance.m_healthText);
			}
			return(null);
		}

		/// <summary>
		/// Creates an arrow image in the markers' canvas with its pivot on the tip
		/// </summary>
		/// <returns>The arrow's image</returns>
		private Image CreateArrow()
		{
			GameObject go = new GameObject("CreatureMarkerArrow", typeof(RectTransform));
			go.transform.SetParent(layer, false);
			Image image = go.AddComponent<Image>();
			image.sprite = GetSprite();
			image.raycastTarget = false;
			RectTransform rect = image.rectTransform;
			rect.pivot = new Vector2(0.5f, 0f);
			rect.sizeDelta = ArrowSize;
			return(image);
		}

		/// <summary>
		/// Draws the arrow (see ArrowSprite) the first time it is needed
		/// </summary>
		/// <returns>The arrow sprite</returns>
		private Sprite GetSprite()
		{
			if (sprite == null)
			{
				sprite = ArrowSprite.Create();
			}
			return(sprite);
		}
	}
}
