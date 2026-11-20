namespace ZombieApocalypse
{
	/// <summary>
	/// Tells whether a player has the corpse run status effect from picking up their tombstone, which keeps their body
	/// from rising as a tombstone zombie viking when they die
	/// </summary>
	internal static class CorpseRun
	{
		/// <summary>
		/// The game's corpse run status effect
		/// </summary>
		private static readonly int CorpseRunEffect = "CorpseRun".GetStableHashCode();

		/// <summary>
		/// Checks whether a player has corpse run, from their status effects, which are only known on their own client
		/// </summary>
		/// <param name="player">The local player</param>
		/// <returns>True when they have it</returns>
		public static bool Protects(Player player)
		{
			SEMan seman = player.GetSEMan();
			return(seman != null && seman.HaveStatusEffect(CorpseRunEffect));
		}
	}
}
