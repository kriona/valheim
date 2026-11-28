using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Talking with another viking (see TalkJob) - finding someone to talk to and the talk the two share
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Finds another viking of the village this client owns that's free to talk - strolling or standing about. In the
		/// rain it's one under a roof when there is one, or else one to bring inside its own house to talk (see
		/// TalkSession), and none when it has no house to talk in
		/// </summary>
		/// <returns>The viking, or null</returns>
		private VillageLife FindPartner()
		{
			List<VillageLife> free = FreePartners();
			if (free.Count == 0 || !VikingWeather.KeepsIndoors(transform.position))
			{
				return((free.Count > 0) ? free[Random.Range(0, free.Count)] : null);
			}
			List<VillageLife> sheltered = free.FindAll((VillageLife other) => VikingWeather.UnderRoof(other.humanoid.GetCenterPoint()));
			if (sheltered.Count > 0)
			{
				return(sheltered[Random.Range(0, sheltered.Count)]);
			}
			return(Home(out VillagePaths.House _) ? free[Random.Range(0, free.Count)] : null);
		}

		/// <summary>
		/// Lists the other vikings of the village this client owns that are free to talk - strolling or standing about,
		/// within TalkSearch
		/// </summary>
		/// <returns>The vikings</returns>
		private List<VillageLife> FreePartners()
		{
			List<VillageLife> free = new List<VillageLife>();
			foreach (VillageLife other in All)
			{
				if (other == this || !other.active || other.village != village || other.ai.m_targetCreature != null)
				{
					continue;
				}
				if (other.job != null && !(other.job is StrollJob) && !(other.job is IdleJob))
				{
					continue;
				}
				if (Vector3.Distance(other.transform.position, transform.position) <= TalkSearch)
				{
					free.Add(other);
				}
			}
			return(free);
		}

		/// <summary>
		/// The talk a viking sitting by the fire is having with a traveler that came up to it, staying in its seat (see
		/// TalkSession.Seated), or null
		/// </summary>
		private TalkSession seatedTalk;

		/// <summary>
		/// Finds another viking of the village this client owns sitting by the fire within TalkSearch, for a traveler to
		/// go and talk to - one not talking with it already, nor fighting
		/// </summary>
		/// <returns>The viking, or null</returns>
		private VillageLife FindSeatedPartner()
		{
			List<VillageLife> seated = new List<VillageLife>();
			foreach (VillageLife other in All)
			{
				if (other == this || !other.active || other.village != village || other.ai.m_targetCreature != null || other.SeatedTalker() != null)
				{
					continue;
				}
				if (other.job is GatherJob gather && gather.Sitting() && Vector3.Distance(other.transform.position, transform.position) <= TalkSearch)
				{
					seated.Add(other);
				}
			}
			return((seated.Count > 0) ? seated[Random.Range(0, seated.Count)] : null);
		}

		/// <summary>
		/// Finds the traveler talking with this viking while it sits by the fire
		/// </summary>
		/// <returns>The traveler, or null when there's none</returns>
		private VillageLife SeatedTalker()
		{
			return((seatedTalk != null && !seatedTalk.Over) ? seatedTalk.Leader : null);
		}

		/// <summary>
		/// Checks whether the viking is still sitting by the fire for a seated talk
		/// </summary>
		/// <param name="session">The talk</param>
		/// <returns>True while it's that talk's and it sits</returns>
		private bool SitsFor(TalkSession session)
		{
			return(seatedTalk == session && job is GatherJob gather && gather.Sitting());
		}

		/// <summary>
		/// Starts the viking talking with another that has come to talk to it
		/// </summary>
		/// <param name="session">The talk</param>
		private void JoinTalk(TalkSession session)
		{
			StopJob();
			job = new TalkJob(this, session);
			job.Begin();
		}

		/// <summary>
		/// Two vikings talking, which both of their jobs share - who's speaking, and when the talk ends
		/// </summary>
		private class TalkSession
		{
			public readonly VillageLife Leader;
			public readonly VillageLife Partner;
			public float Until;
			public VillageLife Speaker;
			public float NextTurn;
			public bool Over;

			/// <summary>
			/// How long they talk once they've met, in seconds
			/// </summary>
			public readonly float Duration;

			/// <summary>
			/// Whether the partner stays sitting by the fire through the talk, keeping its own job, while the leader - a
			/// traveler - comes up to it
			/// </summary>
			public readonly bool Seated;

			/// <summary>
			/// In the rain, when neither is under a roof, the spot inside the leader's house they talk at, whether there is
			/// one, and whether the leader has got there - the partner follows the leader in
			/// </summary>
			public readonly Vector3 Place;
			public readonly bool HasPlace;
			public bool AtPlace;

			/// <summary>
			/// What they say, a line a turn, how many lines have been said, and whether the first has
			/// </summary>
			public readonly string[] Lines;
			public int Said;
			public bool Started;

			/// <summary>
			/// Starts a talk, with the viking that started it speaking first, about what's going on around it - or with or
			/// about a traveler (see TravelerTalk), or now and then about one of the village's dead (see
			/// VillageFallen.Remember), a newcomer, or a quest the one who started
			/// it just came back from, that's out or that's waiting for the morning (see QuestTalk), or the village going
			/// by the name of either of them (see VillageNamesakeSpeech). In the rain, when the partner isn't under
			/// a roof and isn't staying in its seat, they talk inside the leader's house, clear of its doors (see FloorSpot),
			/// with TalkWalkTime more to get there
			/// </summary>
			/// <param name="leader">The viking that started it</param>
			/// <param name="partner">The viking it talks to</param>
			/// <param name="duration">How long they talk, in seconds</param>
			/// <param name="seated">Whether the partner stays sitting by the fire through it</param>
			public TalkSession(VillageLife leader, VillageLife partner, float duration, bool seated = false)
			{
				Leader = leader;
				Partner = partner;
				Duration = duration;
				Seated = seated;
				if (!seated && VikingWeather.KeepsIndoors(leader.transform.position) && !VikingWeather.UnderRoof(partner.humanoid.GetCenterPoint()) && leader.Home(out VillagePaths.House house))
				{
					Place = FloorSpot(house);
					HasPlace = true;
				}
				Until = Time.time + duration + (HasPlace ? TalkWalkTime : 0f);
				Speaker = leader;
				OtherName = NamedFellow(leader, partner);
				ItemName = leader.VillageMaterial();
				FallenName = VillageFallen.Remember(leader.village);
				VillageName = VillageNames.Get(leader.village);
				LinkedName = "";
				LinkedBiome = "";
				string[][] linkedTalks = null;
				if (VillagePortals.Linked(leader.village, out string linked, out Heightmap.Biome biome))
				{
					LinkedName = linked;
					LinkedBiome = VillagePortalSpeech.BiomeName(biome);
					linkedTalks = VillagePortalSpeech.Talks(biome);
				}
				// The talk is picked for both of them, so one with an NPC viking leaves out what only PC vikings talk about
				Character listener = VikingSpeech.Listener;
				VikingSpeech.Listener = partner.humanoid;
				try
				{
					string[] travelerTalk = TravelerTalk(leader, partner);
					NewcomerName = "";
					NewcomerFrom = "";
					string[] newcomerTalk = ((travelerTalk == null) ? NewcomerTalk(leader, partner, VillageName.Length > 0, out NewcomerName, out NewcomerFrom) : null);
					string[] questTalk = ((travelerTalk == null && newcomerTalk == null) ? QuestTalk(leader, partner) : null);
					string[] namesakeTalk = ((travelerTalk == null && newcomerTalk == null && questTalk == null) ? VillageNamesakeSpeech.Talk(VikingNames.Get(leader.humanoid), VikingNames.Get(partner.humanoid), VillageName) : null);
					Lines = (travelerTalk ?? newcomerTalk ?? questTalk ?? namesakeTalk ?? VikingSpeech.Talk(IsNight() || IsDusk(), EnvMan.IsWet(), Time.time - leader.lastFightAt < FightTalkTime, leader.Wounded(), OtherName.Length > 0, ItemName.Length > 0, FallenName.Length > 0, leader.body.HasBeard(), VillageName.Length > 0, linkedTalks));
				}
				finally
				{
					VikingSpeech.Listener = listener;
				}
			}

			/// <summary>
			/// Picks a talk about a viking new to the village, now and then, for a game day after it joined (see
			/// VillageNewcomers) - welcoming it when it's the one talked to, or about one of the newcomers with a name
			/// otherwise, mentioning the village its group came from when there's one. A newcomer doesn't start one
			/// </summary>
			/// <param name="leader">The viking that started the talk</param>
			/// <param name="partner">The viking it talks to</param>
			/// <param name="named">Whether the village has a name</param>
			/// <param name="newcomer">Set to the name of the newcomer talked about, or ""</param>
			/// <param name="from">Set to the village its group came from, or ""</param>
			/// <returns>The talk's lines, or null for a talk about anything else</returns>
			private static string[] NewcomerTalk(VillageLife leader, VillageLife partner, bool named, out string newcomer, out string from)
			{
				newcomer = "";
				from = "";
				if (leader.village == null || VillageNewcomers.IsNew(leader.nview.GetZDO()))
				{
					return(null);
				}
				ZDO partnerZdo = partner.nview.GetZDO();
				if (VillageNewcomers.IsNew(partnerZdo) && Random.value < VillageNewcomers.WelcomeChance)
				{
					from = VillageNewcomers.From(partnerZdo);
					return(PickTalk(VillageNewcomers.Candidates(VillageNewcomers.WelcomeTalks, VillageNewcomers.WelcomeFromTalks, from.Length > 0, named)));
				}
				List<Character> others = leader.village.Vikings().FindAll((Character other) => other != null && other != leader.humanoid && other != partner.humanoid && other.m_nview != null && other.m_nview.IsValid() && VillageNewcomers.IsNew(other.m_nview.GetZDO()) && VikingNames.Get(other).Length > 0);
				if (others.Count == 0 || Random.value >= VillageNewcomers.AboutChance)
				{
					return(null);
				}
				Character talkedAbout = others[Random.Range(0, others.Count)];
				newcomer = VikingNames.Get(talkedAbout);
				from = VillageNewcomers.From(talkedAbout.m_nview.GetZDO());
				return(PickTalk(VillageNewcomers.Candidates(VillageNewcomers.AboutTalks, VillageNewcomers.AboutFromTalks, from.Length > 0, named)));
			}

			/// <summary>
			/// Picks one of a list of talks at random
			/// </summary>
			/// <param name="talks">The talks</param>
			/// <returns>One of them, or null when there are none</returns>
			private static string[] PickTalk(List<string[]> talks)
			{
				return((talks.Count > 0) ? talks[Random.Range(0, talks.Count)] : null);
			}

			/// <summary>
			/// The name of another viking of the village they might talk about, of a material in the village's chests they
			/// might talk about, of one of the village's dead they talk about, of the village itself, and of the village on
			/// the other side of its portal and the biome that stands in, or ""
			/// </summary>
			public readonly string OtherName;
			public readonly string ItemName;
			public readonly string FallenName;
			public readonly string VillageName;
			public readonly string LinkedName;
			public readonly string LinkedBiome;

			/// <summary>
			/// The name of a viking new to the village they talk about, and of the village its group came from, or "" (see
			/// VillageNewcomers)
			/// </summary>
			public readonly string NewcomerName;
			public readonly string NewcomerFrom;

			/// <summary>
			/// Has the viking whose turn it is say the talk's next line, while it has lines left - calling the one it speaks
			/// to by name when it has one
			/// </summary>
			public void SpeakTurn()
			{
				if (Said < Lines.Length && Speaker != null)
				{
					string listener = VikingNames.Get(Other(Speaker).humanoid);
					VikingSpeech.Chatter(Speaker.humanoid, VikingSpeech.Address(Lines[Said], listener, OtherName, ItemName, FallenName, VillageName, LinkedName, LinkedBiome, NewcomerName, NewcomerFrom), VikingSpeech.TalkTime);
					Said++;
				}
			}

			/// <summary>
			/// Picks another loaded viking of the leader's village with a name, at random, to talk about
			/// </summary>
			/// <param name="leader">The viking that started the talk</param>
			/// <param name="partner">The viking it talks to</param>
			/// <returns>Its name, or "" when there's none</returns>
			private static string NamedFellow(VillageLife leader, VillageLife partner)
			{
				List<string> names = new List<string>();
				if (leader.village != null)
				{
					foreach (Character fellow in leader.village.Vikings())
					{
						string name = ((fellow != leader.humanoid && fellow != partner.humanoid) ? VikingNames.Get(fellow) : "");
						if (name.Length > 0)
						{
							names.Add(name);
						}
					}
				}
				return((names.Count > 0) ? names[Random.Range(0, names.Count)] : "");
			}

			/// <summary>
			/// Finds the other viking of the talk
			/// </summary>
			/// <param name="one">One of them</param>
			/// <returns>The other</returns>
			public VillageLife Other(VillageLife one)
			{
				return((one == Leader) ? Partner : Leader);
			}
		}

		/// <summary>
		/// Walking up to another viking and talking with it, taking turns to speak
		/// </summary>
		private class TalkJob : Job
		{
			private readonly TalkSession session;
			private VikingPoses.TalkMode mode = VikingPoses.TalkMode.None;
			private bool met;

			/// <summary>
			/// How the head of a partner staying in its seat moves as it talks, which this job sets for it
			/// </summary>
			private VikingPoses.TalkMode partnerMode = VikingPoses.TalkMode.None;

			/// <summary>
			/// Finds the viking it's talking with
			/// </summary>
			/// <returns>The other viking, or null when it's gone</returns>
			public override Character Focus()
			{
				VillageLife other = session.Other(life);
				return((other != null) ? other.humanoid : null);
			}

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="talk">The talk it's part of</param>
			public TalkJob(VillageLife owner, TalkSession talk) : base(owner)
			{
				session = talk;
			}

			/// <summary>
			/// Walks to the other viking, then faces it and speaks or listens in turn - in the rain, when they talk inside
			/// (see TalkSession), the leader walks to the spot in its house and waits there, and the other follows it in.
			/// A partner staying in its seat speaks its turns from there, and the talk ends once it gets up
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once the talk is over</returns>
			public override bool Tick(float dt)
			{
				VillageLife other = session.Other(life);
				if (session.Over || other == null || !other.active || Time.time >= session.Until)
				{
					return(true);
				}
				if (session.Seated ? !other.SitsFor(session) : (other.job == null || !(other.job is TalkJob)))
				{
					return(true);
				}
				float distance = Utils.DistanceXZ(other.transform.position, life.transform.position);
				if (!met)
				{
					if (session.HasPlace && !session.AtPlace)
					{
						if (session.Leader != life)
						{
							// It follows the leader in, rather than meeting it out in the rain
							if (distance > TalkDistance)
							{
								life.Go(dt, other.transform.position, TalkDistance, false);
								if (life.moveFailed)
								{
									return(true);
								}
							}
							else
							{
								life.ai.StopMoving();
							}
							return(false);
						}
						if (!life.Go(dt, session.Place, 0.75f, false))
						{
							return(false);
						}
						life.moveFailed = false;
						session.AtPlace = true;
					}
					if (distance > TalkDistance && session.HasPlace && session.Leader == life)
					{
						// It waits at its spot for the other to come in
						life.ai.StopMoving();
						life.Face(other.transform.position);
						return(false);
					}
					if (distance > TalkDistance)
					{
						life.Go(dt, other.transform.position, TalkDistance, false);
						if (life.moveFailed)
						{
							return(true);
						}
						return(false);
					}
					met = true;
					if (session.Leader == life)
					{
						session.NextTurn = Time.time + Random.Range(TurnTimeMin, TurnTimeMax);
					}
				}
				life.Face(other.transform.position);
				if (!session.Seated && !other.TalkMet())
				{
					SetMode(VikingPoses.TalkMode.None);
					return(false);
				}
				if (session.Leader == life && !session.Started)
				{
					session.Started = true;
					if (session.HasPlace)
					{
						// The walk inside doesn't count against the talk
						session.Until = Time.time + session.Duration;
					}
					session.NextTurn = Time.time + Random.Range(TurnTimeMin, TurnTimeMax);
					session.SpeakTurn();
				}
				if (session.Leader == life && Time.time >= session.NextTurn)
				{
					session.Speaker = session.Other(session.Speaker);
					session.NextTurn = Time.time + Random.Range(TurnTimeMin, TurnTimeMax);
					if (Random.value < GestureChance)
					{
						VikingPoses.Gesture(session.Speaker.humanoid);
					}
					session.SpeakTurn();
				}
				SetMode((session.Speaker == life) ? VikingPoses.TalkMode.Speaking : VikingPoses.TalkMode.Listening);
				if (session.Seated)
				{
					SetPartnerMode(other, (session.Speaker == other) ? VikingPoses.TalkMode.Speaking : VikingPoses.TalkMode.Listening);
				}
				return(false);
			}

			/// <summary>
			/// Sets how the head of a partner staying in its seat moves as it talks, when it has changed
			/// </summary>
			/// <param name="other">The partner</param>
			/// <param name="next">Speaking, listening or neither</param>
			private void SetPartnerMode(VillageLife other, VikingPoses.TalkMode next)
			{
				if (next != partnerMode)
				{
					partnerMode = next;
					VikingPoses.SetTalk(other.humanoid, partnerMode);
				}
			}

			/// <summary>
			/// Checks whether this viking has reached the other
			/// </summary>
			/// <returns>True once it has</returns>
			public bool Met()
			{
				return(met);
			}

			/// <summary>
			/// Sets how the viking's head moves as it talks, when it has changed
			/// </summary>
			/// <param name="next">Speaking, listening or neither</param>
			private void SetMode(VikingPoses.TalkMode next)
			{
				if (next != mode)
				{
					mode = next;
					VikingPoses.SetTalk(life.humanoid, mode);
				}
			}

			/// <summary>
			/// Ends the talk for both vikings - a partner staying in its seat stops talking there too
			/// </summary>
			public override void End()
			{
				session.Over = true;
				VikingPoses.SetTalk(life.humanoid, VikingPoses.TalkMode.None);
				VillageLife other = session.Other(life);
				if (session.Seated && other != null)
				{
					if (partnerMode != VikingPoses.TalkMode.None && other.humanoid != null)
					{
						VikingPoses.SetTalk(other.humanoid, VikingPoses.TalkMode.None);
					}
					if (other.seatedTalk == session)
					{
						other.seatedTalk = null;
					}
				}
			}
		}

		/// <summary>
		/// Checks whether the viking has reached the viking it's talking with
		/// </summary>
		/// <returns>True when it's talking and has</returns>
		private bool TalkMet()
		{
			TalkJob talk = job as TalkJob;
			return(talk != null && talk.Met());
		}
	}
}
