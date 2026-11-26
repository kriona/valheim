using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Clearing deep snow off the village's ground in the Deep North (see SnowGroundJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Clearing a spot of deep snow off the village's ground in the Deep North - walking to it and swinging at the
		/// ground, which clears it within VillagePaths.SnowReach the way the village's ground is cleared as it's set up
		/// </summary>
		private class SnowGroundJob : Job
		{
			private readonly Vector3 spot;
			private readonly HammerWork work;
			private bool cleared;

			/// <summary>
			/// Where it walks to reach the spot, and whether that's been picked
			/// </summary>
			private Vector3 stand;
			private bool hasStand;

			/// <summary>
			/// When it gives up on the spot if it hasn't got there
			/// </summary>
			private readonly float giveUpAt;

			/// <summary>
			/// Makes the job, with the spot already claimed (see VillageSnow.ClaimDeepSpot)
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="deep">The spot, on the ground</param>
			public SnowGroundJob(VillageLife owner, Vector3 deep) : base(owner)
			{
				spot = deep;
				work = new HammerWork(owner, RepairSwings);
				giveUpAt = Time.time + RepairGiveUpTime;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>The description</returns>
			public override string Describe()
			{
				return("Clear snow off the ground, " + Utils.DistanceXZ(spot, life.transform.position).ToString("F1") + "m off");
			}

			/// <summary>
			/// Walks to the spot, swings at the ground and clears its snow
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's cleared, or it can't get there</returns>
			public override bool Tick(float dt)
			{
				if (!work.Started && Time.time >= giveUpAt)
				{
					life.ai.StopMoving();
					return(true);
				}
				// Where it stands to reach the spot is picked once, so the way there doesn't change as it walks
				if (!hasStand)
				{
					stand = (NavmeshPoints.FindValidPoint(out Vector3 valid, spot, WorkReach, Pathfinding.AgentType.Humanoid) ? valid : spot);
					hasStand = true;
				}
				if (!work.Tick(dt, spot, stand))
				{
					if (life.moveFailed)
					{
						return(true);
					}
					return(false);
				}
				cleared = VillagePaths.ClearSnowAt(spot);
				return(true);
			}

			/// <summary>
			/// Lets go of the spot, taking it off the village's deep snow once it's cleared
			/// </summary>
			public override void End()
			{
				VillageSnow.ReleaseDeepSpot(life.village, spot, cleared);
			}
		}
	}
}
