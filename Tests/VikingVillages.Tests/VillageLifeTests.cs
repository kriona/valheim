using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the rules a living viking's daily life follows - storing things in chests, sharing out jobs, running from
	/// fights, gathering around fires, jumping fences, filling a quiver and claiming beds, chairs and fires
	/// </summary>
	public class VillageLifeTests
	{
		/// <summary>
		/// How long a day is in the tests, in seconds - the game's own
		/// </summary>
		private const float Day = 1200f;

		/// <summary>
		/// Checks that an empty chest takes one first stack of loot, keeping a slot for wood and one for food, and none
		/// once the chest or the village has enough stacks of it
		/// </summary>
		/// <param name="holdsItem">Whether the chest holds the item already</param>
		/// <param name="villageStacks">How many stacks of it the village's chests hold</param>
		/// <param name="stackRoom">How many more its stacks of the item take</param>
		/// <param name="expected">How many fit</param>
		[Theory]
		[InlineData(false, 0, 0, 50)]
		[InlineData(false, 1, 0, 50)]
		[InlineData(false, 2, 0, 0)]
		[InlineData(false, 3, 0, 0)]
		[InlineData(true, 1, 20, 20)]
		[InlineData(true, 0, 0, 0)]
		public void StoresLootAsOneFirstStack(bool holdsItem, int villageStacks, int stackRoom, int expected)
		{
			Assert.Equal(expected, VillageLife.ChestRoomFor(VillageLife.StoreKind.Other, 10, (holdsItem ? 1 : 0), 0, false, holdsItem, villageStacks, stackRoom, 50));
		}

		/// <summary>
		/// Checks that loot leaves a chest's last free slots for wood and food until it holds some of each
		/// </summary>
		[Fact]
		public void KeepsSlotsFreeForWoodAndFood()
		{
			// Ten slots with eight used and no wood or food leave the last two for them
			Assert.Equal(0, VillageLife.ChestRoomFor(VillageLife.StoreKind.Other, 10, 8, 0, false, false, 0, 0, 50));
			// Holding wood, only the food slot is kept
			Assert.Equal(50, VillageLife.ChestRoomFor(VillageLife.StoreKind.Other, 10, 8, 1, false, false, 0, 0, 50));
			// Holding both, the last slot takes loot
			Assert.Equal(50, VillageLife.ChestRoomFor(VillageLife.StoreKind.Other, 10, 9, 1, true, false, 0, 0, 50));
		}

		/// <summary>
		/// Checks that food fills a chest's empty slots but one kept for wood, and wood those but one kept for food
		/// </summary>
		[Fact]
		public void KeepsTheOtherSlotForWoodAndFood()
		{
			Assert.Equal(180, VillageLife.ChestRoomFor(VillageLife.StoreKind.Food, 10, 0, 0, false, false, 0, 0, 20));
			Assert.Equal(0, VillageLife.ChestRoomFor(VillageLife.StoreKind.Food, 10, 9, 0, false, false, 0, 0, 20));
			Assert.Equal(20, VillageLife.ChestRoomFor(VillageLife.StoreKind.Food, 10, 9, 1, false, false, 0, 0, 20));
			Assert.Equal(0, VillageLife.ChestRoomFor(VillageLife.StoreKind.Wood, 10, 9, 3, false, false, 0, 0, 50));
			Assert.Equal(50, VillageLife.ChestRoomFor(VillageLife.StoreKind.Wood, 10, 9, 3, true, false, 0, 0, 50));
		}

		/// <summary>
		/// Checks that wood fills no more than half a chest's slots, with the room on its stacks still taken once it
		/// does
		/// </summary>
		[Fact]
		public void CapsWoodAtItsShare()
		{
			// Nine slots free past the food slot, but only five may hold wood
			Assert.Equal(250, VillageLife.ChestRoomFor(VillageLife.StoreKind.Wood, 10, 0, 0, false, false, 0, 0, 50));
			Assert.Equal(100, VillageLife.ChestRoomFor(VillageLife.StoreKind.Wood, 10, 3, 3, false, true, 0, 0, 50));
			Assert.Equal(10, VillageLife.ChestRoomFor(VillageLife.StoreKind.Wood, 10, 5, 5, false, true, 0, 10, 50));
			Assert.Equal(0, VillageLife.ChestRoomFor(VillageLife.StoreKind.Wood, 7, 4, 4, true, true, 0, 0, 50));
		}

		/// <summary>
		/// Checks that a full chest has no room, never less, and still takes what fits on its stacks
		/// </summary>
		[Fact]
		public void FullChestHasNoRoom()
		{
			Assert.Equal(0, VillageLife.ChestRoomFor(VillageLife.StoreKind.Other, 10, 10, 0, false, false, 0, 0, 50));
			Assert.Equal(0, VillageLife.ChestRoomFor(VillageLife.StoreKind.Food, 10, 10, 0, false, false, 0, 0, 20));
			Assert.Equal(0, VillageLife.ChestRoomFor(VillageLife.StoreKind.Wood, 10, 10, 8, false, true, 0, 0, 50));
			Assert.Equal(7, VillageLife.ChestRoomFor(VillageLife.StoreKind.Food, 10, 10, 0, false, true, 0, 7, 20));
		}

		/// <summary>
		/// Checks that one viking does a job for every so many, rounded up, and always at least one
		/// </summary>
		/// <param name="vikings">How many vikings the village has</param>
		/// <param name="perJob">How many there are for each one doing the job</param>
		/// <param name="expected">How many may do it at once</param>
		[Theory]
		[InlineData(0, 4, 1)]
		[InlineData(1, 4, 1)]
		[InlineData(4, 4, 1)]
		[InlineData(5, 4, 2)]
		[InlineData(8, 4, 2)]
		[InlineData(9, 4, 3)]
		[InlineData(0, int.MaxValue, 1)]
		[InlineData(100, int.MaxValue, 1)]
		[InlineData(3, 1, 3)]
		public void SharesOutJobs(int vikings, int perJob, int expected)
		{
			Assert.Equal(expected, VillageLife.AllowedDoing(vikings, perJob));
		}

		/// <summary>
		/// Checks that a viking at home starts hanging back below a quarter of its health and keeps on until it's back
		/// to two fifths
		/// </summary>
		/// <param name="health">Its share of its health</param>
		/// <param name="hangingBack">Whether it's hanging back already</param>
		/// <param name="expected">Whether it hangs back</param>
		[Theory]
		[InlineData(0.2f, false, true)]
		[InlineData(0.25f, false, false)]
		[InlineData(0.3f, false, false)]
		[InlineData(0.3f, true, true)]
		[InlineData(0.39f, true, true)]
		[InlineData(0.4f, true, false)]
		[InlineData(0.9f, true, false)]
		public void HangsBackUntilRecovered(float health, bool hangingBack, bool expected)
		{
			Assert.Equal(expected, VillageLife.HangsBack(health, hangingBack, 1, 2));
		}

		/// <summary>
		/// Checks that a badly hurt viking at home still fights when it's needed - with no healthy ally, or more enemies
		/// than healthy allies
		/// </summary>
		[Fact]
		public void FightsOnWhenNeeded()
		{
			Assert.True(VillageLife.HangsBack(0.1f, false, 1, 1));
			Assert.True(VillageLife.HangsBack(0.1f, false, 0, 1));
			Assert.False(VillageLife.HangsBack(0.1f, false, 2, 1));
			Assert.False(VillageLife.HangsBack(0.1f, false, 0, 0));
			Assert.False(VillageLife.HangsBack(0.1f, true, 3, 2));
		}

		/// <summary>
		/// Checks that only a viking that ran for being alone stops running, and only once another viking is beside it
		/// </summary>
		[Fact]
		public void StopsRunningOnceNotAlone()
		{
			Assert.True(VillageLife.StopsFleeing(VillageLife.FleeReason.Alone, 1));
			Assert.False(VillageLife.StopsFleeing(VillageLife.FleeReason.Alone, 0));
			Assert.False(VillageLife.StopsFleeing(VillageLife.FleeReason.Wounded, 3));
			Assert.False(VillageLife.StopsFleeing(VillageLife.FleeReason.Outnumbered, 2));
			Assert.False(VillageLife.StopsFleeing(VillageLife.FleeReason.Escort, 2));
		}

		/// <summary>
		/// Checks that a viking away from home runs below a quarter of its health, and a hunter below half
		/// </summary>
		[Fact]
		public void RunsWhenBadlyHurt()
		{
			Assert.Equal(VillageLife.FleeReason.Wounded, VillageLife.FleeFor(0.2f, false, false, 0, false));
			Assert.Equal(VillageLife.FleeReason.None, VillageLife.FleeFor(0.25f, false, false, 0, false));
			Assert.Equal(VillageLife.FleeReason.None, VillageLife.FleeFor(0.4f, false, false, 0, false));
			Assert.Equal(VillageLife.FleeReason.Wounded, VillageLife.FleeFor(0.4f, true, false, 0, false));
			Assert.Equal(VillageLife.FleeReason.Wounded, VillageLife.FleeFor(0.49f, true, false, 0, false));
			Assert.Equal(VillageLife.FleeReason.None, VillageLife.FleeFor(0.5f, true, false, 0, false));
		}

		/// <summary>
		/// Checks that a viking away from home runs from four or more enemies, not three
		/// </summary>
		[Fact]
		public void RunsWhenOutnumbered()
		{
			Assert.Equal(VillageLife.FleeReason.None, VillageLife.FleeFor(1f, false, false, 3, false));
			Assert.Equal(VillageLife.FleeReason.Outnumbered, VillageLife.FleeFor(1f, false, false, 4, false));
			Assert.Equal(VillageLife.FleeReason.Outnumbered, VillageLife.FleeFor(1f, true, false, 9, false));
		}

		/// <summary>
		/// Checks that running for being hurt comes before escorting a hurt ally, that before running from being
		/// outnumbered, and that before running for being attacked alone
		/// </summary>
		[Fact]
		public void WeighsReasonsInOrder()
		{
			Assert.Equal(VillageLife.FleeReason.Wounded, VillageLife.FleeFor(0.1f, false, true, 5, true));
			Assert.Equal(VillageLife.FleeReason.Escort, VillageLife.FleeFor(1f, false, true, 5, true));
			Assert.Equal(VillageLife.FleeReason.Outnumbered, VillageLife.FleeFor(1f, false, false, 5, true));
			Assert.Equal(VillageLife.FleeReason.Alone, VillageLife.FleeFor(1f, false, false, 1, true));
			Assert.Equal(VillageLife.FleeReason.None, VillageLife.FleeFor(1f, false, false, 1, false));
		}

		/// <summary>
		/// Checks how long the night watch's shift has left - the first's until midnight from noon, the second's until
		/// dawn from midnight, and none between dawn and noon
		/// </summary>
		/// <param name="fraction">How far through the day it is</param>
		/// <param name="expected">The seconds left</param>
		[Theory]
		[InlineData(0.5f, 600f)]
		[InlineData(0.75f, 300f)]
		[InlineData(0.9f, 120f)]
		[InlineData(0f, 300f)]
		[InlineData(0.1f, 180f)]
		[InlineData(0.25f, 0f)]
		[InlineData(0.3f, 0f)]
		[InlineData(0.49f, 0f)]
		public void CountsDownTheWatchShift(float fraction, float expected)
		{
			Assert.Equal(expected, VillageLife.ShiftLeft(fraction, Day), 2);
		}

		/// <summary>
		/// Checks that the time left runs out where VillageWatch's shift of that time ends - midnight for the first
		/// shift, dawn for the second
		/// </summary>
		[Fact]
		public void ShiftEndsWhereTheWatchChanges()
		{
			for (int i = 0; i < 100; i++)
			{
				float fraction = i / 100f;
				float left = VillageLife.ShiftLeft(fraction, Day);
				if (left <= 0f)
				{
					continue;
				}
				float end = fraction + left / Day;
				Assert.Equal(((VillageWatch.Shift(fraction) == 0) ? 1f : 0.25f), end, 3);
			}
		}

		/// <summary>
		/// Checks that the ring around a fire stays as it is while the fire has room for everyone gathering
		/// </summary>
		/// <param name="crowd">How many gather</param>
		/// <param name="room">How many it has room for</param>
		[Theory]
		[InlineData(1, 10)]
		[InlineData(10, 10)]
		[InlineData(0, 1)]
		public void KeepsTheRingWithRoom(int crowd, int room)
		{
			Assert.Equal(3.5f, VillageLife.WidenRing(2.5f, 3.5f, crowd, room), 4);
		}

		/// <summary>
		/// Checks that a ring twice as crowded as it has room for widens to twice the area, keeping its inside
		/// </summary>
		[Fact]
		public void WidensTheRingInStepWithTheCrowd()
		{
			float max = VillageLife.WidenRing(2f, 4f, 10, 5);
			// The ring's area was 4 * 4 - 2 * 2 = 12, twice that is 24, so its outside is the square root of 28
			Assert.Equal((float)System.Math.Sqrt(28.0), max, 4);
			Assert.Equal(24f, max * max - 2f * 2f, 3);
			// Half as crowded again, the ring's area of 16 grows to 24, around an inside of 3 squared
			Assert.Equal((float)System.Math.Sqrt(33.0), VillageLife.WidenRing(3f, 5f, 3, 2), 4);
			Assert.Equal(8f, VillageLife.WidenRing(0f, 4f, 4, 1), 3);
		}

		/// <summary>
		/// Checks that a ring around a fire narrows to within its warmth and clear of its flames, each by half a meter
		/// </summary>
		/// <param name="warmth">How far the warmth reaches</param>
		/// <param name="flames">How far the flames reach</param>
		/// <param name="min">The ring's inside</param>
		/// <param name="max">The ring's outside</param>
		/// <param name="expectedMin">Its inside after</param>
		/// <param name="expectedMax">Its outside after</param>
		[Theory]
		// No warmth or flames leave a campfire's ring as it is
		[InlineData(0f, 0f, 2.5f, 3.5f, 2.5f, 3.5f)]
		// Warmth reaching 3 pulls the outside in to 2.5, and the inside to a meter within it
		[InlineData(3f, 0f, 2.5f, 3.5f, 1.5f, 2.5f)]
		// A bonfire's ring within warmth reaching 5, clear of flames reaching 1.5
		[InlineData(5f, 1.5f, 4f, 5.5f, 3.5f, 4.5f)]
		// Flames reaching 2.5 push the inside out to 3
		[InlineData(0f, 2.5f, 2.5f, 3.5f, 3f, 3.5f)]
		// Warmth reaching less than the flames leaves the ring a single line clear of them
		[InlineData(1f, 2f, 2.5f, 3.5f, 2.5f, 2.5f)]
		public void NarrowsTheRingToTheWarmth(float warmth, float flames, float min, float max, float expectedMin, float expectedMax)
		{
			VillageLife.WarmRingWithin(warmth, flames, ref min, ref max);
			Assert.Equal(expectedMin, min, 4);
			Assert.Equal(expectedMax, max, 4);
		}

		/// <summary>
		/// Checks that a narrowed ring's inside is never past its outside, whatever the fire's warmth and flames
		/// </summary>
		[Fact]
		public void NarrowedRingKeepsItsInsideIn()
		{
			for (float warmth = 0f; warmth <= 8f; warmth += 0.5f)
			{
				for (float flames = 0f; flames <= 8f; flames += 0.5f)
				{
					float min = 2.5f;
					float max = 3.5f;
					VillageLife.WarmRingWithin(warmth, flames, ref min, ref max);
					Assert.True(min <= max);
					Assert.True(min >= flames + 0.5f);
				}
			}
		}

		/// <summary>
		/// Checks that a jump that can't get high enough is made from right in front of the fence, also when it only
		/// just reaches the top
		/// </summary>
		[Fact]
		public void JumpsFromInFrontWhenItCantClear()
		{
			Assert.Equal(0.1f, VillageLife.JumpFrom(5f, 10f, 2f, 5f, 1f), 4);
			Assert.Equal(0.1f, VillageLife.JumpFrom(10f, 10f, 5f, 5f, 1f), 4);
		}

		/// <summary>
		/// Checks how far out a jump clears a fence, worked by hand for a jump going up at 10 meters a second against
		/// gravity of 10 to clear 3 meters
		/// </summary>
		[Fact]
		public void JumpsFarEnoughOut()
		{
			// Standing still it jumps from where it is
			Assert.Equal(0f, VillageLife.JumpFrom(10f, 10f, 3f, 0f, 1f), 4);
			// It's above 3 meters from (10 - sqrt 40) / 10 = 0.3675 seconds for 2 sqrt 40 / 10 = 1.2649 seconds, so at 5
			// meters a second it jumps 1.8377 out, and 2.6623 farther for half the 5.3246 it carries past the fence
			Assert.Equal(4.5f, VillageLife.JumpFrom(10f, 10f, 3f, 5f, 1f), 3);
			// Too slow to carry past the fence, it jumps from where it gets above the top in time
			Assert.Equal((float)(0.5 * (10.0 - System.Math.Sqrt(40.0)) / 10.0), VillageLife.JumpFrom(10f, 10f, 3f, 0.5f, 1f), 4);
		}

		/// <summary>
		/// Checks that an archer whose own best arrows already fill its quiver takes none from the chest and keeps all
		/// its own
		/// </summary>
		[Fact]
		public void KeepsAFullQuiverOfTheBest()
		{
			List<VillageLife.ArrowStack> stacks = new List<VillageLife.ArrowStack>
			{
				new VillageLife.ArrowStack(null, 10f, false, 50),
				new VillageLife.ArrowStack(null, 20f, true, 100)
			};
			VillageLife.FillQuiver(stacks, 100);
			Assert.True(stacks[0].Own);
			Assert.Equal(100, stacks[0].Keep);
			Assert.Equal(0, stacks[1].Keep);
		}

		/// <summary>
		/// Checks that an archer takes a chest's better arrows first and puts back its own that no longer fit
		/// </summary>
		[Fact]
		public void TakesBetterArrowsAndPutsBackItsOwn()
		{
			List<VillageLife.ArrowStack> stacks = new List<VillageLife.ArrowStack>
			{
				new VillageLife.ArrowStack(null, 10f, true, 80),
				new VillageLife.ArrowStack(null, 20f, false, 50)
			};
			VillageLife.FillQuiver(stacks, 100);
			Assert.False(stacks[0].Own);
			Assert.Equal(50, stacks[0].Keep);
			Assert.True(stacks[1].Own);
			Assert.Equal(50, stacks[1].Keep);
			Assert.Equal(30, stacks[1].Stack - stacks[1].Keep);
		}

		/// <summary>
		/// Checks that of arrows as good as a chest's, the archer keeps its own and tops up from the chest
		/// </summary>
		[Fact]
		public void KeepsItsOwnOfEqualArrows()
		{
			List<VillageLife.ArrowStack> stacks = new List<VillageLife.ArrowStack>
			{
				new VillageLife.ArrowStack(null, 10f, false, 100),
				new VillageLife.ArrowStack(null, 10f, true, 60)
			};
			VillageLife.FillQuiver(stacks, 100);
			Assert.True(stacks[0].Own);
			Assert.Equal(60, stacks[0].Keep);
			Assert.Equal(40, stacks[1].Keep);
		}

		/// <summary>
		/// Checks that with fewer arrows than the quiver holds, the archer takes them all, best first
		/// </summary>
		[Fact]
		public void TakesAllWhenTheyFit()
		{
			List<VillageLife.ArrowStack> stacks = new List<VillageLife.ArrowStack>
			{
				new VillageLife.ArrowStack(null, 10f, true, 20),
				new VillageLife.ArrowStack(null, 5f, false, 30),
				new VillageLife.ArrowStack(null, 15f, false, 10)
			};
			VillageLife.FillQuiver(stacks, 100);
			Assert.Equal(15f, stacks[0].Damage);
			Assert.Equal(10f, stacks[1].Damage);
			Assert.Equal(5f, stacks[2].Damage);
			Assert.All(stacks, (VillageLife.ArrowStack arrows) => Assert.Equal(arrows.Stack, arrows.Keep));
		}

		/// <summary>
		/// Checks when a bed, chair or fire is free for a viking - unclaimed, claimed by itself, its claim run out or
		/// its holder dead, and not while another living viking's claim lasts
		/// </summary>
		[Fact]
		public void FreesClaimsThatLapse()
		{
			ZDOID me = new ZDOID(5L, 1u);
			ZDOID other = new ZDOID(6L, 2u);
			Assert.True(VillageLife.ClaimFree(ZDOID.None, me, 100L, 50L, false));
			Assert.True(VillageLife.ClaimFree(me, me, 100L, 50L, true));
			Assert.False(VillageLife.ClaimFree(other, me, 100L, 99L, true));
			Assert.True(VillageLife.ClaimFree(other, me, 100L, 100L, true));
			Assert.True(VillageLife.ClaimFree(other, me, 100L, 101L, true));
			Assert.True(VillageLife.ClaimFree(other, me, 100L, 50L, false));
		}

		/// <summary>
		/// Checks that vikings spread out on either side of a line in turn, a step farther for each pair
		/// </summary>
		/// <param name="i">Which of them it is</param>
		/// <param name="expected">How far it turns, in degrees</param>
		[Theory]
		[InlineData(0, 0f)]
		[InlineData(1, -25f)]
		[InlineData(2, 25f)]
		[InlineData(3, -50f)]
		[InlineData(4, 50f)]
		[InlineData(5, -75f)]
		public void FansOutOnEitherSide(int i, float expected)
		{
			Assert.Equal(expected, VillageLife.Fan(i, 25f), 4);
		}

		/// <summary>
		/// Checks that vikings gather farther around a bonfire than any other fire, whatever its name's case
		/// </summary>
		[Fact]
		public void GathersFartherAroundABonfire()
		{
			Assert.Equal(4f, VillageLife.FireRingFor("bonfire", 0f), 4);
			Assert.Equal(5.5f, VillageLife.FireRingFor("Bonfire", 1f), 4);
			Assert.Equal(4.75f, VillageLife.FireRingFor("BONFIRE", 0.5f), 4);
			Assert.Equal(2.5f, VillageLife.FireRingFor("fire_pit", 0f), 4);
			Assert.Equal(3f, VillageLife.FireRingFor("hearth", 0.5f), 4);
			Assert.Equal(3.5f, VillageLife.FireRingFor("fire_pit", 2f), 4);
		}

		/// <summary>
		/// Checks how many vikings fit around a circle 1.75 meters apart, and always at least one
		/// </summary>
		/// <param name="radius">The circle's radius</param>
		/// <param name="expected">How many fit</param>
		[Theory]
		// A campfire's middle, 2 pi 3 / 1.75 = 10.77
		[InlineData(3f, 10)]
		// A bonfire's middle, 2 pi 4.75 / 1.75 = 17.05
		[InlineData(4.75f, 17)]
		[InlineData(0.2f, 1)]
		[InlineData(0f, 1)]
		public void FitsVikingsAroundARing(float radius, int expected)
		{
			Assert.Equal(expected, VillageLife.RingRoom(radius));
		}

		/// <summary>
		/// Checks how near an archer gets to prey before it shoots, by the bows' arrow speeds and spreads from the game
		/// - the crude bow, the finewood bow, the huntsman bow, the 60 m/s bows and the crossbows
		/// </summary>
		/// <param name="speed">How fast the arrows fly, in meters a second</param>
		/// <param name="spread">How far they stray, in degrees</param>
		/// <param name="expected">The range, in meters</param>
		[Theory]
		[InlineData(35f, 1f, 24.5f)]
		[InlineData(50f, 1f, 29.79f)]
		[InlineData(50f, 0f, 35f)]
		[InlineData(60f, 0f, 42f)]
		[InlineData(200f, 0f, 50f)]
		public void SnipeRangeFollowsTheBow(float speed, float spread, float expected)
		{
			Assert.InRange(VillageLife.SnipeRangeFor(speed, spread), expected - 0.01f, expected + 0.01f);
		}

		/// <summary>
		/// Checks that a slow or wild bow still shoots from no nearer than the least range
		/// </summary>
		[Fact]
		public void SnipeRangeHasAFloor()
		{
			Assert.Equal(15f, VillageLife.SnipeRangeFor(10f, 0f));
			Assert.Equal(15f, VillageLife.SnipeRangeFor(60f, 20f));
		}

		/// <summary>
		/// Checks that vikings charge a tough monster together once enough of them have gathered against it, whoever
		/// can shoot
		/// </summary>
		/// <param name="shooters">Whether someone near can shoot it</param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ChargesOnceEnoughHaveGathered(bool shooters)
		{
			Assert.False(VillageLife.KeepsClear(3, 3, 3, shooters));
			Assert.False(VillageLife.KeepsClear(3, 5, 6, shooters));
		}

		/// <summary>
		/// Checks that a viking keeps clear of a tough monster while enough fellows are within call to gather against
		/// it, even with no one to shoot it
		/// </summary>
		[Fact]
		public void KeepsClearWhileTheOthersGather()
		{
			Assert.True(VillageLife.KeepsClear(3, 1, 3, false));
			Assert.True(VillageLife.KeepsClear(3, 2, 4, false));
		}

		/// <summary>
		/// Checks that with too few to take a tough monster on, a viking keeps clear of it while someone can shoot it,
		/// and fights it anyway otherwise
		/// </summary>
		[Fact]
		public void TooFewKiteOnlyWithShooters()
		{
			Assert.True(VillageLife.KeepsClear(4, 1, 2, true));
			Assert.False(VillageLife.KeepsClear(4, 1, 2, false));
		}

		/// <summary>
		/// Checks that a hunter swings around its prey only as far as it takes to come at its back - none when it's
		/// already behind it, just enough when that's within reach either way, and none when the prey faces it, rather
		/// than circling all the way around
		/// </summary>
		[Theory]
		[InlineData(0f, 0f)]
		[InlineData(20f, 0f)]
		[InlineData(-30f, 0f)]
		[InlineData(50f, 20f)]
		[InlineData(-50f, -20f)]
		[InlineData(90f, 60f)]
		[InlineData(-90f, -60f)]
		[InlineData(120f, 0f)]
		[InlineData(180f, 0f)]
		[InlineData(-150f, 0f)]
		public void StalksFromBehindWithoutCircling(float around, float expected)
		{
			Assert.Equal(expected, VillageLife.StalkSwing(around), 4);
		}

		/// <summary>
		/// Checks that a hunter never swings far around its prey, whichever way it faces
		/// </summary>
		[Fact]
		public void NeverSwingsFarAround()
		{
			for (float around = -180f; around <= 180f; around += 5f)
			{
				Assert.InRange(Mathf.Abs(VillageLife.StalkSwing(around)), 0f, 60f);
			}
		}

		/// <summary>
		/// Checks that a viking archer counts an enemy archer a little nearer than it is, and every other enemy at its
		/// distance
		/// </summary>
		/// <param name="distance">How far the enemy is</param>
		/// <param name="preferred">Whether it's an enemy archer and the viking an archer</param>
		/// <param name="expected">The distance it counts as</param>
		[Theory]
		[InlineData(10f, false, 10f)]
		[InlineData(10f, true, 8f)]
		[InlineData(0f, true, 0f)]
		public void ArcherCountsEnemyArchersNearer(float distance, bool preferred, float expected)
		{
			Assert.Equal(expected, VillageLife.TargetDistance(distance, preferred), 4);
		}

		/// <summary>
		/// Checks that the preference is slight - an enemy archer a little farther off is picked over a nearer enemy,
		/// but not one much farther off
		/// </summary>
		[Fact]
		public void PrefersEnemyArchersOnlySlightly()
		{
			Assert.True(VillageLife.TargetDistance(11f, true) < VillageLife.TargetDistance(10f, false));
			Assert.False(VillageLife.TargetDistance(15f, true) < VillageLife.TargetDistance(10f, false));
		}

		/// <summary>
		/// Checks that a viking stopping as near the spot it stands at as it walks to is always within reach of the piece
		/// - within 2.5m of it when the spot is near enough, or right at the spot when it's not, as when a sitting log's
		/// own ground can't be stood on and the spot is 2m off
		/// </summary>
		[Theory]
		[InlineData(0f)]
		[InlineData(0.5f)]
		[InlineData(1.5f)]
		[InlineData(2f)]
		[InlineData(2.5f)]
		public void StoppingAtTheStandReachIsWithinWorkReach(float offset)
		{
			float reach = VillageLife.StandReach(offset);
			// The farthest it can stop from the piece is the spot's offset plus how near it walks to the spot
			Assert.True(VillageLife.InWorkReach(offset + reach, reach));
		}

		/// <summary>
		/// Checks that a spot on the piece's own ground is walked to within the usual reach, and one far from the piece
		/// is walked right up to
		/// </summary>
		[Fact]
		public void StandReachShrinksAsTheSpotMovesOff()
		{
			Assert.Equal(2.5f, VillageLife.StandReach(0f));
			Assert.Equal(1.5f, VillageLife.StandReach(1f));
			Assert.Equal(1f, VillageLife.StandReach(2f));
		}

		/// <summary>
		/// Checks that a viking 4m from a piece and 2m from its spot isn't within reach - the case that left vikings
		/// standing still until they gave up on a repair
		/// </summary>
		[Fact]
		public void FourMetersOffIsNotWithinReach()
		{
			Assert.False(VillageLife.InWorkReach(4f, 2f));
			Assert.True(VillageLife.InWorkReach(2.5f, 2f));
			Assert.True(VillageLife.InWorkReach(4f, 1f));
		}
	}
}
