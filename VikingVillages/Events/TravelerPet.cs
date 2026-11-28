using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A traveler's animal (see TravelerPets) - keeping by its traveler as they walk in, then waiting at its spot at the
	/// village's edge, or for a wolf, keeping by them wherever they go outdoors and waiting at the door of a house they
	/// go into. It fights what its traveler fights and its traveler fights what it fights, turns on a player its
	/// traveler is angry at, and a player hurting it angers its traveler and the village. It goes with its traveler as
	/// they leave, and once they're gone it heads off out of sight too, the way they do. Left to the village when its
	/// traveler dies, it's an ordinary tamed animal from then on
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
		/// The animal's traveler as last found, and whether the animal has headed off after it
		/// </summary>
		private Character owner;
		private bool departing;

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
		/// TravelerPetLeavePatch)
		/// </summary>
		/// <returns>True when it is</returns>
		public bool Departing()
		{
			return(departing);
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
		/// Names the animal once its traveler has a name, with one of its kind's names that isn't its traveler's (see
		/// TravelerPets.PickName)
		/// </summary>
		/// <param name="zdo">The animal's ZDO, owned by this client</param>
		private void GiveName(ZDO zdo)
		{
			if (zdo.GetString(ZDOVars.s_tamedName).Length > 0)
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
		/// Keeps the animal where it belongs while its traveler is in the village - with the traveler as they leave and
		/// as they walk in, then a wolf keeping by them outdoors and waiting at the door of a house they go into, and any
		/// other kind waiting at its spot at the village's edge
		/// </summary>
		/// <param name="zdo">The animal's ZDO, owned by this client</param>
		private void Keep(ZDO zdo)
		{
			ZDO ownerZdo = owner.m_nview.GetZDO();
			Village village = Village.ForFarm(zdo.GetVec3(TravelerPets.HostKey, Vector3.zero));
			if (ownerZdo.GetBool(TravelerPets.LeavingKey) || village == null)
			{
				Follow();
				return;
			}
			if (!zdo.GetBool(TravelerPets.SettledKey))
			{
				if (!village.Contains(owner.transform.position))
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
