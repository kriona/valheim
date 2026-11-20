using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// What a dying creature rises as, handed from its death to its corpse and from its corpse to its zombie
	/// </summary>
	internal class PendingRise
	{
		public int PrefabHash;
		public int Level;

		/// <summary>
		/// When the corpse gets up, in ZNet time ticks
		/// </summary>
		public long RiseAt;

		/// <summary>
		/// The loot rolled when the creature died, which the zombie drops when it dies
		/// </summary>
		public List<KeyValuePair<GameObject, int>> Loot;

		/// <summary>
		/// Whether the creature was killed while cheating, so its loot is marked as cheated
		/// </summary>
		public bool LootCheated;

		/// <summary>
		/// The creature or player that dealt the killing blow, which the zombie goes after first, or none
		/// </summary>
		public ZDOID KillerId = ZDOID.None;
	}

	/// <summary>
	/// Decides which creatures rise, raises them and turns them into zombies
	/// </summary>
	/// <remarks>
	/// A zombie is the creature's own prefab marked with ZombieKey in its ZDO, so it's turned into a zombie again
	/// whenever it loads, on every client. A corpse that's going to rise is the creature's ragdoll marked with RiseAtKey
	/// and the other corpse keys, so it keeps waiting through a reload or a change of owner
	/// </remarks>
	internal static class Zombies
	{
		/// <summary>
		/// On a creature, that it's a zombie, and on a corpse, that it's a zombie's corpse
		/// </summary>
		public static readonly int ZombieKey = "kriona.ZombieApocalypse.zombie".GetStableHashCode();

		/// <summary>
		/// When a zombie starts getting up, in ZNet time ticks
		/// </summary>
		public static readonly int RiseStartKey = "kriona.ZombieApocalypse.riseStart".GetStableHashCode();

		/// <summary>
		/// When a zombie is standing and its AI takes over, in ZNet time ticks
		/// </summary>
		public static readonly int RiseEndKey = "kriona.ZombieApocalypse.riseEnd".GetStableHashCode();

		/// <summary>
		/// The share of its max health a zombie rises with, until its owner first sets its health - 0 once it's set
		/// </summary>
		public static readonly int RiseHealthKey = "kriona.ZombieApocalypse.riseHealth".GetStableHashCode();

		/// <summary>
		/// The fewest and most of its max health a zombie rises with, so it looks hurt as it gets up
		/// </summary>
		private const float RiseHealthMin = 0.25f;
		private const float RiseHealthMax = 0.75f;

		/// <summary>
		/// When a corpse's creature died, in ZNet time ticks
		/// </summary>
		public static readonly int DiedAtKey = "kriona.ZombieApocalypse.diedAt".GetStableHashCode();

		/// <summary>
		/// When a corpse starts getting up, in ZNet time ticks
		/// </summary>
		public static readonly int RiseAtKey = "kriona.ZombieApocalypse.riseAt".GetStableHashCode();

		/// <summary>
		/// The prefab of a corpse's creature
		/// </summary>
		public static readonly int RisePrefabKey = "kriona.ZombieApocalypse.risePrefab".GetStableHashCode();

		/// <summary>
		/// When a risen corpse is removed, in ZNet time ticks
		/// </summary>
		public static readonly int RemoveAtKey = "kriona.ZombieApocalypse.removeAt".GetStableHashCode();

		/// <summary>
		/// The zombie a corpse rose as - a ZDOID, which is stored under a pair of keys made from this name
		/// </summary>
		public const string ZombieIdKey = "kriona.ZombieApocalypse.zombieId";

		/// <summary>
		/// On a corpse and its zombie, who dealt the creature's killing blow - a ZDOID, stored like ZombieIdKey
		/// </summary>
		public const string KillerIdKey = "kriona.ZombieApocalypse.killerId";

		/// <summary>
		/// The animator parameter that puts a creature to sleep
		/// </summary>
		private const string SleepingParameter = "sleeping";

		private static readonly HashSet<string> excludedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly List<string> excludedPrefixes = new List<string>();

		/// <summary>
		/// The ExcludedCreatures text that excludedNames and excludedPrefixes were read from
		/// </summary>
		private static string excludedText;

		/// <summary>
		/// The network time all clients share
		/// </summary>
		/// <returns>The time in ticks, or 0 outside a world</returns>
		public static long Now()
		{
			return((ZNet.instance != null) ? ZNet.instance.GetTime().Ticks : 0L);
		}

		/// <summary>
		/// Converts seconds to ticks
		/// </summary>
		/// <param name="seconds">The number of seconds</param>
		/// <returns>The number of ticks</returns>
		public static long Ticks(float seconds)
		{
			return((long)(seconds * TimeSpan.TicksPerSecond));
		}

		/// <summary>
		/// Checks whether a creature is a zombie
		/// </summary>
		/// <param name="character">The creature</param>
		/// <returns>True when it's a zombie</returns>
		public static bool IsZombie(Character character)
		{
			ZNetView nview = character.m_nview;
			return(nview != null && nview.IsValid() && nview.GetZDO().GetBool(ZombieKey));
		}

		/// <summary>
		/// Checks whether a creature is a zombie that's still getting up
		/// </summary>
		/// <param name="character">The creature</param>
		/// <returns>True until the zombie is standing</returns>
		public static bool IsRising(Character character)
		{
			return(IsZombie(character) && Now() < character.m_nview.GetZDO().GetLong(RiseEndKey));
		}

		/// <summary>
		/// Decides whether a creature that's dying rises as a zombie, rolling RiseChance for one that can - never on a
		/// server without the mod
		/// </summary>
		/// <param name="character">The dying creature</param>
		/// <returns>True when it rises</returns>
		public static bool ShouldRise(Character character)
		{
			if (!ServerCheck.Active || character.IsPlayer() || character.IsBoss() || IsZombie(character))
			{
				return(false);
			}
			switch (character.GetFaction())
			{
				case Character.Faction.Undead:
				case Character.Faction.Players:
				case Character.Faction.PlayerSpawned:
				case Character.Faction.Boss:
				case Character.Faction.TrainingDummy:
					return(false);
			}
			BaseAI ai = character.GetComponent<BaseAI>();
			if (ai == null || (ai is AnimalAI && !Plugin.AnimalsRise.Value))
			{
				return(false);
			}
			// A zombie gets up from its creature's corpse, so a creature that leaves none (e.g., seekers) stays dead
			if (!LeavesCorpse(character))
			{
				return(false);
			}
			if (character.IsTamed() && !Plugin.TamedCreaturesRise.Value)
			{
				return(false);
			}
			if (IsExcluded(Utils.GetPrefabName(character.gameObject)))
			{
				return(false);
			}
			return(UnityEngine.Random.Range(0, 100) < Plugin.RiseChance.Value);
		}

		/// <summary>
		/// Records what a dying creature will rise as
		/// </summary>
		/// <param name="character">The dying creature</param>
		/// <returns>The rise</returns>
		public static PendingRise CreateRise(Character character)
		{
			PendingRise rise = new PendingRise();
			rise.PrefabHash = character.m_nview.GetZDO().GetPrefab();
			rise.Level = character.GetLevel();
			rise.RiseAt = Now() + Ticks(Plugin.RiseDelay.Value);
			rise.LootCheated = character.m_nview.GetZDO().IsCheated();
			return(rise);
		}

		/// <summary>
		/// Spawns a creature as a zombie, owned by this client
		/// </summary>
		/// <param name="rise">The creature to raise</param>
		/// <param name="position">Where it stands</param>
		/// <param name="rotation">Which way it faces</param>
		/// <param name="riseStart">When it starts getting up, in ZNet time ticks</param>
		/// <param name="riseEnd">When it's standing and its AI takes over, in ZNet time ticks</param>
		/// <returns>The zombie, or null when its prefab couldn't be spawned</returns>
		public static Character Raise(PendingRise rise, Vector3 position, Quaternion rotation, long riseStart, long riseEnd)
		{
			GameObject prefab = ZNetScene.instance.GetPrefab(rise.PrefabHash);
			if (prefab == null || prefab.GetComponent<Character>() == null)
			{
				return(null);
			}
			GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
			Character character = instance.GetComponent<Character>();
			ZNetView nview = character.m_nview;
			if (nview == null || !nview.IsValid())
			{
				return(null);
			}
			ZDO zdo = nview.GetZDO();
			zdo.Set(ZombieKey, true);
			zdo.Set(RiseStartKey, riseStart);
			zdo.Set(RiseEndKey, riseEnd);
			zdo.Set(RiseHealthKey, UnityEngine.Random.Range(RiseHealthMin, RiseHealthMax));
			character.SetLevel(rise.Level);
			if (rise.Loot != null && rise.Loot.Count > 0)
			{
				ZombieApocalypse.Loot.Write(zdo, ZombieApocalypse.Loot.ZombieKeys, rise.Loot);
				zdo.Set(ZombieApocalypse.Loot.CheatedKey, rise.LootCheated);
			}
			if (!rise.KillerId.IsNone())
			{
				zdo.Set(KillerIdKey, rise.KillerId);
			}
			MonsterAI ai = instance.GetComponent<MonsterAI>();
			if (ai != null && HasSleepAnimation(character))
			{
				// Set before the first frame so every client's animator starts in the pose it's given here
				bool sleeping = Plugin.WakeUpAnimation.Value;
				ai.m_sleeping = sleeping;
				zdo.Set(ZDOVars.s_sleeping, sleeping);
				ai.m_animator.SetBool(MonsterAI.s_sleeping, sleeping);
			}
			Apply(character);
			return(character);
		}

		/// <summary>
		/// Turns a creature into a zombie - its faction, hostility, loot, breeding, look, limp and getting up - and scales
		/// its health when this client owns it and it's unhurt
		/// </summary>
		/// <remarks>
		/// A zombie is in the Undead faction, which is hostile to players and every other faction but Demon, and
		/// never tamed, since a tamed creature isn't hostile to players whatever its faction. It doesn't flee, and it
		/// attacks player buildings without being provoked first. An animal, which has no attacks, rams its enemies
		/// through ZombieCharge instead of fleeing from them. It rolls no loot of its own, since it drops the
		/// loot its creature rolled when it first died
		/// </remarks>
		/// <param name="character">The creature, already marked with ZombieKey</param>
		public static void Apply(Character character)
		{
			character.m_faction = Character.Faction.Undead;
			if (Plugin.UndeadWeaknesses.Value)
			{
				ApplyUndeadWeaknesses(character);
			}
			ZNetView nview = character.m_nview;
			ZDO zdo = nview.GetZDO();
			if (character.m_tamed)
			{
				character.m_tamed = false;
				if (nview.IsOwner())
				{
					zdo.Set(ZDOVars.s_tamed, false);
				}
			}
			MonsterAI ai = character.GetComponent<MonsterAI>();
			if (ai != null)
			{
				ai.m_fleeIfNotAlerted = false;
				ai.m_fleeIfLowHealth = 0f;
				ai.m_fleeIfHurtWhenTargetCantBeReached = false;
				ai.m_aggravatable = false;
				ai.m_canBeAlerted = true;
			}
			else if (character.GetComponent<AnimalAI>() != null && character.GetComponent<ZombieCharge>() == null)
			{
				character.gameObject.AddComponent<ZombieCharge>();
			}
			CharacterDrop drop = character.GetComponent<CharacterDrop>();
			if (drop != null)
			{
				drop.m_drops = new List<CharacterDrop.Drop>();
			}
			Procreation procreation = character.GetComponent<Procreation>();
			if (procreation != null)
			{
				UnityEngine.Object.Destroy(procreation);
			}
			if (character.GetComponent<ZombieLook>() == null)
			{
				character.gameObject.AddComponent<ZombieLook>();
			}
			if (character.GetComponent<ZombieEyes>() == null)
			{
				character.gameObject.AddComponent<ZombieEyes>();
			}
			if (character.GetComponent<ZombieFlies>() == null)
			{
				character.gameObject.AddComponent<ZombieFlies>();
			}
			if (character.GetComponent<ZombieLimp>() == null)
			{
				character.gameObject.AddComponent<ZombieLimp>();
			}
			if (Now() < zdo.GetLong(RiseEndKey) && character.GetComponent<RiseBlend>() == null)
			{
				character.gameObject.AddComponent<RiseBlend>();
			}
			// The game sets a creature's max health from its level each time it loads. A viking has the player's
			// health instead, which it sets as it loads
			if (nview.IsOwner() && !ZombieViking.IsViking(character))
			{
				bool unhurt = character.GetHealth() >= character.GetMaxHealth();
				float health = character.GetMaxHealthBase() * character.GetLevel() * Plugin.ZombieHealth.Value / 100f;
				character.SetMaxHealth(health);
				SetHealth(character, health, unhurt);
			}
		}

		/// <summary>
		/// Sets a zombie's health on its owner after its max health - part of it the first time, as it rises, and all
		/// of it when it's unhurt
		/// </summary>
		/// <param name="character">The zombie</param>
		/// <param name="maxHealth">Its max health</param>
		/// <param name="unhurt">Whether it was unhurt before its max health was set</param>
		public static void SetHealth(Character character, float maxHealth, bool unhurt)
		{
			ZDO zdo = character.m_nview.GetZDO();
			float share = zdo.GetFloat(RiseHealthKey);
			if (share > 0f)
			{
				zdo.Set(RiseHealthKey, 0f);
				character.SetHealth(maxHealth * share);
			}
			else if (unhurt)
			{
				character.SetHealth(maxHealth);
			}
		}

		/// <summary>
		/// Makes a zombie take damage like the game's undead, starting from its creature's own resistances
		/// </summary>
		/// <remarks>
		/// Like every undead creature it's immune to poison. Living creatures are immune to spirit damage, but the dead
		/// aren't, and a zombie is weak to it, so silver weapons pay off. Its rotting flesh is weak to fire, unless the
		/// creature already resisted fire (e.g., a lava creature). And like skeletons and ghosts it shrugs off arrows,
		/// one step more resistant to pierce damage than the creature was. These change only this zombie's copy
		/// </remarks>
		/// <param name="character">The zombie</param>
		private static void ApplyUndeadWeaknesses(Character character)
		{
			character.m_damageModifiers.m_poison = HitData.DamageModifier.Immune;
			if (character.m_damageModifiers.m_spirit != HitData.DamageModifier.VeryWeak)
			{
				character.m_damageModifiers.m_spirit = HitData.DamageModifier.Weak;
			}
			switch (character.m_damageModifiers.m_fire)
			{
				case HitData.DamageModifier.Normal:
				case HitData.DamageModifier.SlightlyWeak:
				case HitData.DamageModifier.SlightlyResistant:
					character.m_damageModifiers.m_fire = HitData.DamageModifier.Weak;
					break;
			}
			switch (character.m_damageModifiers.m_pierce)
			{
				case HitData.DamageModifier.Normal:
					character.m_damageModifiers.m_pierce = HitData.DamageModifier.Resistant;
					break;
				case HitData.DamageModifier.SlightlyWeak:
				case HitData.DamageModifier.Weak:
					character.m_damageModifiers.m_pierce = HitData.DamageModifier.Normal;
					break;
				case HitData.DamageModifier.VeryWeak:
					character.m_damageModifiers.m_pierce = HitData.DamageModifier.Weak;
					break;
			}
		}

		/// <summary>
		/// Finds the ground under a corpse, or a point just under it when it's floating in water
		/// </summary>
		/// <param name="position">The middle of the corpse</param>
		/// <returns>Where a creature stands up from it</returns>
		public static Vector3 GroundUnder(Vector3 position)
		{
			float lowest = position.y - 0.5f;
			if (ZoneSystem.instance != null && ZoneSystem.instance.FindFloor(position, out float height))
			{
				position.y = Mathf.Max(height, lowest);
			}
			else
			{
				position.y = lowest;
			}
			return(position);
		}

		/// <summary>
		/// Checks whether a creature leaves a corpse when it dies
		/// </summary>
		/// <param name="character">The creature</param>
		/// <returns>True when one of its death effects is a ragdoll</returns>
		private static bool LeavesCorpse(Character character)
		{
			foreach (EffectList.EffectData effect in character.m_deathEffects.m_effectPrefabs)
			{
				if (effect.m_enabled && effect.m_prefab != null && effect.m_prefab.GetComponent<Ragdoll>() != null)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether a creature's animator has a sleeping state to wake up from
		/// </summary>
		/// <param name="character">The creature</param>
		/// <returns>True when it can sleep</returns>
		private static bool HasSleepAnimation(Character character)
		{
			Animator animator = character.m_animator;
			if (animator == null || animator.runtimeAnimatorController == null)
			{
				return(false);
			}
			foreach (AnimatorControllerParameter parameter in animator.parameters)
			{
				if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == SleepingParameter)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks a prefab name against ExcludedCreatures, reading the setting again when it has changed
		/// </summary>
		/// <param name="prefabName">The creature's prefab name</param>
		/// <returns>True when the creature never rises</returns>
		private static bool IsExcluded(string prefabName)
		{
			string text = Plugin.ExcludedCreatures.Value ?? "";
			if (text != excludedText)
			{
				excludedText = text;
				excludedNames.Clear();
				excludedPrefixes.Clear();
				foreach (string part in text.Split(','))
				{
					string name = part.Trim();
					if (name.EndsWith("*"))
					{
						excludedPrefixes.Add(name.Substring(0, name.Length - 1));
					}
					else if (name.Length > 0)
					{
						excludedNames.Add(name);
					}
				}
			}
			if (excludedNames.Contains(prefabName))
			{
				return(true);
			}
			foreach (string prefix in excludedPrefixes)
			{
				if (prefabName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					return(true);
				}
			}
			return(false);
		}
	}
}
