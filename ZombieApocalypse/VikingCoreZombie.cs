using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZombieApocalypse;

namespace Kriona.Vikings
{
	/// <summary>
	/// A player dying by a zombie's hand on this client, while their OnDeath runs
	/// </summary>
	internal class PlayerDeath
	{
		public Player Player;

		/// <summary>
		/// The items the player had equipped, read before their death unequips them
		/// </summary>
		public HashSet<ItemDrop.ItemData> Equipped = new HashSet<ItemDrop.ItemData>();

		/// <summary>
		/// The weapon the player was wielding, or null
		/// </summary>
		public ItemDrop.ItemData Weapon;

		/// <summary>
		/// The tombstone the death made, or null when it made none
		/// </summary>
		public TombStone Grave;

		public VikingData Data;
	}

	/// <summary>
	/// What only a zombie viking's core does - its prefab, how a viking of the world's gear is rolled, a player's body
	/// rising as one carrying their things, its one chance at a forsaken power, and the loot it drops
	/// </summary>
	internal partial class VikingCore
	{
		/// <summary>
		/// On a viking, that it has had its one chance to use its forsaken power
		/// </summary>
		public static readonly int PowerUsedKey = "kriona.ZombieApocalypse.vikingPowerUsed".GetStableHashCode();

		/// <summary>
		/// The routed RPC that tells every client a viking has used a power, with when the cooldown ends in ticks
		/// </summary>
		public const string PowerRpc = "kriona.ZombieApocalypse.VikingPower";

		/// <summary>
		/// The prefabs of silver ore and silver ingots, which stay in the player's tombstone like spirit weapons
		/// </summary>
		private static readonly HashSet<string> SilverPrefabs = new HashSet<string> { "SilverOre", "Silver" };

		/// <summary>
		/// The most of its biome's remains a viking of the world drops, before its stars
		/// </summary>
		private const int RemainsMax = 2;

		/// <summary>
		/// When the next viking may use a forsaken power, in ZNet time ticks
		/// </summary>
		private static long powerCooldownUntil;

		/// <summary>
		/// The player dying by a zombie's hand on this client, while their OnDeath runs
		/// </summary>
		public static PlayerDeath Pending;

		/// <summary>
		/// Builds the zombie viking's prefab, in the Undead faction
		/// </summary>
		/// <param name="scene">The world's ZNetScene</param>
		/// <returns>The prefab, or null when the player's or draugr's prefab is missing</returns>
		private static partial GameObject BuildOwnPrefab(ZNetScene scene)
		{
			return(BuildPrefab<ZombieVikingBody, ZombieVikingAI>(scene, ZombiePrefabName, Character.Faction.Undead, null));
		}

		/// <summary>
		/// Checks whether a viking dying on this client will rise as a zombie, as DeathPatch has decided
		/// </summary>
		/// <param name="dying">The viking</param>
		/// <returns>True when it will</returns>
		public static partial bool Rises(Character dying)
		{
			return(Dying.Creature == dying && Dying.Rise != null);
		}

		/// <summary>
		/// Rolls a new zombie viking of the world's look, health, skill, power and gear for the biome it's in, worn and
		/// left to chance, and marks it as a zombie
		/// </summary>
		private partial void Generate()
		{
			int index = VikingGear.IndexOf(WorldGenerator.instance.GetBiome(transform.position));
			VikingData rolled = VikingGear.RollData(index);
			Inventory gear = VikingGear.RollLoadout(index, humanoid.GetLevel(), rolled, false, Arms.Any, false, false);
			ZDO zdo = SaveGenerated(rolled, gear);
			zdo.Set(ZombieState.ZombieKey, true);
		}

		/// <summary>
		/// Records a dying player's looks, health, power and equipment before their death unequips it
		/// </summary>
		/// <param name="player">The local player, dying by a zombie's hand</param>
		/// <returns>The death</returns>
		public static PlayerDeath BeginPlayerDeath(Player player)
		{
			PlayerDeath death = new PlayerDeath();
			death.Player = player;
			ItemDrop.ItemData[] worn = new ItemDrop.ItemData[] { player.m_rightItem, player.m_leftItem, player.m_chestItem, player.m_legItem, player.m_helmetItem, player.m_shoulderItem, player.m_utilityItem, player.m_trinketItem };
			foreach (ItemDrop.ItemData item in worn)
			{
				if (item != null)
				{
					death.Equipped.Add(item);
				}
			}
			ItemDrop.ItemData weapon = player.GetCurrentWeapon();
			death.Weapon = ((weapon != null && player.GetInventory().ContainsItem(weapon)) ? weapon : null);
			PlayerProfile profile = Game.instance.GetPlayerProfile();
			VikingData data = new VikingData();
			data.Name = profile.GetName();
			data.PlayerId = profile.GetPlayerID();
			data.Model = player.m_modelIndex;
			data.SkinColor = player.m_skinColor;
			data.HairColor = player.m_hairColor;
			data.Beard = player.m_beardItem ?? "";
			data.Hair = player.m_hairItem ?? "";
			data.MaxHealth = player.GetMaxHealth();
			data.Width = player.GetInventory().m_width;
			data.Height = player.GetInventory().m_height;
			data.Power = ((player.m_guardianSE != null && player.m_guardianPowerCooldown <= 0f) ? player.m_guardianSE.NameHash() : 0);
			data.Tombstone = true;
			data.Biome = VikingGear.IndexOf(WorldGenerator.instance.GetBiome(player.transform.position));
			foreach (KeyValuePair<Skills.SkillType, Skills.Skill> skill in player.GetSkills().m_skillData)
			{
				data.Skills[(int)skill.Key] = skill.Value.m_level;
			}
			death.Data = data;
			return(death);
		}

