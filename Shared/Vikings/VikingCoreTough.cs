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
		/// Weighs a monster up against the viking - it's tough when it would take more of the viking's swings to bring
		/// down, through the monster's armor, than the viking could take of its hardest hits, through the viking's
		/// armor (see MonsterThreat.Hit and SwingDamage), so a troll, bear or brute is tough for a viking in the
		/// Meadows' rags and flint, and not for one in the Plains' padded armor and black metal. A root, a player or a
		/// monster whose hit is unknown never is
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
			// Its swings are weighed through the monster's resistances and armor, so one that shrugs off its weapons, like
			// a zombie viking in armor against a spear, is tougher for it
			return(Outlasts(monster.GetMaxHealth(), hit, monster.GetBodyArmor(), humanoid.GetMaxHealth(), BodyArmor(), SwingDamage(monster.GetDamageModifiers())));
		}

		/// <summary>
		/// Checks whether a monster outlasts a viking - it would take more of the viking's swings to bring down, through
		/// the monster's armor, than ToughMargin times as many of the monster's hardest hits as the viking could take,
		/// through the viking's armor. Each side's hit counts for at least 1, so neither takes endless hits
		/// </summary>
		/// <param name="monsterHealth">The monster's health</param>
		/// <param name="monsterHit">The monster's hardest hit, before armor</param>
		/// <param name="monsterArmor">The monster's armor</param>
		/// <param name="vikingHealth">The viking's health</param>
		/// <param name="vikingArmor">The viking's armor</param>
		/// <param name="vikingStrike">What the viking deals in a swing, before armor (see SwingDamage)</param>
		/// <returns>True when it does, so the monster is tough</returns>
		internal static bool Outlasts(float monsterHealth, float monsterHit, float monsterArmor, float vikingHealth, float vikingArmor, float vikingStrike)
		{
			return(Odds(monsterHealth, monsterHit, monsterArmor, vikingHealth, vikingArmor, vikingStrike) > 1f);
		}

		/// <summary>
		/// Works out how far a monster outlasts a viking - how many of the viking's swings it takes to bring down,
		/// through the monster's armor, over ToughMargin times as many of the monster's hardest hits as the viking could
		/// take, through the viking's armor. Each side's hit counts for at least 1, so neither takes endless hits
		/// </summary>
		/// <param name="monsterHealth">The monster's health</param>
		/// <param name="monsterHit">The monster's hardest hit, before armor</param>
		/// <param name="monsterArmor">The monster's armor</param>
		/// <param name="vikingHealth">The viking's health</param>
		/// <param name="vikingArmor">The viking's armor</param>
		/// <param name="vikingStrike">What the viking deals in a swing, before armor (see SwingDamage)</param>
		/// <returns>Above 1 when the monster outlasts the viking, as many times over as it does</returns>
		internal static float Odds(float monsterHealth, float monsterHit, float monsterArmor, float vikingHealth, float vikingArmor, float vikingStrike)
		{
			// Counted as at least 1 before the armor too, since the game's armor works out nothing sensible for no damage
			float dealt = Mathf.Max(1f, HitData.DamageTypes.ApplyArmor(Mathf.Max(1f, vikingStrike), monsterArmor));
			float hitsToKill = monsterHealth / dealt;
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
			return(FightersNeeded(monster.GetHealth(), MonsterThreat.Hit(monster), monster.GetBodyArmor(), humanoid.GetMaxHealth(), BodyArmor(), SwingDamage(monster.GetDamageModifiers())));
		}

		/// <summary>
		/// Works out how many vikings alike it takes to fight a monster hand to hand together - 1 when it doesn't
		/// outlast one of them (see Odds), and otherwise as many times as it does, GroupMargin over, rounded up
		/// </summary>
		/// <param name="monsterHealth">The monster's health</param>
		/// <param name="monsterHit">The monster's hardest hit, before armor</param>
		/// <param name="monsterArmor">The monster's armor</param>
		/// <param name="vikingHealth">A viking's health</param>
		/// <param name="vikingArmor">A viking's armor</param>
		/// <param name="vikingStrike">What a viking deals in a swing, before armor (see SwingDamage)</param>
		/// <returns>How many vikings</returns>
		internal static int FightersNeeded(float monsterHealth, float monsterHit, float monsterArmor, float vikingHealth, float vikingArmor, float vikingStrike)
		{
			float odds = Odds(monsterHealth, monsterHit, monsterArmor, vikingHealth, vikingArmor, vikingStrike);
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
