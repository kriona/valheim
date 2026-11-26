using UnityEngine;
using VikingVillages;

namespace Kriona.Vikings
{
	/// <summary>
	/// What only a living viking's core does - its prefab and its life in its village, how its gear is rolled, its
	/// skills rising with its rest and showing as it reaches a new level, the health its food gives it, and its paths
	/// around danger
	/// </summary>
	internal partial class VikingCore
	{
		/// <summary>
		/// The torch a living viking carries at night, which it starts with one of unless it starts with a dverger circlet
		/// (see Generate)
		/// </summary>
		private const string TorchPrefab = "Torch";

		/// <summary>
		/// Builds the living viking's prefab, in the dvergr's faction
		/// </summary>
		/// <param name="scene">The world's ZNetScene</param>
		/// <returns>The prefab, or null when the player's or draugr's prefab is missing</returns>
		private static partial GameObject BuildOwnPrefab(ZNetScene scene)
		{
			return(BuildPrefab<LivingVikingBody, LivingVikingAI>(scene, LivingPrefabName, Character.Faction.Dverger, FinishLiving));
		}

		/// <summary>
		/// Sets up what only a living viking's prefab has - provoked like the dvergr, by a player hurting it or another
		/// viking near it, quiet, wary of lava, and posing as it goes about its life (see VikingPoses)
		/// </summary>
		/// <param name="viking">The prefab</param>
		/// <param name="ai">Its AI</param>
		private static void FinishLiving(GameObject viking, MonsterAI ai)
		{
			// A living viking doesn't groan like the draugr its AI comes from
			ai.m_aggravatable = true;
			ai.m_idleSound = new EffectList();
			ai.m_alertedEffects = new EffectList();
			// It keeps off lava (see DangerousGround) - fleeing it, never fleeing onto it, and leaving alone an enemy
			// standing over it
			ai.m_avoidLava = true;
			ai.m_avoidLavaFlee = true;
			ai.m_fleeInLava = true;
			ai.m_skipLavaTargets = true;
			VikingPoses.AddTo(viking);
		}

		/// <summary>
		/// Checks whether a living viking dying on this client will rise as a zombie, which only Zombie Apocalypse decides
		/// </summary>
		/// <param name="dying">The viking</param>
		/// <returns>True when it will</returns>
		public static partial bool Rises(Character dying)
		{
			return(ZombieLink.Rises(dying));
		}

		/// <summary>
		/// Checks whether a living viking wears its armor the way a player does, which it does, so its armor reduces
		/// poison, fire and spirit damage too
		/// </summary>
		/// <returns>True</returns>
		public static partial bool WearsArmorLikePlayer()
		{
			return(true);
		}

		/// <summary>
		/// Gives the living viking its life in its village, or a dvergr viking its life with the dvergr
		/// </summary>
		partial void AddParts()
		{
			if (GetComponent<DvergrVikingBody>() != null)
			{
				gameObject.AddComponent<DvergrLife>();
				return;
			}
			life = gameObject.AddComponent<VillageLife>();
		}

