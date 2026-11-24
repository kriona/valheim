using System;
using HarmonyLib;

namespace Kriona.Vikings
{
	/// <summary>
	/// Adds the mod's viking prefab to each world's prefabs, so vikings can spawn and load, then whatever else the mod
	/// adds with it (see AfterRegister)
	/// </summary>
	[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
	internal static partial class VikingPrefabPatch
	{
		/// <summary>
		/// Registers the prefab once the game's own are in
		/// </summary>
		/// <param name="__instance">The world's ZNetScene</param>
		private static void Postfix(ZNetScene __instance)
		{
			VikingCore.Register(__instance);
			AfterRegister(__instance);
		}

		/// <summary>
		/// Adds what the mod needs in each world once its viking prefab is in, like the prefabs it uses alongside it
		/// </summary>
		/// <param name="scene">The world's ZNetScene</param>
		static partial void AfterRegister(ZNetScene scene);
	}

	/// <summary>
	/// Has a zombie viking wield its own choice of weapon instead of a monster's random pick, and leaves a living
	/// viking's sheathed while there's no danger
	/// </summary>
	[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipBestWeapon))]
	internal static class VikingWeaponPatch
	{
		/// <summary>
		/// Equips the viking's weapon and skips the game's pick
		/// </summary>
		/// <param name="__instance">The creature picking a weapon</param>
		/// <returns>False for a viking, to skip the game's pick</returns>
		private static bool Prefix(Humanoid __instance)
		{
			if (!__instance.TryGetComponent(out VikingCore viking))
			{
				return(true);
			}
			viking.PickWeapon();
			return(false);
		}
	}

	/// <summary>
	/// Keeps a zombie viking from going after enemies too far from its home
	/// </summary>
	[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateTarget), new Type[] { typeof(Humanoid), typeof(float), typeof(bool), typeof(bool) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Out })]
	internal static class VikingTargetPatch
	{
		/// <summary>
		/// Forgets the enemy the AI picked when it's out of the viking's reach
		/// </summary>
		/// <param name="__instance">The AI picking a target</param>
		private static void Postfix(MonsterAI __instance)
		{
			if (__instance.TryGetComponent(out VikingCore viking))
			{
				viking.LeashTarget();
			}
		}
	}

	/// <summary>
	/// Keeps a zombie viking with nothing to fight standing by its home instead of wandering, and has a living viking go
	/// about its life in its village
	/// </summary>
	[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.IdleMovement))]
	internal static class VikingIdlePatch
	{
		/// <summary>
		/// Runs the viking's own idling or village life instead of the game's wandering
		/// </summary>
		/// <param name="__instance">The AI idling</param>
		/// <param name="dt">Seconds since the last update</param>
		/// <returns>False for a viking with a home or a village, to skip the wandering</returns>
		private static bool Prefix(BaseAI __instance, float dt)
		{
			return(!__instance.TryGetComponent(out VikingCore viking) || !viking.UpdateIdle(dt));
		}
	}

	/// <summary>
	/// Has a tombstone viking stand and wait where it lost sight of an enemy, instead of running circles around the spot
	/// </summary>
	[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.RandomMovement))]
	internal static class VikingSearchPatch
	{
		/// <summary>
		/// Stops the viking in place of the random movement
		/// </summary>
		/// <param name="__instance">The AI moving</param>
		/// <returns>False for a tombstone viking, to skip the random movement</returns>
		private static bool Prefix(BaseAI __instance)
		{
			if (!__instance.TryGetComponent(out VikingCore viking) || !viking.IsGuard())
			{
				return(true);
			}
			__instance.StopMoving();
			return(false);
		}
	}

	/// <summary>
	/// Keeps a viking from seeing farther than MistSight.Range in the Mistlands' mist (see MistSight)
	/// </summary>
	[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.CanSeeTarget), new Type[] { typeof(Character) })]
	internal static class VikingMistSightPatch
	{
		/// <summary>
		/// Turns the sight down when the mist is in the way
		/// </summary>
		/// <param name="__instance">The AI looking</param>
		/// <param name="target">What it looks at</param>
		/// <param name="__result">Whether it sees it</param>
		private static void Postfix(BaseAI __instance, Character target, ref bool __result)
		{
			if (__result && target != null && __instance.TryGetComponent(out VikingCore _) && MistSight.Blocks(__instance.m_character.m_eye.position, target.GetCenterPoint()))
			{
				__result = false;
			}
		}
	}

	/// <summary>
	/// Keeps a viking from picking out an enemy farther than MistSight.Range in the Mistlands' mist that it doesn't
	/// hear (see MistSight)
	/// </summary>
	[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.CanSenseTarget), new Type[] { typeof(Character), typeof(bool) })]
	internal static class VikingMistSensePatch
	{
		/// <summary>
		/// Turns the sense down when it only saw the target and the mist is in the way
		/// </summary>
		/// <param name="__instance">The AI sensing</param>
		/// <param name="target">What it senses</param>
		/// <param name="__result">Whether it senses it</param>
		private static void Postfix(BaseAI __instance, Character target, ref bool __result)
		{
			if (!__result || target == null || !__instance.TryGetComponent(out VikingCore _) || __instance.CanHearTarget(target))
			{
				return;
			}
			if (MistSight.Blocks(__instance.m_character.m_eye.position, target.GetCenterPoint()))
			{
				__result = false;
			}
		}
	}

	/// <summary>
	/// Notes the zombie viking a hit is being dealt to, so its armor is applied as the damage lands
	/// </summary>
	/// <remarks>
	/// The game only applies a player's armor, partway through this RPC, so the viking's is applied by VikingArmorPatch
	/// at the same point
	/// </remarks>
	[HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage))]
	internal static class VikingHitPatch
	{
		/// <summary>
		/// The viking being hit, while the RPC runs
		/// </summary>
		public static Character Target;

		/// <summary>
		/// Notes the viking, keeping the creature an outer hit was being dealt to
		/// </summary>
		/// <remarks>
		/// A blocked hit damages the attacker inside this RPC, which runs this RPC again at once when this client owns
		/// the attacker, before the blocked hit's own damage is applied
		/// </remarks>
		/// <param name="__instance">The creature being hit</param>
		/// <param name="__state">The creature noted before this hit, put back once it's done</param>
		private static void Prefix(Character __instance, out Character __state)
		{
			__state = Target;
			Target = ((__instance.TryGetComponent(out VikingCore _)) ? __instance : null);
		}

		/// <summary>
		/// Puts back the creature noted before this hit, whether or not the RPC finished
		/// </summary>
		/// <param name="__exception">An exception the RPC threw, or null</param>
		/// <param name="__state">The creature noted before this hit</param>
		/// <returns>The exception, so it's still thrown</returns>
		private static Exception Finalizer(Exception __exception, Character __state)
		{
			Target = __state;
			return(__exception);
		}
	}

	/// <summary>
	/// Reduces a hit on a viking by its armor and wears its armor down, through the same GetBodyArmor and
	/// DamageArmorDurability the game calls for a player's (see VikingBody)
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
	internal static class VikingArmorPatch
	{
		/// <summary>
		/// Applies the armor to the hit being dealt by RPC_Damage
		/// </summary>
		/// <param name="__instance">The creature being damaged</param>
		/// <param name="hit">The hit, after resistances</param>
		private static void Prefix(Character __instance, HitData hit)
		{
			if (VikingHitPatch.Target != __instance)
			{
				return;
			}
			VikingHitPatch.Target = null;
			if (__instance is VikingBody viking)
			{
				hit.ApplyArmor(viking.GetBodyArmor());
				viking.DamageArmorDurability(hit);
			}
		}
	}

	/// <summary>
	/// Makes a zombie viking hit as hard as its biome calls for - its punches, since a player's fists barely scratch,
	/// and a viking of the world's melee weapons, which would hit softer than the biome's creatures at its skill
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.Damage))]
	internal static class VikingDamagePatch
	{
		/// <summary>
		/// Scales a hit from a viking
		/// </summary>
		/// <param name="hit">The hit about to be dealt</param>
		private static void Prefix(HitData hit)
		{
			Character attacker = hit.GetAttacker();
			if (attacker != null && attacker.TryGetComponent(out VikingCore viking))
			{
				float scale = viking.DamageScale();
				if (scale != 1f)
				{
					hit.m_damage.Modify(scale);
				}
			}
		}
	}

	/// <summary>
	/// Has a zombie viking draw its bow fully, since only a player holds a draw
	/// </summary>
	/// <remarks>
	/// A bow's arrow speed, aim and damage scale with the draw, which is nothing for a monster
	/// </remarks>
	[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GetAttackDrawPercentage))]
	internal static class VikingDrawPatch
	{
		/// <summary>
		/// Makes a viking's draw full
		/// </summary>
		/// <param name="__instance">The creature attacking</param>
		/// <param name="__result">How far the bow is drawn, from 0 to 1</param>
		private static void Postfix(Humanoid __instance, ref float __result)
		{
			if (__instance.TryGetComponent(out VikingCore _))
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
	/// Drops a viking's things in the player's tombstone, or its coins and gear, as it dies on its owner, or keeps a
	/// living viking's things for the zombie it rises as
	/// </summary>
	/// <remarks>
	/// Runs after Zombie Apocalypse's DeathPatch has decided whether the viking rises (see VikingCore.Rises), whichever
	/// mod this copy is in
	/// </remarks>
	[HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
	internal static class VikingDeathPatch
	{
		/// <summary>
		/// Makes the tombstone or drops the loot before the viking is removed
		/// </summary>
		/// <param name="__instance">The creature dying</param>
		[HarmonyPriority(Priority.Low)]
		private static void Prefix(Character __instance)
		{
			ZNetView nview = __instance.m_nview;
			if (nview != null && nview.IsValid() && nview.IsOwner() && __instance.TryGetComponent(out VikingCore viking))
			{
				viking.DropGrave(VikingCore.Rises(__instance));
			}
		}
	}
}
