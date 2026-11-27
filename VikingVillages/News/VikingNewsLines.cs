using System;
using System.Collections.Generic;
using Kriona.Vikings;

namespace VikingVillages
{
	/// <summary>
	/// What PC vikings say about the news (see VikingNews) - the lines for each event, kept in pools by what the
	/// event was about (see VikingNewsLinesDeeds, VikingNewsLinesDeaths and VikingNewsLinesFeats) - and the tables that
	/// sort what happened into those pools: the bosses and the biome each is from, Hildir's minibosses, the traders,
	/// dungeons, sea creatures and what kills a player
	/// </summary>
	/// <remarks>
	/// Every line is listed in the mod's DIALOG.md, which changes along with them. A line is told apart by the stable
	/// hash of its text, so editing a line only lets it be said again about news already going around
	/// </remarks>
	internal static partial class VikingNewsLines
	{
		/// <summary>
		/// Where the player's name and a creature's name go in a line
		/// </summary>
		public const string PlayerToken = "{player}";
		public const string CreatureToken = "{creature}";

		/// <summary>
		/// The pools of the sentences put after a line about a boss or biome beyond the viking's own
		/// </summary>
		public const string ImpressedBoss = "impressed/boss";
		public const string ImpressedBiome = "impressed/biome";

		/// <summary>
		/// The pools a tame or death goes to when its own isn't there
		/// </summary>
		public const string AnyTame = "tame/*";
		public const string AnyDeath = "death/any";

		/// <summary>
		/// Every pool, by its key, put together from the three tables the first time it's needed
		/// </summary>
		private static Dictionary<string, string[]> pools;

		/// <summary>
		/// The bosses, by prefab, with the index of the biome each is from (see VikingGear.Biomes)
		/// </summary>
		internal static readonly Dictionary<string, int> BossBiomes = new Dictionary<string, int>
		{
			{ "Eikthyr", 0 },
			{ "gd_king", 1 },
			{ "Bonemass", 2 },
			{ "Dragon", 3 },
			{ "GoblinKing", 4 },
			{ "SeekerQueen", 5 },
			{ "Fader", 6 },
			{ "FrozenKing", 7 }
		};

		/// <summary>
		/// Hildir's minibosses, by prefab, with the key each one's news goes by - Zil and Thungr are one miniboss
		/// </summary>
		internal static readonly Dictionary<string, string> Minibosses = new Dictionary<string, string>
		{
			{ "Skeleton_Hildir", "Brenna" },
			{ "Fenring_Cultist_Hildir", "Geirrhafa" },
			{ "GoblinBrute_Hildir", "ZilThungr" },
			{ "GoblinShaman_Hildir", "ZilThungr" }
		};

		/// <summary>
		/// The sea serpents, whose killing is news, and the creatures of the open sea, where no tree grows - which makes
		/// killing one with a tree all the more impressive
		/// </summary>
		internal static readonly HashSet<string> Serpents = new HashSet<string> { "Serpent", "BonemawSerpent" };
		internal static readonly HashSet<string> SeaCreatures = new HashSet<string> { "Serpent", "BonemawSerpent" };

		/// <summary>
		/// The achievements that are news, by the key the game names them by
		/// </summary>
		internal static readonly HashSet<string> Achievements = new HashSet<string>
		{
			"ach_grindtrees",
			"ach_grindcook",
			"ach_grindharvest",
			"ach_grindforager",
			"ach_grindhoney",
			"ach_grindeat",
			"ach_killallcreatures",
			"ach_findalltrophies",
			"ach_grindfish",
			"ach_bigfish",
			"ach_allweaponcraft",
			"ach_allitemcraft",
			"ach_allfoodcooked",
			"ach_allfoodeaten",
			"ach_builddyrnwyn",
			"ach_buildhigh",
			"ach_buildhighworld",
			"ach_allbuildpieces",
			"ach_explorensew",
			"ach_grindsail",
			"ach_grindsailhelm",
			"ach_kingsqueens",
			"ach_childofodin",
			"ach_deathbytreeall"
		};

