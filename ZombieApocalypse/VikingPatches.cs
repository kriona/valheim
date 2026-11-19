using System;
using HarmonyLib;

namespace ZombieApocalypse
{
	/// <summary>
	/// Adds the zombie viking prefab to each world's prefabs, so vikings can spawn and load
	/// </summary>
	[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
	public static class VikingPrefabPatch
	{
		/// <summary>
		/// Registers the prefab once the game's own are in
		/// </summary>
		/// <param name="__instance">The world's ZNetScene</param>
		private static void Postfix(ZNetScene __instance)
		{
			ZombieViking.Register(__instance);
		}
	}

	/// <summary>
	/// Turns the local player into a zombie viking when a zombie kills them
	/// </summary>
	/// <remarks>
	/// OnDeath makes the player's corpse and tombstone, so the equipment they're wearing is read first, and their
	/// things are split between the tombstone and the viking once both are made
	/// </remarks>
	[HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
	public static class VikingPlayerDeathPatch
	{
		/// <summary>
		/// Records the player's looks and equipment when a zombie dealt the killing blow
		/// </summary>
		/// <param name="__instance">The dying player</param>
		private static void Prefix(Player __instance)
		{
			ZombieViking.Pending = null;
			if (!Plugin.ZombieVikings.Value || !ServerCheck.Active || __instance != Player.m_localPlayer || !__instance.m_nview.IsOwner())
			{
				return;
			}
			Character killer = __instance.m_lastHit?.GetAttacker();
			if (killer != null && Zombies.IsZombie(killer))
			{
				ZombieViking.Pending = ZombieViking.BeginPlayerDeath(__instance);
			}
		}

		/// <summary>
		/// Sets the player's corpse to rise as a viking carrying their things
		/// </summary>
		private static void Postfix()
		{
			PlayerDeath death = ZombieViking.Pending;
			ZombieViking.Pending = null;
			if (death != null)
			{
				ZombieViking.FinishPlayerDeath(death);
			}
		}
	}

	/// <summary>
	/// Notes the tombstone a player's death makes while they die by a zombie's hand
	/// </summary>
	[HarmonyPatch(typeof(TombStone), nameof(TombStone.Setup))]
	public static class VikingGravePatch
	{
		/// <summary>
		/// Records the tombstone for the death in progress
		/// </summary>
		/// <param name="__instance">The tombstone</param>
		private static void Postfix(TombStone __instance)
		{
			PlayerDeath death = ZombieViking.Pending;
			if (death != null && death.Grave == null)
			{
				death.Grave = __instance;
			}
		}
	}

	/// <summary>
	/// Loads a zombie viking's things as it starts
	/// </summary>
	/// <remarks>
	/// Start runs after a viking raised on this client has been handed its data, which its Awake runs before
	/// </remarks>
	[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Start))]
	public static class VikingStartPatch
	{
		/// <summary>
		/// Loads the viking
		/// </summary>
		/// <param name="__instance">The creature starting</param>
		private static void Postfix(Humanoid __instance)
		{
			if (__instance.TryGetComponent(out ZombieViking viking))
			{
				viking.OnStart();
			}
		}
	}

