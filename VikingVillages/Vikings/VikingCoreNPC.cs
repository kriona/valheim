using System.Collections.Generic;
using UnityEngine;
using VikingVillages;

namespace Kriona.Vikings
{
	/// <summary>
	/// What only an NPC viking's core does - its prefab, and how it's rolled up: a peaceful villager's clothes, knife
	/// and skills in place of a PC viking's armor and weapons
	/// </summary>
	internal partial class VikingCore
	{
		/// <summary>
		/// The NPC viking's prefab, once it's built
		/// </summary>
		private static GameObject npcPrefab;

		/// <summary>
		/// Adds the NPC viking's prefab to a world's prefabs, building it the first time - set up the same way as a PC
		/// viking's (see FinishPC)
		/// </summary>
		/// <param name="scene">The world's ZNetScene, as it wakes</param>
		public static void RegisterNPC(ZNetScene scene)
		{
			if (npcPrefab == null)
			{
				npcPrefab = BuildPrefab<NPCVikingBody, NPCVikingAI>(scene, NPCPrefabName, Character.Faction.Dverger, FinishPC);
			}
			if (npcPrefab == null)
			{
				VikingMod.Log.LogWarning("Couldn't build the NPC vikings - the Player or Draugr prefab is missing");
				return;
			}
			AddToScene(scene, npcPrefab);
		}

		/// <summary>
		/// Rolls a new NPC viking's look, health and skills for its village's biome the way a PC viking's are (see
		/// Generate) - but with none of the skills it would fight with, a little in its fists and knife and more in
		/// Farming, Cooking, Crafting and Fishing (see NPCVikings.Skill) - dressed in its biome's clothes and cape (see NPCVikings.Outfit),
		/// with its biome's knife (see NPCVikings.KnifeFor) and no other weapon or shield, and with a torch or dverger circlet, a wisp light in the Mistlands, a strength belt and
		/// pockets by the same odds as a PC viking's, fed and carrying food like a PC viking (see VikingStartFood), and
		/// names it, with the looks its name comes with
		/// </summary>
		private void GenerateNPC()
		{
			int index = VillageCooking.BiomeAt(VillageFarm());
			VikingData rolled = VikingGear.RollData(index);
			int model = VikingNames.PresetModel(nview.GetZDO());
			if (model >= 0 && model != rolled.Model)
			{
				VikingGear.SetModel(rolled, model);
			}
			float unarmed = Random.Range(NPCVikings.FightSkillMin, NPCVikings.FightSkillMax);
			float knives = Random.Range(NPCVikings.FightSkillMin, NPCVikings.FightSkillMax);
			foreach (int skill in new List<int>(rolled.Skills.Keys))
			{
				rolled.Skills[skill] = NPCVikings.Skill((Skills.SkillType)skill, rolled.Skills[skill], unarmed, knives);
			}
			rolled.Height = VikingUpgrades.PCRows(index, Random.value);
			Inventory gear = new Inventory(ZombiePrefabName, null, rolled.Width, rolled.Height);
			foreach (string piece in NPCVikings.Outfit(index, Random.value, Random.value, Random.value, Random.value))
			{
				VikingGear.AddWorn(gear, piece);
			}
			// Its knife of its biome, for slaughtering livestock and for a fight it can't get away from
			ItemDrop.ItemData knife = VikingGear.MakeItem(NPCVikings.KnifeFor(index), 1, 1);
			if (knife != null && gear.AddItem(knife))
			{
				rolled.WeaponSlot = knife.m_gridPos;
			}
			gear.AddItem((VikingGear.PCCirclet(index, Random.value) ? VikingGear.CircletPrefab : TorchPrefab), 1, 1, 0, 0L, "", false);
			if (VikingGear.PCWisp(index))
			{
				VikingGear.AddWisp(gear);
			}
			if (VikingUpgrades.PCBelt(index, Random.value))
			{
				ItemDrop.ItemData belt = gear.AddItem(VikingUpgrades.BeltPrefab, 1, 1, 0, 0L, "", false);
				if (belt != null && !VikingGear.PCWisp(index))
				{
					belt.m_equipped = true;
				}
			}
			ZDO zdo = nview.GetZDO();
			VikingStartFood.Give(zdo, gear, index);
			VikingNames.GiveName(zdo, rolled, false);
			StyleAfterName(zdo, rolled);
			SaveGenerated(rolled, gear);
			if (TestLog.On)
			{
				Plugin.Log.LogInfo("New NPC viking: " + TestLog.Name(humanoid) + ", rolled up for the " + VikingGear.Biomes[index].Biome);
			}
		}
	}
}
