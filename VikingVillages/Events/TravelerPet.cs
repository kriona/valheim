using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A traveler's animal (see TravelerPets) - keeping by its traveler as they walk in, carrying them in, then waiting
	/// at its spot at the village's edge, or for a wolf, keeping by them wherever they go outdoors and waiting at the
	/// door of a house they go into. A valkyrie flies them in to open ground near the village's middle, then roosts on a
	/// roof, and comes down to them when they call her as they leave. It fights what its traveler fights and its
	/// traveler fights what it fights, turns on a player its traveler is angry at, and a player hurting it angers its
	/// traveler and the village. It goes with its traveler as they leave, and once they're gone it heads off out of
	/// sight too, the way they do, a valkyrie flying. Left to the village when its traveler dies, it's an ordinary tamed
	/// animal from then on
	/// </summary>
	/// <remarks>
	/// Added to the animal as it loads on any client (see TravelerPetLoadPatch), and run on the client that owns it
	/// </remarks>
	internal class TravelerPet : MonoBehaviour
	{
		/// <summary>
		/// How often the animal looks to its traveler, in seconds
		/// </summary>
		private const float CheckInterval = 0.5f;

		/// <summary>
		/// How far the game's area aggravation reaches when a player hurts a creature, in meters (see
		/// Character.ApplyDamage)
		/// </summary>
		private const float AngerReach = 20f;

		/// <summary>
		/// Every traveler's animal loaded on this client, by its character, so the game's enemy checks find them fast
		/// </summary>
		private static readonly Dictionary<Character, TravelerPet> pets = new Dictionary<Character, TravelerPet>();

		private Character animal;
		private ZNetView nview;
		private MonsterAI ai;
		private float nextCheck;

		/// <summary>
		/// How near its traveler a saddled animal is for the traveler to be riding it - a valkyrie's seat is high on her
		/// back - and how far ahead of it a point it carries its traveler away toward is, walking and flying, in meters
		/// </summary>
		private const float CarryReach = 6f;
		private const float CarryStep = 30f;
		private const float FlyStep = 100f;

		/// <summary>
		/// The animal's traveler as last found, and whether the animal has headed off after it
		/// </summary>
		private Character owner;
		private bool departing;

		/// <summary>
		/// Whether the animal carries its traveler, riding it, where it carries them - into the village as they arrive, and
		/// away from it as they leave - and for a valkyrie, whether she lands there
		/// </summary>
		private bool carrying;
		private Vector3 carryTo;
		private bool carryLands;

		/// <summary>
		/// A valkyrie's flying, which flies her traveler in and out, or null for an animal that walks
		/// </summary>
		private ValkyrieFlight flight;

		/// <summary>
		/// For a valkyrie - the open ground near the village's middle she lands her traveler on as they arrive, once it's
		/// been found, and as they leave, whether she's coming down to them when they call her, where she lands, and
		/// where they stood when that was found
		/// </summary>
		private bool hasLanding;
		private Vector3 landing;
		private bool answering;
		private Vector3 answerTo;
		private Vector3 answerFrom;

		/// <summary>
		/// How far a traveler can move from where they called their valkyrie before she finds open ground near them again,
		/// and on a client that doesn't run her, how near them and how near their height she has to have landed to count
		/// as come down, in meters
		/// </summary>
		private const float AnswerMove = 6f;
		private const float AnswerNear = 15f;
		private const float AnswerLevel = 2f;

		/// <summary>
		/// The ZDO key on a traveler noting they've called their valkyrie down as they leave (see VillageLife.LeaveJob)
		/// </summary>
		public static readonly int CalledKey = "kri_vv_valkyrie_called".GetStableHashCode();

		/// <summary>
		/// The ZDO key on a valkyrie noting a viking of the village she flies her traveler into has shouted that she's
		/// coming (see Herald), and how near the village's edge she has to be for it, in meters
		/// </summary>
		private static readonly int HeraldedKey = "kri_vv_valkyrie_heralded".GetStableHashCode();
		private const float HeraldReach = 60f;

		/// <summary>
		/// Finds the traveler's animal a character is
		/// </summary>
		/// <param name="character">The character, or null</param>
		/// <returns>Its part as a traveler's animal, or null when it isn't one</returns>
		public static TravelerPet Of(Character character)
		{
			return((character != null && pets.TryGetValue(character, out TravelerPet pet)) ? pet : null);
		}

		/// <summary>
		/// Notes the animal and listens for it being hurt
		/// </summary>
		private void Awake()
		{
			animal = GetComponent<Character>();
			nview = GetComponent<ZNetView>();
			ai = GetComponent<MonsterAI>();
			flight = GetComponent<ValkyrieFlight>();
			if (animal != null)
			{
				pets[animal] = this;
				animal.m_onDamaged += OnDamaged;
			}
		}

		/// <summary>
		/// Forgets the animal as it unloads
		/// </summary>
		private void OnDestroy()
		{
			if (animal != null)
			{
				pets.Remove(animal);
				animal.m_onDamaged -= OnDamaged;
			}
		}

		/// <summary>
		/// Checks whether the animal has been left to the village, its traveler dead
		/// </summary>
		/// <returns>True when it has</returns>
		public bool Orphaned()
		{
			return(nview != null && nview.IsValid() && nview.GetZDO().GetBool(TravelerPets.OrphanKey));
		}

		/// <summary>
		/// Checks whether the animal is heading off out of sight after its traveler, which the game's AI leaves to it (see
		/// TravelerPetMovePatch)
		/// </summary>
		/// <returns>True when it is</returns>
		public bool Departing()
		{
			return(departing);
		}

		/// <summary>
		/// Checks whether the animal carries its traveler riding it, which the game's AI leaves to it (see
		/// TravelerPetMovePatch)
		/// </summary>
		/// <param name="to">Set to where it carries them</param>
		/// <returns>True when it does</returns>
		public bool Carrying(out Vector3 to)
		{
			to = carryTo;
			return(carrying && !departing);
		}

		/// <summary>
		/// Flies a valkyrie carrying her traveler toward where she carries them, landing there as they arrive or to let
		/// them fight (see Carries), on the client that owns her, each AI update
		/// </summary>
		/// <param name="to">Where she carries them</param>
		/// <returns>False for an animal that walks</returns>
		public bool FlyTo(Vector3 to)
		{
			if (flight == null)
			{
				return(false);
			}
			flight.Pilot(to, carryLands);
			return(true);
		}

		/// <summary>
		/// Checks whether a valkyrie is coming down to her traveler, who's called her as they leave (see Answer), and where
		/// she lands - with nothing to fight, and only until they're on her
		/// </summary>
		/// <param name="to">Set to where she lands</param>
		/// <returns>True when she is</returns>
		public bool Answering(out Vector3 to)
		{
			to = answerTo;
			return(answering && !carrying && !departing && flight != null && ai != null && ai.m_targetCreature == null);
		}

		/// <summary>
		/// Flies a valkyrie down to where her traveler waits for her as they leave, landing there and staying until they
		/// get on, on the client that owns her, each AI update
		/// </summary>
		/// <param name="to">Where she lands</param>
		public void FlyDown(Vector3 to)
		{
			if (flight != null)
			{
				flight.Pilot(to, true);
			}
		}

		/// <summary>
		/// Checks whether a valkyrie has come down to where her traveler called her to, for them to get on - landed there,
		/// on the client that runs her, or on another, landed near them on the ground rather than up on a roof
		/// </summary>
		/// <param name="traveler">Her traveler</param>
		/// <returns>True when she has, or for an animal that walks</returns>
		public bool Answered(Character traveler)
		{
			if (flight == null)
			{
				return(true);
			}
			if (flight.IsFlying())
			{
				return(false);
			}
			if (answering)
			{
				return(Utils.DistanceXZ(transform.position, answerTo) <= ValkyrieMotion.ArriveRadius + 1f);
			}
			return(traveler != null && Utils.DistanceXZ(transform.position, traveler.transform.position) <= AnswerNear && Mathf.Abs(transform.position.y - traveler.transform.position.y) <= AnswerLevel);
		}

		/// <summary>
		/// Flies a valkyrie off away from the village once her traveler has gone, on the client that owns her, each AI
		/// update - gone once no player is within TravelerPets.FlownGoneReach of her
		/// </summary>
		/// <returns>False for an animal that walks</returns>
		public bool FlyOff()
		{
			if (flight == null)
			{
				return(false);
			}
			if (Player.GetClosestPlayer(transform.position, TravelerPets.FlownGoneReach) == null)
			{
				Plugin.Log.LogInfo("A traveler's " + Utils.GetPrefabName(gameObject) + " flew off after its traveler");
				nview.Destroy();
				return(true);
			}
			flight.Pilot(AwayPoint(FlyStep), false);
			return(true);
		}

		/// <summary>
		/// Finds a point ahead of the animal away from the middle of the village its traveler visits, or straight ahead
		/// of it when that isn't loaded
		/// </summary>
		/// <param name="step">How far ahead, in meters</param>
		/// <returns>The point</returns>
		private Vector3 AwayPoint(float step)
		{
			Village village = Village.ForFarm(nview.GetZDO().GetVec3(TravelerPets.HostKey, Vector3.zero));
			Vector3 away = ((village != null) ? transform.position - village.Middle : transform.forward);
			away.y = 0f;
			away = ((away.sqrMagnitude > 0.01f) ? away.normalized : transform.forward);
			return(transform.position + away * step);
		}

		/// <summary>
		/// Checks whether the animal is angry at the players - while its traveler is, and never once it's the village's
		/// </summary>
		/// <returns>True when it is</returns>
		public bool Angry()
		{
			if (owner == null || Orphaned())
			{
				return(false);
			}
			BaseAI ownerAI = owner.GetBaseAI();
			return(ownerAI != null && ownerAI.IsAggravated());
		}

		/// <summary>
		/// Finds the animal's traveler, when it's loaded
		/// </summary>
		/// <returns>The traveler, or null</returns>
		private Character FindOwner()
		{
			ZDOID id = nview.GetZDO().GetZDOID(TravelerPets.OwnerKey);
			GameObject found = ((ZNetScene.instance != null && !id.IsNone()) ? ZNetScene.instance.FindInstance(id) : null);
			Character character = ((found != null) ? found.GetComponent<Character>() : null);
			return((character != null && !character.IsDead()) ? character : null);
		}

		/// <summary>
		/// Looks to the traveler every CheckInterval seconds on the animal's owner - naming the animal once its traveler
		/// has a name, heading off once the traveler has gone, fighting what the traveler fights, and otherwise keeping
		/// to where it belongs (see Keep)
		/// </summary>
		private void Update()
		{
			if (Time.time < nextCheck || animal == null || ai == null || nview == null || !nview.IsValid() || !nview.IsOwner() || animal.IsDead())
			{
				return;
			}
			nextCheck = Time.time + CheckInterval;
			ZDO zdo = nview.GetZDO();
			if (zdo.GetBool(TravelerPets.OrphanKey))
			{
				if (owner != null || ai.GetFollowTarget() != null)
				{
					owner = null;
					ai.SetFollowTarget(null);
					ai.SetPatrolPoint();
				}
				return;
			}
			owner = FindOwner();
			GiveName(zdo);
			carrying = Carries(zdo);
			if (carrying)
			{
				if (ai.GetFollowTarget() != null)
				{
					ai.SetFollowTarget(null);
				}
				Herald(zdo);
				return;
			}
			if (owner == null)
			{
				// Its traveler is gone once its time to leave has passed, whether it left in sight or out of reach
				if (NetTime.Now() >= zdo.GetLong(TravelerPets.LeaveAtKey, 0L))
				{
					Depart();
				}
				else
				{
					Stay(zdo.GetVec3(TravelerPets.SpotKey, transform.position));
				}
				return;
			}
			ShareTarget();
			if (ai.m_targetCreature != null)
			{
				return;
			}
			Keep(zdo);
		}

		/// <summary>
		/// Checks whether the animal's traveler rides it, working out where it carries them - as they arrive, to where the
		/// village's vikings walk back to (see Village.RallyPoint), or for a valkyrie, landing at her spot at the village's
		/// edge, and as they leave, on away from the village's middle, CarryStep ahead at a time, or FlyStep flying. A
		/// valkyrie with an enemy within VillageLife.DismountThreat of her traveler lands where she is, so they can get
		/// off and fight
		/// </summary>
		/// <param name="zdo">The animal's ZDO</param>
		/// <returns>True when its traveler rides it</returns>
		private bool Carries(ZDO zdo)
		{
			if (owner == null || !owner.m_nview.GetZDO().GetBool(TravelerPets.RidingKey) || Vector3.Distance(owner.transform.position, transform.position) > CarryReach)
			{
				return(false);
			}
			Village village = Village.ForFarm(zdo.GetVec3(TravelerPets.HostKey, Vector3.zero));
			if (village == null)
			{
				return(false);
			}
			bool leaving = owner.m_nview.GetZDO().GetBool(TravelerPets.LeavingKey);
			if (flight != null && Village.FindEnemyNear(owner.transform.position, VillageLife.DismountThreat) != null)
			{
				carryTo = transform.position;
				carryLands = true;
			}
			else if (flight != null)
			{
				carryTo = (leaving ? AwayPoint(FlyStep) : Landing(village, zdo));
				carryLands = !leaving;
			}
			else
			{
				carryTo = (leaving ? AwayPoint(CarryStep) : village.RallyPoint());
			}
			return(true);
		}

		/// <summary>
		/// Finds where a valkyrie lands her traveler as they arrive - open ground near the village's middle, clear of its
		/// buildings, trees and rocks (see ValkyrieRoost.FindOpenGround) - or her spot at the village's edge when there's
		/// none, found once
		/// </summary>
		/// <param name="village">The village they visit</param>
		/// <param name="zdo">Her ZDO</param>
		/// <returns>Where she lands</returns>
		private Vector3 Landing(Village village, ZDO zdo)
		{
			if (!hasLanding)
			{
				hasLanding = true;
				if (!ValkyrieRoost.FindOpenGround(village.Middle, out landing))
				{
					landing = zdo.GetVec3(TravelerPets.SpotKey, transform.position);
				}
			}
			return(landing);
		}

		/// <summary>
		/// Has a viking of the village a valkyrie flies her traveler into shout that she's coming, and from which way, once
		/// she's within HeraldReach of its edge and before she's in it - the nearest to her who's awake, not fighting and in
		/// the village. Only once, and not as she flies them away again
		/// </summary>
		/// <param name="zdo">Her ZDO, owned by this client</param>
		private void Herald(ZDO zdo)
		{
			if (flight == null || zdo.GetBool(HeraldedKey) || !flight.IsFlying() || owner == null || owner.m_nview.GetZDO().GetBool(TravelerPets.LeavingKey))
			{
				return;
			}
			Village village = Village.ForFarm(zdo.GetVec3(TravelerPets.HostKey, Vector3.zero));
			if (village == null)
			{
				return;
			}
			float outside = village.DistanceOutside(transform.position);
			if (outside > HeraldReach)
			{
				return;
			}
			// Once she's in the village, it's too late to see her coming
			if (outside <= 0f)
			{
				zdo.Set(HeraldedKey, true);
				return;
			}
			Character crier = Crier(village);
			if (crier == null)
			{
				return;
			}
			zdo.Set(HeraldedKey, true);
			string line = ValkyrieSightSpeech.Herald(ValkyrieSightSpeech.Direction(village.Middle, transform.position), Random.value);
			VikingSpeech.SayFirst(crier, line, VikingSpeech.CallTime, VikingSpeech.CallCull);
		}

		/// <summary>
		/// Finds the viking of a village nearest a valkyrie flying in, to shout that she's coming - a PC or NPC viking in
		/// the village, awake and not fighting, and not her own traveler
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The viking, or null when there's none</returns>
		private Character Crier(Village village)
		{
			Character nearest = null;
			float nearestDistance = float.MaxValue;
			List<Character> near = new List<Character>();
			Character.GetCharactersInRange(transform.position, HeraldReach + 80f, near);
			foreach (Character viking in near)
			{
				if (viking == owner || viking.IsDead() || !VikingCore.IsPlayerLikeViking(viking) || !village.Contains(viking.transform.position) || VikingSpeech.Asleep(viking))
				{
					continue;
				}
				BaseAI vikingAI = viking.GetBaseAI();
				if (vikingAI != null && vikingAI.GetTargetCreature() != null)
				{
					continue;
				}
				VillageLife life = viking.GetComponent<VillageLife>();
				if (life != null && life.IsTraveler())
				{
					continue;
				}
				float distance = Vector3.Distance(viking.transform.position, transform.position);
				if (distance < nearestDistance)
				{
					nearest = viking;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Has a valkyrie come down to her traveler as they leave, once they've called her (see VillageLife.LeaveJob) - to
		/// open ground near them, found again should they move off from where she was called to
		/// </summary>
		private void Answer()
		{
			if (answering && Utils.DistanceXZ(owner.transform.position, answerFrom) <= AnswerMove)
			{
				return;
			}
			answering = true;
			answerFrom = owner.transform.position;
			if (!ValkyrieRoost.FindOpenGround(answerFrom, out answerTo))
			{
				answerTo = answerFrom;
			}
		}

		/// <summary>
		/// Names the animal once its traveler has a name, with one of its kind's names that isn't its traveler's (see
		/// TravelerPets.PickName)
		/// </summary>
		/// <param name="zdo">The animal's ZDO, owned by this client</param>
		private void GiveName(ZDO zdo)
		{
			// A valkyrie has a name of her own from the start, which she tells players herself (see ValkyrieTalk)
			if (zdo.GetString(ZDOVars.s_tamedName).Length > 0 || GetComponent<ValkyrieTalk>() != null)
			{
				return;
			}
			ZDO ownerZdo = ((ZDOMan.instance != null) ? ZDOMan.instance.GetZDO(zdo.GetZDOID(TravelerPets.OwnerKey)) : null);
			string ownerName = ((ownerZdo != null) ? VikingNames.Get(ownerZdo) : "");
			TravelerPets.Kind kind = null;
			foreach (TravelerPets.Kind each in TravelerPets.Kinds.Values)
			{
				if (each.Prefab == Utils.GetPrefabName(gameObject))
				{
					kind = each;
				}
			}
			if (kind == null || ownerName.Length == 0)
			{
				return;
			}
			string name = TravelerPets.PickName(kind.Names, ownerName, Random.value);
			if (name.Length > 0)
			{
				zdo.Set(ZDOVars.s_tamedName, name);
				zdo.Set(ZDOVars.s_tamedNameAuthor, "");
			}
		}

		/// <summary>
		/// Has the animal take up the enemy its traveler fights when it has none of its own - however far off - and one
		/// the animal fights is taken up by the traveler in turn (see VillageLife.UpdateTraveler)
		/// </summary>
		private void ShareTarget()
		{
			MonsterAI ownerAI = owner.GetBaseAI() as MonsterAI;
			Character target = ((ownerAI != null) ? ownerAI.m_targetCreature : null);
			if (ai.m_targetCreature == null && target != null && !target.IsDead() && BaseAI.IsEnemy(animal, target))
			{
				ai.m_targetCreature = target;
				ai.SetAlerted(true);
			}
		}

		/// <summary>
		/// Gets the enemy the animal fights, for its traveler to take up
		/// </summary>
		/// <returns>The enemy, or null</returns>
		public Character Target()
		{
			return((ai != null && ai.m_targetCreature != null && !ai.m_targetCreature.IsDead()) ? ai.m_targetCreature : null);
		}

		/// <summary>
		/// Keeps the animal where it belongs while its traveler is in the village and isn't riding it - with the traveler
		/// as they leave, or for one they ride, waiting at its spot for them to get on, and with them as they walk in,
		/// then a wolf keeping by them outdoors and waiting at the door of a house they go into, and any other kind
		/// waiting at its spot at the village's edge. A valkyrie roosts on a roof instead (see ValkyrieFlight.RoostSoon),
		/// flying down to defend the village and back up after, and comes down to her traveler when they call her as they
		/// leave (see Answer)
		/// </summary>
		/// <param name="zdo">The animal's ZDO, owned by this client</param>
		private void Keep(ZDO zdo)
		{
			ZDO ownerZdo = owner.m_nview.GetZDO();
			Village village = Village.ForFarm(zdo.GetVec3(TravelerPets.HostKey, Vector3.zero));
			if (flight != null && village != null)
			{
				zdo.Set(TravelerPets.SettledKey, true);
				if (ai.GetFollowTarget() != null)
				{
					ai.SetFollowTarget(null);
				}
				if (zdo.GetBool(ZDOVars.s_patrol))
				{
					ai.ResetPatrolPoint();
				}
				if (ownerZdo.GetBool(TravelerPets.LeavingKey))
				{
					// She stays where she roosts until they've called her
					if (ownerZdo.GetBool(CalledKey))
					{
						Answer();
					}
				}
				else
				{
					answering = false;
					flight.RoostSoon();
				}
				return;
			}
			if (ownerZdo.GetBool(TravelerPets.LeavingKey) || village == null)
			{
				if (village != null && TravelerPets.IsRidden(animal))
				{
					Stay(zdo.GetVec3(TravelerPets.SpotKey, transform.position));
					return;
				}
				Follow();
				return;
			}
			if (!zdo.GetBool(TravelerPets.SettledKey))
			{
				// A valkyrie goes straight to her spot rather than coming in beside her traveler
				if (flight == null && !village.Contains(owner.transform.position))
				{
					Follow();
					return;
				}
				zdo.Set(TravelerPets.SettledKey, true);
			}
			if (!zdo.GetBool(TravelerPets.FollowsKey))
			{
				Stay(zdo.GetVec3(TravelerPets.SpotKey, transform.position));
				return;
			}
			if (village.InHouse(owner.transform.position, 0f) && village.NearestHouse(owner.transform.position, out VillagePaths.House house))
			{
				Stay(NearestDoorstep(house, owner.transform.position));
				return;
			}
			Follow();
		}

		/// <summary>
		/// Finds the doorstep of a house nearest a point - where a wolf waits for its traveler outside
		/// </summary>
		/// <param name="house">The house</param>
		/// <param name="point">The point</param>
		/// <returns>The doorstep, or the point when the house has none</returns>
		private static Vector3 NearestDoorstep(VillagePaths.House house, Vector3 point)
		{
			List<Vector3> steps = Village.Doorsteps(house);
			if (steps.Count == 0)
			{
				return(point);
			}
			steps.Sort((Vector3 a, Vector3 b) => Utils.DistanceXZ(a, point).CompareTo(Utils.DistanceXZ(b, point)));
			return(steps[0]);
		}

		/// <summary>
		/// Has the animal follow its traveler, the way a tamed animal follows a player
		/// </summary>
		private void Follow()
		{
			if (ai.GetFollowTarget() != owner.gameObject)
			{
				ai.ResetPatrolPoint();
				ai.SetFollowTarget(owner.gameObject);
			}
		}

		/// <summary>
		/// Has the animal wait around a spot, the way a tamed animal told to stay does, going back to it after a fight
		/// </summary>
		/// <param name="spot">The spot</param>
		private void Stay(Vector3 spot)
		{
			if (ai.GetFollowTarget() != null)
			{
				ai.SetFollowTarget(null);
			}
			if (!nview.GetZDO().GetBool(ZDOVars.s_patrol) || Utils.DistanceXZ(nview.GetZDO().GetVec3(ZDOVars.s_patrolPoint, Vector3.zero), spot) > 1f)
			{
				ai.SetPatrolPoint(spot);
			}
		}

		/// <summary>
		/// Has the animal head off out of sight now its traveler has gone (see TravelerPetLeavePatch)
		/// </summary>
		private void Depart()
		{
			if (departing)
			{
				return;
			}
			departing = true;
			ai.SetFollowTarget(null);
			ai.ResetPatrolPoint();
			Plugin.Log.LogInfo("A traveler's " + Utils.GetPrefabName(gameObject) + " is leaving after its traveler");
		}

		/// <summary>
		/// Has a player hurting the animal count as hurting its traveler - angering the traveler however far off, and every
		/// viking near either of them, the way the game angers those near a viking a player hurts - on the client the
		/// animal is hurt on
		/// </summary>
		/// <param name="damage">The damage dealt</param>
		/// <param name="attacker">Who dealt it, or null</param>
		private void OnDamaged(float damage, Character attacker)
		{
			if (damage <= 0f || attacker == null || !attacker.IsPlayer() || Orphaned())
			{
				return;
			}
			Character traveler = FindOwner();
			if (traveler == null)
			{
				return;
			}
			BaseAI ownerAI = traveler.GetBaseAI();
			if (ownerAI != null)
			{
				ownerAI.SetAggravated(true, BaseAI.AggravatedReason.Damage);
			}
			BaseAI.AggravateAllInArea(traveler.transform.position, AngerReach, BaseAI.AggravatedReason.Damage);
			BaseAI.AggravateAllInArea(transform.position, AngerReach, BaseAI.AggravatedReason.Damage);
		}
	}
}
