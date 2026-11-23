using System.Collections.Generic;
using HarmonyLib;

namespace ZombieApocalypse
{
	/// <summary>
	/// Shows whose a bed is when a living viking sleeps in it, whether it's sheltered enough to claim, and the key for
	/// unclaiming a player's own bed (see BedUnclaimPatch)
	/// </summary>
	[HarmonyPatch(typeof(Bed), nameof(Bed.GetHoverText))]
	public static class BedHoverPatch
	{
		/// <summary>
		/// Seconds a bed's shelter is kept before it's checked again, since it's looked up with rays every frame it's
		/// looked at otherwise
		/// </summary>
		private const float ShelterRecheck = 1f;

		/// <summary>
		/// Each bed's shelter as last checked, and when, by its ZDO
		/// </summary>
		private static readonly Dictionary<ZDOID, KeyValuePair<float, bool>> shelter = new Dictionary<ZDOID, KeyValuePair<float, bool>>();

		/// <summary>
		/// Names an unclaimed bed after the viking that sleeps in it - "Ella's Bed", or "Viking Bed" for one with no
		/// name - marks one that isn't sheltered, and adds the unclaim key to the player's own
		/// </summary>
		/// <param name="__instance">The bed looked at</param>
		/// <param name="__result">Its hover text</param>
		private static void Postfix(Bed __instance, ref string __result)
		{
			ZNetView view = __instance.m_nview;
			if (view == null || !view.IsValid() || string.IsNullOrEmpty(__result))
			{
				return;
			}
			ZDO zdo = view.GetZDO();
			string[] lines = __result.Split('\n');
			if (zdo.GetLong(ZDOVars.s_owner, 0L) == 0L)
			{
				ZDO holder = VillageBeds.Holder(zdo, Village.Find(__instance.transform.position));
				if (holder != null)
				{
					string name = VikingNames.Get(holder);
					lines[0] = ((name.Length > 0) ? name + "'s " : "Viking ") + Localization.instance.Localize("$piece_bed");
				}
			}
			if (!Sheltered(__instance, zdo.m_uid))
			{
				lines[0] += " (not sheltered)";
			}
			__result = string.Join("\n", lines);
			if (__instance.IsMine())
			{
				string alt = ((!ZInput.IsNonClassicFunctionality() || !ZInput.IsGamepadActive()) ? "$KEY_AltPlace" : "$KEY_AltKeys");
				__result += Localization.instance.Localize("\n[<color=yellow><b>" + alt + " + $KEY_Use</b></color>] ") + "Unclaim";
			}
		}

		/// <summary>
		/// Checks whether a bed is sheltered enough to claim (see VillageBeds.Sheltered), from its last check when that's
		/// recent
		/// </summary>
		/// <param name="bed">The bed</param>
		/// <param name="id">Its ZDO</param>
		/// <returns>True when it is</returns>
		private static bool Sheltered(Bed bed, ZDOID id)
		{
			float now = UnityEngine.Time.time;
			if (shelter.TryGetValue(id, out KeyValuePair<float, bool> kept) && now - kept.Key < ShelterRecheck)
			{
				return(kept.Value);
			}
			if (shelter.Count > 256)
			{
				shelter.Clear();
			}
			bool sheltered = VillageBeds.Sheltered(bed);
			shelter[id] = new KeyValuePair<float, bool>(now, sheltered);
			return(sheltered);
		}
	}

	/// <summary>
	/// Lets a player unclaim their own bed with the alternate use key, so a viking can sleep in it or another player
	/// can claim it
	/// </summary>
	[HarmonyPatch(typeof(Bed), nameof(Bed.Interact))]
	public static class BedUnclaimPatch
	{
		/// <summary>
		/// Unclaims the player's own bed on the alternate use key, taking away their spawn point when it's there
		/// </summary>
		/// <param name="__instance">The bed</param>
		/// <param name="human">Who's using it</param>
		/// <param name="repeat">Whether the key is held down</param>
		/// <param name="alt">Whether the alternate use key is held</param>
		/// <param name="__result">Whether it was used</param>
		/// <returns>False when it was unclaimed, skipping the game's own use</returns>
		private static bool Prefix(Bed __instance, Humanoid human, bool repeat, bool alt, ref bool __result)
		{
			if (!alt || repeat || __instance.m_nview == null || __instance.m_nview.GetZDO() == null || !__instance.IsMine())
			{
				return(true);
			}
			bool current = __instance.IsCurrent();
			__instance.SetOwner(0L, "");
			if (current)
			{
				Game.instance.GetPlayerProfile().ClearCustomSpawnPoint();
			}
			human.Message(MessageHud.MessageType.Center, "Unclaimed");
			__result = false;
			return(false);
		}
	}
}
