namespace ZombieApocalypse
{
	/// <summary>
	/// Tells whether a player has the corpse run status effect from picking up their tombstone, which keeps them from
	/// making zombies - their own body doesn't rise when they die, and nothing they kill rises
	/// </summary>
	/// <remarks>
	/// A player's status effects are only known on their own client, but what they kill can die on another client
	/// that owns it, so each client keeps whether its player has corpse run in the player's ZDO for the others to read
	/// </remarks>
	internal static class CorpseRun
	{
		/// <summary>
		/// On a player, whether they have corpse run
		/// </summary>
		private static readonly int CorpseRunKey = "kriona.ZombieApocalypse.corpseRun".GetStableHashCode();

		/// <summary>
		/// The game's corpse run status effect
		/// </summary>
		private static readonly int CorpseRunEffect = "CorpseRun".GetStableHashCode();

		/// <summary>
		/// Keeps whether the local player has corpse run in their ZDO - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			Player player = Player.m_localPlayer;
			ZNetView nview = (player != null) ? player.m_nview : null;
			if (nview == null || !nview.IsValid() || !nview.IsOwner())
			{
				return;
			}
			bool protectedNow = HasEffect(player);
			ZDO zdo = nview.GetZDO();
			if (zdo.GetBool(CorpseRunKey) != protectedNow)
			{
				zdo.Set(CorpseRunKey, protectedNow);
			}
		}

		/// <summary>
		/// Checks whether a creature is a player with corpse run
		/// </summary>
		/// <param name="character">The creature, or null</param>
		/// <returns>True for a player with corpse run</returns>
		public static bool Protects(Character character)
		{
			if (character == null || !character.IsPlayer())
			{
				return(false);
			}
			if (character == Player.m_localPlayer)
			{
				return(HasEffect(character));
			}
			ZNetView nview = character.m_nview;
			return(nview != null && nview.IsValid() && nview.GetZDO().GetBool(CorpseRunKey));
		}

		/// <summary>
		/// Checks this client's own player's status effects for corpse run
		/// </summary>
		/// <param name="character">The local player</param>
		/// <returns>True when they have it</returns>
		private static bool HasEffect(Character character)
		{
			SEMan seman = character.GetSEMan();
			return(seman != null && seman.HaveStatusEffect(CorpseRunEffect));
		}
	}
}
