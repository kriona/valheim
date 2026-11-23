using System.Collections.Generic;
using Kriona.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieApocalypse
{
	/// <summary>
	/// Shows what a viking says over its head in the game's own speech bubble, made to fit the line and kept just above
	/// its health bar as it moves - and while it's off screen, pinned to the screen's edge behind an arrow pointing at
	/// it, with its name. Its name also shows on screen while its health bar doesn't
	/// </summary>
	/// <remarks>
	/// The game sizes its bubble for a trader's long speeches and centers the line in it, so a short line sits far above
	/// the speaker, and it hides the bubble while its speaker is off screen. The bubble is still the game's, so it times
	/// out, fades and is culled by distance the game's way, and other mods that look for it find it. It's placed after
	/// both the game's own placing and the camera's move, which run in no set order, so the last placing each frame goes
	/// by where the camera is now
	/// </remarks>
	internal static class VikingBubbles
	{
		/// <summary>
		/// How high above a viking's top point its line is said from, for the game's culling, and where the bubble sits
		/// while its health bar doesn't show, in meters
		/// </summary>
		private const float Lift = 0.5f;

		/// <summary>
		/// How the bubble's text shows - its size, the widest it runs before it wraps, and the room around it, across and
		/// up and down, in the bubble's own units
		/// </summary>
		private const float FontSize = 18f;
		private const float MaxTextWidth = 320f;
		private static readonly Vector2 Padding = new Vector2(36f, 22f);

		/// <summary>
		/// How far above the health bar the bubble sits, and how far in from the screen's edge an arrow's tip sits, in
		/// pixels
		/// </summary>
		private const float HudGap = 2f;
		private const float EdgeMargin = 40f;

		/// <summary>
		/// The arrow pointing at a viking off screen - its size, the gap between its tail and the bubble, in the bubble's
		/// own units, and its color, the gold the game names a speaker in
		/// </summary>
		private static readonly Vector2 ArrowSize = new Vector2(20f, 27f);
		private const float ArrowGap = 2f;
		private static readonly Color ArrowColor = new Color(1f, 0.85f, 0f, 1f);

		/// <summary>
		/// How many meters higher than the player a viking has to be for its arrow to be pinned to the top half of the
		/// screen, so uneven ground doesn't count as higher
		/// </summary>
		private const float HigherMargin = 1f;

		/// <summary>
		/// A viking's bubble - the game's, its line, whether it's laid out with the viking's name and which name, its
		/// size in the bubble's own units, and the arrow pointing at the viking while it's off screen
		/// </summary>
		private class Bubble
		{
			public Character Viking;
			public Chat.NpcText Npc;
			public string Line;
			public bool Named;
			public string Name;
			public Vector2 Size;
			public Image Arrow;
		}

		/// <summary>
		/// The bubble of each viking speaking, by its object
		/// </summary>
		private static readonly Dictionary<GameObject, Bubble> bubbles = new Dictionary<GameObject, Bubble>();

		/// <summary>
		/// Reused for the bubbles that are over, and for the corners of a health bar's name
		/// </summary>
		private static readonly List<GameObject> over = new List<GameObject>();
		private static readonly Vector3[] corners = new Vector3[4];

		/// <summary>
		/// The arrow's sprite, drawn the first time one is needed
		/// </summary>
		private static Sprite arrowSprite;

		/// <summary>
		/// Shows a line a viking says in its bubble, in place of anything it was saying
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <param name="text">What it says</param>
		/// <param name="time">How long it shows, in seconds</param>
		/// <param name="cull">How far away it can be seen, in meters</param>
		public static void Show(Character viking, string text, float time, float cull)
		{
			Chat chat = Chat.instance;
			if (chat == null || viking == null)
			{
				return;
			}
			Vector3 lift = Vector3.up * (viking.GetTopPoint().y - viking.transform.position.y + Lift);
			chat.SetNpcText(viking.gameObject, lift, cull, time, "", text, false);
			Chat.NpcText npc = chat.FindNpcText(viking.gameObject);
			if (npc == null || npc.m_gui == null)
			{
				// The game shows no bubbles while the player has hidden the HUD
				return;
			}
			if (!bubbles.TryGetValue(viking.gameObject, out Bubble bubble))
			{
				bubble = new Bubble();
				bubbles[viking.gameObject] = bubble;
			}
			bubble.Viking = viking;
			bubble.Npc = npc;
			bubble.Line = Localization.instance.Localize(text);
			bubble.Name = null;
			Style(npc);
			Place();
		}

		/// <summary>
		/// Sets a bubble's text up to fit its line - a fixed size rather than shrinking to fit the game's large bubble,
		/// wrapping at MaxTextWidth, with Padding around it
		/// </summary>
		/// <param name="npc">The game's bubble</param>
		private static void Style(Chat.NpcText npc)
		{
			npc.m_textField.enableAutoSizing = false;
			npc.m_textField.fontSize = FontSize;
			npc.m_textField.textWrappingMode = TMPro.TextWrappingModes.Normal;
			npc.m_textField.alignment = TMPro.TextAlignmentOptions.Center;
			RectTransform text = npc.m_textField.rectTransform;
			text.anchorMin = Vector2.zero;
			text.anchorMax = Vector2.one;
			text.offsetMin = Padding * 0.5f;
			text.offsetMax = -Padding * 0.5f;
			if (npc.m_topicField != null)
			{
				npc.m_topicField.gameObject.SetActive(false);
			}
		}

		/// <summary>
		/// Lays a bubble out with or without its viking's name, in the game's colors for a speaker's name over its line,
		/// sizing the bubble to fit - again only when the name is wanted or changes
		/// </summary>
		/// <param name="bubble">The bubble</param>
		/// <param name="named">Whether its viking's name shows</param>
		private static void Layout(Bubble bubble, bool named)
		{
			string name = (named ? SpeakerName(bubble.Viking) : "");
			if (bubble.Name != null && bubble.Named == named && bubble.Name == name)
			{
				return;
			}
			bubble.Named = named;
			bubble.Name = name;
			string text = (named ? "<color=orange>" + name + "</color>\n" + bubble.Line : bubble.Line);
			TMPro.TextMeshProUGUI field = bubble.Npc.m_textField;
			field.text = text;
			float width = Mathf.Min(field.GetPreferredValues(text, MaxTextWidth, 0f).x, MaxTextWidth);
			float height = field.GetPreferredValues(text, width, 0f).y;
			bubble.Size = new Vector2(width, height) + Padding;
			((RectTransform)bubble.Npc.m_gui.transform).sizeDelta = bubble.Size;
		}

		/// <summary>
		/// Gets a viking's name for its bubble, as the local player knows it, without what it's doing in debug mode
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <returns>The name</returns>
		private static string SpeakerName(Character viking)
		{
			return((viking is LivingVikingBody living) ? living.KnownName() : viking.GetHoverName());
		}

		/// <summary>
		/// Places every viking's bubble - just above its health bar, or over its head while its health bar doesn't show,
		/// kept on screen, and while the viking is off screen, behind an arrow pinned to the screen's edge - forgetting
		/// those that are over
		/// </summary>
		public static void Place()
		{
			if (bubbles.Count == 0)
			{
				return;
			}
			Chat chat = Chat.instance;
			Camera camera = Utils.GetMainCamera();
			Player player = Player.m_localPlayer;
			bool hidden = (Hud.instance == null || Hud.instance.m_userHidden);
			Vector2 screenSize = new Vector2(Screen.width, Screen.height);
			foreach (KeyValuePair<GameObject, Bubble> entry in bubbles)
			{
				Bubble bubble = entry.Value;
				if (chat == null || entry.Key == null || bubble.Viking == null || bubble.Npc == null || bubble.Npc.m_gui == null || !chat.m_npcTexts.Contains(bubble.Npc))
				{
					over.Add(entry.Key);
					continue;
				}
				Vector3 said = bubble.Viking.transform.position + bubble.Npc.m_offset;
				bool expired = (bubble.Npc.m_timeout && bubble.Npc.m_ttl <= 0f);
				// Timed out, too far away or with the HUD hidden, it's left to the game to fade out
				if (camera == null || hidden || expired || Vector3.Distance(camera.transform.position, said) > bubble.Npc.m_cullDistance)
				{
					ShowArrow(bubble, false);
					continue;
				}
				RectTransform gui = (RectTransform)bubble.Npc.m_gui.transform;
				float scale = gui.lossyScale.x;
				Vector3 top = bubble.Viking.GetTopPoint();
				Vector3 screen = camera.WorldToScreenPointScaled(top);
				if (ScreenEdge.OnScreen(screen, screenSize))
				{
					float hudHeight = HudHeight(bubble.Viking);
					Layout(bubble, hudHeight < 0f);
					float bottom = ((hudHeight >= 0f) ? screen.y + hudHeight + HudGap : camera.WorldToScreenPointScaled(top + Vector3.up * Lift).y);
					Vector2 placed = ClampBubble(new Vector2(screen.x, bottom), bubble.Size * scale, screenSize);
					gui.position = new Vector3(placed.x, placed.y, 0f);
					ShowArrow(bubble, false);
				}
				else
				{
					Layout(bubble, true);
					bool higher = (player != null && bubble.Viking.transform.position.y > player.transform.position.y + HigherMargin);
					Vector2 tip = ScreenEdge.Pin(screen, screenSize, EdgeMargin, higher, out Vector2 pointing);
					Image arrow = ShowArrow(bubble, true);
					if (arrow != null)
					{
						arrow.rectTransform.position = new Vector3(tip.x, tip.y, 0f);
						arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ScreenEdge.Angle(pointing));
					}
					Vector2 half = bubble.Size * scale * 0.5f;
					Vector2 center = ScreenEdge.Behind(tip, pointing, (ArrowSize.y + ArrowGap) * scale, half, screenSize);
					// The bubble's pivot is its bottom's middle
					gui.position = new Vector3(center.x, center.y - half.y, 0f);
				}
				// The game hides a bubble whose speaker is off screen
				bubble.Npc.SetVisible(true);
			}
			foreach (GameObject key in over)
			{
				if (bubbles.TryGetValue(key, out Bubble bubble) && bubble.Arrow != null)
				{
					Object.Destroy(bubble.Arrow.gameObject);
				}
				bubbles.Remove(key);
			}
			over.Clear();
		}

		/// <summary>
		/// Keeps a bubble on screen - its sides within the screen's, and its bottom no lower than the screen's and its
		/// top no higher - with a bubble wider or taller than the screen against its left side or its bottom
		/// </summary>
		/// <param name="anchor">Where the bubble's bottom's middle goes, in pixels</param>
		/// <param name="size">The bubble's size, in pixels</param>
		/// <param name="screenSize">The screen's size, in pixels</param>
		/// <returns>Where its bottom's middle goes, on screen</returns>
		internal static Vector2 ClampBubble(Vector2 anchor, Vector2 size, Vector2 screenSize)
		{
			float x = Mathf.Clamp(anchor.x, size.x * 0.5f, Mathf.Max(size.x * 0.5f, screenSize.x - size.x * 0.5f));
			float y = Mathf.Clamp(anchor.y, 0f, Mathf.Max(0f, screenSize.y - size.y));
			return(new Vector2(x, y));
		}

		/// <summary>
		/// Works out how far the top of the name over a viking's health bar sits above its top point, in pixels, while
		/// the game shows it
		/// </summary>
		/// <remarks>
		/// The game places the health bar at the viking's top point, so measuring from the bar's own position gives a
		/// height that holds whether the game has moved it yet this frame or not
		/// </remarks>
		/// <param name="viking">The viking</param>
		/// <returns>The height, or -1 when its health bar's name doesn't show</returns>
		private static float HudHeight(Character viking)
		{
			EnemyHud hud = EnemyHud.instance;
			if (hud == null || !hud.m_huds.TryGetValue(viking, out EnemyHud.HudData data) || data.m_gui == null || !data.m_gui.activeSelf)
			{
				return(-1f);
			}
			if (data.m_name == null || !data.m_name.gameObject.activeInHierarchy)
			{
				return(-1f);
			}
			data.m_name.rectTransform.GetWorldCorners(corners);
			return(Mathf.Max(0f, corners[1].y - data.m_gui.transform.position.y));
		}

		/// <summary>
		/// Shows or hides the arrow pointing at a bubble's viking, making it the first time it's shown
		/// </summary>
		/// <param name="bubble">The bubble</param>
		/// <param name="show">Whether it shows</param>
		/// <returns>The arrow, or null when it's hidden or can't be made</returns>
		private static Image ShowArrow(Bubble bubble, bool show)
		{
			if (!show)
			{
				if (bubble.Arrow != null && bubble.Arrow.gameObject.activeSelf)
				{
					bubble.Arrow.gameObject.SetActive(false);
				}
				return(null);
			}
			if (bubble.Arrow == null)
			{
				bubble.Arrow = CreateArrow(bubble.Npc.m_gui.transform.parent);
			}
			if (!bubble.Arrow.gameObject.activeSelf)
			{
				bubble.Arrow.gameObject.SetActive(true);
			}
			return(bubble.Arrow);
		}

		/// <summary>
		/// Makes an arrow beside the game's bubbles, with its pivot on its tip
		/// </summary>
		/// <param name="parent">Where the game's bubbles are</param>
		/// <returns>The arrow</returns>
		private static Image CreateArrow(Transform parent)
		{
			if (arrowSprite == null)
			{
				arrowSprite = ArrowSprite.Create();
			}
			GameObject go = new GameObject("VikingSpeechArrow", typeof(RectTransform));
			go.transform.SetParent(parent, false);
			Image image = go.AddComponent<Image>();
			image.sprite = arrowSprite;
			image.color = ArrowColor;
			image.raycastTarget = false;
			RectTransform rect = image.rectTransform;
			rect.pivot = new Vector2(0.5f, 0f);
			rect.sizeDelta = ArrowSize;
			return(image);
		}
	}
}
