using HarmonyLib;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Makes a zombie that rises inside a raid hunt players like the raid's creatures, until the raid ends
	/// </summary>
	/// <remarks>
	/// Unlike the raid's own creatures, the zombie isn't one of the raid's, so it stays when the raid ends rather than
	/// leaving and despawning. A creature that already hunts players of its own keeps hunting after the raid
	/// </remarks>
	internal static class RaidHunt
	{
		/// <summary>
		/// Set in a zombie's ZDO while it hunts players for a raid
		/// </summary>
		private static readonly int HuntKey = "kriona.ZombieApocalypse.raidHunt".GetStableHashCode();

		/// <summary>
		/// Sets a zombie that just rose to hunt players when it's inside the raid going on, unless the world's
		/// creatures are passive
		/// </summary>
		/// <param name="zombie">The zombie, owned by this client</param>
		/// <param name="position">Where it rose</param>
		public static void Join(Character zombie, Vector3 position)
		{
			RandEventSystem system = RandEventSystem.instance;
			RandomEvent raid = ((system != null) ? system.GetCurrentRandomEvent() : null);
			MonsterAI ai = zombie.GetComponent<MonsterAI>();
			if (raid == null || ai == null || ai.m_huntPlayer || !system.IsInsideRandomEventArea(raid, position) || ZoneSystem.instance.GetGlobalKey(GlobalKeys.PassiveMobs))
			{
				return;
			}
			ai.SetHuntPlayer(true);
			ai.m_nview.GetZDO().Set(HuntKey, true);
		}

		/// <summary>
		/// Stops a zombie hunting players once the raid it joined has ended - on every client that has it loaded, so a
		/// client that takes it over later doesn't keep it hunting, while only its owner writes its ZDO
		/// </summary>
		/// <remarks>
		/// The game only reads a creature's hunt from its ZDO as it wakes, so a client that loaded the zombie during the
		/// raid also takes the owner's end of the hunt from the ZDO
		/// </remarks>
		/// <param name="ai">The zombie's AI</param>
		public static void Update(MonsterAI ai)
		{
			ZDO zdo = ai.m_nview.GetZDO();
			if (zdo == null)
			{
				return;
			}
			if (ai.m_huntPlayer && !ai.m_nview.IsOwner() && !zdo.GetBool(ZDOVars.s_huntPlayer, true))
			{
				ai.m_huntPlayer = false;
			}
			if (!zdo.GetBool(HuntKey))
			{
				return;
			}
			if (RandEventSystem.instance != null && RandEventSystem.instance.GetCurrentRandomEvent() != null)
			{
				return;
			}
			// Only the owner writes the game's own hunt flag to the ZDO, so on other clients this sets the AI's alone
			ai.SetHuntPlayer(false);
			if (ai.m_nview.IsOwner())
			{
				zdo.Set(HuntKey, false);
			}
		}
	}

	/// <summary>
	/// Ends a zombie's hunt for players once its raid is over
	/// </summary>
	[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
	public static class RaidHuntPatch
	{
		/// <summary>
		/// Checks the zombie's raid before its AI updates
		/// </summary>
		/// <param name="__instance">The AI updating</param>
		private static void Prefix(MonsterAI __instance)
		{
			RaidHunt.Update(__instance);
		}
	}
}