		/// <summary>
		/// The creatures whose deaths share lines, by prefab, with the pool they share - those of a kind, like the
		/// greydwarves, share theirs with the creature's name filled in, and the rest have their own
		/// </summary>
		internal static readonly Dictionary<string, string> DeathGroups = new Dictionary<string, string>
		{
			{ "Greyling", "greyling" },
			{ "Greydwarf", "greydwarf" },
			{ "Greydwarf_Elite", "greydwarf" },
			{ "Greydwarf_Shaman", "greydwarf" },
			{ "Greydwarf_Frozen", "greydwarf" },
			{ "Greydwarf_Shaman_Frozen", "greydwarf" },
			{ "Boar", "boar" },
			{ "Neck", "neck" },
			{ "Troll", "troll" },
			{ "Troll_sleeping", "troll" },
			{ "TrollFrost", "troll" },
			{ "Bjorn", "bear" },
			{ "Bjorn_sleeping", "bear" },
			{ "Unbjorn", "bear" },
			{ "Skeleton", "skeleton" },
			{ "Skeleton_NoArcher", "skeleton" },
			{ "Skeleton_Meadows", "skeleton" },
			{ "Skeleton_Meadows_noarcher", "skeleton" },
			{ "Skeleton_Swamps", "skeleton" },
			{ "Skeleton_Swamps_noarcher", "skeleton" },
			{ "Skeleton_Mountains", "skeleton" },
			{ "Skeleton_Mountains_noarcher", "skeleton" },
			{ "Skeleton_DeepNorth", "skeleton" },
			{ "Skeleton_Poison", "skeleton" },
			{ "Skeleton_aspect", "skeleton" },
			{ "Ghost", "ghost" },
			{ "Ghost_sleeping", "ghost" },
			{ "Wraith", "ghost" },
			{ "TentaRoot", "root" },
			{ "TentaRoot_wild", "root" },
			{ "Tendril", "root" },
			{ "Tendril_back", "root" },
			{ "StoneGolem", "golem" },
			{ "Draugr", "draugr" },
			{ "Draugr_Elite", "draugr" },
			{ "Draugr_Ranged", "draugr" },
			{ "Draugr_sleeping", "draugr" },
			{ "Draugr_Elite_sleeping", "draugr" },
			{ "Draugr_Ranged_sleeping", "draugr" },
			{ "Blob", "blob" },
			{ "BlobElite", "blob" },
			{ "BlobTar", "blob" },
			{ "BlobFrost", "blob" },
			{ "BlobLava", "blob" },
			{ "BlobAspect", "blob" },
			{ "Leech", "leech" },
			{ "Leech_cave", "leech" },
			{ "Abomination", "abomination" },
			{ "Surtling", "surtling" },
			{ "Wolf", "wolf" },
			{ "Ulv", "wolf" },
			{ "Fenring", "fenring" },
			{ "Fenring_Cultist", "fenring" },
			{ "Hatchling", "drake" },
			{ "Bat", "bat" },
			{ "Bat_Swamp", "bat" },
			{ "Goblin", "fuling" },
			{ "GoblinArcher", "fuling" },
			{ "GoblinBrute", "fuling" },
			{ "GoblinShaman", "fuling" },
			{ "Deathsquito", "deathsquito" },
			{ "Lox", "lox" },
			{ "Seeker", "seeker" },
			{ "SeekerBrood", "seeker" },
			{ "SeekerBrute", "seeker" },
			{ "Tick", "tick" },
			{ "Gjall", "gjall" },
			{ "Dverger", "dvergr" },
			{ "DvergerAshlands", "dvergr" },
			{ "DvergerMage", "dvergr" },
			{ "DvergerMageFire", "dvergr" },
			{ "DvergerMageIce", "dvergr" },
			{ "DvergerMageSupport", "dvergr" },
			{ "Charred_Archer", "charred" },
			{ "Charred_Archer_Fader", "charred" },
			{ "Charred_Mage", "charred" },
			{ "Charred_Melee", "charred" },
			{ "Charred_Melee_Dyrnwyn", "charred" },
			{ "Charred_Melee_Fader", "charred" },
			{ "Charred_Twitcher", "charred" },
			{ "Charred_Twitcher_Summoned", "charred" },
			{ "Morgen", "morgen" },
			{ "Morgen_NonSleeping", "morgen" },
			{ "Asksvin", "asksvin" },
			{ "Volture", "volture" },
			{ "FallenValkyrie", "valkyrie" },
			{ "Serpent", "serpent" },
			{ "BonemawSerpent", "bonemaw" },
			{ VikingCore.PCPrefabName, "viking" },
			{ VikingCore.NPCPrefabName, "viking" },
			{ VikingCore.DvergrPrefabName, "dvergr" },
			{ VikingCore.ZombiePrefabName, "zombieviking" }
		};

		/// <summary>
		/// Gets every pool, putting them together the first time
		/// </summary>
		/// <returns>The pools, by key</returns>
		internal static Dictionary<string, string[]> Pools()
		{
			if (pools != null)
			{
				return(pools);
			}
			Dictionary<string, string[]> all = new Dictionary<string, string[]>();
			foreach (Dictionary<string, string[]> table in new[] { DeedPools, DeathPools, FeatPools, DvergrPools })
			{
				foreach (KeyValuePair<string, string[]> pool in table)
				{
					all[pool.Key] = pool.Value;
				}
			}
			pools = all;
			return(pools);
		}

