namespace VikingVillages
{
	/// <summary>
	/// A valkyrie's seat - she needs no saddle to be ridden, only to be befriended (tamed) and friendly with whoever
	/// gets on. She keeps the Asksvin's saddle she's built on as an unseen seat, always on her, which the game rides the
	/// way it rides any saddle
	/// </summary>
	internal static class ValkyrieSeat
	{
		/// <summary>
		/// Checks whether a player can get on a valkyrie - she's alive, befriended and isn't angry with them
		/// </summary>
		/// <param name="valkyrie">The valkyrie</param>
		/// <param name="player">The player</param>
		/// <returns>True when they can</returns>
		public static bool CanMount(Character valkyrie, Player player)
		{
			return(valkyrie != null && player != null && !valkyrie.IsDead() && valkyrie.IsTamed() && !BaseAI.IsEnemy(valkyrie, player));
		}

		/// <summary>
		/// Puts her seat on her - noted in her ZDO by the client that owns her, as the game notes a saddle, and shown on
		/// every client
		/// </summary>
		/// <param name="tameable">Her Tameable</param>
		/// <param name="nview">Her ZNetView</param>
		public static void Seat(Tameable tameable, ZNetView nview)
		{
			if (tameable == null || tameable.m_saddle == null || nview == null || !nview.IsValid())
			{
				return;
			}
			if (nview.IsOwner() && !nview.GetZDO().GetBool(ZDOVars.s_haveSaddleHash))
			{
				nview.GetZDO().Set(ZDOVars.s_haveSaddleHash, true);
			}
			tameable.m_saddle.gameObject.SetActive(true);
		}
	}
}
