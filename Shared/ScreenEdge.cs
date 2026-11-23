using UnityEngine;

namespace Kriona.Shared
{
	/// <summary>
	/// Works out where something pointed at shows on screen - whether a point is on screen, and for one that isn't, where
	/// an arrow pointing at it is pinned to the screen's edge, or to a window's edge for one hidden behind it, and where
	/// a label goes behind the arrow
	/// </summary>
	/// <remarks>
	/// Points are in screen pixels, as Camera.WorldToScreenPointScaled gives them, and the screen's size is passed in
	/// </remarks>
	internal static class ScreenEdge
	{
		/// <summary>
		/// Checks whether a projected point is on screen - in front of the camera and within the screen
		/// </summary>
		/// <param name="screen">The point, projected</param>
		/// <param name="size">The screen's size, in pixels</param>
		/// <returns>True when it is</returns>
		internal static bool OnScreen(Vector3 screen, Vector2 size)
		{
			return(screen.z > 0f && screen.x >= 0f && screen.x <= size.x && screen.y >= 0f && screen.y <= size.y);
		}

		/// <summary>
		/// Pins an arrow pointing at an off-screen point to the screen's edge, in the point's direction from the
		/// screen's middle
		/// </summary>
		/// <param name="screen">The point, projected - behind the camera when its z is below 0</param>
		/// <param name="size">The screen's size, in pixels</param>
		/// <param name="margin">How far in from the edge the arrow's tip sits, in pixels</param>
		/// <param name="higher">Whether the point is higher than the player, which lets the arrow be pinned to the top
		/// half of the screen - otherwise it's mirrored into the bottom half, on the same side</param>
		/// <param name="direction">Set to the screen direction the arrow points in, as a unit vector</param>
		/// <returns>Where the arrow's tip goes, in pixels</returns>
		internal static Vector2 Pin(Vector3 screen, Vector2 size, float margin, bool higher, out Vector2 direction)
		{
			Vector2 center = size * 0.5f;
			direction = new Vector2(screen.x, screen.y) - center;
			if (screen.z < 0f)
			{
				// Points behind the camera project mirrored through the screen's middle
				direction = -direction;
			}
			if (!higher && direction.y > 0f)
			{
				// An arrow at the top of the screen reads as the point being above the player
				direction.y = -direction.y;
			}
			if (direction.sqrMagnitude < 0.01f)
			{
				direction = Vector2.down;
			}
			float halfWidth = Mathf.Max(0f, center.x - margin);
			float halfHeight = Mathf.Max(0f, center.y - margin);
			float scale = Mathf.Min(halfWidth / Mathf.Max(Mathf.Abs(direction.x), 0.001f), halfHeight / Mathf.Max(Mathf.Abs(direction.y), 0.001f));
			Vector2 edge = center + direction * scale;
			direction = direction.normalized;
			return(edge);
		}

		/// <summary>
		/// Pins an arrow pointing at a point hidden behind a rectangle on screen, like a window, to the rectangle's edge -
		/// where the line from its middle out through the point leaves it - pointing in at the point
		/// </summary>
		/// <param name="point">The point, in pixels</param>
		/// <param name="rect">The rectangle, in pixels</param>
		/// <param name="direction">Set to the screen direction the arrow points in, as a unit vector</param>
		/// <returns>Where the arrow's tip goes, in pixels</returns>
		internal static Vector2 PinToRect(Vector2 point, Rect rect, out Vector2 direction)
		{
			Vector2 center = rect.center;
			Vector2 outward = point - center;
			if (outward.sqrMagnitude < 0.01f)
			{
				outward = Vector2.down;
			}
			float scale = Mathf.Min(rect.width * 0.5f / Mathf.Max(Mathf.Abs(outward.x), 0.001f), rect.height * 0.5f / Mathf.Max(Mathf.Abs(outward.y), 0.001f));
			direction = -outward.normalized;
			return(center + outward * scale);
		}

		/// <summary>
		/// Works out how far to turn a graphic that points down so it points in a direction
		/// </summary>
		/// <param name="direction">The direction, as a unit vector</param>
		/// <returns>The turn around the screen's axis, in degrees</returns>
		internal static float Angle(Vector2 direction)
		{
			return(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f);
		}

		/// <summary>
		/// Works out where a label's middle goes behind an arrow - back along the way it points from its tip, far enough
		/// that the label's nearest edge clears the arrow - kept on screen
		/// </summary>
		/// <param name="tip">Where the arrow's tip is, in pixels</param>
		/// <param name="pointing">The direction the arrow points in, as a unit vector</param>
		/// <param name="behind">How far behind the tip the label's nearest edge sits - the arrow's length and a gap, in
		/// pixels</param>
		/// <param name="halfSize">Half the label's size, in pixels</param>
		/// <param name="size">The screen's size, in pixels</param>
		/// <returns>The label's middle, in pixels</returns>
		internal static Vector2 Behind(Vector2 tip, Vector2 pointing, float behind, Vector2 halfSize, Vector2 size)
		{
			// How far the label's middle sits from its edge facing the arrow, along the way it points
			float halfExtent = Mathf.Abs(pointing.x) * halfSize.x + Mathf.Abs(pointing.y) * halfSize.y;
			Vector2 center = tip - pointing * (behind + halfExtent);
			center.x = Mathf.Clamp(center.x, halfSize.x, Mathf.Max(halfSize.x, size.x - halfSize.x));
			center.y = Mathf.Clamp(center.y, halfSize.y, Mathf.Max(halfSize.y, size.y - halfSize.y));
			return(center);
		}
	}
}
