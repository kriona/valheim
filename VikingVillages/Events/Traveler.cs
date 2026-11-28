using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A traveling viking - a PC viking from a biome at least BiomeGap harder than the village it visits, which comes
	/// by in the latter half of a day, stays the night by the fire and sets off again the next morning, trading some of
	/// its food for the village's on the way out (see TravelerEvent and EVENTS.md)
	/// </summary>
	/// <remarks>
	/// A traveler is marked with the farm of the village it visits, and never noted as one of the village's own, so it
	/// takes no bed, keeps no watch, goes on no quests and isn't counted, mourned or brought back. It never learns,
	/// uses or teaches a forsaken power, and players can't name it or open its inventory
	/// </remarks>
	internal static class Traveler
	{
		/// <summary>
		/// On a traveler, the farm of the village it visits, the biome it comes from by its index in VikingGear's tables,
		/// and when it leaves in ZNet time ticks
		/// </summary>
		public static readonly int HostKey = "kriona.VikingVillages.travelerHost".GetStableHashCode();
		public static readonly int OriginKey = "kriona.VikingVillages.travelerOrigin".GetStableHashCode();
		public static readonly int LeaveAtKey = "kriona.VikingVillages.travelerLeaveAt".GetStableHashCode();

		/// <summary>
		/// On a traveler, the village it came from and the one it goes to, and some of the vikings of each, by name, one a
		/// line
		/// </summary>
		public static readonly int FromKey = "kriona.VikingVillages.travelerFrom".GetStableHashCode();
		public static readonly int ToKey = "kriona.VikingVillages.travelerTo".GetStableHashCode();
		public static readonly int FromFolkKey = "kriona.VikingVillages.travelerFromFolk".GetStableHashCode();
		public static readonly int ToFolkKey = "kriona.VikingVillages.travelerToFolk".GetStableHashCode();

		/// <summary>
		/// On a traveler, when its owner last noted it loaded in ZNet time ticks, and whether it has traded its food with
		/// the village
		/// </summary>
		public static readonly int SeenKey = "kriona.VikingVillages.travelerSeen".GetStableHashCode();
		public static readonly int TradedKey = "kriona.VikingVillages.travelerTraded".GetStableHashCode();

		/// <summary>
		/// On a farm's generator, the last traveler to visit - its ZDO, name, the biome it came from, the villages it came
		/// from and goes to - and until when in ZNet time ticks the village talks about it, or 0 once it died there
		/// </summary>
		public static readonly KeyValuePair<int, int> VisitorKey = ZDO.GetHashZDOID("kriona.VikingVillages.visitor");
		public static readonly int VisitorNameKey = "kriona.VikingVillages.visitorName".GetStableHashCode();
		public static readonly int VisitorOriginKey = "kriona.VikingVillages.visitorOrigin".GetStableHashCode();
		public static readonly int VisitorFromKey = "kriona.VikingVillages.visitorFrom".GetStableHashCode();
		public static readonly int VisitorToKey = "kriona.VikingVillages.visitorTo".GetStableHashCode();
		public static readonly int VisitorUntilKey = "kriona.VikingVillages.visitorUntil".GetStableHashCode();

		/// <summary>
		/// How many biomes harder than the village's a traveler's own biome is, at least
		/// </summary>
		internal const int BiomeGap = 3;

		/// <summary>
		/// How many foods a traveler carries as it arrives, besides the three it has eaten
		/// </summary>
		internal const int FoodMin = 25;
		internal const int FoodMax = 30;

		/// <summary>
		/// How many foods it takes from a village chest as it leaves, and how many of its own it leaves there
		/// </summary>
		internal const int TradeMin = 3;
		internal const int TradeMax = 4;

		/// <summary>
		/// The time of day the vikings wake, and how many game hours a traveler stays after it before leaving
		/// </summary>
		internal const float DawnFraction = 0.25f;
		internal const float StayMinHours = 1f;
		internal const float StayMaxHours = 2f;

		/// <summary>
		/// How many game days after a traveler leaves the village still talks about it
		/// </summary>
		internal const float TalkDays = 2f;

		/// <summary>
		/// How often a traveler's owner notes it's loaded, and how long it goes unnoted before it counts as having been out
		/// of every player's reach, in seconds
		/// </summary>
		internal const float SeenInterval = 10f;
		internal const float UnseenGap = 30f;

		/// <summary>
		/// How long past its time to leave a traveler still trading its food goes on before it leaves anyway, in game hours
		/// </summary>
		internal const float TradeTimeoutHours = 1f;

		/// <summary>
		/// Checks whether a viking is a traveler
		/// </summary>
		/// <param name="zdo">The viking's ZDO, or null</param>
		/// <returns>True when it is</returns>
		public static bool Is(ZDO zdo)
		{
			return(zdo != null && zdo.GetVec3(HostKey, Vector3.zero) != Vector3.zero);
		}

		/// <summary>
		/// Checks whether a character is a traveler
		/// </summary>
		/// <param name="character">The character, or null</param>
		/// <returns>True when it is</returns>
		public static bool Is(Character character)
		{
			return(character != null && character.m_nview != null && character.m_nview.IsValid() && Is(character.m_nview.GetZDO()));
		}

		/// <summary>
		/// Gets the farm of the village a traveler visits
		/// </summary>
		/// <param name="zdo">The traveler's ZDO</param>
		/// <returns>The farm, or zero for a viking that isn't a traveler</returns>
		public static Vector3 HostOf(ZDO zdo)
		{
			return(zdo.GetVec3(HostKey, Vector3.zero));
		}

		/// <summary>
		/// Lists the biomes a traveler to a village can come from - every biome at least BiomeGap harder than the
		/// village's, up to the hardest
		/// </summary>
		/// <param name="village">The village's biome, by its index in VikingGear's tables</param>
		/// <param name="hardest">The hardest biome's index</param>
		/// <returns>The biomes, easiest first, or none when the village's is too hard</returns>
		internal static List<int> OriginBiomes(int village, int hardest)
		{
			List<int> biomes = new List<int>();
			for (int biome = Mathf.Max(0, village) + BiomeGap; biome <= hardest; biome++)
			{
				biomes.Add(biome);
			}
			return(biomes);
		}

		/// <summary>
		/// Picks the biome a traveler to a village comes from, each that it can come from as likely
		/// </summary>
		/// <param name="village">The village's biome, by its index in VikingGear's tables</param>
		/// <param name="hardest">The hardest biome's index</param>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>The biome's index, or -1 when there's none it can come from</returns>
		internal static int PickOrigin(int village, int hardest, float roll)
		{
			List<int> biomes = OriginBiomes(village, hardest);
			if (biomes.Count == 0)
			{
				return(-1);
			}
			return(biomes[Mathf.Clamp(Mathf.FloorToInt(roll * biomes.Count), 0, biomes.Count - 1)]);
		}

		/// <summary>
		/// Works out when a traveler arriving at a time leaves - the next time the vikings wake after it arrives, and some
		/// game hours after that
		/// </summary>
		/// <param name="now">When it arrives, in world seconds</param>
		/// <param name="dayLength">How long a game day is, in seconds</param>
		/// <param name="stayHours">How many game hours it stays after waking</param>
		/// <returns>When it leaves, in world seconds</returns>
		internal static double LeaveAt(double now, long dayLength, float stayHours)
		{
			if (dayLength <= 0)
			{
				return(now);
			}
			double dawn = (System.Math.Floor(now / dayLength) + DawnFraction) * dayLength;
			if (now >= dawn)
			{
				dawn += dayLength;
			}
			return(dawn + stayHours / 24.0 * dayLength);
		}

		/// <summary>
		/// Checks whether a traveler left while it was out of every player's reach - it was last noted loaded long enough
		/// ago to have been away, and its time to leave has passed
		/// </summary>
		/// <param name="now">The time now, in ticks</param>
		/// <param name="leaveAt">When it was to leave, in ticks</param>
		/// <param name="seen">When it was last noted loaded, in ticks, or 0 when it never was</param>
		/// <param name="gap">How long it goes unnoted while loaded, at most, in ticks</param>
		/// <returns>True when it did</returns>
		internal static bool LeftWhileAway(long now, long leaveAt, long seen, long gap)
		{
			return(seen > 0L && now - seen > gap && now >= leaveAt);
		}

		/// <summary>
		/// Works out how many of a food a traveler takes from a chest, keeping to what's there
		/// </summary>
		/// <param name="wanted">How many it wants</param>
		/// <param name="inChest">How many the chest holds</param>
		/// <returns>How many it takes</returns>
		internal static int TakeCount(int wanted, int inChest)
		{
			return(Mathf.Clamp(wanted, 0, Mathf.Max(0, inChest)));
		}

		/// <summary>
		/// Notes a traveler on the village it visits, for the village to talk about it while it's there and for TalkDays
		/// after it leaves, on the client that owns the village
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="zdo">The traveler's ZDO</param>
		public static void NoteVisit(Village village, ZDO zdo)
		{
			if (village == null || village.Zdo == null || !village.IsOwner() || EnvMan.instance == null)
			{
				return;
			}
			village.Zdo.Set(VisitorKey, zdo.m_uid);
			village.Zdo.Set(VisitorNameKey, VikingNames.Get(zdo));
			village.Zdo.Set(VisitorOriginKey, zdo.GetInt(OriginKey, -1));
			village.Zdo.Set(VisitorFromKey, zdo.GetString(FromKey));
			village.Zdo.Set(VisitorToKey, zdo.GetString(ToKey));
			village.Zdo.Set(VisitorUntilKey, zdo.GetLong(LeaveAtKey, 0L) + NetTime.Ticks(TalkDays * EnvMan.instance.m_dayLengthSec));
		}

		/// <summary>
		/// What a village remembers of the last traveler to visit it
		/// </summary>
		public class Visit
		{
			public string Name;
			public string Home;
			public string From;
			public string To;
			public bool Here;
		}

		/// <summary>
		/// Reads the last traveler to visit a village, while the village still talks about it
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The visit, or null when there's none to talk about</returns>
		public static Visit VisitOf(Village village)
		{
			if (village == null || village.Zdo == null)
			{
				return(null);
			}
			long now = NetTime.Now();
			string name = village.Zdo.GetString(VisitorNameKey);
			if (name.Length == 0 || now >= village.Zdo.GetLong(VisitorUntilKey, 0L))
			{
				return(null);
			}
			Visit visit = new Visit();
			visit.Name = name;
			visit.Home = HomeName(village.Zdo.GetInt(VisitorOriginKey, -1));
			visit.From = village.Zdo.GetString(VisitorFromKey);
			visit.To = village.Zdo.GetString(VisitorToKey);
			ZDOID id = village.Zdo.GetZDOID(VisitorKey);
			GameObject found = ((ZNetScene.instance != null && !id.IsNone()) ? ZNetScene.instance.FindInstance(id) : null);
			Character here = ((found != null) ? found.GetComponent<Character>() : null);
			visit.Here = (here != null && !here.IsDead());
			return(visit);
		}

		/// <summary>
		/// Has the village a traveler visits stop talking about it as it dies, on the client that owns the village, and
		/// leaves its animal to the village (see TravelerPets.Orphan)
		/// </summary>
		/// <param name="character">The creature that died</param>
		public static void OnDeath(Character character)
		{
			if (!Is(character))
			{
				return;
			}
			TravelerPets.Orphan(character.m_nview.GetZDO());
			Village village = Village.ForFarm(HostOf(character.m_nview.GetZDO()));
			if (village != null && village.Zdo != null && village.IsOwner() && village.Zdo.GetZDOID(VisitorKey) == character.GetZDOID())
			{
				village.Zdo.Set(VisitorUntilKey, 0L);
			}
		}

		/// <summary>
		/// Gets the name of the biome a traveler comes from, the way a viking says it
		/// </summary>
		/// <param name="biome">The biome's index in VikingGear's tables, or -1</param>
		/// <returns>The name, like "the Mountains", or "" when it's unknown</returns>
		public static string HomeName(int biome)
		{
			if (biome < 0 || biome >= VikingGear.Biomes.Length)
			{
				return("");
			}
			return(VillagePortalSpeech.BiomeName(VikingGear.Biomes[biome].Biome));
		}

		/// <summary>
		/// Reads the names of some of the vikings of the village a traveler came from or goes to
		/// </summary>
		/// <param name="zdo">The traveler's ZDO</param>
		/// <param name="key">FromFolkKey or ToFolkKey</param>
		/// <returns>The names</returns>
		public static List<string> Folk(ZDO zdo, int key)
		{
			return(SplitNames(zdo.GetString(key)));
		}

		/// <summary>
		/// Splits names written one a line
		/// </summary>
		/// <param name="text">The names</param>
		/// <returns>The names, leaving out blank lines</returns>
		internal static List<string> SplitNames(string text)
		{
			List<string> names = new List<string>();
			foreach (string line in (text ?? "").Split('\n'))
			{
				string name = line.Trim();
				if (name.Length > 0)
				{
					names.Add(name);
				}
			}
			return(names);
		}
	}
}