		/// <summary>
		/// Rolls a new living viking's look, health, skill, power and gear for the biome it's in, or the hardest biome
		/// within HardBiomeVikingReach of its village's farm when that's harder (see VillageFarmhouses.HardestBiomeNear),
		/// as a woman or a man to suit a name it was given before it was rolled (see VikingNames.PresetFirst), and names
		/// it, with the looks its name comes with (see StyleAfterName) - or a dvergr viking's for the Mistlands (see
		/// GenerateDvergr)
		/// </summary>
		private partial void Generate()
		{
			if (IsDvergrViking(humanoid))
			{
				GenerateDvergr();
				return;
			}
			int index = VikingGear.IndexOf(WorldGenerator.instance.GetBiome(transform.position));
			index = Mathf.Max(index, VillageFarmhouses.HardestBiomeNear(VillageFarm(), VillageFarmhouses.HardBiomeVikingReach));
			VikingData rolled = VikingGear.RollData(index);
			// One of its village's first vikings named before it's rolled is a woman or a man to suit the name
			int model = VikingNames.PresetModel(nview.GetZDO());
			if (model >= 0 && model != rolled.Model)
			{
				VikingGear.SetModel(rolled, model);
			}
			// A living viking has kept its armor and weapon in repair, all of it from its own biome, and its village's
			// archer has a bow
			Arms arms = VikingGear.LivingArms(index, nview.GetZDO().GetBool(ArcherKey), Random.value);
			Inventory gear = VikingGear.RollLoadout(index, humanoid.GetLevel(), rolled, true, arms, true, true);
			// With Zombie Apocalypse, from the Mountains on, its weapons may be silver-lined and its bow come with silver
			// arrows
			VikingSilver.Arm(gear, index);
			// It starts with a torch of its own for the night, or a dverger circlet, more often the harder its biome
			gear.AddItem((VikingGear.LivingCirclet(index, Random.value) ? VikingGear.CircletPrefab : TorchPrefab), 1, 1, 0, 0L, "", false);
			// A viking of the Mistlands wears a wisp light against the mist
			if (VikingGear.LivingWisp(index))
			{
				VikingGear.AddWisp(gear);
			}
			// It's named for whether it's a woman or a man - model 1 is a woman's - before its looks are saved, since some
			// names come with looks of their own
			ZDO zdo = nview.GetZDO();
			VikingNames.GiveName(zdo, rolled, false);
			StyleAfterName(zdo, rolled);
			SaveGenerated(rolled, gear);
			if (TestLog.On)
			{
				Plugin.Log.LogInfo("New viking: " + TestLog.Name(humanoid) + ", rolled up for the " + VikingGear.Biomes[index].Biome + (zdo.GetBool(ArcherKey) ? " as the village's archer" : ""));
			}
		}

		/// <summary>
		/// Gives a viking being rolled the looks its name comes with - an Elsa (see VikingNames.IsElsa) wears the game's
		/// Long Braid, with the blondest hair a viking is rolled with, and an Anna (see VikingNames.IsAnna) its Twin
		/// Braids, with red hair
		/// </summary>
		/// <param name="zdo">Its ZDO, with the name it was given</param>
		/// <param name="rolled">Its data, not saved yet</param>
		private static void StyleAfterName(ZDO zdo, VikingData rolled)
		{
			VikingNames.ReadRaw(zdo, out string name, out string _);
			if (VikingNames.IsElsa(name))
			{
				rolled.Hair = VikingGear.LongBraidHair;
				VikingGear.MakeBlondest(rolled);
			}
			else if (VikingNames.IsAnna(name))
			{
				rolled.Hair = VikingGear.TwinBraidsHair;
				VikingGear.MakeRedhead(rolled);
			}
		}

		/// <summary>
		/// Finds where the farm of the living viking's village stands - the one it belongs to, or else the nearest to it,
		/// for one spawned with its village that hasn't been taken in yet
		/// </summary>
		/// <returns>Where the farm stands, or where the viking stands when there's none</returns>
		private Vector3 VillageFarm()
		{
			Vector3 farm = nview.GetZDO().GetVec3(Village.VillageKey, Vector3.zero);
			if (farm != Vector3.zero)
			{
				return(farm);
			}
			ZDO generator = VillageRespawn.FindGenerator(transform.position);
			return((generator != null) ? generator.GetPosition() : transform.position);
		}

		/// <summary>
		/// Raises the living viking's skills faster while it's rested, the way a player's do
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <param name="multiplier">How much it rises, changed here</param>
		partial void ModifySkillGain(Skills.SkillType skill, ref float multiplier)
		{
			if (humanoid is LivingVikingBody living)
			{
				living.Rest.ModifyRaiseSkill(skill, ref multiplier);
			}
		}

		/// <summary>
		/// Glows around the living viking's head on every client as it reaches a new level, the way a player does, and
		/// has it say so
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <param name="raised">Its new level</param>
		partial void OnLevelUp(Skills.SkillType skill, float raised)
		{
			VillageSignals.SendEffect(VillageSignals.LevelUpEffect, humanoid.GetZDOID());
			VikingSpeech.Proud(humanoid, skill, Mathf.FloorToInt(raised));
		}

