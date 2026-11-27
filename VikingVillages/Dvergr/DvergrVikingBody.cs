namespace VikingVillages
{
	/// <summary>
	/// A dvergr viking's body - built on a PC viking's, with the forsaken powers it knows and uses and its name as the local
	/// player knows it, but no village life to feed or rest it, so its health is its biome's and its stars', and no
	/// naming it or opening its inventory - only talking to it
	/// </summary>
	internal class DvergrVikingBody : PCVikingBody
	{
		/// <summary>
		/// The viking's life with the dvergr, found the first time it's needed
		/// </summary>
		private DvergrLife life;

		/// <summary>
		/// Keeps the viking from taking fall damage, the same as the dvergr it lives with
		/// </summary>
		/// <returns>False</returns>
		public override bool TakesFallDamage()
		{
			return(false);
		}

		/// <summary>
		/// Puts the viking's name and the key to talk to it ahead of the game's hover text, when the local player isn't
		/// its enemy
		/// </summary>
		/// <param name="text">The game's hover text</param>
		/// <returns>The hover text</returns>
		protected override string HoverText(string text)
		{
			if (life == null)
			{
				life = GetComponent<DvergrLife>();
			}
			if (life == null || !life.CanTalkTo(Player.m_localPlayer))
			{
				return(text);
			}
			string talk = GetHoverName() + "\n" + Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>] Talk");
			return(string.IsNullOrEmpty(text) ? talk : talk + "\n" + text);
		}
	}
}
