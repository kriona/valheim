using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A raid on the village - noticing it, getting wary with weapons drawn, keeping guard, and saying farewell to
	/// fellows who leave
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Checks whether the viking is waiting for a corpse to rise, aiming at a bird or prey, or ready for a raid on its
		/// village, which keeps its weapon drawn
		/// </summary>
		/// <returns>True while it is</returns>
		public bool IsWary()
		{
			return(waitingFor != ZDOID.None || aimingBow || RaidReady());
		}

		/// <summary>
		/// Checks whether the viking is ready for a raid on its village - one is on, and it has noticed it, awake
		/// </summary>
		/// <returns>True while it is</returns>
		private bool RaidReady()
		{
			return(active && raidNoticed && !inBed && !dulled && VillageRaids.On(village));
		}

		/// <summary>
		/// Has the viking notice a raid on its village, once it's awake - dropping what it's doing unless it's something
		/// that can't wait (see RaidCritical), so it keeps guard instead (see ChooseJob), with the night watch roaring
		/// the alarm that wakes the village at night, and a viking calling it out by day, at most every
		/// RaidCallInterval for each village - and forget it once the raid is over
		/// </summary>
		private void UpdateRaid()
		{
			if (!VillageRaids.On(village))
			{
				raidNoticed = false;
				return;
			}
			// An NPC viking asleep or resting is woken by a raid, so it gets up and shelters inside (see StaysUp)
			if (!raidNoticed && Peaceful() && (inBed || dulled))
			{
				WakeForDanger("a raid");
			}
			if (raidNoticed || inBed || dulled)
			{
				return;
			}
			raidNoticed = true;
			string name = VillageNames.Get(village);
			if (Peaceful())
			{
				// An NPC viking cries out that it's hiding rather than calling the village to arms
				VikingSpeech.SayFirst(humanoid, NPCSpeech.Hide(name), VikingSpeech.CallTime, VikingSpeech.CallCull);
			}
			else if (IsWatch() && (IsNight() || IsDusk()))
			{
				lastAlarm = Time.time;
				ai.StopMoving();
				VikingPoses.Roar(humanoid);
				VikingSpeech.SayFirst(humanoid, VikingSpeech.RaidAlarm(name), VikingSpeech.AlarmTime, VikingSpeech.AlarmCull);
				VillageSignals.SendAlarm(village);
			}
			else if (!(lastRaidCall.TryGetValue(village.Zdo.m_uid, out float last) && Time.time - last < RaidCallInterval))
			{
				lastRaidCall[village.Zdo.m_uid] = Time.time;
				VikingSpeech.SayFirst(humanoid, VikingSpeech.RaidCall(name), VikingSpeech.CallTime, VikingSpeech.CallCull);
			}
			if (job != null && !RaidCritical())
			{
				Interrupt("a raid");
			}
		}

		/// <summary>
		/// Picks what the viking does through a raid on its village - keeping watch at night when it's the watch, taking
		/// done food off a cooking station, eating, or resting by the fire, while it's wounded, or else keeping guard. An
		/// NPC viking shelters instead (see ShelterRaidJob)
		/// </summary>
		/// <returns>The job</returns>
		private Job RaidJob()
		{
			if (Peaceful())
			{
				return(ShelterRaidJob());
			}
			if (IsNight() && IsWatch())
			{
				return(new WatchJob(this));
			}
			// The stations stand over the fire the village keeps guard around, so done food is taken off before it burns
			Job tend = FindTendWork(true, true);
			if (tend != null)
			{
				return(tend);
			}
			if (Wounded())
			{
				Container pantry = ((Time.time >= eatAgainAt) ? FindFoodChest(false) : null);
				if (pantry != null)
				{
					return(new EatJob(this, pantry, false));
				}
				if (!body.Rest.IsRested() && Time.time >= restAgainAt && village.GatherFires().Count > 0)
				{
					return(new GatherJob(this, true));
				}
			}
			return(new GuardJob(this));
		}

		/// <summary>
		/// Checks whether the viking's job is one it keeps at through a raid - keeping guard or watch, eating or resting
		/// by the fire while it's wounded, or lightening its load
		/// </summary>
		/// <returns>True when it is</returns>
		private bool RaidCritical()
		{
			return(job is GuardJob || job is WatchJob || job is EatJob || job is UnburdenJob || (job is GatherJob gather && gather.Heals()));
		}

		/// <summary>
		/// Has the viking wait for a corpse near it to rise as a zombie, rather than go about its day, when it has
		/// nothing to fight and isn't resting - it walks up to the corpse, stands a few meters off facing it, and keeps
		/// its weapon drawn, saying farewell when it's the corpse of one of its village (see SayFarewell)
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True while it waits, so its job is put off</returns>
		private bool UpdateWary(float dt)
		{
			// An NPC viking leaves a corpse about to rise to those who fight
			if ((job != null && job.Resting()) || Peaceful())
			{
				waitingFor = ZDOID.None;
				return(false);
			}
			if (!VillageHazards.NearestRising(transform.position, WaryRange, out VillageHazards.Rising corpse) || village.DistanceOutside(corpse.Position) > LeashRange)
			{
				waitingFor = ZDOID.None;
				return(false);
			}
			if (job != null || attachPoint != null)
			{
				Interrupt("a corpse about to rise");
			}
			waitingFor = corpse.Id;
			float distance = Utils.DistanceXZ(transform.position, corpse.Position);
			if (distance > WaitFar)
			{
				Go(dt, corpse.Position, (WaitNear + WaitFar) / 2f, false);
				moveFailed = false;
				return(true);
			}
			if (distance < WaitNear)
			{
				Vector3 away = transform.position - corpse.Position;
				away.y = 0f;
				mover = "backing away from a corpse";
				ai.MoveTowards(((away.sqrMagnitude < 0.01f) ? -transform.forward : away.normalized), false);
				return(true);
			}
			Face(corpse.Position);
			SayFarewell(corpse.Id);
			return(true);
		}

		/// <summary>
		/// Has the viking say farewell to one of its village whose corpse it stands ready by, about to rise - the first
		/// viking on this client to stand ready by it
		/// </summary>
		/// <param name="corpse">The corpse's ZDO</param>
		private void SayFarewell(ZDOID corpse)
		{
			if (farewelled.Contains(corpse))
			{
				return;
			}
			ZDO zdo = ((ZDOMan.instance != null) ? ZDOMan.instance.GetZDO(corpse) : null);
			if (!VillageFallen.IsOf(zdo, village))
			{
				return;
			}
			if (farewelled.Count >= CorpseMemory)
			{
				farewelled.Clear();
			}
			farewelled.Add(corpse);
			VikingSpeech.Chatter(humanoid, VikingSpeech.RisingFarewell(VikingNames.Get(zdo)), VikingSpeech.TalkTime);
		}
	}
}
