using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Hunting by day, alone or in a party (see HuntJob and HuntParty)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// A party of vikings from the Plains on hunting together (see GroupHunts) - its leader, everyone in it, the
		/// leader among them, the prey they're after, and how far along they are: gathering by the leader until every one
		/// of them is there or the time's up, then following the leader out until it's within PartyEngage of the prey,
		/// then all going for it at once
		/// </summary>
		/// <remarks>
		/// In the fight whichever of them the prey is after keeps it busy, kiting it just out of its reach, while the rest
		/// fight it (see Kites) - so when it turns on another of them, that one takes over the kiting. Everyone in a
		/// party is run by the client that owns its leader, since a party only takes vikings that client owns
		/// </remarks>
		private class HuntParty
		{
			public VillageLife Leader;
			public readonly List<VillageLife> Members = new List<VillageLife>();
			public Character Prey;
			public bool Setting;
			public bool Fighting;
			public readonly float GatherUntil;

			/// <summary>
			/// Makes a party with its leader in it
			/// </summary>
			/// <param name="leader">The viking leading it</param>
			/// <param name="prey">The prey it's after</param>
			/// <param name="gatherUntil">Until when the leader waits for the rest to gather, in Time.time seconds</param>
			public HuntParty(VillageLife leader, Character prey, float gatherUntil)
			{
				Leader = leader;
				Members.Add(leader);
				Prey = prey;
				GatherUntil = gatherUntil;
			}

			/// <summary>
			/// Drops whoever is no longer hunting with the party, and has another of them lead it when its leader has
			/// dropped out
			/// </summary>
			public void Prune()
			{
				Members.RemoveAll((VillageLife member) => member == null || !(member.job is HuntJob hunt) || !hunt.InParty(this));
				if (!Members.Contains(Leader) && Members.Count > 0)
				{
					Leader = Members[0];
				}
			}
		}

		/// <summary>
		/// Hunting by day - going out for the nearest prey within HuntReach of the village, from its biome's prey and
		/// those before it and right after it (see HuntPrey) - a boar, neck or greyling for a viking of the Meadows, and
		/// for an archer a deer too - or only prey and birds that drop something it's short of to craft with (see
		/// GatherFor), fighting it and the greylings it meets, and gathering what drops from the kill and from the
		/// zombie it rises as, to cook or put away back home
		/// </summary>
		/// <remarks>
		/// An archer hunting alone shoots wild prey from afar itself (see Snipe), and otherwise its AI does the fighting,
		/// with the prey treated as an enemy (see Hunts). From the Plains on, wild prey that isn't only for archers is
		/// hunted by a party, each of them with a job of its own (see HuntParty and PartyHunt). When any other enemy
		/// comes near or goes for it, or it's below HuntRetreatShare of its health, it gives up and runs home (see
		/// UpdateFlee)
		/// </remarks>
		private class HuntJob : Job
		{
			private readonly bool archer;
			private readonly string wanted;
			private readonly bool livestock;

			/// <summary>
			/// Whether it only hunts wild prey that drops food, for a village short of it (see FindFoodWork)
			/// </summary>
			private readonly bool foodOnly;
			private Character prey;
			private Vector3 kill;
			private bool engaged;
			private bool collecting;

			/// <summary>
			/// Whether an archer's AI has taken over the fight with its prey from shooting it from afar (see Snipe), and
			/// how many arrows it has loosed at it from afar
			/// </summary>
			private bool handedOver;
			private int snipeShots;

			/// <summary>
			/// Whether the viking stalked its prey this update, where it's stalking toward, the prey it worked that out for
			/// and when it works it out again (see Stalk)
			/// </summary>
			private bool stalked;
			private Vector3 stalkSpot;
			private Character stalkPrey;
			private float nextStalkPlan;
			private float collectUntil;
			private float giveUpAt;
			private ZNetView drop;
			private ZDOID dropId;
			private float actAt = -1f;
			private float nextRenew;

			/// <summary>
			/// Whether the livestock it's slaughtering stands in a pen the viking isn't in, and when it next looks
			/// </summary>
			private bool penApart;
			private float nextPenCheck;

			/// <summary>
			/// The bird an archer is shooting for its feathers, where it last saw it, when it took off, or below 0 while
			/// it's landed, and how many arrows it has loosed at it and when it can loose the next
			/// </summary>
			private RandomFlyingBird bird;
			private ZDOID birdId;
			private Vector3 birdLast;
			private float birdFlyingSince;
			private int shots;
			private float nextShot;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="isArcher">Whether the viking is an archer, which also hunts the prey only archers hunt and
			/// shoots birds</param>
			/// <param name="item">The prefab name of what it hunts for something to craft with, so only prey or birds that
			/// drop it are hunted (see GatherFor), or null for any prey</param>
			/// <param name="slaughter">True to take one of the village's livestock instead of wild prey</param>
			/// <param name="hunters">The hunting party the viking hunts with, or null to hunt alone</param>
			/// <param name="forFood">True to only hunt wild prey that drops food, leaving birds alone</param>
			public HuntJob(VillageLife owner, bool isArcher, string item, bool slaughter, HuntParty hunters = null, bool forFood = false) : base(owner)
			{
				archer = isArcher;
				wanted = item;
				livestock = slaughter;
				foodOnly = forFood;
				party = hunters;
				prey = ((hunters != null) ? hunters.Prey : null);
				NoteFood(prey);
			}

			/// <summary>
			/// Notes that the viking found food around the village when the wild prey it goes for drops some (see
			/// VillageLife.NoteFoodFound) - not livestock, or prey that drops none, like a greyling
			/// </summary>
			/// <param name="target">The prey, or null</param>
			private void NoteFood(Character target)
			{
				if (!livestock && target != null && QuestLoot.DropsFood(Utils.GetPrefabName(target.gameObject)))
				{
					life.NoteFoodFound();
				}
			}

			/// <summary>
			/// The hunting party the viking hunts with, or null when it hunts alone
			/// </summary>
			private readonly HuntParty party;

			/// <summary>
			/// Checks whether the viking hunts with a party
			/// </summary>
			/// <param name="hunters">The party</param>
			/// <returns>True when it does</returns>
			public bool InParty(HuntParty hunters)
			{
				return(party != null && party == hunters);
			}

			/// <summary>
			/// Checks whether the viking is fighting a creature as the prey of its hunting party
			/// </summary>
			/// <param name="creature">The creature</param>
			/// <returns>True when it is</returns>
			public bool PartyFights(Character creature)
			{
				return(party != null && party.Fighting && creature != null && creature == party.Prey);
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name, and what it's out for when it's after something to craft with or only food</returns>
			public override string Describe()
			{
				string name = (livestock ? "Slaughter" : ((party != null) ? "Group hunt" : "Hunt"));
				if (wanted != null)
				{
					return(name + " (" + wanted + ")");
				}
				return(foodOnly ? name + " (food)" : name);
			}

			/// <summary>
			/// Notes that the viking is hunting, so no other viking of the village starts to
			/// </summary>
			public override void Begin()
			{
				life.SetDoing(HuntingKey, true);
				nextRenew = Time.time + ClaimRenew;
				giveUpAt = Time.time + HuntTime;
			}

			/// <summary>
			/// Checks whether the hunter fights a creature rather than running from it - its prey, a greyling, or the
			/// zombie its kill rises as
			/// </summary>
			/// <param name="creature">The creature</param>
			/// <returns>True when it does</returns>
			public bool Allows(Character creature)
			{
				if (creature == null)
				{
					return(false);
				}
				if (creature == prey || IsHuntedMonster(creature))
				{
					return(true);
				}
				return((engaged || collecting) && ZombieState.IsZombie(creature) && Utils.DistanceXZ(creature.transform.position, kill) <= CollectRadius * 2f);
			}

			/// <summary>
			/// Checks whether the viking is shooting a creature from afar as its prey (see Snipe), which its AI leaves to
			/// it until it hands the fight over
			/// </summary>
			/// <param name="creature">The creature</param>
			/// <returns>True when it is</returns>
			public bool Snipes(Character creature)
			{
				return(creature != null && creature == prey && archer && !livestock && party == null && !handedOver && life.ai.m_targetCreature != creature);
			}

			/// <summary>
			/// Checks whether the viking is slaughtering one of the village's livestock rather than hunting wild prey
			/// </summary>
			/// <returns>True when it is</returns>
			public bool Slaughters()
			{
				return(livestock);
			}

			/// <summary>
			/// Checks whether a creature is the prey the viking is after
			/// </summary>
			/// <param name="creature">The creature</param>
			/// <returns>True when it is</returns>
			public bool IsPreyOf(Character creature)
			{
				return(creature != null && creature == prey);
			}

			/// <summary>
			/// Gives up on the prey, leaving it alone for a while (see LetGo) - when a fence keeps the viking from it, say
			/// </summary>
			public void GiveUp()
			{
				LetGo();
			}

			/// <summary>
			/// Checks whether the hunt has the viking out of the village
			/// </summary>
			/// <returns>True when its prey or kill is outside it</returns>
			public override bool Outing()
			{
				return((prey != null && !life.village.Contains(prey.transform.position)) || (bird != null && !life.village.Contains(bird.transform.position)) || ((engaged || collecting) && !life.village.Contains(kill)));
			}

			/// <summary>
			/// Finds prey, stalks it and goes for it, then gathers what drops from the kill - crouched only while it stalks,
			/// so it stands up once it's shooting, fighting or done
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's gathered what dropped, there's no prey, it's hunted for HuntTime, or it's dusk</returns>
			public override bool Tick(float dt)
			{
				stalked = false;
				try
				{
					return(Hunt(dt));
				}
				finally
				{
					life.body.SetStalking(stalked);
				}
			}

			/// <summary>
			/// Finds prey, closes in on it and goes for it, then gathers what drops from the kill
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's gathered what dropped, there's no prey, it's hunted for HuntTime, or it's dusk</returns>
			private bool Hunt(float dt)
			{
				if (IsNight() || IsDusk())
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					life.SetDoing(HuntingKey, true);
					nextRenew = Time.time + ClaimRenew;
				}
				if (collecting)
				{
					return(Collect(dt));
				}
				if (Time.time >= giveUpAt)
				{
					return(true);
				}
				Vector3 position = life.transform.position;
				if (!life.village.Contains(position) && life.EnemyNear(position, ForageSafety, (Character other) => Allows(other) || IgnoredByHunter(prey, other)))
				{
					life.Flee(FleeReason.Forage);
					return(true);
				}
				if (birdId != ZDOID.None)
				{
					return(ShootBird(dt));
				}
				if (party != null)
				{
					return(PartyHunt(dt, position));
				}
				if (prey == null || prey.IsDead())
				{
					if (engaged)
					{
						collecting = true;
						collectUntil = Time.time + CollectTime;
						hunts.Remove(life.humanoid);
						return(false);
					}
					prey = life.FindPrey(archer, wanted, livestock, false, foodOnly);
					// An archer goes for a bird when it's nearer than any prey, when it drops what it's after, unless it's
					// slaughtering livestock or only after food, which birds don't drop
					RandomFlyingBird nearBird = ((archer && !livestock && !foodOnly) ? life.FindBird(wanted) : null);
					if (nearBird != null && (prey == null || Vector3.Distance(nearBird.transform.position, position) < Vector3.Distance(prey.transform.position, position)))
					{
						prey = null;
						StartBird(nearBird);
						return(false);
					}
					if (prey == null)
					{
						return(true);
					}
					NoteFood(prey);
					hunts[life.humanoid] = prey;
					handedOver = false;
					snipeShots = 0;
					life.UpdateOuting(Outing());
				}
				kill = prey.transform.position;
				// Prey that runs off too far, or onto ground harder than the hunter's own biome, is let go
				if (life.village.DistanceOutside(kill) > HuntReach + HuntChase || (!livestock && life.TooHardToHunt(kill)))
				{
					LetGo();
					return(false);
				}
				// Livestock in a pen is slaughtered inside it, never through its fence, so the viking gets into the pen first.
				// A spot just outside a pen can count as fenced in too, so a piece between them means it's outside
				if (livestock && Time.time >= nextPenCheck)
				{
					nextPenCheck = Time.time + SlaughterPenCheck;
					penApart = (Village.FencedIn(kill) && (!Village.FencedIn(position) || PieceAcross(position, kill)));
				}
				if (livestock && penApart)
				{
					life.mover = "getting into the pen";
					life.Go(dt, kill, SlaughterReach, true);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						LetGo();
					}
					return(false);
				}
				// An archer shoots wild prey from afar until it comes close, or it's missed it too often
				if (archer && !livestock && party == null && !handedOver)
				{
					return(Snipe(dt, position));
				}
				float range = (archer ? ArcherEngage : MeleeEngage);
				if (Utils.DistanceXZ(kill, position) > range)
				{
					// Wild prey is stalked, and livestock in its pen just walked up to
					if (livestock)
					{
						life.Go(dt, kill, range * 0.8f, true);
					}
					else
					{
						Stalk(dt, position, range * 0.8f);
					}
					if (life.moveFailed)
					{
						life.moveFailed = false;
						LetGo();
					}
					return(false);
				}
				// Its AI takes it from here, and its job carries on once the fight is over
				engaged = true;
				life.ai.m_targetCreature = prey;
				life.ai.SetAlerted(true);
				return(false);
			}

			/// <summary>
			/// Hunts with the party (see HuntParty) - gathering by the leader, following it out while it stalks the prey,
			/// then going for the prey along with the rest once the leader is within PartyEngage of it, which the AI takes
			/// from there. Once the prey is down the leader gathers what dropped and the rest go back to their day. A party
			/// that's lost too many before the fight starts, or whose prey got away or out of reach, breaks up
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="position">Where the viking stands</param>
			/// <returns>True once the viking's part in the hunt is over</returns>
			private bool PartyHunt(float dt, Vector3 position)
			{
				party.Prune();
				Character target = party.Prey;
				if (target == null || target.IsDead())
				{
					// The kill is where the prey was last seen
					if (engaged && life == party.Leader)
					{
						collecting = true;
						collectUntil = Time.time + CollectTime;
						hunts.Remove(life.humanoid);
						return(false);
					}
					return(true);
				}
				if (!party.Fighting && party.Members.Count < PartyMin)
				{
					return(true);
				}
				if (prey != target)
				{
					NoteFood(target);
				}
				prey = target;
				kill = prey.transform.position;
				hunts[life.humanoid] = prey;
				if (life.village.DistanceOutside(kill) > HuntReach + HuntChase)
				{
					party.Prey = null;
					LetGo();
					return(true);
				}
				if (party.Fighting)
				{
					engaged = true;
					life.ai.m_targetCreature = prey;
					life.ai.SetAlerted(true);
					return(false);
				}
				VillageLife leader = party.Leader;
				if (!party.Setting)
				{
					bool gathered = party.Members.TrueForAll((VillageLife member) => Utils.DistanceXZ(member.transform.position, leader.transform.position) <= PartyGatherRange);
					if (gathered || Time.time >= party.GatherUntil)
					{
						party.Setting = true;
					}
					else if (life != leader)
					{
						life.mover = "joining the hunt";
						life.Go(dt, leader.transform.position, PartyGatherRange / 2f, true);
						return(false);
					}
					else
					{
						life.mover = "gathering the hunt";
						life.ai.StopMoving();
						return(false);
					}
				}
				if (life == leader)
				{
					if (Utils.DistanceXZ(kill, position) <= PartyEngage)
					{
						party.Fighting = true;
						return(false);
					}
					life.mover = "leading the hunt after " + prey.GetHoverName();
					Stalk(dt, position, PartyEngage * 0.8f);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						party.Prey = null;
						LetGo();
						return(true);
					}
					return(false);
				}
				life.mover = "following the hunt";
				float behind = Utils.DistanceXZ(leader.transform.position, position);
				if (behind > PartyFollow)
				{
					life.Go(dt, leader.transform.position, PartyFollow / 2f, behind > PartyGatherRange * 2f);
				}
				else
				{
					life.ai.StopMoving();
				}
				return(false);
			}

			/// <summary>
			/// Shoots the prey from afar - closing in to within its bow's range of it (see VillageLife.SnipeRange), and
			/// nearer while something stands
			/// between them, then standing with its bow drawn, aiming ahead of it and above it for the arrow's drop the
			/// way it aims at a bird, and loosing an arrow every BirdShotInterval seconds. Once the prey comes within
			/// SnipeHandOver, or it has loosed SnipeShotsMax arrows, its AI takes the fight over (see HandOver)
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="position">Where the viking stands</param>
			/// <returns>False, so the hunt goes on</returns>
			private bool Snipe(float dt, Vector3 position)
			{
				float range = life.SnipeRange();
				float distance = Utils.DistanceXZ(kill, position);
				if (distance <= SnipeHandOver || snipeShots >= SnipeShotsMax || life.ai.m_targetCreature == prey)
				{
					HandOver();
					return(false);
				}
				Vector3 center = prey.GetCenterPoint();
				if (distance > range || !ClearShot(center))
				{
					life.aimingBow = false;
					life.mover = "stalking " + prey.GetHoverName();
					Stalk(dt, position, ((distance > range) ? range * 0.8f : SnipeHandOver));
					if (life.moveFailed)
					{
						life.moveFailed = false;
						LetGo();
					}
					return(false);
				}
				life.mover = "shooting at " + prey.GetHoverName();
				life.aimingBow = true;
				life.ai.StopMoving();
				Vector3 aim = life.BirdAim(center, prey.GetVelocity());
				life.ai.LookAt(aim);
				if (Time.time >= nextShot && life.ReadyToShoot(aim) && life.humanoid.StartAttack(null, false))
				{
					snipeShots++;
					nextShot = Time.time + BirdShotInterval;
					engaged = true;
				}
				return(false);
			}

			/// <summary>
			/// Closes in on the prey - hurrying while it's farther off than StalkRange, and from there stalking it, crouched
			/// and quiet at a walk (see LivingVikingBody.SetStalking), toward a spot standOff from it. The spot is off the
			/// prey's back when the viking can get there by swinging no more than StalkSwingMax around it from the way it's
			/// coming, on ground it can walk to - and straight from the way it's coming otherwise, rather than circling all
			/// the way around. Worked out again every StalkReplan seconds as the prey moves and turns
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="position">Where the viking stands</param>
			/// <param name="standOff">How near the prey it goes</param>
			private void Stalk(float dt, Vector3 position, float standOff)
			{
				if (Utils.DistanceXZ(kill, position) > StalkRange)
				{
					life.Go(dt, kill, standOff, true);
					return;
				}
				stalked = true;
				if (Time.time >= nextStalkPlan || stalkPrey != prey)
				{
					nextStalkPlan = Time.time + StalkReplan;
					stalkPrey = prey;
					stalkSpot = StalkSpot(position, standOff);
				}
				life.Go(dt, stalkSpot, StalkSpotReach, false);
			}

			/// <summary>
			/// Works out where the viking stalks toward (see Stalk)
			/// </summary>
			/// <param name="position">Where the viking stands</param>
			/// <param name="standOff">How far from the prey the spot is</param>
			/// <returns>The spot</returns>
			private Vector3 StalkSpot(Vector3 position, float standOff)
			{
				Vector3 from = position - kill;
				from.y = 0f;
				if (from.sqrMagnitude < 0.01f)
				{
					return(kill);
				}
				from.Normalize();
				Vector3 straight = kill + from * standOff;
				Vector3 behind = -prey.transform.forward;
				behind.y = 0f;
				if (behind.sqrMagnitude < 0.01f)
				{
					return(straight);
				}
				// How far around the prey it would have to go to come at its back, kept to StalkSwingMax so it never circles
				// all the way around to get there
				float around = Vector3.SignedAngle(from, behind.normalized, Vector3.up);
				float swing = StalkSwing(around);
				if (swing == 0f)
				{
					return(straight);
				}
				Vector3 spot = kill + Quaternion.Euler(0f, swing, 0f) * from * standOff;
				// Only where the ground lets it - a spot it can stand on and walk to
				if (!NavmeshPoints.FindValidPoint(out Vector3 valid, spot, StalkSpotReach, Pathfinding.AgentType.Humanoid) || !life.ai.HavePath(valid))
				{
					return(straight);
				}
				return(valid);
			}

			/// <summary>
			/// Checks whether nothing stands between the viking's eye and a point on the prey
			/// </summary>
			/// <param name="point">The point</param>
			/// <returns>True when the way is clear</returns>
			private bool ClearShot(Vector3 point)
			{
				Vector3 eye = life.humanoid.m_eye.position;
				return(!Physics.Linecast(eye, point, BaseAI.m_viewBlockMask, QueryTriggerInteraction.Ignore));
			}

			/// <summary>
			/// Hands the fight with the prey over to the viking's AI, which closes in and fights it with whatever suits
			/// the fight - for prey that came close, or that the viking kept missing
			/// </summary>
			private void HandOver()
			{
				handedOver = true;
				engaged = true;
				life.aimingBow = false;
				life.ai.m_targetCreature = prey;
				life.ai.SetAlerted(true);
			}

			/// <summary>
			/// Sets out after a bird, claiming it so no other archer goes for it
			/// </summary>
			/// <param name="target">The bird</param>
			private void StartBird(RandomFlyingBird target)
			{
				bird = target;
				birdId = target.m_nview.GetZDO().m_uid;
				birdLast = target.transform.position;
				birdFlyingSince = -1f;
				shots = 0;
				nextShot = 0f;
				life.ClaimWork(birdId);
				life.UpdateOuting(Outing());
			}

			/// <summary>
			/// Closes in on the bird to within BirdRange, then stands with its bow drawn and looses an arrow at it every
			/// BirdShotInterval seconds while it's landed - never while it's in the air, when it waits for it to land again,
			/// following it, for up to BirdLandWait seconds - giving up after BirdShotsMax, when it flies too far out or
			/// doesn't land again in time, and once it's down, gathers its feathers where it fell
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>False, so the hunt goes on</returns>
			private bool ShootBird(float dt)
			{
				// A bird struck by an arrow breaks apart into its feathers
				if (bird == null)
				{
					bool hit = (shots > 0);
					ReleaseBird();
					if (hit)
					{
						kill = birdLast;
						kill.y = ZoneSystem.instance.GetGroundHeight(kill);
						engaged = true;
						collecting = true;
						collectUntil = Time.time + CollectTime;
					}
					return(false);
				}
				Vector3 position = bird.transform.position;
				birdLast = position;
				if (shots >= BirdShotsMax || life.village.DistanceOutside(position) > HuntReach + HuntChase || life.TooHardToHunt(position))
				{
					LetGoBird();
					return(false);
				}
				bool landed = Landed(bird);
				if (landed)
				{
					birdFlyingSince = -1f;
				}
				else if (birdFlyingSince < 0f)
				{
					birdFlyingSince = Time.time;
				}
				else if (Time.time - birdFlyingSince > BirdLandWait)
				{
					LetGoBird();
					return(false);
				}
				if (Utils.DistanceXZ(position, life.transform.position) > BirdRange)
				{
					life.aimingBow = false;
					Vector3 under = position;
					under.y = ZoneSystem.instance.GetGroundHeight(under);
					life.Go(dt, under, BirdRange * 0.8f, true);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						LetGoBird();
					}
					return(false);
				}
				life.mover = (landed ? "shooting at a bird" : "waiting for a bird to land");
				life.aimingBow = true;
				life.ai.StopMoving();
				Vector3 aim = (landed ? life.BirdAim(position, Vector3.zero) : position);
				life.ai.LookAt(aim);
				if (landed && Time.time >= nextShot && life.ReadyToShoot(aim) && life.humanoid.StartAttack(null, false))
				{
					shots++;
					nextShot = Time.time + BirdShotInterval;
				}
				return(false);
			}

			/// <summary>
			/// Gives up on a bird it couldn't hit or get to, leaving it alone for a while
			/// </summary>
			private void LetGoBird()
			{
				if (bird != null && bird.m_nview != null)
				{
					life.Avoid(bird.m_nview);
				}
				ReleaseBird();
			}

			/// <summary>
			/// Lets go of the bird it was after and puts its bow away
			/// </summary>
			private void ReleaseBird()
			{
				if (birdId != ZDOID.None)
				{
					life.ReleaseWork(birdId);
					birdId = ZDOID.None;
				}
				bird = null;
				shots = 0;
				life.aimingBow = false;
			}

			/// <summary>
			/// Gives up on prey that got away or that it can't get to, leaving it alone for a while - and its AI lets go of
			/// it too, since it's no longer an enemy once it's no longer hunted
			/// </summary>
			private void LetGo()
			{
				if (prey != null && prey.m_nview != null)
				{
					life.Avoid(prey.m_nview);
				}
				if (prey != null && life.ai.m_targetCreature == prey)
				{
					life.ai.m_targetCreature = null;
				}
				hunts.Remove(life.humanoid);
				prey = null;
				engaged = false;
				handedOver = false;
				snipeShots = 0;
				life.aimingBow = false;
			}

			/// <summary>
			/// Gathers what dropped around the kill - waiting while the kill rises as a zombie and its AI fights it, and a
			/// little longer after each thing it picks up
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once nothing more has dropped for a while</returns>
			private bool Collect(float dt)
			{
				if (actAt >= 0f)
				{
					if (Time.time < actAt)
					{
						return(false);
					}
					actAt = -1f;
					life.PickUpDrop(drop);
					ReleaseDrop();
					collectUntil = Mathf.Max(collectUntil, Time.time + ReachTime * 4f);
					return(false);
				}
				if (drop == null || !drop.IsValid())
				{
					ReleaseDrop();
					drop = life.FindDrop(kill);
					if (drop != null)
					{
						dropId = drop.GetZDO().m_uid;
						life.ClaimWork(dropId);
					}
				}
				if (drop != null)
				{
					if (Utils.DistanceXZ(drop.transform.position, life.transform.position) > ItemReach)
					{
						life.Go(dt, drop.transform.position, ItemReach, false);
						if (life.moveFailed)
						{
							life.moveFailed = false;
							life.Avoid(drop);
							ReleaseDrop();
						}
						return(false);
					}
					life.Face(drop.transform.position);
					VikingPoses.Interact(life.humanoid);
					RequestItem(drop);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (VillageHazards.NearestRising(kill, CollectRadius, out VillageHazards.Rising _))
				{
					collectUntil = Mathf.Max(collectUntil, Time.time + ReachTime * 4f);
				}
				life.ai.StopMoving();
				return(Time.time >= collectUntil);
			}

			/// <summary>
			/// Lets go of the dropped item it was after
			/// </summary>
			private void ReleaseDrop()
			{
				if (dropId != ZDOID.None)
				{
					life.ReleaseWork(dropId);
					dropId = ZDOID.None;
				}
				drop = null;
			}

			/// <summary>
			/// Stops hunting - letting go of its prey and what it was gathering, standing up from stalking, and noting it's
			/// no longer hunting
			/// </summary>
			public override void End()
			{
				life.body.SetStalking(false);
				ReleaseDrop();
				ReleaseBird();
				hunts.Remove(life.humanoid);
				if (prey != null && life.ai.m_targetCreature == prey)
				{
					life.ai.m_targetCreature = null;
				}
				life.SetDoing(HuntingKey, false);
			}
		}
	}
}
