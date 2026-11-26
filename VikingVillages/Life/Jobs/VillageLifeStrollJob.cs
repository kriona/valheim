using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Strolling about the village (see StrollJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Strolling to a random spot in the village
		/// </summary>
		private class StrollJob : Job
		{
			private Vector3 target;
			private bool hasTarget;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			public StrollJob(VillageLife owner) : base(owner)
			{
			}

			/// <summary>
			/// Picks where it strolls to
			/// </summary>
			public override void Begin()
			{
				hasTarget = life.village.RandomOpenSpot(out target);
			}

			/// <summary>
			/// Walks there
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's there, or when it can't get there</returns>
			public override bool Tick(float dt)
			{
				return(!hasTarget || life.Go(dt, target, 1f, false));
			}
		}
	}
}