		/// <summary>
		/// Takes everything but food, silver and spirit weapons from the dead player's tombstone, copies the equipment
		/// they kept, and sets their corpse to rise as a viking carrying it - or raises the viking at once when there's
		/// no corpse
		/// </summary>
		/// <param name="death">The death, once the player's OnDeath has made their corpse and tombstone</param>
		public static void FinishPlayerDeath(PlayerDeath death)
		{
			VikingData data = death.Data;
			Inventory carried = new Inventory(ZombiePrefabName, null, data.Width, data.Height);
			Inventory grave = ((death.Grave != null) ? death.Grave.GetComponent<Container>().GetInventory() : null);
			if (grave != null)
			{
				foreach (ItemDrop.ItemData item in grave.GetAllItems().ToList())
				{
					if (StaysInGrave(item))
					{
						continue;
					}
					grave.m_inventory.Remove(item);
					item.m_equipped = death.Equipped.Contains(item);
					carried.m_inventory.Add(item);
				}
				grave.Changed();
			}
			// What's still equipped is what the world let the player keep
			foreach (ItemDrop.ItemData item in death.Player.GetInventory().GetAllItems())
			{
				if (item.m_equipped && !StaysInGrave(item))
				{
					ItemDrop.ItemData copy = item.Clone();
					copy.m_customData[KeptKey] = "1";
					copy.m_equipped = true;
					carried.m_inventory.Add(copy);
				}
			}
			if (death.Weapon != null && !StaysInGrave(death.Weapon))
			{
				data.WeaponSlot = death.Weapon.m_gridPos;
			}
			bool keepGrave = (grave != null && grave.NrOfItems() > 0);
			data.Home = Zombies.GroundUnder(keepGrave ? death.Grave.transform.position : death.Player.GetCenterPoint());
			data.HasHome = true;
			if (grave != null && !keepGrave)
			{
				ZNetScene.instance.Destroy(death.Grave.gameObject);
			}
			byte[] dataBytes = data.ToBytes();
			ZPackage pkg = new ZPackage();
			carried.Save(pkg);
			byte[] inventoryBytes = pkg.GetArray();
			Ragdoll corpse = death.Player.m_ragdoll;
			if (corpse != null && corpse.m_nview != null && corpse.m_nview.IsValid())
			{
				// The corpse holds the player's things until the viking rises, so it's kept with the world
				ZDO zdo = corpse.m_nview.GetZDO();
				zdo.Persistent = true;
				zdo.Set(DataKey, dataBytes);
				zdo.Set(InventoryKey, inventoryBytes);
				long now = NetTime.Now();
				zdo.Set(ZombieState.DiedAtKey, now);
				zdo.Set(ZombieState.RiseAtKey, now + NetTime.Ticks(Plugin.RiseDelay.Value));
				zdo.Set(ZombieState.RisePrefabKey, ZombiePrefabHash);
				if (corpse.GetComponent<RisingCorpse>() == null)
				{
					corpse.gameObject.AddComponent<RisingCorpse>();
				}
				return;
			}
			PendingRise rise = new PendingRise();
			rise.PrefabHash = ZombiePrefabHash;
			rise.Level = 1;
			Transform body = death.Player.transform;
			Quaternion facing = Quaternion.Euler(0f, body.eulerAngles.y, 0f);
			long riseNow = NetTime.Now();
			Character viking = Zombies.Raise(rise, Zombies.GroundUnder(death.Player.GetCenterPoint()), facing, riseNow, riseNow);
			if (viking == null)
			{
				SpawnGrave(death.Player.GetCenterPoint(), facing, carried, data);
				return;
			}
			HandOver(dataBytes, inventoryBytes, viking);
		}

