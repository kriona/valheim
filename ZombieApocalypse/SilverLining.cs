using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Lines weapons and shields with silver at a forge, giving weapons a little spirit damage so the creatures they
	/// kill stay dead, and shields a little spirit damage against the undead that strike them
	/// </summary>
	/// <remarks>
	/// The lining is a flag in the item's custom data, which the game saves and sends with the item wherever it goes,
	/// so it survives other worlds, chests, trading, upgrades and repairs. Each weapon or shield prefab gets a lining
	/// recipe of its own, offered on the forge's Upgrade tab, and the recipe is always read at quality 1 so its cost
	/// and forge level don't grow with the item's level
	/// </remarks>
	internal static class SilverLining
	{
		/// <summary>
		/// In an item's custom data, marks it as silver-lined
		/// </summary>
		public const string LinedKey = "kriona.ZombieApocalypse.silverLined";

		/// <summary>
		/// The forge level lining needs, the same as a silver sword's
		/// </summary>
		public const int ForgeLevel = 3;

		/// <summary>
		/// The spirit damage lining adds, as a share of the weapon's damage at level 1, and the least it adds
		/// </summary>
		private const float SpiritShare = 0.05f;
		private const float MinSpirit = 5f;

		/// <summary>
		/// The spirit damage a lined shield deals to an undead attacker whose blow it holds, as a share of its block
		/// armor at level 1 (at least MinSpirit), and how much more a parry that staggers the attacker deals - a
		/// critical hit, as the game deals to a staggered creature
		/// </summary>
		private const float ShieldShare = 0.2f;
		private const float CriticalMultiplier = 2f;

		/// <summary>
		/// Lining costs a quarter of the metal in the item's recipe, at least 1, in silver, and 1 ectoplasm
		/// </summary>
		private const int MetalDivisor = 4;
		private const int EctoplasmCost = 1;

		private const string ForgePrefab = "forge";
		private const string SilverPrefab = "Silver";
		private const string EctoplasmPrefab = "Ectoplasm";

		/// <summary>
		/// The metals counted in an item's recipe
		/// </summary>
		private static readonly HashSet<string> Metals = new HashSet<string>
		{
			"Copper", "Tin", "Bronze", "Iron", "Silver", "BlackMetal", "Flametal", "FlametalNew"
		};

		/// <summary>
		/// The lining recipe for each weapon prefab, or null for one that can't be lined
		/// </summary>
		private static readonly Dictionary<string, Recipe> recipes = new Dictionary<string, Recipe>();

		/// <summary>
		/// Checks whether an item is silver-lined
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it is</returns>
		public static bool IsLined(ItemDrop.ItemData item)
		{
			return(item != null && item.m_customData.ContainsKey(LinedKey));
		}

		/// <summary>
		/// Marks an item as silver-lined
		/// </summary>
		/// <param name="item">The item</param>
		public static void Line(ItemDrop.ItemData item)
		{
			item.m_customData[LinedKey] = "1";
		}

		/// <summary>
		/// Works out the spirit damage a silver-lined weapon deals
		/// </summary>
		/// <param name="item">The weapon</param>
		/// <returns>A share of its combat damage at level 1, at least MinSpirit</returns>
		public static float SpiritDamage(ItemDrop.ItemData item)
		{
			HitData.DamageTypes damages = item.m_shared.m_damages;
			float total = damages.m_damage + damages.GetTotalPhysicalDamage() + damages.GetTotalElementalDamage() + damages.m_poison;
			return(Mathf.Max(MinSpirit, Mathf.Round(total * SpiritShare)));
		}

		/// <summary>
		/// Checks whether an item is a shield
		/// </summary>
		/// <param name="item">The item, or null</param>
		/// <returns>True for a shield</returns>
		public static bool IsShield(ItemDrop.ItemData item)
		{
			return(item != null && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield);
		}

		/// <summary>
		/// Works out the spirit damage a silver-lined shield deals to an undead attacker whose blow it holds
		/// </summary>
		/// <param name="item">The shield</param>
		/// <param name="critical">True for a parry that staggers the attacker</param>
		/// <returns>The damage (see ShieldSpirit(float, bool))</returns>
		public static float ShieldSpirit(ItemDrop.ItemData item, bool critical)
		{
			return(ShieldSpirit(item.GetBaseBlockPower(1), critical));
		}

		/// <summary>
		/// Works out the spirit damage a silver-lined shield deals from its block armor
		/// </summary>
		/// <param name="blockPower">The shield's block armor at level 1</param>
		/// <param name="critical">True for a parry that staggers the attacker</param>
		/// <returns>A share of the block armor, at least MinSpirit, multiplied for a critical hit</returns>
		internal static float ShieldSpirit(float blockPower, bool critical)
		{
			float spirit = Mathf.Max(MinSpirit, Mathf.Round(blockPower * ShieldShare));
			return(critical ? spirit * CriticalMultiplier : spirit);
		}

		/// <summary>
		/// Checks whether a crafting station is a forge
		/// </summary>
		/// <param name="station">The station, or null</param>
		/// <returns>True for a forge</returns>
		public static bool IsForge(CraftingStation station)
		{
			CraftingStation forge = FindForge();
			return(station != null && forge != null && station.m_name == forge.m_name);
		}

		/// <summary>
		/// Checks whether a recipe is one of the lining recipes
		/// </summary>
		/// <param name="recipe">The recipe, or null</param>
		/// <returns>True for a lining recipe</returns>
		public static bool IsLiningRecipe(Recipe recipe)
		{
			return(recipe != null && recipe.name.StartsWith("Recipe_SilverLine_"));
		}

		/// <summary>
		/// Gets the lining recipe for an item, making it the first time
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>The recipe, or null when the item can't be lined - it's lined already, isn't a weapon or shield,
		/// already deals spirit damage, or is a weapon with no metal in its recipe</returns>
		public static Recipe GetRecipe(ItemDrop.ItemData item)
		{
			if (item == null || IsLined(item) || !(item.IsWeapon() || IsShield(item)) || item.m_dropPrefab == null)
			{
				return(null);
			}
			if (item.m_shared.m_damages.m_spirit > 0f || item.m_shared.m_damagesPerLevel.m_spirit > 0f)
			{
				return(null);
			}
			string prefabName = item.m_dropPrefab.name;
			if (recipes.TryGetValue(prefabName, out Recipe recipe) && (recipe == null || (recipe.m_item != null && recipe.m_craftingStation != null)))
			{
				return(recipe);
			}
			recipe = MakeRecipe(item);
			recipes[prefabName] = recipe;
			return(recipe);
		}

		/// <summary>
		/// Makes the lining recipe for an item's prefab
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>The recipe, or null when the item is a weapon whose recipe has no metal or the forge, silver or
		/// ectoplasm can't be found</returns>
		private static Recipe MakeRecipe(ItemDrop.ItemData item)
		{
			ObjectDB objectDB = ObjectDB.instance;
			CraftingStation forge = FindForge();
			ItemDrop itemDrop = item.m_dropPrefab.GetComponent<ItemDrop>();
			ItemDrop silver = FindItem(SilverPrefab);
			ItemDrop ectoplasm = FindItem(EctoplasmPrefab);
			Recipe original = ((objectDB != null) ? objectDB.GetRecipe(item) : null);
			if (original == null || forge == null || itemDrop == null || silver == null || ectoplasm == null)
			{
				return(null);
			}
			int metal = 0;
			foreach (Piece.Requirement requirement in original.m_resources)
			{
				if (requirement.m_resItem != null && Metals.Contains(requirement.m_resItem.name))
				{
					metal += requirement.m_amount;
				}
			}
			if (metal <= 0 && !IsShield(item))
			{
				return(null);
			}
			Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
			recipe.name = "Recipe_SilverLine_" + item.m_dropPrefab.name;
			recipe.hideFlags = HideFlags.HideAndDontSave;
			recipe.m_item = itemDrop;
			recipe.m_amount = 1;
			recipe.m_enabled = true;
			recipe.m_craftingStation = forge;
			recipe.m_minStationLevel = ForgeLevel;
			recipe.m_resources = new Piece.Requirement[]
			{
				new Piece.Requirement { m_resItem = silver, m_amount = Mathf.Max(1, metal / MetalDivisor), m_amountPerLevel = 0, m_recover = false },
				new Piece.Requirement { m_resItem = ectoplasm, m_amount = EctoplasmCost, m_amountPerLevel = 0, m_recover = false }
			};
			return(recipe);
		}

		/// <summary>
		/// Finds the forge's crafting station
		/// </summary>
		/// <returns>The station, or null when it isn't loaded</returns>
		private static CraftingStation FindForge()
		{
			GameObject prefab = GamePrefabs.Find(ForgePrefab);
			return((prefab != null) ? prefab.GetComponent<CraftingStation>() : null);
		}

		/// <summary>
		/// Finds an item's prefab by name
		/// </summary>
		/// <param name="name">The prefab's name</param>
		/// <returns>The item, or null when it isn't loaded</returns>
		private static ItemDrop FindItem(string name)
		{
			GameObject prefab = ((ObjectDB.instance != null) ? ObjectDB.instance.GetItemPrefab(name) : null);
			return((prefab != null) ? prefab.GetComponent<ItemDrop>() : null);
		}
	}
}
