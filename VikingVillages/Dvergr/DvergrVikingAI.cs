using Kriona.Vikings;

namespace VikingVillages
{
	/// <summary>
	/// A dvergr viking's AI, which fights like a zombie viking's and wanders around its post like the dvergr - it stays
	/// angry at a player who hurt it or a dvergr near it, the same as they do - and turns the viking to a player it
	/// greets or answers (see DvergrLife)
	/// </summary>
	internal class DvergrVikingAI : VikingAI
	{
		/// <summary>
		/// The viking's life with the dvergr, found the first time it's needed
		/// </summary>
		private DvergrLife life;

		/// <summary>
		/// Has the viking face a player it greeted or answered while it has nothing to fight, in place of the game's
		/// update
		/// </summary>
		/// <param name="dt">Seconds since the last update</param>
		/// <returns>True to skip the game's update</returns>
		protected override bool TakeOver(float dt)
		{
			if (life == null)
			{
				life = GetComponent<DvergrLife>();
			}
			return(life != null && life.UpdateFacing());
		}
	}
}
