using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The job a viking is doing with nothing to fight, which every job builds on (see Job)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Something the viking does with nothing to fight, one AI update at a time
		/// </summary>
		private abstract class Job
		{
			protected readonly VillageLife life;

			/// <summary>
			/// Makes the job for a viking
			/// </summary>
			/// <param name="owner">The viking's life</param>
			protected Job(VillageLife owner)
			{
				life = owner;
				StartedAt = Time.time;
			}

			/// <summary>
			/// When the job was made, for the debug hover text
			/// </summary>
			public readonly float StartedAt;

			/// <summary>
			/// Starts the job
			/// </summary>
			public virtual void Begin()
			{
			}

			/// <summary>
			/// Carries the job on
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's done</returns>
			public abstract bool Tick(float dt);

			/// <summary>
			/// Ends the job, whether it's done or was interrupted, letting go of what it claimed and ending its poses
			/// </summary>
			public virtual void End()
			{
			}

			/// <summary>
			/// Checks whether the viking is resting at night in this job, with its senses dulled
			/// </summary>
			/// <returns>True while it rests</returns>
			public virtual bool Resting()
			{
				return(false);
			}

			/// <summary>
			/// Checks whether the viking sits in this job, on the floor or a chair, which rests it by a fire
			/// </summary>
			/// <returns>True while it sits</returns>
			public virtual bool Sitting()
			{
				return(false);
			}

			/// <summary>
			/// Checks whether the job has the viking heading out of the village - for wood, food or loot outside it
			/// </summary>
			/// <returns>True while it does</returns>
			public virtual bool Outing()
			{
				return(false);
			}

			/// <summary>
			/// Finds the creature the job is about, which the viking walks right up to rather than around - the viking
			/// it's talking with, or the boar it's petting
			/// </summary>
			/// <returns>The creature, or null for a job that's about none</returns>
			public virtual Character Focus()
			{
				return(null);
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name, and how far along it is for a job that says</returns>
			public virtual string Describe()
			{
				string name = GetType().Name;
				return(name.EndsWith("Job") ? name.Substring(0, name.Length - 3) : name);
			}
		}
	}
}
