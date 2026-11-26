using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The rules of the boats vikings build to cross the sea in the world - how many boats a group takes and who sits
	/// where, how they steer - and of a viking's stamina in the water, how long it swims before it starts to drown and
	/// how fast it gets its stamina back - worked out apart from the game so they can be tested
	/// </summary>
	internal static class BoatRules
	{
		/// <summary>
		/// How far off the boat's heading the next waypoint has to be for the rudder to be hard over, in degrees
		/// </summary>
		private const float FullRudderAngle = 45f;

		/// <summary>
		/// The stamina a viking has before any food, the same as a player's base stamina (see Player.m_baseStamina), and
		/// how much stamina a second swimming takes at no Swim skill and at full Swim skill (see
		/// Player.m_swimStaminaDrainMinSkill and Player.m_swimStaminaDrainMaxSkill)
		/// </summary>
		internal const float BaseStamina = 75f;
		internal const float SwimDrainUnskilled = 5f;
		internal const float SwimDrainSkilled = 2f;

		/// <summary>
		/// How much of its max health a viking loses each second it swims with no stamina left, as a player drowning
		/// does (see Player.OnSwimming)
		/// </summary>
		internal const float DrownShare = 20f;

		/// <summary>
		/// How much stamina a viking gets back each second at full stamina - twice that when it has none left - and how
		/// long after it last used stamina it starts getting it back, in seconds, the same as a player (see
		/// Player.m_staminaRegen and Player.m_staminaRegenDelay)
		/// </summary>
		internal const float StaminaRegen = 5f;
		internal const float StaminaRegenDelay = 1f;

		/// <summary>
		/// Works out how much stamina a second swimming takes a viking, the way it does a player (see
		/// Player.OnSwimming) - less the more Swim skill it has, before its effects and the world's stamina rate
		/// </summary>
		/// <param name="skillFactor">Its Swim skill, from 0 to 1</param>
		/// <returns>The stamina a second takes</returns>
		internal static float SwimDrain(float skillFactor)
		{
			return(Mathf.Lerp(SwimDrainUnskilled, SwimDrainSkilled, Mathf.Clamp01(skillFactor)));
		}

		/// <summary>
		/// Works out how much stamina a viking gets back a second, the way a player does (see Player.UpdateStats) - faster
		/// the less it has left, before its effects and the world's stamina regen rate
		/// </summary>
		/// <param name="stamina">The stamina it has</param>
		/// <param name="maxStamina">The most it can have</param>
		/// <returns>The stamina it gets back a second</returns>
		internal static float StaminaRegain(float stamina, float maxStamina)
		{
			if (maxStamina <= 0f)
			{
				return(0f);
			}
			return(StaminaRegen + (1f - Mathf.Clamp01(stamina / maxStamina)) * StaminaRegen);
		}

		/// <summary>
		/// Works out how many boats a group takes
		/// </summary>
		/// <param name="vikings">How many vikings are in the group</param>
		/// <param name="crew">How many each boat carries</param>
		/// <returns>How many boats</returns>
		internal static int BoatsFor(int vikings, int crew)
		{
			if (vikings <= 0)
			{
				return(0);
			}
			return((vikings + Mathf.Max(1, crew) - 1) / Mathf.Max(1, crew));
		}

		/// <summary>
		/// Shares a group out among its boats as evenly as they go - the first vikings by ID to the first boat, and so on -
		/// each list's first viking at the helm
		/// </summary>
		/// <param name="ids">The vikings' IDs</param>
		/// <param name="crew">How many each boat carries</param>
		/// <returns>Each boat's crew, in order</returns>
		internal static List<List<string>> Crews(List<string> ids, int crew)
		{
			List<string> sorted = new List<string>(ids);
			sorted.Sort(System.StringComparer.Ordinal);
			int boats = BoatsFor(sorted.Count, crew);
			List<List<string>> crews = new List<List<string>>();
			int at = 0;
			for (int i = 0; i < boats; i++)
			{
				// Spread the rest over the boats left, so no boat sails nearly empty
				int size = (sorted.Count - at + (boats - i) - 1) / (boats - i);
				crews.Add(sorted.GetRange(at, size));
				at += size;
			}
			return(crews);
		}

		/// <summary>
		/// Picks the viking that builds a group's boats - the one of the hardest biome, the lowest ID among those
		/// </summary>
		/// <param name="ids">The vikings' IDs</param>
		/// <param name="biomes">Each one's biome, by VikingGear's order</param>
		/// <returns>The builder's index, or -1 when there's no one</returns>
		internal static int Builder(List<string> ids, List<int> biomes)
		{
			int best = -1;
			for (int i = 0; i < ids.Count; i++)
			{
				if (best < 0 || biomes[i] > biomes[best] || (biomes[i] == biomes[best] && string.CompareOrdinal(ids[i], ids[best]) < 0))
				{
					best = i;
				}
			}
			return(best);
		}

		/// <summary>
		/// Works out where to set the rudder to head for a point - hard over when the point is FullRudderAngle or more off
		/// the boat's heading, to the right for a point to the right
		/// </summary>
		/// <param name="forward">Which way the boat faces</param>
		/// <param name="toward">Which way the point is from the boat</param>
		/// <returns>The rudder, from -1 for hard to the left to 1 for hard to the right</returns>
		internal static float Rudder(Vector3 forward, Vector3 toward)
		{
			forward.y = 0f;
			toward.y = 0f;
			if (forward.sqrMagnitude < 0.0001f || toward.sqrMagnitude < 0.0001f)
			{
				return(0f);
			}
			float angle = Vector3.SignedAngle(forward, toward, Vector3.up);
			return(Mathf.Clamp(angle / FullRudderAngle, -1f, 1f));
		}

		/// <summary>
		/// Works out how long a viking swims before its stamina runs out, the way a player's does - its base stamina and
		/// what its food gives, used faster the less Swim skill it has, and by the world's stamina rate
		/// </summary>
		/// <param name="foodStamina">The stamina its food gives</param>
		/// <param name="skillFactor">Its Swim skill, from 0 to 1</param>
		/// <param name="staminaRate">The world's rate of stamina use (see Game.m_moveStaminaRate)</param>
		/// <returns>How long, in seconds</returns>
		internal static float SwimSeconds(float foodStamina, float skillFactor, float staminaRate)
		{
			float drain = SwimDrain(skillFactor) * Mathf.Max(0.01f, staminaRate);
			return((BaseStamina + Mathf.Max(0f, foodStamina)) / drain);
		}

		/// <summary>
		/// Works out how far a viking swims before its stamina runs out (see SwimSeconds)
		/// </summary>
		/// <param name="foodStamina">The stamina its food gives</param>
		/// <param name="skillFactor">Its Swim skill, from 0 to 1</param>
		/// <param name="staminaRate">The world's rate of stamina use (see Game.m_moveStaminaRate)</param>
		/// <param name="swimSpeed">How fast it swims, in meters a second</param>
		/// <returns>How far, in meters</returns>
		internal static float SwimMeters(float foodStamina, float skillFactor, float staminaRate, float swimSpeed)
		{
			return(SwimSeconds(foodStamina, skillFactor, staminaRate) * Mathf.Max(0f, swimSpeed));
		}

		/// <summary>
		/// Works out how much a viking drowning loses each second
		/// </summary>
		/// <param name="maxHealth">Its max health</param>
		/// <returns>The damage</returns>
		internal static float DrownDamage(float maxHealth)
		{
			return(Mathf.Ceil(Mathf.Max(0f, maxHealth) / DrownShare));
		}

		/// <summary>
		/// Finds the stretch of a road crossed by sea that starts at a waypoint - the last waypoint reached by sea, and the
		/// first after it on land
		/// </summary>
		/// <param name="road">The road</param>
		/// <param name="first">The index of the first waypoint reached by sea</param>
		/// <param name="lastSea">Set to the index of the last waypoint reached by sea</param>
		/// <returns>The index of the waypoint ashore after it, or the road's count when the sea leads to its end</returns>
		internal static int Ashore(TravelRoad road, int first, out int lastSea)
		{
			lastSea = first;
			int at = first;
			while (at < road.Count && road.SeaLeg(at))
			{
				lastSea = at;
				at++;
			}
			return(at);
		}
	}
}
