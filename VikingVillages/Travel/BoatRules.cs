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
		/// The share of how far a viking in the water can still swim that its boat may be away for it to swim there - less
		/// than all of it, since the boat may drift and the way to its ladder isn't straight
		/// </summary>
		internal const float SwimMargin = 0.75f;

		/// <summary>
		/// Works out how far a viking in the water can still swim on the stamina it has left (see LivingVikingBody.OnSwimming)
		/// </summary>
		/// <param name="stamina">The stamina it has left</param>
		/// <param name="skillFactor">Its Swim skill, from 0 to 1</param>
		/// <param name="staminaRate">The world's rate of stamina use (see Game.m_moveStaminaRate)</param>
		/// <param name="swimSpeed">How fast it swims, in meters a second</param>
		/// <returns>How far, in meters</returns>
		internal static float SwimMetersLeft(float stamina, float skillFactor, float staminaRate, float swimSpeed)
		{
			float drain = SwimDrain(skillFactor) * Mathf.Max(0.01f, staminaRate);
			return(Mathf.Max(0f, stamina) / drain * Mathf.Max(0f, swimSpeed));
		}

		/// <summary>
		/// Checks whether a viking overboard swims for its boat's ladder, or treads water - which takes no stamina - until
		/// the boat comes near enough for it to make it (see SwimMargin)
		/// </summary>
		/// <param name="metersLeft">How far it can still swim (see SwimMetersLeft)</param>
		/// <param name="distance">How far the ladder is, in meters</param>
		/// <returns>True when it swims for it</returns>
		internal static bool SwimsForBoat(float metersLeft, float distance)
		{
			return(distance <= metersLeft * SwimMargin);
		}

		/// <summary>
		/// How a quest goes by boat from its village (see QuestSea) - how near the village its road has to reach the sea
		/// for the quest to sail from the village, how long the sea stretch has to be for it to be worth a boat, how far out
		/// the boats sail before the vikings are gone from the world, and how far out the boats come back from, in meters
		/// </summary>
		internal const float QuestSeaReach = 120f;
		internal const float QuestSeaMin = 200f;
		internal const float QuestSailAway = 150f;
		internal const float QuestSailIn = 150f;

		/// <summary>
		/// Finds the sea a quest's road sets out across from its village - its first sea stretch, when the road reaches
		/// the sea within QuestSeaReach of the village through no portal and the stretch is QuestSeaMin long at least -
		/// from the shore it sets sail from, across the sea, to the shore past it
		/// </summary>
		/// <param name="road">The quest's road there</param>
		/// <param name="from">Where the village stands</param>
		/// <returns>The stretch - the shore, the sea's waypoints and the shore past them - or null when the quest goes on
		/// foot from the village</returns>
		internal static TravelRoad QuestSea(TravelRoad road, Vector3 from)
		{
			if (road == null)
			{
				return(null);
			}
			int first = -1;
			for (int i = 1; i < road.Count; i++)
			{
				if (road.PortalLeg(i))
				{
					return(null);
				}
				if (road.SeaLeg(i))
				{
					first = i;
					break;
				}
			}
			if (first < 0 || TravelRoad.Flat(road.Points[first - 1], from) > QuestSeaReach)
			{
				return(null);
			}
			int ashore = Ashore(road, first, out int lastSea);
			TravelRoad sea = new TravelRoad();
			sea.Add(road.Points[first - 1], TravelLeg.Foot);
			for (int i = first; i <= lastSea; i++)
			{
				sea.Add(road.Points[i], TravelLeg.Sea);
			}
			if (ashore < road.Count)
			{
				sea.Add(road.Points[ashore], TravelLeg.Foot);
			}
			return((sea.Meters(1, sea.Points[0]) >= QuestSeaMin) ? sea : null);
		}

		/// <summary>
		/// Shares a quest's vikings out among the boats they sail in - the village's own boats first, each filled up to its
		/// seats, as many as they need, and the rest shared out evenly among as many new boats as they take (see Crews)
		/// </summary>
		/// <param name="ids">The vikings' IDs</param>
		/// <param name="owned">How many each of the village's own boats seats, in the order they're filled</param>
		/// <param name="built">How many a new boat seats</param>
		/// <param name="ownedCrews">Set to the crew of each of the village's boats that goes, in their order, as many as
		/// go</param>
		/// <returns>The crews of the new boats built for the rest</returns>
		internal static List<List<string>> QuestCrews(List<string> ids, List<int> owned, int built, out List<List<string>> ownedCrews)
		{
			ownedCrews = new List<List<string>>();
			int at = 0;
			for (int i = 0; i < owned.Count && at < ids.Count; i++)
			{
				int take = Mathf.Min(Mathf.Max(1, owned[i]), ids.Count - at);
				ownedCrews.Add(ids.GetRange(at, take));
				at += take;
			}
			List<string> rest = ids.GetRange(at, ids.Count - at);
			return((rest.Count > 0) ? Crews(rest, built) : new List<List<string>>());
		}

		/// <summary>
		/// Finds where a quest's boats come back into the world from - QuestSailIn along its sea from the shore it set
		/// sail from, or the sea's far end when it's shorter - and the road home from there, back to that shore
		/// </summary>
		/// <param name="sea">The quest's sea (see QuestSea)</param>
		/// <param name="home">Set to the road home, by sea, ending at the shore the quest set sail from</param>
		/// <returns>Where the boats come back from, on the sea</returns>
		internal static Vector3 SailInFrom(TravelRoad sea, out TravelRoad home)
		{
			home = new TravelRoad();
			int last = sea.Count - 1;
			while (last > 0 && !sea.SeaLeg(last))
			{
				last--;
			}
			Vector3 start = sea.Points[Mathf.Max(0, last)];
			int at = Mathf.Max(0, last);
			float covered = 0f;
			for (int i = 1; i <= last; i++)
			{
				covered += TravelRoad.Flat(sea.Points[i - 1], sea.Points[i]);
				if (covered >= QuestSailIn)
				{
					float over = covered - QuestSailIn;
					float leg = TravelRoad.Flat(sea.Points[i - 1], sea.Points[i]);
					start = Vector3.Lerp(sea.Points[i], sea.Points[i - 1], (leg > 0f) ? over / leg : 0f);
					at = i;
					break;
				}
			}
			home.Add(start, TravelLeg.Foot);
			for (int i = at - 1; i >= 1; i--)
			{
				home.Add(sea.Points[i], TravelLeg.Sea);
			}
			return(start);
		}

		/// <summary>
		/// How a boat comes in to the far shore before it lets its crew off - how near the spot ashore it comes, in meters,
		/// how shallow the water ahead of its bow can get before it stops short of running aground, in meters, and how long
		/// it goes without getting a meter nearer before it counts as aground already, in seconds
		/// </summary>
		internal const float ShoreNear = 6f;
		internal const float ShoalDepth = 1.5f;
		internal const float BeachStall = 4f;

		/// <summary>
		/// Checks whether a boat coming in to the far shore has come as near as it can - near the spot ashore, about to
		/// run aground, or aground already, no longer getting nearer
		/// </summary>
		/// <param name="distance">How far the spot ashore is, in meters</param>
		/// <param name="depthAhead">How deep the water is just ahead of its bow, in meters</param>
		/// <param name="stalledFor">How long it has gone without getting a meter nearer, in seconds</param>
		/// <returns>True when it lets its crew off</returns>
		internal static bool Beached(float distance, float depthAhead, float stalledFor)
		{
			return(distance <= ShoreNear || depthAhead < ShoalDepth || stalledFor >= BeachStall);
		}

		/// <summary>
		/// How near a boat comes to someone overboard before it stops for them, and how near it slows for them, in meters
		/// </summary>
		internal const float RescueStop = 8f;
		internal const float RescueSlow = 30f;

		/// <summary>
		/// Picks how fast a boat goes as it comes about for someone overboard - stopped beside them, slow as it closes in,
		/// and at half sail farther off
		/// </summary>
		/// <param name="distance">How far they are from the boat, in meters</param>
		/// <returns>The speed</returns>
		internal static Ship.Speed RescueSpeed(float distance)
		{
			if (distance <= RescueStop)
			{
				return(Ship.Speed.Stop);
			}
			return((distance <= RescueSlow) ? Ship.Speed.Slow : Ship.Speed.Half);
		}

		/// <summary>
		/// The least share of the sail's push a vikings' boat keeps sailing into the wind, as if it tacked - half what a
		/// wind from straight behind gives, where a player's stops dead (see SailWind)
		/// </summary>
		internal const float HeadwindFloor = 0.35f;

		/// <summary>
		/// Works out how much of the wind a sail catches by where the wind comes from, the way the game does (see
		/// Ship.GetWindAngleFactor) - most with it from the side, 0.7 of that from straight behind, and none heading into
		/// it
		/// </summary>
		/// <param name="windDot">How much the wind blows from straight ahead of the boat - the dot of the way it blows
		/// with the boat's back, 1 straight ahead and -1 straight behind</param>
		/// <returns>The share, from 0 to 1</returns>
		internal static float WindAngle(float windDot)
		{
			float across = Mathf.Lerp(0.7f, 1f, 1f - Mathf.Abs(windDot));
			float ahead = 1f - Mathf.Clamp01((windDot - 0.75f) / 0.05f);
			return(across * ahead);
		}

		/// <summary>
		/// Works out the share of the sail's push a vikings' boat gets from the wind - the game's, by where the wind comes
		/// from and how strong it is (see Ship.GetSailForce), but never less than HeadwindFloor of it for where it comes
		/// from, so the boat makes its way into the wind too
		/// </summary>
		/// <param name="windDot">How much the wind blows from straight ahead (see WindAngle), -1 with Moder's power on
		/// board</param>
		/// <param name="intensity">How strong the wind is, from 0 to 1 (see EnvMan.GetWindIntensity)</param>
		/// <returns>The share</returns>
		internal static float SailWind(float windDot, float intensity)
		{
			return(Mathf.Max(WindAngle(windDot), HeadwindFloor) * Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(intensity)));
		}

		/// <summary>
		/// Checks whether Moder's power would help a boat - the wind from behind it, as the power turns it, filling its
		/// sail more than the wind it has
		/// </summary>
		/// <param name="windDot">How much the wind blows from straight ahead (see WindAngle)</param>
		/// <returns>True when it would</returns>
		internal static bool ModerHelps(float windDot)
		{
			return(WindAngle(windDot) < WindAngle(-1f));
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
