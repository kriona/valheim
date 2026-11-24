using UnityEngine;
using VikingVillages;

namespace Kriona.Vikings
{
	/// <summary>
	/// What only a dvergr viking's core does - its prefab, how it's rolled up and the loot it drops
	/// </summary>
	/// <remarks>
	/// A dvergr viking is a living viking the dvergr have hired, with a living viking's body and gear but no village -
	/// it keeps to the dvergr's outposts and patrols, and fights, takes hits and drops loot the way a zombie viking of
	/// the world does
	/// </remarks>
	internal partial class VikingCore
	{
		/// <summary>
		/// The dvergr's own prefab, whose wandering a dvergr viking copies
		/// </summary>
		private const string DvergrSourcePrefab = "Dverger";

		/// <summary>
		/// The dvergr viking's prefab, once it's built
		/// </summary>
		private static GameObject dvergrPrefab;

		/// <summary>
		/// Adds the dvergr viking's prefab to a world's prefabs, building it the first time
		/// </summary>
		/// <param name="scene">The world's ZNetScene, as it wakes</param>
		public static void RegisterDvergr(ZNetScene scene)
		{
			if (dvergrPrefab == null)
			{
				dvergrPrefab = BuildPrefab<DvergrVikingBody, DvergrVikingAI>(scene, DvergrPrefabName, Character.Faction.Dverger, (GameObject viking, MonsterAI ai) => FinishDvergr(scene, viking, ai));
			}
			if (dvergrPrefab == null)
			{
				VikingMod.Log.LogWarning("Couldn't build the dvergr vikings - the Player or Draugr prefab is missing");
				return;
			}
			AddToScene(scene, dvergrPrefab);
		}

		/// <summary>
		/// Sets up what only a dvergr viking's prefab has - provoked like the dvergr, by a player hurting it or a dvergr
		/// near it, quiet, wandering around where it stands as far and as often as the dvergr do, and waving and moving
		/// its mouth as it talks the way a living viking does (see VikingPoses)
		/// </summary>
		/// <param name="scene">The world's ZNetScene</param>
		/// <param name="viking">The prefab</param>
		/// <param name="ai">Its AI</param>
		private static void FinishDvergr(ZNetScene scene, GameObject viking, MonsterAI ai)
		{
			VikingPoses.AddTo(viking);
			ai.m_aggravatable = true;
			ai.m_idleSound = new EffectList();
			ai.m_alertedEffects = new EffectList();
			GameObject source = scene.GetPrefab(DvergrSourcePrefab);
			MonsterAI dvergrAI = ((source != null) ? source.GetComponent<MonsterAI>() : null);
			if (dvergrAI != null)
			{
				ai.m_randomMoveRange = dvergrAI.m_randomMoveRange;
				ai.m_randomMoveInterval = dvergrAI.m_randomMoveInterval;
			}
		}

		/// <summary>
		/// Rolls a new dvergr viking's look, health, skill, power and gear for the Mistlands, where the dvergr hire them -
		/// its gear all of the Mistlands and fully repaired like a living viking's, with a dverger circlet for light and
		/// a wisp light against the mist - and names it
		/// </summary>
		private void GenerateDvergr()
		{
			int index = VikingGear.IndexOf(Heightmap.Biome.Mistlands);
			VikingData rolled = VikingGear.RollData(index);
			Arms arms = VikingGear.LivingArms(index, false, Random.value);
			Inventory gear = VikingGear.RollLoadout(index, humanoid.GetLevel(), rolled, true, arms, true, true);
			gear.AddItem((VikingGear.LivingCirclet(index, Random.value) ? VikingGear.CircletPrefab : TorchPrefab), 1, 1, 0, 0L, "", false);
			VikingGear.AddWisp(gear);
			ZDO zdo = SaveGenerated(rolled, gear);
			VikingNames.GiveName(zdo, rolled, false);
		}

		/// <summary>
		/// Drops a dvergr viking's coins and remains, more for each star, and each piece of its gear by the config's
		/// DvergrGearDropChance, up to its DvergrGearDropMax, as a zombie viking of the world's drop
		/// </summary>
		partial void DropLoot()
		{
			if (IsDvergrViking(humanoid))
			{
				DropWorldLoot(Plugin.DvergrGearDropChance.Value, Plugin.DvergrGearDropMax.Value);
			}
		}
	}
}
