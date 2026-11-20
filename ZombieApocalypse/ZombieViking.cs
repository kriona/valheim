using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Who a zombie viking was and how it looks and fights - for a tombstone viking, handed from the player's death to
	/// their corpse and on to the viking, and for a viking of the world, rolled when it first loads
	/// </summary>
	internal class VikingData
	{
		private const int Version = 3;

		public string Name = "";
		public long PlayerId;
		public int Model;
		public Vector3 SkinColor = Vector3.one;
		public Vector3 HairColor = Vector3.one;
		public string Beard = "";
		public string Hair = "";
		public float MaxHealth;
		public int Width = Humanoid.DefaultInventoryWidth;
		public int Height = Humanoid.DefaultInventoryHeight;

		/// <summary>
		/// Where the weapon the player was wielding sits in the viking's inventory, or -1, -1 when they had none
		/// </summary>
		public Vector2i WeaponSlot = new Vector2i(-1, -1);

		/// <summary>
		/// The name hash of the forsaken power the player could use when they died, or 0
		/// </summary>
		public int Power;

		/// <summary>
		/// The ground under the player's tombstone, or where they fell when it left none, which the viking guards
		/// </summary>
		public Vector3 Home;

		/// <summary>
		/// Whether Home is set, which it isn't in data written before vikings had a home
		/// </summary>
		public bool HasHome;

		/// <summary>
		/// Whether the viking is a player's, risen by their tombstone, rather than one of the world's
		/// </summary>
		public bool Tombstone = true;

		/// <summary>
		/// The index in VikingGear's tables of the biome the viking's gear, coins and fists come from
		/// </summary>
		public int Biome = -1;

		/// <summary>
		/// A viking of the world's level in every skill, from 0 to 100
		/// </summary>
		public float Skill;

		/// <summary>
		/// A tombstone viking's level in each of the player's skills when they died, by skill type
		/// </summary>
		public Dictionary<int, float> Skills = new Dictionary<int, float>();

		/// <summary>
		/// Writes the data to bytes for a ZDO
		/// </summary>
		/// <returns>The bytes</returns>
		public byte[] ToBytes()
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(Version);
			pkg.Write(Name);
			pkg.Write(PlayerId);
			pkg.Write(Model);
			pkg.Write(SkinColor);
			pkg.Write(HairColor);
			pkg.Write(Beard);
			pkg.Write(Hair);
			pkg.Write(MaxHealth);
			pkg.Write(Width);
			pkg.Write(Height);
			pkg.Write(WeaponSlot);
			pkg.Write(Power);
			pkg.Write(Home);
			pkg.Write(Tombstone);
			pkg.Write(Biome);
			pkg.Write(Skill);
			pkg.Write(Skills.Count);
			foreach (KeyValuePair<int, float> skill in Skills)
			{
				pkg.Write(skill.Key);
				pkg.Write(skill.Value);
			}
			return(pkg.GetArray());
		}

		/// <summary>
		/// Reads data written by ToBytes
		/// </summary>
		/// <param name="bytes">The bytes, or null</param>
		/// <returns>The data, or null when there were no bytes</returns>
		public static VikingData FromBytes(byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0)
			{
				return(null);
			}
			ZPackage pkg = new ZPackage(bytes);
			int version = pkg.ReadInt();
			VikingData data = new VikingData();
			data.Name = pkg.ReadString();
			data.PlayerId = pkg.ReadLong();
			data.Model = pkg.ReadInt();
			data.SkinColor = pkg.ReadVector3();
			data.HairColor = pkg.ReadVector3();
			data.Beard = pkg.ReadString();
			data.Hair = pkg.ReadString();
			data.MaxHealth = pkg.ReadSingle();
			data.Width = pkg.ReadInt();
			data.Height = pkg.ReadInt();
			data.WeaponSlot = pkg.ReadVector2i();
			data.Power = pkg.ReadInt();
			if (version >= 2)
			{
				data.Home = pkg.ReadVector3();
				data.HasHome = true;
			}
			if (version >= 3)
			{
				data.Tombstone = pkg.ReadBool();
				data.Biome = pkg.ReadInt();
				data.Skill = pkg.ReadSingle();
				int count = pkg.ReadInt();
				for (int i = 0; i < count; i++)
				{
					int skill = pkg.ReadInt();
					data.Skills[skill] = pkg.ReadSingle();
				}
			}
			return(data);
		}
	}

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
	/// A zombie viking - a player killed by a zombie, risen from their corpse with their looks, health, armor and things,
	/// or one of the world's, with a random look and gear from VikingGear
	/// </summary>
	/// <remarks>
	/// The viking is its own prefab, built from the player's prefab with the player's parts swapped for a monster's and
	/// the draugr's AI. A tombstone viking carries everything the player had but food and spirit weapons, and keeps it in
	/// its ZDO as it's used up, so it never despawns or loses it. It drops it in a tombstone of the player's when it's
	/// killed. Equipment the world let the player keep is copied to it marked as kept, so it fights with it but never
	/// drops it. A viking of the world rolls its gear the first time it loads, and drops coins and maybe some of its gear.
	/// Each wields its chosen weapon, or else its best one, and switches when that breaks or runs out of ammo. Its armor
	/// protects it and wears down, and it raises its shield against attacks, better the more skilled it is
	/// </remarks>
	internal class ZombieViking : MonoBehaviour
	{
		public const string PrefabName = "ZombieViking";
		public static readonly int PrefabHash = PrefabName.GetStableHashCode();

		/// <summary>
		/// On a viking and the corpse it rises from, its VikingData
		/// </summary>
		public static readonly int DataKey = "kriona.ZombieApocalypse.vikingData".GetStableHashCode();

		/// <summary>
		/// On a viking and the corpse it rises from, its inventory as saved by Inventory.Save
		/// </summary>
		public static readonly int InventoryKey = "kriona.ZombieApocalypse.vikingInventory".GetStableHashCode();

		/// <summary>
		/// On a viking, that it has had its one chance to use its forsaken power
		/// </summary>
		public static readonly int PowerUsedKey = "kriona.ZombieApocalypse.vikingPowerUsed".GetStableHashCode();

		/// <summary>
		/// Custom data on an item the player kept when they died, which the viking uses but never drops
		/// </summary>
		private const string KeptKey = "kriona.ZombieApocalypse.kept";

		/// <summary>
		/// How far from its home the viking wanders while it has nothing to fight
		/// </summary>
		private const float GuardRange = 3f;

		/// <summary>
		/// How far from its home the viking can be with nothing to fight before it walks straight back
		/// </summary>
		private const float HomeRange = 6f;

		/// <summary>
		/// Seconds the viking stands still between strolls around its home, and how near a stroll's end it stops
		/// </summary>
		private const float StrollIntervalMin = 15f;
		private const float StrollIntervalMax = 40f;
		private const float StrollReach = 0.5f;

		/// <summary>
		/// How far from its home the viking goes after an enemy - it ignores enemies farther away, and when it ends up
		/// farther itself, it gives up the fight and runs home
		/// </summary>
		private const float LeashRange = 20f;

		/// <summary>
		/// How far from its home the viking chases an enemy that has hurt it, so it can't be shot from beyond its leash
		/// without answering - past this it runs home whatever happens
		/// </summary>
		private const float GrudgeRange = 100f;

		/// <summary>
		/// Seconds after an enemy last hurt it that the viking keeps chasing them beyond its leash
		/// </summary>
		private const float GrudgeTime = 15f;

		/// <summary>
		/// Seconds the viking tries to run home before it's put there, when it's stuck
		/// </summary>
		private const float ReturnTimeout = 45f;

		/// <summary>
		/// The viking's running speed - a draugr's, slower than a running player
		/// </summary>
		private const float RunSpeed = 5f;

		private const float SyncInterval = 1f;

		/// <summary>
		/// A night viking's running speed and how much faster it walks, and so how much faster than a viking of the day
		/// </summary>
		private const float NightRunSpeed = 7f;
		private const float NightSpeedScale = 1.3f;

		/// <summary>
		/// How near a player comes before a tombstone viking may use its forsaken power, which reaches as far as a
		/// player's does
		/// </summary>
		private const float PowerRange = 10f;

		/// <summary>
		/// The chance a tombstone viking uses its power when a player first comes near, and a viking of the world when it
		/// first spots an enemy
		/// </summary>
		private const float TombstonePowerChance = 0.5f;
		private const float WorldPowerChance = 0.1f;

		/// <summary>
		/// Seconds after any viking uses a forsaken power before another may
		/// </summary>
		private const float PowerCooldown = 30f;

		/// <summary>
		/// The routed RPC that tells every client a viking has used a power, with when the cooldown ends in ticks
		/// </summary>
		public const string PowerRpc = "kriona.ZombieApocalypse.VikingPower";

		/// <summary>
		/// When the next viking may use a forsaken power, in ZNet time ticks
		/// </summary>
		private static long powerCooldownUntil;


		/// <summary>
		/// The most of its biome's remains a viking of the world drops, before its stars
		/// </summary>
		private const int RemainsMax = 2;

		/// <summary>
		/// How the viking raises its shield - how near an enemy's melee attack starts for it to react, how likely it is
		/// to react and how slowly, from no skill to full skill, and how long it holds the shield up
		/// </summary>
		private const float BlockMeleeRange = 5f;
		private const float BlockRangedRange = 30f;
		private const float BlockChanceMin = 0.2f;
		private const float BlockChanceMax = 0.75f;
		private const float BlockDelayMin = 0.45f;
		private const float BlockDelayMax = 0.12f;
		private const float BlockDelayJitter = 0.12f;
		private const float BlockHoldMin = 0.6f;
		private const float BlockHoldMax = 1.1f;

		/// <summary>
		/// Seconds into the forsaken power animation the power takes hold
		/// </summary>
		private const float PowerDelay = 1f;

		/// <summary>
		/// How the AI uses a bow, crossbow or other ranged weapon, since the player's weapons are set up for melee range
		/// </summary>
		private const float RangedAttackRange = 20f;
		private const float RangedAttackAngle = 10f;
		private const float RangedAttackInterval = 2.5f;
		private const float ReloadAttackInterval = 4f;

		/// <summary>
		/// The player dying by a zombie's hand on this client, while their OnDeath runs
		/// </summary>
		public static PlayerDeath Pending;

		private static GameObject prefabHolder;
		private static GameObject prefab;

		private Humanoid humanoid;
		private ZNetView nview;
		private MonsterAI ai;
		private VikingData data;

		/// <summary>
		/// The spot the viking guards, once it's loaded
		/// </summary>
		private Vector3 home;
		private bool hasHome;

		/// <summary>
		/// When the viking started running home after going too far, or a negative number while it isn't
		/// </summary>
		private float returningSince = -1f;

		/// <summary>
		/// Whether the viking is running home from beyond GrudgeRange, which nothing interrupts, rather than beyond its
		/// leash, which being hurt again does
		/// </summary>
		private bool returningFromGrudge;

		/// <summary>
		/// The enemy that last hurt the viking, and until when it chases them beyond its leash
		/// </summary>
		private Character grudge;
		private float grudgeUntil;

		/// <summary>
		/// Where the viking is strolling to, and when it next strolls, or 0 before its first wait has started
		/// </summary>
		private Vector3 strollTarget;
		private bool hasStrollTarget;
		private float nextStroll;
		private bool loaded;
		private bool wasOwner;
		private byte[] savedInventory;
		private float nextSync;

		/// <summary>
		/// The shield the player had equipped, worn alongside a one-handed weapon
		/// </summary>
		private ItemDrop.ItemData shield;

		/// <summary>
		/// A ranged weapon's shared data while TuneWeaponAI has changed it, and its own values to put back
		/// </summary>
		private ItemDrop.ItemData.SharedData tunedShared;
		private float tunedRange;
		private float tunedRangeMin;
		private float tunedAngle;
		private float tunedInterval;

		/// <summary>
		/// The prefab's walking pace, which a night viking's is scaled from
		/// </summary>
		private float baseSpeed;

		/// <summary>
		/// When the viking raises its shield against the attack it saw coming, and until when it holds it up, or negative
		/// numbers while it isn't
		/// </summary>
		private float blockAt = -1f;
		private float blockUntil = -1f;

		/// <summary>
		/// Whether the viking's enemy was attacking last frame, so a new attack is reacted to once
		/// </summary>
		private bool enemyWasAttacking;

		/// <summary>
		/// Checks whether a creature is a zombie viking
		/// </summary>
		/// <param name="character">The creature</param>
		/// <returns>True when it is</returns>
		public static bool IsViking(Character character)
		{
			ZNetView view = character.m_nview;
			return(view != null && view.IsValid() && view.GetZDO().GetPrefab() == PrefabHash);
		}

		/// <summary>
		/// Adds the viking prefab to a world's prefabs, building it the first time
		/// </summary>
		/// <param name="scene">The world's ZNetScene, as it wakes</param>
		public static void Register(ZNetScene scene)
		{
			if (scene.m_namedPrefabs.ContainsKey(PrefabHash))
			{
				return;
			}
			GameObject built = BuildPrefab(scene);
			if (built == null)
			{
				Plugin.Log.LogWarning("Couldn't build the zombie viking - the Player or Draugr prefab is missing");
				return;
			}
			scene.m_prefabs.Add(built);
			scene.m_namedPrefabs.Add(PrefabHash, built);
		}

		/// <summary>
		/// Builds the viking prefab from the player's prefab, swapping the player's parts for a monster's with the
		/// draugr's AI
		/// </summary>
		/// <remarks>
		/// The copy sits under an inactive object, so none of its parts wake while it's changed, and it's kept across
		/// worlds
		/// </remarks>
		/// <param name="scene">The world's ZNetScene</param>
		/// <returns>The prefab, or null when the player's or draugr's prefab is missing</returns>
		private static GameObject BuildPrefab(ZNetScene scene)
		{
			if (prefab != null)
			{
				return(prefab);
			}
			GameObject playerPrefab = scene.GetPrefab("Player");
			GameObject draugrPrefab = scene.GetPrefab("Draugr");
			if (playerPrefab == null || draugrPrefab == null || draugrPrefab.GetComponent<MonsterAI>() == null)
			{
				return(null);
			}
			if (prefabHolder == null)
			{
				prefabHolder = new GameObject("ZombieApocalypse prefabs");
				prefabHolder.SetActive(false);
				Object.DontDestroyOnLoad(prefabHolder);
			}
			GameObject viking = Object.Instantiate(playerPrefab, prefabHolder.transform, false);
			viking.name = PrefabName;
			Player player = viking.GetComponent<Player>();
			Humanoid body = viking.AddComponent<Humanoid>();
			CopyFields(typeof(Character), player, body);
			CopyFields(typeof(Humanoid), player, body);
			Object.DestroyImmediate(player);
			RemoveComponent<PlayerController>(viking);
			RemoveComponent<Talker>(viking);
			RemoveComponent<Skills>(viking);
			body.m_name = "Viking";
			body.m_faction = Character.Faction.Undead;
			body.m_runSpeed = RunSpeed;
			body.m_defaultItems = new GameObject[0];
			// A player's death shakes their own controller, which a viking's shouldn't
			EffectList.EffectData[] deathEffects = body.m_deathEffects.m_effectPrefabs;
			body.m_deathEffects = new EffectList();
			body.m_deathEffects.m_effectPrefabs = deathEffects.Where((EffectList.EffectData effect) => effect.m_prefab == null || !effect.m_prefab.name.Contains("vibration")).ToArray();
			ZNetView view = viking.GetComponent<ZNetView>();
			view.m_persistent = true;
			view.m_type = ZDO.ObjectType.Default;
			MonsterAI ai = viking.AddComponent<MonsterAI>();
			MonsterAI draugrAI = draugrPrefab.GetComponent<MonsterAI>();
			CopyFields(typeof(BaseAI), draugrAI, ai);
			CopyFields(typeof(MonsterAI), draugrAI, ai);
			ai.m_pathAgentType = Pathfinding.AgentType.Humanoid;
			ai.m_avoidWater = false;
			ai.m_randomMoveRange = GuardRange;
			ai.m_enableHuntPlayer = false;
			viking.AddComponent<ZombieViking>();
			prefab = viking;
			return(prefab);
		}

		/// <summary>
		/// Copies the fields one type declares from one component to another
		/// </summary>
		/// <param name="type">The type whose own fields are copied</param>
		/// <param name="from">The component copied from</param>
		/// <param name="to">The component copied to</param>
		private static void CopyFields(System.Type type, object from, object to)
		{
			foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
			{
				if (!field.IsInitOnly && !field.IsLiteral)
				{
					field.SetValue(to, field.GetValue(from));
				}
			}
		}

		/// <summary>
		/// Removes a component from an object when it has one
		/// </summary>
		/// <typeparam name="T">The component's type</typeparam>
		/// <param name="target">The object</param>
		private static void RemoveComponent<T>(GameObject target) where T : Component
		{
			T component = target.GetComponent<T>();
			if (component != null)
			{
				Object.DestroyImmediate(component);
			}
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
		/// Takes everything but food and spirit weapons from the dead player's tombstone, copies the equipment they
		/// kept, and sets their corpse to rise as a viking carrying it - or raises the viking at once when there's no
		/// corpse
		/// </summary>
		/// <param name="death">The death, once the player's OnDeath has made their corpse and tombstone</param>
		public static void FinishPlayerDeath(PlayerDeath death)
		{
			VikingData data = death.Data;
			Inventory carried = new Inventory(PrefabName, null, data.Width, data.Height);
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
				long now = Zombies.Now();
				zdo.Set(Zombies.DiedAtKey, now);
				zdo.Set(Zombies.RiseAtKey, now + Zombies.Ticks(Plugin.RiseDelay.Value));
				zdo.Set(Zombies.RisePrefabKey, PrefabHash);
				if (corpse.GetComponent<RisingCorpse>() == null)
				{
					corpse.gameObject.AddComponent<RisingCorpse>();
				}
				return;
			}
			PendingRise rise = new PendingRise();
			rise.PrefabHash = PrefabHash;
			rise.Level = 1;
			Transform body = death.Player.transform;
			Quaternion facing = Quaternion.Euler(0f, body.eulerAngles.y, 0f);
			long riseNow = Zombies.Now();
			Character viking = Zombies.Raise(rise, Zombies.GroundUnder(death.Player.GetCenterPoint()), facing, riseNow, riseNow);
			if (viking == null)
			{
				SpawnGrave(death.Player.GetCenterPoint(), facing, carried, data);
				return;
			}
			HandOver(dataBytes, inventoryBytes, viking);
		}

		/// <summary>
		/// Checks whether an item stays in the player's tombstone instead of going with their viking - food, and
		/// weapons and ammo that deal spirit damage, which the undead can't bear
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
			bool armament = (item.IsWeapon() || shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo || shared.m_itemType == ItemDrop.ItemData.ItemType.AmmoNonEquipable);
			return(armament && item.GetDamage().m_spirit > 0f);
		}

		/// <summary>
		/// Gives a viking that's just been raised its data and inventory from the corpse it rose from
		/// </summary>
		/// <param name="corpse">The corpse's ZDO</param>
		/// <param name="viking">The viking, owned by this client</param>
		public static void HandOver(ZDO corpse, Character viking)
		{
			byte[] dataBytes = corpse.GetByteArray(DataKey);
			if (dataBytes != null)
			{
				HandOver(dataBytes, corpse.GetByteArray(InventoryKey), viking);
			}
		}

		/// <summary>
		/// Gives a viking that's just been raised its data, inventory and name, before its Start loads them
		/// </summary>
		/// <param name="dataBytes">Its VikingData</param>
		/// <param name="inventoryBytes">Its inventory, or null</param>
		/// <param name="viking">The viking, owned by this client</param>
		private static void HandOver(byte[] dataBytes, byte[] inventoryBytes, Character viking)
		{
			ZDO zdo = viking.m_nview.GetZDO();
			zdo.Set(DataKey, dataBytes);
			if (inventoryBytes != null)
			{
				zdo.Set(InventoryKey, inventoryBytes);
			}
			VikingData data = VikingData.FromBytes(dataBytes);
			zdo.Set(ZDOVars.s_overrideHoverName, data.Name);
		}

		/// <summary>
		/// Drops the things a corpse holds for its viking in a tombstone, when the viking couldn't be raised
		/// </summary>
		/// <param name="corpse">The corpse's ZDO</param>
		/// <param name="position">Where the tombstone goes</param>
		/// <param name="rotation">Which way it faces</param>
		public static void DropGrave(ZDO corpse, Vector3 position, Quaternion rotation)
		{
			VikingData data = VikingData.FromBytes(corpse.GetByteArray(DataKey));
			byte[] inventoryBytes = corpse.GetByteArray(InventoryKey);
			if (data == null || inventoryBytes == null)
			{
				return;
			}
			Inventory carried = new Inventory(PrefabName, null, data.Width, data.Height);
			carried.Load(new ZPackage(inventoryBytes));
			SpawnGrave(position, rotation, carried, data);
		}

		/// <summary>
		/// Puts everything in an inventory but kept items in a new tombstone of the player's, the same as their own
		/// death makes, unequipped and in the same slots
		/// </summary>
		/// <param name="position">Where the tombstone goes</param>
		/// <param name="rotation">Which way it faces</param>
		/// <param name="items">The items</param>
		/// <param name="data">The viking's data, naming the player</param>
		private static void SpawnGrave(Vector3 position, Quaternion rotation, Inventory items, VikingData data)
		{
			Inventory dropped = new Inventory(PrefabName, null, data.Width, data.Height);
			foreach (ItemDrop.ItemData item in items.GetAllItems())
			{
				if (!IsKept(item))
				{
					item.m_equipped = false;
					dropped.m_inventory.Add(item);
				}
			}
			if (dropped.NrOfItems() == 0)
			{
				return;
			}
			GameObject tombstone = Game.instance.m_playerPrefab.GetComponent<Player>().m_tombstone;
			GameObject instance = Object.Instantiate(tombstone, position, rotation);
			instance.GetComponent<Container>().GetInventory().MoveInventoryToGrave(dropped);
			instance.GetComponent<TombStone>().Setup(data.Name, data.PlayerId);
		}

		/// <summary>
		/// Checks whether an item is a copy of equipment the player kept
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it's kept</returns>
		private static bool IsKept(ItemDrop.ItemData item)
		{
			return(item.m_customData.ContainsKey(KeptKey));
		}

		/// <summary>
		/// Finds the viking's parts
		/// </summary>
		private void Awake()
		{
			humanoid = GetComponent<Humanoid>();
			nview = GetComponent<ZNetView>();
			ai = GetComponent<MonsterAI>();
			baseSpeed = humanoid.m_speed;
		}

		/// <summary>
		/// Loads the viking on its owner as it starts, rolling a viking of the world's look and gear the first time
		/// </summary>
		public void OnStart()
		{
			if (nview != null && nview.IsValid() && nview.IsOwner())
			{
				Load();
				wasOwner = loaded;
			}
		}

		/// <summary>
		/// On the owner, loads the viking when it's handed over, saves its inventory as it changes, raises its shield
		/// and gives it its one chance to use its forsaken power
		/// </summary>
		private void Update()
		{
			if (nview == null || !nview.IsValid())
			{
				return;
			}
			if (!nview.IsOwner())
			{
				wasOwner = false;
				return;
			}
			// A new owner loads the inventory again, since the last owner may have used some of it
			if (!loaded || !wasOwner)
			{
				Load();
				wasOwner = loaded;
				if (!loaded)
				{
					return;
				}
			}
			if (Time.time >= nextSync)
			{
				nextSync = Time.time + SyncInterval;
				DropBrokenShield();
				SaveInventory();
			}
			UpdateBlock();
			UpdatePower();
		}

		/// <summary>
		/// Rolls a new viking of the world's look, health, skill, power and gear for the biome it's in, and marks it as a
		/// zombie
		/// </summary>
		private void Generate()
		{
			int index = VikingGear.IndexOf(WorldGenerator.instance.GetBiome(transform.position));
			VikingData rolled = VikingGear.RollData(index);
			Inventory gear = VikingGear.RollLoadout(index, humanoid.GetLevel(), rolled);
			ZPackage pkg = new ZPackage();
			gear.Save(pkg);
			ZDO zdo = nview.GetZDO();
			zdo.Set(DataKey, rolled.ToBytes());
			zdo.Set(InventoryKey, pkg.GetArray());
			zdo.Set(Zombies.ZombieKey, true);
			data = null;
		}

		/// <summary>
		/// Finds the index of the biome the viking's gear, coins and fists come from
		/// </summary>
		/// <returns>The index into VikingGear's tables</returns>
		private int BiomeIndex()
		{
			if (GetData() != null && data.Biome >= 0)
			{
				return(data.Biome);
			}
			return(VikingGear.IndexOf(WorldGenerator.instance.GetBiome(transform.position)));
		}

		/// <summary>
		/// Gets the viking's level in a skill - a viking of the world's own level in everything, or the dead player's
		/// level for a tombstone viking
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <returns>The level from 0 to 100, or a negative number before the viking has data</returns>
		public float SkillLevel(Skills.SkillType skill)
		{
			if (GetData() == null)
			{
				return(-1f);
			}
			if (!data.Tombstone)
			{
				return(data.Skill);
			}
			// A tombstone viking from before vikings had skills fights at its biome's middle skill
			if (data.Skills.Count == 0)
			{
				BiomeGear gear = VikingGear.Get(BiomeIndex());
				return((gear.SkillMin + gear.SkillMax) / 2f);
			}
			return(data.Skills.TryGetValue((int)skill, out float level) ? level : 0f);
		}

		/// <summary>
		/// Works out how much harder the viking hits than a player of its skill with what's in its hands - its fists by
		/// its biome's punch, and a viking of the world's melee weapons by its biome's MeleeScale
		/// </summary>
		/// <returns>The multiplier for the hit's damage</returns>
		public float DamageScale()
		{
			ItemDrop.ItemData weapon = CurrentWeapon();
			BiomeGear gear = VikingGear.Get(BiomeIndex());
			if (weapon == null)
			{
				ItemDrop unarmed = humanoid.m_unarmedWeapon;
				float baseDamage = ((unarmed != null) ? unarmed.m_itemData.m_shared.m_damages.GetTotalPhysicalDamage() : 0f);
				return((baseDamage > 0f) ? gear.Unarmed / baseDamage : 1f);
			}
			bool ranged = (weapon.m_shared.m_attack != null && weapon.m_shared.m_attack.m_attackType == Attack.AttackType.Projectile);
			return((GetData() != null && !data.Tombstone && !ranged) ? gear.MeleeScale : 1f);
		}

		/// <summary>
		/// Adds up the armor of the pieces the viking wears, at the config's ArmorEffectiveness percent of what they give a
		/// player
		/// </summary>
		/// <returns>Its armor</returns>
		private float BodyArmor()
		{
			float armor = 0f;
			foreach (ItemDrop.ItemData piece in WornArmor())
			{
				armor += piece.GetArmor();
			}
			return(armor * Plugin.ArmorEffectiveness.Value / 100f);
		}

		/// <summary>
		/// Lists the armor pieces the viking wears
		/// </summary>
		/// <returns>Its chest, legs, helmet and cape, those it has on</returns>
		private List<ItemDrop.ItemData> WornArmor()
		{
			List<ItemDrop.ItemData> pieces = new List<ItemDrop.ItemData>();
			foreach (ItemDrop.ItemData piece in new ItemDrop.ItemData[] { humanoid.m_chestItem, humanoid.m_legItem, humanoid.m_helmetItem, humanoid.m_shoulderItem })
			{
				if (piece != null)
				{
					pieces.Add(piece);
				}
			}
			return(pieces);
		}

		/// <summary>
		/// Reduces a hit by the viking's armor and wears down a random piece by the damage left, taking it off when it
		/// breaks - the same as a player's armor
		/// </summary>
		/// <param name="hit">The hit, after resistances</param>
		public void ApplyArmor(HitData hit)
		{
			List<ItemDrop.ItemData> pieces = WornArmor();
			if (pieces.Count == 0)
			{
				return;
			}
			hit.ApplyArmor(BodyArmor());
			float damage = hit.GetTotalPhysicalDamage() + hit.GetTotalElementalDamage();
			if (damage <= 0f)
			{
				return;
			}
			ItemDrop.ItemData piece = pieces[Random.Range(0, pieces.Count)];
			if (!piece.m_shared.m_useDurability)
			{
				return;
			}
			piece.m_durability = Mathf.Max(0f, piece.m_durability - damage);
			if (piece.m_durability <= 0f)
			{
				humanoid.UnequipItem(piece, false);
			}
		}

		/// <summary>
		/// Adds the resistances of the viking's armor to its own, the same as a player's
		/// </summary>
		/// <param name="mods">Its damage modifiers</param>
		public void ApplyArmorMods(ref HitData.DamageModifiers mods)
		{
			foreach (ItemDrop.ItemData piece in WornArmor())
			{
				mods.Apply(piece.m_shared.m_damageModifiers);
			}
		}

		/// <summary>
		/// Takes off the viking's shield once it has broken
		/// </summary>
		private void DropBrokenShield()
		{
			ItemDrop.ItemData left = humanoid.m_leftItem;
			if (left != null && left.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield && left.m_shared.m_useDurability && left.m_durability <= 0f)
			{
				humanoid.UnequipItem(left, false);
			}
		}

		/// <summary>
		/// Raises the viking's shield against an enemy's attack it sees starting - more often and closer to the moment of
		/// the hit the more skilled it is, but never every time or perfectly - and lowers it again after a moment
		/// </summary>
		private void UpdateBlock()
		{
			ItemDrop.ItemData left = humanoid.m_leftItem;
			bool hasShield = (left != null && left.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield);
			if (!hasShield || ai == null || humanoid.IsDead())
			{
				StopBlocking();
				enemyWasAttacking = false;
				return;
			}
			float now = Time.time;
			if (blockAt >= 0f && now >= blockAt)
			{
				blockAt = -1f;
				humanoid.m_blocking = true;
				blockUntil = now + Random.Range(BlockHoldMin, BlockHoldMax);
			}
			if (blockUntil >= 0f && now >= blockUntil)
			{
				StopBlocking();
			}
			Character enemy = ai.m_targetCreature;
			bool attacking = (enemy != null && enemy.InAttack() && Vector3.Distance(enemy.transform.position, transform.position) <= ReactRange(enemy));
			if (attacking && !enemyWasAttacking && blockAt < 0f && blockUntil < 0f && !humanoid.InAttack())
			{
				float skill = Mathf.Clamp01(SkillLevel(Skills.SkillType.Blocking) / 100f);
				if (Random.value < Mathf.Lerp(BlockChanceMin, BlockChanceMax, skill))
				{
					float delay = Mathf.Lerp(BlockDelayMin, BlockDelayMax, skill) + Random.Range(-BlockDelayJitter, BlockDelayJitter);
					blockAt = now + Mathf.Max(0.05f, delay);
				}
			}
			enemyWasAttacking = attacking;
		}

		/// <summary>
		/// Works out how near an enemy's attack has to start for the viking to raise its shield
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>The distance - far for an enemy with a bow or other ranged weapon</returns>
		private static float ReactRange(Character enemy)
		{
			Humanoid armed = enemy as Humanoid;
			ItemDrop.ItemData weapon = ((armed != null) ? armed.GetCurrentWeapon() : null);
			bool ranged = (weapon != null && weapon.m_shared.m_attack != null && weapon.m_shared.m_attack.m_attackType == Attack.AttackType.Projectile);
			return(ranged ? BlockRangedRange : BlockMeleeRange);
		}

		/// <summary>
		/// Lowers the viking's shield and forgets any block it was about to make
		/// </summary>
		private void StopBlocking()
		{
			humanoid.m_blocking = false;
			blockAt = -1f;
			blockUntil = -1f;
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
		/// Reads the viking's data from its ZDO, or null before it's handed over
		/// </summary>
		/// <returns>The data</returns>
		private VikingData GetData()
		{
			if (data == null && nview != null && nview.IsValid())
			{
				data = VikingData.FromBytes(nview.GetZDO().GetByteArray(DataKey));
			}
			return(data);
		}

		/// <summary>
		/// Loads the viking's inventory from its ZDO, puts its armor back on, picks its weapon and gives it its looks,
		/// health and pace - rolling a viking of the world first when it has no data yet
		/// </summary>
		/// <remarks>
		/// A tombstone viking is handed its data in the frame it spawns, before its Start loads it, so a viking still
		/// without data here is one of the world's
		/// </remarks>
		private void Load()
		{
			if (GetData() == null)
			{
				Generate();
				if (GetData() == null)
				{
					return;
				}
			}
			ZDO zdo = nview.GetZDO();
			Inventory inventory = humanoid.GetInventory();
			humanoid.UnequipAllItems();
			inventory.m_width = data.Width;
			inventory.m_height = data.Height;
			byte[] bytes = zdo.GetByteArray(InventoryKey);
			if (bytes != null)
			{
				inventory.Load(new ZPackage(bytes));
			}
			else
			{
				inventory.RemoveAll();
			}
			savedInventory = bytes;
			List<ItemDrop.ItemData> worn = new List<ItemDrop.ItemData>();
			foreach (ItemDrop.ItemData item in inventory.GetAllItems())
			{
				if (item.m_equipped)
				{
					item.m_equipped = false;
					worn.Add(item);
				}
			}
			shield = null;
			foreach (ItemDrop.ItemData item in worn)
			{
				ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
				if (type == ItemDrop.ItemData.ItemType.Shield)
				{
					shield = item;
				}
				else if (!item.IsWeapon() && type != ItemDrop.ItemData.ItemType.Ammo && type != ItemDrop.ItemData.ItemType.AmmoNonEquipable)
				{
					humanoid.EquipItem(item, false);
				}
			}
			EquipWeapon();
			SetHome();
			SetLooks(humanoid.m_visEquipment);
			// A viking of the world's health is multiplied by its stars like any creature's, a tombstone viking's is the
			// player's own
			float health = data.MaxHealth * (data.Tombstone ? 1 : humanoid.GetLevel());
			bool unhurt = humanoid.GetHealth() >= humanoid.GetMaxHealth();
			if (health > 0f)
			{
				humanoid.SetMaxHealth(health);
				if (unhurt)
				{
					humanoid.SetHealth(health);
				}
			}
			bool night = (!data.Tombstone && ai != null && ai.DespawnInDay());
			humanoid.m_runSpeed = (night ? NightRunSpeed : RunSpeed);
			humanoid.m_speed = baseSpeed * (night ? NightSpeedScale : 1f);
			loaded = true;
		}

		/// <summary>
		/// Makes the player's tombstone the viking's home, which its AI wanders around and its spawn point, so it's
		/// where the game measures how far it has chased from
		/// </summary>
		private void SetHome()
		{
			if (ai == null || !data.Tombstone)
			{
				return;
			}
			home = (data.HasHome ? data.Home : ai.m_spawnPoint);
			hasHome = true;
			ai.m_spawnPoint = home;
			nview.GetZDO().Set(ZDOVars.s_spawnPoint, home);
			ai.SetPatrolPoint(home);
		}

		/// <summary>
		/// Checks whether the viking guards a tombstone, rather than roaming like a viking of the world
		/// </summary>
		/// <returns>True once a tombstone viking has its home</returns>
		public bool IsGuard()
		{
			return(hasHome);
		}

		/// <summary>
		/// Checks whether a point is within a distance of the viking's home
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="range">The distance</param>
		/// <returns>True when it is, or when the viking has no home yet</returns>
		private bool NearHome(Vector3 point, float range)
		{
			return(!hasHome || Utils.DistanceXZ(point, home) <= range);
		}

		/// <summary>
		/// Checks whether the viking is still after an enemy that hurt it
		/// </summary>
		/// <param name="target">The enemy</param>
		/// <returns>True when the enemy hurt it within GrudgeTime</returns>
		private bool HoldsGrudge(Character target)
		{
			return(target != null && target == grudge && Time.time <= grudgeUntil);
		}

		/// <summary>
		/// Notes an enemy that hurt the viking, so it chases them beyond its leash, and stops it running home from its
		/// leash to do so
		/// </summary>
		/// <param name="attacker">Who hurt it, or null</param>
		public void OnHurt(Character attacker)
		{
			if (attacker == null || attacker.IsDead() || !BaseAI.IsEnemy(humanoid, attacker))
			{
				return;
			}
			grudge = attacker;
			grudgeUntil = Time.time + GrudgeTime;
			if (returningSince >= 0f && !returningFromGrudge)
			{
				returningSince = -1f;
				ai.m_targetCreature = attacker;
				ai.m_lastKnownTargetPos = attacker.transform.position;
				ai.m_beenAtLastPos = false;
				ai.m_targetStatic = null;
				ai.SetAlerted(true);
			}
		}

		/// <summary>
		/// Forgets an enemy the viking has picked that's too far from its home to go after - beyond its leash, or beyond
		/// GrudgeRange for an enemy that has just hurt it
		/// </summary>
		public void LeashTarget()
		{
			if (ai == null || !hasHome)
			{
				return;
			}
			Character target = ai.m_targetCreature;
			if (target != null && !NearHome(target.transform.position, (HoldsGrudge(target) ? GrudgeRange : LeashRange)))
			{
				ai.m_targetCreature = null;
			}
			if (ai.m_targetStatic != null && !NearHome(ai.m_targetStatic.transform.position, LeashRange))
			{
				ai.m_targetStatic = null;
			}
		}

		/// <summary>
		/// Runs the viking home when it has gone too far from it - beyond its leash, or beyond GrudgeRange while it chases
		/// an enemy that hurt it - ignoring everything but being hurt again from its leash until it's back, and puts it
		/// there when it's stuck
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True while it's running home, so the AI's own update is skipped</returns>
		public bool UpdateReturn(float dt)
		{
			if (ai == null || !hasHome || !nview.IsOwner() || RiseBlend.IsHeld(ai))
			{
				return(false);
			}
			float distance = Utils.DistanceXZ(transform.position, home);
			if (returningSince < 0f)
			{
				float limit = (HoldsGrudge(ai.m_targetCreature) ? GrudgeRange : LeashRange);
				if (distance <= limit)
				{
					return(false);
				}
				returningSince = Time.time;
				returningFromGrudge = (distance > GrudgeRange);
				if (returningFromGrudge)
				{
					grudge = null;
				}
				ai.m_targetCreature = null;
				ai.m_targetStatic = null;
				ai.SetAlerted(false);
			}
			if (distance <= HomeRange)
			{
				returningSince = -1f;
				return(false);
			}
			if (Time.time - returningSince > ReturnTimeout)
			{
				returningSince = -1f;
				ai.StopMoving();
				Vector3 landing = home + Vector3.up * 0.5f;
				humanoid.m_body.position = landing;
				transform.position = landing;
				return(true);
			}
			ai.MoveTo(dt, home, 0f, true);
			return(true);
		}

		/// <summary>
		/// Keeps the viking by its home while it has nothing to fight - walking straight back when it has strayed, and
		/// otherwise standing still, with a short stroll now and then
		/// </summary>
		/// <remarks>
		/// The game's wandering runs from spot to spot without pausing while a creature is alerted, which the viking is
		/// as it rises and for a while after each fight
		/// </remarks>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True when it's handled, so the game's wandering is skipped</returns>
		public bool UpdateIdle(float dt)
		{
			if (ai == null || !hasHome)
			{
				return(false);
			}
			if (Utils.DistanceXZ(transform.position, home) > HomeRange)
			{
				hasStrollTarget = false;
				ai.MoveTo(dt, home, 0f, false);
				return(true);
			}
			if (hasStrollTarget)
			{
				if (ai.MoveTo(dt, strollTarget, StrollReach, false))
				{
					hasStrollTarget = false;
					nextStroll = Time.time + Random.Range(StrollIntervalMin, StrollIntervalMax);
				}
				return(true);
			}
			ai.StopMoving();
			if (nextStroll == 0f)
			{
				nextStroll = Time.time + Random.Range(StrollIntervalMin, StrollIntervalMax);
			}
			else if (Time.time >= nextStroll)
			{
				Vector2 offset = Random.insideUnitCircle * GuardRange;
				strollTarget = home + new Vector3(offset.x, 0f, offset.y);
				hasStrollTarget = true;
			}
			return(true);
		}

		/// <summary>
		/// Gives a body the player's model, skin and hair
		/// </summary>
		/// <param name="visual">The viking's or its corpse's equipment visuals, owned by this client</param>
		private void SetLooks(VisEquipment visual)
		{
			if (visual == null || GetData() == null)
			{
				return;
			}
			visual.SetModel(data.Model);
			visual.SetSkinColor(data.SkinColor);
			visual.SetHairColor(data.HairColor);
			visual.SetBeardItem(data.Beard.GetStableHashCode());
			visual.SetHairItem(data.Hair.GetStableHashCode());
		}

		/// <summary>
		/// Gives the viking's corpse the player's looks, since a monster's corpse only gets its equipment
		/// </summary>
		/// <param name="ragdoll">The corpse, just made by this client</param>
		public void OnRagdollCreated(Ragdoll ragdoll)
		{
			SetLooks(ragdoll.GetComponent<VisEquipment>());
		}

		/// <summary>
		/// Writes the viking's inventory to its ZDO when it has changed - arrows shot, weapons worn down
		/// </summary>
		private void SaveInventory()
		{
			ZPackage pkg = new ZPackage();
			humanoid.GetInventory().Save(pkg);
			byte[] bytes = pkg.GetArray();
			if (savedInventory != null && bytes.SequenceEqual(savedInventory))
			{
				return;
			}
			nview.GetZDO().Set(InventoryKey, bytes);
			savedInventory = bytes;
		}

		/// <summary>
		/// As the viking dies on its owner, drops everything a tombstone viking carries but kept items in a tombstone of
		/// the player's, or a viking of the world's coins and maybe some of its gear
		/// </summary>
		public void DropGrave()
		{
			if (!loaded)
			{
				Load();
			}
			if (!loaded)
			{
				return;
			}
			if (data.Tombstone)
			{
				SpawnGrave(humanoid.GetCenterPoint(), transform.rotation, humanoid.GetInventory(), data);
			}
			else
			{
				DropLoot();
			}
			humanoid.GetInventory().m_inventory.Clear();
		}

		/// <summary>
		/// Drops a viking of the world's coins and remains, more for each star, and each piece of its gear by
		/// the config's GearDropChance, up to its GearDropMax, as it is
		/// </summary>
		private void DropLoot()
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
		/// <param name="prefabName">The item's prefab name</param>
		/// <param name="amount">How many to drop</param>
		private static void AddLoot(List<KeyValuePair<GameObject, int>> loot, string prefabName, int amount)
		{
			GameObject prefab = ObjectDB.instance.GetItemPrefab(prefabName);
			if (amount > 0 && prefab != null)
			{
				loot.Add(new KeyValuePair<GameObject, int>(prefab, amount));
			}
		}

		/// <summary>
		/// Wields the viking's weapon - what the player was wielding while it still works, or else its best one, or its
		/// fists when it has none left - with the player's shield beside a one-handed weapon
		/// </summary>
		public void EquipWeapon()
		{
			if (GetData() == null)
			{
				return;
			}
			ItemDrop.ItemData choice = ChooseWeapon();
			ItemDrop.ItemData current = CurrentWeapon();
			if (choice != current)
			{
				if (choice != null)
				{
					humanoid.EquipItem(choice, false);
				}
				else
				{
					if (humanoid.m_rightItem != null && humanoid.m_rightItem.IsWeapon())
					{
						humanoid.UnequipItem(humanoid.m_rightItem, false);
					}
					if (humanoid.m_leftItem != null && humanoid.m_leftItem.IsWeapon())
					{
						humanoid.UnequipItem(humanoid.m_leftItem, false);
					}
				}
			}
			bool oneHanded = (choice == null || choice.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon);
			bool shieldWorks = (shield != null && (!shield.m_shared.m_useDurability || shield.m_durability > 0f));
			if (shieldWorks && oneHanded && !humanoid.IsItemEquiped(shield) && humanoid.GetInventory().ContainsItem(shield))
			{
				humanoid.EquipItem(shield, false);
			}
		}

		/// <summary>
		/// Switches weapons before an attack when the one in hand has broken or run out of ammo
		/// </summary>
		public void CheckWeapon()
		{
			ItemDrop.ItemData current = CurrentWeapon();
			if (current != null && !CanUse(current))
			{
				EquipWeapon();
			}
		}

		/// <summary>
		/// Wears down the weapon the viking just attacked with, the same as a player's
		/// </summary>
		public void WearWeapon()
		{
			ItemDrop.ItemData weapon = CurrentWeapon();
			if (weapon != null && weapon.m_shared.m_useDurability)
			{
				weapon.m_durability = Mathf.Max(0f, weapon.m_durability - weapon.m_shared.m_useDurabilityDrain * Game.m_durabilityRate);
			}
		}

		/// <summary>
		/// Finds the weapon in the viking's hands
		/// </summary>
		/// <returns>The weapon, or null when it's fighting with its fists</returns>
		private ItemDrop.ItemData CurrentWeapon()
		{
			ItemDrop.ItemData weapon = humanoid.GetCurrentWeapon();
			return((weapon != null && humanoid.GetInventory().ContainsItem(weapon)) ? weapon : null);
		}

		/// <summary>
		/// Picks the weapon the viking should wield
		/// </summary>
		/// <returns>The weapon the player was wielding while it still works, or else the one that deals the most
		/// damage, or null for fists</returns>
		private ItemDrop.ItemData ChooseWeapon()
		{
			Inventory inventory = humanoid.GetInventory();
			if (data.WeaponSlot.x >= 0)
			{
				ItemDrop.ItemData held = inventory.GetItemAt(data.WeaponSlot.x, data.WeaponSlot.y);
				if (held != null && CanUse(held))
				{
					return(held);
				}
			}
			ItemDrop.ItemData best = null;
			float bestDamage = 0f;
			foreach (ItemDrop.ItemData item in inventory.GetAllItems())
			{
				if (!CanUse(item))
				{
					continue;
				}
				float damage = Damage(item);
				if (best == null || damage > bestDamage)
				{
					best = item;
					bestDamage = damage;
				}
			}
			return(best);
		}

		/// <summary>
		/// Checks whether the viking can fight with an item - a weapon that isn't broken, has ammo when it takes ammo,
		/// doesn't need eitr, which the viking has none of, and deals damage
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it can</returns>
		private bool CanUse(ItemDrop.ItemData item)
		{
			if (!item.IsWeapon())
			{
				return(false);
			}
			ItemDrop.ItemData.SharedData shared = item.m_shared;
			if (shared.m_useDurability && item.m_durability <= 0f)
			{
				return(false);
			}
			Attack attack = shared.m_attack;
			if (attack == null || string.IsNullOrEmpty(attack.m_attackAnimation) || attack.m_attackEitr > 0f)
			{
				return(false);
			}
			if (!string.IsNullOrEmpty(shared.m_ammoType) && FindAmmo(item) == null)
			{
				return(false);
			}
			return(Damage(item) > 0f);
		}

		/// <summary>
		/// Adds up the damage a weapon deals to creatures, with the ammo it would shoot
		/// </summary>
		/// <param name="weapon">The weapon</param>
		/// <returns>Its damage, leaving out chopping and mining</returns>
		private float Damage(ItemDrop.ItemData weapon)
		{
			float damage = CombatDamage(weapon);
			ItemDrop.ItemData ammo = FindAmmo(weapon);
			if (ammo != null)
			{
				damage += CombatDamage(ammo);
			}
			return(damage);
		}

		/// <summary>
		/// Adds up the damage an item deals to creatures
		/// </summary>
		/// <param name="item">The weapon or ammo</param>
		/// <returns>Its damage, leaving out chopping and mining</returns>
		private static float CombatDamage(ItemDrop.ItemData item)
		{
			HitData.DamageTypes damage = item.GetDamage();
			return(damage.m_damage + damage.GetTotalPhysicalDamage() + damage.GetTotalElementalDamage() + damage.m_poison + damage.m_spirit);
		}

		/// <summary>
		/// Finds the ammo a weapon would shoot, the way an attack picks it
		/// </summary>
		/// <param name="weapon">The weapon</param>
		/// <returns>The ammo, or null when the weapon takes none or there's none left</returns>
		private ItemDrop.ItemData FindAmmo(ItemDrop.ItemData weapon)
		{
			string ammoType = weapon.m_shared.m_ammoType;
			return(string.IsNullOrEmpty(ammoType) ? null : humanoid.GetInventory().GetAmmoItem(ammoType));
		}

		/// <summary>
		/// Gives a ranged weapon in the viking's hands an archer's range and pace for the length of its AI's update
		/// </summary>
		/// <remarks>
		/// A player's weapons share their settings with every copy of the item and only the AI reads these ones, so
		/// they're changed just while this viking's AI runs and put back by RestoreWeaponAI
		/// </remarks>
		public void TuneWeaponAI()
		{
			RestoreWeaponAI();
			ItemDrop.ItemData weapon = CurrentWeapon();
			if (weapon == null || weapon.m_shared.m_attack == null || weapon.m_shared.m_attack.m_attackType != Attack.AttackType.Projectile)
			{
				return;
			}
			ItemDrop.ItemData.SharedData shared = weapon.m_shared;
			tunedShared = shared;
			tunedRange = shared.m_aiAttackRange;
			tunedRangeMin = shared.m_aiAttackRangeMin;
			tunedAngle = shared.m_aiAttackMaxAngle;
			tunedInterval = shared.m_aiAttackInterval;
			shared.m_aiAttackRange = RangedAttackRange;
			shared.m_aiAttackRangeMin = 0f;
			shared.m_aiAttackMaxAngle = RangedAttackAngle;
			shared.m_aiAttackInterval = (shared.m_attack.m_requiresReload ? ReloadAttackInterval : RangedAttackInterval);
		}

		/// <summary>
		/// Puts back the settings TuneWeaponAI changed
		/// </summary>
		public void RestoreWeaponAI()
		{
			if (tunedShared == null)
			{
				return;
			}
			tunedShared.m_aiAttackRange = tunedRange;
			tunedShared.m_aiAttackRangeMin = tunedRangeMin;
			tunedShared.m_aiAttackMaxAngle = tunedAngle;
			tunedShared.m_aiAttackInterval = tunedInterval;
			tunedShared = null;
		}

		/// <summary>
		/// Gives the viking its one chance to use its forsaken power once it's standing - a tombstone viking the first
		/// time a player comes near, a viking of the world the first time it spots an enemy - unless another viking has
		/// used one within PowerCooldown
		/// </summary>
		private void UpdatePower()
		{
			ZDO zdo = nview.GetZDO();
			if (data.Power == 0 || zdo.GetBool(PowerUsedKey) || Zombies.IsRising(humanoid))
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
				chance = TombstonePowerChance;
			}
			else
			{
				if (ai == null || ai.m_targetCreature == null)
				{
					return;
				}
				chance = WorldPowerChance;
			}
			zdo.Set(PowerUsedKey, true);
			long now = Zombies.Now();
			if (Random.value >= chance || now < powerCooldownUntil)
			{
				return;
			}
			powerCooldownUntil = now + Zombies.Ticks(PowerCooldown);
			if (ZRoutedRpc.instance != null)
			{
				ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, PowerRpc, powerCooldownUntil);
			}
			StartCoroutine(UsePower(data.Power));
		}

		/// <summary>
		/// Plays the forsaken power animation, then gives the power to the viking and the undead around it
		/// </summary>
		/// <param name="power">The power's status effect name hash</param>
		/// <returns>The coroutine</returns>
		private IEnumerator UsePower(int power)
		{
			humanoid.m_zanim.SetTrigger("gpower");
			yield return new WaitForSeconds(PowerDelay);
			if (humanoid == null || humanoid.IsDead() || !nview.IsValid() || ObjectDB.instance.GetStatusEffect(power) == null)
			{
				yield break;
			}
			List<Character> nearby = new List<Character>();
			Character.GetCharactersInRange(transform.position, PowerRange, nearby);
			foreach (Character character in nearby)
			{
				bool undead = (!character.IsPlayer() && !character.IsDead() && character.GetFaction() == Character.Faction.Undead);
				if (character == humanoid || undead)
				{
					character.GetSEMan().AddStatusEffect(power, true);
				}
			}
		}
	}
}
