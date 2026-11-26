using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The vikings of a village a player disbanded gathering at its bonfire before they set out together for another
	/// village - the one they can get to soonest, no harder than the easiest biome among them, sailing in the best boat
	/// that biome has
	/// </summary>
	/// <remarks>
	/// The client that disbands a village notes on its bonfire when it was disbanded and asks the server for the village
	/// they go to (see VillageTravel.Ask), noting the answer there too, so each viking, whichever client owns it, works out
	/// from the bonfire where the group is at (see PhaseOf). They gather at the fire for up to GatherMax seconds - less
	/// once they're all there - stay a while, and one of them says where they're going before they set out. The vikings
	/// of the village no player was near as it was disbanded go straight onto the road once the village is known (see
	/// VillageTravel.Queue). With no village to go to, they leave the way they always have (see VillageDeparted). Those
	/// out on the village's quest go on to the same village when they're done (see VillageTravel.QuestsDisbanded)
	/// </remarks>
	internal static class VillageDisband
	{
		/// <summary>
		/// On a bonfire, when the village at it was disbanded and when its vikings had all gathered at it, in ZNet time
		/// ticks, how long they stay once they have, in seconds, whether the village they go to is known yet (see Searched),
		/// the easiest and hardest biomes among them, and whether any of them carries something a portal won't take (see
		/// TravelParty)
		/// </summary>
		public static readonly int DisbandAtKey = "kriona.VikingVillages.fireDisbandAt".GetStableHashCode();
		public static readonly int GatheredAtKey = "kriona.VikingVillages.fireGatheredAt".GetStableHashCode();
		public static readonly int RemainKey = "kriona.VikingVillages.fireRemain".GetStableHashCode();
		public static readonly int SearchedKey = "kriona.VikingVillages.fireSearched".GetStableHashCode();
		public static readonly int BiomeKey = "kriona.VikingVillages.fireBiome".GetStableHashCode();
		public static readonly int HardestKey = "kriona.VikingVillages.fireHardestBiome".GetStableHashCode();
		public static readonly int UnteleportableKey = "kriona.VikingVillages.fireUnteleportable".GetStableHashCode();

		/// <summary>
		/// Whether the village a disbanded village's vikings go to is known - not yet, found, or there's none
		/// </summary>
		public const int SearchPending = 0;
		public const int SearchFound = 1;
		public const int SearchNone = 2;

		/// <summary>
		/// The most seconds the vikings wait for each other at the fire - long enough for them to empty the village's
		/// chests first (see VillageLife.UpdateDisbandLoot) - how long they stay once they're all there, picked at random,
		/// how long after one says where they're going they set out, and the most seconds they wait on the server's
		/// answer past when they'd say it
		/// </summary>
		public const float GatherMax = 240f;
		private const float RemainMin = 20f;
		private const float RemainMax = 30f;
		public const float SetOutDelay = 2f;
		public const float AnswerWait = 30f;

		/// <summary>
		/// How near the fire a viking has to be to count as there, how far out from it they stand, and how far from it they
		/// count as the group, in meters
		/// </summary>
		public const float GatheredReach = 7f;
		public const float StandReach = 4f;
		private const float GroupReach = 250f;

		/// <summary>
		/// Seconds between this client's looks at whether the vikings it disbanded are all at the fire
		/// </summary>
		private const float CheckInterval = 1f;

		/// <summary>
		/// Where the group of a disbanded village is at
		/// </summary>
		internal enum Phase
		{
			Gathering,
			Remaining,
			Announcing,
			SetOut
		}

		/// <summary>
		/// The bonfires this client disbanded a village at, by their ZDO, still looking after their vikings' gathering, and
		/// when it next looks
		/// </summary>
		private static readonly List<ZDOID> fires = new List<ZDOID>();
		private static float nextCheck;

		/// <summary>
		/// Notes on a bonfire that its village was just disbanded, so its vikings gather at it, and asks the server for the
		/// village they go to - by what its vikings loaded here can do on the road together (see TravelParty), or the
		/// village's own biome when none of them is loaded here
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="fire">Its bonfire's ZDO, owned by this client</param>
		public static void Begin(Village village, ZDO fire)
		{
			TravelParty party = null;
			foreach (Character character in village.Vikings())
			{
				Humanoid viking = (character as Humanoid);
				if (viking != null && viking.GetComponent<VikingCore>() != null)
				{
					bool first = (party == null);
					party = (party ?? new TravelParty());
					TravelPlanner.AddTo(party, viking, first);
				}
			}
			if (party == null)
			{
				party = TravelParty.Of(VillageFarmhouses.HardestBiomeNear(village.Farm, VillageFarmhouses.HardBiomeVikingReach));
			}
			fire.Set(DisbandAtKey, NetTime.Now());
			fire.Set(GatheredAtKey, 0L);
			fire.Set(RemainKey, Random.Range(RemainMin, RemainMax));
			fire.Set(SearchedKey, SearchPending);
			fire.Set(BiomeKey, party.Easiest);
			fire.Set(HardestKey, party.Hardest);
			fire.Set(UnteleportableKey, party.Unteleportable);
			fire.Set(VillageTravel.DestinationKey, Vector3.zero);
			ZDOID id = fire.m_uid;
			fires.Remove(id);
			fires.Add(id);
			Vector3 from = fire.GetPosition();
			Vector3 farm = village.Farm;
			TravelPlanner.Ask(from, party, new List<Vector3> { farm }, (TravelPlanner.Answer answer) => Answered(id, farm, answer));
			NeighborNews.Report(NeighborEvent.Disbanded, farm, VillageNames.Get(village), "");
		}

		/// <summary>
		/// Reads what a disbanded village's vikings can do on the road together, as noted on its bonfire
		/// </summary>
		/// <param name="fire">The bonfire's ZDO</param>
		/// <param name="fallback">The biome to take when none is noted, by VikingGear's order</param>
		/// <returns>The party</returns>
		public static TravelParty PartyOf(ZDO fire, int fallback)
		{
			TravelParty party = new TravelParty();
			party.Easiest = fire.GetInt(BiomeKey, fallback);
			party.Hardest = fire.GetInt(HardestKey, party.Easiest);
			party.Unteleportable = fire.GetBool(UnteleportableKey);
			return(party);
		}

		/// <summary>
		/// Notes the village the vikings go to on the bonfire, as the server answers - and sends the vikings that were
		/// kept at the bonfire, as no player was near them, onto the road there, and the vikings out on the village's
		/// quests there too (see VillageTravel.QuestsDisbanded)
		/// </summary>
		/// <param name="id">The bonfire's ZDO</param>
		/// <param name="farm">Where the village disbanded stood</param>
		/// <param name="answer">The server's answer</param>
		private static void Answered(ZDOID id, Vector3 farm, TravelPlanner.Answer answer)
		{
			ZDO fire = ((ZDOMan.instance != null) ? ZDOMan.instance.GetZDO(id) : null);
			if (fire == null || fire.GetLong(DisbandAtKey, 0L) == 0L)
			{
				return;
			}
			VillageTravel.QuestsDisbanded(farm, fire.GetPosition(), answer);
			if (!fire.IsOwner())
			{
				fire.SetOwner(ZDOMan.GetSessionID());
			}
			if (!answer.Found)
			{
				fire.Set(SearchedKey, SearchNone);
				return;
			}
			fire.Set(VillageTravel.DestinationKey, answer.Destination);
			fire.Set(VillageTravel.DestinationNameKey, answer.Name);
			fire.Set(VillageTravel.DestinationAuthorKey, answer.Author);
			fire.Set(VillageTravel.RouteKey, answer.Road.Encode());
			fire.Set(SearchedKey, SearchFound);
			List<byte[]> kept = VillageDeparted.TakeKept(fire);
			if (kept.Count == 0)
			{
				return;
			}
			TravelEntry entry = new TravelEntry();
			entry.Vikings = kept;
			entry.Destination = answer.Destination;
			entry.Name = answer.Name;
			entry.From = answer.Road.Approach();
			entry.OriginFire = fire.GetPosition();
			entry.Unloaded = fire.GetLong(DisbandAtKey, 0L) + NetTime.Ticks(GatherMax + fire.GetFloat(RemainKey, RemainMax) + SetOutDelay);
			entry.Remaining = answer.Seconds;
			entry.Due = entry.Unloaded + NetTime.Ticks(answer.Seconds);
			entry.FromName = VillageNames.AtFire(fire);
			TravelParty party = PartyOf(fire, 0);
			entry.Biome = party.Easiest;
			entry.BoatBiome = party.Hardest;
			entry.Unteleportable = party.Unteleportable;
			VillageTravel.Queue(entry);
			Plugin.Log.LogInfo("Travel: " + kept.Count + " vikings of the village disbanded at " + fire.GetPosition().ToString("F0") + " that no player was near set out for the village at " + answer.Destination.ToString("F0"));
		}

		/// <summary>
		/// Checks whether a bonfire's village was disbanded with its vikings gathering at it, rather than leaving the way
		/// they did before
		/// </summary>
		/// <param name="fire">The bonfire's ZDO, or null</param>
		/// <returns>True when it was</returns>
		public static bool Gathers(ZDO fire)
		{
			return(fire != null && fire.GetLong(DisbandAtKey, 0L) != 0L);
		}

		/// <summary>
		/// Forgets a disbanded village's gathering at its bonfire, as a village is started there again
		/// </summary>
		/// <param name="fire">The bonfire's ZDO, owned by this client</param>
		public static void Clear(ZDO fire)
		{
			fire.Set(DisbandAtKey, 0L);
			fire.Set(GatheredAtKey, 0L);
			fire.Set(SearchedKey, SearchPending);
			fire.Set(VillageTravel.DestinationKey, Vector3.zero);
			fires.Remove(fire.m_uid);
		}

		/// <summary>
		/// Works out where the group of a village disbanded at a bonfire is at now
		/// </summary>
		/// <param name="fire">The bonfire's ZDO</param>
		/// <param name="announceAt">Set to when one of them says where they're going, in ZNet time ticks</param>
		/// <param name="setOutAt">Set to when they set out, in ticks</param>
		/// <returns>Where they're at</returns>
		public static Phase PhaseOf(ZDO fire, out long announceAt, out long setOutAt)
		{
			return(PhaseAt(NetTime.Now(), fire.GetLong(DisbandAtKey, 0L), fire.GetLong(GatheredAtKey, 0L), fire.GetFloat(RemainKey, RemainMax), fire.GetInt(SearchedKey, SearchPending) != SearchPending, NetTime.Ticks(1f), out announceAt, out setOutAt));
		}

		/// <summary>
		/// Works out where the group of a disbanded village is at - gathering at the fire until they're all there or
		/// GatherMax is up, staying a while, one of them saying where they're going once the server has answered or
		/// AnswerWait is up, and setting out SetOutDelay later
		/// </summary>
		/// <param name="now">The time now, in ticks</param>
		/// <param name="disbandAt">When the village was disbanded, in ticks</param>
		/// <param name="gatheredAt">When they were all at the fire, in ticks, or 0 while they aren't</param>
		/// <param name="remain">How long they stay once they're gathered, in seconds</param>
		/// <param name="answered">Whether the server has answered where they go</param>
		/// <param name="ticksPerSecond">How many ticks a second has</param>
		/// <param name="announceAt">Set to when one of them says where they're going, in ticks</param>
		/// <param name="setOutAt">Set to when they set out, in ticks</param>
		/// <returns>Where they're at</returns>
		internal static Phase PhaseAt(long now, long disbandAt, long gatheredAt, float remain, bool answered, long ticksPerSecond, out long announceAt, out long setOutAt)
		{
			long gatherEnd = disbandAt + (long)(GatherMax * ticksPerSecond);
			if (gatheredAt > 0L && gatheredAt < gatherEnd)
			{
				gatherEnd = gatheredAt;
			}
			announceAt = gatherEnd + (long)(remain * ticksPerSecond);
			if (!answered)
			{
				long late = announceAt + (long)(AnswerWait * ticksPerSecond);
				announceAt = ((now < late) ? System.Math.Max(announceAt, now + 1L) : late);
			}
			setOutAt = announceAt + (long)(SetOutDelay * ticksPerSecond);
			if (now < gatherEnd)
			{
				return(Phase.Gathering);
			}
			if (now < announceAt)
			{
				return(Phase.Remaining);
			}
			return((now < setOutAt) ? Phase.Announcing : Phase.SetOut);
		}

		/// <summary>
		/// Lists the vikings of the group of a village disbanded at a bonfire that this client has - those still leaving
		/// it and not on the road yet
		/// </summary>
		/// <param name="fire">The bonfire's ZDO</param>
		/// <returns>Their ZDOs</returns>
		public static List<ZDO> Group(ZDO fire)
		{
			List<ZDO> zdos = new List<ZDO>();
			VillageRespawn.ZdosNear(fire.GetPosition(), GroupReach, zdos);
			Vector3 position = fire.GetPosition();
			return(zdos.FindAll((ZDO zdo) => zdo.GetPrefab() == VikingCore.LivingPrefabHash && zdo.GetBool(VillageFounding.LeavingKey) && !VillageTravel.OnRoad(zdo) && Utils.DistanceXZ(VillageDeparted.FirePosition(zdo), position) <= 1f));
		}

		/// <summary>
		/// Checks whether a viking is the one of its group to say where they're going - the one with the lowest ID, so
		/// every client picks the same one
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <param name="fire">The bonfire's ZDO</param>
		/// <returns>True when it is</returns>
		public static bool Announces(ZDO viking, ZDO fire)
		{
			foreach (ZDO other in Group(fire))
			{
				if (Before(other.m_uid, viking.m_uid))
				{
					return(false);
				}
			}
			return(true);
		}

		/// <summary>
		/// Checks whether one ZDO's ID comes before another's
		/// </summary>
		/// <param name="a">One ID</param>
		/// <param name="b">The other</param>
		/// <returns>True when a comes first</returns>
		internal static bool Before(ZDOID a, ZDOID b)
		{
			return(a.UserID < b.UserID || (a.UserID == b.UserID && a.ID < b.ID));
		}

		/// <summary>
		/// Reads the village a disbanded village's vikings go to from its bonfire, as the server answered
		/// </summary>
		/// <param name="fire">The bonfire's ZDO</param>
		/// <returns>The answer, not found when there's none</returns>
		public static TravelPlanner.Answer Destination(ZDO fire)
		{
			TravelPlanner.Answer answer = new TravelPlanner.Answer();
			answer.Destination = fire.GetVec3(VillageTravel.DestinationKey, Vector3.zero);
			answer.Found = (fire.GetInt(SearchedKey, SearchPending) == SearchFound && answer.Destination != Vector3.zero);
			answer.Name = fire.GetString(VillageTravel.DestinationNameKey, "");
			answer.Author = fire.GetString(VillageTravel.DestinationAuthorKey, "");
			answer.Road = TravelRoad.Decode(fire.GetByteArray(VillageTravel.RouteKey));
			return(answer);
		}

		/// <summary>
		/// Gets the name of the group a disbanded village's vikings set out as
		/// </summary>
		/// <param name="fire">The bonfire's ZDO</param>
		/// <returns>The group's name</returns>
		public static string GroupName(ZDO fire)
		{
			Vector3 position = fire.GetPosition();
			return("disbanded " + position.x.ToString("F0") + " " + position.z.ToString("F0") + " " + fire.GetLong(DisbandAtKey, 0L));
		}

		/// <summary>
		/// Notes on each bonfire this client disbanded a village at when its vikings are all gathered there, ending the
		/// wait for the rest - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (fires.Count == 0 || Time.time < nextCheck || ZDOMan.instance == null || !ServerCheck.Active)
			{
				return;
			}
			nextCheck = Time.time + CheckInterval;
			for (int i = fires.Count - 1; i >= 0; i--)
			{
				ZDO fire = ZDOMan.instance.GetZDO(fires[i]);
				if (fire == null || !fire.IsOwner() || !Gathers(fire) || fire.GetLong(GatheredAtKey, 0L) != 0L || PhaseOf(fire, out long _, out long _) != Phase.Gathering)
				{
					fires.RemoveAt(i);
					continue;
				}
				Vector3 position = fire.GetPosition();
				List<ZDO> group = Group(fire);
				if (group.Count > 0 && group.TrueForAll((ZDO zdo) => Utils.DistanceXZ(zdo.GetPosition(), position) <= GatheredReach))
				{
					fire.Set(GatheredAtKey, NetTime.Now());
					fires.RemoveAt(i);
				}
			}
		}

		/// <summary>
		/// Forgets the bonfires this client disbanded villages at, as it leaves a world
		/// </summary>
		public static void Forget()
		{
			fires.Clear();
		}
	}
}
