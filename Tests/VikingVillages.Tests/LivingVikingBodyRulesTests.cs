using System.Collections.Generic;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests when a living viking's gear changes start and are done, how much its rest changes what the game heals it
	/// by, how much a fall hurts it, how its weight shows and whose name makes it immune to frost
	/// </summary>
	public class LivingVikingBodyRulesTests
	{
		/// <summary>
		/// Queues a change that puts on a piece a player didn't pick, which the viking didn't find
		/// </summary>
		/// <param name="changes">The changes queued</param>
		/// <param name="now">The time now, in seconds</param>
		/// <param name="type">The piece's item type</param>
		/// <param name="duration">The piece's equip time, in seconds</param>
		/// <returns>The change the piece is part of</returns>
		private static LivingVikingBody.GearChange PutOn(List<LivingVikingBody.GearChange> changes, float now, ItemDrop.ItemData.ItemType type, float duration)
		{
			return(LivingVikingBodyRules.QueueChange(changes, now, type, duration, true, false, false));
		}

		/// <summary>
		/// Checks that a viking named Elsa is immune to frost whatever its case and byname, and one whose name only
		/// starts with it or has none isn't
		/// </summary>
		/// <param name="name">The viking's name</param>
		/// <param name="expected">True when it's immune</param>
		[Theory]
		[InlineData("Elsa", true)]
		[InlineData("elsa the Wise", true)]
		[InlineData(" Elsa Stormcloak ", true)]
		[InlineData("Anna", false)]
		[InlineData("Elsabet", false)]
		[InlineData("", false)]
		[InlineData(null, false)]
		public void ElsaIsFrostImmune(string name, bool expected)
		{
			Assert.Equal(expected, LivingVikingBodyRules.FrostImmune(name));
		}

		/// <summary>
		/// Checks that a change with nothing queued before it starts now and is done after the piece's equip time
		/// </summary>
		[Fact]
		public void StartsNowWithNothingQueued()
		{
			List<LivingVikingBody.GearChange> changes = new List<LivingVikingBody.GearChange>();
			LivingVikingBody.GearChange change = PutOn(changes, 10f, ItemDrop.ItemData.ItemType.Helmet, 1.5f);
			Assert.Single(changes);
			Assert.Same(changes[0], change);
			Assert.False(change.Paired);
			Assert.Equal(ItemDrop.ItemData.ItemType.Helmet, change.Type);
			Assert.Equal(10f, change.Start, 3);
			Assert.Equal(11.5f, change.DoneAt, 3);
		}

		/// <summary>
		/// Checks that a change queued behind another starts once that one is done, not when it's queued
		/// </summary>
		[Fact]
		public void StartsWhenTheLastChangeIsDone()
		{
			List<LivingVikingBody.GearChange> changes = new List<LivingVikingBody.GearChange>();
			PutOn(changes, 10f, ItemDrop.ItemData.ItemType.Helmet, 2f);
			LivingVikingBody.GearChange second = PutOn(changes, 10.5f, ItemDrop.ItemData.ItemType.Chest, 3f);
			Assert.Equal(2, changes.Count);
			Assert.Equal(12f, second.Start, 3);
			Assert.Equal(15f, second.DoneAt, 3);
		}

		/// <summary>
		/// Checks that a shield joins the one-handed weapon queued before it as one change, done when the slower of the
		/// two is
		/// </summary>
		[Fact]
		public void JoinsAShieldToTheWeaponBeforeIt()
		{
			List<LivingVikingBody.GearChange> changes = new List<LivingVikingBody.GearChange>();
			LivingVikingBody.GearChange weapon = PutOn(changes, 5f, ItemDrop.ItemData.ItemType.OneHandedWeapon, 1f);
			LivingVikingBody.GearChange shield = PutOn(changes, 5.2f, ItemDrop.ItemData.ItemType.Shield, 2.5f);
			Assert.Single(changes);
			Assert.Same(weapon, shield);
			Assert.True(weapon.Paired);
			Assert.Equal(5f, weapon.Start, 3);
			Assert.Equal(7.5f, weapon.DoneAt, 3);
		}

		/// <summary>
		/// Checks that a faster weapon joining a shield leaves the change done when the shield is
		/// </summary>
		[Fact]
		public void KeepsTheSlowerTimeWhenTheFasterJoins()
		{
			List<LivingVikingBody.GearChange> changes = new List<LivingVikingBody.GearChange>();
			LivingVikingBody.GearChange shield = PutOn(changes, 5f, ItemDrop.ItemData.ItemType.Shield, 2.5f);
			PutOn(changes, 5f, ItemDrop.ItemData.ItemType.OneHandedWeapon, 1f);
			Assert.Single(changes);
			Assert.Equal(7.5f, shield.DoneAt, 3);
		}

		/// <summary>
		/// Checks that a third piece for a hand queues as a change of its own after a weapon and shield joined together
		/// </summary>
		[Fact]
		public void QueuesAThirdHandPieceSeparately()
		{
			List<LivingVikingBody.GearChange> changes = new List<LivingVikingBody.GearChange>();
			PutOn(changes, 0f, ItemDrop.ItemData.ItemType.OneHandedWeapon, 1f);
			PutOn(changes, 0f, ItemDrop.ItemData.ItemType.Shield, 2f);
			LivingVikingBody.GearChange third = PutOn(changes, 0f, ItemDrop.ItemData.ItemType.OneHandedWeapon, 1f);
			Assert.Equal(2, changes.Count);
			Assert.False(third.Paired);
			Assert.Equal(2f, third.Start, 3);
			Assert.Equal(3f, third.DoneAt, 3);
		}

		/// <summary>
		/// Checks that a shield doesn't join a weapon change that's put on or taken off differently, picked by a player
		/// when the other isn't, or found when the other isn't
		/// </summary>
		/// <param name="putOn">Whether the shield is put on - the weapon is</param>
		/// <param name="chosen">Whether a player picked the shield - not the weapon</param>
		/// <param name="found">Whether the viking found the shield - not the weapon</param>
		[Theory]
		[InlineData(false, false, false)]
		[InlineData(true, true, false)]
		[InlineData(true, false, true)]
		public void QueuesDifferentChangesSeparately(bool putOn, bool chosen, bool found)
		{
			List<LivingVikingBody.GearChange> changes = new List<LivingVikingBody.GearChange>();
			PutOn(changes, 0f, ItemDrop.ItemData.ItemType.OneHandedWeapon, 1f);
			LivingVikingBody.GearChange shield = LivingVikingBodyRules.QueueChange(changes, 0f, ItemDrop.ItemData.ItemType.Shield, 2f, putOn, chosen, found);
			Assert.Equal(2, changes.Count);
			Assert.False(changes[0].Paired);
			Assert.Equal(1f, shield.Start, 3);
			Assert.Equal(3f, shield.DoneAt, 3);
		}

		/// <summary>
		/// Checks that pieces that don't go in a hand each queue one after another
		/// </summary>
		/// <param name="first">The first piece's item type</param>
		/// <param name="second">The second's</param>
		[Theory]
		[InlineData(ItemDrop.ItemData.ItemType.OneHandedWeapon, ItemDrop.ItemData.ItemType.OneHandedWeapon)]
		[InlineData(ItemDrop.ItemData.ItemType.Shield, ItemDrop.ItemData.ItemType.Shield)]
		[InlineData(ItemDrop.ItemData.ItemType.TwoHandedWeapon, ItemDrop.ItemData.ItemType.Shield)]
		[InlineData(ItemDrop.ItemData.ItemType.Helmet, ItemDrop.ItemData.ItemType.Shield)]
		public void QueuesOtherPiecesSeparately(ItemDrop.ItemData.ItemType first, ItemDrop.ItemData.ItemType second)
		{
			List<LivingVikingBody.GearChange> changes = new List<LivingVikingBody.GearChange>();
			PutOn(changes, 0f, first, 1f);
			PutOn(changes, 0f, second, 1f);
			Assert.Equal(2, changes.Count);
			Assert.False(LivingVikingBodyRules.HandPair(first, second));
		}

		/// <summary>
		/// Checks that a one-handed weapon and a shield go in a hand each whichever comes first
		/// </summary>
		[Fact]
		public void PairsAWeaponAndShieldEitherWay()
		{
			Assert.True(LivingVikingBodyRules.HandPair(ItemDrop.ItemData.ItemType.OneHandedWeapon, ItemDrop.ItemData.ItemType.Shield));
			Assert.True(LivingVikingBodyRules.HandPair(ItemDrop.ItemData.ItemType.Shield, ItemDrop.ItemData.ItemType.OneHandedWeapon));
		}

		/// <summary>
		/// Checks that an equip time below 0 counts as none, for a change of its own and for a piece joining one
		/// </summary>
		[Fact]
		public void CountsANegativeEquipTimeAsNone()
		{
			List<LivingVikingBody.GearChange> changes = new List<LivingVikingBody.GearChange>();
			LivingVikingBody.GearChange helmet = PutOn(changes, 10f, ItemDrop.ItemData.ItemType.Helmet, -1f);
			Assert.Equal(10f, helmet.DoneAt, 3);
			LivingVikingBody.GearChange weapon = PutOn(changes, 10f, ItemDrop.ItemData.ItemType.OneHandedWeapon, 1f);
			PutOn(changes, 10f, ItemDrop.ItemData.ItemType.Shield, -3f);
			Assert.Equal(10f, weapon.Start, 3);
			Assert.Equal(11f, weapon.DoneAt, 3);
		}

		/// <summary>
		/// Checks that the viking heals more than the game healed it by a multiplier above 1, less by one below 1, and
		/// the same by 1 or when the game didn't heal it
		/// </summary>
		/// <param name="healed">How much the game healed it</param>
		/// <param name="multiplier">Its regen multiplier</param>
		/// <param name="expected">The health to add, below 0 to take back</param>
		[Theory]
		[InlineData(10f, 1.5f, 5f)]
		[InlineData(10f, 0.5f, -5f)]
		[InlineData(8f, 0.75f, -2f)]
		[InlineData(10f, 1f, 0f)]
		[InlineData(0f, 2f, 0f)]
		[InlineData(-4f, 2f, 0f)]
		[InlineData(-4f, 0.5f, 0f)]
		public void ChangesHealingByRest(float healed, float multiplier, float expected)
		{
			Assert.Equal(expected, LivingVikingBodyRules.RegenDelta(healed, multiplier), 3);
		}

		/// <summary>
		/// Checks that the weight shows plainly up to the most the viking can carry, and in red past it only while the
		/// flash is on
		/// </summary>
		/// <param name="weight">The weight it carries</param>
		/// <param name="flash">Whether the flash is on</param>
		/// <param name="expected">The text, with 300 the most it can carry</param>
		[Theory]
		[InlineData(120, true, "120/300")]
		[InlineData(120, false, "120/300")]
		[InlineData(300, true, "300/300")]
		[InlineData(301, true, "<color=red>301</color>/300")]
		[InlineData(301, false, "301/300")]
		public void ShowsWeight(int weight, bool flash, string expected)
		{
			Assert.Equal(expected, LivingVikingBodyRules.FormatWeight(weight, 300, flash));
		}

		/// <summary>
		/// Checks that a fall hurts the same as a player's - nothing up to 4 meters, rising evenly to 100 at 20 meters
		/// and no more past it
		/// </summary>
		/// <param name="height">How far the viking fell, in meters</param>
		/// <param name="expected">The damage</param>
		[Theory]
		[InlineData(0f, 0f)]
		[InlineData(3f, 0f)]
		[InlineData(4f, 0f)]
		[InlineData(8f, 25f)]
		[InlineData(12f, 50f)]
		[InlineData(20f, 100f)]
		[InlineData(50f, 100f)]
		public void HurtsByFallHeight(float height, float expected)
		{
			Assert.Equal(expected, LivingVikingBodyRules.FallDamage(height), 3);
		}
	}
}
