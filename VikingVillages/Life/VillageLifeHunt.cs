using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Which creatures a viking hunts - its biome's prey, the birds it shoots and the hunting parties of the Plains -
	/// and how far off it shoots them from
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Checks whether a viking on this client is hunting a creature, which its AI treats as an enemy
		/// </summary>
		/// <param name="hunter">The viking</param>
		/// <param name="prey">The creature</param>
		/// <returns>True when it is</returns>
		public static bool Hunts(Character hunter, Character prey)
		{
			return(hunter != null && prey != null && hunts.TryGetValue(hunter, out Character hunted) && hunted == prey);
		}

		/// <summary>
		/// Checks whether a viking is an archer, fighting with a bow or crossbow (see VikingCore.IsArcher)
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <returns>True when it is</returns>
		public static bool HasBow(Character viking)
		{
			VikingCore archer = ((viking != null) ? viking.GetComponent<VikingCore>() : null);
			return(archer != null && archer.IsArcher());
		}

		/// <summary>
		/// Checks whether the viking may hunt - an archer always, or anyone when none of its village's vikings is one
		/// </summary>
		/// <param name="archer">Set to whether it's an archer, which also hunts the prey only archers hunt (see
		/// HuntPrey)</param>
		/// <returns>True when it may</returns>
		private bool MayHunt(out bool archer)
		{
			archer = HasBow(humanoid);
			return(archer || !village.Vikings().Exists((Character other) => HasBow(other)));
		}

		/// <summary>
		/// Checks whether a creature is prey the viking hunts - untamed prey of a biome within its reach (see
		/// HuntPrey.Hunts), living or risen as a zombie, that's alive and not one of the village's livestock
		/// </summary>
		/// <param name="creature">The creature</param>
		/// <param name="archer">Whether the viking is an archer</param>
		/// <returns>True when it is</returns>
		private bool IsPrey(Character creature, bool archer)
		{
			if (creature == null || creature.IsDead() || creature.IsTamed() || FarmSpawns.IsLivestock(creature))
			{
				return(false);
			}
			int biome = ((viking != null) ? viking.BiomeIndex() : 0);
			return(HuntPrey.Hunts(Utils.GetPrefabName(creature.gameObject), archer, biome));
		}

		/// <summary>
		/// Checks whether the viking is aiming its bow at a bird or at prey it's shooting from afar, so it keeps its bow
		/// in its hands
		/// </summary>
		/// <returns>True while it is</returns>
		public bool AimingBow()
		{
			return(aimingBow);
		}

		/// <summary>
		/// Checks whether the viking hunts wild prey as a party - from the Plains on, by the biome its gear comes from,
		/// where the prey fights back hard enough that one viking alone shouldn't take it on (see HuntParty)
		/// </summary>
		/// <returns>True when it does</returns>
		private bool GroupHunts()
		{
			return(viking != null && viking.BiomeIndex() >= VikingGear.IndexOf(Heightmap.Biome.Plains));
		}

		/// <summary>
		/// Lists the other vikings of the village this client owns that are free to join a hunting party - strolling
		/// or standing about, with nothing to fight and not hurt, within PartyRecruitRange - nearest first
		/// </summary>
		/// <returns>The vikings</returns>
		private List<VillageLife> FreeHunters()
		{
			List<VillageLife> free = new List<VillageLife>();
			foreach (VillageLife other in All)
			{
				// An NPC viking never hunts, so it's never one of a hunting party
				if (other == this || other.Peaceful() || !other.active || other.village != village || other.ai == null || other.ai.m_targetCreature != null || other.inBed || other.Wounded())
				{
					continue;
				}
				if (other.job != null && !(other.job is StrollJob) && !(other.job is IdleJob))
				{
					continue;
				}
				if (Vector3.Distance(other.transform.position, transform.position) <= PartyRecruitRange)
				{
					free.Add(other);
				}
			}
			free.Sort((VillageLife a, VillageLife b) => Vector3.Distance(a.transform.position, transform.position).CompareTo(Vector3.Distance(b.transform.position, transform.position)));
			return(free);
		}

		/// <summary>
		/// Checks whether the viking could lead a hunting party out now - there's prey for a party (see FindPrey) and
		/// enough free vikings to go with it (see FreeHunters)
		/// </summary>
		/// <param name="wanted">The prefab name of what the prey has to drop, or null for any prey</param>
		/// <returns>True when it could</returns>
		private bool CanLeadParty(string wanted)
		{
			return(GroupHunts() && FreeHunters().Count >= PartyMin - 1 && FindPrey(false, wanted, false, true) != null);
		}

		/// <summary>
		/// Leads a hunting party out - picking the prey and bringing the nearest free vikings, up to PartyMax with the
		/// leader, each of them taking up the party's hunt (see HuntParty)
		/// </summary>
		/// <param name="wanted">The prefab name of what the prey has to drop, or null for any prey</param>
		/// <returns>The leader's job, or null when there's no prey or too few to go</returns>
		private Job LeadParty(string wanted)
		{
			List<VillageLife> free = FreeHunters();
			Character target = ((free.Count >= PartyMin - 1) ? FindPrey(false, wanted, false, true) : null);
			if (target == null)
			{
				return(null);
			}
			HuntParty party = new HuntParty(this, target, Time.time + PartyGatherTime);
			for (int i = 0; i < free.Count && party.Members.Count < PartyMax; i++)
			{
				VillageLife member = free[i];
				party.Members.Add(member);
				member.StopJob();
				member.job = new HuntJob(member, HasBow(member.humanoid), wanted, false, party);
				member.job.Begin();
			}
			return(new HuntJob(this, HasBow(humanoid), wanted, false, party));
		}

		/// <summary>
		/// Checks whether the viking is out hunting or slaughtering livestock
		/// </summary>
		/// <returns>True when it is</returns>
		public bool Hunting()
		{
			return(job is HuntJob);
		}

		/// <summary>
		/// Checks whether a creature is prey that isn't dangerous (see HuntPrey.IsHarmless), which a hunter doesn't
		/// mind near the prey it's after or near itself - while a dangerous one, like a lox, means a fight with one
		/// could become a fight with all of them
		/// </summary>
		/// <param name="creature">The creature</param>
		/// <returns>True when it is</returns>
		private static bool IsHarmlessPrey(Character creature)
		{
			return(creature != null && HuntPrey.IsHarmless(Utils.GetPrefabName(creature.gameObject)));
		}

		/// <summary>
		/// Checks whether a hunter doesn't mind a creature near the prey it's after or near itself (see
		/// HuntPrey.Ignores) - prey that isn't dangerous, or another of its prey's kind when that kind doesn't herd
		/// </summary>
		/// <param name="prey">The prey, or null with none picked yet</param>
		/// <param name="creature">The other creature</param>
		/// <returns>True when it doesn't mind it</returns>
		private static bool IgnoredByHunter(Character prey, Character creature)
		{
			if (creature == null)
			{
				return(false);
			}
			string preyName = ((prey != null) ? Utils.GetPrefabName(prey.gameObject) : null);
			return(HuntPrey.Ignores(preyName, Utils.GetPrefabName(creature.gameObject)));
		}

		/// <summary>
		/// Checks whether a creature is a monster a hunter fights rather than runs from - a greyling
		/// </summary>
		/// <param name="creature">The creature</param>
		/// <returns>True when it is</returns>
		private static bool IsHuntedMonster(Character creature)
		{
			return(creature != null && Utils.GetPrefabName(creature.gameObject) == HuntPrey.FoughtOnTheWay);
		}

		/// <summary>
		/// Finds the nearest prey within HuntReach of the village that no other viking is after and with no enemy near it
		/// but what a hunter doesn't mind (see IgnoredByHunter), so dangerous prey is only hunted alone - only in the
		/// village while the viking may not leave it (see MayLeave)
		/// </summary>
		/// <param name="archer">Whether the viking is an archer, which also hunts the prey only archers hunt (see
		/// HuntPrey)</param>
		/// <param name="wanted">The prefab name of what the prey has to drop, like resin from a greyling, or null for any
		/// prey</param>
		/// <param name="livestock">True to take only the village's own livestock, which it slaughters when nothing wild
		/// drops what it's after (see GatherFor)</param>
		/// <param name="party">True for prey a hunting party goes after - from the Plains on, a viking hunts wild prey
		/// that isn't only for archers only as a party, and alone only what is (see GroupHunts)</param>
		/// <param name="food">True to take only prey that drops food, like meat (see QuestLoot.DropsFood)</param>
		/// <returns>The prey, or null when there's none</returns>
		private Character FindPrey(bool archer, string wanted, bool livestock, bool party = false, bool food = false)
		{
			float reach = (MayLeave() ? HuntReach : 0f);
			bool grouped = (!livestock && GroupHunts());
			List<Character> found = new List<Character>();
			foreach (Character creature in Character.GetAllCharacters())
			{
				bool prey = (livestock ? (creature != null && !creature.IsDead() && FarmSpawns.IsLivestock(creature) && !ZombieState.IsZombie(creature)) : IsPrey(creature, archer));
				if (!prey || (wanted != null && !Drops(creature.gameObject, wanted)) || village.DistanceOutside(creature.transform.position) > reach || hunts.ContainsValue(creature))
				{
					continue;
				}
				if (food && !QuestLoot.DropsFood(Utils.GetPrefabName(creature.gameObject)))
				{
					continue;
				}
				// Wild prey is only hunted on ground no harder than the hunter's own biome
				if (!livestock && TooHardToHunt(creature.transform.position))
				{
					continue;
				}
				if (grouped && HuntPrey.ForParty(Utils.GetPrefabName(creature.gameObject)) != party)
				{
					continue;
				}
				if (creature.m_nview != null && creature.m_nview.IsValid() && Avoided(creature.m_nview.GetZDO().m_uid))
				{
					continue;
				}
				found.Add(creature);
			}
			found.Sort((Character a, Character b) => Vector3.Distance(a.transform.position, transform.position).CompareTo(Vector3.Distance(b.transform.position, transform.position)));
			foreach (Character prey in found)
			{
				if (!EnemyNear(prey.transform.position, ForageSafety, (Character other) => other == prey || IgnoredByHunter(prey, other)))
				{
					return(prey);
				}
			}
			return(null);
		}

		/// <summary>
		/// Checks whether there's wild prey about that drops food for the village's hunters - the prey only archers hunt
		/// too when the village has an archer, hunted alone or by a party (see FindPrey)
		/// </summary>
		/// <returns>True when there is</returns>
		private bool WildFoodNear()
		{
			bool archers = village.Vikings().Exists((Character other) => HasBow(other));
			return(FindPrey(archers, null, false, false, true) != null || FindPrey(archers, null, false, true, true) != null);
		}

		/// <summary>
		/// Checks whether a spot is on ground harder than the viking's own biome - the one its gear comes from - which it
		/// doesn't go hunting on, so a hunter of the Meadows doesn't go after prey out on the Plains
		/// </summary>
		/// <param name="point">The spot</param>
		/// <returns>True when it's harder</returns>
		private bool TooHardToHunt(Vector3 point)
		{
			if (WorldGenerator.instance == null || viking == null)
			{
				return(false);
			}
			return(VikingGear.IndexOf(WorldGenerator.instance.GetBiome(point)) > viking.BiomeIndex());
		}

		/// <summary>
		/// Finds the nearest bird within HuntReach of the village for an archer to shoot for its feathers - a seagull,
		/// crow or the like, landed on the ground, since the vikings never shoot at one in the air - that no other viking
		/// is after and with no enemy near it but prey that isn't dangerous (see IsHarmlessPrey), only in the village while
		/// the viking may not leave it (see MayLeave)
		/// </summary>
		/// <param name="wanted">The prefab name of what the bird has to drop, like feathers, or null for any bird</param>
		/// <returns>The bird, or null when there's none</returns>
		private RandomFlyingBird FindBird(string wanted)
		{
			float reach = (MayLeave() ? HuntReach : 0f);
			List<KeyValuePair<float, RandomFlyingBird>> found = new List<KeyValuePair<float, RandomFlyingBird>>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZNetView view = instance.Value;
				if (view == null || !view.IsValid() || village.DistanceOutside(view.transform.position) > reach || Avoided(instance.Key.m_uid))
				{
					continue;
				}
				if (wanted != null && !Drops(view.gameObject, wanted))
				{
					continue;
				}
				RandomFlyingBird bird = view.GetComponent<RandomFlyingBird>();
				if (bird == null || !Landed(bird) || view.GetComponent<Destructible>() == null || TooHardToHunt(view.transform.position))
				{
					continue;
				}
				if (workClaims.TryGetValue(instance.Key.m_uid, out VillageLife holder) && holder != this && holder != null && holder.job != null)
				{
					continue;
				}
				found.Add(new KeyValuePair<float, RandomFlyingBird>(Vector3.Distance(view.transform.position, transform.position), bird));
			}
			found.Sort((KeyValuePair<float, RandomFlyingBird> a, KeyValuePair<float, RandomFlyingBird> b) => a.Key.CompareTo(b.Key));
			foreach (KeyValuePair<float, RandomFlyingBird> entry in found)
			{
				if (!EnemyNear(entry.Value.transform.position, ForageSafety, (Character other) => IsHarmlessPrey(other)))
				{
					return(entry.Value);
				}
			}
			return(null);
		}

		/// <summary>
		/// Checks whether a bird has landed on the ground, as the game notes it for every client (see
		/// RandomFlyingBird.CustomFixedUpdate)
		/// </summary>
		/// <param name="bird">The bird</param>
		/// <returns>True while it's landed</returns>
		private static bool Landed(RandomFlyingBird bird)
		{
			return(bird.m_nview != null && bird.m_nview.IsValid() && bird.m_nview.GetZDO().GetBool(ZDOVars.s_landed));
		}

		/// <summary>
		/// Works out where an archer aims to hit a bird - ahead of it by as far as it flies while the arrow is on its
		/// way, and above it by as far as the arrow drops on the way there
		/// </summary>
		/// <param name="target">Where the bird is</param>
		/// <param name="velocity">How fast it's flying, in meters a second</param>
		/// <returns>The point to aim at</returns>
		private Vector3 BirdAim(Vector3 target, Vector3 velocity)
		{
			ItemDrop.ItemData weapon = humanoid.GetCurrentWeapon();
			if (weapon == null || string.IsNullOrEmpty(weapon.m_shared.m_ammoType))
			{
				return(target);
			}
			float speed = weapon.m_shared.m_attack.m_projectileVel;
			GameObject shot = weapon.m_shared.m_attack.m_attackProjectile;
			ItemDrop.ItemData ammo = humanoid.GetAmmoItem();
			if (ammo == null || !humanoid.GetInventory().ContainsItem(ammo))
			{
				ammo = humanoid.GetInventory().GetAmmoItem(weapon.m_shared.m_ammoType);
			}
			if (ammo != null && ammo.m_shared.m_attack.m_attackProjectile != null)
			{
				speed += ammo.m_shared.m_attack.m_projectileVel;
				shot = ammo.m_shared.m_attack.m_attackProjectile;
			}
			Projectile projectile = ((shot != null) ? shot.GetComponent<Projectile>() : null);
			float gravity = ((projectile != null) ? projectile.m_gravity : 0f);
			float time = Vector3.Distance(humanoid.m_eye.position, target) / Mathf.Max(1f, speed);
			return(target + velocity * time + Vector3.up * (0.5f * gravity * time * time));
		}

		/// <summary>
		/// Works out how near the viking gets to prey before it stops to shoot it from afar, from the bow it shoots
		/// with and the ammo it has for it (see SnipeRangeFor) - SnipeRangeMin when it has none
		/// </summary>
		/// <returns>The range, in meters</returns>
		private float SnipeRange()
		{
			ItemDrop.ItemData bow = ((viking != null) ? viking.Bow() : null);
			Attack attack = ((bow != null) ? bow.m_shared.m_attack : null);
			if (attack == null)
			{
				return(SnipeRangeMin);
			}
			float speed = attack.m_projectileVel;
			float spread = attack.m_projectileAccuracy;
			ItemDrop.ItemData ammo = humanoid.GetInventory().GetAmmoItem(bow.m_shared.m_ammoType);
			if (ammo != null && ammo.m_shared.m_attack != null)
			{
				speed += ammo.m_shared.m_attack.m_projectileVel;
				spread += ammo.m_shared.m_attack.m_projectileAccuracy;
			}
			return(SnipeRangeFor(speed, spread));
		}

		/// <summary>
		/// Works out how near an archer gets to prey before it stops to shoot it from afar - as far as its arrows fly in
		/// SnipeFlightTime, and no farther than they stray SnipeSpreadTolerance from where it aims, between
		/// SnipeRangeMin and SightRange. A viking always draws its bow fully, so its arrows fly at the bow's full speed
		/// and spread as little as the bow lets them (see VikingDrawPatch)
		/// </summary>
		/// <param name="speed">How fast its arrows fly, in meters a second</param>
		/// <param name="spread">How far its arrows stray from where it aims, either way, in degrees</param>
		/// <returns>The range, in meters</returns>
		internal static float SnipeRangeFor(float speed, float spread)
		{
			float range = speed * SnipeFlightTime;
			if (spread > 0f)
			{
				range = Mathf.Min(range, SnipeSpreadTolerance / Mathf.Tan(spread * Mathf.Deg2Rad));
			}
			return(Mathf.Clamp(range, SnipeRangeMin, SightRange));
		}

		/// <summary>
		/// Checks whether an archer is ready to loose an arrow at a point - its bow is in its hands with arrows for it,
		/// and it faces the point
		/// </summary>
		/// <param name="aim">The point</param>
		/// <returns>True when it is</returns>
		private bool ReadyToShoot(Vector3 aim)
		{
			ItemDrop.ItemData weapon = humanoid.GetCurrentWeapon();
			if (weapon == null || string.IsNullOrEmpty(weapon.m_shared.m_ammoType) || humanoid.GetInventory().GetAmmoItem(weapon.m_shared.m_ammoType) == null || humanoid.InAttack())
			{
				return(false);
			}
			Vector3 toward = aim - transform.position;
			toward.y = 0f;
			Vector3 facing = transform.forward;
			facing.y = 0f;
			return(toward.sqrMagnitude < 0.01f || Vector3.Angle(toward, facing) <= BirdAimAngle);
		}
	}
}
