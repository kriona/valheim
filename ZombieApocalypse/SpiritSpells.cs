using System;
using System.Collections.Generic;
using HarmonyLib;
using Kriona.Vikings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ZombieApocalypse
{
	/// <summary>
	/// Bright white spirit spells that living dvergr mages cast - a bolt and a nova - put first against the undead
	/// </summary>
	/// <remarks>
	/// The spells are copies of the ice mage's ice bolt and frost nova, with their projectile, the burst where the bolt
	/// lands and the ring the nova casts recolored white at the same brightness, and spirit damage in place of frost.
	/// The effects' textures are turned gray too, since some of them are blue themselves. Every client gives a mage its
	/// items as it loads, so each gives a mage that isn't a zombie the spells along with them. A mage picks between its
	/// spells at random, except that against an undead target - one of the Undead faction or weak to spirit damage,
	/// which zombies are - a spirit spell comes first whenever one is ready. The spell items are added to ObjectDB too,
	/// so a client whose mage holds one can name it to the others
	/// </remarks>
	internal static class SpiritSpells
	{
		public const string BoltName = "DvergerStaffSpirit_bolt";
		public const string BoltProjectileName = "DvergerStaffSpirit_projectile";
		public const string BoltHitName = "fx_DvergerMage_Spirit_hit";
		public const string NovaName = "DvergerStaffSpiritNova";
		public const string NovaAoeName = "DvergerStaffSpiritNova_aoe";
		public const string NovaRingName = "fx_DvergerMage_SpiritNova_ring";

		/// <summary>
		/// The mage and spells the spirit spells are copied from
		/// </summary>
		private const string SourceMage = "DvergerMageIce";
		private const string SourceBolt = "DvergerStaffIce_icebolt";
		private const string SourceNova = "DvergerStaffNova";

		/// <summary>
		/// The mages that cast the spells
		/// </summary>
		private static readonly HashSet<string> Mages = new HashSet<string> { "DvergerMage", "DvergerMageFire", "DvergerMageIce", "DvergerMageSupport" };

		/// <summary>
		/// The spirit damage the bolt and the nova do, before the target's resistances
		/// </summary>
		private const float BoltDamage = 150f;
		private const float NovaDamage = 112f;

		/// <summary>
		/// How much brighter the spells' lights and glow are than the frost spells'
		/// </summary>
		private const float GlowBoost = 1.5f;

		/// <summary>
		/// The spell items, and the projectiles and effects they use, which go in the world's prefabs
		/// </summary>
		private static readonly List<GameObject> spells = new List<GameObject>();
		private static readonly List<GameObject> prefabs = new List<GameObject>();

		private static bool built;
		private static GameObject prefabHolder;

		/// <summary>
		/// Adds the spells' projectiles and effects to a world's prefabs, and the spells to ObjectDB, building them the
		/// first time
		/// </summary>
		/// <param name="scene">The world's ZNetScene, as it wakes</param>
		public static void Register(ZNetScene scene)
		{
			Build(scene);
			foreach (GameObject prefab in prefabs)
			{
				int hash = prefab.name.GetStableHashCode();
				if (!scene.m_namedPrefabs.ContainsKey(hash))
				{
					scene.m_prefabs.Add(prefab);
					scene.m_namedPrefabs.Add(hash, prefab);
				}
			}
			RegisterItems(ObjectDB.instance);
		}

		/// <summary>
		/// Lists the names of the spell items and the projectiles and effects they use, once they're built
		/// </summary>
		/// <returns>The names</returns>
		public static List<string> Names()
		{
			List<string> names = new List<string>();
			foreach (GameObject built in spells)
			{
				if (built != null)
				{
					names.Add(built.name);
				}
			}
			foreach (GameObject built in prefabs)
			{
				if (built != null)
				{
					names.Add(built.name);
				}
			}
			return(names);
		}

		/// <summary>
		/// Adds the spells to an ObjectDB's items, once they're built
		/// </summary>
		/// <param name="db">The ObjectDB, or null</param>
		public static void RegisterItems(ObjectDB db)
		{
			if (db == null)
			{
				return;
			}
			foreach (GameObject spell in spells)
			{
				int hash = spell.name.GetStableHashCode();
				if (!db.m_itemByHash.ContainsKey(hash))
				{
					db.m_items.Add(spell);
					db.m_itemByHash[hash] = spell;
				}
			}
		}

		/// <summary>
		/// Gives a living dvergr mage the spells, after the game has given it its own
		/// </summary>
		/// <param name="humanoid">The creature just given its items</param>
		public static void Give(Humanoid humanoid)
		{
			if (spells.Count == 0 || !Mages.Contains(Utils.GetPrefabName(humanoid.gameObject)) || ZombieState.IsZombie(humanoid))
			{
				return;
			}
			foreach (GameObject spell in spells)
			{
				humanoid.GiveDefaultItem(spell);
			}
		}

		/// <summary>
		/// Finds a mage's spirit spells, when its target is undead
		/// </summary>
		/// <param name="humanoid">The creature choosing a weapon</param>
		/// <param name="target">Its target, or null</param>
		/// <returns>The spells to put first, or null when there are none</returns>
		public static List<ItemDrop.ItemData> FindForUndead(Humanoid humanoid, Character target)
		{
			if (spells.Count == 0 || target == null || !IsUndead(target))
			{
				return(null);
			}
			List<ItemDrop.ItemData> found = null;
			foreach (ItemDrop.ItemData carried in humanoid.GetInventory().GetAllItems())
			{
				if (carried.m_dropPrefab != null && spells.Contains(carried.m_dropPrefab))
				{
					if (found == null)
					{
						found = new List<ItemDrop.ItemData>();
					}
					found.Add(carried);
				}
			}
			return(found);
		}

		/// <summary>
		/// Checks whether a creature is undead - of the Undead faction, as zombies are, or weak to spirit damage
		/// </summary>
		/// <param name="character">The creature</param>
		/// <returns>True when it's undead</returns>
		public static bool IsUndead(Character character)
		{
			HitData.DamageModifier spirit = character.m_damageModifiers.m_spirit;
			return(character.m_faction == Character.Faction.Undead || spirit == HitData.DamageModifier.Weak || spirit == HitData.DamageModifier.VeryWeak);
		}

		/// <summary>
		/// Builds the spells from the ice mage's, once, logging any that can't be
		/// </summary>
		/// <remarks>
		/// The copies sit under an inactive object, so none of their parts wake while they're changed, and they're kept
		/// across worlds
		/// </remarks>
		/// <param name="scene">The world's ZNetScene</param>
		private static void Build(ZNetScene scene)
		{
			if (built)
			{
				return;
			}
			built = true;
			prefabHolder = new GameObject("ZombieApocalypse spirit spells");
			prefabHolder.SetActive(false);
			Object.DontDestroyOnLoad(prefabHolder);
			if (!BuildBolt(FindSourceSpell(scene, SourceBolt)))
			{
				Plugin.Log.LogWarning("Couldn't build the dvergr spirit bolt - the " + SourceMage + "'s " + SourceBolt + " is missing");
			}
			if (!BuildNova(FindSourceSpell(scene, SourceNova)))
			{
				Plugin.Log.LogWarning("Couldn't build the dvergr spirit nova - the " + SourceMage + "'s " + SourceNova + " is missing");
			}
		}

		/// <summary>
		/// Builds the spirit bolt from the ice bolt - the spell, its projectile and the burst where it lands
		/// </summary>
		/// <param name="source">The ice bolt, or null</param>
		/// <returns>True when it's built</returns>
		private static bool BuildBolt(GameObject source)
		{
			ItemDrop drop = ((source != null) ? source.GetComponent<ItemDrop>() : null);
			GameObject sourceProjectile = ((drop != null) ? drop.m_itemData.m_shared.m_attack.m_attackProjectile : null);
			if (sourceProjectile == null || sourceProjectile.GetComponent<Projectile>() == null)
			{
				return(false);
			}
			GameObject projectile = CopyEffect(sourceProjectile, BoltProjectileName);
			EffectList hitEffects = projectile.GetComponent<Projectile>().m_hitEffects;
			ReplaceFirst(hitEffects, BoltHitName);
			GameObject spell = CopySpell(source, BoltName, "Spirit bolt", BoltDamage);
			spell.GetComponent<ItemDrop>().m_itemData.m_shared.m_attack.m_attackProjectile = projectile;
			return(true);
		}

		/// <summary>
		/// Builds the spirit nova from the frost nova - the spell, the burst around the mage that does the damage, and
		/// the ring the spell casts
		/// </summary>
		/// <param name="source">The frost nova, or null</param>
		/// <returns>True when it's built</returns>
		private static bool BuildNova(GameObject source)
		{
			ItemDrop drop = ((source != null) ? source.GetComponent<ItemDrop>() : null);
			GameObject sourceAoe = ((drop != null) ? drop.m_itemData.m_shared.m_attack.m_attackProjectile : null);
			if (sourceAoe == null || sourceAoe.GetComponent<Aoe>() == null)
			{
				return(false);
			}
			GameObject aoe = Copy(sourceAoe, NovaAoeName);
			prefabs.Add(aoe);
			// The burst does its own damage rather than the spell's
			Aoe burst = aoe.GetComponent<Aoe>();
			burst.m_damage = new HitData.DamageTypes();
			burst.m_damage.m_spirit = NovaDamage;
			GameObject spell = CopySpell(source, NovaName, "Spirit nova", NovaDamage);
			ItemDrop.ItemData.SharedData shared = spell.GetComponent<ItemDrop>().m_itemData.m_shared;
			shared.m_attack.m_attackProjectile = aoe;
			ReplaceFirst(shared.m_startEffect, NovaRingName);
			return(true);
		}

		/// <summary>
		/// Finds one of the ice mage's spells
		/// </summary>
		/// <param name="scene">The world's ZNetScene</param>
		/// <param name="name">The spell's prefab name</param>
		/// <returns>The spell's prefab, or null</returns>
		private static GameObject FindSourceSpell(ZNetScene scene, string name)
		{
			GameObject mage = scene.GetPrefab(SourceMage);
			Humanoid humanoid = ((mage != null) ? mage.GetComponent<Humanoid>() : null);
			if (humanoid == null)
			{
				return(null);
			}
			foreach (GameObject carried in humanoid.m_defaultItems)
			{
				if (carried != null && carried.name == name)
				{
					return(carried);
				}
			}
			return(null);
		}

		/// <summary>
		/// Copies a spell with spirit damage in place of its own, and adds it to the spells mages are given
		/// </summary>
		/// <param name="source">The spell</param>
		/// <param name="name">The copy's prefab name</param>
		/// <param name="displayName">The copy's name</param>
		/// <param name="damage">Its spirit damage</param>
		/// <returns>The copy</returns>
		private static GameObject CopySpell(GameObject source, string name, string displayName, float damage)
		{
			GameObject spell = Copy(source, name);
			ItemDrop.ItemData.SharedData shared = spell.GetComponent<ItemDrop>().m_itemData.m_shared;
			shared.m_name = displayName;
			shared.m_damages = new HitData.DamageTypes();
			shared.m_damages.m_spirit = damage;
			shared.m_aiPrioritized = false;
			spells.Add(spell);
			return(spell);
		}

		/// <summary>
		/// Swaps the first effect in a list for a white copy of it
		/// </summary>
		/// <param name="effects">The list, changed in place</param>
		/// <param name="name">The copy's prefab name</param>
		private static void ReplaceFirst(EffectList effects, string name)
		{
			foreach (EffectList.EffectData effect in effects.m_effectPrefabs)
			{
				if (effect.m_prefab != null)
				{
					effect.m_prefab = CopyEffect(effect.m_prefab, name);
					return;
				}
			}
		}

		/// <summary>
		/// Copies an effect, turns it white and adds it to the world's prefabs, since the effects have ZNetViews
		/// </summary>
		/// <param name="source">The effect</param>
		/// <param name="name">The copy's prefab name</param>
		/// <returns>The copy</returns>
		private static GameObject CopyEffect(GameObject source, string name)
		{
			GameObject copy = Copy(source, name);
			SpiritWhite.Whiten(copy, GlowBoost);
			prefabs.Add(copy);
			return(copy);
		}

		/// <summary>
		/// Copies a prefab under the inactive holder with a new name
		/// </summary>
		/// <param name="source">The prefab</param>
		/// <param name="name">The copy's name</param>
		/// <returns>The copy</returns>
		private static GameObject Copy(GameObject source, string name)
		{
			GameObject copy = Object.Instantiate(source, prefabHolder.transform, false);
			copy.name = name;
			return(copy);
		}
	}

	/// <summary>
	/// Gives living dvergr mages the spirit spells along with their own items
	/// </summary>
	[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GiveDefaultItems))]
	public static class SpiritSpellsGivePatch
	{
		/// <summary>
		/// Adds the spells once the game has given the mage its items
		/// </summary>
		/// <param name="__instance">The creature given its items</param>
		private static void Postfix(Humanoid __instance)
		{
			SpiritSpells.Give(__instance);
		}
	}

	/// <summary>
	/// Puts a mage's spirit spells first against an undead target
	/// </summary>
	[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipBestWeapon))]
	public static class SpiritSpellsChoicePatch
	{
		/// <summary>
		/// Marks the spirit spells as the mage's first choice while it picks, when its target is undead
		/// </summary>
		/// <remarks>
		/// The mark is on the spells' shared data, so it's taken off again as soon as the mage has picked. The game only
		/// considers a spell that's off cooldown with the target in its range, so the nova is picked up close and the
		/// bolt farther out
		/// </remarks>
		/// <param name="__instance">The creature choosing a weapon</param>
		/// <param name="targetCreature">Its target, or null</param>
		/// <param name="__state">The spells marked, or null</param>
		private static void Prefix(Humanoid __instance, Character targetCreature, out List<ItemDrop.ItemData> __state)
		{
			__state = SpiritSpells.FindForUndead(__instance, targetCreature);
			if (__state == null)
			{
				return;
			}
			foreach (ItemDrop.ItemData spell in __state)
			{
				spell.m_shared.m_aiPrioritized = true;
			}
		}

		/// <summary>
		/// Takes the mark off the spells, whether or not the pick finished
		/// </summary>
		/// <param name="__state">The spells marked, or null</param>
		/// <param name="__exception">An exception the pick threw, or null</param>
		/// <returns>The exception, so it's still thrown</returns>
		private static Exception Finalizer(List<ItemDrop.ItemData> __state, Exception __exception)
		{
			if (__state != null)
			{
				foreach (ItemDrop.ItemData spell in __state)
				{
					spell.m_shared.m_aiPrioritized = false;
				}
			}
			return(__exception);
		}
	}

	/// <summary>
	/// Adds the spirit spells to ObjectDB as it wakes, when they're already built
	/// </summary>
	[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
	public static class SpiritSpellsObjectDBPatch
	{
		/// <summary>
		/// Adds the spells to the items
		/// </summary>
		/// <param name="__instance">The ObjectDB</param>
		private static void Postfix(ObjectDB __instance)
		{
			SpiritSpells.RegisterItems(__instance);
		}
	}
}