	/// <summary>
	/// Has a zombie viking wield its own choice of weapon instead of a monster's random pick
	/// </summary>
	[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipBestWeapon))]
	public static class VikingWeaponPatch
	{
		/// <summary>
		/// Equips the viking's weapon and skips the game's pick
		/// </summary>
		/// <param name="__instance">The creature picking a weapon</param>
		/// <returns>False for a viking, to skip the game's pick</returns>
		private static bool Prefix(Humanoid __instance)
		{
			if (!__instance.TryGetComponent(out ZombieViking viking))
			{
				return(true);
			}
			viking.EquipWeapon();
			return(false);
		}
	}

	/// <summary>
	/// Keeps a zombie viking from going after enemies too far from its home
	/// </summary>
	[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateTarget), new Type[] { typeof(Humanoid), typeof(float), typeof(bool), typeof(bool) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Out })]
	public static class VikingTargetPatch
	{
		/// <summary>
		/// Forgets the enemy the AI picked when it's out of the viking's reach
		/// </summary>
		/// <param name="__instance">The AI picking a target</param>
		private static void Postfix(MonsterAI __instance)
		{
			if (__instance.TryGetComponent(out ZombieViking viking))
			{
				viking.LeashTarget();
			}
		}
	}

	/// <summary>
	/// Lets a zombie viking go after an enemy that hurts it from beyond its leash
	/// </summary>
	[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.OnDamaged))]
	public static class VikingHurtPatch
	{
		/// <summary>
		/// Notes who hurt the viking
		/// </summary>
		/// <param name="__instance">The AI of the creature hurt</param>
		/// <param name="attacker">Who hurt it, or null</param>
		private static void Postfix(MonsterAI __instance, Character attacker)
		{
			if (__instance.TryGetComponent(out ZombieViking viking))
			{
				viking.OnHurt(attacker);
			}
		}
	}

	/// <summary>
	/// Keeps a zombie viking with nothing to fight standing by its home instead of wandering
	/// </summary>
	[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.IdleMovement))]
	public static class VikingIdlePatch
	{
		/// <summary>
		/// Runs the viking's own idling instead of the game's wandering
		/// </summary>
		/// <param name="__instance">The AI idling</param>
		/// <param name="dt">Seconds since the last update</param>
		/// <returns>False for a viking with a home, to skip the wandering</returns>
		private static bool Prefix(BaseAI __instance, float dt)
		{
			return(!__instance.TryGetComponent(out ZombieViking viking) || !viking.UpdateIdle(dt));
		}
	}

	/// <summary>
	/// Has a zombie viking stand and wait where it lost sight of an enemy, instead of running circles around the spot
	/// </summary>
	[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.RandomMovement))]
	public static class VikingSearchPatch
	{
		/// <summary>
		/// Stops the viking in place of the random movement
		/// </summary>
		/// <param name="__instance">The AI moving</param>
		/// <returns>False for a viking, to skip the random movement</returns>
		private static bool Prefix(BaseAI __instance)
		{
			if (!__instance.TryGetComponent(out ZombieViking _))
			{
				return(true);
			}
			__instance.StopMoving();
			return(false);
		}
	}

	/// <summary>
	/// Has a zombie viking draw its bow fully, since only a player holds a draw
	/// </summary>
	/// <remarks>
	/// A bow's arrow speed, aim and damage scale with the draw, which is nothing for a monster
	/// </remarks>
	[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GetAttackDrawPercentage))]
	public static class VikingDrawPatch
	{
		/// <summary>
		/// Makes a viking's draw full
		/// </summary>
		/// <param name="__instance">The creature attacking</param>
		/// <param name="__result">How far the bow is drawn, from 0 to 1</param>
		private static void Postfix(Humanoid __instance, ref float __result)
		{
			if (__instance.TryGetComponent(out ZombieViking _))
			{
				ItemDrop.ItemData weapon = __instance.GetCurrentWeapon();
				if (weapon != null && weapon.m_shared.m_attack.m_bowDraw)
				{
					__result = 1f;
				}
			}
		}
	}

	/// <summary>
	/// Keeps a zombie viking's crossbow loaded, since only a player reloads
	/// </summary>
	/// <remarks>
	/// The viking's AI waits between shots instead
	/// </remarks>
	[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.IsWeaponLoaded))]
	public static class VikingReloadPatch
	{
		/// <summary>
		/// Reports a viking's weapon as loaded
		/// </summary>
		/// <param name="__instance">The creature attacking</param>
		/// <param name="__result">Whether its weapon is loaded</param>
		private static void Postfix(Humanoid __instance, ref bool __result)
		{
			if (__instance.TryGetComponent(out ZombieViking _))
			{
				__result = true;
			}
		}
	}

	/// <summary>
	/// Switches a zombie viking off a broken or empty weapon before it attacks, and wears its weapon down as it does
	/// </summary>
	/// <remarks>
	/// The game only wears down a player's weapons
	/// </remarks>
	[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
	public static class VikingAttackPatch
	{
		/// <summary>
		/// Switches weapons when the one in hand can't be used
		/// </summary>
		/// <param name="__instance">The creature attacking</param>
		private static void Prefix(Humanoid __instance)
		{
			if (__instance.TryGetComponent(out ZombieViking viking))
			{
				viking.CheckWeapon();
			}
		}

		/// <summary>
		/// Wears down the weapon an attack started with
		/// </summary>
		/// <param name="__instance">The creature attacking</param>
		/// <param name="__result">Whether the attack started</param>
		private static void Postfix(Humanoid __instance, bool __result)
		{
			if (__result && __instance.TryGetComponent(out ZombieViking viking))
			{
				viking.WearWeapon();
			}
		}
	}

	/// <summary>
	/// Gives a zombie viking's corpse the player's looks
	/// </summary>
	[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.OnRagdollCreated))]
	public static class VikingRagdollPatch
	{
		/// <summary>
		/// Sets the corpse's model, skin and hair
		/// </summary>
		/// <param name="__instance">The creature that died</param>
		/// <param name="ragdoll">Its corpse</param>
		private static void Postfix(Humanoid __instance, Ragdoll ragdoll)
		{
			if (__instance.TryGetComponent(out ZombieViking viking))
			{
				viking.OnRagdollCreated(ragdoll);
			}
		}
	}

	/// <summary>
	/// Drops a zombie viking's things in the player's tombstone as it dies on its owner
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
	public static class VikingDeathPatch
	{
		/// <summary>
		/// Makes the tombstone before the viking is removed
		/// </summary>
		/// <param name="__instance">The creature dying</param>
		private static void Prefix(Character __instance)
		{
			ZNetView nview = __instance.m_nview;
			if (nview != null && nview.IsValid() && nview.IsOwner() && __instance.TryGetComponent(out ZombieViking viking))
			{
				viking.DropGrave();
			}
		}
	}

	/// <summary>
	/// Runs a zombie viking home when it has strayed too far, and gives its ranged weapon an archer's range while its AI
	/// runs
	/// </summary>
	[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
	public static class VikingAIPatch
	{
		/// <summary>
		/// Skips the update while the viking runs home, or tunes its weapon for it
		/// </summary>
		/// <param name="__instance">The AI updating</param>
		/// <param name="dt">Seconds since the last update</param>
		/// <param name="__result">Whether the AI handled the update, set to true while the viking runs home</param>
		/// <returns>False while the viking runs home, to skip the update</returns>
		private static bool Prefix(MonsterAI __instance, float dt, ref bool __result)
		{
			if (!__instance.TryGetComponent(out ZombieViking viking))
			{
				return(true);
			}
			if (viking.UpdateReturn(dt))
			{
				__result = true;
				return(false);
			}
			viking.TuneWeaponAI();
			return(true);
		}

		/// <summary>
		/// Puts the weapon's settings back, whether or not the update finished
		/// </summary>
		/// <param name="__instance">The AI updating</param>
		/// <param name="__exception">An exception the update threw, or null</param>
		/// <returns>The exception, so it's still thrown</returns>
		private static Exception Finalizer(MonsterAI __instance, Exception __exception)
		{
			if (__instance.TryGetComponent(out ZombieViking viking))
			{
				viking.RestoreWeaponAI();
			}
			return(__exception);
		}
	}
}
