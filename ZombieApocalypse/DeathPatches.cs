using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// The creature dying on this client and what becomes of it and its loot, while its OnDeath runs
	/// </summary>
	internal static class Dying
	{
		/// <summary>
		/// Whether a creature is dying on its owner, so a corpse being set up is its corpse
		/// </summary>
		public static bool Active;

		/// <summary>
		/// Whether the dying creature is a zombie, so its corpse is tinted and drops the loot it carries
		/// </summary>
		public static bool IsZombie;

		/// <summary>
		/// The dying creature, whose held items its corpse keeps
		/// </summary>
		public static Character Creature;

		/// <summary>
		/// What the dying creature rises as, or null when it stays dead
		/// </summary>
		public static PendingRise Rise;

		/// <summary>
		/// The ZDO of the dying creature's corpse, once the corpse is set up
		/// </summary>
		public static ZDO Corpse;

		/// <summary>
		/// Whether the corpse holds the creature's loot, so it isn't also dropped on the spot or rolled again
		/// </summary>
		public static bool CorpseHasLoot;

		/// <summary>
		/// The loot a dying zombie carries from its creature's first death
		/// </summary>
		public static List<KeyValuePair<GameObject, int>> ZombieLoot;

		/// <summary>
		/// Whether the zombie's loot is marked as cheated
		/// </summary>
		public static bool ZombieLootCheated;

		/// <summary>
		/// Where loot is dropped when the dying creature leaves no corpse to hold it
		/// </summary>
		public static Vector3 DropPosition;

		/// <summary>
		/// Forgets the last creature that died
		/// </summary>
		public static void Reset()
		{
			Active = false;
			IsZombie = false;
			Creature = null;
			Rise = null;
			Corpse = null;
			CorpseHasLoot = false;
			ZombieLoot = null;
			ZombieLootCheated = false;
		}
	}

	/// <summary>
	/// Decides whether a creature dying on its owner rises, and carries the loot from its first death to its
	/// zombie's death
	/// </summary>
	/// <remarks>
	/// OnDeath creates the creature's corpse partway through and destroys the creature at the end, so everything
	/// needed from the creature is read first and the corpse is marked by CorpseSetupPatch. A creature that rises
	/// has its own drop turned off, and its loot is kept in its corpse for the zombie to carry
	/// </remarks>
	[HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
	public static class DeathPatch
	{
		/// <summary>
		/// Records what the creature rises as and holds back its loot, or reads the loot a dying zombie carries
		/// </summary>
		/// <param name="__instance">The dying creature</param>
		private static void Prefix(Character __instance)
		{
			Dying.Reset();
			ZNetView nview = __instance.m_nview;
			if (nview == null || !nview.IsValid() || !nview.IsOwner())
			{
				return;
			}
			Dying.Active = true;
			Dying.IsZombie = Zombies.IsZombie(__instance);
			Dying.Creature = __instance;
			Dying.DropPosition = __instance.GetCenterPoint();
			if (Dying.IsZombie)
			{
				ZDO zdo = nview.GetZDO();
				Dying.ZombieLoot = Loot.Read(zdo, Loot.ZombieKeys);
				Dying.ZombieLootCheated = zdo.GetBool(Loot.CheatedKey);
			}
			else if (Zombies.ShouldRise(__instance))
			{
				Dying.Rise = Zombies.CreateRise(__instance);
				Character killer = __instance.m_lastHit?.GetAttacker();
				if (killer != null)
				{
					Dying.Rise.KillerId = killer.GetZDOID();
				}
				CharacterDrop drop = __instance.GetComponent<CharacterDrop>();
				if (drop != null)
				{
					drop.SetDropsEnabled(false);
				}
			}
		}

		/// <summary>
		/// Drops a zombie's loot when it left no corpse to drop it, and keeps a rising creature's loot for its zombie,
		/// or drops it when no corpse was set up to rise after all
		/// </summary>
		/// <param name="__instance">The creature that died</param>
		private static void Postfix(Character __instance)
		{
			if (!Dying.Active)
			{
				return;
			}
			if (Dying.IsZombie && !Dying.CorpseHasLoot && Dying.ZombieLoot != null && Dying.ZombieLoot.Count > 0)
			{
				CharacterDrop.DropItems(Dying.ZombieLoot, Dying.DropPosition, 0.5f, Dying.ZombieLootCheated);
			}
			PendingRise rise = Dying.Rise;
			if (rise != null)
			{
				if (!Dying.CorpseHasLoot)
				{
					// The corpse doesn't keep loot, or there's no corpse, so the drop the game skipped is rolled here
					CharacterDrop drop = __instance.GetComponent<CharacterDrop>();
					List<KeyValuePair<GameObject, int>> loot = ((drop != null) ? drop.GenerateDropList() : new List<KeyValuePair<GameObject, int>>());
					if (Dying.Corpse != null)
					{
						Loot.Write(Dying.Corpse, Loot.CorpseKeys, loot);
					}
					else if (loot.Count > 0)
					{
						CharacterDrop.DropItems(loot, Dying.DropPosition, 0.5f, rise.LootCheated);
					}
				}
			}
			Dying.Reset();
		}
	}

	/// <summary>
	/// Notes whether each hit that becomes a creature's last dealt spirit damage, so a creature killed by one stays
	/// dead
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage))]
	public static class SpiritHitPatch
	{
		/// <summary>
		/// Reads the hit's spirit damage before the creature's resistances take it away
		/// </summary>
		/// <param name="__instance">The creature being hit</param>
		/// <param name="hit">The hit</param>
		/// <param name="__state">The creature's last hit before this one, and whether this one deals spirit damage</param>
		private static void Prefix(Character __instance, HitData hit, out (HitData lastHit, bool spirit) __state)
		{
			__state = (__instance.m_lastHit, hit != null && hit.m_damage.m_spirit > 0f);
		}

		/// <summary>
		/// Keeps whether the hit dealt spirit damage when the game made it the creature's last
		/// </summary>
		/// <param name="__instance">The creature that was hit</param>
		/// <param name="__state">The creature's last hit before this one, and whether this one deals spirit damage</param>
		private static void Postfix(Character __instance, (HitData lastHit, bool spirit) __state)
		{
			if (__instance.m_lastHit != __state.lastHit)
			{
				SpiritKills.SetLastHit(__instance, __state.spirit);
			}
		}
	}

	/// <summary>
	/// Marks the corpse of a creature that rises, or of a zombie, as it's set up on the owner
	/// </summary>
	[HarmonyPatch(typeof(Ragdoll), nameof(Ragdoll.Setup))]
	public static class CorpseSetupPatch
	{
		/// <summary>
		/// Stores the rise in the corpse's ZDO and starts it waiting, or tints a zombie's corpse, keeps its eyes
		/// glowing and gives it the zombie's loot to drop
		/// </summary>
		/// <param name="__instance">The corpse</param>
		private static void Postfix(Ragdoll __instance)
		{
			if (!Dying.Active || Dying.Corpse != null)
			{
				return;
			}
			ZDO zdo = __instance.m_nview.GetZDO();
			if (Dying.IsZombie)
			{
				Dying.Corpse = zdo;
				zdo.Set(Zombies.ZombieKey, true);
				if (__instance.GetComponent<ZombieLook>() == null)
				{
					__instance.gameObject.AddComponent<ZombieLook>();
				}
				if (__instance.GetComponent<ZombieEyes>() == null)
				{
					__instance.gameObject.AddComponent<ZombieEyes>();
				}
				if (CorpseItems.Store(zdo, Dying.Creature))
				{
					__instance.gameObject.AddComponent<CorpseItems>();
				}
				// A corpse with m_dropItems drops the loot in its ZDO when the game removes it
				if (__instance.m_dropItems && Dying.ZombieLoot != null)
				{
					Loot.Write(zdo, Loot.CorpseKeys, Dying.ZombieLoot);
					if (Dying.ZombieLootCheated)
					{
						zdo.SetCheated(true);
					}
					Dying.CorpseHasLoot = true;
				}
				return;
			}
			PendingRise rise = Dying.Rise;
			if (rise == null)
			{
				return;
			}
			Dying.Corpse = zdo;
			// Setup has already stored the creature's loot in a corpse with m_dropItems
			Dying.CorpseHasLoot = __instance.m_dropItems;
			zdo.Set(Zombies.DiedAtKey, Zombies.Now());
			zdo.Set(Zombies.RiseAtKey, rise.RiseAt);
			zdo.Set(Zombies.RisePrefabKey, rise.PrefabHash);
			if (rise.Seed != 0)
			{
				zdo.Set(Zombies.RiseSeedKey, rise.Seed);
			}
			if (!rise.KillerId.IsNone())
			{
				zdo.Set(Zombies.KillerIdKey, rise.KillerId);
			}
			if (CorpseItems.Store(zdo, Dying.Creature))
			{
				__instance.gameObject.AddComponent<CorpseItems>();
			}
			ZombieViking.HandToCorpse(Dying.Creature, zdo);
			__instance.gameObject.AddComponent<RisingCorpse>();
		}
	}

	/// <summary>
	/// Sets up a corpse loaded from its ZDO - on another client, or after a reload - as rising or as a zombie's
	/// </summary>
	[HarmonyPatch(typeof(Ragdoll), nameof(Ragdoll.Awake))]
	public static class CorpseAwakePatch
	{
		/// <summary>
		/// Adds the rising, or the tint and glowing eyes, the corpse's ZDO asks for
		/// </summary>
		/// <param name="__instance">The corpse</param>
		private static void Postfix(Ragdoll __instance)
		{
			ZNetView nview = __instance.m_nview;
			if (nview == null || !nview.IsValid())
			{
				return;
			}
			ZDO zdo = nview.GetZDO();
			if (zdo.GetLong(Zombies.RiseAtKey) != 0L)
			{
				__instance.gameObject.AddComponent<RisingCorpse>();
			}
			else if (zdo.GetBool(Zombies.ZombieKey))
			{
				__instance.gameObject.AddComponent<ZombieLook>();
				__instance.gameObject.AddComponent<ZombieEyes>();
			}
			if (CorpseItems.Has(zdo))
			{
				__instance.gameObject.AddComponent<CorpseItems>();
			}
		}
	}

	/// <summary>
	/// Keeps a rising corpse until its zombie is standing, since RisingCorpse removes it then, and lets a zombie's
	/// corpse leave a poison cloud as it rots away
	/// </summary>
	[HarmonyPatch(typeof(Ragdoll), nameof(Ragdoll.DestroyNow))]
	public static class CorpseRemovePatch
	{
		/// <summary>
		/// Skips the game's removal of a rising corpse, or maybe leaves a poison cloud where a zombie's lies
		/// </summary>
		/// <param name="__instance">The corpse</param>
		/// <returns>False for a rising corpse, to skip the removal</returns>
		private static bool Prefix(Ragdoll __instance)
		{
			if (__instance.GetComponent<RisingCorpse>() != null)
			{
				return(false);
			}
			PoisonCloud.OnCorpseRemoved(__instance);
			return(true);
		}
	}
}
