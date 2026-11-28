using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The Traveling Viking event - a Viking from a biome at least three harder than the village's comes by in the
	/// latter half of the day, stays the night by the fire and sets off again the next morning (see Traveler and
	/// EVENTS.md)
	/// </summary>
	internal static class TravelerEvent
	{
		/// <summary>
		/// The event's ID, for the console
		/// </summary>
		internal const string Id = "traveler";

		/// <summary>
		/// The chance it happens on a day, and how many days its cooldown lasts
		/// </summary>
		internal const float Chance = 0.1f;
		internal const int CooldownDays = 6;

		/// <summary>
		/// The time of day the traveler arrives within - from noon to two game hours before dusk, so they come in daylight
		/// </summary>
		internal const float ArriveFrom = 0.5f;
		internal const float ArriveUntil = VillageLife.DuskStart - 2f / 24f;

		/// <summary>
		/// The event, for villages of the Meadows through the Plains
		/// </summary>
		public static readonly VillageEvent Event = new VillageEvent(Id, "Traveling Viking", Chance, CooldownDays, VikingGear.IndexOf(Heightmap.Biome.Meadows), VikingGear.IndexOf(Heightmap.Biome.Plains), ArriveFrom, ArriveUntil, CanStart, Start);

		/// <summary>
		/// Checks whether a traveler can arrive at a village now - not while a raid is on it
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>True when they can</returns>
		private static bool CanStart(Village village)
		{
			return(!VillageRaids.On(village));
		}

		/// <summary>
		/// Sends a traveler to a village, from a biome picked at random (see Send)
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		/// <returns>False when none could come yet</returns>
		private static bool Start(Village village)
		{
			return(Send(village, -1));
		}

		/// <summary>
		/// Sends a traveler to a village - spawning a PC viking marked as a traveler outside the farm's fence, out of every
		/// player's sight, the way a newcomer comes (see VillageRespawn.ReadyToArrive), to walk in - from a biome at least
		/// Traveler.BiomeGap harder than the village's, picked at random among them, or the one given. It comes from one
		/// village and goes to another (see TravelerPlaces), maybe bringing an animal of its biome (see TravelerPets), and
		/// leaves the morning after it arrives, a while after the vikings wake (see Traveler.LeaveAt)
		/// </summary>
		/// <param name="village">The village, run by this client</param>
		/// <param name="origin">The biome it comes from, by its index in VikingGear's tables, or -1 to pick one</param>
		/// <returns>False when none could come yet - with an enemy near, no spot to come from, or no biome hard enough</returns>
		internal static bool Send(Village village, int origin)
		{
			if (origin < 0)
			{
				origin = Traveler.PickOrigin(VillageCooking.BiomeAt(village.Farm), VikingGear.Biomes.Length - 1, Random.value);
			}
			if (origin < 0 || ZNetScene.instance == null || ZNet.instance == null || EnvMan.instance == null)
			{
				return(false);
			}
			if (!VillageRespawn.ReadyToArrive(village, out Vector3 spot, out bool outside, out string why))
			{
				if (TestLog.On)
				{
					Plugin.Log.LogInfo("A traveler can't come to " + VillageNames.Get(village) + " yet - " + why);
				}
				return(false);
			}
			GameObject prefab = ZNetScene.instance.GetPrefab(VikingCore.PCPrefabHash);
			if (prefab == null)
			{
				return(false);
			}
			GameObject instance = Object.Instantiate(prefab, spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
			BaseAI ai = instance.GetComponent<BaseAI>();
			if (ai != null)
			{
				ai.SetPatrolPoint();
			}
			ZDO zdo = instance.GetComponent<ZNetView>().GetZDO();
			double leaveAt = Traveler.LeaveAt(ZNet.instance.GetTimeSeconds(), EnvMan.instance.m_dayLengthSec, Random.Range(Traveler.StayMinHours, Traveler.StayMaxHours));
			TravelerPlaces.PickPair(TravelerPlaces.Candidates(VillageNames.Typed(village)), Random.value, Random.value, out string from, out string to);
			zdo.Set(Traveler.HostKey, village.Farm);
			zdo.Set(Traveler.OriginKey, origin);
			zdo.Set(Traveler.LeaveAtKey, (long)(leaveAt * System.TimeSpan.TicksPerSecond));
			zdo.Set(Traveler.FromKey, from);
			zdo.Set(Traveler.ToKey, to);
			zdo.Set(VillageLife.ArrivingKey, outside);
			TravelerPlaces.AskFolk(zdo);
			TravelerPets.Bring(zdo, origin, spot, village);
			TestLog.Count("Events", "Traveling Viking");
			Plugin.Log.LogInfo("A traveler from " + Traveler.HomeName(origin) + " came to " + VillageNames.Get(village) + " at " + spot.ToString("F0") + ", from " + from + " on the way to " + to);
			return(true);
		}
	}
}
