using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// The ZDO keys a loot list is stored under - a count, then a prefab hash and an amount for each entry
	/// </summary>
	internal class LootKeys
	{
		public readonly int CountKey;
		public readonly string HashPrefix;
		public readonly string AmountPrefix;

		/// <summary>
		/// Names the keys of a loot list
		/// </summary>
		/// <param name="countKey">The key holding the number of entries</param>
		/// <param name="hashPrefix">The name each entry's prefab hash key starts with, followed by its index</param>
		/// <param name="amountPrefix">The name each entry's amount key starts with, followed by its index</param>
		public LootKeys(int countKey, string hashPrefix, string amountPrefix)
		{
			CountKey = countKey;
			HashPrefix = hashPrefix;
			AmountPrefix = amountPrefix;
		}
	}

	/// <summary>
	/// Carries a creature's loot from its first death to its zombie's death
	/// </summary>
	/// <remarks>
	/// The loot is rolled when the creature first dies and kept in its corpse's ZDO, where the game keeps it too. When
	/// the corpse rises it moves to the zombie's ZDO, and when the zombie dies it moves to the zombie's corpse, which
	/// drops it when the game removes the corpse - or it's dropped on the spot when the zombie leaves no corpse
	/// </remarks>
	internal static class Loot
	{
		/// <summary>
		/// The keys the game stores a corpse's loot under, so Ragdoll drops it when the corpse is removed
		/// </summary>
		public static readonly LootKeys CorpseKeys = new LootKeys(ZDOVars.s_drops, "drop_hash", "drop_amount");

		/// <summary>
		/// The keys a zombie carries its creature's loot under
		/// </summary>
		public static readonly LootKeys ZombieKeys = new LootKeys("kriona.ZombieApocalypse.loot".GetStableHashCode(), "kriona.ZombieApocalypse.lootHash", "kriona.ZombieApocalypse.lootAmount");

		/// <summary>
		/// Whether a zombie's loot came from a creature killed while cheating, so it's marked as cheated when dropped
		/// </summary>
		public static readonly int CheatedKey = "kriona.ZombieApocalypse.lootCheated".GetStableHashCode();

		/// <summary>
		/// Reads a loot list from a ZDO
		/// </summary>
		/// <param name="zdo">The ZDO holding the list</param>
		/// <param name="keys">The keys it's stored under</param>
		/// <returns>Each item prefab with its amount, leaving out prefabs that no longer exist</returns>
		public static List<KeyValuePair<GameObject, int>> Read(ZDO zdo, LootKeys keys)
		{
			List<KeyValuePair<GameObject, int>> loot = new List<KeyValuePair<GameObject, int>>();
			int count = zdo.GetInt(keys.CountKey);
			for (int i = 0; i < count; i++)
			{
				GameObject prefab = ZNetScene.instance.GetPrefab(zdo.GetInt(keys.HashPrefix + i));
				if (prefab != null)
				{
					loot.Add(new KeyValuePair<GameObject, int>(prefab, zdo.GetInt(keys.AmountPrefix + i)));
				}
			}
			return(loot);
		}

		/// <summary>
		/// Writes a loot list to a ZDO, replacing any list already under those keys
		/// </summary>
		/// <param name="zdo">The ZDO to hold the list</param>
		/// <param name="keys">The keys to store it under</param>
		/// <param name="loot">Each item prefab with its amount</param>
		public static void Write(ZDO zdo, LootKeys keys, List<KeyValuePair<GameObject, int>> loot)
		{
			zdo.Set(keys.CountKey, loot.Count);
			for (int i = 0; i < loot.Count; i++)
			{
				zdo.Set(keys.HashPrefix + i, ZNetScene.instance.GetPrefabHash(loot[i].Key));
				zdo.Set(keys.AmountPrefix + i, loot[i].Value);
			}
		}

		/// <summary>
		/// Empties a loot list in a ZDO
		/// </summary>
		/// <param name="zdo">The ZDO holding the list</param>
		/// <param name="keys">The keys it's stored under</param>
		public static void Clear(ZDO zdo, LootKeys keys)
		{
			zdo.Set(keys.CountKey, 0);
		}
	}
}