		/// <summary>
		/// Gets a boss's prefab without its phase, for a boss fought in phases like Kall Fimbulbringer
		/// </summary>
		/// <param name="prefab">The boss's prefab</param>
		/// <returns>The boss's key, or "" when it isn't one of the bosses</returns>
		public static string BossKey(string prefab)
		{
			int phase = prefab.LastIndexOf("_p", StringComparison.Ordinal);
			if (phase > 0 && phase + 2 < prefab.Length && char.IsDigit(prefab[phase + 2]))
			{
				prefab = prefab.Substring(0, phase);
			}
			return(BossBiomes.ContainsKey(prefab) ? prefab : "");
		}

		/// <summary>
		/// Gets the key a miniboss's news goes by
		/// </summary>
		/// <param name="prefab">The creature's prefab</param>
		/// <returns>The key, or "" when it isn't one of Hildir's minibosses</returns>
		public static string MinibossKey(string prefab)
		{
			string plain = (prefab.EndsWith("_nochest", StringComparison.Ordinal) ? prefab.Substring(0, prefab.Length - "_nochest".Length) : prefab);
			return(Minibosses.TryGetValue(plain, out string key) ? key : "");
		}

		/// <summary>
		/// Gets the key a trader's news goes by
		/// </summary>
		/// <param name="prefab">The trader's prefab</param>
		/// <returns>Haldor, Hildir or BogWitch, or "" for another trader</returns>
		public static string TraderKey(string prefab)
		{
			string lower = prefab.ToLowerInvariant();
			if (lower.Contains("haldor"))
			{
				return("Haldor");
			}
			if (lower.Contains("hildir"))
			{
				return("Hildir");
			}
			return(lower.Contains("bogwitch") ? "BogWitch" : "");
		}

		/// <summary>
		/// Gets the key a dungeon's news goes by, from its location's name - Hildir's versions counting as the dungeon
		/// they're made from
		/// </summary>
		/// <param name="location">The location's prefab</param>
		/// <returns>BurialChambers, TrollCave or FrostCave, or "" for another location</returns>
		public static string DungeonKey(string location)
		{
			string lower = location.ToLowerInvariant();
			if (lower.Contains("sunken"))
			{
				return("");
			}
			if (lower.StartsWith("crypt", StringComparison.Ordinal) || lower.Contains("hildir_crypt"))
			{
				return("BurialChambers");
			}
			if (lower.Contains("trollcave"))
			{
				return("TrollCave");
			}
			return((lower.Contains("mountaincave") || lower.Contains("hildir_cave")) ? "FrostCave" : "");
		}

		/// <summary>
		/// Gets the pool a creature's killing of a player goes to - a boss's or miniboss's own, its group's (see
		/// DeathGroups), or the one for any creature
		/// </summary>
		/// <param name="prefab">The creature's prefab</param>
		/// <returns>The subject of the death</returns>
		public static string DeathSubject(string prefab)
		{
			string boss = BossKey(prefab);
			if (boss.Length > 0)
			{
				return("boss/" + boss);
			}
			string miniboss = MinibossKey(prefab);
			if (miniboss.Length > 0)
			{
				return("miniboss/" + miniboss);
			}
			return(DeathGroups.TryGetValue(prefab, out string group) ? group : "creature");
		}

		/// <summary>
		/// Gets the index of a biome found, in the order vikings' biomes go (see VikingGear.Biomes)
		/// </summary>
		/// <param name="subject">The biome's name, as Heightmap.Biome writes it</param>
		/// <returns>The index, or -1 for the ocean or an unknown biome</returns>
		public static int BiomeIndex(string subject)
		{
			if (!Enum.TryParse(subject, out Heightmap.Biome biome) || biome == Heightmap.Biome.Ocean)
			{
				return(-1);
			}
			for (int i = 0; i < VikingGear.Biomes.Length; i++)
			{
				if (VikingGear.Biomes[i].Biome == biome)
				{
					return(i);
				}
			}
			return(-1);
		}

