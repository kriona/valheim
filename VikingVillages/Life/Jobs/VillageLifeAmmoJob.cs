using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Filling an archer's quiver from the village's chests (see AmmoJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// An archer filling its quiver from one of the village's chests - walking to it, opening it, taking the best
		/// arrows for its bow and putting back its own that don't make the cut (see TakeArrows) - or restocking its
		/// silver arrows from it (see TakeSilverArrows)
		/// </summary>
		private class AmmoJob : Job
		{
			private readonly Container chest;
			private readonly bool silver;
			private float actAt = -1f;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="store">The chest, holding arrows for its bow</param>
			/// <param name="silverArrows">True to restock its silver arrows, false to fill its quiver</param>
			public AmmoJob(VillageLife owner, Container store, bool silverArrows = false) : base(owner)
			{
				chest = store;
				silver = silverArrows;
			}

			/// <summary>
			/// Walks to the chest and fills its quiver from it - leaving a chest it can't get to, or that has nothing for
			/// it after all, alone for a while
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's done, or it can't get to the chest</returns>
			public override bool Tick(float dt)
			{
				if (chest == null || !life.Usable(chest))
				{
					return(true);
				}
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						return(true);
					}
					return(false);
				}
				if (actAt < 0f)
				{
					life.Face(chest.transform.position);
					VikingPoses.Interact(life.humanoid);
					life.ShowChestOpen(chest);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (Time.time < actAt)
				{
					return(false);
				}
				if (!(silver ? life.TakeSilverArrows(chest) : life.TakeArrows(chest)))
				{
					life.Avoid(chest.m_nview);
				}
				return(true);
			}
		}
	}
}
