using System.Collections.Generic;

namespace CreatureMarker
{
	/// <summary>
	/// A portal in the world, with its height measured the first time it is marked
	/// </summary>
	internal class WorldPortal
	{
		/// <summary>
		/// The portal
		/// </summary>
		public TeleportWorld source;

		/// <summary>
		/// The name token of the kind of portal, like "$piece_portal", shown when the portal has no tag
		/// </summary>
		public string pieceNameToken;

		/// <summary>
		/// How far the top of the portal's frame sits above its position, or -1 until it has been measured
		/// </summary>
		private float height = -1f;

		/// <summary>
		/// How far the top of the portal's frame sits above its position, measured the first time it is needed
		/// </summary>
		/// <returns>The height in meters</returns>
		public float GetHeight()
		{
			if (height < 0f)
			{
				height = NPC.MeasureHeight(source);
			}
			return(height);
		}

		/// <summary>
		/// The portal's tag, which pairs it with the portal it leads to, or the kind of portal when it has no tag
		/// </summary>
		/// <returns>The tag without rich text, or the piece's name token</returns>
		public string GetNameToken()
		{
			string tag = source.GetText();
			if (string.IsNullOrEmpty(tag))
			{
				return(pieceNameToken);
			}
			return(tag.RemoveRichTextTags());
		}
	}

	/// <summary>
	/// The portals in the world, gathered as they are created since the game keeps no list of them
	/// </summary>
	internal static class PortalList
	{
		private static readonly List<WorldPortal> portals = new List<WorldPortal>();

		/// <summary>
		/// The portals that still exist
		/// </summary>
		/// <returns>The list, with destroyed portals removed</returns>
		public static List<WorldPortal> Get()
		{
			portals.RemoveAll((WorldPortal portal) => (portal.source == null));
			return(portals);
		}

		/// <summary>
		/// Adds a portal
		/// </summary>
		/// <param name="source">The portal that was just created</param>
		public static void Add(TeleportWorld source)
		{
			foreach (WorldPortal existing in portals)
			{
				if (existing.source == source)
				{
					return;
				}
			}
			Piece piece = source.GetComponent<Piece>();
			WorldPortal portal = new WorldPortal();
			portal.source = source;
			portal.pieceNameToken = ((piece != null && !string.IsNullOrEmpty(piece.m_name)) ? piece.m_name : "$piece_portal");
			portals.Add(portal);
		}
	}
}