		/// <summary>
		/// Gets the pool of lines about an event
		/// </summary>
		/// <param name="news">The event</param>
		/// <returns>The pool's key, which may not be there for an event of an unknown subject</returns>
		public static string PoolKey(VikingNews.Event news)
		{
			switch (news.Kind)
			{
			case VikingNews.AllSkills:
			case VikingNews.Leviathan:
			case VikingNews.Edge:
				return(news.Kind);
			case VikingNews.Tame:
				return(Pools().ContainsKey("tame/" + news.Subject) ? "tame/" + news.Subject : AnyTame);
			case VikingNews.Tree:
				if (BossKey(news.Subject).Length > 0)
				{
					return("tree/boss");
				}
				return(SeaCreatures.Contains(news.Subject) ? "tree/sea" : "tree/monster");
			case VikingNews.Death:
				return(Pools().ContainsKey("death/" + news.Subject) ? "death/" + news.Subject : AnyDeath);
			default:
				return(news.Kind + "/" + news.Subject);
			}
		}

		/// <summary>
		/// Gets the pool of the sentence put after a line about an event, when it's a boss or a biome beyond the one the
		/// viking is from
		/// </summary>
		/// <param name="news">The event</param>
		/// <param name="vikingBiome">The index of the viking's biome (see VikingGear.Biomes)</param>
		/// <returns>The pool's key, or null when there's nothing to add</returns>
		public static string ImpressedKey(VikingNews.Event news, int vikingBiome)
		{
			if (news.Kind == VikingNews.Boss && BossBiomes.TryGetValue(news.Subject, out int bossBiome) && bossBiome > vikingBiome)
			{
				return(ImpressedBoss);
			}
			if (news.Kind == VikingNews.Biome && BiomeIndex(news.Subject) > vikingBiome)
			{
				return(ImpressedBiome);
			}
			return(null);
		}

		/// <summary>
		/// Picks what a viking says about an event - a line of its pool no viking has said about it yet, with a sentence
		/// after it when it's a boss or biome beyond the viking's own - and fills in the player's and creature's names
		/// </summary>
		/// <param name="news">The event</param>
		/// <param name="vikingBiome">The index of the viking's biome (see VikingGear.Biomes)</param>
		/// <param name="player">The name of the player it's said to</param>
		/// <param name="creature">The name of the creature it's about, or ""</param>
		/// <param name="pick">Picks a number from 0 up to, but not including, the one given</param>
		/// <param name="line">Set to the stable hash of the line picked, to note it said</param>
		/// <returns>What the viking says, or null when every line has been said</returns>
		public static string Choose(VikingNews.Event news, int vikingBiome, string player, string creature, Func<int, int> pick, out int line)
		{
			return(Choose(news, vikingBiome, player, creature, pick, out line, false));
		}

		/// <summary>
		/// Picks what a PC or dvergr viking says about an event - a line of its kind's pool no viking has said about
		/// it yet (see PoolKey and DvergrPoolKey), with a sentence of its kind's after it when it's a boss or biome beyond
		/// the viking's own - and fills in the player's and creature's names
		/// </summary>
		/// <param name="news">The event</param>
		/// <param name="vikingBiome">The index of the viking's biome (see VikingGear.Biomes)</param>
		/// <param name="player">The name of the player it's said to</param>
		/// <param name="creature">The name of the creature it's about, or ""</param>
		/// <param name="pick">Picks a number from 0 up to, but not including, the one given</param>
		/// <param name="line">Set to the stable hash of the line picked, to note it said</param>
		/// <param name="dvergr">True for a dvergr viking</param>
		/// <returns>What the viking says, or null when every line has been said</returns>
		public static string Choose(VikingNews.Event news, int vikingBiome, string player, string creature, Func<int, int> pick, out int line, bool dvergr)
		{
			line = 0;
			if (!Pools().TryGetValue((dvergr ? DvergrPoolKey(news) : PoolKey(news)), out string[] pool))
			{
				return(null);
			}
			List<string> unused = new List<string>();
			foreach (string text in pool)
			{
				if (!news.UsedLines.Contains(text.GetStableHashCode()))
				{
					unused.Add(text);
				}
			}
			if (unused.Count == 0)
			{
				return(null);
			}
			string said = unused[pick(unused.Count)];
			line = said.GetStableHashCode();
			string impressed = ImpressedKey(news, vikingBiome);
			if (impressed != null && dvergr)
			{
				impressed = DvergrPrefix + impressed;
			}
			if (impressed != null && Pools().TryGetValue(impressed, out string[] after) && after.Length > 0)
			{
				said += " " + after[pick(after.Length)];
			}
			return(Fill(said, player, creature));
		}

		/// <summary>
		/// Fills in the player's and creature's names in a line
		/// </summary>
		/// <param name="line">The line</param>
		/// <param name="player">The player's name</param>
		/// <param name="creature">The creature's name, or ""</param>
		/// <returns>The line as said</returns>
		public static string Fill(string line, string player, string creature)
		{
			return(line.Replace(PlayerToken, player ?? "").Replace(CreatureToken, creature ?? ""));
		}
	}
}
