using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The animals traveling vikings bring along - by the biome a traveler comes from, half the time, a tamed wolf from
	/// the Mountains, and a saddled lox from the Plains, valkyrie from the Mistlands, asksvin from the Ashlands and moose
	/// from the Deep North - with 0-2 stars and a name of its kind, never its owner's (see TravelerPet)
	/// </summary>
	internal static class TravelerPets
	{
		/// <summary>
		/// On a traveler, its animal
		/// </summary>
		public static readonly KeyValuePair<int, int> PetKey = ZDO.GetHashZDOID("kriona.VikingVillages.travelerPet");

		/// <summary>
		/// On a traveler, that it's setting off from the village, so its animal goes with it
		/// </summary>
		public static readonly int LeavingKey = "kriona.VikingVillages.travelerLeaving".GetStableHashCode();

		/// <summary>
		/// On a traveler, that it comes in riding its saddled animal, until it's on it, and that it's riding it, so the
		/// animal carries it
		/// </summary>
		public static readonly int RideInKey = "kriona.VikingVillages.travelerRideIn".GetStableHashCode();
		public static readonly int RidingKey = "kriona.VikingVillages.travelerRiding".GetStableHashCode();

		/// <summary>
		/// On a traveler's animal, the traveler, the farm of the village they visit, the spot at the village's edge it
		/// keeps to while there, when the traveler leaves in ZNet time ticks, whether it keeps by the traveler rather than
		/// at its spot, whether the traveler has walked into the village, and whether the traveler died, leaving it to the
		/// village. Creature Marker reads HostKey and OrphanKey to mark a traveler's animal as theirs rather than a
		/// player's, so those two can never change
		/// </summary>
		public static readonly KeyValuePair<int, int> OwnerKey = ZDO.GetHashZDOID("kriona.VikingVillages.petOwner");
		public static readonly int HostKey = "kriona.VikingVillages.petHost".GetStableHashCode();
		public static readonly int SpotKey = "kriona.VikingVillages.petSpot".GetStableHashCode();
		public static readonly int LeaveAtKey = "kriona.VikingVillages.petLeaveAt".GetStableHashCode();
		public static readonly int FollowsKey = "kriona.VikingVillages.petFollows".GetStableHashCode();
		public static readonly int SettledKey = "kriona.VikingVillages.petSettled".GetStableHashCode();
		public static readonly int OrphanKey = "kriona.VikingVillages.petOrphan".GetStableHashCode();

		/// <summary>
		/// How likely a traveler is to bring an animal from a biome that has one, and the most stars it has
		/// </summary>
		internal const float PetChance = 0.5f;
		internal const int MaxStars = 2;

		/// <summary>
		/// How far from its spot an animal comes in beside its traveler, and how far past the village's edge the village's
		/// vikings go to pet it, in meters
		/// </summary>
		private const float SpawnSpread = 2f;
		internal const float PetReach = 25f;

		/// <summary>
		/// How far past the village's edge a valkyrie lands her traveler, how much farther out on the ground they start
		/// from when they fly in, and how far a valkyrie flying off is from every player once she and her traveler are
		/// gone, in meters - farther than a traveler on foot, since she's seen far off in the sky
		/// </summary>
		private const float LandOutside = 3f;
		private const float FlyInReach = 50f;
		internal const float FlownGoneReach = 80f;

		/// <summary>
		/// A kind of animal a traveler brings - its prefab, whether it wears a saddle, whether its traveler rides it in and
		/// out, whether it flies them rather than carrying them on the ground, whether it keeps by the traveler rather than
		/// waiting at the village's edge, and the names it's given
		/// </summary>
		internal class Kind
		{
			public string Prefab;
			public bool Saddled;
			public bool Ridden;
			public bool Flies;
			public bool Follows;
			public string[] Names;
		}

		/// <summary>
		/// The names a wolf is given
		/// </summary>
		internal static readonly string[] WolfNames =
		{
			"Sköll", "Freki", "Larentia", "Moro", "Nymeria", "Grey Wind", "White Fang", "Akela", "Gmork", "Maugrim", "Lupo",
			"Kiba", "Wendell", "Hati", "Mánagarmr", "Varg", "Kveldúlf", "Gráfeldr"
		};

		/// <summary>
		/// The names a lox is given
		/// </summary>
		internal static readonly string[] LoxNames =
		{
			"Auðumbla", "Hjortron", "Krusbär", "Jordgubbe", "Smultron", "Honung", "Torstein", "Bergdis", "Järnben", "Gråsten",
			"Brynja", "Torvald", "Heidrun", "Gjalp", "Greip"
		};

		/// <summary>
		/// The names a valkyrie is given, which no viking goes by (see VikingNames.FemaleNames)
		/// </summary>
		internal static readonly string[] ValkyrieNames =
		{
			"Brynhildr", "Sigrún", "Sigrdrífa", "Göndul", "Gunnr", "Skuld"
		};

		/// <summary>
		/// The names an asksvin is given
		/// </summary>
		internal static readonly string[] AsksvinNames =
		{
			"Ikdnøkke", "Ildnäkki", "Ildmanden", "Bäckahästen", "Bålmanden", "Flammegrimen", "Kvernknurr", "Ildnix", "Glødemanden"
		};

		/// <summary>
		/// The names a moose is given
		/// </summary>
		internal static readonly string[] MooseNames =
		{
			"Kristoff", "Sven"
		};

		/// <summary>
		/// The animal a traveler from each biome may bring. The valkyrie is Viking Villages' own tamed valkyrie (see
		/// ValkyriePrefab), who flies her traveler in and out (see ValkyrieFlight.Pilot)
		/// </summary>
		internal static readonly Dictionary<Heightmap.Biome, Kind> Kinds = new Dictionary<Heightmap.Biome, Kind>
		{
			{ Heightmap.Biome.Mountain, new Kind { Prefab = "Wolf", Saddled = false, Ridden = false, Flies = false, Follows = true, Names = WolfNames } },
			{ Heightmap.Biome.Plains, new Kind { Prefab = "Lox", Saddled = true, Ridden = true, Flies = false, Follows = false, Names = LoxNames } },
			{ Heightmap.Biome.Mistlands, new Kind { Prefab = ValkyriePrefab.PrefabName, Saddled = true, Ridden = true, Flies = true, Follows = false, Names = ValkyrieNames } },
			{ Heightmap.Biome.AshLands, new Kind { Prefab = "Asksvin", Saddled = true, Ridden = true, Flies = false, Follows = false, Names = AsksvinNames } },
			{ Heightmap.Biome.DeepNorth, new Kind { Prefab = "Moose", Saddled = true, Ridden = true, Flies = false, Follows = false, Names = MooseNames } }
		};

		/// <summary>
		/// Finds the kind of animal a traveler brings that a creature is
		/// </summary>
		/// <param name="animal">The creature, or null</param>
		/// <returns>The kind, or null when it's none of them</returns>
		private static Kind KindOf(Character animal)
		{
			if (animal == null)
			{
				return(null);
			}
			string prefab = Utils.GetPrefabName(animal.gameObject);
			foreach (Kind kind in Kinds.Values)
			{
				if (kind.Prefab == prefab)
				{
					return(kind);
				}
			}
			return(null);
		}

		/// <summary>
		/// Checks whether a traveler's animal is one it rides in and out (see Kind.Ridden)
		/// </summary>
		/// <param name="animal">The animal</param>
		/// <returns>True when it is</returns>
		internal static bool IsRidden(Character animal)
		{
			Kind kind = KindOf(animal);
			return(kind != null && kind.Ridden);
		}

		/// <summary>
		/// Checks whether a creature is kept from players who haven't turned on PVP, the way a player is - a traveler's
		/// valkyrie, until she's left to the village, and a traveler who brought one, even once she's gone
		/// </summary>
		/// <param name="character">The creature</param>
		/// <returns>True when it is</returns>
		internal static bool NeedsPVP(Character character)
		{
			if (character == null || character.m_nview == null || !character.m_nview.IsValid())
			{
				return(false);
			}
			ZDO zdo = character.m_nview.GetZDO();
			if (Traveler.Is(zdo))
			{
				Kind kind = KindFor(zdo.GetInt(Traveler.OriginKey, -1));
				return(kind != null && kind.Flies && !zdo.GetZDOID(PetKey).IsNone());
			}
			Kind own = KindOf(character);
			return(own != null && own.Flies && IsPet(zdo) && !zdo.GetBool(OrphanKey));
		}

		/// <summary>
		/// Picks the animal a traveler from a biome brings - half the time when its biome has one (see Kinds), or always
		/// </summary>
		/// <param name="origin">The biome, by its index in VikingGear's tables</param>
		/// <param name="always">Whether it always brings one when its biome has one, as the console asks for</param>
		/// <returns>The kind, or null when it brings none</returns>
		internal static Kind Pick(int origin, bool always)
		{
			Kind kind = KindFor(origin);
			return((kind != null && (always || Random.value < PetChance)) ? kind : null);
		}

		/// <summary>
		/// Works out where a traveler flown in on a valkyrie starts and where she lands them - landing on open ground
		/// LandOutside past the village's edge, on the side of the spot they'd have walked in from, and starting
		/// FlyInReach farther out on the ground, out of every player's sight. They start from the spot they'd have walked
		/// in from when that point isn't loaded, is in the water or can be seen
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="arrival">The spot outside the village they'd have walked in from (see VillageRespawn.ReadyToArrive)</param>
		/// <param name="from">Set to where they start</param>
		/// <param name="landing">Set to where she lands them</param>
		internal static void FlightPlan(Village village, Vector3 arrival, out Vector3 from, out Vector3 landing)
		{
			Vector3 outward = arrival - village.Middle;
			outward.y = 0f;
			outward = ((outward.sqrMagnitude > 0.01f) ? outward.normalized : Vector3.forward);
			float pull = Mathf.Max(0f, village.DistanceOutside(arrival) - LandOutside);
			if (!village.OpenSpotNear(arrival - outward * pull, out landing))
			{
				landing = arrival;
			}
			from = arrival;
			Vector3 far = landing + outward * FlyInReach;
			if (ZoneSystem.instance == null || !ZoneSystem.instance.IsZoneLoaded(far) || !ZoneSystem.instance.GetSolidHeight(far, out float height) || height <= ZoneSystem.instance.m_waterLevel)
			{
				return;
			}
			far.y = height;
			if (VillageRespawn.Hidden(far))
			{
				from = far;
			}
		}

		/// <summary>
		/// Picks the name an animal is given - one of its kind's at random, leaving out its owner's first name
		/// </summary>
		/// <param name="names">Its kind's names</param>
		/// <param name="owner">Its owner's name, with or without a byname, or ""</param>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>The name, or "" when there's none left</returns>
		internal static string PickName(string[] names, string owner, float roll)
		{
			string first = VikingLoreSpeech.FirstName(owner ?? "");
			List<string> free = new List<string>(names).FindAll((string name) => !string.Equals(name, first, System.StringComparison.OrdinalIgnoreCase));
			if (free.Count == 0)
			{
				return("");
			}
			return(free[Mathf.Clamp(Mathf.FloorToInt(roll * free.Count), 0, free.Count - 1)]);
		}

		/// <summary>
		/// Finds the kind of animal a traveler from a biome may bring
		/// </summary>
		/// <param name="origin">The biome, by its index in VikingGear's tables</param>
		/// <returns>The kind, or null for a biome that has none</returns>
		internal static Kind KindFor(int origin)
		{
			if (origin < 0 || origin >= VikingGear.Biomes.Length)
			{
				return(null);
			}
			return(Kinds.TryGetValue(VikingGear.Biomes[origin].Biome, out Kind kind) ? kind : null);
		}

		/// <summary>
		/// Has a traveler just sent to a village bring an animal (see Pick) - spawned beside it, tamed, with 0-2 stars and a
		/// saddle for a kind that wears one, keeping by it as it walks in, or carrying it in for one it rides. Its name is
		/// given once its owner has one (see TravelerPet)
		/// </summary>
		/// <param name="traveler">The traveler's ZDO, owned by this client</param>
		/// <param name="kind">The kind of animal, or null for none</param>
		/// <param name="from">Where the traveler starts, outside the village</param>
		/// <param name="spot">The spot at the village's edge its animal keeps to - where it starts from, or for a valkyrie,
		/// where she lands it (see FlightPlan)</param>
		/// <param name="village">The village it visits</param>
		public static void Bring(ZDO traveler, Kind kind, Vector3 from, Vector3 spot, Village village)
		{
			if (kind == null || ZNetScene.instance == null)
			{
				return;
			}
			GameObject prefab = ZNetScene.instance.GetPrefab(kind.Prefab);
			if (prefab == null)
			{
				Plugin.Log.LogWarning("A traveler couldn't bring a " + kind.Prefab + " - the prefab is missing");
				return;
			}
			Vector2 offset = Random.insideUnitCircle * SpawnSpread;
			GameObject instance = Object.Instantiate(prefab, from + new Vector3(offset.x, 0f, offset.y), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
			Character animal = instance.GetComponent<Character>();
			ZNetView view = instance.GetComponent<ZNetView>();
			if (animal == null || view == null || !view.IsValid())
			{
				return;
			}
			ZDO zdo = view.GetZDO();
			animal.SetLevel(1 + VikingStartFood.CountFor(Random.value, 0, MaxStars));
			animal.SetTamed(true);
			zdo.Set(OwnerKey, traveler.m_uid);
			zdo.Set(HostKey, village.Farm);
			zdo.Set(SpotKey, spot);
			zdo.Set(LeaveAtKey, traveler.GetLong(Traveler.LeaveAtKey, 0L));
			zdo.Set(FollowsKey, kind.Follows);
			Tameable tameable = instance.GetComponent<Tameable>();
			if (kind.Saddled && tameable != null && tameable.m_saddle != null)
			{
				zdo.Set(ZDOVars.s_haveSaddleHash, true);
				view.InvokeRPC(ZNetView.Everybody, "SetSaddle", true);
				// A kind ridden carries its traveler in, or flies them (see VillageLife.UpdateRiding)
				if (kind.Ridden)
				{
					traveler.Set(RideInKey, true);
				}
			}
			traveler.Set(PetKey, zdo.m_uid);
			if (instance.GetComponent<TravelerPet>() == null)
			{
				instance.AddComponent<TravelerPet>();
			}
			TestLog.Count("Traveler pets", kind.Prefab);
			Plugin.Log.LogInfo("The traveler brought a " + kind.Prefab + " with " + (animal.GetLevel() - 1) + " stars");
		}

		/// <summary>
		/// Checks whether a creature is an animal a traveler brought, its traveler alive, or left to the village
		/// </summary>
		/// <param name="zdo">The creature's ZDO, or null</param>
		/// <returns>True when it is</returns>
		public static bool IsPet(ZDO zdo)
		{
			return(zdo != null && zdo.GetVec3(HostKey, Vector3.zero) != Vector3.zero);
		}

		/// <summary>
		/// Checks whether a creature is an animal a traveler brought to a village, or left to it, that the village's
		/// vikings and travelers pet - alive and tamed, in the village or within PetReach past its edge. A valkyrie isn't
		/// petted by anyone, being a creature of her own mind (see ValkyrieTalk)
		/// </summary>
		/// <param name="character">The creature</param>
		/// <param name="village">The village</param>
		/// <returns>True when it is</returns>
		public static bool PettableIn(Character character, Village village)
		{
			if (character == null || character.IsDead() || !character.IsTamed() || character.m_nview == null || !character.m_nview.IsValid() || character.GetComponent<ValkyrieFlight>() != null)
			{
				return(false);
			}
			ZDO zdo = character.m_nview.GetZDO();
			return(IsPet(zdo) && Utils.DistanceXZ(zdo.GetVec3(HostKey, Vector3.zero), village.Farm) <= Village.FarmReach && village.DistanceOutside(character.transform.position) <= PetReach);
		}

		/// <summary>
		/// Finds a traveler's animal, when it's loaded
		/// </summary>
		/// <param name="traveler">The traveler's ZDO</param>
		/// <returns>The animal, or null</returns>
		public static Character PetOf(ZDO traveler)
		{
			ZDOID id = traveler.GetZDOID(PetKey);
			GameObject found = ((ZNetScene.instance != null && !id.IsNone()) ? ZNetScene.instance.FindInstance(id) : null);
			return((found != null) ? found.GetComponent<Character>() : null);
		}

		/// <summary>
		/// Leaves a traveler's animal to the village as the traveler dies - an ordinary tamed animal from then on, keeping
		/// its name and saddle, staying where it is
		/// </summary>
		/// <param name="traveler">The traveler's ZDO</param>
		public static void Orphan(ZDO traveler)
		{
			Character animal = PetOf(traveler);
			if (animal == null || animal.IsDead())
			{
				return;
			}
			if (!animal.m_nview.IsOwner())
			{
				animal.m_nview.ClaimOwnership();
			}
			animal.m_nview.GetZDO().Set(OrphanKey, true);
			Plugin.Log.LogInfo("A traveler's " + Utils.GetPrefabName(animal.gameObject) + " stays on with the village, its owner dead");
		}
	}
}
