using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Eating from one of the village's chests (see EatJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Eating from a chest when wounded, or to keep its foods up while there's food enough to go around (see
		/// FoodToSpare) - walking to the chest it eats from (see FindFoodChest), opening it and eating the food there it
		/// eats now (see FoodIn)
		/// </summary>
		private class EatJob : Job
		{
			private Container chest;
			private float eatAt = -1f;
			private readonly bool topUp;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="pantry">The chest, with food in it the viking can eat</param>
			/// <param name="topUp">True when it's keeping its foods up rather than eating for its wounds</param>
			public EatJob(VillageLife owner, Container pantry, bool topUp) : base(owner)
			{
				chest = pantry;
				this.topUp = topUp;
			}

			/// <summary>
			/// Walks to the chest and eats - finding another when this one's food is gone or it can't be used, and
			/// leaving eating for EatRetryTime when there's none, or leaving a chest it can't get to alone
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has eaten, or there's no food it can eat</returns>
			public override bool Tick(float dt)
			{
				if (chest == null || !life.Usable(chest) || life.FoodIn(chest, topUp) == null)
				{
					chest = life.FindFoodChest(topUp);
					eatAt = -1f;
					if (chest == null)
					{
						life.eatAgainAt = Time.time + EatRetryTime;
						return(true);
					}
				}
				if (eatAt >= 0f)
				{
					if (Time.time < eatAt)
					{
						return(false);
					}
					eatAt = -1f;
					if (life.EatFrom(chest, topUp))
					{
						return(true);
					}
					chest = null;
					return(false);
				}
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						chest = null;
					}
					return(false);
				}
				life.Face(chest.transform.position);
				VikingPoses.Interact(life.humanoid);
				chest.m_openEffects.Create(chest.transform.position, chest.transform.rotation);
				eatAt = Time.time + ReachTime;
				return(false);
			}
		}
	}
}
