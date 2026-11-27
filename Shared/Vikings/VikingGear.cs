using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// A set of armor a viking can wear - always the chest and legs, and maybe the helmet and cape
	/// </summary>
	internal class ArmorSet
	{
		public string Chest;
		public string Legs;
		public string Helmet;
		public string Cape;

		/// <summary>
		/// Names the pieces of a set
		/// </summary>
		/// <param name="chest">The chest piece's prefab</param>
		/// <param name="legs">The leg piece's prefab</param>
		/// <param name="helmet">The helmet's prefab, or null when the set has none</param>
		/// <param name="cape">The cape's prefab, or null when the set has none</param>
		public ArmorSet(string chest, string legs, string helmet, string cape)
		{
			Chest = chest;
			Legs = legs;
			Helmet = helmet;
			Cape = cape;
		}
	}

	/// <summary>
	/// The weapons a viking starts with - one of any kind, a melee weapon only, a bow or crossbow only, or a bow or
	/// crossbow and a melee weapon to fight up close with
	/// </summary>
	internal enum Arms
	{
		Any,
		Melee,
		Ranged,
		Both
	}

	/// <summary>
	/// What vikings of one biome carry, how tough and skilled they are and what they drop
	/// </summary>
	internal class BiomeGear
	{
		public Heightmap.Biome Biome;
		public string[] Weapons;

		/// <summary>
		/// Arrows and bolts, each matched to the bows or crossbows that shoot it by its ammo type
		/// </summary>
		public string[] Ammo;
		public string[] Shields;
		public ArmorSet[] Armor;

		/// <summary>
		/// The cape a viking of the biome wears when its armor set has none or it wears no armor, for a biome whose
		/// vikings always wear one (see VikingGear.AlwaysCaped), or null
		/// </summary>
		public string Cape;
		public float Health;
		public float SkillMin;
		public float SkillMax;
		public int CoinsMin;
		public int CoinsMax;

		/// <summary>
		/// The item a viking drops from what's left of its body, like a draugr's entrails, or null for none
		/// </summary>
		public string Remains = "Entrails";

		/// <summary>
		/// The base damage of a viking's fists, before its skill and stars, in place of a player's
		/// </summary>
		public float Unarmed;

		/// <summary>
		/// How much harder a viking of the world hits with a melee weapon than a player of its skill would, so it hits
		/// about as hard as the biome's own creatures
		/// </summary>
		public float MeleeScale = 1f;

		/// <summary>
		/// The forsaken power a zombie viking of the biome can use - its own boss's, like Eikthyr's in the Meadows, and
		/// in the Deep North, which has no boss, Fader's - or null
		/// </summary>
		public string Power;
	}

	/// <summary>
	/// The gear tables for vikings of the world, and the rolling of each one's looks and loadout
	/// </summary>
	/// <remarks>
	/// The biomes are in the order they're reached, so a viking draws its gear from its own biome and the one before.
	/// Weapons and ammo that deal spirit damage are never picked, since the undead can't bear them
	/// </remarks>
	internal static class VikingGear
	{
		private const int AmmoStack = 100;

		/// <summary>
		/// The weapon Deep North vikings never carry, though it's in the Ashlands gear they draw from
		/// </summary>
		internal const string DeepNorthBanned = "SwordDyrnwyn";

		/// <summary>
		/// Skin tones from palest to darkest, and hair colors, as the player's customization stores them
		/// </summary>
		private static readonly Vector3 PaleSkin = new Vector3(1f, 1f, 1f);
		private static readonly Vector3 DarkSkin = new Vector3(0.45f, 0.33f, 0.27f);
		private static readonly Vector3[] HairColors = new Vector3[]
		{
			new Vector3(0.12f, 0.09f, 0.07f),
			new Vector3(0.38f, 0.24f, 0.14f),
			new Vector3(0.95f, 0.8f, 0.5f),
			new Vector3(0.75f, 0.32f, 0.13f),
			new Vector3(0.7f, 0.7f, 0.7f)
		};
		private const int HairStyles = 31;
		private const int BeardStyles = 21;
		private const float BeardChance = 0.7f;

		/// <summary>
		/// The blond and red of HairColors, and the range a viking's hair color is brightened or darkened by as it's
		/// rolled
		/// </summary>
		private const int BlondHair = 2;
		private const int RedHair = 3;
		private const float HairBrightnessMin = 0.85f;
		private const float HairBrightnessMax = 1.1f;

		/// <summary>
		/// The hair the game calls Long Braid and Twin Braids, as the player's customization stores them
		/// </summary>
		public const string LongBraidHair = "Hair11";
		public const string TwinBraidsHair = "Hair13";

		/// <summary>
		/// The beard item of a viking with no beard, as the player's customization stores it
		/// </summary>
		public const string NoBeard = "BeardNone";

		public static readonly BiomeGear[] Biomes = new BiomeGear[]
		{
			new BiomeGear
			{
				Biome = Heightmap.Biome.Meadows,
				Weapons = new[] { "AxeStone", "AxeFlint", "Club", "SpearFlint", "KnifeFlint", "Bow", "AxeEarly" },
				Ammo = new[] { "ArrowWood", "ArrowFlint", "ArrowFire" },
				Shields = new[] { "ShieldWood" },
				Armor = new[] { new ArmorSet("ArmorRagsChest", "ArmorRagsLegs", null, null), new ArmorSet("ArmorLeatherChest", "ArmorLeatherLegs", "HelmetLeather", "CapeDeerHide") },
				Health = 50f, SkillMin = VikingCore.SkillFloor, SkillMax = 12f, CoinsMin = 0, CoinsMax = 10, Remains = null, Unarmed = 25f, MeleeScale = 1.4f, Power = "GP_Eikthyr"
			},
			new BiomeGear
			{
				Biome = Heightmap.Biome.BlackForest,
				Weapons = new[] { "AxeBronze", "MaceBronze", "SwordBronze", "SpearBronze", "AtgeirBronze", "KnifeCopper", "FistBjornClaw", "SledgeStagbreaker", "BowFineWood" },
				Ammo = new[] { "ArrowBronze" },
				Shields = new[] { "ShieldBronzeBuckler" },
				Armor = new[] { new ArmorSet("ArmorTrollLeatherChest", "ArmorTrollLeatherLegs", "HelmetTrollLeather", "CapeTrollHide"), new ArmorSet("ArmorBerserkerChest", "ArmorBerserkerLegs", "HelmetBerserkerHood", null), new ArmorSet("ArmorBronzeChest", "ArmorBronzeLegs", "HelmetBronze", null) },
				Health = 120f, SkillMin = 13f, SkillMax = 25f, CoinsMin = 10, CoinsMax = 30, Remains = null, Unarmed = 45f, MeleeScale = 1.7f, Power = "GP_TheElder"
			},
			new BiomeGear
			{
				Biome = Heightmap.Biome.Swamp,
				Weapons = new[] { "AxeIron", "Battleaxe", "MaceIron", "SwordIron", "SpearElderbark", "AtgeirIron", "SledgeIron", "BowHuntsman" },
				Ammo = new[] { "ArrowIron", "ArrowPoison" },
				Shields = new[] { "ShieldBanded", "ShieldRoots", "ShieldIronBuckler" },
				Armor = new[] { new ArmorSet("ArmorRootChest", "ArmorRootLegs", "HelmetRoot", null), new ArmorSet("ArmorIronChest", "ArmorIronLegs", "HelmetIron", null) },
				Health = 200f, SkillMin = 26f, SkillMax = 37f, CoinsMin = 30, CoinsMax = 40, Unarmed = 75f, MeleeScale = 1.8f, Power = "GP_Bonemass"
			},
			new BiomeGear
			{
				Biome = Heightmap.Biome.Mountain,
				Weapons = new[] { "SpearWolfFang", "BowDraugrFang" },
				Ammo = new[] { "ArrowObsidian", "ArrowFrost" },
				Shields = new[] { "ShieldSilver" },
				Armor = new[] { new ArmorSet("ArmorFenringChest", "ArmorFenringLegs", "HelmetFenring", null) },
				Cape = "CapeWolf",
				Health = 300f, SkillMin = 38f, SkillMax = 50f, CoinsMin = 40, CoinsMax = 70, Unarmed = 95f, MeleeScale = 1.75f, Power = "GP_Moder"
			},
			new BiomeGear
			{
				Biome = Heightmap.Biome.Plains,
				Weapons = new[] { "AxeBlackMetal", "BattleaxeBlackmetal", "MaceNeedle", "SwordBlackmetal", "AtgeirBlackmetal", "KnifeBlackMetal", "FistBjornUndeadClaw", "KnifeSkollAndHati" },
				Ammo = new[] { "ArrowNeedle" },
				Shields = new[] { "ShieldBlackmetal" },
				Armor = new[] { new ArmorSet("ArmorLoxChest", "ArmorLoxLegs", "HelmetLox", "CapeLox"), new ArmorSet("ArmorBerserkerUndeadChest", "ArmorBerserkerUndeadLegs", "HelmetBerserkerUndead", null), new ArmorSet("ArmorPaddedCuirass", "ArmorPaddedGreaves", "HelmetPadded", "CapeLinen") },
				Cape = "CapeLox",
				Health = 450f, SkillMin = 50f, SkillMax = 62f, CoinsMin = 70, CoinsMax = 100, Unarmed = 110f, MeleeScale = 1.25f, Power = "GP_Yagluth"
			},
			new BiomeGear
			{
				Biome = Heightmap.Biome.Mistlands,
				Weapons = new[] { "SledgeDemolisher", "SpearCarapace", "AtgeirHimminAfl", "AxeJotunBane", "BowSpineSnap", "BattleaxeSkullSplittur", "CrossbowArbalest" },
				Ammo = new[] { "ArrowCarapace", "BoltBone", "BoltIron" },
				Shields = new[] { "ShieldCarapace", "ShieldCarapaceBuckler" },
				Armor = new[] { new ArmorSet("ArmorCarapaceChest", "ArmorCarapaceLegs", "HelmetCarapace", "CapeFeather") },
				Cape = "CapeFeather",
				Health = 650f, SkillMin = 63f, SkillMax = 75f, CoinsMin = 100, CoinsMax = 140, Unarmed = 125f, Power = "GP_Queen"
			},
			new BiomeGear
			{
				Biome = Heightmap.Biome.AshLands,
				Weapons = new[] { "AxeBerzerkr", "MaceEldner", "SwordDyrnwyn", "SpearSplitner", "CrossbowRipper" },
				Ammo = new[] { "ArrowCharred", "BoltBlackmetal", "BoltCarapace", "BoltCharred" },
				Shields = new[] { "ShieldFlametal" },
				Armor = new[] { new ArmorSet("ArmorAshlandsMediumChest", "ArmorAshlandsMediumlegs", "HelmetAshlandsMediumHood", "CapeAsksvin"), new ArmorSet("ArmorFlametalChest", "ArmorFlametalLegs", "HelmetFlametal", "CapeAsh") },
				Cape = "CapeAsksvin",
				Health = 900f, SkillMin = 76f, SkillMax = 87f, CoinsMin = 140, CoinsMax = 200, Unarmed = 145f, Power = "GP_Fader"
			},
			new BiomeGear
			{
				Biome = Heightmap.Biome.DeepNorth,
				Weapons = new[] { "AxeGold", "BattleaxeGold", "MaceGold", "SledgeGold", "AtgeirGold", "SpearGold", "SwordGold", "THSwordGold", "KnifeGold", "FistGold", "BowGold", "CrossbowGold" },
				Ammo = new[] { "ArrowBloodGold", "BoltBloodGold" },
				Shields = new[] { "ShieldGold", "ShieldGoldBuckler" },
				Armor = new[] { new ArmorSet("ArmorDeepNorthMediumChest", "ArmorDeepNorthMediumlegs", "HelmetDNMediumHood", "CapeDeepNorth"), new ArmorSet("ArmorDeepNorthHeavyChest", "ArmorDeepNorthHeavylegs", "HelmetDNHeavy", "CapeDeepNorth") },
				Cape = "CapeDeepNorth",
				Health = 1200f, SkillMin = 88f, SkillMax = 100f, CoinsMin = 200, CoinsMax = 300, Unarmed = 165f, Power = "GP_Fader"
			}
		};

		/// <summary>
		/// Finds a biome's place in the tables
		/// </summary>
		/// <param name="biome">The biome, where Ocean and unknown biomes count as Meadows</param>
		/// <returns>The index into Biomes</returns>
		public static int IndexOf(Heightmap.Biome biome)
		{
			for (int i = 0; i < Biomes.Length; i++)
			{
				if (Biomes[i].Biome == biome)
				{
					return(i);
				}
			}
			return(0);
		}

		/// <summary>
		/// Gets the gear of a biome, clamping an unknown index to Meadows
		/// </summary>
		/// <param name="index">The index into Biomes</param>
		/// <returns>The biome's gear</returns>
		public static BiomeGear Get(int index)
		{
			return(Biomes[Mathf.Clamp(index, 0, Biomes.Length - 1)]);
		}

		/// <summary>
		/// Rolls a viking of the world's looks, health, skills and power for its biome - each skill its own level within
		/// the biome's range, and one more kept for a skill the game adds later
		/// </summary>
		/// <param name="index">The index of its biome</param>
		/// <returns>Its data, without its weapon slot, which RollLoadout sets</returns>
		public static VikingData RollData(int index)
		{
			BiomeGear gear = Get(index);
			VikingData data = new VikingData();
			data.Tombstone = false;
			data.Biome = index;
			data.SkinColor = Vector3.Lerp(PaleSkin, DarkSkin, Random.value);
			data.HairColor = HairColors[Random.Range(0, HairColors.Length)] * Random.Range(HairBrightnessMin, HairBrightnessMax);
			data.Hair = "Hair" + Random.Range(1, HairStyles + 1);
			SetModel(data, Random.Range(0, 2));
			data.MaxHealth = gear.Health;
			data.Skill = Random.Range(gear.SkillMin, gear.SkillMax);
			foreach (Skills.SkillType skill in System.Enum.GetValues(typeof(Skills.SkillType)))
			{
				if (skill != Skills.SkillType.None && skill != Skills.SkillType.All)
				{
					data.Skills[(int)skill] = Random.Range(gear.SkillMin, gear.SkillMax);
				}
			}
			data.Power = (string.IsNullOrEmpty(gear.Power) ? 0 : gear.Power.GetStableHashCode());
			return(data);
		}

		/// <summary>
		/// Makes a viking being rolled a woman or a man, taking a woman's beard away or rolling a man's
		/// </summary>
		/// <param name="data">Its data, before its gear is rolled</param>
		/// <param name="model">1 for a woman, 0 for a man</param>
		public static void SetModel(VikingData data, int model)
		{
			data.Model = model;
			data.Beard = ((model == 0 && Random.value < BeardChance) ? "Beard" + Random.Range(1, BeardStyles + 1) : NoBeard);
		}

		/// <summary>
		/// Gives a viking being rolled the blondest hair color a viking is rolled with - the blond of HairColors at the
		/// brightest of the range it's rolled in
		/// </summary>
		/// <param name="data">Its data</param>
		public static void MakeBlondest(VikingData data)
		{
			data.HairColor = HairColors[BlondHair] * HairBrightnessMax;
		}

		/// <summary>
		/// Gives a viking being rolled red hair - the red of HairColors, brightened or darkened the way any viking's hair
		/// color is rolled
		/// </summary>
		/// <param name="data">Its data</param>
		public static void MakeRedhead(VikingData data)
		{
			data.HairColor = HairColors[RedHair] * Random.Range(HairBrightnessMin, HairBrightnessMax);
		}

		/// <summary>
		/// The share of a biome's PC vikings that start with only a bow or crossbow and the share that start with
		/// only a melee weapon, the rest starting with both
		/// </summary>
		internal class ArmsSplit
		{
			public float Ranged;
			public float Melee;
		}

		/// <summary>
		/// How the PC vikings of the biomes before the Mountains are split between bows, melee weapons and both -
		/// a biome not listed, like the Meadows, gives its village's archer both and the rest one weapon of any kind
		/// </summary>
		internal static readonly Dictionary<Heightmap.Biome, ArmsSplit> ArmsSplits = new Dictionary<Heightmap.Biome, ArmsSplit>()
		{
			{ Heightmap.Biome.BlackForest, new ArmsSplit { Ranged = 1f / 3f, Melee = 1f / 3f } },
			{ Heightmap.Biome.Swamp, new ArmsSplit { Ranged = 0.2f, Melee = 0.2f } }
		};

		/// <summary>
		/// Picks the weapons a PC viking of a biome starts with. From the Mountains on, every one has a bow or
		/// crossbow and a melee weapon. A biome in ArmsSplits splits them by its shares - except one marked as its
		/// village's archer, which has a bow either way, alone or with a melee weapon in the same proportion as the
		/// rest. Elsewhere one marked as its village's archer has both, and the rest one weapon of any kind
		/// </summary>
		/// <param name="index">The index of its biome</param>
		/// <param name="marked">Whether it's marked as its village's archer (see VikingCore.ArcherKey)</param>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>Its weapons</returns>
		internal static Arms PCArms(int index, bool marked, float roll)
		{
			if (index >= IndexOf(Heightmap.Biome.Mountain))
			{
				return(Arms.Both);
			}
			if (ArmsSplits.TryGetValue(Get(index).Biome, out ArmsSplit split))
			{
				if (marked)
				{
					return((roll * (1f - split.Melee) < split.Ranged) ? Arms.Ranged : Arms.Both);
				}
				if (roll < split.Ranged)
				{
					return(Arms.Ranged);
				}
				return((roll < split.Ranged + split.Melee) ? Arms.Melee : Arms.Both);
			}
			return(marked ? Arms.Both : Arms.Any);
		}

		/// <summary>
		/// The dverger circlet a PC viking can start with in place of a torch, which lights its way as it wears it
		/// (see VikingCore.UpdateTorch)
		/// </summary>
		public const string CircletPrefab = "HelmetDverger";

		/// <summary>
		/// The chance that a PC viking of a biome before the Mistlands starts with a dverger circlet rather than a
		/// torch - a biome not listed, like the Meadows, never has one, and from the Mistlands on every one has
		/// </summary>
		internal static readonly Dictionary<Heightmap.Biome, float> CircletChances = new Dictionary<Heightmap.Biome, float>()
		{
			{ Heightmap.Biome.BlackForest, 0.1f },
			{ Heightmap.Biome.Swamp, 0.3f },
			{ Heightmap.Biome.Mountain, 0.5f },
			{ Heightmap.Biome.Plains, 0.7f }
		};

		/// <summary>
		/// Checks whether a PC viking of a biome starts with a dverger circlet rather than a torch, by CircletChances
		/// </summary>
		/// <param name="index">The index of its biome</param>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>True for a circlet</returns>
		internal static bool PCCirclet(int index, float roll)
		{
			if (index >= IndexOf(Heightmap.Biome.Mistlands))
			{
				return(true);
			}
			return(CircletChances.TryGetValue(Get(index).Biome, out float chance) && roll < chance);
		}

		/// <summary>
		/// The wisp light a PC viking of the Mistlands wears, which pushes back the mist around it
		/// </summary>
		public const string WispPrefab = "Demister";

		/// <summary>
		/// Checks whether a PC viking of a biome starts with a wisp light - one of the Mistlands
		/// </summary>
		/// <param name="index">The index of its biome</param>
		/// <returns>True when it does</returns>
		internal static bool PCWisp(int index)
		{
			return(index == IndexOf(Heightmap.Biome.Mistlands));
		}

		/// <summary>
		/// Gives a viking a wisp light, worn
		/// </summary>
		/// <param name="inventory">Its inventory</param>
		public static void AddWisp(Inventory inventory)
		{
			Wear(inventory, MakeItem(WispPrefab, 1, 1), false);
		}

		/// <summary>
		/// Rolls a viking of the world's armor, weapon, ammo and shield from its biome and the one before, by the chances
		/// in the config, upgraded once for each of its stars when ZombieStarsUpgradeGear is on, with each piece marked as
		/// worn. From the Mountains on it always wears a cape - its set's, or its biome's when its set has none or it
		/// wears no armor (see AlwaysCaped)
		/// </summary>
		/// <param name="index">The index of its biome</param>
		/// <param name="level">Its level, where 2 is one star</param>
		/// <param name="data">Its data, given the slot of its weapon</param>
		/// <param name="armed">True for a viking that always wears armor and wields a weapon, skipping ZombieArmorChance
		/// and ZombieWeaponChance - its helmet and shield are still rolled, but for its helmet from the Mountains on
		/// (see AlwaysHelmeted)</param>
		/// <param name="arms">The weapons it wields - any but Any always gives it one, skipping ZombieWeaponChance, and
		/// a bow or crossbow comes with its arrows or bolts</param>
		/// <param name="repaired">True for gear that's fully repaired, like a PC viking's, rather than worn down
		/// between ZombieDurabilityMin and ZombieDurabilityMax like a zombie viking's</param>
		/// <param name="ownBiome">True for gear only from its own biome, like a PC viking's, each piece falling back
		/// to the nearest biome below with some that will do only when its own has none - like a bow, for a biome with
		/// none</param>
		/// <returns>The inventory holding the gear</returns>
		public static Inventory RollLoadout(int index, int level, VikingData data, bool armed, Arms arms, bool repaired, bool ownBiome)
		{
			bool worn = !repaired;
			Inventory inventory = new Inventory(VikingCore.ZombiePrefabName, null, data.Width, data.Height);
			bool deepNorth = (Get(index).Biome == Heightmap.Biome.DeepNorth);
			int quality = (VikingSettings.Get(VikingSettings.ZombieStarsUpgradeGear, true) ? level : 1);
			System.Predicate<string> allowed = (string name) => !(deepNorth && name == DeepNorthBanned) && IsAllowed(name);
			// A PC viking from the Mountains on always wears its set's helmet, which every set there has, and every
			// viking from there on wears a cape
			bool helmeted = (armed && AlwaysHelmeted(index));
			bool caped = AlwaysCaped(index);
			bool capeWorn = false;
			if (armed || Roll(VikingSettings.ZombieArmorChance))
			{
				List<ArmorSet> sets = Gather(index, ownBiome, (BiomeGear gear) => gear.Armor, null);
				ArmorSet set = sets[Random.Range(0, sets.Count)];
				Wear(inventory, MakeItem(set.Chest, quality, 1), worn);
				Wear(inventory, MakeItem(set.Legs, quality, 1), worn);
				if (set.Helmet != null && (helmeted || Roll(VikingSettings.HelmetChance)))
				{
					Wear(inventory, MakeItem(set.Helmet, quality, 1), worn);
				}
				if (set.Cape != null && (caped || Roll(VikingSettings.CapeChance)))
				{
					Wear(inventory, MakeItem(set.Cape, quality, 1), worn);
					capeWorn = true;
				}
			}
			if (caped && !capeWorn && Get(index).Cape != null)
			{
				Wear(inventory, MakeItem(Get(index).Cape, quality, 1), worn);
			}
			ItemDrop.ItemData weapon = null;
			bool shoots = (arms == Arms.Ranged || arms == Arms.Both);
			if (armed || arms != Arms.Any || Roll(VikingSettings.ZombieWeaponChance))
			{
				System.Predicate<string> wanted = allowed;
				if (shoots)
				{
					wanted = (string name) => allowed(name) && IsBow(name);
				}
				else if (arms == Arms.Melee)
				{
					wanted = (string name) => allowed(name) && !IsRangedWeapon(name);
				}
				List<string> weapons = Gather(index, ownBiome, (BiomeGear gear) => gear.Weapons, wanted);
				if (weapons.Count == 0)
				{
					weapons = Gather(index, ownBiome, (BiomeGear gear) => gear.Weapons, allowed);
				}
				if (weapons.Count > 0)
				{
					weapon = MakeItem(weapons[Random.Range(0, weapons.Count)], quality, 1);
				}
			}
			if (weapon != null && worn)
			{
				WearDown(weapon);
			}
			if (weapon != null && inventory.AddItem(weapon))
			{
				data.WeaponSlot = weapon.m_gridPos;
				string ammoType = weapon.m_shared.m_ammoType;
				if (!string.IsNullOrEmpty(ammoType))
				{
					List<ItemDrop.ItemData> ammo = new List<ItemDrop.ItemData>();
					foreach (string name in Gather(index, ownBiome, (BiomeGear gear) => gear.Ammo, (string name) => Fits(name, ammoType)))
					{
						ammo.Add(MakeItem(name, 1, AmmoStack));
					}
					if (ammo.Count > 0)
					{
						inventory.AddItem(ammo[Random.Range(0, ammo.Count)]);
					}
				}
			}
			// A viking given both carries a melee weapon of its biome beside its bow, to fight up close with
			ItemDrop.ItemData sidearm = null;
			if (arms == Arms.Both)
			{
				List<string> melee = Gather(index, ownBiome, (BiomeGear gear) => gear.Weapons, (string name) => allowed(name) && !IsRangedWeapon(name));
				sidearm = ((melee.Count > 0) ? MakeItem(melee[Random.Range(0, melee.Count)], quality, 1) : null);
				if (sidearm != null && !inventory.AddItem(sidearm))
				{
					sidearm = null;
				}
			}
			ItemDrop.ItemData handed = (sidearm ?? weapon);
			bool freeHand = (handed == null || !handed.IsTwoHanded());
			if (freeHand && Roll(VikingSettings.ShieldChance))
			{
				List<string> shields = Gather(index, ownBiome, (BiomeGear gear) => gear.Shields, null);
				Wear(inventory, MakeItem(shields[Random.Range(0, shields.Count)], quality, 1), worn);
			}
			return(inventory);
		}

		/// <summary>
		/// Gathers what a piece of a viking's gear is picked from - its biome's and the one before's, or only its own
		/// biome's when it should have that, falling back to the nearest biome below with some that will do when its
		/// own has none
		/// </summary>
		/// <typeparam name="T">What's gathered, like a weapon's prefab name or an armor set</typeparam>
		/// <param name="index">The index of its biome</param>
		/// <param name="ownBiome">True to keep to its own biome's when it has any</param>
		/// <param name="part">What each biome has of it</param>
		/// <param name="wanted">Which of them will do, or null for all</param>
		/// <returns>What will do, its own biome's first</returns>
		private static List<T> Gather<T>(int index, bool ownBiome, System.Func<BiomeGear, IEnumerable<T>> part, System.Predicate<T> wanted)
		{
			List<T> found = new List<T>();
			int lowest = (ownBiome ? 0 : Mathf.Max(0, index - 1));
			for (int i = index; i >= lowest; i--)
			{
				foreach (T item in part(Get(i)))
				{
					if (wanted == null || wanted(item))
					{
						found.Add(item);
					}
				}
				if (ownBiome && found.Count > 0)
				{
					break;
				}
			}
			return(found);
		}

		/// <summary>
		/// Checks whether ammo fits a weapon - its ammo type, with no spirit damage, which a viking couldn't use well
		/// </summary>
		/// <param name="name">The ammo's prefab name</param>
		/// <param name="ammoType">The weapon's ammo type</param>
		/// <returns>True when it fits</returns>
		private static bool Fits(string name, string ammoType)
		{
			ItemDrop.ItemData candidate = MakeItem(name, 1, AmmoStack);
			return(candidate != null && candidate.m_shared.m_ammoType == ammoType && candidate.GetDamage().m_spirit <= 0f);
		}

		/// <summary>
		/// Checks whether every PC viking of a biome wears a helmet - the Mountains and every biome after, by Biomes'
		/// order
		/// </summary>
		/// <param name="index">The index of the biome</param>
		/// <returns>True when it does</returns>
		internal static bool AlwaysHelmeted(int index)
		{
			return(index >= IndexOf(Heightmap.Biome.Mountain));
		}

		/// <summary>
		/// Checks whether every viking of a biome, living or zombie, wears a cape - the Mountains and every biome after,
		/// by Biomes' order
		/// </summary>
		/// <param name="index">The index of the biome</param>
		/// <returns>True when it does</returns>
		internal static bool AlwaysCaped(int index)
		{
			return(index >= IndexOf(Heightmap.Biome.Mountain));
		}

		/// <summary>
		/// Checks whether a biome's weather freezes - the Mountains and the Deep North
		/// </summary>
		/// <param name="index">The index of the biome</param>
		/// <returns>True when it does</returns>
		public static bool Freezes(int index)
		{
			Heightmap.Biome biome = Get(index).Biome;
			return(biome == Heightmap.Biome.Mountain || biome == Heightmap.Biome.DeepNorth);
		}

		/// <summary>
		/// Checks whether a spot is in a biome whose weather freezes (see Freezes)
		/// </summary>
		/// <param name="position">The spot</param>
		/// <returns>True when it is</returns>
		public static bool FreezesAt(Vector3 position)
		{
			return(WorldGenerator.instance != null && Freezes(IndexOf(WorldGenerator.instance.GetBiome(position))));
		}

		/// <summary>
		/// Checks whether a piece of gear resists frost, so wearing it keeps the cold off the way it does a player
		/// </summary>
		/// <param name="item">The piece, or null</param>
		/// <returns>True when it does</returns>
		public static bool KeepsOffFrost(ItemDrop.ItemData item)
		{
			return(item != null && item.m_shared.m_damageModifiers.Exists((HitData.DamageModPair mod) => mod.m_type == HitData.DamageType.Frost && KeepsOffFrost(mod.m_modifier)));
		}

		/// <summary>
		/// Checks whether a frost damage modifier is a resistance, which keeps the cold off the way it does a player
		/// </summary>
		/// <param name="frost">The modifier</param>
		/// <returns>True when it is</returns>
		public static bool KeepsOffFrost(HitData.DamageModifier frost)
		{
			return(frost == HitData.DamageModifier.Resistant || frost == HitData.DamageModifier.VeryResistant || frost == HitData.DamageModifier.SlightlyResistant);
		}

		/// <summary>
		/// Rolls a percent chance from the config
		/// </summary>
		/// <param name="chance">The setting, as a percent, or null when the mod has no such setting, which always succeeds</param>
		/// <returns>True when the roll succeeds</returns>
		private static bool Roll(ConfigEntry<int> chance)
		{
			return(Random.value * 100f < VikingSettings.Get(chance, 100));
		}

		/// <summary>
		/// Adds an item to an inventory marked as worn, so it's put on as the viking loads
		/// </summary>
		/// <param name="inventory">The inventory</param>
		/// <param name="item">The item, or null when it couldn't be made</param>
		/// <param name="wornDown">True to wear it down first (see WearDown), false to leave it fully repaired</param>
		private static void Wear(Inventory inventory, ItemDrop.ItemData item, bool wornDown)
		{
			if (item == null)
			{
				return;
			}
			item.m_equipped = true;
			if (wornDown)
			{
				WearDown(item);
			}
			inventory.AddItem(item);
		}

		/// <summary>
		/// Wears an item down to between the config's ZombieDurabilityMin and ZombieDurabilityMax percent of its full
		/// durability
		/// </summary>
		/// <param name="item">The item</param>
		private static void WearDown(ItemDrop.ItemData item)
		{
			if (item.m_shared.m_useDurability)
			{
				int least = VikingSettings.Get(VikingSettings.ZombieDurabilityMin, 100);
				int most = VikingSettings.Get(VikingSettings.ZombieDurabilityMax, 100);
				float min = Mathf.Min(least, most);
				float max = Mathf.Max(least, most);
				item.m_durability = item.GetMaxDurability() * Random.Range(min, max) / 100f;
			}
		}

		/// <summary>
		/// Checks that a weapon exists and deals no spirit damage
		/// </summary>
		/// <param name="name">The weapon's prefab name</param>
		/// <returns>True when a viking can carry it</returns>
		private static bool IsAllowed(string name)
		{
			ItemDrop.ItemData item = MakeItem(name, 1, 1);
			return(item != null && item.GetDamage().m_spirit <= 0f);
		}

		/// <summary>
		/// Checks whether a weapon is a bow, one that shoots arrows
		/// </summary>
		/// <param name="name">The weapon's prefab name</param>
		/// <returns>True when it is</returns>
		private static bool IsBow(string name)
		{
			ItemDrop.ItemData item = MakeItem(name, 1, 1);
			return(item != null && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow && !string.IsNullOrEmpty(item.m_shared.m_ammoType));
		}

		/// <summary>
		/// Checks whether a weapon shoots - a bow or crossbow
		/// </summary>
		/// <param name="name">The weapon's prefab name</param>
		/// <returns>True when it does</returns>
		private static bool IsRangedWeapon(string name)
		{
			return(VikingCore.IsRanged(MakeItem(name, 1, 1)));
		}

		/// <summary>
		/// Makes a new item from its prefab, in a random style when it has several, fully repaired
		/// </summary>
		/// <param name="name">The item's prefab name</param>
		/// <param name="quality">Its quality, capped at the item's highest</param>
		/// <param name="stack">How many, capped at its stack size</param>
		/// <returns>The item, or null when the game has no such item</returns>
		public static ItemDrop.ItemData MakeItem(string name, int quality, int stack)
		{
			GameObject prefab = ((ObjectDB.instance != null) ? ObjectDB.instance.GetItemPrefab(name) : null);
			ItemDrop drop = ((prefab != null) ? prefab.GetComponent<ItemDrop>() : null);
			if (drop == null)
			{
				return(null);
			}
			ItemDrop.ItemData item = drop.m_itemData.Clone();
			item.m_dropPrefab = prefab;
			item.m_quality = Mathf.Clamp(quality, 1, Mathf.Max(1, item.m_shared.m_maxQuality));
			item.m_stack = Mathf.Clamp(stack, 1, Mathf.Max(1, item.m_shared.m_maxStackSize));
			item.m_variant = ((item.m_shared.m_variants > 1) ? Random.Range(0, item.m_shared.m_variants) : 0);
			item.m_worldLevel = Game.m_worldLevel;
			item.m_durability = item.GetMaxDurability();
			item.m_equipped = false;
			return(item);
		}
	}
}
