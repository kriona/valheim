using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Rebuilding a piece of the village that was destroyed (see RebuildJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Makes sure the viking carries enough wood to rebuild, getting more when it doesn't - from the village's chests
		/// first, then the ground
		/// </summary>
		/// <param name="needed">The wood the job takes, at most CarryMax</param>
		/// <param name="run">The job's wood run, started and finished here</param>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="done">Set to true when there's no wood left to get, so the job is over</param>
		/// <returns>True once it carries enough</returns>
		private bool HaveWood(int needed, ref WoodRun run, float dt, out bool done)
		{
			done = false;
			int enough = Mathf.Min(needed, CarryMax);
			if (CarriedWood() >= enough)
			{
				return(true);
			}
			if (run == null)
			{
				run = new WoodRun(this, enough, true);
			}
			if (run.Tick(dt))
			{
				run = null;
				return(false);
			}
			done = run.None;
			return(false);
		}

		/// <summary>
		/// Rebuilding a piece of the village that was destroyed - getting wood from the village's chests or the ground
		/// until it carries the piece's wood, then swinging at where it stood and putting it back
		/// </summary>
		private class RebuildJob : Job
		{
			private readonly PieceRecord record;
			private readonly bool isFire;
			private WoodRun run;
			private HammerWork work;

			/// <summary>
			/// Makes the job, with the piece already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="missing">The piece</param>
			/// <param name="fire">True when it's the village's bonfire or campfire, which is built back as the village's</param>
			public RebuildJob(VillageLife owner, PieceRecord missing, bool fire) : base(owner)
			{
				record = missing;
				isFire = fire;
				work = new HammerWork(owner, RebuildSwings);
			}

			/// <summary>
			/// Gathers the wood, then builds the piece
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's built, there's no more wood to gather, or it can't get there</returns>
			public override bool Tick(float dt)
			{
				if (!life.HaveWood(record.Wood, ref run, dt, out bool done))
				{
					return(done);
				}
				if (!work.Tick(dt, record.Position))
				{
					return(life.moveFailed);
				}
				bool built = (isFire ? life.village.RebuildFire(record) : VillageRecords.Rebuild(life.village, record));
				if (built)
				{
					life.UseWood(record.Wood);
				}
				return(true);
			}

			/// <summary>
			/// Checks whether the wood the viking is after lies outside the village
			/// </summary>
			/// <returns>True when it does</returns>
			public override bool Outing()
			{
				return(run != null && run.Outing());
			}

			/// <summary>
			/// Lets go of the piece and the wood it was after
			/// </summary>
			public override void End()
			{
				if (run != null)
				{
					run.Release();
				}
				life.ReleaseWork(RecordKey(record));
			}
		}
	}
}
