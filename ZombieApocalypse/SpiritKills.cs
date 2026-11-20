using System.Runtime.CompilerServices;

namespace ZombieApocalypse
{
	/// <summary>
	/// Remembers which creatures were last hit with spirit damage, so a creature killed by spirit damage (e.g., a
	/// silver weapon or a dvergr mage's spell) stays dead
	/// </summary>
	/// <remarks>
	/// Living creatures are immune to spirit damage, so by the time the game records a hit as the creature's last
	/// its spirit damage is gone. The hit's spirit damage is read as it arrives instead, and kept for the creature
	/// whenever the hit becomes its last
	/// </remarks>
	internal static class SpiritKills
	{
		/// <summary>
		/// The creatures whose last hit dealt spirit damage
		/// </summary>
		private static readonly ConditionalWeakTable<Character, object> spiritHit = new ConditionalWeakTable<Character, object>();

		/// <summary>
		/// Records whether a creature's last hit dealt spirit damage
		/// </summary>
		/// <param name="character">The creature</param>
		/// <param name="spirit">Whether the hit dealt spirit damage, before the creature's resistances</param>
		public static void SetLastHit(Character character, bool spirit)
		{
			spiritHit.Remove(character);
			if (spirit)
			{
				spiritHit.Add(character, null);
			}
		}

		/// <summary>
		/// Checks whether a creature's last hit dealt spirit damage
		/// </summary>
		/// <param name="character">The creature</param>
		/// <returns>True when it did</returns>
		public static bool LastHitWasSpirit(Character character)
		{
			return(spiritHit.TryGetValue(character, out _));
		}
	}
}
