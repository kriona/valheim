using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace ZombieApocalypse
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
		public float Health;
		public float SkillMin;
		public float SkillMax;
		public int CoinsMin;
		public int CoinsMax;

		/// <summary>
		/// The item a viking drops from what's left of its body, like a draugr's entrails
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
		/// The forsaken power a viking of the biome can use, or null
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
		private const string DeepNorthBanned = "SwordDyrnwyn";

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

		public static readonly BiomeGear[] Biomes = new BiomeGear[]
		{
			new BiomeGear
			{
				Biome = Heightmap.Biome.Meadows,
				Weapons = new[] { "AxeStone", "AxeFlint", "Club", "SpearFlint", "KnifeFlint", "Bow", "AxeEarly" },
				Ammo = new[] { "ArrowWood", "ArrowFlint", "ArrowFire" },
				Shields = new[] { "ShieldWood" },
				Armor = new[] { new ArmorSet("ArmorRagsChest", "ArmorRagsLegs", null, null), new ArmorSet("ArmorLeatherChest", "ArmorLeatherLegs", "HelmetLeather", "CapeDeerHide") },
				Health = 50f, SkillMin = 0f, SkillMax = 12f, CoinsMin = 0, CoinsMax = 10, Remains = "BoneFragments", Unarmed = 25f, MeleeScale = 1.4f, Power = null
			},
			new BiomeGear
			{
				Biome = Heightmap.Biome.BlackForest,
				Weapons = new[] { "AxeBronze", "MaceBronze", "SwordBronze", "SpearBronze", "AtgeirBronze", "KnifeCopper", "FistBjornClaw", "SledgeStagbreaker", "BowFineWood" },
				Ammo = new[] { "ArrowBronze" },
				Shields = new[] { "ShieldBronzeBuckler" },
				Armor = new[] { new ArmorSet("ArmorTrollLeatherChest", "ArmorTrollLeatherLegs", "HelmetTrollLeather", "CapeTrollHide"), new ArmorSet("ArmorBerserkerChest", "ArmorBerserkerLegs", "HelmetBerserkerHood", null), new ArmorSet("ArmorBronzeChest", "ArmorBronzeLegs", "HelmetBronze", null) },
				Health = 120f, SkillMin = 13f, SkillMax = 25f, CoinsMin = 10, CoinsMax = 30, Remains = "BoneFragments", Unarmed = 45f, MeleeScale = 1.7f, Power = "GP_Eikthyr"
			},
			new BiomeGear
			{
				Biome = Heightmap.Biome.Swamp,
				Weapons = new[] { "AxeIron", "Battleaxe", "MaceIron", "SwordIron", "SpearElderbark", "AtgeirIron", "SledgeIron", "BowHuntsman" },
				Ammo = new[] { "ArrowIron", "ArrowPoison" },
				Shields = new[] { "ShieldBanded", "ShieldRoots", "ShieldIronBuckler" },
				Armor = new[] { new ArmorSet("ArmorRootChest", "ArmorRootLegs", "HelmetRoot", null), new ArmorSet("ArmorIronChest", "ArmorIronLegs", "HelmetIron", null) },
				Health = 200f, SkillMin = 26f, SkillMax = 37f, CoinsMin = 30, CoinsMax = 40, Unarmed = 75f, MeleeScale = 1.8f, Power = "GP_TheElder"
			},
			new BiomeGear
			{
				Biome = Heightmap.Biome.Mountain,
				Weapons = new[] { "SpearWolfFang", "BowDraugrFang" },
				Ammo = new[] { "ArrowObsidian", "ArrowFrost" },
				Shields = new[] { "ShieldSilver" },
				Armor = new[] { new ArmorSet("ArmorFenringChest", "ArmorFenringLegs", "HelmetFenring", null) },
				Health = 300f, SkillMin = 38f, SkillMax = 50f, CoinsMin = 40, CoinsMax = 70, Unarmed = 95f, MeleeScale = 1.75f, Power = "GP_Bonemass"
			},
			new BiomeGear
			{
				Biome = Heightmap.Biome.Plains,
				Weapons = new[] { "AxeBlackMetal", "BattleaxeBlackmetal", "MaceNeedle", "SwordBlackmetal", "AtgeirBlackmetal", "KnifeBlackMetal", "FistBjornUndeadClaw", "KnifeSkollAndHati" },
				Ammo = new[] { "ArrowNeedle" },
				Shields = new[] { "ShieldBlackmetal" },
				Armor = new[] { new ArmorSet("ArmorLoxChest", "ArmorLoxLegs", "HelmetLox", "CapeLox"), new ArmorSet("ArmorBerserkerUndeadChest", "ArmorBerserkerUndeadLegs", "HelmetBerserkerUndead", null), new ArmorSet("ArmorPaddedCuirass", "ArmorPaddedGreaves", "HelmetPadded", "CapeLinen") },
				Health = 450f, SkillMin = 50f, SkillMax = 62f, CoinsMin = 70, CoinsMax = 100, Unarmed = 110f, MeleeScale = 1.25f, Power = "GP_Moder"
			},
			new BiomeGear
			{
				Biome = Heightmap.Biome.Mistlands,
				Weapons = new[] { "SledgeDemolisher", "SpearCarapace", "AtgeirHimminAfl", "AxeJotunBane", "BowSpineSnap", "BattleaxeSkullSplittur", "CrossbowArbalest" },
				Ammo = new[] { "ArrowCarapace", "BoltBone", "BoltIron" },
				Shields = new[] { "ShieldCarapace", "ShieldCarapaceBuckler" },
				Armor = new[] { new ArmorSet("ArmorCarapaceChest", "ArmorCarapaceLegs", "HelmetCarapace", "CapeFeather") },
				Health = 650f, SkillMin = 63f, SkillMax = 75f, CoinsMin = 100, CoinsMax = 140, Unarmed = 125f, Power = "GP_Yagluth"
			},
			new BiomeGear
			{
				Biome = Heightmap.Biome.AshLands,
				Weapons = new[] { "AxeBerzerkr", "MaceEldner", "SwordDyrnwyn", "SpearSplitner", "CrossbowRipper" },
				Ammo = new[] { "ArrowCharred", "BoltBlackmetal", "BoltCarapace", "BoltCharred" },
				Shields = new[] { "ShieldFlametal" },
				Armor = new[] { new ArmorSet("ArmorAshlandsMediumChest", "ArmorAshlandsMediumlegs", "HelmetAshlandsMediumHood", "CapeAsksvin"), new ArmorSet("ArmorFlametalChest", "ArmorFlametalLegs", "HelmetFlametal", "CapeAsh") },
				Health = 900f, SkillMin = 76f, SkillMax = 87f, CoinsMin = 140, CoinsMax = 200, Unarmed = 145f, Power = "GP_Queen"
			},
			new BiomeGear
			{
				Biome = Heightmap.Biome.DeepNorth,
				Weapons = new[] { "AxeGold", "BattleaxeGold", "MaceGold", "SledgeGold", "AtgeirGold", "SpearGold", "SwordGold", "THSwordGold", "KnifeGold", "FistGold", "BowGold", "CrossbowGold" },
				Ammo = new[] { "ArrowBloodGold", "BoltBloodGold" },
				Shields = new[] { "ShieldGold", "ShieldGoldBuckler" },
				Armor = new[] { new ArmorSet("ArmorDeepNorthMediumChest", "ArmorDeepNorthMediumlegs", "HelmetDNMediumHood", "CapeDeepNorth"), new ArmorSet("ArmorDeepNorthHeavyChest", "ArmorDeepNorthHeavylegs", "HelmetDNHeavy", "CapeDeepNorth") },
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
		/// Rolls a viking of the world's looks, health, skill and power for its biome
		/// </summary>
		/// <param name="index">The index of its biome</param>
		/// <returns>Its data, without its weapon slot, which RollLoadout sets</returns>
		public static VikingData RollData(int index)
		{
			BiomeGear gear = Get(index);
			VikingData data = new VikingData();
			data.Tombstone = false;
			data.Biome = index;
			data.Model = Random.Range(0, 2);
			data.SkinColor = Vector3.Lerp(PaleSkin, DarkSkin, Random.value);
			data.HairColor = HairColors[Random.Range(0, HairColors.Length)] * Random.Range(0.85f, 1.1f);
			data.Hair = "Hair" + Random.Range(1, HairStyles + 1);
			data.Beard = ((data.Model == 0 && Random.value < BeardChance) ? "Beard" + Random.Range(1, BeardStyles + 1) : "BeardNone");
			data.MaxHealth = gear.Health;
			data.Skill = Random.Range(gear.SkillMin, gear.SkillMax);
			data.Power = (string.IsNullOrEmpty(gear.Power) ? 0 : gear.Power.GetStableHashCode());
			return(data);
		}

		/// <summary>
		/// Rolls a viking of the world's armor, weapon, ammo and shield from its biome and the one before, by the chances
		/// in the config, upgraded once for each of its stars when StarsUpgradeGear is on, with each piece marked as worn
		/// </summary>
		/// <param name="index">The index of its biome</param>
		/// <param name="level">Its level, where 2 is one star</param>
		/// <param name="data">Its data, given the slot of its weapon</param>
		/// <returns>The inventory holding the gear</returns>
		public static Inventory RollLoadout(int index, int level, VikingData data)
		{
			Inventory inventory = new Inventory(ZombieViking.PrefabName, null, data.Width, data.Height);
			List<BiomeGear> pool = new List<BiomeGear>();
			pool.Add(Get(index));
			if (index > 0)
			{
				pool.Add(Get(index - 1));
			}
			bool deepNorth = (Get(index).Biome == Heightmap.Biome.DeepNorth);
			int quality = (Plugin.StarsUpgradeGear.Value ? level : 1);
			if (Roll(Plugin.ArmorChance))
			{
				List<ArmorSet> sets = new List<ArmorSet>();
				foreach (BiomeGear gear in pool)
				{
					sets.AddRange(gear.Armor);
				}
				ArmorSet set = sets[Random.Range(0, sets.Count)];
				Wear(inventory, MakeItem(set.Chest, quality, 1));
				Wear(inventory, MakeItem(set.Legs, quality, 1));
				if (set.Helmet != null && Roll(Plugin.HelmetChance))
				{
					Wear(inventory, MakeItem(set.Helmet, quality, 1));
				}
				if (set.Cape != null && Roll(Plugin.CapeChance))
				{
					Wear(inventory, MakeItem(set.Cape, quality, 1));
				}
			}
			ItemDrop.ItemData weapon = null;
			if (Roll(Plugin.WeaponChance))
			{
				List<string> weapons = new List<string>();
				foreach (BiomeGear gear in pool)
				{
					foreach (string name in gear.Weapons)
					{
						if (!(deepNorth && name == DeepNorthBanned) && IsAllowed(name))
						{
							weapons.Add(name);
						}
					}
				}
				if (weapons.Count > 0)
				{
					weapon = MakeItem(weapons[Random.Range(0, weapons.Count)], quality, 1);
				}
			}
			if (weapon != null)
			{
				Wear(weapon);
			}
			if (weapon != null && inventory.AddItem(weapon))
			{
				data.WeaponSlot = weapon.m_gridPos;
				string ammoType = weapon.m_shared.m_ammoType;
				if (!string.IsNullOrEmpty(ammoType))
				{
					List<ItemDrop.ItemData> ammo = new List<ItemDrop.ItemData>();
					foreach (BiomeGear gear in pool)
					{
						foreach (string name in gear.Ammo)
						{
							ItemDrop.ItemData candidate = MakeItem(name, 1, AmmoStack);
							if (candidate != null && candidate.m_shared.m_ammoType == ammoType && candidate.GetDamage().m_spirit <= 0f)
							{
								ammo.Add(candidate);
							}
						}
					}
					if (ammo.Count > 0)
					{
						inventory.AddItem(ammo[Random.Range(0, ammo.Count)]);
					}
				}
			}
			bool freeHand = (weapon == null || !weapon.IsTwoHanded());
			if (freeHand && Roll(Plugin.ShieldChance))
			{
				List<string> shields = new List<string>();
				foreach (BiomeGear gear in pool)
				{
					shields.AddRange(gear.Shields);
				}
				Wear(inventory, MakeItem(shields[Random.Range(0, shields.Count)], quality, 1));
			}
			return(inventory);
		}

		/// <summary>
		/// Rolls a percent chance from the config
		/// </summary>
		/// <param name="chance">The setting, as a percent</param>
		/// <returns>True when the roll succeeds</returns>
		private static bool Roll(ConfigEntry<int> chance)
		{
			return(Random.value * 100f < chance.Value);
		}

		/// <summary>
		/// Adds an item to an inventory marked as worn, so it's put on as the viking loads
		/// </summary>
		/// <param name="inventory">The inventory</param>
		/// <param name="item">The item, or null when it couldn't be made</param>
		private static void Wear(Inventory inventory, ItemDrop.ItemData item)
		{
			if (item == null)
			{
				return;
			}
			item.m_equipped = true;
			Wear(item);
			inventory.AddItem(item);
		}

		/// <summary>
		/// Wears an item down to between the config's DurabilityMin and DurabilityMax percent of its full durability
		/// </summary>
		/// <param name="item">The item</param>
		private static void Wear(ItemDrop.ItemData item)
		{
			if (item.m_shared.m_useDurability)
			{
				float min = Mathf.Min(Plugin.DurabilityMin.Value, Plugin.DurabilityMax.Value);
				float max = Mathf.Max(Plugin.DurabilityMin.Value, Plugin.DurabilityMax.Value);
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
