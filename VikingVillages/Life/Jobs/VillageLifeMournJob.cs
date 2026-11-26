using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Mourning one of the village's vikings who died (see MournJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Finds a corpse of the viking's village for it to mourn, when it would drop what it's doing for that (see
		/// YieldsToMourning) - the nearest within MournRange that stays dead and that it hasn't mourned yet, with fewer
		/// than MournersMax mourning it and no enemy within MournClear of it
		/// </summary>
		/// <returns>The job, or null when there's none</returns>
		private Job FindMourning()
		{
			if (!YieldsToMourning())
			{
				return(null);
			}
			Ragdoll corpse = VillageFallen.NearestCorpse(village, transform.position, MournRange, (Ragdoll candidate) => mourned.Contains(candidate.m_nview.GetZDO().m_uid) || Mourners(candidate) >= MournersMax);
			if (corpse == null || Village.FindEnemyNear(corpse.GetAverageBodyPosition(), MournClear) != null)
			{
				return(null);
			}
			return(new MournJob(this, corpse));
		}

		/// <summary>
		/// Checks whether the viking drops its job to mourn one of its village - anything but mourning already, hunting,
		/// cooking or lightening its load, and never while it keeps watch, lies in bed or rests with its senses dulled
		/// </summary>
		/// <returns>True when it does</returns>
		private bool YieldsToMourning()
		{
			if (IsWatch() || inBed || dulled || (job != null && job.Resting()) || VillageRaids.On(village))
			{
				return(false);
			}
			return(!(job is MournJob || job is HuntJob || job is CookJob || job is UnburdenJob));
		}

		/// <summary>
		/// Counts the vikings on this client mourning a corpse
		/// </summary>
		/// <param name="corpse">The corpse</param>
		/// <returns>How many are</returns>
		private static int Mourners(Ragdoll corpse)
		{
			int count = 0;
			foreach (VillageLife life in All)
			{
				if (life.job is MournJob mourning && mourning.Corpse == corpse)
				{
					count++;
				}
			}
			return(count);
		}

		/// <summary>
		/// Walking over to the corpse of one of the village's vikings and mourning it - standing by it, facing it, with
		/// the player's emotes of grief now and then, and maybe a word of farewell
		/// </summary>
		private class MournJob : Job
		{
			/// <summary>
			/// The corpse it mourns
			/// </summary>
			public readonly Ragdoll Corpse;

			/// <summary>
			/// Whether the viking says a word of farewell, so the others mourning the corpse know one will
			/// </summary>
			public bool Speaks;

			/// <summary>
			/// Where by the corpse it stands, until when it stands there - or -1 until it's there - when it next grieves,
			/// and when it speaks, or -1 when it doesn't or already has
			/// </summary>
			private Vector3 spot;
			private float until = -1f;
			private float nextGrieve;
			private float speakAt = -1f;

			/// <summary>
			/// Where the corpse lay when the viking last saw it, so it can pick up the gear the dead dropped there once it
			/// has mourned, even when the corpse is gone by then
			/// </summary>
			private Vector3 lay;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="corpse">The corpse</param>
			public MournJob(VillageLife owner, Ragdoll corpse) : base(owner)
			{
				Corpse = corpse;
			}

			/// <summary>
			/// Notes the corpse as mourned, so the viking doesn't come back to it, and picks where it stands - MournDistance
			/// from the corpse, on the side it comes from, turned farther around it for each viking already mourning it
			/// </summary>
			public override void Begin()
			{
				if (life.mourned.Count >= CorpseMemory)
				{
					life.mourned.Clear();
				}
				life.mourned.Add(Corpse.m_nview.GetZDO().m_uid);
				Vector3 body = Corpse.GetAverageBodyPosition();
				Vector3 from = life.transform.position - body;
				from.y = 0f;
				float angle = ((from.sqrMagnitude < 0.01f) ? Random.Range(0f, 360f) : Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg);
				int index = Mathf.Max(0, Mourners(Corpse) - 1);
				angle += Fan(index, 360f / MournersMax);
				spot = body + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * MournDistance;
			}

			/// <summary>
			/// Walks to its spot by the corpse - or mourns from where it got to when it can't get there but is within
			/// WaitFar of the corpse - then faces the corpse, grieving every so often and saying its farewell once. Once
			/// it has mourned, it goes on to pick up the gear the dead dropped, the way it does what an enemy it killed
			/// dropped (see FindKillLoot)
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has mourned long enough, or the corpse is gone or out of reach</returns>
			public override bool Tick(float dt)
			{
				if (Corpse == null || Corpse.m_nview == null || !Corpse.m_nview.IsValid())
				{
					if (until >= 0f)
					{
						Mourned();
					}
					return(true);
				}
				Vector3 body = Corpse.GetAverageBodyPosition();
				lay = body;
				if (until < 0f)
				{
					if (Utils.DistanceXZ(life.transform.position, spot) > 0.5f && Utils.DistanceXZ(life.transform.position, body) > MournDistance + 0.5f)
					{
						life.Go(dt, spot, 0.5f, false);
						if (!life.moveFailed)
						{
							return(false);
						}
						if (Utils.DistanceXZ(life.transform.position, body) > WaitFar)
						{
							return(true);
						}
						life.moveFailed = false;
					}
					until = Time.time + Random.Range(MournTimeMin, MournTimeMax);
					nextGrieve = Time.time + Random.Range(0.5f, 1.5f);
					if (!OtherSpeaks() || Random.value < MournSpeakChance)
					{
						Speaks = true;
						speakAt = Time.time + Random.Range(MournSpeakDelayMin, MournSpeakDelayMax);
					}
				}
				life.Face(body);
				if (Time.time >= nextGrieve)
				{
					VikingPoses.Grieve(life.humanoid);
					nextGrieve = Time.time + Random.Range(GrieveIntervalMin, GrieveIntervalMax);
				}
				if (speakAt >= 0f && Time.time >= speakAt)
				{
					speakAt = -1f;
					VikingSpeech.Chatter(life.humanoid, VikingSpeech.Mourn(VikingNames.Get(Corpse.m_nview.GetZDO())), VikingSpeech.TalkTime);
				}
				if (Time.time < until)
				{
					return(false);
				}
				Mourned();
				return(true);
			}

			/// <summary>
			/// Notes where the corpse lay as the spot of a kill, so the viking picks up what lies around it next (see
			/// PicksUpKillLoot)
			/// </summary>
			private void Mourned()
			{
				life.killSpot = lay;
				life.killAt = Time.time;
			}

			/// <summary>
			/// Describes the job for the debug hover text - walking to the corpse, or standing by it and how long it stays
			/// </summary>
			/// <returns>The description</returns>
			public override string Describe()
			{
				if (Corpse == null)
				{
					return("Mourn, the corpse is gone");
				}
				return((until < 0f) ? "Mourn, walking to the corpse, " + Utils.DistanceXZ(life.transform.position, spot).ToString("F1") + "m off" : "Mourn by the corpse, " + Mathf.Max(0f, until - Time.time).ToString("F0") + "s left");
			}

			/// <summary>
			/// Checks whether another viking on this client mourning the same corpse says a word of farewell
			/// </summary>
			/// <returns>True when one does</returns>
			private bool OtherSpeaks()
			{
				foreach (VillageLife other in All)
				{
					if (other != life && other.job is MournJob mourning && mourning.Corpse == Corpse && mourning.Speaks)
					{
						return(true);
					}
				}
				return(false);
			}
		}
	}
}
