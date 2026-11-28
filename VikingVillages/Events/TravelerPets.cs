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
		/// On a traveler's animal, the traveler, the farm of the village they visit, the spot at the village's edge it
		/// keeps to while there, when the traveler leaves in ZNet time ticks, whether it keeps by the traveler rather than
		/// at its spot, whether the traveler has walked into the village, and whether the traveler died, leaving it to the
		/// village
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
		/// A kind of animal a traveler brings - its prefab, whether it wears a saddle, whether it keeps by the traveler
		/// rather than waiting at the village's edge, and the names it's given
		/// </summary>
		internal class Kind
		{
			public string Prefab;
			public bool Saddled;
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
		/// ValkyriePrefab)
		/// </summary>
		internal static readonly Dictionary<Heightmap.Biome, Kind> Kinds = new Dictionary<Heightmap.Biome, Kind>
		{
			{ Heightmap.Biome.Mountain, new Kind { Prefab = "Wolf", Saddled = false, Follows = true, Names = WolfNames } },
			{ Heightmap.Biome.Plains, new Kind { Prefab = "Lox", Saddled = true, Follows = false, Names = LoxNames } },
			{ Heightmap.Biome.Mistlands, new Kind { Prefab = "TameValkyrie", Saddled = true, Follows = false, Names = ValkyrieNames } },
			{ Heightmap.Biome.AshLands, new Kind { Prefab = "Asksvin", Saddled = true, Follows = false, Names = AsksvinNames } },
			{ Heightmap.Biome.DeepNorth, new Kind { Prefab = "Moose", Saddled = true, Follows = false, Names = MooseNames } }
		};

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
		/// Has a traveler just sent to a village bring an animal, half the time when its biome has one (see Kinds) - spawned
		/// beside it, tamed, with 0-2 stars and a saddle for a kind that wears one, keeping by it as it walks in. Its name
		/// is given once its owner has one (see TravelerPet)
		/// </summary>
		/// <param name="traveler">The traveler's ZDO, owned by this client</param>
		/// <param name="origin">The biome it comes from, by its index in VikingGear's tables</param>
		/// <param name="spot">Where it arrives, outside the village - the spot at the village's edge its animal keeps to</param>
		/// <param name="village">The village it visits</param>
		public static void Bring(ZDO traveler, int origin, Vector3 spot, Village village)
		{
			Kind kind = KindFor(origin);
			if (kind == null || Random.value >= PetChance || ZNetScene.instance == null)
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
			GameObject instance = Object.Instantiate(prefab, spot + new Vector3(offset.x, 0f, offset.y), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
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
		/// vikings and travelers pet - alive and tamed, in the village or within PetReach past its edge
		/// </summary>
		/// <param name="character">The creature</param>
		/// <param name="village">The village</param>
		/// <returns>True when it is</returns>
		public static bool PettableIn(Character character, Village village)
		{
			if (character == null || character.IsDead() || !character.IsTamed() || character.m_nview == null || !character.m_nview.IsValid())
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
