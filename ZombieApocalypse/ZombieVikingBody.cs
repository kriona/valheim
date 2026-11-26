using Kriona.Vikings;

namespace ZombieApocalypse
{
	/// <summary>
	/// A zombie viking's body, which works like a monster's apart from what it shares with a living viking
	/// </summary>
	internal class ZombieVikingBody : VikingBody
	{
		/// <summary>
		/// Gets the zombie's name as the local player sees it - ZombieName and the name of the living or dvergr viking it
		/// rose from to a player that viking knew, and plain Zombie Viking to anyone else, without Viking Villages' "(?)"
		/// for a viking that doesn't know them yet, since a zombie never gets to know anyone (see VikingName)
		/// </summary>
		/// <returns>Its name</returns>
		public override string GetHoverName()
		{
			Player player = Player.m_localPlayer;
			string name = ((player != null && m_nview != null && m_nview.IsValid()) ? VikingName.ShownTo(m_nview.GetZDO(), player.GetPlayerID()) : "");
			if (name.Length == 0)
			{
				return(base.GetHoverName());
			}
			string word = Plugin.ZombieName.Value;
			return(string.IsNullOrEmpty(word) ? name : word + " " + name);
		}

		/// <summary>
		/// Gets how much the zombie's heat is kept off - all of it for a zombie of the Ashlands, which the lava and the
		/// boiling sea don't hurt, and none otherwise, as for any creature but a player
		/// </summary>
		/// <remarks>
		/// Character.UpdateLava, UpdateAshlandsWater and UpdateHeatDamage scale the heat built up and the damage dealt
		/// by what's left of 1 after this, so all of it means the heat never builds and the lava never sets it burning.
		/// Fire from anywhere else still burns it
		/// </remarks>
		/// <returns>1 for a zombie of the Ashlands, otherwise the game's 0</returns>
		public override float GetEquipmentHeatResistanceModifier()
		{
			return((viking != null && viking.OfAshlands()) ? 1f : base.GetEquipmentHeatResistanceModifier());
		}
	}
}
