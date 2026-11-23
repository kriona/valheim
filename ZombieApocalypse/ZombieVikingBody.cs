namespace ZombieApocalypse
{
	/// <summary>
	/// A zombie viking's body, which works like a monster's apart from what it shares with a living viking
	/// </summary>
	internal class ZombieVikingBody : VikingBody
	{
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
