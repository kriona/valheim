using Kriona.Vikings;

namespace VikingVillages
{
	/// <summary>
	/// A kind of creature living vikings hunt - its prefab, the biome it's the prey of, whether only an archer goes
	/// after it, and whether it's dangerous
	/// </summary>
	internal struct PreyKind
	{
		public string Prefab;
		public Heightmap.Biome Biome;

		/// <summary>
		/// Whether only an archer hunts it - one that flies, or runs too fast to catch
		/// </summary>
		public bool ArcherOnly;

		/// <summary>
		/// Whether it fights back hard enough that a hunter only goes after one with no other enemy near - and for one
		/// that herds, no other of its kind either, since a fight with one could turn into a fight with all of them
		/// </summary>
		public bool Dangerous;

		/// <summary>
		/// Whether others of its kind come to its aid, so another near it rules a hunt out like any other enemy - not a
		/// leech, which keeps to itself
		/// </summary>
		public bool Herds;

		/// <summary>
		/// Makes a kind of prey
		/// </summary>
		/// <param name="prefab">Its prefab name</param>
		/// <param name="biome">The biome it's the prey of</param>
		/// <param name="archerOnly">Whether only an archer hunts it</param>
		/// <param name="dangerous">Whether it's dangerous</param>
		/// <param name="herds">Whether others of its kind come to its aid</param>
		public PreyKind(string prefab, Heightmap.Biome biome, bool archerOnly, bool dangerous, bool herds)
		{
			Prefab = prefab;
			Biome = biome;
			ArcherOnly = archerOnly;
			Dangerous = dangerous;
			Herds = herds;
		}
	}

	/// <summary>
	/// The prey living vikings hunt in each biome, and which of it a viking takes on - prey of its own biome, the
	/// biomes before it, and the one right after, by the biome its gear comes from (see VikingCore.BiomeIndex), so a
	/// Meadows viking hunts in the Black Forest but not the Swamp
	/// </summary>
	/// <remarks>
	/// The young of a herd, like a lox calf or a wolf cub, aren't hunted. The Swamp's prey is the leech, which an archer
	/// shoots from the bank (see VikingCore.UpdatePathing), with no other enemy near but other leeches, and a lone
	/// draugr, with no other enemy near it - not even another draugr - for the entrails it drops, which sausages take
	/// </remarks>
	internal static class HuntPrey
	{
		/// <summary>
		/// How many biomes past its own a viking hunts the prey of
		/// </summary>
		internal const int BiomesAhead = 1;

		/// <summary>
		/// The monster a hunter fights on the way rather than running from, by prefab name
		/// </summary>
		public const string FoughtOnTheWay = "Greyling";

		/// <summary>
		/// Every kind of prey, in the order of its biomes
		/// </summary>
		internal static readonly PreyKind[] Kinds = new PreyKind[]
		{
			new PreyKind("Boar", Heightmap.Biome.Meadows, false, false, false),
			new PreyKind("Neck", Heightmap.Biome.Meadows, false, false, false),
			new PreyKind("Deer", Heightmap.Biome.Meadows, true, false, false),
			new PreyKind(FoughtOnTheWay, Heightmap.Biome.BlackForest, false, false, false),
			new PreyKind("Leech", Heightmap.Biome.Swamp, true, true, false),
			new PreyKind("Draugr", Heightmap.Biome.Swamp, false, true, true),
			new PreyKind("Wolf", Heightmap.Biome.Mountain, false, true, true),
			new PreyKind("Hatchling", Heightmap.Biome.Mountain, true, true, true),
			new PreyKind("Lox", Heightmap.Biome.Plains, false, true, true),
			new PreyKind("Deathsquito", Heightmap.Biome.Plains, true, true, true),
			new PreyKind("Hare", Heightmap.Biome.Mistlands, false, false, false),
			new PreyKind("Asksvin", Heightmap.Biome.AshLands, false, true, true),
			new PreyKind("Volture", Heightmap.Biome.AshLands, true, true, true),
			new PreyKind("Moose", Heightmap.Biome.DeepNorth, false, true, true),
			new PreyKind("Seal", Heightmap.Biome.DeepNorth, false, false, false)
		};

		/// <summary>
		/// Finds a kind of prey by its prefab name
		/// </summary>
		/// <param name="prefab">The prefab name</param>
		/// <param name="kind">The kind</param>
		/// <returns>False when the creature isn't prey</returns>
		internal static bool Find(string prefab, out PreyKind kind)
		{
			foreach (PreyKind candidate in Kinds)
			{
				if (candidate.Prefab == prefab)
				{
					kind = candidate;
					return(true);
				}
			}
			kind = new PreyKind();
			return(false);
		}

		/// <summary>
		/// Checks whether a viking hunts prey of a biome - one of its own biome or before it, or up to BiomesAhead past
		/// it
		/// </summary>
		/// <param name="preyBiome">The index of the prey's biome (see VikingGear.IndexOf)</param>
		/// <param name="vikingBiome">The index of the biome the viking's gear comes from</param>
		/// <returns>True when it does</returns>
		internal static bool InReach(int preyBiome, int vikingBiome)
		{
			return(preyBiome <= vikingBiome + BiomesAhead);
		}

		/// <summary>
		/// Checks whether a viking hunts a kind of creature - prey within its reach (see InReach), and for prey only an
		/// archer hunts, one that's an archer
		/// </summary>
		/// <param name="prefab">The creature's prefab name</param>
		/// <param name="archer">Whether the viking is an archer</param>
		/// <param name="vikingBiome">The index of the biome the viking's gear comes from</param>
		/// <returns>True when it does</returns>
		public static bool Hunts(string prefab, bool archer, int vikingBiome)
		{
			if (!Find(prefab, out PreyKind kind) || (kind.ArcherOnly && !archer))
			{
				return(false);
			}
			return(InReach(VikingGear.IndexOf(kind.Biome), vikingBiome));
		}

		/// <summary>
		/// Checks whether a kind of creature is prey that vikings from the Plains on only hunt as a party (see
		/// VillageLife.GroupHunts) - any but what only an archer hunts, which flies or runs too fast for a party
		/// </summary>
		/// <param name="prefab">The creature's prefab name</param>
		/// <returns>True when it is</returns>
		public static bool ForParty(string prefab)
		{
			return(Find(prefab, out PreyKind kind) && !kind.ArcherOnly);
		}

		/// <summary>
		/// Checks whether a kind of creature is prey that isn't dangerous, which a hunter doesn't mind near the prey it's
		/// after or near itself
		/// </summary>
		/// <param name="prefab">The creature's prefab name</param>
		/// <returns>True when it is</returns>
		public static bool IsHarmless(string prefab)
		{
			return(Find(prefab, out PreyKind kind) && !kind.Dangerous);
		}

		/// <summary>
		/// Checks whether a hunter doesn't mind a creature near the prey it's after or near itself - prey that isn't
		/// dangerous, or another of the prey's own kind when that kind doesn't herd, like another leech
		/// </summary>
		/// <param name="prey">The prey's prefab name, or null with no prey picked yet</param>
		/// <param name="other">The other creature's prefab name</param>
		/// <returns>True when it doesn't mind it</returns>
		public static bool Ignores(string prey, string other)
		{
			if (IsHarmless(other))
			{
				return(true);
			}
			return(prey != null && other == prey && Find(prey, out PreyKind kind) && !kind.Herds);
		}
	}
}
