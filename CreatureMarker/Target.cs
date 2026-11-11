using UnityEngine;

namespace CreatureMarker
{
	/// <summary>
	/// A marked creature, fish, bird or item, with what the arrow and map dot need to show it
	/// </summary>
	public struct Target
	{
		/// <summary>
		/// The Character, Trader, Petable, Fish, RandomFlyingBird, Leviathan, Odin, Raven, Valkyrie or ItemDrop being
		/// marked, so a marker can tell when it is given a different one
		/// </summary>
		public Object source;

		/// <summary>
		/// The target's position in the world
		/// </summary>
		public Vector3 position;

		/// <summary>
		/// The world point the arrow sits above - the top of a creature's head, or a fish's, bird's or item's position
		/// </summary>
		public Vector3 top;

		/// <summary>
		/// The target's name token, localized by the marker when it shows the name
		/// </summary>
		public string nameToken;

		/// <summary>
		/// How many items are in an item's stack, shown after its name when it is more than 1 - 0 for targets that
		/// aren't items
		/// </summary>
		public int count;

		/// <summary>
		/// The creature's level, which is its star count plus one, or the fish's level - both start at 1, and birds are
		/// always level 1
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
