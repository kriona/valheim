using System;
using System.Collections.Generic;
using Kriona.Vikings;
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
		/// The seed the creature picked its random items with (e.g., whether a troll carries a log), or 0 for none
		/// </summary>
		public int Seed;

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
	/// Decides which creatures rise, raises them and turns them into zombies, marking them as ZombieState describes
	/// </summary>
	internal static class Zombies
	{
		/// <summary>
		/// The fewest and most of its max health a zombie rises with, so it looks hurt as it gets up
		/// </summary>
		private const float RiseHealthMin = 0.25f;
		private const float RiseHealthMax = 0.75f;

		/// <summary>
		/// The animator parameter that puts a creature to sleep
		/// </summary>
		private const string SleepingParameter = "sleeping";

		private static HashSet<string> excludedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static List<string> excludedPrefixes = new List<string>();

		/// <summary>
		/// The ExcludedCreatures text that excludedNames and excludedPrefixes were read from
		/// </summary>
		private static string excludedText;

		/// <summary>
		/// Decides whether a creature that's dying rises as a zombie, rolling RiseChance for one that can - never on a
		/// server without the mod, when it was killed by spirit damage, or when it took FireStaysDead of its damage as
		/// fire
		/// </summary>
		/// <param name="character">The dying creature</param>
		/// <returns>True when it rises</returns>
		public static bool ShouldRise(Character character)
		{
			if (!ServerCheck.Active || character.IsPlayer() || character.IsBoss() || ZombieState.IsZombie(character))
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
			if (SpiritKills.LastHitWasSpirit(character) || FireKills.Burned(character))
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
			// A living viking - PC, NPC or dvergr - rises as a zombie viking
			rise.PrefabHash = (VikingCore.IsLivingViking(character) ? VikingCore.ZombiePrefabHash : character.m_nview.GetZDO().GetPrefab());
			rise.Level = character.GetLevel();
			Humanoid humanoid = character as Humanoid;
			rise.Seed = ((humanoid != null) ? humanoid.m_seed : 0);
			rise.RiseAt = NetTime.Now() + NetTime.Ticks(Plugin.RiseDelay.Value);
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
			zdo.Set(ZombieState.ZombieKey, true);
			zdo.Set(ZombieState.RiseStartKey, riseStart);
			zdo.Set(ZombieState.RiseEndKey, riseEnd);
			zdo.Set(ZombieState.RiseHealthKey, UnityEngine.Random.Range(RiseHealthMin, RiseHealthMax));
			Humanoid humanoid = character as Humanoid;
			if (humanoid != null && rise.Seed != 0)
			{
				// Set before Start, where the creature picks its random items, and in the ZDO for other clients' copies
				humanoid.m_seed = rise.Seed;
				zdo.Set(ZDOVars.s_seed, rise.Seed);
			}
			character.SetLevel(rise.Level);
			if (rise.Loot != null && rise.Loot.Count > 0)
			{
				ZombieApocalypse.Loot.Write(zdo, ZombieApocalypse.Loot.ZombieKeys, rise.Loot);
				zdo.Set(ZombieApocalypse.Loot.CheatedKey, rise.LootCheated);
			}
			if (!rise.KillerId.IsNone())
			{
				zdo.Set(ZombieState.KillerIdKey, rise.KillerId);
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
		/// Turns a creature into a zombie - its faction, hostility, loot, breeding, look, limp, lurch and getting up - and scales
		/// its health when this client owns it and it's unhurt
		/// </summary>
		/// <remarks>
		/// A zombie is in the Undead faction, which is hostile to players and every other faction but Demon, and
		/// never tamed, since a tamed creature isn't hostile to players whatever its faction. It doesn't flee, and it
		/// attacks player buildings without being provoked first. An animal, which has no attacks, rams its enemies
		/// through ZombieCharge instead of fleeing from them. It rolls no loot of its own, since it drops the
		/// loot its creature rolled when it first died
		/// </remarks>
		/// <param name="character">The creature, already marked with ZombieState.ZombieKey</param>
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
			if (NetTime.Now() < zdo.GetLong(ZombieState.RiseEndKey) && character.GetComponent<RiseBlend>() == null)
			{
				character.gameObject.AddComponent<RiseBlend>();
			}
			if (character.GetComponent<ZombieLurch>() == null)
			{
				character.gameObject.AddComponent<ZombieLurch>();
			}
			// The game sets a creature's max health from its level each time it loads. A viking has the player's
			// health instead, which it sets as it loads
			if (nview.IsOwner() && !VikingCore.IsZombieViking(character))
			{
				bool unhurt = character.GetHealth() >= character.GetMaxHealth();
				float health = character.GetMaxHealthBase() * character.GetLevel() * Plugin.ZombieHealth.Value / 100f;
				character.SetMaxHealth(health);
				ZombieState.SetHealth(character, health, unhurt);
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
			character.m_damageModifiers = UndeadModifiers(character.m_damageModifiers);
		}

		/// <summary>
		/// Works out how a zombie takes damage from how its creature did (see ApplyUndeadWeaknesses) - immune to poison,
		/// weak to spirit, weak to fire unless the creature resisted it or was already weak to it, and a step more
		/// resistant to pierce
		/// </summary>
		/// <param name="modifiers">The creature's resistances</param>
		/// <returns>The zombie's</returns>
		internal static HitData.DamageModifiers UndeadModifiers(HitData.DamageModifiers modifiers)
		{
			modifiers.m_poison = HitData.DamageModifier.Immune;
			if (modifiers.m_spirit != HitData.DamageModifier.VeryWeak)
			{
				modifiers.m_spirit = HitData.DamageModifier.Weak;
			}
			switch (modifiers.m_fire)
			{
				case HitData.DamageModifier.Normal:
				case HitData.DamageModifier.SlightlyWeak:
				case HitData.DamageModifier.SlightlyResistant:
					modifiers.m_fire = HitData.DamageModifier.Weak;
					break;
			}
			switch (modifiers.m_pierce)
			{
				case HitData.DamageModifier.Normal:
					modifiers.m_pierce = HitData.DamageModifier.Resistant;
					break;
				case HitData.DamageModifier.SlightlyWeak:
				case HitData.DamageModifier.Weak:
					modifiers.m_pierce = HitData.DamageModifier.Normal;
					break;
				case HitData.DamageModifier.VeryWeak:
					modifiers.m_pierce = HitData.DamageModifier.Weak;
					break;
			}
			return(modifiers);
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
				ParseExcluded(text, out excludedNames, out excludedPrefixes);
			}
			return(Matches(prefabName, excludedNames, excludedPrefixes));
		}

		/// <summary>
		/// Reads an ExcludedCreatures list - comma-separated prefab names, where one ending in * is the start of the
		/// names it matches, so a lone * matches every creature
		/// </summary>
		/// <param name="text">The list</param>
		/// <param name="names">The whole names in it, matched whatever their case</param>
		/// <param name="prefixes">The starts of names in it, without their *</param>
		internal static void ParseExcluded(string text, out HashSet<string> names, out List<string> prefixes)
		{
			names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			prefixes = new List<string>();
			foreach (string part in text.Split(','))
			{
				string name = part.Trim();
				if (name.EndsWith("*"))
				{
					prefixes.Add(name.Substring(0, name.Length - 1).Trim());
				}
				else if (name.Length > 0)
				{
					names.Add(name);
				}
			}
		}

		/// <summary>
		/// Checks a prefab name against the names and starts of names read from an ExcludedCreatures list, whatever their
		/// case (see ParseExcluded)
		/// </summary>
		/// <param name="prefabName">The creature's prefab name</param>
		/// <param name="names">The whole names</param>
		/// <param name="prefixes">The starts of names</param>
		/// <returns>True when it matches one</returns>
		internal static bool Matches(string prefabName, HashSet<string> names, List<string> prefixes)
		{
			if (names.Contains(prefabName))
			{
				return(true);
			}
			foreach (string prefix in prefixes)
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
