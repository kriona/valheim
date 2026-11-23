using Kriona.Vikings;

namespace ZombieApocalypse
{
	/// <summary>
	/// A zombie viking's AI, which fights like a monster's apart from what it shares with a living viking's, held still
	/// while it gets up and hunting players while it's in a raid
	/// </summary>
	internal class ZombieVikingAI : VikingAI
	{
		/// <summary>
		/// Checks whether the zombie is held still while it gets up from its corpse (see RiseBlend)
		/// </summary>
		/// <returns>True while it's held</returns>
		public override bool Held()
		{
			return(RiseBlend.IsHeld(this));
		}

		/// <summary>
		/// Ends the zombie's hunt for a raid once the raid is over, while the game's update is skipped (see RaidHunt)
		/// </summary>
		protected override void OnSkippedUpdate()
		{
			RaidHunt.Update(this);
		}
	}
}
