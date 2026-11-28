using System.Reflection;
using TMPro;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Shows the name of the village the player is in at the top of the screen, while they're inside its rings and not
	/// high above it (see Look), styled
	/// like the name over its fire (see VillageNameTags) - in the clock's spot when aedenthorn's Clock Mod is installed,
	/// moving the clock down under it, or else where that clock would be - and behind the big map and the loading screen
	/// while either shows (see VillageNameTags.Sort)
	/// </summary>
	/// <remarks>
	/// Clock Mod draws its clock with IMGUI at a window position it keeps in static fields and saves to its config
	/// whenever the window has moved, so the clock is moved down only while its own OnGUI runs and put back after it
	/// (see VillageNameClockPatch), leaving the position it saves alone. IMGUI measures from the top of the screen in
	/// pixels, and the health bars' canvas the name is shown in from the bottom
	/// </remarks>
	internal static class VillageNameHud
	{
		/// <summary>
		/// Clock Mod's plugin GUID
		/// </summary>
		public const string ClockGuid = "aedenthorn.ClockMod";

		/// <summary>
		/// How often the village the player is in is looked for again, in seconds
		/// </summary>
		private const float LookInterval = 0.5f;

		/// <summary>
		/// Where the name's top sits with no clock, as a share of the screen's height - Clock Mod's own default
		/// </summary>
		private const float ScreenTop = 0.03f;

		/// <summary>
		/// How far down the clock is moved while the name shows in its spot, in pixels - 0 while no name shows
		/// </summary>
		public static float ClockShift;

		/// <summary>
		/// The name of the village the player is in, or "" when they're in none or it has no name
		/// </summary>
		private static string name = "";

		private static float nextLook;

		/// <summary>
		/// Clock Mod's static fields - its window, where it's put, the clock text's place in the window and whether it
		/// has set itself up - or null when it isn't installed or doesn't have them
		/// </summary>
		private static FieldInfo clockWindow;
		private static FieldInfo clockPosition;
		private static FieldInfo clockText;
		private static FieldInfo clockReady;

		/// <summary>
		/// Where the clock's text sits in its window, noted as the clock draws it - IMGUI only lays the text out outside
		/// its Layout event, and holds a placeholder in Clock Mod's field the rest of the time
		/// </summary>
		private static Rect clockLine;
		private static bool clockLineNoted;

		/// <summary>
		/// The name's label, and the name it was last set to
		/// </summary>
		private static TextMeshProUGUI label;
		private static string shownName;

		/// <summary>
		/// Finds the fields of Clock Mod's plugin that the name and the clock's move go by
		/// </summary>
		/// <param name="plugin">Clock Mod's plugin type</param>
		/// <returns>False when it doesn't have them all, so the clock is left where it is</returns>
		public static bool HookClock(System.Type plugin)
		{
			BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
			clockWindow = plugin.GetField("windowRect", flags);
			clockPosition = plugin.GetField("clockPosition", flags);
			clockText = plugin.GetField("timeRect", flags);
			clockReady = plugin.GetField("configApplied", flags);
			bool found = (clockWindow != null && clockWindow.FieldType == typeof(Rect) && clockPosition != null && clockPosition.FieldType == typeof(Vector2) && clockText != null && clockText.FieldType == typeof(Rect) && clockReady != null && clockReady.FieldType == typeof(bool));
			if (!found)
			{
				clockWindow = null;
				clockPosition = null;
				clockText = null;
				clockReady = null;
				Plugin.Log.LogWarning("Clock Mod doesn't have the fields expected, so the village name shows in its own spot and the clock isn't moved");
			}
			return(found);
		}

		/// <summary>
		/// Moves Clock Mod's clock down or back up, along with the position it compares its window to, so it doesn't
		/// take the move for a drag and save it
		/// </summary>
		/// <param name="shift">How far down to move it, in pixels - negative to move it back up</param>
		public static void MoveClock(float shift)
		{
			if (shift == 0f || clockWindow == null)
			{
				return;
			}
			Rect window = (Rect)clockWindow.GetValue(null);
			window.y += shift;
			clockWindow.SetValue(null, window);
			Vector2 position = (Vector2)clockPosition.GetValue(null);
			position.y += shift;
			clockPosition.SetValue(null, position);
		}

		/// <summary>
		/// Notes where the clock's text sits in its window once the clock has drawn it, outside IMGUI's Layout event
		/// </summary>
		public static void NoteClockLine()
		{
			if (clockText == null || Event.current == null || Event.current.type == EventType.Layout)
			{
				return;
			}
			clockLine = (Rect)clockText.GetValue(null);
			clockLineNoted = true;
		}

		/// <summary>
		/// Looks for the village the local player is inside every LookInterval seconds, and shows its name where the
		/// clock's text starts, or at the top middle of the screen with no clock, while the HUD shows
		/// </summary>
		public static void Update()
		{
			if (Time.time >= nextLook)
			{
				nextLook = Time.time + LookInterval;
				name = Look();
			}
			EnemyHud hud = EnemyHud.instance;
			if (name.Length == 0 || hud == null || hud.m_hudRoot == null || Hud.instance == null || !Hud.instance.IsVisible())
			{
				Hide();
				return;
			}
			if (label == null || label.transform.parent != hud.m_hudRoot.transform)
			{
				if (label != null)
				{
					Object.Destroy(label.gameObject);
				}
				label = VillageNameTags.MakeLabel(hud, "VillageNameHud");
				label.rectTransform.pivot = new Vector2(0.5f, 1f);
				shownName = null;
			}
			if (shownName != name)
			{
				shownName = name;
				label.text = name;
				label.rectTransform.sizeDelta = label.GetPreferredValues();
			}
			bool clock = (clockReady != null && (bool)clockReady.GetValue(null));
			Vector2 spot = (clock ? ClockSpot() : new Vector2(Screen.width / 2f, Screen.height * ScreenTop));
			label.rectTransform.position = new Vector3(spot.x, Screen.height - spot.y, 0f);
			VillageNameTags.Sort(label);
			if (!label.gameObject.activeSelf)
			{
				label.gameObject.SetActive(true);
			}
			float scale = ((label.canvas != null) ? label.canvas.scaleFactor : 1f);
			ClockShift = (clock ? label.rectTransform.sizeDelta.y * scale : 0f);
		}

		/// <summary>
		/// Finds where the clock's text starts - the middle of its top, in IMGUI's pixels from the top left - before it's
		/// moved down
		/// </summary>
		/// <returns>The spot</returns>
		private static Vector2 ClockSpot()
		{
			Rect window = (Rect)clockWindow.GetValue(null);
			// Until the clock has shown, the top left of its window
			return(clockLineNoted ? new Vector2(window.x + clockLine.center.x, window.y + clockLine.y) : window.position);
		}

		/// <summary>
		/// Hides the name and puts the clock back in its spot
		/// </summary>
		private static void Hide()
		{
			ClockShift = 0f;
			if (label != null && label.gameObject.activeSelf)
			{
				label.gameObject.SetActive(false);
			}
		}

		/// <summary>
		/// Finds the name of the loaded village the local player is inside - within its rings, and within the sphere
		/// around its middle that reaches as far as its rings do, so flying high over it doesn't count (see InSphere)
		/// </summary>
		/// <returns>The name, or "" when they're in none or it has no name</returns>
		private static string Look()
		{
			Player player = Player.m_localPlayer;
			if (player == null || ZNetScene.instance == null)
			{
				return("");
			}
			Vector3 at = player.transform.position;
			foreach (Village village in Village.Loaded())
			{
				if (village.Contains(at) && InSphere(at, village.Middle, village.Radius))
				{
					return(VillageNames.Get(village));
				}
			}
			return("");
		}

		/// <summary>
		/// Checks whether a point is within a sphere - a village's rings seen from above reach Radius from its middle, so
		/// the sphere holds the ground they cover and the air just over it, but not high above it
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="middle">The sphere's middle</param>
		/// <param name="radius">Its radius, in meters</param>
		/// <returns>True when the point is within it</returns>
		internal static bool InSphere(Vector3 point, Vector3 middle, float radius)
		{
			return((point - middle).sqrMagnitude <= radius * radius);
		}
	}
}
