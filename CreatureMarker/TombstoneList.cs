using System.Collections.Generic;

namespace CreatureMarker
{
	/// <summary>
	/// A tombstone in the world, with its height measured the first time it is marked
	/// </summary>
	internal class WorldTombstone
	{
		/// <summary>
		/// The tombstone
		/// </summary>
		public TombStone source;

		/// <summary>
		/// How far the top of the stone sits above its position, or -1 until it has been measured
		/// </summary>
		private float height = -1f;

		/// <summary>
		/// How far the top of the stone sits above its position, measured the first time it is needed
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
		/// The name of the player whose tombstone it is, or the tombstone's own name when it has no owner
		/// </summary>
		/// <returns>The owner's name without rich text, or the tombstone's name token</returns>
		public string GetNameToken()
		{
			string owner = source.GetOwnerName();
			if (string.IsNullOrEmpty(owner))
			{
				return(source.m_text);
			}
			return(owner.RemoveRichTextTags());
		}
	}

	/// <summary>
	/// The tombstones in the world, gathered as they are created since the game keeps no list of them
	/// </summary>
	internal static class TombstoneList
	{
		private static readonly List<WorldTombstone> tombstones = new List<WorldTombstone>();

		/// <summary>
		/// The tombstones that still exist
		/// </summary>
		/// <returns>The list, with destroyed tombstones removed</returns>
		public static List<WorldTombstone> Get()
		{
			tombstones.RemoveAll((WorldTombstone tombstone) => (tombstone.source == null));
			return(tombstones);
		}

		/// <summary>
		/// Adds a tombstone
		/// </summary>
		/// <param name="source">The tombstone that was just created</param>
		public static void Add(TombStone source)
		{
			foreach (WorldTombstone existing in tombstones)
			{
				if (existing.source == source)
				{
					return;
				}
			}
			WorldTombstone tombstone = new WorldTombstone();
			tombstone.source = source;
			tombstones.Add(tombstone);
		}
	}
}
