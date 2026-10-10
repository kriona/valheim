using System;
using System.Collections.Generic;
using UnityEngine;

namespace TweakStats
{
	/// <summary>
	/// Finds the game's prefabs and objects by the names used in the config file
	/// </summary>
	public static class Prefabs
	{
		/// <summary>
		/// Every item a player can carry - creature attacks are items too, but have no icon
		/// </summary>
		/// <returns>The items' ItemDrop components on their prefabs</returns>
		public static List<ItemDrop> Items()
		{
			List<ItemDrop> items = new List<ItemDrop>();
			if (ObjectDB.instance == null)
			{
				return(items);
			}
			foreach (GameObject prefab in ObjectDB.instance.m_items)
			{
				if (prefab == null)
				{
					continue;
				}
				ItemDrop item = prefab.GetComponent<ItemDrop>();
				if (item != null && item.m_itemData.m_shared.m_icons != null && item.m_itemData.m_shared.m_icons.Length > 0)
				{
					items.Add(item);
				}
			}
			return(items);
		}

		/// <summary>
		/// Every crafting recipe
		/// </summary>
		/// <returns>The recipes</returns>
		public static List<Recipe> Recipes()
		{
			List<Recipe> recipes = new List<Recipe>();
			if (ObjectDB.instance == null)
			{
				return(recipes);
			}
			foreach (Recipe recipe in ObjectDB.instance.m_recipes)
			{
				if (recipe != null && recipe.m_item != null)
				{
					recipes.Add(recipe);
				}
			}
			return(recipes);
		}

		/// <summary>
		/// The name the config file uses for an object
		/// </summary>
		/// <param name="unityObject">An object, or a component on a prefab</param>
		/// <returns>The prefab name, e.g. SwordIron rather than $item_sword_iron</returns>
		public static string NameOf(UnityEngine.Object unityObject)
		{
			if (unityObject is Component component)
			{
				return(component.gameObject.name);
			}
			return(unityObject.name);
		}

		/// <summary>
		/// The game's display name for an item, in the current language
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>The display name, e.g. Iron sword</returns>
		public static string DisplayName(ItemDrop item)
		{
			return(Localize(item.m_itemData.m_shared.m_name));
		}

		/// <summary>
		/// Replaces the game's $tokens in some text with their words in the current language
		/// </summary>
		/// <param name="text">The text, e.g. $item_sword_iron</param>
		/// <returns>The translated text, e.g. Iron sword</returns>
		public static string Localize(string text)
		{
			return((Localization.instance != null) ? Localization.instance.Localize(text) : text);
		}

		/// <summary>
		/// Whether the config file can name an object of this type
		/// </summary>
		/// <param name="type">The type of the stat that refers to the object</param>
		/// <returns>True for items, prefabs, status effects and crafting stations</returns>
		public static bool CanFind(Type type)
		{
			return(type == typeof(ItemDrop) || type == typeof(GameObject) || typeof(StatusEffect).IsAssignableFrom(type) || type == typeof(CraftingStation));
		}

		/// <summary>
		/// What the config file calls an object of this type, for messages
		/// </summary>
		/// <param name="type">The type of the stat that refers to the object</param>
		/// <returns>A short description, e.g. status effect</returns>
		public static string Describe(Type type)
		{
			if (type == typeof(ItemDrop))
			{
				return("item");
			}
			if (typeof(StatusEffect).IsAssignableFrom(type))
			{
				return("status effect");
			}
			if (type == typeof(CraftingStation))
			{
				return("crafting station");
			}
			return("prefab");
		}

