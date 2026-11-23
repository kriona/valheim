using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Adds the zombie viking prefab to each world's prefabs, so vikings can spawn and load, puts it in the body piles'
	/// spawns and listens for vikings using forsaken powers
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
			VikingCore.Register(__instance);
			PoisonCloud.Register(__instance);
			SpiritSpells.Register(__instance);
			VikingSpawns.AddToBodyPiles(__instance);
			ZRoutedRpc.instance?.Register<long>(VikingCore.PowerRpc, VikingCore.OnPowerUsed);
		}
	}

	/// <summary>
	/// Turns the local player into a zombie viking when a zombie kills them, unless they have corpse run
	/// </summary>
	/// <remarks>
	/// OnDeath makes the player's corpse and tombstone, so the equipment they're wearing is read first, and their
	/// things are split between the tombstone and the viking once both are made
	/// </remarks>
	[HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
	public static class VikingPlayerDeathPatch
	{
		/// <summary>
		/// Records the player's looks and equipment when a zombie dealt the killing blow and they don't have corpse
		/// run
		/// </summary>
		/// <remarks>
		/// Corpse run is checked before the game's OnDeath runs, while the player still has their status effects
		/// </remarks>
		/// <param name="__instance">The dying player</param>
		private static void Prefix(Player __instance)
		{
			VikingCore.Pending = null;
			if (!Plugin.Zombies.Value || !Plugin.TombstoneZombies.Value || !ServerCheck.Active || __instance != Player.m_localPlayer || !__instance.m_nview.IsOwner())
			{
				return;
			}
			Character killer = __instance.m_lastHit?.GetAttacker();
			if (killer != null && Zombies.IsZombie(killer) && !CorpseRun.Protects(__instance))
			{
				VikingCore.Pending = VikingCore.BeginPlayerDeath(__instance);
			}
		}

		/// <summary>
		/// Sets the player's corpse to rise as a viking carrying their things
		/// </summary>
		private static void Postfix()
		{
			PlayerDeath death = VikingCore.Pending;
			VikingCore.Pending = null;
			if (death != null)
			{
				VikingCore.FinishPlayerDeath(death);
			}
		}
	}

	/// <summary>
	/// Shares a player's forsaken power with the living vikings near them, the way the game shares it with the players
	/// near them - every one that isn't hostile to the player gets the power's effect too - and has one or two of those
	/// awake thank the player for it
	/// </summary>
	[HarmonyPatch(typeof(Player), nameof(Player.ActivateGuardianPower))]
	public static class VikingSharedPowerPatch
	{
		/// <summary>
		/// How far from the player the power reaches, the same as the game's reach to other players
		/// </summary>
		private const float PowerReach = 10f;

		/// <summary>
		/// What a viking that doesn't know the player calls them when it thanks them
		/// </summary>
		private const string Stranger = "traveler";

		/// <summary>
		/// Notes whether the power is about to be used - it's ready and the player has one
		/// </summary>
		/// <param name="__instance">The player</param>
		/// <param name="__state">Whether it's used</param>
		private static void Prefix(Player __instance, out bool __state)
		{
			__state = (__instance.m_guardianPowerCooldown <= 0f && __instance.m_guardianSE != null);
		}

		/// <summary>
		/// Gives the power's effect to the living vikings within PowerReach once it's used, on the client that owns each,
		/// and has one or two of them thank the player (see Thank)
		/// </summary>
		/// <param name="__instance">The player</param>
		/// <param name="__state">Whether it was used</param>
		private static void Postfix(Player __instance, bool __state)
		{
			if (!__state || !ServerCheck.Active)
			{
				return;
			}
			int effect = __instance.m_guardianSE.NameHash();
			List<Character> given = new List<Character>();
			foreach (Character character in Character.GetAllCharacters())
			{
				if (character is LivingVikingBody && !character.IsDead() && !BaseAI.IsEnemy(character, __instance) && Vector3.Distance(character.transform.position, __instance.transform.position) <= PowerReach)
				{
					character.GetSEMan().AddStatusEffect(effect, true, 0, 0f, -1);
					given.Add(character);
				}
			}
			Thank(__instance, given);
		}

		/// <summary>
		/// Has one of the vikings given a player's power thank them for it, or one or two when there are more, picked at
		/// random among those awake - each with a line of its own, by the player's name once it knows them
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="given">The vikings given the power</param>
		private static void Thank(Player player, List<Character> given)
		{
			List<Character> awake = given.FindAll((Character viking) => !VikingSpeech.Asleep(viking));
			if (awake.Count == 0)
			{
				return;
			}
			int count = ((awake.Count >= 2) ? UnityEngine.Random.Range(1, 3) : 1);
			List<Character> speakers = new List<Character>();
			while (speakers.Count < count)
			{
				int picked = UnityEngine.Random.Range(0, awake.Count);
				speakers.Add(awake[picked]);
				awake.RemoveAt(picked);
			}
			string power = Localization.instance.Localize(player.m_guardianSE.m_name);
			List<string> names = speakers.ConvertAll((Character viking) => (VikingSpeech.Knows(viking, player.GetPlayerID()) ? player.GetPlayerName() : Stranger));
			List<string> lines = VikingSpeech.PowerShared(power, names);
			for (int i = 0; i < lines.Count; i++)
			{
				VikingSpeech.Say(speakers[i], lines[i], VikingSpeech.TalkTime, VikingSpeech.TalkCull);
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
			PlayerDeath death = VikingCore.Pending;
			if (death != null && death.Grave == null)
			{
				death.Grave = __instance;
			}
		}
	}

	/// <summary>
	/// Has a zombie viking wield its own choice of weapon instead of a monster's random pick, and leaves a living
	/// viking's sheathed while there's no danger
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
			if (!__instance.TryGetComponent(out VikingCore viking))
			{
				return(true);
			}
			viking.PickWeapon();
			return(false);
		}
	}

	/// <summary>
	/// Keeps a player with PvP off from hurting a living viking that isn't hostile to them - by a weapon, an arrow or an
	/// area attack - so it isn't angered by accident, the same as PvP keeps players from hurting each other
	/// </summary>
	/// <remarks>
	/// The game lets a player with PvP off hit a creature that can be angered, like the dvergr and living vikings, so
	/// the hit is stopped here, where every hit lands. A viking that's already hostile can be fought back, and a hit
	/// the game marks as ignoring PvP still lands
	/// </remarks>
	[HarmonyPatch(typeof(Character), nameof(Character.Damage))]
	public static class VikingPvpPatch
	{
		/// <summary>
		/// Skips a hit on a living viking from a player with PvP off, unless the viking is hostile
		/// </summary>
		/// <param name="__instance">The creature hit</param>
		/// <param name="hit">The hit</param>
		/// <returns>False to skip the hit</returns>
		private static bool Prefix(Character __instance, HitData hit)
		{
			if (hit == null || hit.m_ignorePVP || __instance == null || !VikingCore.IsLivingViking(__instance))
			{
				return(true);
			}
			Player player = hit.GetAttacker() as Player;
			if (player == null || player.IsPVPEnabled())
			{
				return(true);
			}
			BaseAI ai = __instance.GetBaseAI();
			if (ai != null && ai.IsAggravated())
			{
				return(true);
			}
			VikingPvpTip.Show(player);
			return(false);
		}
	}

	/// <summary>
	/// Spares a living viking from the console's kill commands, and the debug mode's kill key (K) that runs them, unless
	/// it's angry at players or the local player has PvP on, counting the vikings spared for VikingKillReportPatch
	/// </summary>
	/// <remarks>
	/// The commands all ask the same check, a function local to the one that sets the console's commands up, which is
	/// found by its name
	/// </remarks>
	[HarmonyPatch]
	public static class VikingKillAllPatch
	{
		/// <summary>
		/// How many living vikings the command being run has spared
		/// </summary>
		public static int Spared;

		/// <summary>
		/// Skips the patch when the game has no such check, so the other patches still go in
		/// </summary>
		/// <returns>True when it has</returns>
		private static bool Prepare()
		{
			return(TargetMethod() != null);
		}

		/// <summary>
		/// Finds the kill commands' check
		/// </summary>
		/// <returns>The check, or null when the game has none</returns>
		private static System.Reflection.MethodBase TargetMethod()
		{
			foreach (System.Reflection.MethodInfo method in AccessTools.GetDeclaredMethods(typeof(Terminal)))
			{
				if (method.Name.Contains("ShouldKillAll"))
				{
					return(method);
				}
			}
			return(null);
		}

		/// <summary>
		/// Turns the commands down for a living viking they spare (see Spares)
		/// </summary>
		/// <param name="__0">The creature the commands would kill</param>
		/// <param name="__result">Whether they kill it</param>
		private static void Postfix(Character __0, ref bool __result)
		{
			if (__result && Spares(__0))
			{
				__result = false;
				Spared++;
			}
		}

		/// <summary>
		/// Checks whether a kill command spares a creature - a living viking that isn't angry at players, while the local
		/// player's PvP is off
		/// </summary>
		/// <param name="character">The creature</param>
		/// <returns>True when it spares it</returns>
		public static bool Spares(Character character)
		{
			if (character == null || !VikingCore.IsLivingViking(character))
			{
				return(false);
			}
			BaseAI ai = character.GetBaseAI();
			Player player = Player.m_localPlayer;
			return((ai == null || !ai.IsAggravated()) && (player == null || !player.IsPVPEnabled()));
		}
	}

	/// <summary>
	/// Spares a living viking from the deadly hit a kill command deals, for kill commands other mods put in place of the
	/// game's that don't ask its check (see VikingKillAllPatch) - a hit from no one, of at least KillDamage, while a
	/// command with "kill" in it runs (see VikingKillReportPatch)
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.Damage))]
	public static class VikingKillHitPatch
	{
		/// <summary>
		/// The least damage a hit from a kill command deals - the commands deal 1E+10
		/// </summary>
		private const float KillDamage = 1E+9f;

		/// <summary>
		/// Skips the hit for a living viking the command spares
		/// </summary>
		/// <param name="__instance">The creature hit</param>
		/// <param name="hit">The hit</param>
		/// <returns>False to skip the hit</returns>
		private static bool Prefix(Character __instance, HitData hit)
		{
			if (!VikingKillReportPatch.Killing || hit == null || hit.m_damage.m_damage < KillDamage || hit.GetAttacker() != null || !VikingKillAllPatch.Spares(__instance))
			{
				return(true);
			}
			VikingKillAllPatch.Spared++;
			return(false);
		}
	}

	/// <summary>
	/// Notes while a console command with "kill" in it runs, and tells the player how many living vikings it spared once
	/// it has run, having Hugin explain why the first time (see VikingKillAllPatch)
	/// </summary>
	/// <remarks>
	/// A command can run others, like another mod's alias, so the count starts with the first and is told once it ends
	/// </remarks>
	[HarmonyPatch(typeof(Terminal), nameof(Terminal.TryRunCommand))]
	public static class VikingKillReportPatch
	{
		/// <summary>
		/// Whether a command with "kill" in it is running, and how many commands are running, one inside another
		/// </summary>
		public static bool Killing;
		private static int depth;

		/// <summary>
		/// Starts the count for the first command about to run, and notes whether it's a kill command
		/// </summary>
		/// <param name="text">The command</param>
		private static void Prefix(string text)
		{
			if (depth == 0)
			{
				VikingKillAllPatch.Spared = 0;
				Killing = false;
			}
			depth++;
			if (text != null && text.IndexOf("kill", System.StringComparison.OrdinalIgnoreCase) >= 0)
			{
				Killing = true;
			}
		}

		/// <summary>
		/// Tells the player about the vikings the commands spared once the first one has run, if they spared any - even
		/// when a command failed
		/// </summary>
		private static void Finalizer()
		{
			depth = Mathf.Max(0, depth - 1);
			if (depth > 0)
			{
				return;
			}
			Killing = false;
			int spared = VikingKillAllPatch.Spared;
			VikingKillAllPatch.Spared = 0;
			Player player = Player.m_localPlayer;
			if (spared == 0 || player == null)
			{
				return;
			}
			player.Message(MessageHud.MessageType.TopLeft, "Spared " + spared + " living " + ((spared == 1) ? "Viking" : "Vikings"));
			VikingPvpTip.ShowKill(player);
		}
	}

	/// <summary>
	/// Makes a living viking's jump as much stronger as a player's for its Jump skill (see VikingCore.JumpSkillBonus),
	/// and raises the skill for each jump it makes, since the game only does both for a creature with skills of its own,
	/// like a player
	/// </summary>
	/// <remarks>
	/// The jump's forces are raised for the jump and put back after it, and the skill is raised only when the jump goes
	/// ahead, as ForceJump is called from it (see VikingJumpedPatch)
	/// </remarks>
	[HarmonyPatch(typeof(Character), nameof(Character.Jump))]
	public static class VikingJumpSkillPatch
	{
		/// <summary>
		/// The viking jumping right now, while its jump runs
		/// </summary>
		public static Character Jumping;

		/// <summary>
		/// Raises the viking's jump forces by its Jump skill for this jump
		/// </summary>
		/// <param name="__instance">The creature jumping</param>
		/// <param name="__state">Its jump forces before they're raised, or null when they aren't</param>
		private static void Prefix(Character __instance, out float[] __state)
		{
			__state = null;
			if (__instance.IsPlayer() || !VikingCore.IsLivingViking(__instance) || !__instance.TryGetComponent(out VikingCore viking))
			{
				return;
			}
			Jumping = __instance;
			float level = viking.SkillLevel(Skills.SkillType.Jump);
			if (level <= 0f)
			{
				return;
			}
			__state = new[] { __instance.m_jumpForce, __instance.m_jumpForceForward };
			float boost = VikingCore.SkillBoost(level, VikingCore.JumpSkillBonus);
			__instance.m_jumpForce *= boost;
			__instance.m_jumpForceForward *= boost;
		}

		/// <summary>
		/// Puts the viking's jump forces back
		/// </summary>
		/// <param name="__instance">The creature that jumped</param>
		/// <param name="__state">Its jump forces before they were raised, or null when they weren't</param>
		private static void Postfix(Character __instance, float[] __state)
		{
			if (Jumping == __instance)
			{
				Jumping = null;
			}
			if (__state != null)
			{
				__instance.m_jumpForce = __state[0];
				__instance.m_jumpForceForward = __state[1];
			}
		}
	}

	/// <summary>
	/// Raises a living viking's Jump skill when its jump goes ahead (see VikingJumpSkillPatch)
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.ForceJump))]
	public static class VikingJumpedPatch
	{
		/// <summary>
		/// Raises the skill of the viking whose jump this is
		/// </summary>
		/// <param name="__instance">The creature thrown into the air</param>
		private static void Prefix(Character __instance)
		{
			if (VikingJumpSkillPatch.Jumping == __instance && __instance.TryGetComponent(out VikingCore viking))
			{
				viking.RaiseSkill(Skills.SkillType.Jump, 1f);
			}
		}
	}

	/// <summary>
	/// Bends a living viking's move around the creatures and players in its way, before its AI turns it to move
	/// </summary>
	[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.MoveTowards))]
	public static class VikingSteerPatch
	{
		/// <summary>
		/// Steers the move of a living viking with village life
		/// </summary>
		/// <param name="__instance">The AI moving</param>
		/// <param name="dir">The way it's moving, bent here</param>
		private static void Prefix(BaseAI __instance, ref Vector3 dir)
		{
			if (__instance is MonsterAI && __instance.TryGetComponent(out VillageLife life) && life.enabled)
			{
				dir = life.Steer(dir.normalized);
			}
		}
	}

	/// <summary>
	/// Notes how long each living viking's swing takes to land after it starts, so a viking with a two-handed weapon
	/// can time its swing at a diving deathsquito (see VikingCore.UpdateDiveDefense)
	/// </summary>
	[HarmonyPatch(typeof(Attack), nameof(Attack.OnAttackTrigger))]
	public static class VikingSwingTimePatch
	{
		/// <summary>
		/// Hands the time since the swing started to VikingCore as the swing lands
		/// </summary>
		/// <param name="__instance">The attack landing</param>
		private static void Prefix(Attack __instance)
		{
			if (__instance.m_character is LivingVikingBody && __instance.m_attackType != Attack.AttackType.Projectile)
			{
				VikingCore.NoteSwing(__instance.m_weapon, __instance.m_time);
			}
		}
	}

	/// <summary>
	/// Stops a living viking short of the lava and the boiling sea when a move would take it there from safe ground -
	/// one the navmesh doesn't steer, as when its AI moves it straight at something (see DangerousGround)
	/// </summary>
	/// <remarks>
	/// It runs after VikingSteerPatch, so it looks at the move as it's bent
	/// </remarks>
	[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.MoveTowards))]
	public static class VikingDangerStepPatch
	{
		/// <summary>
		/// Stops the viking in place, still turning it the way it meant to go, when its step ahead is dangerous
		/// </summary>
		/// <param name="__instance">The AI moving</param>
		/// <param name="dir">The way it's moving</param>
		/// <returns>False to skip the move</returns>
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(BaseAI __instance, Vector3 dir)
		{
			Character character = __instance.m_character;
			if (!VikingCore.IsLivingViking(character) || !DangerousGround.StepsIntoDanger(character, dir))
			{
				return(true);
			}
			__instance.LookTowards(dir.normalized);
			__instance.StopMoving();
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
	public static class VikingIdlePatch
	{
		/// <summary>
		/// Runs the viking's own idling or village life instead of the game's wandering
		/// </summary>
		/// <param name="__instance">The AI idling</param>
		/// <param name="dt">Seconds since the last update</param>
		/// <returns>False for a viking with a home or a village, to skip the wandering</returns>
		private static bool Prefix(BaseAI __instance, float dt)
		{
			if (__instance.TryGetComponent(out VillageLife life) && life.Idle(dt))
			{
				return(false);
			}
			return(!__instance.TryGetComponent(out VikingCore viking) || !viking.UpdateIdle(dt));
		}
	}

	/// <summary>
	/// Has a tombstone viking stand and wait where it lost sight of an enemy, instead of running circles around the spot
	/// </summary>
	[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.RandomMovement))]
	public static class VikingSearchPatch
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
	/// Notes the zombie viking a hit is being dealt to, so its armor is applied as the damage lands
	/// </summary>
	/// <remarks>
	/// The game only applies a player's armor, partway through this RPC, so the viking's is applied by VikingArmorPatch
	/// at the same point
	/// </remarks>
	[HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage))]
	public static class VikingHitPatch
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
	public static class VikingArmorPatch
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
	public static class VikingDamagePatch
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
	public static class VikingDrawPatch
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
	/// Runs after DeathPatch has decided whether the viking rises
	/// </remarks>
	[HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
	public static class VikingDeathPatch
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
				viking.DropGrave(Dying.Creature == __instance && Dying.Rise != null);
			}
		}
	}
}
