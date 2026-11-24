using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Works out a quest's fight from the game - the creatures it kills (see Fought), with their health, resistances and
	/// attacks from their prefabs, and each viking's weapons, skills, armor, shield and health - with QuestFight doing
	/// the sums
	/// </summary>
	/// <remarks>
	/// Both sides' hits go through the world's modifiers the way the game's do (see Character.RPC_Damage and
	/// ApplyDamage): the enemies' by the enemy damage rate, and the vikings' by the player damage rate, since the game
	/// scales every hit on a creature that isn't a player by it, and in a world past level 0 by the armor that gives
	/// enemies. Every enemy is taken to be one with no stars
	/// </remarks>
	internal static class QuestFightGame
	{
		/// <summary>
		/// How many of a quest's enemies come at the vikings at once, for those where it's more than one - a crypt's or
		/// burial chamber's dead in twos, and a frost cave's beasts and cultists and a fuling village's in threes
		/// </summary>
		private static readonly Dictionary<QuestKind, int> Packs = new Dictionary<QuestKind, int>
		{
			{ QuestKind.RaidCrypts, 2 },
			{ QuestKind.RaidBurialChambers, 2 },
			{ QuestKind.HuntGhosts, 2 },
			{ QuestKind.HuntGoblins, 3 },
			{ QuestKind.RaidFrostCaves, 3 }
		};

		/// <summary>
		/// What a creature's prefab gives it - its health, the least time between its attacks and its attacks (see
		/// Enemy), its resistances, and each attack's damage and how much the attack multiplies it by
		/// </summary>
		private class Foe
		{
			public Enemy Enemy = new Enemy();
			public HitData.DamageModifiers Resistances;
			public List<HitData.DamageTypes> Damages = new List<HitData.DamageTypes>();
			public List<float> Multipliers = new List<float>();
		}

		/// <summary>
		/// The creatures read so far, by prefab name, since every one of a prefab is the same
		/// </summary>
		private static readonly Dictionary<string, Foe> known = new Dictionary<string, Foe>();

		/// <summary>
		/// Gets how many of a quest's enemies come at the vikings at once (see Packs)
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <returns>How many</returns>
		public static int PackOf(QuestKind kind)
		{
			return(Packs.TryGetValue(kind, out int pack) ? pack : 1);
		}

		/// <summary>
		/// Works out a quest's fight for the vikings that go
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="members">The vikings</param>
		/// <returns>How it goes, or null when the quest kills nothing or a creature's prefab can't be read</returns>
		public static FightResult Fight(QuestKind kind, List<Humanoid> members)
		{
			List<Foe> foes = new List<Foe>();
			List<int> counts = new List<int>();
			foreach (KeyValuePair<string, int> fought in Fought(kind, members.Count))
			{
				Foe foe = FoeOf(fought.Key);
				if (foe == null)
				{
					return(null);
				}
				foes.Add(foe);
				counts.Add(fought.Value);
			}
			if (foes.Count == 0 || members.Count == 0)
			{
				return(null);
			}
			List<Fighter> fighters = new List<Fighter>();
			foreach (Humanoid humanoid in members)
			{
				fighters.Add(FighterOf(humanoid, foes));
			}
			List<Enemy> kinds = foes.ConvertAll((Foe foe) => foe.Enemy);
			return(QuestFight.Fight(kinds, QuestFight.Order(counts), PackOf(kind), fighters));
		}

		/// <summary>
		/// Lists the creatures a quest fights and how many of each - those it kills (see QuestLoot.Sources)
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="vikings">How many go</param>
		/// <returns>Each creature's prefab name and how many, none for a quest that kills nothing</returns>
		internal static List<KeyValuePair<string, int>> Fought(QuestKind kind, int vikings)
		{
			List<KeyValuePair<string, int>> fought = new List<KeyValuePair<string, int>>();
			foreach (QuestLoot.LootSource source in QuestLoot.Sources(kind))
			{
				if (source.Kill)
				{
					fought.Add(new KeyValuePair<string, int>(source.Prefab, source.CountFor(vikings)));
				}
			}
			return(fought);
		}

		/// <summary>
		/// Checks whether a viking would be expected to lose all its health in a quest's fight, alongside as many as the
		/// quest takes as strong as it is
		/// </summary>
		/// <param name="kind">The quest</param>
		/// <param name="humanoid">The viking</param>
		/// <param name="wanted">How many the quest takes</param>
		/// <returns>True when it would, or null when the quest kills nothing or a creature's prefab can't be read</returns>
		public static bool? ExpectedToDie(QuestKind kind, Humanoid humanoid, int wanted)
		{
			List<Humanoid> alike = new List<Humanoid>();
			for (int i = 0; i < Mathf.Max(1, wanted); i++)
			{
				alike.Add(humanoid);
			}
			FightResult fight = Fight(kind, alike);
			return((fight != null) ? fight.HarmShares[0] >= 1f : (bool?)null);
		}

		/// <summary>
		/// Weighs a viking up against each kind of enemy - what a swing of its deals it, through its resistances, the
		/// world's player damage rate and its world level's armor, and what each of its attacks deals the viking,
		/// through the viking's resistances and the world's enemy damage rate
		/// </summary>
		/// <param name="humanoid">The viking</param>
		/// <param name="foes">The kinds of enemy</param>
		/// <returns>The viking's side of the fight</returns>
		private static Fighter FighterOf(Humanoid humanoid, List<Foe> foes)
		{
			VikingCore core = humanoid.GetComponent<VikingCore>();
			Fighter fighter = new Fighter
			{
				Health = humanoid.GetMaxHealth(),
				Armor = humanoid.GetBodyArmor(),
				BlockPower = ((core != null) ? core.BlockPower() : 0f)
			};
			HitData.DamageModifiers resistances = humanoid.GetDamageModifiers();
			foreach (Foe foe in foes)
			{
				float swing = ((core != null) ? core.SwingDamage(foe.Resistances) : 0f);
				if (Game.m_worldLevel > 0 && Game.instance != null)
				{
					swing = HitData.DamageTypes.ApplyArmor(swing, Game.m_worldLevel * Game.instance.m_worldLevelEnemyBaseAC);
				}
				fighter.Swings.Add(swing * Game.m_playerDamageRate);
				List<Blow> blows = new List<Blow>();
				for (int i = 0; i < foe.Damages.Count; i++)
				{
					HitData hit = new HitData();
					hit.m_damage = foe.Damages[i];
					hit.ApplyResistance(resistances, out HitData.DamageModifier _);
					float total = MonsterThreat.SwingHit(hit.m_damage, foe.Multipliers[i]) * Game.m_enemyDamageRate;
					float overTime = (hit.m_damage.m_poison + hit.m_damage.m_fire + hit.m_damage.m_spirit) * foe.Multipliers[i] * Game.m_enemyDamageRate;
					blows.Add(new Blow { Direct = total - overTime, OverTime = overTime });
				}
				fighter.Blows.Add(blows);
			}
			return(fighter);
		}

		/// <summary>
		/// Gets a creature, read from its prefab the first time - its health, resistances and the attacks of the
		/// weapons it spawns with: those it always has, and those it may have, each by the chance it does - one of its
		/// random weapons, and the weapons of one of its random sets
		/// </summary>
		/// <param name="name">The creature's prefab name</param>
		/// <returns>The creature, or null when its prefab can't be read</returns>
		private static Foe FoeOf(string name)
		{
			if (known.TryGetValue(name, out Foe found))
			{
				return(found);
			}
			GameObject prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(name) : null);
			Humanoid humanoid = ((prefab != null) ? prefab.GetComponent<Humanoid>() : null);
			if (humanoid == null)
			{
				return(null);
			}
			MonsterAI ai = prefab.GetComponent<MonsterAI>();
			Foe foe = new Foe { Resistances = humanoid.m_damageModifiers };
			foe.Enemy.Health = humanoid.GetMaxHealthBase();
			foe.Enemy.MinInterval = ((ai != null) ? ai.m_minAttackInterval : 0f);
			AddAttacks(foe, humanoid.m_defaultItems, 1f);
			GameObject[] randomWeapons = (humanoid.m_randomWeapon ?? new GameObject[0]);
			foreach (GameObject weapon in randomWeapons)
			{
				AddAttacks(foe, new GameObject[] { weapon }, 1f / randomWeapons.Length);
			}
			Humanoid.ItemSet[] sets = (humanoid.m_randomSets ?? new Humanoid.ItemSet[0]);
			foreach (Humanoid.ItemSet set in sets)
			{
				AddAttacks(foe, set.m_items, 1f / sets.Length);
			}
			known[name] = foe;
			return(foe);
		}

		/// <summary>
		/// Adds the attacks of the weapons a creature may spawn with that harm its enemies - leaving out armor, attacks
		/// it aims at its friends, like a heal, and those that deal nothing, like a taunt
		/// </summary>
		/// <param name="foe">The creature</param>
		/// <param name="items">The items, or null</param>
		/// <param name="share">The chance it has them</param>
		private static void AddAttacks(Foe foe, GameObject[] items, float share)
		{
			foreach (GameObject given in (items ?? new GameObject[0]))
			{
				ItemDrop drop = ((given != null) ? given.GetComponent<ItemDrop>() : null);
				ItemDrop.ItemData weapon = ((drop != null) ? drop.m_itemData : null);
				Attack attack = ((weapon != null && weapon.IsWeapon()) ? weapon.m_shared.m_attack : null);
				if (attack == null || attack.m_attackType == Attack.AttackType.None || weapon.m_shared.m_aiTargetType != ItemDrop.ItemData.AiTarget.Enemy)
				{
					continue;
				}
				HitData.DamageTypes damage = weapon.GetDamage();
				if (MonsterThreat.SwingHit(damage, attack.m_damageMultiplier) <= 0f)
				{
					continue;
				}
				foe.Enemy.Attacks.Add(new EnemyAttack { Interval = weapon.m_shared.m_aiAttackInterval, Area = (attack.m_attackType == Attack.AttackType.Area), Share = share });
				foe.Damages.Add(damage);
				foe.Multipliers.Add(attack.m_damageMultiplier);
			}
		}
	}
}