		/// <summary>
		/// Finds an object by its name, ignoring case
		/// </summary>
		/// <param name="name">A prefab name, or for a crafting station also its game name, e.g. forge or $piece_forge</param>
		/// <param name="type">The type of the stat that will refer to the object</param>
		/// <returns>The object, or null when there isn't one with that name</returns>
		public static UnityEngine.Object Find(string name, Type type)
		{
			if (ObjectDB.instance == null)
			{
				return(null);
			}
			if (type == typeof(ItemDrop))
			{
				return(FindItem(name));
			}
			if (type == typeof(GameObject))
			{
				return(FindPrefab(name));
			}
			if (typeof(StatusEffect).IsAssignableFrom(type))
			{
				foreach (StatusEffect effect in ObjectDB.instance.m_StatusEffects)
				{
					if (effect != null && type.IsInstanceOfType(effect) && effect.name.Equals(name, StringComparison.OrdinalIgnoreCase))
					{
						return(effect);
					}
				}
				return(null);
			}
			if (type == typeof(CraftingStation))
			{
				return(FindStation(name));
			}
			return(null);
		}

		/// <summary>
		/// Finds an item prefab by name, ignoring case
		/// </summary>
		/// <param name="name">The prefab name</param>
		/// <returns>The item's ItemDrop, or null</returns>
		public static ItemDrop FindItem(string name)
		{
			GameObject prefab = ObjectDB.instance.GetItemPrefab(name);
			if (prefab != null)
			{
				return(prefab.GetComponent<ItemDrop>());
			}
			foreach (GameObject item in ObjectDB.instance.m_items)
			{
				if (item != null && item.name.Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					return(item.GetComponent<ItemDrop>());
				}
			}
			return(null);
		}

		/// <summary>
		/// Finds any networked prefab or item by name, ignoring case
		/// </summary>
		/// <param name="name">The prefab name</param>
		/// <returns>The prefab, or null</returns>
		private static GameObject FindPrefab(string name)
		{
			if (ZNetScene.instance != null)
			{
				GameObject prefab = ZNetScene.instance.GetPrefab(name);
				if (prefab != null)
				{
					return(prefab);
				}
				foreach (GameObject scenePrefab in ZNetScene.instance.m_prefabs)
				{
					if (scenePrefab != null && scenePrefab.name.Equals(name, StringComparison.OrdinalIgnoreCase))
					{
						return(scenePrefab);
					}
				}
			}
			ItemDrop item = FindItem(name);
			return((item != null) ? item.gameObject : null);
		}

		/// <summary>
		/// Finds a crafting station among the stations recipes use and the pieces the build tools place
		/// </summary>
		/// <param name="name">The station's prefab name or game name, e.g. forge, $piece_forge or piece_forge</param>
		/// <returns>The station, or null</returns>
		private static CraftingStation FindStation(string name)
		{
			string token = name.TrimStart('$');
			foreach (CraftingStation station in Stations())
			{
				if (station.gameObject.name.Equals(name, StringComparison.OrdinalIgnoreCase) || station.m_name.TrimStart('$').Equals(token, StringComparison.OrdinalIgnoreCase))
				{
					return(station);
				}
			}
			return(null);
		}

		/// <summary>
		/// Every crafting station a recipe uses or a build tool can place
		/// </summary>
		/// <returns>The stations, without repeats</returns>
		private static List<CraftingStation> Stations()
		{
			List<CraftingStation> stations = new List<CraftingStation>();
			foreach (Recipe recipe in ObjectDB.instance.m_recipes)
			{
				if (recipe != null)
				{
					AddStation(stations, recipe.m_craftingStation);
					AddStation(stations, recipe.m_repairStation);
				}
			}
			foreach (GameObject item in ObjectDB.instance.m_items)
			{
				PieceTable table = (item != null) ? item.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_buildPieces : null;
				if (table == null)
				{
					continue;
				}
				foreach (GameObject piece in table.m_pieces)
				{
					if (piece != null)
					{
						AddStation(stations, piece.GetComponent<CraftingStation>());
					}
				}
			}
			return(stations);
		}

		/// <summary>
		/// Adds a station to a list when it isn't null or already there
		/// </summary>
		/// <param name="stations">The list</param>
		/// <param name="station">The station</param>
		private static void AddStation(List<CraftingStation> stations, CraftingStation station)
		{
			if (station != null && !stations.Contains(station))
			{
				stations.Add(station);
			}
		}
	}
}
