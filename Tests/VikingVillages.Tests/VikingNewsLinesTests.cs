using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Kriona.Vikings;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the lines vikings tell news with - that every event has its pool and each pool enough lines that fill in
	/// cleanly - and how what happened is sorted into pools, how a line is picked and when a viking is impressed
	/// </summary>
	public class VikingNewsLinesTests
	{
		/// <summary>
		/// The pools whose lines name a creature, each of which has to
		/// </summary>
		private static readonly HashSet<string> CreaturePools = new HashSet<string>
		{
			"tame/*",
			"tree/monster",
			"tree/sea",
			"tree/boss",
			"death/creature",
			"death/player",
			"dvergr/boss",
			"dvergr/miniboss",
			"dvergr/trader/BogWitch",
			"dvergr/tame",
			"dvergr/tree",
			"dvergr/serpent",
			"dvergr/death/creature",
			"dvergr/death/boss",
			"dvergr/death/player"
		};

		/// <summary>
		/// The kinds of hit a player's death has no pool of its own for - those with an attacker, which goes by the
		/// attacker, and the edge of the world, which isn't a death to the vikings
		/// </summary>
		private static readonly HashSet<HitData.HitType> HitsWithoutPools = new HashSet<HitData.HitType>
		{
			HitData.HitType.Undefined,
			HitData.HitType.EnemyHit,
			HitData.HitType.PlayerHit,
			HitData.HitType.EdgeOfWorld
		};

		/// <summary>
		/// Lists the pools every event that can happen needs
		/// </summary>
		/// <returns>Their keys</returns>
		private static List<string> NeededPools()
		{
			List<string> needed = new List<string> { "allskills", "leviathan", "edge", "tame/*", "tree/monster", "tree/sea", "tree/boss", "death/any", "death/creature", "death/player", VikingNewsLines.ImpressedBoss, VikingNewsLines.ImpressedBiome };
			foreach (string boss in VikingNewsLines.BossBiomes.Keys)
			{
				needed.Add("boss/" + boss);
				needed.Add("death/boss/" + boss);
			}
			foreach (string miniboss in VikingNewsLines.Minibosses.Values)
			{
				needed.Add("miniboss/" + miniboss);
				needed.Add("death/miniboss/" + miniboss);
			}
			foreach (string trader in new[] { "Haldor", "Hildir", "BogWitch" })
			{
				needed.Add("trader/" + trader);
			}
			foreach (string dungeon in new[] { "BurialChambers", "TrollCave", "FrostCave" })
			{
				needed.Add("dungeon/" + dungeon);
			}
			for (int i = 1; i < VikingGear.Biomes.Length; i++)
			{
				needed.Add("biome/" + VikingGear.Biomes[i].Biome.ToString());
			}
			needed.Add("biome/Ocean");
			foreach (Skills.SkillType skill in Enum.GetValues(typeof(Skills.SkillType)))
			{
				if (skill != Skills.SkillType.None && skill != Skills.SkillType.All)
				{
					needed.Add("skill/" + skill.ToString());
				}
			}
			foreach (string achievement in VikingNewsLines.Achievements)
			{
				needed.Add("achievement/" + achievement);
			}
			foreach (string group in VikingNewsLines.DeathGroups.Values)
			{
				needed.Add("death/" + group);
			}
			foreach (string serpent in VikingNewsLines.Serpents)
			{
				needed.Add("serpent/" + serpent);
			}
			foreach (HitData.HitType hit in Enum.GetValues(typeof(HitData.HitType)))
			{
				if (!HitsWithoutPools.Contains(hit))
				{
					needed.Add("death/hit/" + hit.ToString());
				}
			}
			return(needed);
		}

		/// <summary>
		/// Checks that every event that can happen has a pool of lines
		/// </summary>
		[Fact]
		public void EveryEventHasItsPool()
		{
			Dictionary<string, string[]> pools = VikingNewsLines.Pools();
			foreach (string key in NeededPools())
			{
				Assert.True(pools.ContainsKey(key), "Missing pool " + key);
			}
		}

		/// <summary>
		/// Checks that every pool has at least 4 different lines, each filling in only the tokens it may - a creature's
		/// name in every line of the pools that name one and in no other - and written in plain text, with no dash but
		/// the hyphen and no ellipsis character
		/// </summary>
		[Fact]
		public void PoolsHaveCleanLines()
		{
			Regex token = new Regex("\\{[^}]*\\}");
			foreach (KeyValuePair<string, string[]> pool in VikingNewsLines.Pools())
			{
				Assert.True(pool.Value.Length >= 4, pool.Key + " has fewer than 4 lines");
				Assert.Equal(pool.Value.Length, new HashSet<string>(pool.Value).Count);
				bool namesCreature = (CreaturePools.Contains(pool.Key) || IsSharedDeath(pool.Key));
				foreach (string line in pool.Value)
				{
					foreach (Match match in token.Matches(line))
					{
						Assert.True(match.Value == VikingNewsLines.PlayerToken || (namesCreature && match.Value == VikingNewsLines.CreatureToken), pool.Key + " fills in " + match.Value + ": " + line);
					}
					if (CreaturePools.Contains(pool.Key))
					{
						Assert.Contains(VikingNewsLines.CreatureToken, line);
					}
					foreach (char c in line)
					{
						Assert.True(c < 128, pool.Key + " has a character that isn't plain text: " + line);
					}
				}
			}
		}

		/// <summary>
		/// Checks whether a death pool is shared by creatures of a kind, which name the creature
		/// </summary>
		/// <param name="key">The pool's key</param>
		/// <returns>True when two or more creatures share it</returns>
		private static bool IsSharedDeath(string key)
		{
			int sharing = 0;
			foreach (string group in VikingNewsLines.DeathGroups.Values)
			{
				if ("death/" + group == key)
				{
					sharing++;
				}
			}
			return(sharing > 1);
		}

		/// <summary>
		/// Checks how bosses, minibosses, traders and dungeons are told apart by their prefabs
		/// </summary>
		[Fact]
		public void SortsPrefabs()
		{
			Assert.Equal("FrozenKing", VikingNewsLines.BossKey("FrozenKing_p3"));
			Assert.Equal("Eikthyr", VikingNewsLines.BossKey("Eikthyr"));
			Assert.Equal("", VikingNewsLines.BossKey("Troll"));
			Assert.Equal("Brenna", VikingNewsLines.MinibossKey("Skeleton_Hildir_nochest"));
			Assert.Equal("ZilThungr", VikingNewsLines.MinibossKey("GoblinShaman_Hildir"));
			Assert.Equal("", VikingNewsLines.MinibossKey("GoblinShaman"));
			Assert.Equal("BogWitch", VikingNewsLines.TraderKey("BogWitch"));
			Assert.Equal("Haldor", VikingNewsLines.TraderKey("Haldor"));
			Assert.Equal("BurialChambers", VikingNewsLines.DungeonKey("Crypt3"));
			Assert.Equal("BurialChambers", VikingNewsLines.DungeonKey("Hildir_crypt"));
			Assert.Equal("", VikingNewsLines.DungeonKey("SunkenCrypt4"));
			Assert.Equal("TrollCave", VikingNewsLines.DungeonKey("TrollCave02"));
			Assert.Equal("FrostCave", VikingNewsLines.DungeonKey("MountainCave02"));
			Assert.Equal("boss/Dragon", VikingNewsLines.DeathSubject("Dragon"));
			Assert.Equal("miniboss/Geirrhafa", VikingNewsLines.DeathSubject("Fenring_Cultist_Hildir"));
			Assert.Equal("greydwarf", VikingNewsLines.DeathSubject("Greydwarf_Shaman"));
			Assert.Equal("creature", VikingNewsLines.DeathSubject("SomeModdedBeast"));
		}

		/// <summary>
		/// Makes an event
		/// </summary>
		/// <param name="kind">Its kind</param>
		/// <param name="subject">What it was about</param>
		/// <returns>The event</returns>
		private static VikingNews.Event Event(string kind, string subject)
		{
			VikingNews.Event news = new VikingNews.Event();
			news.Kind = kind;
			news.Subject = subject;
			news.Remaining = 4;
			return(news);
		}

		/// <summary>
		/// Checks the pools events go to - a tree's by what it killed, and a tame's or death's own pool or the one for any
		/// </summary>
		[Fact]
		public void PicksPools()
		{
			Assert.Equal("tree/boss", VikingNewsLines.PoolKey(Event(VikingNews.Tree, "gd_king")));
			Assert.Equal("tree/sea", VikingNewsLines.PoolKey(Event(VikingNews.Tree, "Serpent")));
			Assert.Equal("tree/monster", VikingNewsLines.PoolKey(Event(VikingNews.Tree, "Troll")));
			Assert.Equal("tame/Wolf", VikingNewsLines.PoolKey(Event(VikingNews.Tame, "Wolf")));
			Assert.Equal(VikingNewsLines.AnyTame, VikingNewsLines.PoolKey(Event(VikingNews.Tame, "Hen")));
			Assert.Equal("death/troll", VikingNewsLines.PoolKey(Event(VikingNews.Death, "troll")));
			Assert.Equal(VikingNewsLines.AnyDeath, VikingNewsLines.PoolKey(Event(VikingNews.Death, "hit/Nothing")));
			Assert.Equal("edge", VikingNewsLines.PoolKey(Event(VikingNews.Edge, "")));
			Assert.Equal("biome/Plains", VikingNewsLines.PoolKey(Event(VikingNews.Biome, "Plains")));
		}

		/// <summary>
		/// Checks that a viking is impressed by a boss or biome beyond its own, and not by one of its own or before, or by
		/// the ocean
		/// </summary>
		[Fact]
		public void ImpressedBeyondItsOwnBiome()
		{
			int mountains = VikingGear.IndexOf(Heightmap.Biome.Mountain);
			Assert.Equal(VikingNewsLines.ImpressedBoss, VikingNewsLines.ImpressedKey(Event(VikingNews.Boss, "GoblinKing"), mountains));
			Assert.Null(VikingNewsLines.ImpressedKey(Event(VikingNews.Boss, "Dragon"), mountains));
			Assert.Equal(VikingNewsLines.ImpressedBiome, VikingNewsLines.ImpressedKey(Event(VikingNews.Biome, "Plains"), mountains));
			Assert.Null(VikingNewsLines.ImpressedKey(Event(VikingNews.Biome, "Swamp"), mountains));
			Assert.Null(VikingNewsLines.ImpressedKey(Event(VikingNews.Biome, "Ocean"), 0));
			Assert.Null(VikingNewsLines.ImpressedKey(Event(VikingNews.Skill, "Swords"), 0));
		}

		/// <summary>
		/// Checks that a line no viking said yet is picked, with the names filled in and an impressed sentence after it
		/// when the boss is beyond the viking's biome, and none once every line is said
		/// </summary>
		[Fact]
		public void ChoosesUnsaidLines()
		{
			VikingNews.Event news = Event(VikingNews.Boss, "Fader");
			string[] pool = VikingNewsLines.Pools()["boss/Fader"];
			for (int i = 1; i < pool.Length; i++)
			{
				news.UsedLines.Add(pool[i].GetStableHashCode());
			}
			string said = VikingNewsLines.Choose(news, 0, "Astrid", "", (int count) => 0, out int line);
			Assert.Equal(pool[0].GetStableHashCode(), line);
			Assert.StartsWith(VikingNewsLines.Fill(pool[0], "Astrid", ""), said);
			Assert.True(said.Length > VikingNewsLines.Fill(pool[0], "Astrid", "").Length);
			Assert.DoesNotContain(VikingNewsLines.PlayerToken, said);
			news.UsedLines.Add(line);
			Assert.Null(VikingNewsLines.Choose(news, 0, "Astrid", "", (int count) => 0, out _));
		}

		/// <summary>
		/// Checks that a line names the player and the creature
		/// </summary>
		[Fact]
		public void FillsNames()
		{
			Assert.Equal("Astrid dropped a tree on a Troll", VikingNewsLines.Fill("{player} dropped a tree on a {creature}", "Astrid", "Troll"));
		}
	}
}
