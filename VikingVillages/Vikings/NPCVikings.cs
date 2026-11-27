using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The rules NPC vikings are rolled by - how many of a village's vikings are NPC vikings, the skills they start with,
	/// and the clothes and cape each biome's wear
	/// </summary>
	/// <remarks>
	/// An NPC viking is a peaceful villager: it lives in its village the way a PC viking does, but never takes up a weapon
	/// other than a knife, or a shield, runs from a fight and shelters through a raid, and does the village's chores first (see
	/// VillageLife.LeavesToNPC)
	/// </remarks>
	internal static class NPCVikings
	{
		/// <summary>
		/// One viking in this many of a village is an NPC viking - its 5th, 10th, 15th and so on
		/// </summary>
		internal const int NPCEvery = 5;

		/// <summary>
		/// How much higher an NPC viking's skills at a villager's work (see CraftSkills) start than its biome's range,
		/// capped at 100
		/// </summary>
		internal const float CraftBonus = 20f;

		/// <summary>
		/// The skills an NPC viking is better at than its biome's range, by CraftBonus - the work of a villager rather
		/// than a fighter
		/// </summary>
		internal static readonly HashSet<Skills.SkillType> CraftSkills = new HashSet<Skills.SkillType>
		{
			Skills.SkillType.Farming,
			Skills.SkillType.Cooking,
			Skills.SkillType.Crafting,
			Skills.SkillType.Fishing
		};

		/// <summary>
		/// The range an NPC viking's Unarmed and Knives skills each start in, the only skills it has for a fight
		/// </summary>
		internal const float FightSkillMin = 1f;
		internal const float FightSkillMax = 5f;

		/// <summary>
		/// The knife an NPC viking of a biome carries, for slaughtering livestock and for a fight it can't get away from -
		/// its biome's own, or the nearest biome's below it with one (see KnifeFor)
		/// </summary>
		internal static readonly Dictionary<Heightmap.Biome, string> Knives = new Dictionary<Heightmap.Biome, string>()
		{
			{ Heightmap.Biome.Meadows, "KnifeFlint" },
			{ Heightmap.Biome.BlackForest, "KnifeCopper" },
			{ Heightmap.Biome.Mountain, "KnifeSilver" },
			{ Heightmap.Biome.Plains, "KnifeBlackMetal" },
			{ Heightmap.Biome.DeepNorth, "KnifeGold" }
		};

		/// <summary>
		/// The share of NPC vikings in a harvest outfit, or a dress or tunic, who wear a hat as well
		/// </summary>
		internal const float HatChance = 0.5f;

		/// <summary>
		/// The straw hat a harvest outfit always comes with
		/// </summary>
		internal const string StrawHat = "HelmetStrawHat";

		/// <summary>
		/// The harvest outfits, the tunic and the dress
		/// </summary>
		internal const string HarvestTunic = "ArmorHarvester1";
		internal const string HarvestDress = "ArmorHarvester2";

		/// <summary>
		/// The trousers an NPC viking in a tunic wears, one picked at random
		/// </summary>
		internal static readonly string[] Trousers = { "ArmorRagsLegs", "ArmorLeatherLegs" };

		/// <summary>
		/// What NPC vikings of a biome wear - a dress, tunic or harvest outfit for the chest, and the hats they may wear
		/// with one that isn't a harvest outfit
		/// </summary>
		internal class Outfits
		{
			public Heightmap.Biome Biome;
			public string[] Chests;
			public string[] Hats;
		}

		/// <summary>
		/// The biomes with clothes of their own, in the order they're reached - a biome without any wears the nearest
		/// biome's below it (see OutfitsFor)
		/// </summary>
		internal static readonly Outfits[] Wardrobe =
		{
			new Outfits
			{
				Biome = Heightmap.Biome.Meadows,
				Chests = new[] { "ArmorDress10", "ArmorTunic10" },
				Hats = new[] { "HelmetHat5", "HelmetHat10", "HelmetSweatBand" }
			},
			new Outfits
			{
				Biome = Heightmap.Biome.BlackForest,
				Chests = new[] { "ArmorDress1", "ArmorDress4", "ArmorDress7", "ArmorTunic1", "ArmorTunic4", "ArmorTunic7", HarvestTunic, HarvestDress },
				Hats = new[] { "HelmetHat1", "HelmetHat3" }
			},
			new Outfits
			{
				Biome = Heightmap.Biome.Mountain,
				Chests = new[] { "ArmorDress2", "ArmorDress5", "ArmorDress8", "ArmorTunic2", "ArmorTunic5", "ArmorTunic8", HarvestTunic, HarvestDress },
				Hats = new[] { "HelmetHat2", "HelmetHat4", "HelmetHat6" }
			},
			new Outfits
			{
				Biome = Heightmap.Biome.Plains,
				Chests = new[] { "ArmorDress3", "ArmorDress6", "ArmorDress9", "ArmorTunic3", "ArmorTunic6", "ArmorTunic9", HarvestTunic, HarvestDress },
				Hats = new[] { "HelmetHat7", "HelmetHat8", "HelmetHat9" }
			}
		};

		/// <summary>
		/// The cape an NPC viking of a biome wears, by biome - one that keeps the frost off where the weather freezes, a
		/// linen cape in the Plains and Ashlands and a feather cape in the Mistlands. A biome not listed has none
		/// </summary>
		internal static readonly Dictionary<Heightmap.Biome, string> Capes = new Dictionary<Heightmap.Biome, string>()
		{
			{ Heightmap.Biome.Mountain, "CapeWolf" },
			{ Heightmap.Biome.Plains, "CapeLinen" },
			{ Heightmap.Biome.Mistlands, "CapeFeather" },
			{ Heightmap.Biome.AshLands, "CapeLinen" },
			{ Heightmap.Biome.DeepNorth, "CapeDeepNorth" }
		};

		/// <summary>
		/// The skills a viking fights with, which an NPC viking starts with none of - every weapon's but the knife's,
		/// blocking, both kinds of magic and dodging. Its Unarmed and Knives skills are the ones it has a little of (see
		/// FightSkillMin)
		/// </summary>
		internal static readonly HashSet<Skills.SkillType> CombatSkills = new HashSet<Skills.SkillType>
		{
			Skills.SkillType.Swords,
			Skills.SkillType.Clubs,
			Skills.SkillType.Polearms,
			Skills.SkillType.Spears,
			Skills.SkillType.Blocking,
			Skills.SkillType.Axes,
			Skills.SkillType.Bows,
			Skills.SkillType.Crossbows,
			Skills.SkillType.ElementalMagic,
			Skills.SkillType.BloodMagic,
			Skills.SkillType.Dodge
		};

		/// <summary>
		/// Checks whether the next viking to come to a village is an NPC viking, keeping one in every NPCEvery of its
		/// vikings an NPC viking - so a village rolled up from nothing has its 5th, 10th and so on, and one that loses or
		/// gains a viking brings in whichever kind keeps it nearest that
		/// </summary>
		/// <param name="vikings">How many vikings the village has now</param>
		/// <param name="npcs">How many of them are NPC vikings</param>
		/// <returns>True when the next is an NPC viking</returns>
		internal static bool NextIsNPC(int vikings, int npcs)
		{
			return(npcs < (vikings + 1) / NPCEvery);
		}

		/// <summary>
		/// Works out an NPC viking's level in a skill from the level its biome rolled for it - none in the skills it would
		/// fight with, a little in its fists and knife, CraftBonus more in Farming, Cooking, Crafting and Fishing (see
		/// CraftSkills), and the level rolled for the rest
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <param name="rolled">The level its biome rolled for it</param>
		/// <param name="unarmed">Its Unarmed level, rolled between FightSkillMin and FightSkillMax</param>
		/// <param name="knives">Its Knives level, rolled the same way</param>
		/// <returns>Its level, from 0 to 100</returns>
		internal static float Skill(Skills.SkillType skill, float rolled, float unarmed, float knives)
		{
			if (skill == Skills.SkillType.Unarmed)
			{
				return(unarmed);
			}
			if (skill == Skills.SkillType.Knives)
			{
				return(knives);
			}
			if (CombatSkills.Contains(skill))
			{
				return(0f);
			}
			if (CraftSkills.Contains(skill))
			{
				return(Mathf.Min(100f, rolled + CraftBonus));
			}
			return(rolled);
		}

		/// <summary>
		/// Finds the clothes NPC vikings of a biome wear - the biome's own, or the nearest biome's below it with some, so
		/// the Swamp wears the Black Forest's and every biome from the Mistlands on the Plains'
		/// </summary>
		/// <param name="index">The index of the biome in VikingGear's tables</param>
		/// <returns>The clothes</returns>
		internal static Outfits OutfitsFor(int index)
		{
			Outfits found = Wardrobe[0];
			foreach (Outfits outfits in Wardrobe)
			{
				if (VikingGear.IndexOf(outfits.Biome) <= index)
				{
					found = outfits;
				}
			}
			return(found);
		}

		/// <summary>
		/// Checks whether a chest piece is a tunic, which an NPC viking wears with trousers - a dress, the harvest dress
		/// included, needs none
		/// </summary>
		/// <param name="chest">The chest piece's prefab name</param>
		/// <returns>True when it is</returns>
		internal static bool IsTunic(string chest)
		{
			return(chest.StartsWith("ArmorTunic") || chest == HarvestTunic);
		}

		/// <summary>
		/// Picks one of a list by a roll, a list's first for a roll of 0 and its last for a roll just under 1
		/// </summary>
		/// <param name="list">The list, not empty</param>
		/// <param name="roll">The roll, from 0 to 1</param>
		/// <returns>The one picked</returns>
		private static string PickBy(string[] list, float roll)
		{
			return(list[Mathf.Clamp(Mathf.FloorToInt(roll * list.Length), 0, list.Length - 1)]);
		}

		/// <summary>
		/// Picks an NPC viking's clothes for its biome (see OutfitsFor) - a dress, tunic or harvest outfit, with rag or
		/// leather trousers under a tunic, the straw hat with a harvest outfit, and otherwise one of the biome's hats
		/// HatChance of the time - and the cape its biome's NPC vikings wear (see Capes)
		/// </summary>
		/// <param name="index">The index of its biome in VikingGear's tables</param>
		/// <param name="chestRoll">The roll that picks its chest piece, from 0 to 1</param>
		/// <param name="hatRoll">The roll for whether it wears a hat, from 0 to 1</param>
		/// <param name="hatPick">The roll that picks its hat, from 0 to 1</param>
		/// <param name="trousersPick">The roll that picks its trousers, from 0 to 1</param>
		/// <returns>The prefab names of what it wears</returns>
		internal static List<string> Outfit(int index, float chestRoll, float hatRoll, float hatPick, float trousersPick)
		{
			Outfits outfits = OutfitsFor(index);
			List<string> worn = new List<string>();
			string chest = PickBy(outfits.Chests, chestRoll);
			worn.Add(chest);
			if (IsTunic(chest))
			{
				worn.Add(PickBy(Trousers, trousersPick));
			}
			if (chest == HarvestTunic || chest == HarvestDress)
			{
				worn.Add(StrawHat);
			}
			else if (hatRoll < HatChance)
			{
				worn.Add(PickBy(outfits.Hats, hatPick));
			}
			string cape = CapeFor(index);
			if (cape != null)
			{
				worn.Add(cape);
			}
			return(worn);
		}

		/// <summary>
		/// Finds the knife an NPC viking of a biome carries - its biome's own, or the nearest biome's below it with one,
		/// so the Swamp's carry the Black Forest's and the Mistlands' and Ashlands' the Plains'
		/// </summary>
		/// <param name="index">The index of its biome in VikingGear's tables</param>
		/// <returns>The knife's prefab name</returns>
		internal static string KnifeFor(int index)
		{
			for (int i = Mathf.Clamp(index, 0, VikingGear.Biomes.Length - 1); i >= 0; i--)
			{
				if (Knives.TryGetValue(VikingGear.Get(i).Biome, out string knife))
				{
					return(knife);
				}
			}
			return(Knives[Heightmap.Biome.Meadows]);
		}

		/// <summary>
		/// Finds the cape an NPC viking of a biome wears (see Capes)
		/// </summary>
		/// <param name="index">The index of its biome in VikingGear's tables</param>
		/// <returns>The cape's prefab name, or null when its biome's wear none</returns>
		internal static string CapeFor(int index)
		{
			return(Capes.TryGetValue(VikingGear.Get(index).Biome, out string cape) ? cape : null);
		}
	}
}
