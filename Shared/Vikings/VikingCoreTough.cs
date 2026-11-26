using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// Weighing up a monster - whether it's too tough for a viking to bring down alone, and how many vikings it takes
	/// together
	/// </summary>
	internal partial class VikingCore
	{
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
	}
}
