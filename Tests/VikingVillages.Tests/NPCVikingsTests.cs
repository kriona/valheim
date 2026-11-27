using System.Collections.Generic;
using Kriona.Vikings;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the rules NPC vikings are rolled by - which of a village's vikings are NPC vikings, their skills, and the
	/// clothes and capes each biome's wear
	/// </summary>
	public class NPCVikingsTests
	{
		/// <summary>
		/// Checks that a village rolled up from nothing has its 5th, 10th and 15th vikings as NPC vikings and the rest as
		/// PC vikings
		/// </summary>
		[Fact]
		public void MakesEveryFifthVikingAnNPC()
		{
			int npcs = 0;
			List<int> places = new List<int>();
			for (int vikings = 0; vikings < 15; vikings++)
			{
				if (NPCVikings.NextIsNPC(vikings, npcs))
				{
					npcs++;
					places.Add(vikings + 1);
				}
			}
			Assert.Equal(new List<int> { 5, 10, 15 }, places);
		}

		/// <summary>
		/// Checks that a village that lost or gained a viking brings in whichever kind keeps one in every five an NPC
		/// viking
		/// </summary>
		/// <param name="vikings">How many vikings it has</param>
		/// <param name="npcs">How many of them are NPC vikings</param>
		/// <param name="expected">True when the next is an NPC viking</param>
		[Theory]
		[InlineData(0, 0, false)]
		[InlineData(3, 0, false)]
		[InlineData(4, 0, true)]
		[InlineData(4, 1, false)]
		[InlineData(5, 0, true)]
		[InlineData(5, 1, false)]
		[InlineData(8, 1, false)]
		[InlineData(9, 1, true)]
		[InlineData(9, 2, false)]
		[InlineData(12, 0, true)]
		public void KeepsOneInFiveAnNPC(int vikings, int npcs, bool expected)
		{
			Assert.Equal(expected, NPCVikings.NextIsNPC(vikings, npcs));
		}

		/// <summary>
		/// Checks that an NPC viking has none of the skills it would fight with, its rolled Unarmed and Knives levels,
		/// more Farming, Cooking, Crafting and Fishing capped at 100, and what its biome rolled for the rest
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <param name="rolled">What its biome rolled</param>
		/// <param name="expected">Its level</param>
		[Theory]
		[InlineData(Skills.SkillType.Knives, 40f, 4f)]
		[InlineData(Skills.SkillType.Swords, 40f, 0f)]
		[InlineData(Skills.SkillType.Bows, 40f, 0f)]
		[InlineData(Skills.SkillType.Blocking, 40f, 0f)]
		[InlineData(Skills.SkillType.Axes, 40f, 0f)]
		[InlineData(Skills.SkillType.Dodge, 40f, 0f)]
		[InlineData(Skills.SkillType.Crossbows, 40f, 0f)]
		[InlineData(Skills.SkillType.ElementalMagic, 40f, 0f)]
		[InlineData(Skills.SkillType.Unarmed, 40f, 3f)]
		[InlineData(Skills.SkillType.Farming, 40f, 60f)]
		[InlineData(Skills.SkillType.Cooking, 10f, 30f)]
		[InlineData(Skills.SkillType.Cooking, 95f, 100f)]
		[InlineData(Skills.SkillType.WoodCutting, 40f, 40f)]
		[InlineData(Skills.SkillType.Run, 12f, 12f)]
		[InlineData(Skills.SkillType.Crafting, 55f, 75f)]
		[InlineData(Skills.SkillType.Fishing, 30f, 50f)]
		[InlineData(Skills.SkillType.Fishing, 90f, 100f)]
		[InlineData(Skills.SkillType.Sneak, 30f, 30f)]
		public void RollsPeacefulSkills(Skills.SkillType skill, float rolled, float expected)
		{
			Assert.Equal(expected, NPCVikings.Skill(skill, rolled, 3f, 4f), 3);
		}

		/// <summary>
		/// Checks each biome's knife - its own, or the nearest biome's below it with one
		/// </summary>
		/// <param name="biome">The biome</param>
		/// <param name="expected">Its knife</param>
		[Theory]
		[InlineData(Heightmap.Biome.Meadows, "KnifeFlint")]
		[InlineData(Heightmap.Biome.BlackForest, "KnifeCopper")]
		[InlineData(Heightmap.Biome.Swamp, "KnifeCopper")]
		[InlineData(Heightmap.Biome.Mountain, "KnifeSilver")]
		[InlineData(Heightmap.Biome.Plains, "KnifeBlackMetal")]
		[InlineData(Heightmap.Biome.Mistlands, "KnifeBlackMetal")]
		[InlineData(Heightmap.Biome.AshLands, "KnifeBlackMetal")]
		[InlineData(Heightmap.Biome.DeepNorth, "KnifeGold")]
		public void CarriesTheNearestBiomesKnife(Heightmap.Biome biome, string expected)
		{
			Assert.Equal(expected, NPCVikings.KnifeFor(VikingGear.IndexOf(biome)));
		}

		/// <summary>
		/// Checks that each biome wears its own clothes, or the nearest biome's below it with some
		/// </summary>
		/// <param name="biome">The biome</param>
		/// <param name="expected">The biome whose clothes it wears</param>
		[Theory]
		[InlineData(Heightmap.Biome.Meadows, Heightmap.Biome.Meadows)]
		[InlineData(Heightmap.Biome.BlackForest, Heightmap.Biome.BlackForest)]
		[InlineData(Heightmap.Biome.Swamp, Heightmap.Biome.BlackForest)]
		[InlineData(Heightmap.Biome.Mountain, Heightmap.Biome.Mountain)]
		[InlineData(Heightmap.Biome.Plains, Heightmap.Biome.Plains)]
		[InlineData(Heightmap.Biome.Mistlands, Heightmap.Biome.Plains)]
		[InlineData(Heightmap.Biome.AshLands, Heightmap.Biome.Plains)]
		[InlineData(Heightmap.Biome.DeepNorth, Heightmap.Biome.Plains)]
		public void WearsTheNearestBiomesClothes(Heightmap.Biome biome, Heightmap.Biome expected)
		{
			Assert.Equal(expected, NPCVikings.OutfitsFor(VikingGear.IndexOf(biome)).Biome);
		}

		/// <summary>
		/// Checks that the Plains have all three beaded dresses and tunics
		/// </summary>
		[Fact]
		public void PlainsHaveEveryBeadedDress()
		{
			string[] chests = NPCVikings.OutfitsFor(VikingGear.IndexOf(Heightmap.Biome.Plains)).Chests;
			foreach (string beaded in new[] { "ArmorDress3", "ArmorDress6", "ArmorDress9", "ArmorTunic3", "ArmorTunic6", "ArmorTunic9" })
			{
				Assert.Contains(beaded, chests);
			}
		}

		/// <summary>
		/// Checks that tunics and the harvest tunic count as tunics, and dresses and the harvest dress don't
		/// </summary>
		/// <param name="chest">The chest piece</param>
		/// <param name="expected">True when it's a tunic</param>
		[Theory]
		[InlineData("ArmorTunic1", true)]
		[InlineData("ArmorTunic10", true)]
		[InlineData("ArmorHarvester1", true)]
		[InlineData("ArmorHarvester2", false)]
		[InlineData("ArmorDress5", false)]
		public void KnowsTunics(string chest, bool expected)
		{
			Assert.Equal(expected, NPCVikings.IsTunic(chest));
		}

		/// <summary>
		/// Checks that a Meadows NPC viking in a dress wears a hat only when its roll says so, and no cape
		/// </summary>
		[Fact]
		public void DressesAMeadowsViking()
		{
			int meadows = VikingGear.IndexOf(Heightmap.Biome.Meadows);
			Assert.Equal(new List<string> { "ArmorDress10", "HelmetHat5" }, NPCVikings.Outfit(meadows, 0f, 0.1f, 0f, 0f));
			Assert.Equal(new List<string> { "ArmorDress10" }, NPCVikings.Outfit(meadows, 0f, 0.9f, 0f, 0f));
		}

		/// <summary>
		/// Checks that a tunic comes with trousers, picked by their roll
		/// </summary>
		[Fact]
		public void PutsTrousersUnderATunic()
		{
			int meadows = VikingGear.IndexOf(Heightmap.Biome.Meadows);
			Assert.Equal(new List<string> { "ArmorTunic10", "ArmorRagsLegs" }, NPCVikings.Outfit(meadows, 0.99f, 0.9f, 0f, 0f));
			Assert.Equal(new List<string> { "ArmorTunic10", "ArmorLeatherLegs" }, NPCVikings.Outfit(meadows, 0.99f, 0.9f, 0f, 0.99f));
		}

		/// <summary>
		/// Checks that a harvest outfit always comes with the straw hat, the harvest tunic with trousers too, and a
		/// Mountains viking with its wolf fur cape
		/// </summary>
		[Fact]
		public void DressesAHarvester()
		{
			int mountains = VikingGear.IndexOf(Heightmap.Biome.Mountain);
			string[] chests = NPCVikings.OutfitsFor(mountains).Chests;
			float tunicRoll = (System.Array.IndexOf(chests, NPCVikings.HarvestTunic) + 0.5f) / chests.Length;
			float dressRoll = (System.Array.IndexOf(chests, NPCVikings.HarvestDress) + 0.5f) / chests.Length;
			Assert.Equal(new List<string> { NPCVikings.HarvestTunic, "ArmorRagsLegs", NPCVikings.StrawHat, "CapeWolf" }, NPCVikings.Outfit(mountains, tunicRoll, 0.9f, 0f, 0f));
			Assert.Equal(new List<string> { NPCVikings.HarvestDress, NPCVikings.StrawHat, "CapeWolf" }, NPCVikings.Outfit(mountains, dressRoll, 0.9f, 0f, 0f));
		}

		/// <summary>
		/// Checks each biome's cape - frost-resisting where the weather freezes, linen in the Plains and Ashlands, a
		/// feather cape in the Mistlands, and none elsewhere
		/// </summary>
		/// <param name="biome">The biome</param>
		/// <param name="expected">Its cape, or null</param>
		[Theory]
		[InlineData(Heightmap.Biome.Meadows, null)]
		[InlineData(Heightmap.Biome.BlackForest, null)]
		[InlineData(Heightmap.Biome.Swamp, null)]
		[InlineData(Heightmap.Biome.Mountain, "CapeWolf")]
		[InlineData(Heightmap.Biome.Plains, "CapeLinen")]
		[InlineData(Heightmap.Biome.Mistlands, "CapeFeather")]
		[InlineData(Heightmap.Biome.AshLands, "CapeLinen")]
		[InlineData(Heightmap.Biome.DeepNorth, "CapeDeepNorth")]
		public void GivesEachBiomesCape(Heightmap.Biome biome, string expected)
		{
			Assert.Equal(expected, NPCVikings.CapeFor(VikingGear.IndexOf(biome)));
		}

		/// <summary>
		/// Checks that every biome's chest pieces and hats can be picked by a roll from 0 to just under 1
		/// </summary>
		[Fact]
		public void PicksEveryPieceByItsRoll()
		{
			foreach (NPCVikings.Outfits outfits in NPCVikings.Wardrobe)
			{
				int index = VikingGear.IndexOf(outfits.Biome);
				HashSet<string> seen = new HashSet<string>();
				for (int i = 0; i < outfits.Chests.Length; i++)
				{
					seen.Add(NPCVikings.Outfit(index, (i + 0.5f) / outfits.Chests.Length, 0.9f, 0f, 0f)[0]);
				}
				Assert.Equal(outfits.Chests.Length, seen.Count);
				Assert.Equal(outfits.Chests[outfits.Chests.Length - 1], NPCVikings.Outfit(index, 0.9999f, 0.9f, 0f, 0f)[0]);
			}
		}
	}
}
