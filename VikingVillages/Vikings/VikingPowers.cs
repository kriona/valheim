using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A living viking's forsaken powers - the ones it knows, which are its biome's and those of every biome before it
	/// along with any it has been given by a player or another viking, and using them the way a player does, each use
	/// putting every power out of reach for the power's cooldown
	/// </summary>
	/// <remarks>
	/// What it knows, when its cooldown ends, which powers it's under and the power it's in the act of using are kept in
	/// its ZDO, so every client sees them and they carry over to whichever client owns the viking next. A power is used
	/// for a fight when there's more than one enemy and not only the Meadows' creatures (see FightPowers), as a job takes
	/// the viking out of the village or has it foraging, and when it's hurt (see WorkPowers) - only one viking using a
	/// power at a time, and one already under it only when at least 2 vikings near it aren't
	/// </remarks>
	internal class VikingPowers
	{
		/// <summary>
		/// The forsaken powers by status effect name, in the order their bosses are beaten - each the power a viking of
		/// the next biome along knows, so a Black Forest viking knows Eikthyr's and a Plains viking every power up to
		/// Moder's (see VikingGear.Biomes)
		/// </summary>
		internal static readonly string[] Names = { "GP_Eikthyr", "GP_TheElder", "GP_Bonemass", "GP_Moder", "GP_Yagluth", "GP_Queen", "GP_Fader" };
		internal const int Eikthyr = 0;
		internal const int Elder = 1;
		internal const int Bonemass = 2;
		internal const int Moder = 3;
		internal const int Yagluth = 4;
		internal const int Queen = 5;
		internal const int Fader = 6;

		/// <summary>
		/// The powers' status effect name hashes, in the order of Names
		/// </summary>
		private static readonly int[] Hashes = HashNames();

		/// <summary>
		/// On a living viking, the powers it has learned beyond its biome's, one bit for each in the order of Names
		/// </summary>
		private static readonly int LearnedKey = "kriona.VikingVillages.vikingPowersLearned".GetStableHashCode();

		/// <summary>
		/// On a living viking, the powers it's under, one bit for each, as its owner last saw them
		/// </summary>
		private static readonly int UnderKey = "kriona.VikingVillages.vikingPowersUnder".GetStableHashCode();

		/// <summary>
		/// On a living viking, when it may use a power again, in ZNet time ticks
		/// </summary>
		private static readonly int ReadyKey = "kriona.VikingVillages.vikingPowerReady".GetStableHashCode();

		/// <summary>
		/// On a living viking, the power it's in the act of using, as its index in Names plus one, or 0, and until when it
		/// is, in ZNet time ticks
		/// </summary>
		private static readonly int UsingKey = "kriona.VikingVillages.vikingPowerUsing".GetStableHashCode();
		private static readonly int UsingUntilKey = "kriona.VikingVillages.vikingPowerUsingUntil".GetStableHashCode();

		/// <summary>
		/// On a living viking, the viking it's on its way to teach a power, and until when that holds, in ZNet time ticks
		/// </summary>
		private const string PupilKey = "kriona.VikingVillages.vikingPowerPupil";
		private static readonly int PupilUntilKey = "kriona.VikingVillages.vikingPowerPupilUntil".GetStableHashCode();

		/// <summary>
		/// How far a power reaches from the one using it, the same as a player's
		/// </summary>
		public const float Reach = 10f;

		/// <summary>
		/// How far from a viking the enemies it counts for a fight are, how many count as a lot, and how hurt it has to
		/// be, as a share of its health, to count as hurt
		/// </summary>
		private const float FightRange = 20f;
		internal const int ManyEnemies = 3;
		private const float HurtShare = 0.5f;

		/// <summary>
		/// How long a viking is in the act of using a power, in seconds - its animation, up to the power taking hold and
		/// a moment after, while no other viking near uses the same power - and how far from it they wait
		/// </summary>
		private const float UseTime = 3f;
		private const float UseWaitRange = 40f;

		/// <summary>
		/// The fewest vikings near one already under a power who aren't, for it to use the power again
		/// </summary>
		internal const int UnpoweredNeeded = 2;

		/// <summary>
		/// How long a viking waits after using a power before using another when the power has no cooldown of its own,
		/// in seconds - the game's 20 minutes
		/// </summary>
		private const float FallbackCooldown = 1200f;

		/// <summary>
		/// Seconds between a viking's looks at whether to use a power, and between its owner noting which it's under
		/// </summary>
		private const float CheckInterval = 1f;

		/// <summary>
		/// How long a viking's claim on a viking it's teaching lasts, in seconds, renewed as it goes
		/// </summary>
		public const float PupilClaimTime = 30f;

		/// <summary>
		/// The Meadows' creatures, by prefab name, that a fight against alone isn't worth a power
		/// </summary>
		private static readonly HashSet<string> MeadowsCreatures = new HashSet<string> { "Greyling", "Boar", "Boar_piggy", "Neck", "Deer" };

		/// <summary>
		/// The kinds of damage a creature's attacks deal that a power guards against
		/// </summary>
		[System.Flags]
		internal enum Elements
		{
			None = 0,
			Poison = 1,
			Fire = 2,
			Frost = 4
		}

		/// <summary>
		/// The kinds of damage each creature's attacks deal, by prefab name, worked out from its prefab the first time
		/// </summary>
		private static readonly Dictionary<string, Elements> creatureElements = new Dictionary<string, Elements>();

		/// <summary>
		/// The viking whose powers these are, and its looks, gear and skills
		/// </summary>
		private readonly LivingVikingBody viking;
		private VikingCore core;

		/// <summary>
		/// When the viking next looks at whether to use a power for a fight, and when its owner next notes which it's
		/// under, in Time.time seconds
		/// </summary>
		private float nextFightCheck;
		private float nextUnderCheck;

		/// <summary>
		/// Starts a living viking's powers
		/// </summary>
		/// <param name="viking">The viking</param>
		public VikingPowers(LivingVikingBody viking)
		{
			this.viking = viking;
		}

		/// <summary>
		/// Hashes the powers' status effect names
		/// </summary>
		/// <returns>The hashes, in the order of Names</returns>
		private static int[] HashNames()
		{
			int[] hashes = new int[Names.Length];
			for (int i = 0; i < Names.Length; i++)
			{
				hashes[i] = Names[i].GetStableHashCode();
			}
			return(hashes);
		}

		/// <summary>
		/// Finds a power by its status effect name hash
		/// </summary>
		/// <param name="hash">The status effect's name hash</param>
		/// <returns>Its index in Names, or -1 when it isn't a forsaken power</returns>
		internal static int IndexOf(int hash)
		{
			for (int i = 0; i < Hashes.Length; i++)
			{
				if (Hashes[i] == hash)
				{
					return(i);
				}
			}
			return(-1);
		}

		/// <summary>
		/// Works out the powers a viking of a biome knows from the start - those of every biome up to its own, where the
		/// Meadows have none
		/// </summary>
		/// <param name="biome">The biome's index in VikingGear.Biomes</param>
		/// <returns>The powers, one bit for each in the order of Names</returns>
		internal static int BiomeMask(int biome)
		{
			int count = Mathf.Clamp(biome, 0, Names.Length);
			return((1 << count) - 1);
		}

		/// <summary>
		/// Works out the powers a viking knows from the start - its biome's and those before (see BiomeMask), and for a
		/// dvergr viking the Queen's too, which the dvergr they work for have taught them
		/// </summary>
		/// <param name="biome">The biome's index in VikingGear.Biomes</param>
		/// <param name="dvergr">Whether it's a dvergr viking</param>
		/// <returns>The powers, one bit for each in the order of Names</returns>
		internal static int StartMask(int biome, bool dvergr)
		{
			return(BiomeMask(biome) | (dvergr ? 1 << Queen : 0));
		}

		/// <summary>
		/// Gets the viking's ZDO
		/// </summary>
		/// <returns>The ZDO, or null while it has none</returns>
		private ZDO Zdo()
		{
			return((viking.m_nview != null && viking.m_nview.IsValid()) ? viking.m_nview.GetZDO() : null);
		}

		/// <summary>
		/// Gets the viking's looks, gear and skills
		/// </summary>
		/// <returns>Its core, or null</returns>
		private VikingCore Core()
		{
			if (core == null)
			{
				core = viking.GetComponent<VikingCore>();
			}
			return(core);
		}

		/// <summary>
		/// Gets the powers the viking knows - those it knows from the start (see StartMask), and any it has learned
		/// </summary>
		/// <returns>The powers, one bit for each in the order of Names</returns>
		public int Known()
		{
			ZDO zdo = Zdo();
			VikingCore own = Core();
			if (zdo == null || own == null || own.GetData() == null)
			{
				return(0);
			}
			return(zdo.GetInt(LearnedKey) | StartMask(own.BiomeIndex(), VikingCore.IsDvergrViking(viking)));
		}

		/// <summary>
		/// Checks whether the viking knows a power
		/// </summary>
		/// <param name="power">The power's index in Names</param>
		/// <returns>True when it does</returns>
		public bool Knows(int power)
		{
			return((Known() & (1 << power)) != 0);
		}

		/// <summary>
		/// Gets the powers another living viking knows
		/// </summary>
		/// <param name="other">The viking</param>
		/// <returns>The powers, one bit for each, or none for a character that isn't a living viking</returns>
		public static int KnownBy(Character other)
		{
			return((other is LivingVikingBody body && body.Powers != null) ? body.Powers.Known() : 0);
		}

		/// <summary>
		/// Notes that the viking has been given a status effect - learning it, and noting it's under it, when it's a
		/// forsaken power - on its owner
		/// </summary>
		/// <param name="hash">The status effect's name hash</param>
		public void Received(int hash)
		{
			int power = IndexOf(hash);
			ZDO zdo = Zdo();
			if (power < 0 || zdo == null || !viking.m_nview.IsOwner())
			{
				return;
			}
			int bit = 1 << power;
			int learned = zdo.GetInt(LearnedKey);
			if ((learned & bit) == 0 && (Known() & bit) == 0)
			{
				zdo.Set(LearnedKey, learned | bit);
				TestLog.Count("Powers", "learned");
				if (TestLog.On || Player.m_debugMode)
				{
					Plugin.Log.LogInfo("Power learned: " + TestLog.Name(viking) + " learned " + PowerName(power) + "'s power, and now knows " + Describe(Known()));
				}
			}
			zdo.Set(UnderKey, zdo.GetInt(UnderKey) | bit);
		}

		/// <summary>
		/// Notes a viking teaching a power to another, as it uses the power beside them - for the test log's totals, and
		/// in the log during a test run or in debug mode, with the powers the one taught already knew, warning when they
		/// knew this one
		/// </summary>
		/// <param name="teacher">The viking teaching</param>
		/// <param name="pupil">The viking taught</param>
		/// <param name="power">The power, by index in Names</param>
		public static void LogTeaching(Character teacher, Character pupil, int power)
		{
			int knew = KnownBy(pupil);
			bool already = ((knew & (1 << power)) != 0);
			TestLog.Count("Powers", "taught");
			if (already)
			{
				TestLog.Count("Troubles", "powers taught to a viking who already knew them");
			}
			if (!TestLog.On && !Player.m_debugMode)
			{
				return;
			}
			string line = "Power taught: " + TestLog.Name(teacher) + " uses " + PowerName(power) + "'s power to teach " + TestLog.Label(pupil) + ", who knew " + Describe(knew);
			if (already)
			{
				Plugin.Log.LogWarning(line + " - this one included");
			}
			else
			{
				Plugin.Log.LogInfo(line);
			}
		}

		/// <summary>
		/// Gets a power's name for the log - the boss it's from, like Eikthyr or Elder
		/// </summary>
		/// <param name="power">The power, by index in Names</param>
		/// <returns>The name</returns>
		internal static string PowerName(int power)
		{
			return(Names[power].Substring(3).Replace("The", ""));
		}

		/// <summary>
		/// Lists the powers in a set by name, for the log, in the order of Names
		/// </summary>
		/// <param name="powers">The powers, one bit for each in the order of Names</param>
		/// <returns>The names separated by commas, or "none"</returns>
		internal static string Describe(int powers)
		{
			List<string> names = new List<string>();
			for (int i = 0; i < Names.Length; i++)
			{
				if ((powers & (1 << i)) != 0)
				{
					names.Add(PowerName(i));
				}
			}
			return((names.Count > 0) ? string.Join(", ", names) : "none");
		}

		/// <summary>
		/// Notes in the viking's ZDO which powers it's under, every CheckInterval seconds on its owner, so other clients'
		/// vikings can tell
		/// </summary>
		public void Update()
		{
			ZDO zdo = Zdo();
			if (zdo == null || !viking.m_nview.IsOwner() || Time.time < nextUnderCheck)
			{
				return;
			}
			nextUnderCheck = Time.time + CheckInterval;
			SEMan seman = viking.GetSEMan();
			int under = 0;
			for (int i = 0; i < Hashes.Length; i++)
			{
				if (seman != null && seman.HaveStatusEffect(Hashes[i]))
				{
					under |= 1 << i;
				}
			}
			if (zdo.GetInt(UnderKey) != under)
			{
				zdo.Set(UnderKey, under);
			}
		}

		/// <summary>
		/// Checks whether a living viking is under a power - for the viking itself, by its own status effects, and for
		/// another, as its owner last noted
		/// </summary>
		/// <param name="other">The viking</param>
		/// <param name="power">The power's index in Names</param>
		/// <returns>True when it is</returns>
		internal static bool Under(Character other, int power)
		{
			if (other.m_nview == null || !other.m_nview.IsValid())
			{
				return(false);
			}
			if (other.m_nview.IsOwner() && other.GetSEMan() != null)
			{
				return(other.GetSEMan().HaveStatusEffect(Hashes[power]));
			}
			return((other.m_nview.GetZDO().GetInt(UnderKey) & (1 << power)) != 0);
		}

		/// <summary>
		/// Checks whether the viking's cooldown is over, so it may use a power
		/// </summary>
		/// <returns>True when it is</returns>
		public bool Ready()
		{
			ZDO zdo = Zdo();
			return(zdo != null && zdo.GetLong(ReadyKey) <= NetTime.Now());
		}

		/// <summary>
		/// Puts off the end of a viking's cooldown by the time the world skipped ahead while players slept, the way a
		/// player's cooldown only counts the time that passes for them - unless it had already ended before the skip
		/// </summary>
		/// <param name="zdo">The viking's ZDO, owned by this client</param>
		/// <param name="skipped">How far the world skipped ahead, in ZNet time ticks</param>
		public static void Postpone(ZDO zdo, long skipped)
		{
			long ready = zdo.GetLong(ReadyKey);
			long postponed = VikingRested.Postponed(ready, NetTime.Now(), skipped);
			if (postponed != ready)
			{
				zdo.Set(ReadyKey, postponed);
			}
		}

		/// <summary>
		/// Checks whether the viking can use a power now, the way a player could - alive, awake, not in the middle of an
		/// attack, and standing unless it's seated on a boat, where a player can use one too
		/// </summary>
		/// <param name="seated">True when it may be seated on a boat</param>
		/// <returns>True when it can</returns>
		private bool Able(bool seated)
		{
			return(!viking.IsDead() && !viking.InAttack() && (seated || !viking.IsAttached()) && !VikingSpeech.Asleep(viking) && !ZombieState.IsRising(viking));
		}

		/// <summary>
		/// Uses Moder's power on the boat the viking sits on, turning the wind to blow from behind it, when it knows the
		/// power and may use it (see Use)
		/// </summary>
		/// <returns>True when it uses it</returns>
		public bool UseForWind()
		{
			return(Knows(Moder) && Use(Moder, null, true));
		}

		/// <summary>
		/// Uses a power the viking knows once its cooldown is over - unless another viking near is in the act of using
		/// the same one, or, when it isn't teaching, it's under the power already and fewer than UnpoweredNeeded vikings
		/// near it aren't. It says which power it uses, plays the player's animation for it and gives it to itself, the
		/// creatures of its faction and the players it isn't hostile to around it (see VikingCore.StartPower), and its
		/// cooldown starts, as long as the
		/// power's own, the same as a player's
		/// </summary>
		/// <param name="power">The power's index in Names</param>
		/// <param name="pupil">The viking it's teaching the power to, or null</param>
		/// <param name="seated">True when it may use it seated on a boat</param>
		/// <returns>True when it uses it</returns>
		public bool Use(int power, Character pupil = null, bool seated = false)
		{
			ZDO zdo = Zdo();
			VikingCore own = Core();
			if (zdo == null || own == null || !viking.m_nview.IsOwner() || !Ready() || !Knows(power) || !Able(seated) || ObjectDB.instance == null)
			{
				return(false);
			}
			StatusEffect effect = ObjectDB.instance.GetStatusEffect(Hashes[power]);
			if (effect == null || InUse(power))
			{
				return(false);
			}
			if (pupil == null && Under(viking, power) && Unpowered(power) < UnpoweredNeeded)
			{
				return(false);
			}
			long now = NetTime.Now();
			zdo.Set(ReadyKey, now + NetTime.Ticks((effect.m_cooldown > 0f) ? effect.m_cooldown : FallbackCooldown));
			zdo.Set(UsingKey, power + 1);
			zdo.Set(UsingUntilKey, now + NetTime.Ticks(UseTime));
			string name = Localization.instance.Localize(effect.m_name);
			string line;
			if (pupil != null)
			{
				line = VikingSpeech.PowerTaught(name, VikingNames.Get(pupil));
			}
			else if (VikingCore.IsDvergrViking(viking))
			{
				line = DvergrSpeech.PowerUsed(name);
			}
			else
			{
				line = VikingSpeech.PowerUsed(name);
			}
			VikingSpeech.SayFirst(viking, line, VikingSpeech.TalkTime, VikingSpeech.CallCull);
			PowerGivers.Announce(viking, Hashes[power]);
			own.StartPower(Hashes[power]);
			return(true);
		}

		/// <summary>
		/// Checks whether another living viking within UseWaitRange is in the act of using a power, on any client
		/// </summary>
		/// <param name="power">The power's index in Names</param>
		/// <returns>True when one is</returns>
		private bool InUse(int power)
		{
			long now = NetTime.Now();
			foreach (Character other in Character.GetAllCharacters())
			{
				if (other == viking || !(other is LivingVikingBody) || other.IsDead() || other.m_nview == null || !other.m_nview.IsValid())
				{
					continue;
				}
				ZDO zdo = other.m_nview.GetZDO();
				if (zdo.GetInt(UsingKey) == power + 1 && zdo.GetLong(UsingUntilKey) > now && Vector3.Distance(other.transform.position, viking.transform.position) <= UseWaitRange)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Counts the other living vikings within Reach of the viking who aren't under a power, so would gain by it
		/// </summary>
		/// <param name="power">The power's index in Names</param>
		/// <returns>How many</returns>
		private int Unpowered(int power)
		{
			int count = 0;
			foreach (Character other in Character.GetAllCharacters())
			{
				if (other != viking && other is LivingVikingBody && !other.IsDead() && Vector3.Distance(other.transform.position, viking.transform.position) <= Reach && !Under(other, power))
				{
					count++;
				}
			}
			return(count);
		}

		/// <summary>
		/// Uses a power for the fight the viking is in, every CheckInterval seconds, by the enemies within FightRange
		/// and how hurt it is (see FightPowers) - the first of those it knows and may use
		/// </summary>
		public void UpdateFight()
		{
			if (Time.time < nextFightCheck)
			{
				return;
			}
			nextFightCheck = Time.time + CheckInterval;
			if (!Ready() || Known() == 0)
			{
				return;
			}
			List<Character> nearby = new List<Character>();
			Character.GetCharactersInRange(viking.transform.position, FightRange, nearby);
			int enemies = 0;
			bool meadowsOnly = true;
			Elements elements = Elements.None;
			foreach (Character other in nearby)
			{
				if (other == viking || other.IsDead() || !BaseAI.IsEnemy(viking, other))
				{
					continue;
				}
				enemies++;
				meadowsOnly = meadowsOnly && FromMeadows(other);
				elements |= ElementsOf(other);
			}
			UseFirst(FightPowers(enemies, meadowsOnly, elements, Hurt()));
		}

		/// <summary>
		/// Uses a power for what the viking is doing with nothing to fight - for foraging or heading out of the village,
		/// as it starts, or for being hurt (see WorkPowers). Its village life calls it every second or so
		/// </summary>
		/// <param name="farming">True as it starts a job that raises its Farming skill</param>
		/// <param name="outing">True as its job takes it out of the village</param>
		public void UpdateWork(bool farming, bool outing)
		{
			if (Ready() && Known() != 0)
			{
				UseFirst(WorkPowers(farming, outing, Hurt()));
			}
		}

		/// <summary>
		/// Uses the first of some powers the viking knows and may use
		/// </summary>
		/// <param name="powers">The powers, most wanted first</param>
		private void UseFirst(List<int> powers)
		{
			foreach (int power in powers)
			{
				if (Knows(power) && Use(power))
				{
					return;
				}
			}
		}

		/// <summary>
		/// Checks whether the viking is hurt enough to want the Elder's power
		/// </summary>
		/// <returns>True when it's below HurtShare of its health</returns>
		private bool Hurt()
		{
			return(viking.GetHealth() < viking.GetMaxHealth() * HurtShare);
		}

		/// <summary>
		/// Picks the powers worth using in a fight, most wanted first - none against a single enemy or only the Meadows'
		/// creatures, and otherwise the Queen's against poison, Fader's against fire, Moder's against frost, Yagluth's,
		/// Bonemass's and the Elder's against ManyEnemies or more, and the Elder's when hurt
		/// </summary>
		/// <param name="enemies">How many enemies are near</param>
		/// <param name="meadowsOnly">Whether every one of them is a creature of the Meadows</param>
		/// <param name="elements">The kinds of damage their attacks deal</param>
		/// <param name="hurt">Whether the viking is hurt</param>
		/// <returns>The powers, by index in Names</returns>
		internal static List<int> FightPowers(int enemies, bool meadowsOnly, Elements elements, bool hurt)
		{
			List<int> powers = new List<int>();
			if (enemies <= 1 || meadowsOnly)
			{
				return(powers);
			}
			if ((elements & Elements.Poison) != 0)
			{
				powers.Add(Queen);
			}
			if ((elements & Elements.Fire) != 0)
			{
				powers.Add(Fader);
			}
			if ((elements & Elements.Frost) != 0)
			{
				powers.Add(Moder);
			}
			if (enemies >= ManyEnemies)
			{
				powers.Add(Yagluth);
				powers.Add(Bonemass);
				powers.Add(Elder);
			}
			if (hurt && !powers.Contains(Elder))
			{
				powers.Add(Elder);
			}
			return(powers);
		}

		/// <summary>
		/// Picks the powers worth using with nothing to fight, most wanted first - Yagluth's as it starts a job that
		/// raises its Farming skill, Eikthyr's as a job takes it out of the village, and the Elder's when hurt
		/// </summary>
		/// <param name="farming">True as it starts a job that raises its Farming skill</param>
		/// <param name="outing">True as its job takes it out of the village</param>
		/// <param name="hurt">Whether the viking is hurt</param>
		/// <returns>The powers, by index in Names</returns>
		internal static List<int> WorkPowers(bool farming, bool outing, bool hurt)
		{
			List<int> powers = new List<int>();
			if (farming)
			{
				powers.Add(Yagluth);
			}
			if (outing)
			{
				powers.Add(Eikthyr);
			}
			if (hurt)
			{
				powers.Add(Elder);
			}
			return(powers);
		}

		/// <summary>
		/// Checks whether a creature is one of the Meadows' - a greyling, boar, neck or deer, or a zombie viking of the
		/// Meadows
		/// </summary>
		/// <param name="creature">The creature</param>
		/// <returns>True when it is</returns>
		private static bool FromMeadows(Character creature)
		{
			if (creature.TryGetComponent(out VikingCore other))
			{
				return(other.GetData() != null && other.BiomeIndex() == 0);
			}
			return(MeadowsCreatures.Contains(Utils.GetPrefabName(creature.gameObject)));
		}

		/// <summary>
		/// Works out the kinds of damage a creature's attacks deal - a viking's by the weapon and ammo it fights with, and
		/// any other creature's by the attacks its prefab gives it, worked out once for each kind
		/// </summary>
		/// <param name="creature">The creature</param>
		/// <returns>The kinds of damage</returns>
		internal static Elements ElementsOf(Character creature)
		{
			if (creature is Humanoid humanoid && creature.TryGetComponent(out VikingCore _))
			{
				return(ItemElements(humanoid.GetCurrentWeapon()) | ItemElements(humanoid.m_ammoItem));
			}
			string name = Utils.GetPrefabName(creature.gameObject);
			if (creatureElements.TryGetValue(name, out Elements known))
			{
				return(known);
			}
			Elements found = Elements.None;
			GameObject prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(name) : null);
			Humanoid attacker = ((prefab != null) ? prefab.GetComponent<Humanoid>() : null);
			if (attacker != null)
			{
				found |= PrefabsElements(attacker.m_defaultItems) | PrefabsElements(attacker.m_randomWeapon);
				foreach (Humanoid.ItemSet set in attacker.m_randomSets ?? new Humanoid.ItemSet[0])
				{
					found |= PrefabsElements(set.m_items);
				}
			}
			creatureElements[name] = found;
			return(found);
		}

		/// <summary>
		/// Works out the kinds of damage some item prefabs' attacks deal
		/// </summary>
		/// <param name="items">The prefabs, or null</param>
		/// <returns>The kinds of damage</returns>
		private static Elements PrefabsElements(GameObject[] items)
		{
			Elements found = Elements.None;
			foreach (GameObject item in items ?? new GameObject[0])
			{
				ItemDrop drop = ((item != null) ? item.GetComponent<ItemDrop>() : null);
				if (drop != null)
				{
					found |= ItemElements(drop.m_itemData);
				}
			}
			return(found);
		}

		/// <summary>
		/// Works out the kinds of damage an item's attacks deal - its own damage and status effect, and what its attacks
		/// shoot or spawn
		/// </summary>
		/// <param name="item">The item, or null</param>
		/// <returns>The kinds of damage</returns>
		private static Elements ItemElements(ItemDrop.ItemData item)
		{
			if (item == null || item.m_shared == null)
			{
				return(Elements.None);
			}
			ItemDrop.ItemData.SharedData shared = item.m_shared;
			Elements found = DamageElements(shared.m_damages) | EffectElements(shared.m_attackStatusEffect);
			found |= AttackElements(shared.m_attack) | AttackElements(shared.m_secondaryAttack);
			return(found);
		}

		/// <summary>
		/// Works out the kinds of damage what an attack shoots or spawns deals
		/// </summary>
		/// <param name="attack">The attack, or null</param>
		/// <returns>The kinds of damage</returns>
		private static Elements AttackElements(Attack attack)
		{
			if (attack == null)
			{
				return(Elements.None);
			}
			return(SpawnElements(attack.m_attackProjectile, 2) | SpawnElements(attack.m_spawnOnTrigger, 2));
		}

		/// <summary>
		/// Works out the kinds of damage a projectile or area of effect deals, and what it spawns as it hits
		/// </summary>
		/// <param name="spawned">The prefab, or null</param>
		/// <param name="depth">How many more spawns deep to look</param>
		/// <returns>The kinds of damage</returns>
		private static Elements SpawnElements(GameObject spawned, int depth)
		{
			if (spawned == null || depth <= 0)
			{
				return(Elements.None);
			}
			Elements found = Elements.None;
			Projectile projectile = spawned.GetComponent<Projectile>();
			if (projectile != null)
			{
				found |= DamageElements(projectile.m_damage) | SpawnElements(projectile.m_spawnOnHit, depth - 1);
			}
			Aoe aoe = spawned.GetComponentInChildren<Aoe>();
			if (aoe != null)
			{
				found |= DamageElements(aoe.m_damage);
				if (!string.IsNullOrEmpty(aoe.m_statusEffect) && ObjectDB.instance != null)
				{
					found |= EffectElements(ObjectDB.instance.GetStatusEffect(aoe.m_statusEffect.GetStableHashCode()));
				}
			}
			return(found);
		}

		/// <summary>
		/// Works out the kinds of damage in some damage
		/// </summary>
		/// <param name="damage">The damage</param>
		/// <returns>The kinds of damage</returns>
		internal static Elements DamageElements(HitData.DamageTypes damage)
		{
			Elements found = Elements.None;
			if (damage.m_poison > 0f)
			{
				found |= Elements.Poison;
			}
			if (damage.m_fire > 0f)
			{
				found |= Elements.Fire;
			}
			if (damage.m_frost > 0f)
			{
				found |= Elements.Frost;
			}
			return(found);
		}

		/// <summary>
		/// Works out the kind of damage a status effect an attack gives deals - poisoning, burning or frost
		/// </summary>
		/// <param name="effect">The status effect, or null</param>
		/// <returns>The kind of damage</returns>
		private static Elements EffectElements(StatusEffect effect)
		{
			if (effect == null)
			{
				return(Elements.None);
			}
			int hash = effect.NameHash();
			if (hash == SEMan.s_statusEffectPoison)
			{
				return(Elements.Poison);
			}
			if (hash == SEMan.s_statusEffectBurning)
			{
				return(Elements.Fire);
			}
			return((hash == SEMan.s_statusEffectFrost) ? Elements.Frost : Elements.None);
		}

		/// <summary>
		/// Picks a power the viking knows that one of the other living vikings of its village doesn't, and the nearest of
		/// them to teach it to - awake, in the village and with no other viking teaching them - for the village's
		/// gathering at dusk, once the viking's cooldown is over
		/// </summary>
		/// <param name="village">The viking's village</param>
		/// <param name="power">The power to teach, by index in Names</param>
		/// <returns>The viking to teach it to, or null when there's none</returns>
		public Character FindPupil(Village village, out int power)
		{
			power = -1;
			// A viking seated at the gathering gets up to teach, so only being able to use a power at all counts here
			int known = Known();
			if (known == 0 || !Ready() || viking.IsDead() || VikingSpeech.Asleep(viking))
			{
				return(null);
			}
			List<Character> vikings = village.Vikings();
			Character best = null;
			float bestDistance = float.MaxValue;
			foreach (Character other in vikings)
			{
				if (other == viking || VikingSpeech.Asleep(other) || !village.Contains(other.transform.position) || Taught(other, vikings))
				{
					continue;
				}
				int missing = known & ~KnownBy(other);
				float distance = Vector3.Distance(other.transform.position, viking.transform.position);
				if (missing != 0 && distance < bestDistance)
				{
					best = other;
					bestDistance = distance;
					power = Highest(missing);
				}
			}
			return(best);
		}

		/// <summary>
		/// Finds the last of the powers in a set, the one beaten latest
		/// </summary>
		/// <param name="powers">The powers, one bit for each in the order of Names</param>
		/// <returns>Its index in Names, or -1 for none</returns>
		internal static int Highest(int powers)
		{
			for (int i = Names.Length - 1; i >= 0; i--)
			{
				if ((powers & (1 << i)) != 0)
				{
					return(i);
				}
			}
			return(-1);
		}

		/// <summary>
		/// Checks whether another of the village's vikings is on its way to teach a viking, on any client
		/// </summary>
		/// <param name="pupil">The viking</param>
		/// <param name="vikings">The village's vikings</param>
		/// <returns>True when one is</returns>
		private bool Taught(Character pupil, List<Character> vikings)
		{
			long now = NetTime.Now();
			ZDOID id = pupil.GetZDOID();
			foreach (Character other in vikings)
			{
				if (other == viking || other.m_nview == null || !other.m_nview.IsValid())
				{
					continue;
				}
				ZDO zdo = other.m_nview.GetZDO();
				if (zdo.GetZDOID(PupilKey) == id && zdo.GetLong(PupilUntilKey) > now)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Notes on the viking the viking it's on its way to teach, for the next PupilClaimTime seconds, so no other
		/// viking sets out to teach them too - or that it's teaching no one
		/// </summary>
		/// <param name="pupil">The viking it's teaching, or null</param>
		public void ClaimPupil(Character pupil)
		{
			ZDO zdo = Zdo();
			if (zdo == null || !viking.m_nview.IsOwner())
			{
				return;
			}
			zdo.Set(PupilKey, (pupil != null) ? pupil.GetZDOID() : ZDOID.None);
			zdo.Set(PupilUntilKey, (pupil != null) ? NetTime.Now() + NetTime.Ticks(PupilClaimTime) : 0L);
		}

		/// <summary>
		/// Describes the viking's powers for the debug hover text - those it knows, which it's under and when it may use
		/// one again
		/// </summary>
		/// <returns>The description</returns>
		public string Describe()
		{
			int known = Known();
			if (known == 0)
			{
				return("none");
			}
			List<string> names = new List<string>();
			for (int i = 0; i < Names.Length; i++)
			{
				if ((known & (1 << i)) != 0)
				{
					names.Add(Names[i].Substring(3) + (Under(viking, i) ? " (under)" : ""));
				}
			}
			ZDO zdo = Zdo();
			float left = ((zdo != null) ? (float)(zdo.GetLong(ReadyKey) - NetTime.Now()) / System.TimeSpan.TicksPerSecond : 0f);
			return(string.Join(", ", names) + ((left > 0f) ? ", ready in " + VikingFood.Duration(left) : ", ready"));
		}
	}
}
