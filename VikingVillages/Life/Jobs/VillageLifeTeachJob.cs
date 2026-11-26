using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Teaching a forsaken power at the gathering at dusk (see TeachJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Teaching a forsaken power at the village's gathering at dusk - walking up to one of the village who doesn't
		/// know a power the viking does and using it beside them, so they learn it, along with everyone else it reaches,
		/// while no other viking sets out to teach them (see VikingPowers.ClaimPupil)
		/// </summary>
		private class TeachJob : Job
		{
			private readonly Character pupil;
			private readonly int power;
			private float usedAt = -1f;
			private float nextRenew;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="learner">The viking it teaches</param>
			/// <param name="taught">The power it teaches, by index in VikingPowers.Names</param>
			public TeachJob(VillageLife owner, Character learner, int taught) : base(owner)
			{
				pupil = learner;
				power = taught;
			}

			/// <summary>
			/// Finds the viking it teaches, which it walks right up to rather than around
			/// </summary>
			/// <returns>The viking</returns>
			public override Character Focus()
			{
				return(pupil);
			}

			/// <summary>
			/// Notes the viking it teaches, so no other viking sets out to teach them too
			/// </summary>
			public override void Begin()
			{
				life.body.Powers.ClaimPupil(pupil);
				nextRenew = Time.time + VikingPowers.PupilClaimTime * 0.5f;
			}

			/// <summary>
			/// Walks up to the viking it teaches and uses the power beside them, waiting while another viking near is in
			/// the act of using it, then stands by them a moment - giving up once dusk is over, after TeachTimeout
			/// seconds, or when they've died or learned it meanwhile
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's done</returns>
			public override bool Tick(float dt)
			{
				if (usedAt >= 0f)
				{
					if (pupil != null)
					{
						life.Face(pupil.transform.position);
					}
					return(Time.time - usedAt >= TeachLinger);
				}
				if (pupil == null || pupil.IsDead() || !IsDusk() || Time.time - StartedAt > TeachTimeout || (VikingPowers.KnownBy(pupil) & (1 << power)) != 0)
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					life.body.Powers.ClaimPupil(pupil);
					nextRenew = Time.time + VikingPowers.PupilClaimTime * 0.5f;
				}
				if (Utils.DistanceXZ(pupil.transform.position, life.transform.position) > TeachReach)
				{
					life.Go(dt, pupil.transform.position, TeachReach, false);
					return(life.moveFailed);
				}
				life.ai.StopMoving();
				life.Face(pupil.transform.position);
				if (life.body.Powers.Use(power, pupil))
				{
					usedAt = Time.time;
					VikingPowers.LogTeaching(life.humanoid, pupil, power);
					return(false);
				}
				// Another viking near using the same power has it wait, while a cooldown that has started since ends it
				return(!life.body.Powers.Ready());
			}

			/// <summary>
			/// Lets go of the viking it was teaching
			/// </summary>
			public override void End()
			{
				life.body.Powers.ClaimPupil(null);
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>The power it teaches and to whom</returns>
			public override string Describe()
			{
				string name = ((pupil != null) ? VikingNames.Get(pupil) : "");
				return("Teach " + VikingPowers.Names[power].Substring(3) + " to " + ((name.Length > 0) ? name : "a viking"));
			}
		}
	}
}
