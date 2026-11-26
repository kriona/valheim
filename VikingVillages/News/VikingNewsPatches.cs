using HarmonyLib;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Reports what a creature's death is news of, before the game destroys it (see VikingNewsWatch.OnCreatureDeath)
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
	public static class NewsCreatureDeathPatch
	{
		/// <summary>
		/// Reports the death of a creature that isn't a player
		/// </summary>
		/// <param name="__instance">The dying creature</param>
		private static void Prefix(Character __instance)
		{
			if (!(__instance is Player))
			{
				VikingNewsWatch.OnCreatureDeath(__instance);
			}
		}
	}

	/// <summary>
	/// Reports the local player's death (see VikingNewsWatch.OnPlayerDeath)
	/// </summary>
	[HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
	public static class NewsPlayerDeathPatch
	{
		/// <summary>
		/// Reports the death with the hit that killed them
		/// </summary>
		/// <param name="__instance">The dying player</param>
		private static void Prefix(Player __instance)
		{
			if (__instance != null && __instance == Player.m_localPlayer && __instance.m_nview != null && __instance.m_nview.IsOwner())
			{
				VikingNewsWatch.OnPlayerDeath(__instance, __instance.m_lastHit);
			}
		}
	}

	/// <summary>
	/// Reports a biome the local player discovers - when the game shows its discovery message, leaving out the Meadows
	/// and counting a biome's alternate versions as the biome itself
	/// </summary>
	[HarmonyPatch(typeof(Player), nameof(Player.AddKnownBiome))]
	public static class NewsBiomePatch
	{
		/// <summary>
		/// Notes whether the player hasn't known the biome until now
		/// </summary>
		/// <param name="__instance">The player</param>
		/// <param name="biome">The biome's sector</param>
		/// <param name="__state">Set to true when it's new to them</param>
		private static void Prefix(Player __instance, BiomeSector biome, out bool __state)
		{
			__state = (__instance == Player.m_localPlayer && biome != null && !__instance.m_knownBiome.Contains(biome.GetName()));
		}

		/// <summary>
		/// Reports the biome once the game has noted it, the first time this character finds it in the world
		/// </summary>
		/// <param name="__instance">The player</param>
		/// <param name="biome">The biome's sector</param>
		/// <param name="__state">Whether it was new to them</param>
		private static void Postfix(Player __instance, BiomeSector biome, bool __state)
		{
			if (!__state || biome.Biome == Heightmap.Biome.Meadows || biome.Biome == Heightmap.Biome.None)
			{
				return;
			}
			VikingNewsStore.Report(VikingNews.Biome, biome.Biome.ToString(), "", VikingNewsWatch.Only(__instance), true);
		}
	}

	/// <summary>
	/// Reports the local player talking to Haldor, Hildir or the Bog Witch, which trading with them starts with
	/// </summary>
	[HarmonyPatch(typeof(Trader), nameof(Trader.Interact))]
	public static class NewsTraderPatch
	{
		/// <summary>
		/// Reports the trader, the first time this character talks to them in the world
		/// </summary>
		/// <param name="__instance">The trader</param>
		/// <param name="character">Who's talking to them</param>
		/// <param name="hold">Whether the key is held</param>
		private static void Postfix(Trader __instance, Humanoid character, bool hold)
		{
			Player player = character as Player;
			if (hold || __instance == null || player == null || player != Player.m_localPlayer)
			{
				return;
			}
			string trader = VikingNewsLines.TraderKey(Utils.GetPrefabName(__instance.gameObject));
			if (trader.Length > 0)
			{
				VikingNewsStore.Report(VikingNews.Trader, trader, __instance.m_name, VikingNewsWatch.Only(player), true);
			}
		}
	}

	/// <summary>
	/// Reports an animal tamed, by the player nearest it - the one the game tells it's tamed - but not by the console's
	/// tame command
	/// </summary>
	[HarmonyPatch(typeof(Tameable), nameof(Tameable.Tame))]
	public static class NewsTamePatch
	{
		/// <summary>
		/// The farthest the game tells a player an animal they're near is tamed, in meters
		/// </summary>
		private const float TamedMessageRange = 30f;

		/// <summary>
		/// Reports the animal as it's tamed on its owner, the first time this character tames its kind in the world
		/// </summary>
		/// <param name="__instance">The animal</param>
		private static void Prefix(Tameable __instance)
		{
			if (VikingNewsWatch.TamingAll || __instance == null || __instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner() || __instance.m_character == null || __instance.IsTamed())
			{
				return;
			}
			Player player = Player.GetClosestPlayer(__instance.transform.position, TamedMessageRange);
			if (player != null)
			{
				VikingNewsStore.Report(VikingNews.Tame, Utils.GetPrefabName(__instance.gameObject), __instance.m_character.m_name, VikingNewsWatch.Only(player), true);
			}
		}
	}

	/// <summary>
	/// Keeps the animals the console's tame command tames from being news
	/// </summary>
	[HarmonyPatch(typeof(Tameable), nameof(Tameable.TameAllInArea))]
	public static class NewsTameAllPatch
	{
		/// <summary>
		/// Notes that the command is taming
		/// </summary>
		private static void Prefix()
		{
			VikingNewsWatch.TamingAll = true;
		}

		/// <summary>
		/// Notes that it's done
		/// </summary>
		private static void Postfix()
		{
			VikingNewsWatch.TamingAll = false;
		}
	}

	/// <summary>
	/// Reports the local player mastering a skill, and mastering every skill, as a skill reaches 100 - the console's
	/// raiseskill command doesn't go through here
	/// </summary>
	[HarmonyPatch(typeof(Player), nameof(Player.OnSkillLevelup))]
	public static class NewsSkillPatch
	{
		/// <summary>
		/// The top level of a skill
		/// </summary>
		private const float MaxLevel = 100f;

		/// <summary>
		/// Reports the skill, and every skill once each the game has is at the top, the first time for this character in
		/// the world
		/// </summary>
		/// <param name="__instance">The player</param>
		/// <param name="skill">The skill</param>
		/// <param name="level">Its new level</param>
		private static void Postfix(Player __instance, Skills.SkillType skill, float level)
		{
			if (__instance == null || __instance != Player.m_localPlayer || level < MaxLevel)
			{
				return;
			}
			VikingNewsStore.Report(VikingNews.Skill, skill.ToString(), "", VikingNewsWatch.Only(__instance), true);
			Skills skills = __instance.GetSkills();
			if (skills == null)
			{
				return;
			}
			foreach (Skills.SkillDef def in skills.m_skills)
			{
				if (skills.GetSkillLevel(def.m_skill) < MaxLevel)
				{
					return;
				}
			}
			VikingNewsStore.Report(VikingNews.AllSkills, "", "", VikingNewsWatch.Only(__instance), true);
		}
	}

	/// <summary>
	/// Reports the achievements that are news as the local player earns them - only those shown as earned, not the
	/// ones the game quietly marks earned as it starts
	/// </summary>
	[HarmonyPatch(typeof(Achievements), nameof(Achievements.AchievementEvent))]
	public static class NewsAchievementPatch
	{
		/// <summary>
		/// Reports the achievement, the first time for this character in the world
		/// </summary>
		/// <param name="ach">The achievement</param>
		/// <param name="showPopup">Whether the game shows it as earned</param>
		private static void Postfix(Achievement ach, bool showPopup)
		{
			Player player = Player.m_localPlayer;
			if (!showPopup || ach == null || player == null)
			{
				return;
			}
			string key = Key(ach);
			if (key.Length > 0)
			{
				VikingNewsStore.Report(VikingNews.Achievement, key, "", VikingNewsWatch.Only(player), true);
			}
		}

		/// <summary>
		/// Finds the key an achievement that's news goes by - its ID, or the localization key of its name
		/// </summary>
		/// <param name="ach">The achievement</param>
		/// <returns>The key, or "" when it isn't news</returns>
		private static string Key(Achievement ach)
		{
			string id = (ach.m_id ?? "");
			if (VikingNewsLines.Achievements.Contains(id))
			{
				return(id);
			}
			string name = (ach.m_name ?? "").TrimStart('$');
			return(VikingNewsLines.Achievements.Contains(name) ? name : "");
		}
	}

	/// <summary>
	/// Notes each leviathan as it loads, for the local player sailing near one (see VikingNewsWatch)
	/// </summary>
	[HarmonyPatch(typeof(Leviathan), nameof(Leviathan.Awake))]
	public static class NewsLeviathanPatch
	{
		/// <summary>
		/// Notes the leviathan
		/// </summary>
		/// <param name="__instance">The leviathan</param>
		private static void Postfix(Leviathan __instance)
		{
			VikingNewsWatch.AddLeviathan(__instance);
		}
	}

	/// <summary>
	/// Notes the last player to hit a tree, so the log it falls as knows who felled it (see VikingNewsWatch)
	/// </summary>
	[HarmonyPatch(typeof(TreeBase), nameof(TreeBase.RPC_Damage))]
	public static class NewsTreeHitPatch
	{
		/// <summary>
		/// Notes the hit on the tree's owner
		/// </summary>
		/// <param name="__instance">The tree</param>
		/// <param name="hit">The hit</param>
		private static void Prefix(TreeBase __instance, HitData hit)
		{
			VikingNewsWatch.OnTreeHit(__instance, hit);
		}
	}

	/// <summary>
	/// Gives the log a tree falls as the player who felled it
	/// </summary>
	[HarmonyPatch(typeof(TreeBase), nameof(TreeBase.SpawnLog))]
	public static class NewsTreeFallPatch
	{
		/// <summary>
		/// Notes who felled the tree as its log spawns
		/// </summary>
		/// <param name="__instance">The falling tree</param>
		private static void Prefix(TreeBase __instance)
		{
			VikingNewsWatch.StartFelling(__instance);
		}

		/// <summary>
		/// Forgets them once it has
		/// </summary>
		private static void Postfix()
		{
			VikingNewsWatch.EndFelling();
		}
	}

	/// <summary>
	/// Gives the pieces a log breaks into the player who felled it
	/// </summary>
	[HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Destroy), new[] { typeof(HitData), typeof(bool) })]
	public static class NewsLogBreakPatch
	{
		/// <summary>
		/// Notes who felled the log as its pieces spawn
		/// </summary>
		/// <param name="__instance">The breaking log</param>
		private static void Prefix(TreeLog __instance)
		{
			VikingNewsWatch.StartBreaking(__instance);
		}

		/// <summary>
		/// Forgets them once they have
		/// </summary>
		private static void Postfix()
		{
			VikingNewsWatch.EndFelling();
		}
	}

	/// <summary>
	/// Has a log that just spawned take the player who brought down its tree or log
	/// </summary>
	[HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Awake))]
	public static class NewsLogSpawnPatch
	{
		/// <summary>
		/// Gives the log its feller
		/// </summary>
		/// <param name="__instance">The new log</param>
		private static void Postfix(TreeLog __instance)
		{
			VikingNewsWatch.OnLogSpawned(__instance);
		}
	}

	/// <summary>
	/// Has a log take the player who hit it, since they're the one who sent it rolling
	/// </summary>
	[HarmonyPatch(typeof(TreeLog), nameof(TreeLog.RPC_Damage))]
	public static class NewsLogHitPatch
	{
		/// <summary>
		/// Gives the log the player, on its owner
		/// </summary>
		/// <param name="__instance">The log</param>
		/// <param name="hit">The hit</param>
		private static void Prefix(TreeLog __instance, HitData hit)
		{
			VikingNewsWatch.OnLogHit(__instance, hit);
		}
	}
}
