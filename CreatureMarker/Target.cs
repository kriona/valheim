using UnityEngine;

namespace CreatureMarker
{
	/// <summary>
	/// A marked creature, fish, bird, item, portal or tombstone, with what the arrow and map dot need to show it
	/// </summary>
	public struct Target
	{
		/// <summary>
		/// The Character, Trader, Petable, Fish, RandomFlyingBird, Leviathan, Odin, Raven, Valkyrie, ItemDrop,
		/// TeleportWorld or TombStone being marked, the portal a trader or pet is seen through, or the game's text above
		/// a map ping or shout, so a marker can tell when it is given a different one
		/// </summary>
		public Object source;

		/// <summary>
		/// Whether the target is a map ping or a shout, like the one a player sends when they arrive in the world, whose
		/// sender's name the game shows above it and on the map itself
		/// </summary>
		public bool chatText;

		/// <summary>
		/// Whether the target is a trader or pet seen through a portal's PortalPreview view, placed where it appears on
		/// this side of the portal, which gets no map dot
		/// </summary>
		public bool throughPortal;

		/// <summary>
		/// The target's position in the world
		/// </summary>
		public Vector3 position;

		/// <summary>
		/// The world point the arrow sits above - the top of a creature's head, a portal's frame or a tombstone, a
		/// fish's, bird's or item's position, or where the game puts a ping's or shout's text
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

		/// <summary>
		/// Whether the target is a creature that leaves players alone until they provoke it, like the Dvergr, and has
		/// been provoked - shown with a red ! beside its arrow
		/// </summary>
		public bool provoked;
	}
}
