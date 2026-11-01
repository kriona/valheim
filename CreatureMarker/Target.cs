using UnityEngine;

namespace CreatureMarker
{
	/// <summary>
	/// A marked creature or fish, with what the arrow and map dot need to show it
	/// </summary>
	public struct Target
	{
		/// <summary>
		/// The Character or Fish being marked, so a marker can tell when it is given a different one
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
		/// The marker color
		/// </summary>
		public Color color;
	}
}
