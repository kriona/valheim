using HarmonyLib;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Has a player riding a valkyrie take her up with jump, bring her down with crouch and have her attack with their
	/// attack and secondary attack, rather than any of those getting them off her
	/// </summary>
	[HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
	internal static class ValkyrieControlsPatch
	{
		/// <summary>
		/// Notes whether the local player holds jump and crouch on the valkyrie they ride, has her attack when they
		/// attack (see ValkyrieCombat.RiderAttack), and keeps jump and attacking from getting them off her
		/// </summary>
		/// <param name="__instance">The player</param>
		/// <param name="attack">Whether attack was just pressed, which gets a rider off their mount</param>
		/// <param name="secondaryAttack">Whether secondary attack was just pressed, which gets a rider off their mount</param>
		/// <param name="jump">Whether jump was just pressed, which gets a rider off their mount</param>
		private static void Prefix(Player __instance, ref bool attack, ref bool secondaryAttack, ref bool jump)
		{
			if (__instance != Player.m_localPlayer || !(__instance.m_doodadController is Sadle saddle) || !saddle.IsValid() || saddle.m_character == null || !saddle.m_character.TryGetComponent(out ValkyrieFlight flight))
			{
				return;
			}
			bool input = __instance.TakeInput();
			flight.SetRiderInput(input && (ZInput.GetButton("Jump") || ZInput.GetButton("JoyJump")), input && (ZInput.GetButton("Crouch") || ZInput.GetButton("JoyCrouch")));
			if ((attack || secondaryAttack) && saddle.m_character is Humanoid body && saddle.m_character.IsOwner())
			{
				ValkyrieCombat.RiderAttack(body, secondaryAttack, __instance.GetLookDir());
			}
			attack = false;
			secondaryAttack = false;
			jump = false;
		}
	}

	/// <summary>
	/// Steers a valkyrie by her rider's keys, in place of the game's riding: left and right - the arrow keys, or the
	/// side keys and stick - turn her to go that way, on the ground she walks only while a key that moves her is held,
	/// and holding block as she flies takes her up or down the way they look (see ValkyrieMotion.RideSpeed,
	/// ValkyrieMotion.RideDirection and ValkyrieFlight.SetRiderLook)
	/// </summary>
	[HarmonyPatch(typeof(Sadle), nameof(Sadle.ApplyControlls))]
	internal static class ValkyrieSteerPatch
	{
		/// <summary>
		/// Sends a valkyrie's saddle her rider's controls the way the game does, on the rider's client, by her rules
		/// </summary>
		/// <param name="__instance">The saddle</param>
		/// <param name="moveDir">The rider's movement keys, x to the side and z forward</param>
		/// <param name="lookDir">The way the rider looks</param>
		/// <param name="run">Whether run is held</param>
		/// <param name="block">Whether block is held</param>
		/// <returns>False for a valkyrie, to skip the game's riding controls</returns>
		private static bool Prefix(Sadle __instance, Vector3 moveDir, Vector3 lookDir, bool run, bool block)
		{
			Player player = Player.m_localPlayer;
			if (player == null || __instance.m_character == null || __instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_character.TryGetComponent(out ValkyrieFlight flight))
			{
				return(true);
			}
			float arrows = 0f;
			if (player.TakeInput())
			{
				arrows = (ZInput.GetKey(KeyCode.RightArrow, false) ? 1f : 0f) - (ZInput.GetKey(KeyCode.LeftArrow, false) ? 1f : 0f);
			}
			float side = Mathf.Clamp(moveDir.x + arrows, -1f, 1f);
			bool grounded = !flight.IsFlying();
			Vector3 dir = ValkyrieMotion.RideDirection(lookDir, moveDir.z, side, run, block);
			int speed = ValkyrieMotion.RideSpeed(moveDir.z, side, run, block, grounded);
			float skill = player.GetSkills().GetSkillFactor(Skills.SkillType.Ride);
			flight.SetRiderLook(block, lookDir);
			if (grounded && speed == ValkyrieMotion.RideTurn)
			{
				// The saddle keeps walking on through a turn, so she's stopped first to only turn
				__instance.m_nview.InvokeRPC("Controls", Vector3.zero, ValkyrieMotion.RideStop, skill);
			}
			__instance.m_nview.InvokeRPC("Controls", dir, speed, skill);
			return(false);
		}
	}

	/// <summary>
	/// Has a valkyrie fly up and down after her saddle moves her, and come down on her own with no rider in the air
	/// </summary>
	[HarmonyPatch(typeof(Sadle), nameof(Sadle.UpdateRiding))]
	internal static class ValkyrieRidingPatch
	{
		/// <summary>
		/// Moves a valkyrie up or down, and lands her (see ValkyrieFlight.Ride)
		/// </summary>
		/// <param name="__instance">The saddle</param>
		/// <param name="__result">Whether the saddle moved her, so her AI leaves her be, changed when her flying does</param>
		private static void Postfix(Sadle __instance, ref bool __result)
		{
			if (__instance.m_character == null || !__instance.m_character.TryGetComponent(out ValkyrieFlight flight))
			{
				return;
			}
			bool ridden = __instance.isActiveAndEnabled && __instance.HaveValidUser();
			__result = flight.Ride(__result, ridden);
		}
	}

	/// <summary>
	/// Has a valkyrie's AI take her up to fight and bring her down again (see ValkyrieFlight.ThinkAI), and leaves her
	/// standing on a roof she roosts on rather than wandering off it (see ValkyrieFlight.Roosted)
	/// </summary>
	[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
	internal static class ValkyrieAIFlightPatch
	{
		/// <summary>
		/// Stops a roosting valkyrie wherever her AI would have walked her - its update still looks for enemies, which
		/// take her up off the roof to fight
		/// </summary>
		/// <param name="__instance">The AI</param>
		private static void Postfix(MonsterAI __instance)
		{
			if (__instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner() || !__instance.TryGetComponent(out ValkyrieFlight flight) || flight.Piloted())
			{
				return;
			}
			if (flight.Roosted(__instance))
			{
				__instance.StopMoving();
			}
		}

		/// <summary>
		/// Takes a valkyrie up or brings her down on the client that owns her, skipping her AI's update while she comes
		/// down
		/// </summary>
		/// <param name="__instance">The AI</param>
		/// <param name="__result">Whether the AI's update ran, set when it's skipped</param>
		/// <returns>False to skip the AI's update</returns>
		private static bool Prefix(MonsterAI __instance, ref bool __result)
		{
			if (__instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner() || !__instance.TryGetComponent(out ValkyrieFlight flight))
			{
				return(true);
			}
			if (flight.ThinkAI(__instance))
			{
				return(true);
			}
			__result = true;
			return(false);
		}
	}

	/// <summary>
	/// Has a valkyrie who's flying fly on when she's dipped into water, rather than swim - the game swims anything in
	/// water deep enough, flying or not, holding it at the surface, which left her stuck there with no way up. Flying, she
	/// rises clear of it (see ValkyrieMotion.AboveWater)
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.UpdateSwimming))]
	internal static class ValkyrieWaterPatch
	{
		/// <summary>
		/// Flies a flying valkyrie in place of swimming her
		/// </summary>
		/// <param name="__instance">The creature in the water</param>
		/// <param name="dt">The time since the last update, in seconds</param>
		/// <returns>False for a flying valkyrie, to skip swimming her</returns>
		private static bool Prefix(Character __instance, float dt)
		{
			if (!__instance.m_flying || __instance.GetComponent<ValkyrieFlight>() == null)
			{
				return(true);
			}
			__instance.UpdateFlying(dt);
			return(false);
		}
	}

	/// <summary>
	/// Has a flying valkyrie climb over what's in her way ahead, however she's flown (see ValkyrieFlight.Steer)
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.UpdateFlying))]
	internal static class ValkyrieObstaclePatch
	{
		/// <summary>
		/// Gives a valkyrie's flying the way up over what's in her way for this update, keeping the way she was told to
		/// fly to put back after, so it's turned afresh each update rather than over and over
		/// </summary>
		/// <param name="__instance">The flying creature</param>
		/// <param name="__state">The way she was told to fly, or null when it's left alone</param>
		private static void Prefix(Character __instance, out Vector3? __state)
		{
			__state = null;
			if (__instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner() || !__instance.TryGetComponent(out ValkyrieFlight flight))
			{
				return;
			}
			Vector3 told = __instance.m_moveDir;
			Vector3 steered = flight.Steer(told);
			if (steered != told)
			{
				__state = told;
				__instance.m_moveDir = steered;
			}
		}

		/// <summary>
		/// Puts back the way she was told to fly
		/// </summary>
		/// <param name="__instance">The flying creature</param>
		/// <param name="__state">The way she was told to fly, or null when it was left alone</param>
		private static void Postfix(Character __instance, Vector3? __state)
		{
			if (__state.HasValue)
			{
				__instance.m_moveDir = __state.Value;
			}
		}
	}

	/// <summary>
	/// Keeps a flying valkyrie's body level, turning only side to side (see ValkyrieFlight.KeepLevel), and holds her
	/// speed while she carries a rider she caught down (see ValkyrieCatch.Steady)
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.UpdateFlying))]
	internal static class ValkyrieLevelPatch
	{
		/// <summary>
		/// Levels a valkyrie after the game turned her toward where she's flying, and steadies her catch's descent
		/// </summary>
		/// <param name="__instance">The flying creature</param>
		private static void Postfix(Character __instance)
		{
			if (__instance.TryGetComponent(out ValkyrieFlight flight))
			{
				flight.KeepLevel();
			}
			if (__instance.TryGetComponent(out ValkyrieCatch catcher))
			{
				catcher.Steady();
			}
		}
	}

	/// <summary>
	/// Keeps a valkyrie's attacks from hurting a standing oak, though they cut every other tree a bronze axe does
	/// </summary>
	[HarmonyPatch(typeof(TreeBase), nameof(TreeBase.Damage))]
	internal static class ValkyrieOakPatch
	{
		/// <summary>
		/// The standing oak's prefab
		/// </summary>
		private const string OakPrefab = "Oak1";

		/// <summary>
		/// Drops a valkyrie's hit on an oak, on the client that made it, before it's sent to the tree's owner
		/// </summary>
		/// <param name="__instance">The tree</param>
		/// <param name="hit">The hit</param>
		/// <returns>False for a valkyrie's hit on an oak, to skip it</returns>
		private static bool Prefix(TreeBase __instance, HitData hit)
		{
			Character attacker = hit.GetAttacker();
			if (attacker == null || attacker.GetComponent<ValkyrieFlight>() == null)
			{
				return(true);
			}
			return(Utils.GetPrefabName(__instance.gameObject) != OakPrefab);
		}
	}

	/// <summary>
	/// Puts a valkyrie's top at her head rather than at the top of her body's capsule, which reaches far above her, so
	/// her health bar and other mods' markers sit just over her head (see ValkyrieFlight.TopPoint)
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.GetTopPoint))]
	internal static class ValkyrieTopPointPatch
	{
		/// <summary>
		/// Moves a valkyrie's top to just above her head
		/// </summary>
		/// <param name="__instance">The creature</param>
		/// <param name="__result">The top of its body's capsule, changed for a valkyrie</param>
		private static void Postfix(Character __instance, ref Vector3 __result)
		{
			if (__instance.TryGetComponent(out ValkyrieFlight flight))
			{
				__result = flight.TopPoint(__result);
			}
		}
	}
}
