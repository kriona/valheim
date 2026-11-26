using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kriona.Vikings
{
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
		/// Checks whether this mod's vikings wear their armor the way a player does, to the whole of a hit - poison, fire
		/// and spirit included - rather than only to the damage it deals at once (see VikingPlayerArmorPatch)
		/// </summary>
		/// <returns>True when they do</returns>
		public static partial bool WearsArmorLikePlayer();

		/// <summary>
		/// Gets the name of the viking prefab this mod adds
		/// </summary>
		/// <returns>Its name, or "" before it's built</returns>
		public static string OwnPrefabName()
		{
			return((prefab != null) ? prefab.name : "");
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
