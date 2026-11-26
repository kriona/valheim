
namespace VikingVillages
{
	/// <summary>
	/// Taking down a sitting log left by the village's old fire (see TakeDownJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Taking down a sitting log left by the village's old fire - walking to it, swinging at it and taking it down,
		/// carrying its wood away to build it back around the new fire (see VillageSeats.TryMove)
		/// </summary>
		private class TakeDownJob : Job
		{
			private readonly ZNetView log;
			private readonly ZDOID id;
			private readonly HammerWork work;

			/// <summary>
			/// Makes the job, with the log already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="standing">The log</param>
			public TakeDownJob(VillageLife owner, ZNetView standing) : base(owner)
			{
				log = standing;
				id = standing.GetZDO().m_uid;
				work = new HammerWork(owner, RebuildSwings);
			}

			/// <summary>
			/// Walks to the log, swings at it and takes it down, keeping its wood
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's down, gone or the viking can't get to it</returns>
			public override bool Tick(float dt)
			{
				if (log == null || !log.IsValid())
				{
					return(true);
				}
				if (!work.Tick(dt, log.transform.position))
				{
					return(life.moveFailed);
				}
				int wood = VillageSeats.TakeDown(log);
				ItemDrop.ItemData plain = ItemOf("Wood");
				if (plain != null)
				{
					life.AddWood(plain, wood);
				}
				return(true);
			}

			/// <summary>
			/// Lets go of the log
			/// </summary>
			public override void End()
			{
				life.ReleaseWork(id);
			}
		}
	}
}
