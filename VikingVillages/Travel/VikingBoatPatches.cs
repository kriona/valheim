using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Makes a ship a vikings' boat as it loads, when its ZDO says it's one (see VikingBoat)
	/// </summary>
	[HarmonyPatch(typeof(Ship), "Awake")]
	public static class VikingBoatAwakePatch
	{
		/// <summary>
		/// Turns the ship into a vikings' boat when it's one, and notes it among the ships loaded (see VikingBoat.Ships)
		/// </summary>
		/// <param name="__instance">The ship</param>
		private static void Postfix(Ship __instance)
		{
			VikingBoat.NoteShip(__instance);
			VikingBoat.Take(__instance);
		}
	}

	/// <summary>
	/// Lets a vikings' boat sail with no player on board - the game stops a ship and slows it to a drift whenever no
	/// player is on it, which this counts its crew as one for while it sails (see VikingBoat.Sails)
	/// </summary>
	[HarmonyPatch(typeof(Ship), nameof(Ship.CustomFixedUpdate))]
	public static class VikingBoatRidersPatch
	{
		/// <summary>
		/// The ship being moved right now, for Riders
		/// </summary>
		private static Ship moving;

		/// <summary>
		/// Notes the ship being moved
		/// </summary>
		/// <param name="__instance">The ship</param>
		private static void Prefix(Ship __instance)
		{
			moving = __instance;
		}

		/// <summary>
		/// Forgets the ship once it has been moved
		/// </summary>
		private static void Postfix()
		{
			moving = null;
		}

		/// <summary>
		/// Has the ship count the players on board through Riders
		/// </summary>
		/// <param name="instructions">The method's instructions</param>
		/// <returns>The instructions, changed</returns>
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo count = AccessTools.PropertyGetter(typeof(List<Player>), nameof(List<Player>.Count));
			MethodInfo riders = AccessTools.Method(typeof(VikingBoatRidersPatch), nameof(Riders));
			foreach (CodeInstruction instruction in instructions)
			{
				if (instruction.Calls(count))
				{
					instruction.opcode = OpCodes.Call;
					instruction.operand = riders;
				}
				yield return(instruction);
			}
		}

		/// <summary>
		/// Counts the players on board the ship being moved - at least one for a vikings' boat under way
		/// </summary>
		/// <param name="players">The players on board</param>
		/// <returns>How many the ship counts</returns>
		public static int Riders(List<Player> players)
		{
			VikingBoat boat = VikingBoat.Of(moving);
			return((boat != null && boat.Sails()) ? Mathf.Max(1, players.Count) : players.Count);
		}
	}

	/// <summary>
	/// Counts a vikings' boat under way as steered, so its sail and rudder move and it can back off (see
	/// Ship.HaveControllingPlayer)
	/// </summary>
	[HarmonyPatch(typeof(Ship), "HaveControllingPlayer")]
	public static class VikingBoatSteeredPatch
	{
		/// <summary>
		/// Counts the boat as steered while it sails
		/// </summary>
		/// <param name="__instance">The ship</param>
		/// <param name="__result">Whether it's steered</param>
		private static void Postfix(Ship __instance, ref bool __result)
		{
			VikingBoat boat = VikingBoat.Of(__instance);
			if (boat != null && boat.Sails())
			{
				__result = true;
			}
		}
	}

	/// <summary>
	/// Sails a vikings' boat by the world's wind, the way the game does a player's (see Ship.GetSailForce) - its strength
	/// and where it comes from, with the wind from behind while anyone on board is under Moder's power - but always
	/// pushing it straight ahead, since it steers itself along its road, and never stopping it dead heading into the
	/// wind (see BoatRules.SailWind)
	/// </summary>
	[HarmonyPatch(typeof(Ship), "GetSailForce")]
	public static class VikingBoatWindPatch
	{
		/// <summary>
		/// How strong the wind is with no weather to go by, from 0 to 1 (see EnvMan.GetWindIntensity)
		/// </summary>
		private const float NoWeatherWind = 0.5f;

		/// <summary>
		/// Works out the sail's push from the world's wind
		/// </summary>
		/// <param name="__instance">The ship</param>
		/// <param name="sailSize">How much sail is up, from 0 to 1</param>
		/// <param name="__result">The push</param>
		/// <returns>False to skip the game's own for a vikings' boat</returns>
		private static bool Prefix(Ship __instance, float sailSize, ref Vector3 __result)
		{
			VikingBoat boat = VikingBoat.Of(__instance);
			if (boat == null)
			{
				return(true);
			}
			Vector3 forward = __instance.transform.forward;
			float windDot = -1f;
			float intensity = NoWeatherWind;
			if (EnvMan.instance != null)
			{
				intensity = EnvMan.instance.GetWindIntensity();
				if (!boat.ModerWind())
				{
					windDot = Vector3.Dot(EnvMan.instance.GetWindDir(), -forward);
				}
			}
			float factor = BoatRules.SailWind(windDot, intensity);
			Vector3 target = forward * (factor * __instance.m_sailForceFactor * sailSize);
			__instance.m_sailForce = Vector3.SmoothDamp(__instance.m_sailForce, target, ref __instance.m_windChangeVelocity, 1f, 99f);
			__result = __instance.m_sailForce;
			return(false);
		}
	}

	/// <summary>
	/// Keeps a vikings' boat from dropping what it's built of when it breaks, since they built it from nothing
	/// </summary>
	[HarmonyPatch(typeof(Piece), nameof(Piece.DropResources))]
	public static class VikingBoatDropPatch
	{
		/// <summary>
		/// Skips the drop for a vikings' boat
		/// </summary>
		/// <param name="__instance">The piece</param>
		/// <returns>False to skip it</returns>
		private static bool Prefix(Piece __instance)
		{
			return(__instance.GetComponent<VikingBoat>() == null);
		}
	}

	/// <summary>
	/// Keeps a player from taking the helm of a vikings' boat - they ride along as a passenger
	/// </summary>
	[HarmonyPatch(typeof(ShipControlls), nameof(ShipControlls.Interact))]
	public static class VikingBoatHelmPatch
	{
		/// <summary>
		/// Turns the player away from the helm of a vikings' boat
		/// </summary>
		/// <param name="__instance">The helm</param>
		/// <param name="character">Who's using it</param>
		/// <param name="__result">Whether it was used</param>
		/// <returns>False to skip the game's own for a vikings' boat</returns>
		private static bool Prefix(ShipControlls __instance, Humanoid character, ref bool __result)
		{
			if (VikingBoat.Of(__instance.m_ship) == null)
			{
				return(true);
			}
			if (character != null)
			{
				character.Message(MessageHud.MessageType.Center, "A Viking has the helm");
			}
			__result = false;
			return(false);
		}
	}
}
