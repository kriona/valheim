using UnityEngine;

namespace CreatureMarker
{
	/// <summary>
	/// A marked creature or fish, with what the arrow and map dot need to show it
	/// </summary>
	public struct Target
	{
		/// <summary>
		/// The Character, Trader, Petable or Fish being marked, so a marker can tell when it is given a different one
		/// </summary>
		public Object source;

		/// <summary>
		/// The target's position in the world
		/// </summary>
		public Vector3 position;

		/// <summary>
		/// The world point the arrow sits above - the top of a creature's head, or a fish's position
		/// </summary>
		public Vector3 top;

		/// <summary>
		/// The target's name token, localized by the marker when it shows the name
		/// </summary>
		public string nameToken;

		/// <summary>
		/// The creature's level, which is its star count plus one, or the fish's level - both start at 1
		/// </summary>
		public int level;

		/// <summary>
		/// Whether the target is a fish, whose level is shown as a number instead of stars
		/// </summary>
		public bool fish;

		/// <summary>
		/// The marker color
		/// </summary>
		public Color color;
	}
}
