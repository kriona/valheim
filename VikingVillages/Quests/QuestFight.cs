using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// One of an enemy's attacks in a quest's fight - how long the enemy waits before using it again, in seconds,
	/// whether it strikes all around, hitting every viking at once, and the chance the enemy has it at all, for one of
	/// the weapons an enemy of its kind may spawn with
	/// </summary>
	internal class EnemyAttack
	{
		public float Interval;
		public bool Area;
		public float Share = 1f;
	}

	/// <summary>
	/// A kind of enemy in a quest's fight - its health, the least time it waits between any two attacks, in seconds,
	/// and its attacks
	/// </summary>
	internal class Enemy
	{
		public float Health;
		public float MinInterval;
		public List<EnemyAttack> Attacks = new List<EnemyAttack>();
	}

	/// <summary>
	/// What a hit of an enemy's attack deals a viking, through the viking's resistances but before its shield and
	/// armor - what it deals at once, and the poison, fire and spirit damage it deals over time
	/// </summary>
	internal class Blow
	{
		public float Direct;
		public float OverTime;
	}

	/// <summary>
	/// A viking in a quest's fight - its most health, its armor and its shield's block power, 0 without one, and for
	/// each kind of enemy, by the same index as the fight's enemies, what a swing of its deals that enemy through its
	/// resistances, and what a hit of each of that enemy's attacks deals it, in the order of the attacks
	/// </summary>
	internal class Fighter
	{
		public float Health;
		public float Armor;
		public float BlockPower;
		public List<float> Swings = new List<float>();
		public List<List<Blow>> Blows = new List<List<Blow>>();
	}

	/// <summary>
	/// How a quest's fight goes - how long it takes, in seconds, and the health it harms each viking by and the share of
	/// its health as it set out that is, in the order of the fighters
	/// </summary>
	internal class FightResult
	{
		public float Seconds;
		public List<float> Harms = new List<float>();
		public List<float> HarmShares = new List<float>();
	}

	/// <summary>
	/// Works out a quest's fight from both sides' numbers - how long the vikings take to bring down the enemies coming
	/// at them, a pack at a time, and what the enemies' attacks deal them meanwhile through their shields and armor -
	/// apart from the game, so it can be tested
	/// </summary>
	/// <remarks>
	/// It goes by averages rather than playing out each blow: the vikings all swing at one enemy at a time, each
	/// every QuestSim.SwingSeconds, every enemy of a pack attacks until it's down, using each attack as often as its
	/// interval lets it, and LandShare, EnemyLandShare and BlockShare stand in for what the numbers can't show - missed
	/// swings, dodging, enemies turning between vikings and shields raised in time. A blocked hit goes through the
	/// shield's block power the way the game's does, as if it were armor (see Humanoid.BlockAttack), and every hit
	/// then through the viking's armor (see HitData.DamageTypes.ApplyArmor). Poison, fire and spirit damage are dealt
	/// over time, and a new dose only takes the place of the last rather than adding to it (see SE_Poison.AddDamage),
	/// so each of an enemy's attacks deals a viking that part of its hit once at most
	/// </remarks>
	internal static class QuestFight
	{
		/// <summary>
		/// The share of the vikings' swings that land on their enemy
		/// </summary>
		public const float LandShare = 0.75f;

		/// <summary>
		/// The share of the enemies' attacks that land on the vikings they're aimed at
		/// </summary>
		public const float EnemyLandShare = 0.6f;

		/// <summary>
		/// The share of the hits landing on a viking with a shield that it blocks
		/// </summary>
		public const float BlockShare = 0.5f;

		/// <summary>
		/// The least time one of an enemy's attacks takes, start to finish, in seconds, so it never attacks more often
		/// than that whatever its intervals
		/// </summary>
		public const float AttackSeconds = 2f;

		/// <summary>
		/// The longest a fight goes on, in seconds, so vikings who can't hurt their enemies don't fight them forever
		/// </summary>
		public const float MostSeconds = 1800f;

		/// <summary>
		/// Works out a fight - the enemies coming at the vikings pack by pack, in the order given, and the vikings
		/// bringing down each pack's one at a time while the rest of it keeps attacking. Each enemy's attacks that strike
		/// all around land on every viking, and the rest are shared out among them evenly, as it turns from one to
		/// another
		/// </summary>
		/// <param name="kinds">The kinds of enemy</param>
		/// <param name="foes">The enemies, in the order they come, each by its kind's index</param>
		/// <param name="pack">How many come at once, or 0 for all of them</param>
		/// <param name="fighters">The vikings</param>
		/// <returns>How it goes</returns>
		public static FightResult Fight(List<Enemy> kinds, List<int> foes, int pack, List<Fighter> fighters)
		{
			FightResult result = new FightResult();
			List<float> killSeconds = new List<float>();
			for (int k = 0; k < kinds.Count; k++)
			{
				float perSecond = 0f;
				foreach (Fighter fighter in fighters)
				{
					float swing = ((k < fighter.Swings.Count) ? fighter.Swings[k] : 0f);
					perSecond += System.Math.Max(0f, swing) * LandShare / QuestSim.SwingSeconds;
				}
				killSeconds.Add(FightSeconds(kinds[k].Health, perSecond));
			}
			List<float> alive = AliveSeconds(foes, killSeconds, pack, out float seconds);
			result.Seconds = seconds;
			foreach (Fighter fighter in fighters)
			{
				float harm = 0f;
				for (int i = 0; i < foes.Count; i++)
				{
					int k = foes[i];
					List<Blow> blows = ((k < fighter.Blows.Count) ? fighter.Blows[k] : new List<Blow>());
					harm += HarmFrom(kinds[k], blows, alive[i], fighters.Count, fighter.Armor, fighter.BlockPower);
				}
				result.Harms.Add(harm);
				result.HarmShares.Add(Share(harm, fighter.Health));
			}
			return(result);
		}

		/// <summary>
		/// Works out how long each enemy keeps attacking - in each pack, the first until the vikings bring it down, the
		/// next until they've brought it down after the first, and so on - and how long the whole fight takes, each up
		/// to MostSeconds
		/// </summary>
		/// <param name="foes">The enemies, in the order they come, each by its kind's index</param>
		/// <param name="killSeconds">How long the vikings take to bring down an enemy of each kind, in seconds</param>
		/// <param name="pack">How many come at once, or 0 for all of them</param>
		/// <param name="seconds">Set to how long the whole fight takes, in seconds</param>
		/// <returns>How long each enemy attacks, in seconds, in the order of the enemies</returns>
		internal static List<float> AliveSeconds(List<int> foes, List<float> killSeconds, int pack, out float seconds)
		{
			int size = ((pack > 0) ? pack : System.Math.Max(1, foes.Count));
			List<float> alive = new List<float>();
			float elapsed = 0f;
			float packTime = 0f;
			for (int i = 0; i < foes.Count; i++)
			{
				if (i % size == 0)
				{
					elapsed += packTime;
					packTime = 0f;
				}
				packTime += killSeconds[foes[i]];
				alive.Add(System.Math.Min(MostSeconds, packTime));
			}
			seconds = System.Math.Min(MostSeconds, elapsed + packTime);
			return(alive);
		}

		/// <summary>
		/// Works out the harm an enemy does a viking while it attacks - each of its attacks used as often as it can be
		/// (see Uses), landing EnemyLandShare of the time, on every viking for one that strikes all around and on one of
		/// them otherwise, through the viking's shield and armor (see Taken), with what it deals over time counted once
		/// at most
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <param name="blows">What a hit of each of its attacks deals the viking, in the order of its attacks</param>
		/// <param name="seconds">How long it attacks, in seconds</param>
		/// <param name="vikings">How many vikings it turns between</param>
		/// <param name="armor">The viking's armor</param>
		/// <param name="blockPower">The viking's shield's block power, or 0 without one</param>
		/// <returns>The harm, in health</returns>
		internal static float HarmFrom(Enemy enemy, List<Blow> blows, float seconds, int vikings, float armor, float blockPower)
		{
			List<float> uses = Uses(enemy.Attacks, enemy.MinInterval, seconds);
			float harm = 0f;
			for (int a = 0; a < enemy.Attacks.Count && a < blows.Count; a++)
			{
				Blow blow = blows[a];
				float hit = System.Math.Max(0f, blow.Direct) + System.Math.Max(0f, blow.OverTime);
				if (hit <= 0f)
				{
					continue;
				}
				float landed = uses[a] * EnemyLandShare * (enemy.Attacks[a].Area ? 1f : 1f / System.Math.Max(1, vikings));
				float taken = Taken(hit, armor, blockPower);
				harm += landed * taken * System.Math.Max(0f, blow.Direct) / hit;
				harm += System.Math.Min(landed, 1f) * taken * System.Math.Max(0f, blow.OverTime) / hit;
			}
			return(harm);
		}

		/// <summary>
		/// Works out how long the vikings take to bring an enemy down - its health over what they deal it each second,
		/// up to MostSeconds
		/// </summary>
		/// <param name="health">The enemy's health</param>
		/// <param name="perSecond">What the vikings deal it each second, all together</param>
		/// <returns>How long, in seconds</returns>
		internal static float FightSeconds(float health, float perSecond)
		{
			if (perSecond <= 0f)
			{
				return(MostSeconds);
			}
			return(System.Math.Min(MostSeconds, System.Math.Max(0f, health) / perSecond));
		}

		/// <summary>
		/// Works out how many times an enemy uses each of its attacks - each as often as its interval lets it, times the
		/// chance it has it, but never more than one every AttackSeconds or minInterval, whichever's longer, so when
		/// they'd come more often than that, all of them less often alike
		/// </summary>
		/// <param name="attacks">The enemy's attacks</param>
		/// <param name="minInterval">The least time the enemy waits between any two attacks, in seconds</param>
		/// <param name="seconds">How long it attacks, in seconds</param>
		/// <returns>How many times it uses each, in the order of the attacks</returns>
		internal static List<float> Uses(List<EnemyAttack> attacks, float minInterval, float seconds)
		{
			float least = System.Math.Max(AttackSeconds, minInterval);
			List<float> rates = new List<float>();
			float total = 0f;
			foreach (EnemyAttack attack in attacks)
			{
				float rate = System.Math.Max(0f, attack.Share) / System.Math.Max(least, attack.Interval);
				rates.Add(rate);
				total += rate;
			}
			float scale = ((total > 1f / least) ? 1f / least / total : 1f);
			List<float> uses = new List<float>();
			foreach (float rate in rates)
			{
				uses.Add(rate * scale * System.Math.Max(0f, seconds));
			}
			return(uses);
		}

		/// <summary>
		/// Puts the enemies of each kind in the order they come - one of each kind in turn, while there are any left of
		/// it, so a pack mixes them
		/// </summary>
		/// <param name="counts">How many there are of each kind, by its index</param>
		/// <returns>The enemies, each by its kind's index</returns>
		internal static List<int> Order(List<int> counts)
		{
			List<int> left = new List<int>(counts);
			List<int> foes = new List<int>();
			bool any = true;
			while (any)
			{
				any = false;
				for (int k = 0; k < left.Count; k++)
				{
					if (left[k] > 0)
					{
						foes.Add(k);
						left[k]--;
						any = true;
					}
				}
			}
			return(foes);
		}

		/// <summary>
		/// Works out what a hit that lands deals a viking, on average - through its armor, and for a viking with a
		/// shield, BlockShare of the time through the shield's block power first
		/// </summary>
		/// <param name="hit">The hit, through the viking's resistances</param>
		/// <param name="armor">The viking's armor</param>
		/// <param name="blockPower">Its shield's block power, or 0 without one</param>
		/// <returns>The damage it takes</returns>
		internal static float Taken(float hit, float armor, float blockPower)
		{
			if (hit <= 0f)
			{
				return(0f);
			}
			float open = HitData.DamageTypes.ApplyArmor(hit, armor);
			if (blockPower <= 0f)
			{
				return(open);
			}
			float blocked = HitData.DamageTypes.ApplyArmor(HitData.DamageTypes.ApplyArmor(hit, blockPower), armor);
			return(BlockShare * blocked + (1f - BlockShare) * open);
		}

		/// <summary>
		/// Works out the share of a viking's health a fight harms it by, rounded down to hundredths like a quest's
		/// danger (see QuestSim.HarmShare), so a fight that barely touches it does it no harm
		/// </summary>
		/// <param name="harm">The harm, in health</param>
		/// <param name="health">Its most health</param>
		/// <returns>The share, with no cap</returns>
		internal static float Share(float harm, float health)
		{
			if (health <= 0f)
			{
				return(0f);
			}
			// A sliver more for the rounding, so a share that's exactly whole isn't rounded down for a float's error
			return((float)(System.Math.Floor(harm / health * 100.0 + 1e-6) / 100.0));
		}
	}
}