		/// <summary>
		/// Sets the living viking's max health from its biome and what it has eaten (see VikingFood.MaxHealth) - a
		/// dvergr viking's is its biome's and its stars', the same as a zombie viking of the world's
		/// </summary>
		/// <param name="health">Its max health, set here</param>
		partial void LivingMaxHealth(ref float health)
		{
			if (humanoid is LivingVikingBody living && IsLivingViking(humanoid))
			{
				health = living.Food.MaxHealth();
			}
		}

		/// <summary>
		/// Adds the living viking's ID (see VikingIdentity) and door line (see VikingDoors.DebugLine) to its debug summary
		/// </summary>
		/// <param name="text">The summary so far</param>
		partial void AddDebugLines(System.Text.StringBuilder text)
		{
			string id = ((humanoid.m_nview != null) ? VikingIdentity.Get(humanoid.m_nview.GetZDO()) : "");
			text.Append("ID: " + ((id.Length > 0) ? id : "none yet") + "\n");
			text.Append(VikingDoors.DebugLine(humanoid));
		}

		/// <summary>
		/// Names the living viking for the log by the name its village knows it by (see VikingNames)
		/// </summary>
		/// <param name="label">Set to its name</param>
		partial void NameForLog(ref string label)
		{
			label = TestLog.Label(humanoid);
		}

		/// <summary>
		/// Has the living viking keep out of the water while it has an enemy or is hunting and isn't on fire - its paths
		/// leave the water out, so a chase into it ends at the shore and a hunter shoots prey in the water, like a leech,
		/// from the bank - and walk the usual paths otherwise, which are the ones kept off roofs (see NavmeshRoofs)
		/// </summary>
		public void UpdatePathing()
		{
			if (ai == null)
			{
				return;
			}
			// A viking on fire runs for the water, even in a fight (see VillageLife.UpdateDousing)
			bool burning = humanoid.GetSEMan().HaveStatusEffect(SEMan.s_statusEffectBurning);
			bool ashore = (ai.GetTargetCreature() != null || (life != null && life.Hunting()));
			ai.m_pathAgentType = ((ashore && !burning) ? Pathfinding.AgentType.HumanoidAvoidWater : Pathfinding.AgentType.Humanoid);
		}

		/// <summary>
		/// Checks whether the living viking keeps off roofs - unless it's in a fight, as it only walks over a roof to get
		/// at an enemy: it has a target, is wary (see IVikingLife.IsWary) or has a deathsquito after it. Being alerted or
		/// hurt a moment ago with no enemy to fight doesn't take it onto a roof
		/// </summary>
		/// <returns>True when it does</returns>
		public bool AvoidsRoofs()
		{
			return(!Fighting());
		}

		/// <summary>
		/// Checks whether the living viking is in a fight - it has a target, is wary (see IVikingLife.IsWary) or has a
		/// deathsquito after it
		/// </summary>
		/// <returns>True when it is</returns>
		private bool Fighting()
		{
			return((life != null && life.IsWary()) || diver != null || (ai != null && ai.GetTargetCreature() != null));
		}

		/// <summary>
		/// Checks whether the living viking has no danger near - not alerted, without a target, not hurt in the last few
		/// seconds, not waiting for a corpse to rise and with no deathsquito after it
		/// </summary>
		/// <returns>True when it has none</returns>
		public bool NoDangerNear()
		{
			return(!InDanger());
		}

		/// <summary>
		/// Gets the navmesh areas the living viking's paths leave out - always the lava and the boiling sea (see
		/// DangerousGround), and roofs while it keeps off them (see AvoidsRoofs)
		/// </summary>
		/// <returns>The areas, as a navmesh area mask</returns>
		public int AvoidedAreas()
		{
			return(DangerousGround.AreaMask | (AvoidsRoofs() ? (1 << NavmeshRoofs.RoofArea) : 0));
		}
	}
}