		/// <summary>
		/// Checks whether an item stays in the player's tombstone instead of going with their viking - food, silver
		/// ore and ingots, and weapons and ammo that deal spirit damage, which the undead can't bear
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it stays</returns>
		private static bool StaysInGrave(ItemDrop.ItemData item)
		{
			ItemDrop.ItemData.SharedData shared = item.m_shared;
			if (shared.m_food > 0f)
			{
				return(true);
			}
			if (item.m_dropPrefab != null && SilverPrefabs.Contains(item.m_dropPrefab.name))
			{
				return(true);
			}
			bool armament = (item.IsWeapon() || shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo || shared.m_itemType == ItemDrop.ItemData.ItemType.AmmoNonEquipable);
			return(armament && item.GetDamage().m_spirit > 0f);
		}

		/// <summary>
		/// Notes that a viking somewhere has used a forsaken power, so none uses one until the cooldown ends
		/// </summary>
		/// <param name="sender">The client that sent it</param>
		/// <param name="until">When the cooldown ends, in ZNet time ticks</param>
		public static void OnPowerUsed(long sender, long until)
		{
			if (until > powerCooldownUntil)
			{
				powerCooldownUntil = until;
			}
		}

		/// <summary>
		/// Forgets the forsaken power cooldown when leaving a world, since it's in that world's time
		/// </summary>
		public static void ResetPowerCooldown()
		{
			powerCooldownUntil = 0L;
		}

		/// <summary>
		/// Gives a zombie viking its one chance to use its forsaken power once it's standing - a tombstone viking the
		/// first time a player comes near, a viking of the world the first time it spots an enemy - once no other viking
		/// has used one within the config's PowerCooldown, waiting for that to pass before it takes its chance
		/// </summary>
		partial void UpdatePower()
		{
			ZDO zdo = nview.GetZDO();
			if (data.Power == 0 || zdo.GetBool(PowerUsedKey) || ZombieState.IsRising(humanoid))
			{
				return;
			}
			float chance;
			if (data.Tombstone)
			{
				if (!Player.IsPlayerInRange(transform.position, PowerRange))
				{
					return;
				}
				chance = Plugin.TombstonePowerChance.Value;
			}
			else
			{
				if (ai == null || ai.m_targetCreature == null)
				{
					return;
				}
				chance = Plugin.WorldPowerChance.Value;
			}
			long now = NetTime.Now();
			if (now < powerCooldownUntil)
			{
				return;
			}
			zdo.Set(PowerUsedKey, true);
			if (Random.value * 100f >= chance)
			{
				return;
			}
			powerCooldownUntil = now + NetTime.Ticks(Plugin.PowerCooldown.Value);
			if (ZRoutedRpc.instance != null)
			{
				ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, PowerRpc, powerCooldownUntil);
			}
			StartCoroutine(UsePower(data.Power, false));
		}

		/// <summary>
		/// Drops a zombie viking of the world's coins and remains, more for each star, and each piece of its gear by
		/// the config's GearDropChance, up to its GearDropMax, as it is
		/// </summary>
		partial void DropLoot()
		{
			Vector3 position = humanoid.GetCenterPoint();
			BiomeGear gear = VikingGear.Get(BiomeIndex());
			int level = humanoid.GetLevel();
			List<KeyValuePair<GameObject, int>> loot = new List<KeyValuePair<GameObject, int>>();
			AddLoot(loot, "Coins", Random.Range(gear.CoinsMin, gear.CoinsMax + 1) * level);
			AddLoot(loot, gear.Remains, Random.Range(0, RemainsMax + 1) * level);
			if (loot.Count > 0)
			{
				CharacterDrop.DropItems(loot, position, 0.5f, nview.GetZDO().IsCheated());
			}
			List<ItemDrop.ItemData> pieces = new List<ItemDrop.ItemData>();
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
				if (type != ItemDrop.ItemData.ItemType.Ammo && type != ItemDrop.ItemData.ItemType.AmmoNonEquipable)
				{
					pieces.Add(item);
				}
			}
			int dropped = 0;
			while (pieces.Count > 0 && dropped < Plugin.GearDropMax.Value)
			{
				int index = Random.Range(0, pieces.Count);
				ItemDrop.ItemData piece = pieces[index];
				pieces.RemoveAt(index);
				if (Random.value * 100f >= Plugin.GearDropChance.Value)
				{
					continue;
				}
				piece.m_equipped = false;
				ItemDrop.DropItem(piece, piece.m_stack, position + Random.insideUnitSphere * 0.5f, Quaternion.identity);
				dropped++;
			}
		}

		/// <summary>
		/// Adds an item to a list of loot, unless there's none of it or the game has no such item
		/// </summary>
		/// <param name="loot">The loot, as each item's prefab and amount</param>
		/// <param name="prefabName">The item's prefab name, or null for none</param>
		/// <param name="amount">How many to drop</param>
		private static void AddLoot(List<KeyValuePair<GameObject, int>> loot, string prefabName, int amount)
		{
			GameObject prefab = (string.IsNullOrEmpty(prefabName) ? null : ObjectDB.instance.GetItemPrefab(prefabName));
			if (amount > 0 && prefab != null)
			{
				loot.Add(new KeyValuePair<GameObject, int>(prefab, amount));
			}
		}
	}
}
