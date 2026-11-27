using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
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
	/// Has a zombie viking wield its own choice of weapon instead of a monster's random pick, and leaves a PC
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
	/// Keeps a zombie viking with nothing to fight standing by its home instead of wandering, and has a PC viking go
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
	/// Notes the viking a hit is being dealt to, so its armor is applied as the damage lands
	/// </summary>
	/// <remarks>
	/// The game only applies a player's armor, partway through this RPC. A viking that wears its armor the way a player
	/// does has it applied there by VikingPlayerArmorPatch, which takes it off Target, and any other has it applied by
	/// VikingArmorPatch as the damage lands
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
	/// DamageArmorDurability the game calls for a player's (see VikingBody), for a viking VikingPlayerArmorPatch hasn't
	/// already applied it to
	/// </summary>
	/// <remarks>
	/// The damage lands after RPC_Damage has split the poison, fire and spirit off to be dealt over time, so the armor
	/// applied here doesn't reduce them
	/// </remarks>
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
	/// Applies the armor of a viking that wears it the way a player does (see VikingCore.WearsArmorLikePlayer) where
	/// RPC_Damage applies a player's - to the whole hit, poison, fire and spirit included, before they're split off to
	/// be dealt over time - with the game's own GetBodyArmor and DamageArmorDurability (see VikingBody)
	/// </summary>
	/// <remarks>
	/// It adds the viking to the condition the game checks before applying a player's armor rather than taking the
	/// place of the IsPlayer call, so the other mod's copy of this patch adds its own vikings to it too. The viking
	/// then skips the world level's armor for enemies, as a player does. A viking given its armor here is taken off
	/// VikingHitPatch.Target, so VikingArmorPatch doesn't apply it again - and when this patch can't find its place,
	/// VikingArmorPatch still applies it
	/// </remarks>
	[HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage))]
	internal static class VikingPlayerArmorPatch
	{
		/// <summary>
		/// Adds a call to WearsArmor between the IsPlayer check before the game applies a player's armor and its branch,
		/// leaving the method as it is when the armor isn't applied behind a branch
		/// </summary>
		/// <param name="instructions">The method's instructions</param>
		/// <returns>The instructions with the call added</returns>
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo bodyArmor = AccessTools.Method(typeof(Character), nameof(Character.GetBodyArmor));
			List<CodeInstruction> result = new List<CodeInstruction>(instructions);
			int armor = result.FindIndex((CodeInstruction instruction) => instruction.Calls(bodyArmor));
			int branch = -1;
			for (int i = armor - 1; i >= 0; i--)
			{
				if (result[i].opcode == OpCodes.Brfalse || result[i].opcode == OpCodes.Brfalse_S)
				{
					branch = i;
					break;
				}
			}
			if (armor < 0 || branch < 0)
			{
				VikingMod.Log.LogWarning("Couldn't find where Character.RPC_Damage applies a player's armor - vikings' armor won't reduce poison, fire or spirit damage");
				return(result);
			}
			result.InsertRange(branch, new[] { new CodeInstruction(OpCodes.Ldarg_0), CodeInstruction.Call(typeof(VikingPlayerArmorPatch), nameof(WearsArmor)) });
			return(result);
		}

		/// <summary>
		/// Checks whether the game applies a player's armor to a creature being hit - a player, or one of this mod's
		/// vikings that wears its armor the way a player does, which it then takes off VikingHitPatch.Target
		/// </summary>
		/// <param name="player">What the game's check, and any other mod's patch of it, found</param>
		/// <param name="character">The creature being hit</param>
		/// <returns>True when its armor is applied</returns>
		private static bool WearsArmor(bool player, Character character)
		{
			if (player || !VikingCore.WearsArmorLikePlayer() || VikingHitPatch.Target != character)
			{
				return(player);
			}
			VikingHitPatch.Target = null;
			return(true);
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
	/// PC viking's things for the zombie it rises as
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

	/// <summary>
	/// Keeps the game's update going when one of the AIs it updates throws - logging the exception, emptying the
	/// game's list of the AIs being updated, which the game only empties once every AI has run, and stopping the
	/// exception there
	/// </summary>
	/// <remarks>
	/// Left full, the next frame's AIs are added behind the ones already there, so the same AI throws first every
	/// frame from then on - even a destroyed one - and none after it is updated again. Thrown on, the exception also
	/// leaves MonoUpdaters.FixedUpdate before it lowers its AI timer, so the AIs run, and throw, on every fixed step
	/// from then on, and everything it updates after them - every creature's and player's movement, layer and fall
	/// through the ground (Character), and the Aoe and EffectArea updates - never runs again. Stopped here, a throw
	/// costs only the AIs after it in that update
	/// </remarks>
	[HarmonyPatch(typeof(MonoUpdatersExtra), nameof(MonoUpdatersExtra.UpdateAI))]
	internal static class MonoUpdaterAIClearPatch
	{
		/// <summary>
		/// Logs the exception an AI threw, empties the list and stops the exception
		/// </summary>
		/// <param name="__exception">An exception an AI threw, or null</param>
		/// <param name="container">The list of AIs being updated</param>
		/// <returns>Null, so the game's update goes on</returns>
		private static Exception Finalizer(Exception __exception, List<IUpdateAI> container)
		{
			if (__exception != null)
			{
				UnityEngine.Debug.LogException(__exception);
				container.Clear();
			}
			return(null);
		}
	}

	/// <summary>
	/// Keeps the game's update going when one of the objects it gives their fixed update throws, as
	/// MonoUpdaterAIClearPatch does for the AIs, so the updates MonoUpdaters.FixedUpdate runs after it still run
	/// </summary>
	[HarmonyPatch(typeof(MonoUpdatersExtra), nameof(MonoUpdatersExtra.CustomFixedUpdate))]
	internal static class MonoUpdaterFixedClearPatch
	{
		/// <summary>
		/// Logs the exception an object threw, empties the list and stops the exception
		/// </summary>
		/// <param name="__exception">An exception an object threw, or null</param>
		/// <param name="container">The list of objects being updated</param>
		/// <returns>Null, so the game's update goes on</returns>
		private static Exception Finalizer(Exception __exception, List<IMonoUpdater> container)
		{
			if (__exception != null)
			{
				UnityEngine.Debug.LogException(__exception);
				container.Clear();
			}
			return(null);
		}
	}

	/// <summary>
	/// Keeps the game's update going when one of the objects it gives their update throws, as
	/// MonoUpdaterAIClearPatch does for the AIs, so the updates MonoUpdaters.Update runs after it still run
	/// </summary>
	[HarmonyPatch(typeof(MonoUpdatersExtra), nameof(MonoUpdatersExtra.CustomUpdate))]
	internal static class MonoUpdaterUpdateClearPatch
	{
		/// <summary>
		/// Logs the exception an object threw, empties the list and stops the exception
		/// </summary>
		/// <param name="__exception">An exception an object threw, or null</param>
		/// <param name="container">The list of objects being updated</param>
		/// <returns>Null, so the game's update goes on</returns>
		private static Exception Finalizer(Exception __exception, List<IMonoUpdater> container)
		{
			if (__exception != null)
			{
				UnityEngine.Debug.LogException(__exception);
				container.Clear();
			}
			return(null);
		}
	}

	/// <summary>
	/// Keeps the game's update going when one of the objects it gives their late update throws, as
	/// MonoUpdaterAIClearPatch does for the AIs, so the updates MonoUpdaters.LateUpdate runs after it still run
	/// </summary>
	[HarmonyPatch(typeof(MonoUpdatersExtra), nameof(MonoUpdatersExtra.CustomLateUpdate))]
	internal static class MonoUpdaterLateClearPatch
	{
		/// <summary>
		/// Logs the exception an object threw, empties the list and stops the exception
		/// </summary>
		/// <param name="__exception">An exception an object threw, or null</param>
		/// <param name="container">The list of objects being updated</param>
		/// <returns>Null, so the game's update goes on</returns>
		private static Exception Finalizer(Exception __exception, List<IMonoUpdater> container)
		{
			if (__exception != null)
			{
				UnityEngine.Debug.LogException(__exception);
				container.Clear();
			}
			return(null);
		}
	}
}
