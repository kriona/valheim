using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Kriona.Vikings
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
		/// Checks whether the viking has a beard
		/// </summary>
		/// <returns>True when it has</returns>
		public bool HasBeard()
		{
			return(!string.IsNullOrEmpty(Beard) && Beard != VikingGear.NoBeard);
		}

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
	/// The core of every viking, living or zombie - who it is, how it looks, its gear and skills and how it fights. A
	/// zombie viking is a player killed by a zombie, risen from their corpse with their looks, health, armor and things,
	/// or one of the world's, with a random look and gear from VikingGear, and a living viking is one of the world's too
	/// </summary>
	/// <remarks>
	/// Each kind is its own prefab, built from the player's prefab with the player's parts swapped for a monster's and
	/// the draugr's AI - the living viking by Viking Villages and the zombie viking by Zombie Apocalypse, each with its
	/// own copy of this class, finished by a part of its own that does what only its kind does. A zombie viking is in
	/// the Undead faction. A living viking is in the dvergr's, so it fights zombies and monsters and leaves players alone
	/// until one attacks it or another viking nearby, and with Zombie Apocalypse its corpse rises as a zombie viking with
	/// its looks and gear, handed over in the corpse's ZDO, which drops its coins when it's killed in turn. Viking
	/// Villages adds the dvergr viking too, a living viking the dvergr have hired, which keeps to their outposts and
	/// patrols and fights the way a zombie viking does, and rises the same way a living viking does. A tombstone
	/// viking carries everything the player had but food, silver and spirit weapons, and keeps it in its ZDO as it's
	/// used up, so it never despawns or loses it. It drops it in a tombstone of the player's when it's killed. Equipment
	/// the world let the player keep is copied to it marked as kept, so it fights with it but never drops it. A viking of
	/// the world rolls its gear the first time it loads, and drops coins and maybe some of its gear. Each wields its
	/// chosen weapon, or else its best one, and switches when that breaks or runs out of ammo. Its armor protects it and
	/// wears down, and it raises its shield against attacks, better the more skilled it is
	/// </remarks>
	internal partial class VikingCore : MonoBehaviour
	{
		/// <summary>
		/// The zombie viking's prefab, whose name saved worlds store, so it can't change
		/// </summary>
		public const string ZombiePrefabName = "ZombieViking";
		public static readonly int ZombiePrefabHash = ZombiePrefabName.GetStableHashCode();

		/// <summary>
		/// The living viking's prefab
		/// </summary>
		public const string LivingPrefabName = "Viking";
		public static readonly int LivingPrefabHash = LivingPrefabName.GetStableHashCode();

		/// <summary>
		/// The dvergr viking's prefab - a living viking hired by the dvergr, which Viking Villages adds beside its village
		/// vikings and Zombie Apocalypse raises as a zombie viking, whose name saved worlds store, so it can't change
		/// </summary>
		public const string DvergrPrefabName = "DvergrViking";
		public static readonly int DvergrPrefabHash = DvergrPrefabName.GetStableHashCode();

		/// <summary>
		/// Asks the viking's owner for its debug summary, from a client in debug mode hovering a viking it doesn't own
		/// </summary>
		private const string DebugAskRpc = "kriona.Vikings.VikingDebugAsk";

		/// <summary>
		/// The owner's answer, with its debug summary and what the viking is doing
		/// </summary>
		private const string DebugAnswerRpc = "kriona.Vikings.VikingDebugAnswer";

		/// <summary>
		/// Seconds between asks while the viking stays hovered
		/// </summary>
		private const float DebugAskInterval = 1f;

		/// <summary>
		/// Seconds the owner's answer is shown before it counts as out of date, a few asks' worth so a late answer
		/// doesn't make the summary flicker
		/// </summary>
		private const float DebugAnswerLife = 3f;

		/// <summary>
		/// On a viking and the corpse it rises from, its VikingData - written by Viking Villages for a living viking and
		/// read by Zombie Apocalypse for the zombie it rises as, so it can't change
		/// </summary>
		public static readonly int DataKey = "kriona.Vikings.vikingData".GetStableHashCode();

		/// <summary>
		/// On a viking and the corpse it rises from, its inventory as saved by Inventory.Save, handed over like DataKey
		/// </summary>
		public static readonly int InventoryKey = "kriona.Vikings.vikingInventory".GetStableHashCode();

		/// <summary>
		/// Custom data on an item the player kept when they died, which the viking uses but never drops
		/// </summary>
		private const string KeptKey = "kriona.ZombieApocalypse.kept";

		/// <summary>
		/// How far from its home the viking wanders while it has nothing to fight
		/// </summary>
		private const float GuardRange = 3f;

		/// <summary>
		/// On a living viking whose gear isn't rolled yet, that it's to be given a bow when its gear is rolled, so every
		/// village has one who can shoot (see VikingGear.LivingArms) - taken off once its gear is rolled (see Load)
		/// </summary>
		public static readonly int ArcherKey = "kriona.VikingVillages.vikingArcher".GetStableHashCode();

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

		/// <summary>
		/// How fast each skill rises for a player, read from the player's prefab the first time it's wanted
		/// </summary>
		private static Dictionary<Skills.SkillType, float> increaseSteps;

		/// <summary>
		/// How a living viking runs up a slope it would only walk elsewhere - the least the ground has to slope, in
		/// degrees, how straight up it has to be heading, as the share of its way that goes up the slope, and how long it
		/// keeps running once it's no longer climbing, in seconds, so it doesn't slow down on a bump halfway up
		/// </summary>
		private const float ClimbAngle = 20f;
		private const float ClimbHeading = 0.3f;
		private const float ClimbHold = 1f;

		/// <summary>
		/// How long a living viking runs or swims for each rise of its Run or Swim skill, in seconds, the same as a
		/// player, how much faster it runs and harder it jumps at full Run and Jump skill, the same as a player, and how
		/// much faster it swims at full Swim skill - a player's Swim only eases their stamina, which a viking has none of
		/// </summary>
		private const float RunSkillInterval = 1f;
		public const float RunSkillBonus = 0.25f;
		public const float JumpSkillBonus = 0.4f;
		private const float SwimSkillBonus = 0.25f;

		/// <summary>
		/// How long after it's hurt a living viking still counts itself in danger, in seconds
		/// </summary>
		private const float DangerHurtTime = 10f;

		private const float SyncInterval = 1f;

		/// <summary>
		/// A night viking's running speed and how much faster it walks, and so how much faster than a viking of the day
		/// </summary>
		private const float NightRunSpeed = 7f;
		private const float NightSpeedScale = 1.3f;

		/// <summary>
		/// How far a viking's forsaken power reaches, as far as a player's does, and how near a player comes before a
		/// tombstone viking may use its own
		/// </summary>
		private const float PowerRange = 10f;

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
		/// How much of a melee weapon's reach the viking strikes from, so its swing lands rather than falling just short,
		/// and the least it strikes from, in meters (see MeleeReach)
		/// </summary>
		private const float MeleeReachShare = 0.85f;
		private const float MeleeReachMin = 0.75f;

		private static GameObject prefabHolder;

		/// <summary>
		/// The viking prefab this mod adds - the living viking's in Viking Villages and the zombie viking's in Zombie
		/// Apocalypse
		/// </summary>
		private static GameObject prefab;

		private Humanoid humanoid;
		private ZNetView nview;
		private MonsterAI ai;
		private VikingData data;

		/// <summary>
		/// A living viking's life in its village, added with the viking's parts (see AddParts), or null for a zombie
		/// viking
		/// </summary>
		private IVikingLife life = null;

		/// <summary>
		/// When this client last asked the viking's owner for its debug summary
		/// </summary>
		private float debugAskedAt = float.NegativeInfinity;

		/// <summary>
		/// The owner's last debug summary and what the viking was doing ("" for nothing), and when it arrived
		/// </summary>
		private string ownerSummary;
		private string ownerJob = "";
		private float ownerAnsweredAt = float.NegativeInfinity;

		/// <summary>
		/// Whether a living viking has its weapon and shield sheathed on its back
		/// </summary>
		private bool sheathed;

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
		/// Until when a living viking keeps running up the slope it's climbing
		/// </summary>
		private float climbUntil;

		/// <summary>
		/// How long a living viking has been running and swimming since its Run and Swim skills last rose, and how fast
		/// it swims before its Swim skill, or a negative number before that's read
		/// </summary>
		private float runSkillTimer;
		private float swimSkillTimer;
		private float baseSwimSpeed = -1f;

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
		public static bool IsZombieViking(Character character)
		{
			return(IsPrefab(character, ZombiePrefabHash));
		}

		/// <summary>
		/// Checks whether a creature is a living viking
		/// </summary>
		/// <param name="character">The creature</param>
		/// <returns>True when it is</returns>
		public static bool IsLivingViking(Character character)
		{
			return(IsPrefab(character, LivingPrefabHash));
		}

		/// <summary>
		/// Checks whether a creature is a dvergr viking
		/// </summary>
		/// <param name="character">The creature</param>
		/// <returns>True when it is</returns>
		public static bool IsDvergrViking(Character character)
		{
			return(IsPrefab(character, DvergrPrefabHash));
		}

		/// <summary>
		/// Checks whether a creature is a viking that's alive - a living viking or a dvergr viking - which raises its
		/// skills, keeps its gear repaired and rises as a zombie viking with its looks and gear
		/// </summary>
		/// <param name="character">The creature</param>
		/// <returns>True when it is</returns>
		public static bool IsLivingOrDvergr(Character character)
		{
			return(IsLivingViking(character) || IsDvergrViking(character));
		}

		/// <summary>
		/// Checks whether a creature is one of a prefab, by its ZDO, so either mod's vikings are known to both mods
		/// </summary>
		/// <param name="character">The creature, or null</param>
		/// <param name="hash">The prefab's name hash</param>
		/// <returns>True when it is</returns>
		private static bool IsPrefab(Character character, int hash)
		{
			ZNetView view = ((character != null) ? character.m_nview : null);
			return(view != null && view.IsValid() && view.GetZDO().GetPrefab() == hash);
		}

		/// <summary>
		/// Checks whether a viking dying on this client will rise as a zombie, as Zombie Apocalypse has decided by the
		/// time a low-priority prefix of Character.OnDeath runs
		/// </summary>
		/// <param name="dying">The viking</param>
		/// <returns>True when it will</returns>
		public static partial bool Rises(Character dying);

		/// <summary>
		/// Gets the name of the viking prefab this mod adds
		/// </summary>
		/// <returns>Its name, or "" before it's built</returns>
		public static string OwnPrefabName()
		{
			return((prefab != null) ? prefab.name : "");
		}

		/// <summary>
		/// Adds this mod's viking prefab to a world's prefabs, building it the first time (see BuildOwnPrefab)
		/// </summary>
		/// <param name="scene">The world's ZNetScene, as it wakes</param>
		public static void Register(ZNetScene scene)
		{
			if (prefab == null)
			{
				prefab = BuildOwnPrefab(scene);
			}
			if (prefab == null)
			{
				VikingMod.Log.LogWarning("Couldn't build the vikings - the Player or Draugr prefab is missing");
				return;
			}
			AddToScene(scene, prefab);
		}

		/// <summary>
		/// Adds a built prefab to a world's prefabs, unless it's in already
		/// </summary>
		/// <param name="scene">The world's ZNetScene</param>
		/// <param name="built">The prefab</param>
		private static void AddToScene(ZNetScene scene, GameObject built)
		{
			int hash = built.name.GetStableHashCode();
			if (!scene.m_namedPrefabs.ContainsKey(hash))
			{
				scene.m_prefabs.Add(built);
				scene.m_namedPrefabs.Add(hash, built);
			}
		}

		/// <summary>
		/// Builds the viking prefab this mod adds, with its kind's body and AI (see BuildPrefab)
		/// </summary>
		/// <param name="scene">The world's ZNetScene</param>
		/// <returns>The prefab, or null when the player's or draugr's prefab is missing</returns>
		private static partial GameObject BuildOwnPrefab(ZNetScene scene);

		/// <summary>
		/// Builds a viking prefab from the player's prefab, swapping the player's parts for a monster's with the
		/// draugr's AI
		/// </summary>
		/// <remarks>
		/// The copy sits under an inactive object, so none of its parts wake while it's changed, and it's kept across
		/// worlds
		/// </remarks>
		/// <typeparam name="TBody">The kind's body</typeparam>
		/// <typeparam name="TAI">The kind's AI</typeparam>
		/// <param name="scene">The world's ZNetScene</param>
		/// <param name="name">The prefab's name</param>
		/// <param name="faction">The viking's faction</param>
		/// <param name="finish">Sets up what only the kind has, before its VikingCore is added - given the prefab and
		/// its AI - or null when it has nothing more</param>
		/// <returns>The prefab, or null when the player's or draugr's prefab is missing</returns>
		private static GameObject BuildPrefab<TBody, TAI>(ZNetScene scene, string name, Character.Faction faction, System.Action<GameObject, MonsterAI> finish) where TBody : VikingBody where TAI : VikingAI
		{
			GameObject playerPrefab = scene.GetPrefab("Player");
			GameObject draugrPrefab = scene.GetPrefab("Draugr");
			if (playerPrefab == null || draugrPrefab == null || draugrPrefab.GetComponent<MonsterAI>() == null)
			{
				return(null);
			}
			if (prefabHolder == null)
			{
				prefabHolder = new GameObject(VikingMod.Name + " prefabs");
				prefabHolder.SetActive(false);
				Object.DontDestroyOnLoad(prefabHolder);
			}
			GameObject viking = Object.Instantiate(playerPrefab, prefabHolder.transform, false);
			viking.name = name;
			Player player = viking.GetComponent<Player>();
			VikingBody body = viking.AddComponent<TBody>();
			CopyFields(typeof(Character), player, body);
			CopyFields(typeof(Humanoid), player, body);
			Object.DestroyImmediate(player);
			RemoveComponent<PlayerController>(viking);
			RemoveComponent<Talker>(viking);
			RemoveComponent<Skills>(viking);
			body.m_name = "Viking";
			body.m_faction = faction;
			body.m_runSpeed = RunSpeed;
			body.m_defaultItems = new GameObject[0];
			// A player's death shakes their own controller, which a viking's shouldn't
			EffectList.EffectData[] deathEffects = body.m_deathEffects.m_effectPrefabs;
			body.m_deathEffects = new EffectList();
			body.m_deathEffects.m_effectPrefabs = deathEffects.Where((EffectList.EffectData effect) => effect.m_prefab == null || !effect.m_prefab.name.Contains("vibration")).ToArray();
			ZNetView view = viking.GetComponent<ZNetView>();
			view.m_persistent = true;
			view.m_type = ZDO.ObjectType.Default;
			MonsterAI ai = viking.AddComponent<TAI>();
			MonsterAI draugrAI = draugrPrefab.GetComponent<MonsterAI>();
			CopyFields(typeof(BaseAI), draugrAI, ai);
			CopyFields(typeof(MonsterAI), draugrAI, ai);
			ai.m_pathAgentType = Pathfinding.AgentType.Humanoid;
			// The game's sight through the Mistlands' mist goes by the mist this client shows, so a viking's goes by
			// MistSight in its place
			ai.m_mistVision = true;
			ai.m_avoidWater = false;
			ai.m_randomMoveRange = GuardRange;
			ai.m_enableHuntPlayer = false;
			finish?.Invoke(viking, ai);
			viking.AddComponent<VikingCore>();
			return(viking);
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
		/// Checks whether a living viking is an archer, from its ZDO - one marked to get a bow, or one whose gear holds a
		/// bow of its own
		/// </summary>
		/// <param name="zdo">The viking's ZDO</param>
		/// <returns>True when it is</returns>
		public static bool IsArcher(ZDO zdo)
		{
			if (zdo.GetBool(ArcherKey))
			{
				return(true);
			}
			VikingData saved = VikingData.FromBytes(zdo.GetByteArray(DataKey));
			byte[] bytes = zdo.GetByteArray(InventoryKey);
			if (saved == null || bytes == null)
			{
				return(false);
			}
			Inventory inventory = new Inventory(ZombiePrefabName, null, saved.Width, saved.Height);
			inventory.Load(new ZPackage(bytes));
			return(inventory.GetAllItems().Exists((ItemDrop.ItemData item) => IsBow(item) && !VikingItems.IsCarried(item)));
		}

		/// <summary>
		/// Checks whether an item is a bow, one that shoots arrows
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it is</returns>
		public static bool IsBow(ItemDrop.ItemData item)
		{
			return(item != null && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow && !string.IsNullOrEmpty(item.m_shared.m_ammoType));
		}

		/// <summary>
		/// Hands a dying living or dvergr viking's data and inventory to its corpse, for the zombie viking it rises as
		/// </summary>
		/// <param name="creature">The dying creature</param>
		/// <param name="corpse">Its corpse's ZDO, owned by this client</param>
		public static void HandToCorpse(Character creature, ZDO corpse)
		{
			if (creature == null || !IsLivingOrDvergr(creature))
			{
				return;
			}
			ZDO zdo = creature.m_nview.GetZDO();
			byte[] dataBytes = zdo.GetByteArray(DataKey);
			if (dataBytes == null)
			{
				return;
			}
			// The corpse holds the viking's things until its zombie rises, so it's kept with the world
			corpse.Persistent = true;
			corpse.Set(DataKey, dataBytes);
			byte[] inventoryBytes = zdo.GetByteArray(InventoryKey);
			if (inventoryBytes != null)
			{
				corpse.Set(InventoryKey, inventoryBytes);
			}
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
		/// Drops the things a corpse holds for its viking in a tombstone, when the viking couldn't be raised - the wood
		/// and loot it carried from its life in a village on the ground beside it
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
			Inventory carried = new Inventory(ZombiePrefabName, null, data.Width, data.Height);
			carried.Load(new ZPackage(inventoryBytes));
			VikingItems.DropCarried(carried, position);
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
			Inventory dropped = new Inventory(ZombiePrefabName, null, data.Width, data.Height);
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
		/// Finds the viking's parts, adds those only its kind has (see AddParts) and listens for asks for its debug
		/// summary and their answers
		/// </summary>
		private void Awake()
		{
			humanoid = GetComponent<Humanoid>();
			nview = GetComponent<ZNetView>();
			ai = GetComponent<MonsterAI>();
			baseSpeed = humanoid.m_speed;
			AddParts();
			if (nview != null && nview.GetZDO() != null)
			{
				nview.Register(DebugAskRpc, RPC_DebugAsk);
				nview.Register<string, string>(DebugAnswerRpc, RPC_DebugAnswer);
			}
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
			UpdateWalk();
			UpdateRunSkill();
			BurnTorch();
			UpdateSheath();
			UpdateCircletDanger();
			if (Time.time >= nextSync)
			{
				nextSync = Time.time + SyncInterval;
				DropBrokenShield();
				UpdateTorch();
				SaveInventory();
			}
			UpdateBlock();
			UpdatePower();
		}

		/// <summary>
		/// Adds what only the viking's kind has as it wakes, like a living viking's life in its village
		/// </summary>
		partial void AddParts();

		/// <summary>
		/// Gives a zombie viking its one chance to use its forsaken power, on its owner
		/// </summary>
		partial void UpdatePower();

		/// <summary>
		/// Rolls a new viking of the world's look, health, skill, power and gear, the way its kind is rolled, and saves it
		/// (see SaveGenerated)
		/// </summary>
		private partial void Generate();

		/// <summary>
		/// Saves a newly rolled viking's data and gear to its ZDO, for Load to read
		/// </summary>
		/// <param name="rolled">Its data</param>
		/// <param name="gear">Its gear</param>
		/// <returns>Its ZDO</returns>
		private ZDO SaveGenerated(VikingData rolled, Inventory gear)
		{
			ZPackage pkg = new ZPackage();
			gear.Save(pkg);
			ZDO zdo = nview.GetZDO();
			zdo.Set(DataKey, rolled.ToBytes());
			zdo.Set(InventoryKey, pkg.GetArray());
			data = null;
			return(zdo);
		}

		/// <summary>
		/// Finds the index of the biome the viking's gear, coins and fists come from
		/// </summary>
		/// <returns>The index into VikingGear's tables</returns>
		public int BiomeIndex()
		{
			if (GetData() != null && data.Biome >= 0)
			{
				return(data.Biome);
			}
			return(VikingGear.IndexOf(WorldGenerator.instance.GetBiome(transform.position)));
		}

		/// <summary>
		/// Checks whether the viking's gear, coins and fists come from the Ashlands
		/// </summary>
		/// <returns>True when they do</returns>
		public bool OfAshlands()
		{
			return(BiomeIndex() == VikingGear.IndexOf(Heightmap.Biome.AshLands));
		}

		/// <summary>
		/// Gets the viking's level in a skill, with what its effects add, like a piece of gear that raises a skill, and
		/// rounded down to a whole level, the way the game's Skills.GetSkillLevel gives a player's for everything it
		/// goes into (see ExactSkillLevel)
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <returns>The level, or a negative number before the viking has data</returns>
		public float SkillLevel(Skills.SkillType skill)
		{
			float level = ExactSkillLevel(skill);
			return((level >= 0f) ? Mathf.Floor(level) : level);
		}

		/// <summary>
		/// Gets the viking's level in a skill, with what its effects add, including how far it has come toward the next
		/// level, for the skills dialog's bars (see OwnSkillLevel)
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <returns>The level, or a negative number before the viking has data</returns>
		public float ExactSkillLevel(Skills.SkillType skill)
		{
			float level = OwnSkillLevel(skill);
			if (level >= 0f)
			{
				humanoid.GetSEMan().ModifySkillLevel(skill, ref level);
			}
			return(level);
		}

		/// <summary>
		/// Gets the viking's own level in a skill, before what its effects add - a viking of the world's own level in it,
		/// as rolled (see VikingGear.RollData) and raised as a living viking practices it (see RaiseSkill), or its one
		/// level for every skill when it was rolled before each skill had its own, or the dead player's level for a
		/// tombstone viking
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <returns>The level from 0 to 100, or a negative number before the viking has data</returns>
		private float OwnSkillLevel(Skills.SkillType skill)
		{
			if (GetData() == null)
			{
				return(-1f);
			}
			// Every viking is at least SkillFloor in every skill, a tombstone viking whose player never used one included
			if (!data.Tombstone)
			{
				return(Mathf.Max(SkillFloor, data.Skills.TryGetValue((int)skill, out float own) ? own : data.Skill));
			}
			// A tombstone viking from before vikings had skills fights at its biome's middle skill
			if (data.Skills.Count == 0)
			{
				BiomeGear gear = VikingGear.Get(BiomeIndex());
				return(Mathf.Max(SkillFloor, (gear.SkillMin + gear.SkillMax) / 2f));
			}
			return(Mathf.Max(SkillFloor, data.Skills.TryGetValue((int)skill, out float level) ? level : 0f));
		}

		/// <summary>
		/// The lowest level a viking has in any skill
		/// </summary>
		public const float SkillFloor = 1f;

		/// <summary>
		/// Raises a living or dvergr viking's skill the way a player's rises, for the same things, like a hit with a
		/// weapon or a block - by the skill's step, the world's skill gain rate, its status effects and being rested,
		/// toward the next level the way a player's is, from its own level in everything - glowing around its head on
		/// every client as it reaches a new level, the way a player does
		/// </summary>
		/// <remarks>
		/// Its levels are kept in its data as it rises, rather than a player's whole levels and progress toward the next,
		/// so it rises a bit at a time at the rate a player's fills up
		/// </remarks>
		/// <param name="skill">The skill</param>
		/// <param name="factor">How much the action raises it, as the game gives it</param>
		public void RaiseSkill(Skills.SkillType skill, float factor)
		{
			if (skill == Skills.SkillType.None || !IsLivingOrDvergr(humanoid) || nview == null || !nview.IsValid() || !nview.IsOwner())
			{
				return;
			}
			// Read again, since another client may have raised its skills while it owned the viking
			data = VikingData.FromBytes(nview.GetZDO().GetByteArray(DataKey));
			if (data == null || data.Tombstone)
			{
				return;
			}
			float level = OwnSkillLevel(skill);
			if (level >= 100f)
			{
				return;
			}
			float multiplier = 1f;
			humanoid.GetSEMan().ModifyRaiseSkill(skill, ref multiplier);
			ModifySkillGain(skill, ref multiplier);
			float gain = IncreaseStep(skill) * factor * multiplier * Game.m_skillGainRate;
			float raised = RaisedLevel(level, gain);
			data.Skills[(int)skill] = raised;
			nview.GetZDO().Set(DataKey, data.ToBytes());
			if (!LeveledUp(level, raised))
			{
				return;
			}
			OnLevelUp(skill, raised);
			if (Player.m_debugMode)
			{
				string label = data.Name;
				NameForLog(ref label);
				VikingMod.Log.LogInfo("Skill: " + label + " at " + transform.position.ToString("F0") + " reached " + skill + " " + Mathf.Floor(raised));
			}
		}

		/// <summary>
		/// Changes how much a skill rises by what only the viking's kind has, like a living viking being rested
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <param name="multiplier">How much it rises, changed here</param>
		partial void ModifySkillGain(Skills.SkillType skill, ref float multiplier);

		/// <summary>
		/// Shows the viking reaching a new level in a skill, the way its kind does
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <param name="raised">Its new level</param>
		partial void OnLevelUp(Skills.SkillType skill, float raised);

		/// <summary>
		/// Works out how much a player has to gain in a skill to reach the next level from a level, the game's
		/// Skills.Skill.GetNextLevelRequirement
		/// </summary>
		/// <param name="level">The level, from 0 to 100</param>
		/// <returns>The gain the next level takes</returns>
		internal static float LevelRequirement(float level)
		{
			return(Mathf.Pow(Mathf.Floor(level + 1f), 1.5f) * 0.5f + 0.5f);
		}

		/// <summary>
		/// Works out a living viking's level in a skill after a gain - rising by the share of what the next level takes
		/// (see LevelRequirement) that the gain is, so a player's gain to a whole level takes the viking there too, up to
		/// 100
		/// </summary>
		/// <param name="level">Its level, from 0 to 100</param>
		/// <param name="gain">What it gains, as a player's skill would</param>
		/// <returns>The raised level</returns>
		internal static float RaisedLevel(float level, float gain)
		{
			return(Mathf.Min(100f, level + gain / LevelRequirement(level)));
		}

		/// <summary>
		/// Checks whether a skill rising reached a new whole level
		/// </summary>
		/// <param name="level">The level before</param>
		/// <param name="raised">The level after</param>
		/// <returns>True when it did</returns>
		internal static bool LeveledUp(float level, float raised)
		{
			return(Mathf.Floor(raised) > Mathf.Floor(level));
		}

		/// <summary>
		/// Gets how fast a skill rises for a player, which a viking's rises at too
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <returns>Its step, 1 when the player has none for it</returns>
		private static float IncreaseStep(Skills.SkillType skill)
		{
			if (increaseSteps == null)
			{
				Skills skills = ((Game.instance != null && Game.instance.m_playerPrefab != null) ? Game.instance.m_playerPrefab.GetComponent<Skills>() : null);
				if (skills == null)
				{
					return(1f);
				}
				increaseSteps = new Dictionary<Skills.SkillType, float>();
				foreach (Skills.SkillDef def in skills.m_skills)
				{
					increaseSteps[def.m_skill] = def.m_increseStep;
				}
			}
			return(increaseSteps.TryGetValue(skill, out float step) ? step : 1f);
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
		/// Adds up the armor of the pieces the viking wears, with what its effects add, the way a player's adds up - all
		/// of it for a living or dvergr viking, and the config's ZombieArmorEffectiveness percent of it for a zombie
		/// viking
		/// </summary>
		/// <returns>Its armor</returns>
		public float BodyArmor()
		{
			float armor = 0f;
			foreach (ItemDrop.ItemData piece in WornArmor())
			{
				armor += piece.GetArmor();
			}
			humanoid.GetSEMan().ApplyArmorMods(ref armor);
			float effectiveness = (IsLivingOrDvergr(humanoid) ? 100f : VikingSettings.Get(VikingSettings.ZombieArmorEffectiveness, 100));
			return(armor * effectiveness / 100f);
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
		/// Wears down a random piece of the viking's armor by the damage a hit has left after its armor, taking it off
		/// when it breaks - the same as a player's armor, which stays on broken
		/// </summary>
		/// <param name="hit">The hit, after resistances and armor</param>
		public void WearArmor(HitData hit)
		{
			List<ItemDrop.ItemData> pieces = WornArmor();
			if (pieces.Count == 0)
			{
				return;
			}
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
			// Guarding against a deathsquito's dive, it holds the shield up until the sting is spent (see Guard)
			if (guarding)
			{
				humanoid.m_blocking = true;
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
		/// How a living viking meets a deathsquito diving at it - the diver's prefab, how near one after the viking is
		/// watched for, in meters, and how often, in seconds, how near it comes before the viking raises its shield and
		/// holds it, in meters, how long after its sting the viking has to strike back, in seconds, how fast it closes
		/// in on the viking to count as diving, in meters a second, and how long after it comes within reach a
		/// two-handed swing lands, in seconds
		/// </summary>
		private const string DiverPrefab = "Deathsquito";
		private const float DiverWatchRange = 15f;
		private const float DiverCheckInterval = 0.25f;
		private const float ShieldUpRange = 8f;
		private const float CounterWindow = 0.6f;
		private const float DiveSpeed = 3f;
		private const float SwingLate = 0.05f;

		/// <summary>
		/// How long a swing takes to land after it starts when the viking hasn't swung the weapon yet, in seconds, and
		/// how much each swing it sees moves what it remembers toward that swing's
		/// </summary>
		private const float SwingDelayDefault = 0.5f;
		private const float SwingDelayLearn = 0.3f;

		/// <summary>
		/// How long each weapon's swing takes to land after it starts, by the weapon's name, learned from living vikings'
		/// swings (see NoteSwing)
		/// </summary>
		private static readonly Dictionary<string, float> swingDelays = new Dictionary<string, float>();

		/// <summary>
		/// The deathsquito diving at the viking, whether it was stinging at the last update, whether the viking holds
		/// its shield up against it, until when it strikes back after a sting, in Time.time seconds, and when it next
		/// looks for one
		/// </summary>
		private Character diver;
		private bool diverWasStinging;
		private bool guarding;
		private float counterUntil = -1f;
		private float nextDiverCheck;

		/// <summary>
		/// Meets a deathsquito diving at a living viking, taking the AI's update over while it does - with a shield
		/// beside a one-handed weapon it raises the shield as the deathsquito comes within ShieldUpRange and holds it, then
		/// strikes back once the sting is spent, and with a two-handed weapon it stands its ground as the deathsquito
		/// dives and times its swing to land just after it comes within reach (see SwingNow). Its weapon is picked for
		/// this while one is after it (see DiveWeapon)
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True while it's meeting the dive, so the AI's own update is skipped</returns>
		public bool UpdateDiveDefense(float dt)
		{
			if (!IsLivingViking(humanoid) || ai == null || humanoid.IsDead() || humanoid.IsAttached() || GearHeld())
			{
				EndDiveDefense();
				return(false);
			}
			if (Time.time >= nextDiverCheck)
			{
				nextDiverCheck = Time.time + DiverCheckInterval;
				Character found = FindDiver();
				if (found != diver)
				{
					diver = found;
					guarding = false;
					counterUntil = -1f;
					EquipWeapon();
				}
			}
			if (diver == null || diver.IsDead())
			{
				EndDiveDefense();
				return(false);
			}
			ItemDrop.ItemData weapon = CurrentWeapon();
			if (weapon == null || IsRanged(weapon) || weapon.m_shared.m_attack == null)
			{
				guarding = false;
				return(false);
			}
			Vector3 center = diver.GetCenterPoint();
			Vector3 toward = humanoid.GetCenterPoint() - center;
			float distance = Mathf.Max(0f, toward.magnitude - diver.GetRadius());
			bool stinging = diver.InAttack();
			bool stung = (diverWasStinging && !stinging);
			diverWasStinging = stinging;
			float reach = MeleeReach(weapon.m_shared.m_attack);
			ItemDrop.ItemData left = humanoid.m_leftItem;
			if (left != null && left.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
			{
				return(Guard(center, distance, reach, stung));
			}
			if (weapon.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon)
			{
				float closing = ((toward.sqrMagnitude > 0.0001f) ? Vector3.Dot(diver.GetVelocity(), toward.normalized) : 0f);
				return(TimeSwing(weapon, center, distance, reach, closing));
			}
			return(false);
		}

		/// <summary>
		/// Holds the viking's shield up facing the deathsquito while it's within ShieldUpRange, and once its sting is
		/// spent, lowers the shield and strikes back within CounterWindow while it's in reach - leaving the AI to go
		/// after it when it's out of reach by then
		/// </summary>
		/// <param name="center">The deathsquito's middle</param>
		/// <param name="distance">How far it is, past its size, in meters</param>
		/// <param name="reach">How far the viking's weapon reaches, in meters</param>
		/// <param name="stung">Whether its sting has just ended</param>
		/// <returns>True while the viking guards or strikes back, so the AI's own update is skipped</returns>
		private bool Guard(Vector3 center, float distance, float reach, bool stung)
		{
			if (stung && distance <= ShieldUpRange)
			{
				counterUntil = Time.time + CounterWindow;
			}
			if (counterUntil >= 0f)
			{
				guarding = false;
				StopBlocking();
				if (humanoid.InAttack())
				{
					counterUntil = -1f;
					return(true);
				}
				if (Time.time >= counterUntil || distance > reach)
				{
					counterUntil = ((Time.time >= counterUntil) ? -1f : counterUntil);
					return(false);
				}
				ai.StopMoving();
				ai.LookAt(center);
				if (humanoid.StartAttack(diver, false))
				{
					counterUntil = -1f;
				}
				return(true);
			}
			if (humanoid.InAttack())
			{
				return(true);
			}
			if (distance > ShieldUpRange)
			{
				guarding = false;
				return(false);
			}
			guarding = true;
			ai.StopMoving();
			ai.LookAt(center);
			humanoid.m_blocking = true;
			return(true);
		}

		/// <summary>
		/// Stands the viking's ground with a two-handed weapon facing a deathsquito diving at it - closing in at
		/// DiveSpeed or faster within DiverWatchRange, or hovering within reach - and swings so the blow lands just after
		/// it comes within reach (see SwingNow), leaving the AI to go after one that's neither
		/// </summary>
		/// <param name="weapon">The viking's weapon</param>
		/// <param name="center">The deathsquito's middle</param>
		/// <param name="distance">How far it is, past its size, in meters</param>
		/// <param name="reach">How far the weapon reaches, in meters</param>
		/// <param name="closing">How fast it's closing in on the viking, in meters a second</param>
		/// <returns>True while the viking stands its ground or swings, so the AI's own update is skipped</returns>
		private bool TimeSwing(ItemDrop.ItemData weapon, Vector3 center, float distance, float reach, float closing)
		{
			guarding = false;
			if (humanoid.InAttack())
			{
				return(true);
			}
			bool diving = (closing >= DiveSpeed && distance <= DiverWatchRange);
			if (!diving && distance > reach)
			{
				return(false);
			}
			ai.StopMoving();
			ai.LookAt(center);
			if (SwingNow(distance, (diving ? closing : 0f), reach, SwingDelay(weapon), SwingLate))
			{
				humanoid.StartAttack(diver, false);
			}
			return(true);
		}

		/// <summary>
		/// Checks whether to start a swing at something coming in so the blow lands a moment after it comes within reach
		/// - now when it's already within reach, and otherwise once it's due within reach no later than the swing would
		/// land, less a moment
		/// </summary>
		/// <param name="distance">How far it is, in meters</param>
		/// <param name="closing">How fast it's closing in, in meters a second</param>
		/// <param name="reach">How far the swing reaches, in meters</param>
		/// <param name="delay">How long the swing takes to land after it starts, in seconds</param>
		/// <param name="late">How long after it comes within reach the blow should land, in seconds</param>
		/// <returns>True to swing now</returns>
		internal static bool SwingNow(float distance, float closing, float reach, float delay, float late)
		{
			if (distance <= reach)
			{
				return(true);
			}
			if (closing <= 0f)
			{
				return(false);
			}
			return((distance - reach) / closing <= delay - late);
		}

		/// <summary>
		/// Finds how long a weapon's swing takes to land after it starts, from living vikings' swings with it (see
		/// NoteSwing), or SwingDelayDefault before any
		/// </summary>
		/// <param name="weapon">The weapon</param>
		/// <returns>The time, in seconds</returns>
		private static float SwingDelay(ItemDrop.ItemData weapon)
		{
			return(swingDelays.TryGetValue(weapon.m_shared.m_name, out float delay) ? delay : SwingDelayDefault);
		}

		/// <summary>
		/// Notes how long a living viking's swing took to land after it started, moving what's remembered for the weapon
		/// SwingDelayLearn of the way toward it
		/// </summary>
		/// <param name="weapon">The weapon swung</param>
		/// <param name="delay">How long the swing took to land, in seconds</param>
		public static void NoteSwing(ItemDrop.ItemData weapon, float delay)
		{
			if (weapon == null || delay <= 0f)
			{
				return;
			}
			string name = weapon.m_shared.m_name;
			swingDelays[name] = (swingDelays.TryGetValue(name, out float known) ? Mathf.Lerp(known, delay, SwingDelayLearn) : delay);
		}

		/// <summary>
		/// Finds the nearest deathsquito within DiverWatchRange that's after the viking
		/// </summary>
		/// <returns>The deathsquito, or null when there's none</returns>
		private Character FindDiver()
		{
			List<Character> near = new List<Character>();
			Character.GetCharactersInRange(transform.position, DiverWatchRange, near);
			Character nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (Character other in near)
			{
				if (other == null || other.IsDead() || Utils.GetPrefabName(other.gameObject) != DiverPrefab)
				{
					continue;
				}
				BaseAI otherAI = other.GetBaseAI();
				if (otherAI == null || otherAI.GetTargetCreature() != humanoid)
				{
					continue;
				}
				float distance = Vector3.Distance(other.transform.position, transform.position);
				if (distance < nearestDistance)
				{
					nearest = other;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Picks the weapon a living viking meets a deathsquito with - its best one-handed melee weapon while it has a
		/// shield that works, so the shield goes beside it, or else its best two-handed melee weapon
		/// </summary>
		/// <returns>The weapon, or null when it has neither</returns>
		private ItemDrop.ItemData DiveWeapon()
		{
			if (ShieldWorks())
			{
				ItemDrop.ItemData oneHanded = BestMelee(ItemDrop.ItemData.ItemType.OneHandedWeapon, diver);
				if (oneHanded != null)
				{
					return(oneHanded);
				}
			}
			return(BestMelee(ItemDrop.ItemData.ItemType.TwoHandedWeapon, diver));
		}

		/// <summary>
		/// Finds the viking's melee weapon of one kind that hurts a creature the most
		/// </summary>
		/// <param name="type">The kind of weapon, like one-handed</param>
		/// <param name="target">The creature</param>
		/// <returns>The weapon, or null when it has none of that kind</returns>
		private ItemDrop.ItemData BestMelee(ItemDrop.ItemData.ItemType type, Character target)
		{
			ItemDrop.ItemData best = null;
			float bestDamage = 0f;
			HitData.DamageModifiers? resistances = Resistances(target);
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (item.m_shared.m_itemType != type || !CanUse(item) || IsRanged(item))
				{
					continue;
				}
				float damage = Damage(item, resistances);
				if (best == null || damage > bestDamage)
				{
					best = item;
					bestDamage = damage;
				}
			}
			return(best);
		}

		/// <summary>
		/// Stops meeting a deathsquito's dive - lowering the shield it held up against one and picking its weapon for
		/// the fight in front of it again
		/// </summary>
		private void EndDiveDefense()
		{
			bool had = (diver != null);
			diver = null;
			counterUntil = -1f;
			diverWasStinging = false;
			if (guarding)
			{
				guarding = false;
				StopBlocking();
			}
			if (had)
			{
				EquipWeapon();
			}
		}

		/// <summary>
		/// Reads the viking's data from its ZDO, or null before it's handed over
		/// </summary>
		/// <returns>The data</returns>
		public VikingData GetData()
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
			// The archer mark only gives a bow as the gear is first rolled, and left on it would go on counting the viking
			// as one with a bow after it's lost it (see IsArcher). It's set to false rather than removed, since a removal
			// isn't sent to the other clients
			if (nview.IsOwner() && zdo.GetBool(ArcherKey))
			{
				zdo.Set(ArcherKey, false);
			}
			Inventory inventory = humanoid.GetInventory();
			humanoid.UnequipAllItems();
			inventory.m_width = data.Width;
			inventory.m_height = data.Height;
			byte[] bytes = zdo.GetByteArray(InventoryKey);
			if (bytes != null)
			{
				inventory.Load(new ZPackage(bytes));
				// A prop it was holding for show when the last owner saved, like a hammer, isn't its own
				VikingItems.RemoveProps(inventory);
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
			// A living viking's shield is saved on its back rather than in its hand while its weapon is sheathed
			if (shield == null && IsLivingViking(humanoid))
			{
				shield = BestShield();
			}
			EquipWeapon();
			sheathed = false;
			SetHome();
			SetLooks(humanoid.m_visEquipment);
			float health = BaseHealth();
			LivingMaxHealth(ref health);
			bool unhurt = humanoid.GetHealth() >= humanoid.GetMaxHealth();
			if (health > 0f)
			{
				humanoid.SetMaxHealth(health);
				ZombieState.SetHealth(humanoid, health, unhurt);
			}
			bool night = (!data.Tombstone && ai != null && ai.DespawnInDay());
			humanoid.m_runSpeed = (night ? NightRunSpeed : RunSpeed);
			humanoid.m_speed = baseSpeed * (night ? NightSpeedScale : 1f);
			loaded = true;
		}

		/// <summary>
		/// Gets the viking's health from its data - a viking of the world's is multiplied by its stars like any
		/// creature's, and a tombstone viking's is the player's own. A living viking's biome and food set its health
		/// instead (see LivingMaxHealth)
		/// </summary>
		/// <returns>The health, or 0 before the viking's data is read</returns>
		public float BaseHealth()
		{
			return((data != null) ? data.MaxHealth * (data.Tombstone ? 1 : humanoid.GetLevel()) : 0f);
		}

		/// <summary>
		/// Sets a living viking's max health, which its biome and what it has eaten decide
		/// </summary>
		/// <param name="health">Its max health, set here</param>
		partial void LivingMaxHealth(ref float health);

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
			if (life != null)
			{
				life.OnHurt(attacker);
			}
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
		/// GrudgeRange for an enemy that has just hurt it - or, for a living viking in its village, too far from the
		/// village
		/// </summary>
		public void LeashTarget()
		{
			if (life != null && life.IsActive())
			{
				life.LeashTarget();
				return;
			}
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
			if (ai == null || !hasHome || !nview.IsOwner() || (ai is VikingAI vikingAI && vikingAI.Held()))
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
				// From where it lands, so the move home isn't taken for a fall
				humanoid.m_maxAirAltitude = landing.y;
				return(true);
			}
			ai.MoveTo(dt, home, 0f, true);
			return(true);
		}

		/// <summary>
		/// Runs a living viking's life in its village while it has nothing to fight, or keeps a viking with a home by it
		/// - walking straight back when it has strayed, and otherwise standing still, with a short stroll now and then
		/// </summary>
		/// <remarks>
		/// The game's wandering runs from spot to spot without pausing while a creature is alerted, which the viking is
		/// as it rises and for a while after each fight
		/// </remarks>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True when it's handled, so the game's wandering is skipped</returns>
		public bool UpdateIdle(float dt)
		{
			if (life != null && life.Idle(dt))
			{
				return(true);
			}
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
		/// the player's, everything a living viking has, or the coins and maybe some of the gear of a viking of the
		/// world or a dvergr viking, along with the wood and loot it carried from its life in a village, on the ground -
		/// or, for a living or dvergr viking that will rise, keeps everything for its zombie, which drops it in turn
		/// </summary>
		/// <param name="rises">Whether the viking's corpse will rise</param>
		public void DropGrave(bool rises)
		{
			if (!loaded)
			{
				Load();
			}
			if (!loaded)
			{
				return;
			}
			VikingItems.RemoveProps(humanoid.GetInventory());
			if (rises && IsLivingOrDvergr(humanoid))
			{
				// Its zombie takes up the shield it had equipped, and a shield sheathed on its back isn't
				if (shield != null && humanoid.GetInventory().ContainsItem(shield))
				{
					shield.m_equipped = true;
				}
				SaveInventory();
				return;
			}
			VikingItems.DropCarried(humanoid.GetInventory(), humanoid.GetCenterPoint());
			if (data.Tombstone)
			{
				SpawnGrave(humanoid.GetCenterPoint(), transform.rotation, humanoid.GetInventory(), data);
			}
			else if (IsLivingViking(humanoid))
			{
				DropEverything();
			}
			else
			{
				DropLoot();
			}
			humanoid.GetInventory().m_inventory.Clear();
		}

		/// <summary>
		/// Drops everything a living viking that stays dead has on the ground around it - all of its gear and what's in
		/// its inventory, ammo included, and nothing else, no coins or remains
		/// </summary>
		private void DropEverything()
		{
			Vector3 position = humanoid.GetCenterPoint();
			foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(humanoid.GetInventory().GetAllItems()))
			{
				if (item.m_dropPrefab == null)
				{
					continue;
				}
				item.m_equipped = false;
				ItemDrop.DropItem(item, item.m_stack, position + Random.insideUnitSphere * 0.5f, Quaternion.identity);
			}
		}

		/// <summary>
		/// Drops the coins, remains and maybe some of the gear of a zombie viking of the world or a dvergr viking, the
		/// way its kind's settings have it (see DropWorldLoot)
		/// </summary>
		partial void DropLoot();

		/// <summary>
		/// The most of its biome's remains a viking of the world drops, before its stars
		/// </summary>
		private const int RemainsMax = 2;

		/// <summary>
		/// Drops a viking of the world's coins and remains, more for each star, and each piece of its gear by a chance,
		/// up to a most, as it is
		/// </summary>
		/// <param name="gearDropChance">The percent chance each piece of its gear drops</param>
		/// <param name="gearDropMax">The most pieces of gear it drops</param>
		private void DropWorldLoot(float gearDropChance, int gearDropMax)
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
			while (pieces.Count > 0 && dropped < gearDropMax)
			{
				int index = Random.Range(0, pieces.Count);
				ItemDrop.ItemData piece = pieces[index];
				pieces.RemoveAt(index);
				if (Random.value * 100f >= gearDropChance)
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

		/// <summary>
		/// Checks whether a living viking leaves its gear as it is, while a player has its inventory open (see
		/// VikingInventory)
		/// </summary>
		/// <returns>True while it does</returns>
		private bool GearHeld()
		{
			return(life != null && life.GearHeld());
		}

		/// <summary>
		/// Wields the viking's weapon - what the player was wielding while it still works, or else its best one, or its
		/// fists when it has none left - with the player's shield beside a one-handed weapon, unless it's leaving its
		/// gear as it is (see GearHeld)
		/// </summary>
		/// <param name="force">True to wield it even while the viking leaves its gear as it is, for gear a player had
		/// it put on</param>
		public void EquipWeapon(bool force = false)
		{
			if (GetData() == null || (GearHeld() && !force))
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
			ReadyAmmo(choice);
		}

		/// <summary>
		/// Readies the ammo a bow or crossbow hurts the creature the viking is fighting most with (see BestAmmo), so its
		/// next shot is that
		/// </summary>
		/// <param name="weapon">The weapon in its hands, or null</param>
		private void ReadyAmmo(ItemDrop.ItemData weapon)
		{
			if (!IsRanged(weapon))
			{
				return;
			}
			Character target = ((ai != null) ? ai.GetTargetCreature() : null);
			ItemDrop.ItemData best = BestAmmo(weapon, Resistances(target));
			if (best != null && best != humanoid.GetAmmoItem())
			{
				humanoid.EquipItem(best, false);
			}
		}

		/// <summary>
		/// Has a living or dvergr viking walk while there's no danger and it's at home, and move at its full pace once
		/// there's danger - when it's alerted, has a target, or was hurt in the last few seconds - or it's away from its
		/// village's core, or it's climbing a slope or taking a run-up to a fence, so it makes it over
		/// </summary>
		/// <remarks>
		/// A creature moves at its jog whenever it isn't running, which for a viking built from the player is a quick
		/// trot. Walking beats both, and the AI never changes it, so it's set here every update
		/// </remarks>
		private void UpdateWalk()
		{
			if (ai == null || !IsLivingOrDvergr(humanoid))
			{
				return;
			}
			bool away = (life != null && life.AwayFromHome());
			bool hopping = (life != null && life.Hopping());
			humanoid.SetWalk(!InDanger() && !away && !hopping && !Climbing());
		}

		/// <summary>
		/// Raises a living or dvergr viking's Run and Swim skills for every RunSkillInterval seconds it spends running or
		/// swimming, the way a player's rise, and speeds its swimming by its Swim skill (see SwimSkillBonus)
		/// </summary>
		private void UpdateRunSkill()
		{
			if (!IsLivingOrDvergr(humanoid))
			{
				return;
			}
			if (baseSwimSpeed < 0f)
			{
				baseSwimSpeed = humanoid.m_swimSpeed;
			}
			float swim = SkillLevel(Skills.SkillType.Swim);
			humanoid.m_swimSpeed = baseSwimSpeed * SkillBoost(swim, SwimSkillBonus);
			bool moving = (humanoid.m_moveDir.sqrMagnitude >= 0.01f);
			if (moving && humanoid.IsSwimming())
			{
				swimSkillTimer += Time.deltaTime;
				if (swimSkillTimer > RunSkillInterval)
				{
					swimSkillTimer = 0f;
					RaiseSkill(Skills.SkillType.Swim, 1f);
				}
			}
			else if (moving && humanoid.IsRunning())
			{
				runSkillTimer += Time.deltaTime;
				if (runSkillTimer > RunSkillInterval)
				{
					runSkillTimer = 0f;
					RaiseSkill(Skills.SkillType.Run, 1f);
				}
			}
		}

		/// <summary>
		/// Checks whether a living viking is climbing a slope - on ground sloping at least ClimbAngle and heading up it -
		/// or was within the last ClimbHold seconds
		/// </summary>
		/// <returns>True when it is</returns>
		public bool Climbing()
		{
			if (IsClimbStep(humanoid.m_lastGroundNormal, humanoid.m_moveDir, humanoid.IsOnGround()))
			{
				climbUntil = Time.time + ClimbHold;
			}
			return(Time.time < climbUntil);
		}

		/// <summary>
		/// Checks whether a step is a climb - on the ground, which slopes at least ClimbAngle, heading up it at least
		/// ClimbHeading of the way
		/// </summary>
		/// <param name="normal">The ground's normal</param>
		/// <param name="moveDir">Which way the viking is moving, where only its way across the ground counts</param>
		/// <param name="onGround">Whether it's on the ground</param>
		/// <returns>True when it is</returns>
		internal static bool IsClimbStep(Vector3 normal, Vector3 moveDir, bool onGround)
		{
			Vector3 heading = moveDir;
			heading.y = 0f;
			Vector3 downhill = new Vector3(normal.x, 0f, normal.z);
			bool sloped = (onGround && Vector3.Angle(normal, Vector3.up) >= ClimbAngle);
			return(sloped && heading.sqrMagnitude > 0.01f && downhill.sqrMagnitude > 0.0001f && Vector3.Dot(heading.normalized, downhill.normalized) <= -ClimbHeading);
		}

		/// <summary>
		/// Checks whether a living viking is in danger - alerted, with a target, hurt in the last few seconds, waiting
		/// for a corpse to rise, or with a deathsquito after it
		/// </summary>
		/// <returns>True when it is</returns>
		private bool InDanger()
		{
			if ((life != null && life.IsWary()) || diver != null)
			{
				return(true);
			}
			return(ai != null && (ai.IsAlerted() || ai.GetTargetCreature() != null || ai.m_timeSinceHurt < DangerHurtTime));
		}

		/// <summary>
		/// Has a living viking sheath its weapon and shield on its back while there's no danger, and draw them again as
		/// soon as there is - a torch or a hammer it holds stays in its hands
		/// </summary>
		/// <remarks>
		/// The weapon and shield shown on its back are the ones it would draw, noted as the hands' hidden items the way
		/// a player's sheathed weapons are. Equipping a torch or hammer forgets the hidden items, so they're noted again
		/// whenever they've changed
		/// </remarks>
		/// <param name="force">True to sheath it even while the viking leaves its gear as it is, for gear a player had it
		/// put on</param>
		public void UpdateSheath(bool force = false)
		{
			if (ai == null || !IsLivingViking(humanoid) || GetData() == null || humanoid.IsDead() || (GearHeld() && !force))
			{
				return;
			}
			if (InDanger())
			{
				if (sheathed)
				{
					sheathed = false;
					humanoid.m_hiddenLeftItem = null;
					humanoid.m_hiddenRightItem = null;
					EquipWeapon();
					humanoid.SetupVisEquipment(humanoid.m_visEquipment, false);
					humanoid.m_zanim.SetTrigger("equip_hip");
				}
				return;
			}
			bool putAway = false;
			foreach (ItemDrop.ItemData held in new ItemDrop.ItemData[] { humanoid.m_rightItem, humanoid.m_leftItem })
			{
				if (IsArmament(held))
				{
					humanoid.UnequipItem(held, false);
					putAway = true;
				}
			}
			ItemDrop.ItemData weapon = ChooseWeapon();
			bool leftHanded = (weapon != null && weapon.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow);
			bool oneHanded = (weapon == null || weapon.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon);
			bool shieldWorks = (shield != null && (!shield.m_shared.m_useDurability || shield.m_durability > 0f) && humanoid.GetInventory().ContainsItem(shield));
			ItemDrop.ItemData backRight = (leftHanded ? null : weapon);
			ItemDrop.ItemData backLeft = (leftHanded ? weapon : ((oneHanded && shieldWorks) ? shield : null));
			if (!sheathed || putAway || humanoid.m_hiddenRightItem != backRight || humanoid.m_hiddenLeftItem != backLeft)
			{
				sheathed = true;
				humanoid.m_hiddenRightItem = backRight;
				humanoid.m_hiddenLeftItem = backLeft;
				humanoid.SetupVisEquipment(humanoid.m_visEquipment, false);
				if (putAway)
				{
					humanoid.m_zanim.SetTrigger("equip_hip");
				}
			}
		}

		/// <summary>
		/// Describes the viking's state and AI for the hover text in debug mode - what kind it is, its health, who runs
		/// it, what it's after and holding, and for a living viking its village life
		/// </summary>
		/// <remarks>
		/// Most of it is only known on the client that owns the viking, which runs its AI and life, so another client
		/// asks the owner for its summary while the viking is hovered and shows that once it arrives
		/// </remarks>
		/// <returns>The description, in gray text</returns>
		public string DebugSummary()
		{
			if (nview == null || !nview.IsValid() || nview.IsOwner())
			{
				return(OwnSummary("this client"));
			}
			AskOwner();
			if (Time.time - ownerAnsweredAt <= DebugAnswerLife && ownerSummary != null)
			{
				return(ownerSummary);
			}
			return(OwnSummary("another client, asking it for the rest"));
		}

		/// <summary>
		/// Names what the viking is doing, for its name in debug mode - from its own life on its owner, or from the
		/// owner's last answer on another client while the viking is hovered
		/// </summary>
		/// <returns>Its job, or null when it has none or isn't known here</returns>
		public string DebugJob()
		{
			if (nview == null || !nview.IsValid() || nview.IsOwner())
			{
				return(life?.DebugJob());
			}
			if (Time.time - ownerAnsweredAt <= DebugAnswerLife && ownerJob.Length > 0)
			{
				return(ownerJob);
			}
			return(null);
		}

		/// <summary>
		/// Asks the viking's owner for its debug summary, at most once a DebugAskInterval
		/// </summary>
		private void AskOwner()
		{
			if (Time.time - debugAskedAt < DebugAskInterval)
			{
				return;
			}
			debugAskedAt = Time.time;
			nview.InvokeRPC(nview.GetZDO().GetOwner(), DebugAskRpc);
		}

		/// <summary>
		/// Answers a client's ask with this client's debug summary of the viking and what it's doing, on its owner
		/// </summary>
		/// <param name="sender">The client that asked</param>
		private void RPC_DebugAsk(long sender)
		{
			if (!nview.IsOwner())
			{
				return;
			}
			Player player = Player.m_localPlayer;
			string owner = ((player != null) ? player.GetPlayerName() : "the server");
			nview.InvokeRPC(sender, DebugAnswerRpc, OwnSummary(owner), life?.DebugJob() ?? "");
		}

		/// <summary>
		/// Keeps the owner's answer to show in the hover text
		/// </summary>
		/// <param name="sender">The owner</param>
		/// <param name="summary">The owner's debug summary</param>
		/// <param name="job">What the viking is doing, or "" for nothing</param>
		private void RPC_DebugAnswer(long sender, string summary, string job)
		{
			ownerSummary = summary;
			ownerJob = job;
			ownerAnsweredAt = Time.time;
		}

		/// <summary>
		/// Describes the viking as this client knows it, for DebugSummary
		/// </summary>
		/// <param name="owner">Who runs the viking, for the Owner line</param>
		/// <returns>The description, in gray text</returns>
		private string OwnSummary(string owner)
		{
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			string kind = (IsLivingViking(humanoid) ? "Viking" : (IsDvergrViking(humanoid) ? "Dvergr viking" : ((GetData() != null && data.Tombstone) ? "Tombstone zombie viking" : "Zombie viking")));
			text.Append(kind + ", " + Mathf.CeilToInt(humanoid.GetHealth()) + "/" + Mathf.CeilToInt(humanoid.GetMaxHealth()) + " health\n");
			text.Append("Owner: " + owner + "\n");
			AddDebugLines(text);
			if (ai != null)
			{
				Character target = ai.GetTargetCreature();
				string targetText = ((target != null) ? target.GetHoverName() + " at " + Vector3.Distance(target.transform.position, transform.position).ToString("F1") + "m" : "none");
				text.Append("Target: " + targetText + "\n");
				List<string> flags = new List<string>();
				if (ai.IsAlerted())
				{
					flags.Add("alerted");
				}
				if (ai.IsAggravated())
				{
					flags.Add("aggravated");
				}
				if (ai.IsSleeping())
				{
					flags.Add("asleep");
				}
				if (IsLivingViking(humanoid))
				{
					flags.Add((InDanger() ? "in danger" : "calm") + ", " + (sheathed ? "sheathed" : "drawn"));
				}
				if (returningSince >= 0f)
				{
					flags.Add("running home");
				}
				if (flags.Count > 0)
				{
					text.Append(string.Join(", ", flags) + "\n");
				}
			}
			ItemDrop.ItemData right = humanoid.m_rightItem;
			ItemDrop.ItemData left = humanoid.m_leftItem;
			string hands = ((right != null) ? right.m_shared.m_name : "-") + " / " + ((left != null) ? left.m_shared.m_name : "-");
			text.Append("Hands: " + Localization.instance.Localize(hands) + "\n");
			if (life != null)
			{
				text.Append(life.DebugSummary());
			}
			return("<color=#C0C0C0>" + text.ToString().TrimEnd('\n') + "</color>");
		}

		/// <summary>
		/// Adds the lines only the viking's kind has to its debug summary, after its owner
		/// </summary>
		/// <param name="text">The summary so far</param>
		partial void AddDebugLines(System.Text.StringBuilder text);

		/// <summary>
		/// Names the viking for the log the way its kind knows it, for a kind whose name isn't the one in its data
		/// </summary>
		/// <param name="label">Its name from its data, set to the name to log</param>
		partial void NameForLog(ref string label);

		/// <summary>
		/// Finds the best shield a living viking has of its own - not one it carries for a chest - that isn't broken
		/// </summary>
		/// <returns>The shield, or null when it has none</returns>
		private ItemDrop.ItemData BestShield()
		{
			ItemDrop.ItemData best = null;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Shield || VikingItems.IsCarried(item) || (item.m_shared.m_useDurability && item.m_durability <= 0f))
				{
					continue;
				}
				if (best == null || item.GetBlockPower(0f) > best.GetBlockPower(0f))
				{
					best = item;
				}
			}
			return(best);
		}

		/// <summary>
		/// Has a living viking take up gear it picked up when it's better than its own - armor for a slot it has nothing
		/// in or more armor than what it wears there, a shield that blocks better, a utility item like a strength belt
		/// for an empty utility slot, or a weapon that deals more damage, counting its ammo, than its best of the same
		/// kind - melee, or a bow or crossbow - since every living viking carries one of each and picks between them as
		/// it fights (see ChooseWeapon). A dverger circlet, when it has none, it keeps for when it wants light
		/// </summary>
		/// <remarks>
		/// Armor, a shield and a weapon it takes up replace what it had of the kind, which it no longer wants. With no
		/// danger near, gear it wields goes on its back
		/// </remarks>
		/// <param name="item">The item, already in its inventory</param>
		/// <param name="replaced">The armor, shield or weapon it no longer wants, or null</param>
		/// <returns>True when it took the item up</returns>
		public bool TakeUpGear(ItemDrop.ItemData item, out ItemDrop.ItemData replaced)
		{
			replaced = null;
			if (item == null || GearHeld() || !humanoid.GetInventory().ContainsItem(item) || !WouldTakeUp(item))
			{
				return(false);
			}
			// A dverger circlet is kept for when the viking wants light (see UpdateTorch)
			if (IsCirclet(item))
			{
				SaveInventory();
				return(true);
			}
			ItemDrop.ItemData worn = WornIn(item.m_shared.m_itemType, out bool armor, item);
			if (armor)
			{
				humanoid.EquipItem(item, false);
				replaced = worn;
				SaveInventory();
				return(true);
			}
			if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Utility)
			{
				humanoid.EquipItem(item, false);
				SaveInventory();
				return(true);
			}
			if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
			{
				ItemDrop.ItemData old = (ShieldWorks() ? shield : null);
				if (old != null && humanoid.IsItemEquiped(old))
				{
					humanoid.UnequipItem(old, false);
				}
				shield = item;
				replaced = old;
				if (InDanger())
				{
					EquipWeapon();
				}
				SaveInventory();
				return(true);
			}
			replaced = BestOfKind(IsRanged(item), item);
			data.WeaponSlot = item.m_gridPos;
			nview.GetZDO().Set(DataKey, data.ToBytes());
			if (InDanger())
			{
				EquipWeapon();
			}
			SaveInventory();
			return(true);
		}

		/// <summary>
		/// Has a living viking put on a piece of gear a player picked for it, whether or not it's better than its own -
		/// armor or a utility item where it goes, ammo as what it shoots, and a shield or weapon in its hands the way a
		/// player wields one (see Draw), even while it leaves its gear as it is. Left to itself, it later switches to
		/// anything better it has (see VillageLife.UpdatePutOn)
		/// </summary>
		/// <param name="item">The piece, in its inventory</param>
		/// <returns>False when it isn't something the viking can put on</returns>
		public bool Wear(ItemDrop.ItemData item)
		{
			if (item == null || !IsLivingViking(humanoid) || GetData() == null || !humanoid.GetInventory().ContainsItem(item) || VikingItems.IsProp(item))
			{
				return(false);
			}
			ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
			WornIn(type, out bool armor);
			if (armor || type == ItemDrop.ItemData.ItemType.Utility || type == ItemDrop.ItemData.ItemType.Ammo)
			{
				humanoid.EquipItem(item, true);
				SaveInventory();
				return(true);
			}
			bool isShield = (type == ItemDrop.ItemData.ItemType.Shield);
			if (!isShield && (!item.IsWeapon() || type == ItemDrop.ItemData.ItemType.Torch))
			{
				return(false);
			}
			// Handed over by a player, it's the viking's own rather than loot it carries for a chest
			VikingItems.Keep(item);
			if (isShield)
			{
				shield = item;
			}
			else
			{
				data.WeaponSlot = item.m_gridPos;
				nview.GetZDO().Set(DataKey, data.ToBytes());
			}
			Draw(item);
			SaveInventory();
			return(true);
		}

		/// <summary>
		/// Has the viking take a weapon or shield a player had it put on into its hands, the way a player wields one,
		/// drawing what it had sheathed on its back - a shield beside a one-handed weapon, and its weapon beside a shield.
		/// It sheathes them again once there's no danger and it looks after its gear again (see UpdateSheath)
		/// </summary>
		/// <param name="item">The weapon or shield</param>
		private void Draw(ItemDrop.ItemData item)
		{
			bool wasSheathed = sheathed;
			sheathed = false;
			humanoid.m_hiddenLeftItem = null;
			humanoid.m_hiddenRightItem = null;
			if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
			{
				EquipWeapon(true);
			}
			if (!humanoid.IsItemEquiped(item))
			{
				humanoid.EquipItem(item, true);
			}
			if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon && ShieldWorks() && !humanoid.IsItemEquiped(shield))
			{
				humanoid.EquipItem(shield, false);
			}
			humanoid.SetupVisEquipment(humanoid.m_visEquipment, false);
			if (wasSheathed)
			{
				humanoid.m_zanim.SetTrigger("equip_hip");
			}
		}

		/// <summary>
		/// Finds the weapon the viking wields as a rule
		/// </summary>
		/// <returns>The weapon, or null when it has none</returns>
		public ItemDrop.ItemData MainWeaponItem()
		{
			return(MainWeapon());
		}

		/// <summary>
		/// Gets where the weapon the viking wields as a rule lies in its inventory
		/// </summary>
		/// <returns>The slot, or (-1, -1) when it has none noted</returns>
		public Vector2i WeaponSlot()
		{
			return((GetData() != null) ? data.WeaponSlot : new Vector2i(-1, -1));
		}

		/// <summary>
		/// Keeps the weapon the viking wields as a rule its own after it has moved within its inventory - by a player
		/// rearranging it, say - since it's noted by where it lies
		/// </summary>
		/// <param name="weapon">The weapon</param>
		public void FollowWeapon(ItemDrop.ItemData weapon)
		{
			if (weapon == null || GetData() == null || !humanoid.GetInventory().ContainsItem(weapon) || data.WeaponSlot == weapon.m_gridPos)
			{
				return;
			}
			data.WeaponSlot = weapon.m_gridPos;
			nview.GetZDO().Set(DataKey, data.ToBytes());
		}

		/// <summary>
		/// Checks whether the viking would take up a piece of gear it doesn't have yet, by the same rules as TakeUpGear -
		/// armor for a slot it has nothing in or more armor than what it wears there, a shield that blocks better, a
		/// utility item for an empty utility slot, or a weapon better than its best of the same kind. In a biome whose
		/// weather freezes, armor that resists frost comes first while nothing else it wears does, whatever its armor. A
		/// dverger circlet it would take up only when it has none, for its light
		/// </summary>
		/// <param name="item">The item, in its inventory or not</param>
		/// <returns>True when it would</returns>
		public bool WouldTakeUp(ItemDrop.ItemData item)
		{
			if (item == null || !IsLivingViking(humanoid) || GetData() == null || humanoid.IsDead())
			{
				return(false);
			}
			if (item.m_shared.m_useDurability && item.m_durability <= 0f)
			{
				return(false);
			}
			// A dverger circlet is wanted for its light, by a viking with none, rather than for its armor
			if (IsCirclet(item))
			{
				return(FindCirclet(item) == null);
			}
			ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
			ItemDrop.ItemData worn = WornIn(type, out bool armor, item);
			if (armor)
			{
				bool warm = VikingGear.KeepsOffFrost(item);
				bool wornWarm = VikingGear.KeepsOffFrost(worn);
				// Whether it freezes and what else keeps the frost off only matter between pieces that differ in it
				bool freezing = (warm != wornWarm && VikingGear.FreezesAt(transform.position));
				bool othersWarm = (freezing && OthersKeepOffFrost(worn));
				return(WantsArmor(warm, wornWarm, freezing, othersWarm, worn != null, item.GetArmor(), ((worn != null) ? worn.GetArmor() : 0f)));
			}
			if (type == ItemDrop.ItemData.ItemType.Shield)
			{
				return(!ShieldWorks() || item.GetBlockPower(0f) > shield.GetBlockPower(0f));
			}
			// A utility item that does something when worn, like a strength belt, goes in an empty utility slot - a viking
			// keeps to the one it wears, so a spare goes to a chest for another
			if (type == ItemDrop.ItemData.ItemType.Utility)
			{
				return(humanoid.m_utilityItem == null && item.m_shared.m_equipStatusEffect != null);
			}
			if (!item.IsWeapon() || type == ItemDrop.ItemData.ItemType.Torch)
			{
				return(false);
			}
			// A bow is weighed with the ammo it would shoot, and wanted before the viking has arrows for it
			if (!CanUse(item, false))
			{
				return(false);
			}
			ItemDrop.ItemData best = BestOfKind(IsRanged(item), item);
			return(best == null || Damage(item) > Damage(best));
		}

		/// <summary>
		/// Checks whether the viking would take up a piece of armor over what it wears in that slot - one with more armor,
		/// or in a slot it has nothing in. Where the weather freezes, a piece that keeps the frost off comes before armor
		/// while nothing else it wears does - one is taken up whatever its armor, and never given up for one that doesn't
		/// </summary>
		/// <param name="warm">Whether the piece keeps the frost off</param>
		/// <param name="wornWarm">Whether what it wears in the slot keeps the frost off, false when it wears nothing there</param>
		/// <param name="freezing">Whether the weather freezes where the viking is</param>
		/// <param name="othersWarm">Whether anything else it wears keeps the frost off</param>
		/// <param name="hasWorn">Whether it wears anything in the slot</param>
		/// <param name="armor">The piece's armor</param>
		/// <param name="wornArmor">The armor of what it wears in the slot</param>
		/// <returns>True when it would</returns>
		internal static bool WantsArmor(bool warm, bool wornWarm, bool freezing, bool othersWarm, bool hasWorn, float armor, float wornArmor)
		{
			if (warm != wornWarm && freezing && !othersWarm)
			{
				return(warm);
			}
			return(!hasWorn || armor > wornArmor);
		}

		/// <summary>
		/// Finds the viking's best weapon of a kind by the damage it deals, counting a bow or crossbow it has no ammo for
		/// yet
		/// </summary>
		/// <param name="ranged">True for bows and crossbows, false for melee weapons</param>
		/// <param name="except">A weapon to leave out, like the one being weighed against the rest, or null</param>
		/// <returns>The weapon, or null when it has none</returns>
		private ItemDrop.ItemData BestOfKind(bool ranged, ItemDrop.ItemData except)
		{
			ItemDrop.ItemData best = null;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (item == except || IsRanged(item) != ranged || !CanUse(item, false))
				{
					continue;
				}
				if (best == null || Damage(item) > Damage(best))
				{
					best = item;
				}
			}
			return(best);
		}

		/// <summary>
		/// Checks whether any armor or utility item the viking wears keeps the frost off, leaving one piece out - or the
		/// viking is immune to frost of itself, so it needs nothing to keep it off
		/// </summary>
		/// <param name="except">The piece to leave out, like one it would swap, or null</param>
		/// <returns>True when one does, or it's immune</returns>
		private bool OthersKeepOffFrost(ItemDrop.ItemData except)
		{
			if (humanoid.m_damageModifiers.m_frost == HitData.DamageModifier.Immune)
			{
				return(true);
			}
			foreach (ItemDrop.ItemData piece in new ItemDrop.ItemData[] { humanoid.m_chestItem, humanoid.m_legItem, humanoid.m_helmetItem, humanoid.m_shoulderItem, humanoid.m_utilityItem })
			{
				if (piece != null && piece != except && VikingGear.KeepsOffFrost(piece))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Finds the armor the viking wears in the slot an item of a type goes in - for the head, the helmet it wears
		/// when its dverger circlet is off, while the circlet is on in its place (see OwnHelmet)
		/// </summary>
		/// <param name="type">The item's type</param>
		/// <param name="armor">Set to whether the type is armor</param>
		/// <param name="except">The item being weighed up, left out of the helmets under a circlet, or null</param>
		/// <returns>What it wears there, or null when it wears nothing there or the type isn't armor</returns>
		private ItemDrop.ItemData WornIn(ItemDrop.ItemData.ItemType type, out bool armor, ItemDrop.ItemData except = null)
		{
			armor = true;
			if (type == ItemDrop.ItemData.ItemType.Helmet)
			{
				return(IsCirclet(humanoid.m_helmetItem) ? OwnHelmet(except) : humanoid.m_helmetItem);
			}
			if (type == ItemDrop.ItemData.ItemType.Chest)
			{
				return(humanoid.m_chestItem);
			}
			if (type == ItemDrop.ItemData.ItemType.Legs)
			{
				return(humanoid.m_legItem);
			}
			if (type == ItemDrop.ItemData.ItemType.Shoulder)
			{
				return(humanoid.m_shoulderItem);
			}
			armor = false;
			return(null);
		}

		/// <summary>
		/// Checks whether the viking has a shield that still works
		/// </summary>
		/// <returns>True when it has</returns>
		private bool ShieldWorks()
		{
			return(shield != null && humanoid.GetInventory().ContainsItem(shield) && (!shield.m_shared.m_useDurability || shield.m_durability > 0f));
		}

		/// <summary>
		/// Wields the viking's weapon when its AI picks one, unless it's a living viking with its weapon sheathed and no
		/// danger near
		/// </summary>
		public void PickWeapon()
		{
			if (sheathed && !InDanger())
			{
				return;
			}
			EquipWeapon();
		}

		/// <summary>
		/// Checks whether an item in the viking's hands is something it fights with - a weapon other than a torch, or
		/// a shield - rather than a torch or a prop
		/// </summary>
		/// <param name="item">The item, or null</param>
		/// <returns>True when it is</returns>
		private static bool IsArmament(ItemDrop.ItemData item)
		{
			if (item == null || VikingItems.IsProp(item))
			{
				return(false);
			}
			ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
			return(type == ItemDrop.ItemData.ItemType.Shield || (item.IsWeapon() && type != ItemDrop.ItemData.ItemType.Torch));
		}

		/// <summary>
		/// Has a living or dvergr viking pull out a torch when it's dark while it isn't fighting, or while it fights with
		/// a one-handed weapon and nothing in its other hand, and put it away otherwise - in its village from just before
		/// sunset until dawn while it's away from the fire and not resting, and elsewhere at night - and always by day
		/// </summary>
		/// <remarks>
		/// The torch goes in the hand beside a one-handed weapon, or in place of another weapon. When the viking spots an
		/// enemy its AI wields its weapon again, which puts the torch away for a shield or a weapon that takes both
		/// hands, and keeps it beside a one-handed weapon with no shield - so a viking woken to fight pulls one out
		/// beside a one-handed weapon too. It's a torch of its own, which burns down while it's held (see BurnTorch) - a
		/// viking with none goes without, and crafts another (see VillageLife.FindTorchWork). A viking with a dverger
		/// circlet of its own wears that in place of its helmet instead, and never pulls out a torch (see WearCirclet)
		/// </remarks>
		public void UpdateTorch()
		{
			if (!IsLivingOrDvergr(humanoid) || ai == null || GearHeld())
			{
				return;
			}
			ItemDrop.ItemData torch = FindTorch();
			bool night = ((life != null && life.IsActive()) ? life.WantsTorch() : EnvMan.IsNight());
			ItemDrop.ItemData circlet = FindCirclet(null);
			if (circlet != null)
			{
				if (torch != null && humanoid.IsItemEquiped(torch))
				{
					humanoid.UnequipItem(torch, false);
					if (InDanger())
					{
						EquipWeapon();
					}
				}
				WearCirclet(circlet, night && !InDanger());
				return;
			}
			bool fighting = (ai.IsAlerted() || ai.GetTargetCreature() != null);
			ItemDrop.ItemData right = humanoid.m_rightItem;
			bool handFree = (right != null && right.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon && humanoid.m_leftItem == null);
			if (night && (!fighting || handFree))
			{
				if (torch != null && !humanoid.IsItemEquiped(torch))
				{
					humanoid.EquipItem(torch, false);
				}
			}
			else if (!night && torch != null && humanoid.IsItemEquiped(torch))
			{
				humanoid.UnequipItem(torch, false);
				// With no danger the weapon stays sheathed on its back
				if (InDanger())
				{
					EquipWeapon();
				}
			}
		}

		/// <summary>
		/// Finds the viking's own torch that's burnt down the most, so it uses that one up first
		/// </summary>
		/// <returns>The torch, or null when it has none</returns>
		private ItemDrop.ItemData FindTorch()
		{
			ItemDrop.ItemData found = null;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch && !VikingItems.IsCarried(item) && (found == null || item.m_durability < found.m_durability))
				{
					found = item;
				}
			}
			return(found);
		}

		/// <summary>
		/// Checks whether an item is a dverger circlet, which a living viking wears for light in place of a torch
		/// </summary>
		/// <param name="item">The item, or null</param>
		/// <returns>True when it is</returns>
		public static bool IsCirclet(ItemDrop.ItemData item)
		{
			return(item != null && item.m_dropPrefab != null && item.m_dropPrefab.name == VikingGear.CircletPrefab);
		}

		/// <summary>
		/// Finds the viking's own dverger circlet - not one it carries for a chest - the one it wears when it has several
		/// </summary>
		/// <param name="except">A circlet to leave out, like one being weighed up, or null</param>
		/// <returns>The circlet, or null when it has none</returns>
		private ItemDrop.ItemData FindCirclet(ItemDrop.ItemData except)
		{
			if (IsCirclet(humanoid.m_helmetItem) && humanoid.m_helmetItem != except)
			{
				return(humanoid.m_helmetItem);
			}
			return(humanoid.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => item != except && IsCirclet(item) && !VikingItems.IsCarried(item) && !VikingItems.IsProp(item)));
		}

		/// <summary>
		/// Checks whether the viking has a dverger circlet of its own, which lights its way so it never needs a torch
		/// </summary>
		/// <returns>True when it has</returns>
		public bool HasCirclet()
		{
			return(FindCirclet(null) != null);
		}

		/// <summary>
		/// Finds the helmet of its own with the most armor that the viking wears when its dverger circlet is off - not one
		/// it carries for a chest
		/// </summary>
		/// <param name="except">A helmet to leave out, like one being weighed against it, or null</param>
		/// <returns>The helmet, or null when it has none</returns>
		private ItemDrop.ItemData OwnHelmet(ItemDrop.ItemData except)
		{
			ItemDrop.ItemData best = null;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (item == except || item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Helmet || IsCirclet(item) || VikingItems.IsCarried(item) || VikingItems.IsProp(item))
				{
					continue;
				}
				if (best == null || item.GetArmor() > best.GetArmor())
				{
					best = item;
				}
			}
			return(best);
		}

		/// <summary>
		/// Checks whether the viking puts an item on and takes it off as it needs light, rather than wearing whatever is
		/// best - its dverger circlet, and any helmet while the circlet is on in its place (see WearCirclet)
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it does</returns>
		public bool SwapsForLight(ItemDrop.ItemData item)
		{
			if (item == null)
			{
				return(false);
			}
			return(IsCirclet(item) || (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Helmet && IsCirclet(humanoid.m_helmetItem)));
		}

		/// <summary>
		/// Puts the viking's dverger circlet on in place of its helmet while it wants light, or takes it off and puts its
		/// helmet back on (see OwnHelmet)
		/// </summary>
		/// <param name="circlet">The circlet</param>
		/// <param name="lit">True to wear the circlet</param>
		private void WearCirclet(ItemDrop.ItemData circlet, bool lit)
		{
			if (lit)
			{
				if (!humanoid.IsItemEquiped(circlet))
				{
					humanoid.EquipItem(circlet, false);
				}
				return;
			}
			if (!humanoid.IsItemEquiped(circlet))
			{
				return;
			}
			humanoid.UnequipItem(circlet, false);
			ItemDrop.ItemData helmet = OwnHelmet(null);
			if (helmet != null)
			{
				humanoid.EquipItem(helmet, false);
			}
		}

		/// <summary>
		/// Has a living or dvergr viking wearing its dverger circlet put its helmet back on as soon as it's in danger,
		/// rather than at its next UpdateTorch
		/// </summary>
		private void UpdateCircletDanger()
		{
			if (!IsCirclet(humanoid.m_helmetItem) || !IsLivingOrDvergr(humanoid) || ai == null || GearHeld() || !InDanger())
			{
				return;
			}
			WearCirclet(humanoid.m_helmetItem, false);
		}

		/// <summary>
		/// Burns down the torch a living or dvergr viking holds the way a player's does, since the game only wears down
		/// a player's equipment - putting it away and dropping it from its inventory once it's burnt out
		/// </summary>
		private void BurnTorch()
		{
			if (!IsLivingOrDvergr(humanoid))
			{
				return;
			}
			ItemDrop.ItemData torch = ((humanoid.m_rightItem != null && humanoid.m_rightItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch) ? humanoid.m_rightItem : humanoid.m_leftItem);
			if (torch == null || torch.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Torch || !torch.m_shared.m_useDurability)
			{
				return;
			}
			humanoid.DrainEquipedItemDurability(torch, Time.deltaTime);
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
		/// How close an enemy comes before a living viking with a bow draws its melee weapon instead, and how far off it
		/// gets before the viking goes back to picking by the fight, in meters - apart, so it doesn't switch back and forth
		/// </summary>
		private const float MeleeDrawRange = 3f;
		private const float MeleeKeepRange = 6f;

		/// <summary>
		/// How much of its damage a bow or crossbow is counted for against a melee weapon, since it shoots more slowly
		/// than a melee weapon swings
		/// </summary>
		private const float RangedPace = 0.6f;

		/// <summary>
		/// Picks the weapon the viking should wield. A zombie viking wields the one the player was wielding while it
		/// still works, or else the one that deals the most damage. A living viking with both a melee weapon and a bow
		/// or crossbow with ammo picks between them for the fight in front of it (see ChooseForFight), and the bow or
		/// crossbow while it aims at a bird or at prey from afar (see VillageLife.AimingBow). Against a deathsquito diving
		/// at it, it picks a one-handed weapon to go beside its shield, or a two-handed one (see DiveWeapon)
		/// </summary>
		/// <returns>The weapon, or null for fists</returns>
		private ItemDrop.ItemData ChooseWeapon()
		{
			if (!IsLivingViking(humanoid))
			{
				if (data.WeaponSlot.x >= 0)
				{
					ItemDrop.ItemData held = humanoid.GetInventory().GetItemAt(data.WeaponSlot.x, data.WeaponSlot.y);
					if (held != null && CanUse(held))
					{
						return(held);
					}
				}
				return(BestWeapon(false));
			}
			// A deathsquito diving at it is met with a shield and a one-handed weapon, or a two-handed one (see
			// UpdateDiveDefense)
			if (diver != null && !diver.IsDead())
			{
				ItemDrop.ItemData diveWeapon = DiveWeapon();
				if (diveWeapon != null)
				{
					return(diveWeapon);
				}
			}
			// Each weapon is weighed against the enemy in front of it, through its resistances
			Character target = ((ai != null) ? ai.GetTargetCreature() : null);
			HitData.DamageModifiers? resistances = Resistances(target);
			ItemDrop.ItemData melee = BestWeapon(true, resistances);
			ItemDrop.ItemData ranged = BestRanged(resistances);
			if (melee == null || ranged == null)
			{
				return(melee ?? ranged);
			}
			// Aiming at a bird or at prey from afar, it shoots
			if (life != null && life.AimingBow())
			{
				return(ranged);
			}
			return(ChooseForFight(melee, ranged, target));
		}

		/// <summary>
		/// Picks between a living viking's melee weapon and its bow for the fight in front of it - the bow against a
		/// root, its melee weapon with its enemy up close (see CloseQuarters), the bow against an enemy it can't reach
		/// or get to through the crowd around it, or one it's keeping away from (see VillageLife.WantsBow), and the bow
		/// while it's below HurtRangedShare of its health, even one that hurts less - and otherwise the one that hurts
		/// most, by its damage through the enemy's resistances and the viking's skill with it
		/// </summary>
		/// <param name="melee">Its best melee weapon against the enemy</param>
		/// <param name="ranged">Its best bow or crossbow it has ammo for against the enemy</param>
		/// <param name="target">The enemy, or null</param>
		/// <returns>The weapon</returns>
		private ItemDrop.ItemData ChooseForFight(ItemDrop.ItemData melee, ItemDrop.ItemData ranged, Character target)
		{
			if (target != null)
			{
				if (MonsterThreat.IsRoot(target))
				{
					return(ranged);
				}
				if (CloseQuarters(target))
				{
					return(melee);
				}
				if (life != null && life.WantsBow(target))
				{
					return(ranged);
				}
				// Hurt, it would rather keep its distance than trade blows
				if (humanoid.GetHealthPercentage() < HurtRangedShare)
				{
					return(ranged);
				}
			}
			HitData.DamageModifiers? resistances = Resistances(target);
			return(PrefersRanged(Hurt(ranged, resistances), Hurt(melee, resistances)) ? ranged : melee);
		}

		/// <summary>
		/// The share of its health a living viking has to be below to shoot its enemy rather than fight it hand to hand
		/// </summary>
		private const float HurtRangedShare = 0.5f;

		/// <summary>
		/// Checks whether a bow or crossbow hurts more than a melee weapon, counting it for RangedPace of its damage since
		/// it shoots more slowly - the melee weapon when they come out even
		/// </summary>
		/// <param name="ranged">How hard the viking hits with the bow (see Hurt)</param>
		/// <param name="melee">How hard it hits with the melee weapon</param>
		/// <returns>True for the bow</returns>
		internal static bool PrefersRanged(float ranged, float melee)
		{
			return(ranged * RangedPace > melee);
		}

		/// <summary>
		/// Measures how hard the viking hits a creature with a weapon - its damage, counting its ammo, through the
		/// creature's resistances, scaled by the viking's skill with it the way a player's damage is, and a melee weapon
		/// by its biome's MeleeScale (see DamageScale)
		/// </summary>
		/// <param name="weapon">The weapon</param>
		/// <param name="resistances">The creature's resistances, or null for its damage as it is</param>
		/// <returns>The damage</returns>
		private float Hurt(ItemDrop.ItemData weapon, HitData.DamageModifiers? resistances)
		{
			float scale = ((!IsRanged(weapon) && GetData() != null && !data.Tombstone) ? VikingGear.Get(BiomeIndex()).MeleeScale : 1f);
			return(SkillScaled(Damage(weapon, resistances) * scale, SkillLevel(weapon.m_shared.m_skillType)));
		}

		/// <summary>
		/// The share of a weapon's damage a viking with no skill in it deals - the rest comes with skill, the same as a
		/// player's
		/// </summary>
		public const float SkillDamageBase = 0.4f;

		/// <summary>
		/// Works out the damage a viking deals with a skill, the way a player's is scaled by theirs - SkillDamageBase of
		/// it with no skill, rising to all of it at 100
		/// </summary>
		/// <param name="damage">The full damage</param>
		/// <param name="level">The viking's level in the skill, where below 0 counts as 0 and above 100 as 100</param>
		/// <returns>The damage it deals</returns>
		internal static float SkillScaled(float damage, float level)
		{
			float skill = Mathf.Clamp01(level / 100f);
			return(damage * (SkillDamageBase + (1f - SkillDamageBase) * skill));
		}

		/// <summary>
		/// Works out how much a skill raises what it boosts, the way a player's Run and Jump skills do - nothing with no
		/// skill, rising to the whole bonus at 100
		/// </summary>
		/// <param name="level">The viking's level in the skill, where below 0 counts as 0 and above 100 as 100</param>
		/// <param name="bonus">How much more it adds at full skill, like 0.4 for 40% more</param>
		/// <returns>The factor to multiply by, from 1 to 1 + bonus</returns>
		internal static float SkillBoost(float level, float bonus)
		{
			return(1f + Mathf.Clamp01(level / 100f) * bonus);
		}

		/// <summary>
		/// Finds the viking's best bow or crossbow it has ammo for, by the damage it deals with that ammo
		/// </summary>
		/// <param name="resistances">The resistances of the creature it's for, which the damage goes through, or null</param>
		/// <returns>The weapon, or null when it has none it can shoot</returns>
		private ItemDrop.ItemData BestRanged(HitData.DamageModifiers? resistances = null)
		{
			ItemDrop.ItemData best = null;
			float bestDamage = 0f;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (!IsRanged(item) || !CanUse(item))
				{
					continue;
				}
				float damage = Damage(item, resistances);
				if (best == null || damage > bestDamage)
				{
					best = item;
					bestDamage = damage;
				}
			}
			return(best);
		}

		/// <summary>
		/// Measures the hardest the viking hits a creature with any weapon it can fight with, by its damage through the
		/// creature's resistances and its skill, or with its fists when it has none
		/// </summary>
		/// <param name="target">The creature, or null for the damage as it is</param>
		/// <returns>The damage</returns>
		public float StrikeDamage(Character target = null)
		{
			return(Strike(Resistances(target), 1f));
		}

		/// <summary>
		/// Measures how much the viking deals a creature in the time a melee swing takes - its hardest hit (see
		/// StrikeDamage), counting a bow or crossbow at RangedPace of its damage, since it shoots more slowly
		/// </summary>
		/// <param name="resistances">The creature's resistances</param>
		/// <returns>The damage</returns>
		public float SwingDamage(HitData.DamageModifiers resistances)
		{
			return(Strike(resistances, RangedPace));
		}

		/// <summary>
		/// Measures the hardest the viking hits a creature with its best melee weapon, its best bow or crossbow or its
		/// fists, through the creature's resistances and its skill
		/// </summary>
		/// <param name="resistances">The creature's resistances, or null for the damage as it is</param>
		/// <param name="rangedShare">The share of a bow's or crossbow's damage it's counted for</param>
		/// <returns>The damage</returns>
		private float Strike(HitData.DamageModifiers? resistances, float rangedShare)
		{
			ItemDrop.ItemData melee = BestWeapon(true, resistances);
			ItemDrop.ItemData ranged = BestRanged(resistances);
			HitData.DamageTypes fists = new HitData.DamageTypes();
			fists.m_blunt = ((GetData() != null) ? VikingGear.Get(BiomeIndex()).Unarmed : 0f);
			float best = SkillScaled(CombatDamage(Resisted(fists, resistances)), SkillLevel(Skills.SkillType.Unarmed));
			if (melee != null)
			{
				best = Mathf.Max(best, Hurt(melee, resistances));
			}
			if (ranged != null)
			{
				best = Mathf.Max(best, Hurt(ranged, resistances) * rangedShare);
			}
			return(best);
		}

		/// <summary>
		/// Measures how much a working shield of the viking's holds back of a blocked hit - its block power at the
		/// viking's blocking skill, the way the game works it out as it blocks (see Humanoid.BlockAttack)
		/// </summary>
		/// <returns>The block power, or 0 with no shield that works</returns>
		public float BlockPower()
		{
			return(ShieldWorks() ? shield.GetBlockPower(Mathf.Clamp01(SkillLevel(Skills.SkillType.Blocking) / 100f)) : 0f);
		}

		/// <summary>
		/// Gets a creature's resistances, as the game puts a hit on it through them
		/// </summary>
		/// <param name="target">The creature, or null</param>
		/// <returns>Its resistances, or null with no creature</returns>
		private static HitData.DamageModifiers? Resistances(Character target)
		{
			return((target != null) ? target.GetDamageModifiers() : (HitData.DamageModifiers?)null);
		}

		/// <summary>
		/// How many more hits than the viking could take a monster may need to go down before it counts as tough
		/// </summary>
		private const float ToughMargin = 1f;

		/// <summary>
		/// Weighs a monster up against the viking - it's tough when it would take more of the viking's hardest hits to
		/// bring down than the viking could take of its hardest, through the viking's armor (see MonsterThreat.Hit), so
		/// a troll, bear or brute is tough for a viking in the Meadows' rags and flint, and not for one in the Plains'
		/// padded armor and black metal. A root, a player or a monster whose hit is unknown never is
		/// </summary>
		/// <param name="monster">The monster</param>
		/// <returns>True when it's tough</returns>
		public bool IsTough(Character monster)
		{
			if (monster == null || monster.IsPlayer() || MonsterThreat.IsRoot(monster) || GetData() == null)
			{
				return(false);
			}
			float hit = MonsterThreat.Hit(monster);
			if (hit <= 0f)
			{
				return(false);
			}
			// Its hits are weighed through the monster's resistances, so one that shrugs off its weapons is tougher for it
			return(Outlasts(monster.GetMaxHealth(), hit, humanoid.GetMaxHealth(), BodyArmor(), StrikeDamage(monster)));
		}

		/// <summary>
		/// Checks whether a monster outlasts a viking - it would take more of the viking's hardest hits to bring down
		/// than ToughMargin times as many of the monster's hardest as the viking could take, through its armor. Each
		/// side's hit counts for at least 1, so neither takes endless hits
		/// </summary>
		/// <param name="monsterHealth">The monster's health</param>
		/// <param name="monsterHit">The monster's hardest hit, before armor</param>
		/// <param name="vikingHealth">The viking's health</param>
		/// <param name="vikingArmor">The viking's armor</param>
		/// <param name="vikingStrike">The viking's hardest hit (see StrikeDamage)</param>
		/// <returns>True when it does, so the monster is tough</returns>
		internal static bool Outlasts(float monsterHealth, float monsterHit, float vikingHealth, float vikingArmor, float vikingStrike)
		{
			return(Odds(monsterHealth, monsterHit, vikingHealth, vikingArmor, vikingStrike) > 1f);
		}

		/// <summary>
		/// Works out how far a monster outlasts a viking - how many of the viking's hardest hits it takes to bring down,
		/// over ToughMargin times as many of the monster's hardest as the viking could take, through its armor. Each
		/// side's hit counts for at least 1, so neither takes endless hits
		/// </summary>
		/// <param name="monsterHealth">The monster's health</param>
		/// <param name="monsterHit">The monster's hardest hit, before armor</param>
		/// <param name="vikingHealth">The viking's health</param>
		/// <param name="vikingArmor">The viking's armor</param>
		/// <param name="vikingStrike">The viking's hardest hit (see StrikeDamage)</param>
		/// <returns>Above 1 when the monster outlasts the viking, as many times over as it does</returns>
		internal static float Odds(float monsterHealth, float monsterHit, float vikingHealth, float vikingArmor, float vikingStrike)
		{
			float hitsToKill = monsterHealth / Mathf.Max(1f, vikingStrike);
			float taken = Mathf.Max(1f, HitData.DamageTypes.ApplyArmor(monsterHit, vikingArmor));
			float hitsToDie = vikingHealth / taken;
			return(hitsToKill / (hitsToDie * ToughMargin));
		}

		/// <summary>
		/// How many times as many vikings as would bring a tough monster down just before it brings one of them down
		/// gather before they charge it together, so a few to spare are in on it
		/// </summary>
		private const float GroupMargin = 1.5f;

		/// <summary>
		/// Works out how many vikings it takes to fight a monster hand to hand together - 1 for one that isn't tough
		/// (see IsTough), and otherwise enough like this viking, GroupMargin over, that their hits bring it down from
		/// the health it has left before it could bring the first of them down
		/// </summary>
		/// <param name="monster">The monster</param>
		/// <returns>How many vikings, this one among them</returns>
		public int FightersNeeded(Character monster)
		{
			if (!IsTough(monster))
			{
				return(1);
			}
			return(FightersNeeded(monster.GetHealth(), MonsterThreat.Hit(monster), humanoid.GetMaxHealth(), BodyArmor(), StrikeDamage(monster)));
		}

		/// <summary>
		/// Works out how many vikings alike it takes to fight a monster hand to hand together - 1 when it doesn't
		/// outlast one of them (see Odds), and otherwise as many times as it does, GroupMargin over, rounded up
		/// </summary>
		/// <param name="monsterHealth">The monster's health</param>
		/// <param name="monsterHit">The monster's hardest hit, before armor</param>
		/// <param name="vikingHealth">A viking's health</param>
		/// <param name="vikingArmor">A viking's armor</param>
		/// <param name="vikingStrike">A viking's hardest hit (see StrikeDamage)</param>
		/// <returns>How many vikings</returns>
		internal static int FightersNeeded(float monsterHealth, float monsterHit, float vikingHealth, float vikingArmor, float vikingStrike)
		{
			float odds = Odds(monsterHealth, monsterHit, vikingHealth, vikingArmor, vikingStrike);
			return((odds <= 1f) ? 1 : Mathf.CeilToInt(odds * GroupMargin));
		}

		/// <summary>
		/// Checks whether the viking hangs on to an item of its own when it lightens its load - what it wears or has on
		/// its back, its shield, its best melee weapon and bow, the ammo its bow shoots, its torch, and its dverger
		/// circlet and the helmet it wears when the circlet is off
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it does</returns>
		public bool Keeps(ItemDrop.ItemData item)
		{
			if (item == null || humanoid.IsItemEquiped(item) || item == humanoid.m_hiddenRightItem || item == humanoid.m_hiddenLeftItem || item == shield)
			{
				return(true);
			}
			ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
			if (type == ItemDrop.ItemData.ItemType.Torch || IsCirclet(item) || (IsCirclet(humanoid.m_helmetItem) && item == OwnHelmet(null)))
			{
				return(true);
			}
			ItemDrop.ItemData bow = BestOfKind(true, null);
			if (type == ItemDrop.ItemData.ItemType.Ammo && bow != null && item.m_shared.m_ammoType == bow.m_shared.m_ammoType)
			{
				return(true);
			}
			return(item == bow || item == BestOfKind(false, null));
		}

		/// <summary>
		/// Measures the viking's best melee weapon by the damage it deals
		/// </summary>
		/// <returns>The damage, or 0 when it fights with its fists</returns>
		public float MeleeDamage()
		{
			ItemDrop.ItemData weapon = BestWeapon(true);
			return((weapon != null) ? Damage(weapon) : 0f);
		}

		/// <summary>
		/// Finds the weapon the viking can fight with that deals the most damage
		/// </summary>
		/// <param name="melee">True to leave out bows and crossbows</param>
		/// <param name="resistances">The resistances of the creature it's for, which the damage goes through - so a
		/// weapon of what it's weak to over one of what it resists - or null</param>
		/// <returns>The weapon, or null when it has none</returns>
		private ItemDrop.ItemData BestWeapon(bool melee, HitData.DamageModifiers? resistances = null)
		{
			ItemDrop.ItemData best = null;
			float bestDamage = 0f;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (!CanUse(item) || (melee && IsRanged(item)))
				{
					continue;
				}
				float damage = Damage(item, resistances);
				if (best == null || damage > bestDamage)
				{
					best = item;
					bestDamage = damage;
				}
			}
			return(best);
		}

		/// <summary>
		/// Checks whether the viking's target is close enough to fight hand to hand - within MeleeDrawRange, or
		/// MeleeKeepRange while it already has a melee weapon in its hands
		/// </summary>
		/// <param name="target">The target</param>
		/// <returns>True when it is</returns>
		private bool CloseQuarters(Character target)
		{
			ItemDrop.ItemData current = CurrentWeapon();
			float range = ((current != null && !IsRanged(current)) ? MeleeKeepRange : MeleeDrawRange);
			return(Utils.DistanceXZ(target.transform.position, transform.position) <= range);
		}

		/// <summary>
		/// Checks whether the viking can fight with an item - a weapon of its own, not loot it carries or a tool it holds
		/// for show, other than a torch that isn't broken, has ammo when it takes ammo, doesn't need eitr, which the viking has none of, and
		/// deals damage to more than tamed creatures. A living viking keeps a pickaxe for mining and never fights with one
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it can</returns>
		private bool CanUse(ItemDrop.ItemData item)
		{
			return(CanUse(item, true));
		}

		/// <summary>
		/// Checks whether an item is a pickaxe - one that mines with the Pickaxes skill
		/// </summary>
		/// <param name="item">The item, or null</param>
		/// <returns>True when it is</returns>
		public static bool IsPickaxe(ItemDrop.ItemData item)
		{
			return(item != null && item.m_shared.m_skillType == Skills.SkillType.Pickaxes && item.m_shared.m_damages.m_pickaxe > 0f);
		}

		/// <summary>
		/// Checks whether the viking can fight with an item, or could once it has ammo for it
		/// </summary>
		/// <param name="item">The item</param>
		/// <param name="needAmmo">False to leave out whether it has ammo for a weapon that takes ammo</param>
		/// <returns>True when it can</returns>
		private bool CanUse(ItemDrop.ItemData item, bool needAmmo)
		{
			if (!item.IsWeapon() || item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch || VikingItems.IsCarried(item) || VikingItems.IsProp(item))
			{
				return(false);
			}
			// A weapon that only hurts tamed creatures, like the butcher knife, can't hurt the viking's enemies (see
			// Attack.DoMeleeAttack and DoAreaAttack)
			if (item.m_shared.m_tamedOnly)
			{
				return(false);
			}
			if (IsPickaxe(item) && IsLivingViking(humanoid))
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
				return(!needAmmo);
			}
			return(Damage(item) > 0f);
		}

		/// <summary>
		/// Checks whether a weapon fights from afar - a bow or crossbow, which shoots ammo
		/// </summary>
		/// <param name="item">The weapon, or null</param>
		/// <returns>True when it does</returns>
		public static bool IsRanged(ItemDrop.ItemData item)
		{
			return(item != null && !string.IsNullOrEmpty(item.m_shared.m_ammoType) && item.m_shared.m_attack != null && item.m_shared.m_attack.m_attackType == Attack.AttackType.Projectile);
		}

		/// <summary>
		/// Finds the weapon the viking fights with as a rule - the one it's set to wield while it isn't broken, even
		/// while it's out of ammo, or else the one it would wield now
		/// </summary>
		/// <returns>The weapon, or null for its fists</returns>
		private ItemDrop.ItemData MainWeapon()
		{
			if (GetData() == null)
			{
				return(null);
			}
			if (data.WeaponSlot.x >= 0)
			{
				ItemDrop.ItemData held = humanoid.GetInventory().GetItemAt(data.WeaponSlot.x, data.WeaponSlot.y);
				if (held != null && CanUse(held, false))
				{
					return(held);
				}
			}
			return(ChooseWeapon());
		}

		/// <summary>
		/// Checks whether the viking can shoot - a living viking with a bow or crossbow it has ammo for, which it picks
		/// up as the fight calls for it, or a zombie viking that fights with one as a rule
		/// </summary>
		/// <returns>True when it can</returns>
		public bool IsArcher()
		{
			return(IsLivingViking(humanoid) ? (BestRanged() != null) : IsRanged(MainWeapon()));
		}

		/// <summary>
		/// Finds the bow or crossbow the viking shoots with - the one in its hands, or else its best one it has ammo for
		/// </summary>
		/// <returns>The weapon, or null when it has none</returns>
		public ItemDrop.ItemData Bow()
		{
			ItemDrop.ItemData current = CurrentWeapon();
			return((current != null && IsRanged(current)) ? current : BestRanged());
		}

		/// <summary>
		/// Rates a piece of gear against others of its kind, by the same measures it's taken up by (see WouldTakeUp) - a
		/// shield by how well it blocks, a weapon by the damage it deals with the ammo it would shoot, and armor by how
		/// much armor it gives
		/// </summary>
		/// <param name="item">The piece</param>
		/// <returns>Its rating</returns>
		public float GearValue(ItemDrop.ItemData item)
		{
			if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
			{
				return(item.GetBlockPower(0f));
			}
			return(item.IsWeapon() ? Damage(item) : item.GetArmor());
		}

		/// <summary>
		/// Adds up the damage a weapon deals to creatures, with the ammo it would shoot
		/// </summary>
		/// <param name="weapon">The weapon</param>
		/// <returns>Its damage, leaving out chopping and mining</returns>
		private float Damage(ItemDrop.ItemData weapon)
		{
			return(Damage(weapon, null));
		}

		/// <summary>
		/// Adds up the damage a weapon deals to a creature, with the ammo it would shoot at it (see BestAmmo), through
		/// the creature's resistances the way the game applies them - more of what it's weak to, less of what it resists
		/// and none of what it's immune to, its armor and status effects included - or as it is with no creature
		/// </summary>
		/// <param name="weapon">The weapon</param>
		/// <param name="resistances">The creature's resistances, or null</param>
		/// <returns>Its damage, leaving out chopping and mining</returns>
		private float Damage(ItemDrop.ItemData weapon, HitData.DamageModifiers? resistances)
		{
			HitData.DamageTypes damage = weapon.GetDamage();
			ItemDrop.ItemData ammo = (BestAmmo(weapon, resistances) ?? FindAmmo(weapon));
			if (ammo != null)
			{
				damage.Add(ammo.GetDamage());
			}
			return(CombatDamage(Resisted(damage, resistances)));
		}

		/// <summary>
		/// Finds the ammo the viking has that a bow or crossbow hurts a creature most with, along with the weapon's own
		/// damage, through the creature's resistances - or with no creature, the ammo that hurts the living most, which
		/// are immune to spirit damage, so silver arrows are kept for the dead. Of ammo that hurts as much, the one with
		/// the least spirit damage
		/// </summary>
		/// <param name="weapon">The weapon</param>
		/// <param name="resistances">The creature's resistances, or null with no creature</param>
		/// <returns>The ammo, or null when the weapon takes none or the viking has none it can ready</returns>
		private ItemDrop.ItemData BestAmmo(ItemDrop.ItemData weapon, HitData.DamageModifiers? resistances)
		{
			string ammoType = weapon.m_shared.m_ammoType;
			if (string.IsNullOrEmpty(ammoType))
			{
				return(null);
			}
			HitData.DamageTypes own = weapon.GetDamage();
			ItemDrop.ItemData best = null;
			float bestDamage = 0f;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo || item.m_shared.m_ammoType != ammoType || VikingItems.IsCarried(item) || VikingItems.IsProp(item))
				{
					continue;
				}
				HitData.DamageTypes damage = own;
				damage.Add(item.GetDamage());
				if (resistances == null)
				{
					damage.m_spirit = 0f;
				}
				float hurt = CombatDamage(Resisted(damage, resistances));
				if (best == null || hurt > bestDamage || (hurt == bestDamage && item.GetDamage().m_spirit < best.GetDamage().m_spirit))
				{
					best = item;
					bestDamage = hurt;
				}
			}
			return(best);
		}

		/// <summary>
		/// Puts damage through a creature's resistances, the way the game does as it's hit
		/// </summary>
		/// <param name="damage">The damage</param>
		/// <param name="resistances">The creature's resistances, or null to leave the damage as it is</param>
		/// <returns>The damage it would take</returns>
		private static HitData.DamageTypes Resisted(HitData.DamageTypes damage, HitData.DamageModifiers? resistances)
		{
			if (resistances == null)
			{
				return(damage);
			}
			HitData hit = new HitData();
			hit.m_damage = damage;
			hit.ApplyResistance(resistances.Value, out HitData.DamageModifier _);
			return(hit.m_damage);
		}

		/// <summary>
		/// Adds up the damage an item deals to creatures
		/// </summary>
		/// <param name="item">The weapon or ammo</param>
		/// <returns>Its damage, leaving out chopping and mining</returns>
		private static float CombatDamage(ItemDrop.ItemData item)
		{
			return(CombatDamage(item.GetDamage()));
		}

		/// <summary>
		/// Adds up damage that hurts creatures
		/// </summary>
		/// <param name="damage">The damage</param>
		/// <returns>Its total, leaving out chopping and mining</returns>
		private static float CombatDamage(HitData.DamageTypes damage)
		{
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
		/// Gives the weapon in the viking's hands an AI that knows its reach for the length of its AI's update - a ranged
		/// weapon an archer's range and pace, and a melee weapon, fists included, the reach of its swing (see MeleeReach),
		/// so a knife strikes from up close and a polearm from farther off, and for a living viking, a player's pace, with
		/// no wait between swings
		/// </summary>
		/// <remarks>
		/// A player's weapons share their settings with every copy of the item and only the AI reads these ones, so
		/// they're changed just while this viking's AI runs and put back by RestoreWeaponAI. The AI strikes once its
		/// enemy is within the weapon's AI range, which a player's weapons don't set to the reach of their swing
		/// </remarks>
		public void TuneWeaponAI()
		{
			RestoreWeaponAI();
			ItemDrop.ItemData weapon = humanoid.GetCurrentWeapon();
			Attack attack = ((weapon != null) ? weapon.m_shared.m_attack : null);
			if (attack == null)
			{
				return;
			}
			Attack.AttackType type = attack.m_attackType;
			bool ranged = (type == Attack.AttackType.Projectile && CurrentWeapon() != null);
			bool melee = (type == Attack.AttackType.Horizontal || type == Attack.AttackType.Vertical || type == Attack.AttackType.Area);
			if (!ranged && !melee)
			{
				return;
			}
			ItemDrop.ItemData.SharedData shared = weapon.m_shared;
			tunedShared = shared;
			tunedRange = shared.m_aiAttackRange;
			tunedRangeMin = shared.m_aiAttackRangeMin;
			tunedAngle = shared.m_aiAttackMaxAngle;
			tunedInterval = shared.m_aiAttackInterval;
			if (melee)
			{
				shared.m_aiAttackRange = MeleeReach(attack);
				// A living viking swings as fast as a player does - again as soon as its last swing ends, which carries
				// on the weapon's combo - rather than waiting out the AI's interval between attacks
				if (IsLivingViking(humanoid))
				{
					shared.m_aiAttackInterval = 0f;
				}
				return;
			}
			shared.m_aiAttackRange = RangedAttackRange;
			shared.m_aiAttackRangeMin = 0f;
			shared.m_aiAttackMaxAngle = RangedAttackAngle;
			shared.m_aiAttackInterval = (shared.m_attack.m_requiresReload ? ReloadAttackInterval : RangedAttackInterval);
		}

		/// <summary>
		/// Works out how near an enemy has to be, past its own radius, for a melee swing to reach it - the attack's range,
		/// how far a swing reaches, or for one that strikes all around a point ahead, like a sledge's, how far ahead that
		/// point is, so the blow lands on the enemy - less a margin (see MeleeReachShare), since the AI measures from the
		/// viking's middle and the swing from its hand
		/// </summary>
		/// <param name="attack">The weapon's attack</param>
		/// <returns>The reach, in meters</returns>
		private static float MeleeReach(Attack attack)
		{
			return(Mathf.Max(MeleeReachMin, attack.m_attackRange * MeleeReachShare));
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
		/// Has a living or dvergr viking use a forsaken power now - its animation, then the power for it, the creatures
		/// of its faction that aren't angry at players and the players it isn't hostile to around it (see UsePower)
		/// </summary>
		/// <param name="power">The power's status effect name hash</param>
		public void StartPower(int power)
		{
			StartCoroutine(UsePower(power, true));
		}

		/// <summary>
		/// Plays the forsaken power animation, then gives the power to the viking and the creatures of its faction around
		/// it that aren't angry at players - for a living or dvergr viking, the dvergr and the other vikings like it - and,
		/// for one of those, the players around it it isn't hostile to, the way a player's reaches the players near them
		/// </summary>
		/// <param name="power">The power's status effect name hash</param>
		/// <param name="sharePlayers">True to give it to the players around it too</param>
		/// <returns>The coroutine</returns>
		private IEnumerator UsePower(int power, bool sharePlayers)
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
				BaseAI allyAI = character.GetBaseAI();
				bool angry = (allyAI != null && allyAI.IsAggravated());
				bool ally = (!character.IsPlayer() && !character.IsDead() && !angry && character.GetFaction() == humanoid.GetFaction());
				bool friend = (sharePlayers && character.IsPlayer() && !character.IsDead() && !BaseAI.IsEnemy(humanoid, character));
				if (character == humanoid || ally || friend)
				{
					character.GetSEMan().AddStatusEffect(power, true);
				}
			}
		}
	}
}
