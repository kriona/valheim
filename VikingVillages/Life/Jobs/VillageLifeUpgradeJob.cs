using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Taking up better gear from the village's chests (see UpgradeJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Taking up better gear from one of the village's chests - walking to it, opening it and taking out armor, a
		/// shield or a weapon better than its own to wear or wield, carrying what it replaces to put back
		/// </summary>
		private class UpgradeJob : Job
		{
			private Container chest;
			private float actAt = -1f;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="armory">The chest, holding gear the viking would take up</param>
			public UpgradeJob(VillageLife owner, Container armory) : base(owner)
			{
				chest = armory;
			}

			/// <summary>
			/// Walks to the chest and takes up the gear in it - leaving a chest it can't get to alone
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's taken the gear up, the gear's gone, or it can't get to the chest</returns>
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
				life.TakeUpgrade(chest);
				return(true);
			}
		}
	}
}
