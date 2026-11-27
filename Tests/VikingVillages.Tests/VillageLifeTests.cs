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
			Assert.Equal(expected, VillageLife.ChestRoomFor(VillageLife.StoreKind.Other, 10, (holdsItem ? 1 : 0), 0, false, holdsItem, villageStacks, 2, stackRoom, 50));
		}

		/// <summary>
		/// Checks that loot leaves a chest's last free slots for wood and food until it holds some of each
		/// </summary>
		[Fact]
		public void KeepsSlotsFreeForWoodAndFood()
		{
			// Ten slots with eight used and no wood or food leave the last two for them
			Assert.Equal(0, VillageLife.ChestRoomFor(VillageLife.StoreKind.Other, 10, 8, 0, false, false, 0, 2, 0, 50));
			// Holding wood, only the food slot is kept
			Assert.Equal(50, VillageLife.ChestRoomFor(VillageLife.StoreKind.Other, 10, 8, 1, false, false, 0, 2, 0, 50));
			// Holding both, the last slot takes loot
			Assert.Equal(50, VillageLife.ChestRoomFor(VillageLife.StoreKind.Other, 10, 9, 1, true, false, 0, 2, 0, 50));
		}

		/// <summary>
		/// Checks that food fills a chest's empty slots but one kept for wood, and wood those but one kept for food
		/// </summary>
		[Fact]
		public void KeepsTheOtherSlotForWoodAndFood()
		{
			Assert.Equal(180, VillageLife.ChestRoomFor(VillageLife.StoreKind.Food, 10, 0, 0, false, false, 0, 2, 0, 20));
			Assert.Equal(0, VillageLife.ChestRoomFor(VillageLife.StoreKind.Food, 10, 9, 0, false, false, 0, 2, 0, 20));
			Assert.Equal(20, VillageLife.ChestRoomFor(VillageLife.StoreKind.Food, 10, 9, 1, false, false, 0, 2, 0, 20));
			Assert.Equal(0, VillageLife.ChestRoomFor(VillageLife.StoreKind.Wood, 10, 9, 3, false, false, 0, 2, 0, 50));
			Assert.Equal(50, VillageLife.ChestRoomFor(VillageLife.StoreKind.Wood, 10, 9, 3, true, false, 0, 2, 0, 50));
		}

		/// <summary>
		/// Checks that wood fills no more than half a chest's slots, with the room on its stacks still taken once it
		/// does
		/// </summary>
		[Fact]
		public void CapsWoodAtItsShare()
		{
			// Nine slots free past the food slot, but only five may hold wood
			Assert.Equal(250, VillageLife.ChestRoomFor(VillageLife.StoreKind.Wood, 10, 0, 0, false, false, 0, 2, 0, 50));
			Assert.Equal(100, VillageLife.ChestRoomFor(VillageLife.StoreKind.Wood, 10, 3, 3, false, true, 0, 2, 0, 50));
			Assert.Equal(10, VillageLife.ChestRoomFor(VillageLife.StoreKind.Wood, 10, 5, 5, false, true, 0, 2, 10, 50));
			Assert.Equal(0, VillageLife.ChestRoomFor(VillageLife.StoreKind.Wood, 7, 4, 4, true, true, 0, 2, 0, 50));
		}

		/// <summary>
		/// Checks that a full chest has no room, never less, and still takes what fits on its stacks
		/// </summary>
		[Fact]
		public void FullChestHasNoRoom()
		{
			Assert.Equal(0, VillageLife.ChestRoomFor(VillageLife.StoreKind.Other, 10, 10, 0, false, false, 0, 2, 0, 50));
			Assert.Equal(0, VillageLife.ChestRoomFor(VillageLife.StoreKind.Food, 10, 10, 0, false, false, 0, 2, 0, 20));
			Assert.Equal(0, VillageLife.ChestRoomFor(VillageLife.StoreKind.Wood, 10, 10, 8, false, true, 0, 2, 0, 50));
			Assert.Equal(7, VillageLife.ChestRoomFor(VillageLife.StoreKind.Food, 10, 10, 0, false, true, 0, 2, 7, 20));
		}

		/// <summary>
		/// Checks that tools, which don't stack, go into a chest beside one already there, until the village keeps as
		/// many as it wants
		/// </summary>
		/// <param name="villageStacks">How many of the tool the village's chests hold</param>
		/// <param name="expected">How many fit</param>
		[Theory]
		[InlineData(1, 1)]
		[InlineData(3, 1)]
		[InlineData(4, 0)]
		public void KeepsToolsBesideEachOther(int villageStacks, int expected)
		{
			Assert.Equal(expected, VillageLife.ChestRoomFor(VillageLife.StoreKind.Other, 10, 1, 1, true, true, villageStacks, VillageLife.PickaxesWanted, 0, 1));
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
		/// Checks that a fence is jumped from ground its top stands up to 1.3 meters above - the 1.4 a viking jumps less
		/// the margin - and not from the foot of a bank below it, like a hillside pen's downhill side, 1.85 meters under
		/// the top
		/// </summary>
		[Fact]
		public void JumpsAFenceOnlyFromGroundNearItsTop()
		{
			Assert.True(VillageLife.HopFrom(103f, 104.25f));
			Assert.True(VillageLife.HopFrom(102.97f, 104.25f));
			Assert.False(VillageLife.HopFrom(102.9f, 104.25f));
			Assert.False(VillageLife.HopFrom(102.4f, 104.25f));
			// A fence sunk into the hillside, its top level with the ground outside, is jumped
			Assert.True(VillageLife.HopFrom(104.6f, 104.53f));
		}

		/// <summary>
		/// Checks that a viking backs up for a run-up over level or rising ground, and ground falling up to 0.3 meters,
		/// but not down a slope falling away from the fence
		/// </summary>
		[Fact]
		public void BacksUpOnlyWhereTheGroundDoesntFallAway()
		{
			Assert.True(VillageLife.RunUpLevel(103f, 103f));
			Assert.True(VillageLife.RunUpLevel(103f, 104f));
			Assert.True(VillageLife.RunUpLevel(103f, 102.75f));
			Assert.False(VillageLife.RunUpLevel(103f, 102.6f));
			Assert.False(VillageLife.RunUpLevel(103f, 100.1f));
		}

		/// <summary>
		/// Checks that a viking shut in a pen climbs over its fence on its fourth jump out, however far apart they come,
		/// and counts from none again after it
		/// </summary>
		[Fact]
		public void ClimbsOutOfAPenOnTheFourthJump()
		{
			int hops = 0;
			Assert.False(VillageLife.PenHopDue(ref hops, true));
			Assert.False(VillageLife.PenHopDue(ref hops, true));
			Assert.False(VillageLife.PenHopDue(ref hops, true));
			Assert.True(VillageLife.PenHopDue(ref hops, true));
			Assert.Equal(0, hops);
			Assert.False(VillageLife.PenHopDue(ref hops, true));
		}

		/// <summary>
		/// Checks that a jump made while not shut in, like one into a pen or one that got out, starts the count over
		/// </summary>
		[Fact]
		public void StartsCountingPenJumpsOverOnceOut()
		{
			int hops = 0;
			Assert.False(VillageLife.PenHopDue(ref hops, true));
			Assert.False(VillageLife.PenHopDue(ref hops, true));
			Assert.False(VillageLife.PenHopDue(ref hops, true));
			Assert.False(VillageLife.PenHopDue(ref hops, false));
			Assert.Equal(0, hops);
			Assert.False(VillageLife.PenHopDue(ref hops, true));
			Assert.False(VillageLife.PenHopDue(ref hops, true));
			Assert.False(VillageLife.PenHopDue(ref hops, true));
			Assert.True(VillageLife.PenHopDue(ref hops, true));
		}

		/// <summary>
		/// Checks that a jump east over a fence whose face is at x 10 and whose top is 1.3 meters up counts as over once
		/// the viking is 0.4 meters past the face, whatever the ground's height there
		/// </summary>
		[Fact]
		public void JumpsCountOverPastTheFence()
		{
			Vector3 face = new Vector3(10f, 0.4f, 5f);
			Assert.Equal(VillageLife.HopOutcome.Over, VillageLife.HopResult(face, Vector3.right, new Vector3(11.5f, -0.1f, 5f), 1.3f));
			Assert.Equal(VillageLife.HopOutcome.Over, VillageLife.HopResult(face, Vector3.right, new Vector3(10.45f, 0f, 5f), 1.3f));
			Assert.Equal(VillageLife.HopOutcome.Over, VillageLife.HopResult(face, Vector3.right, new Vector3(10.6f, 1.3f, 5f), 1.3f));
		}

		/// <summary>
		/// Checks that a viking short of 0.4 meters past a fence's face counts as on top of it with its feet within 0.3
		/// meters of the fence's top, and as fallen back below that - including one that ended up off to the side
		/// </summary>
		[Fact]
		public void JumpsCountOnTopOrFallenBack()
		{
			Vector3 face = new Vector3(10f, 0.4f, 5f);
			Assert.Equal(VillageLife.HopOutcome.OnTop, VillageLife.HopResult(face, Vector3.right, new Vector3(10.2f, 1.35f, 5f), 1.3f));
			Assert.Equal(VillageLife.HopOutcome.OnTop, VillageLife.HopResult(face, Vector3.right, new Vector3(10.1f, 1.05f, 5f), 1.3f));
			Assert.Equal(VillageLife.HopOutcome.FellBack, VillageLife.HopResult(face, Vector3.right, new Vector3(9f, 0f, 5f), 1.3f));
			Assert.Equal(VillageLife.HopOutcome.FellBack, VillageLife.HopResult(face, Vector3.right, new Vector3(10.2f, 0.9f, 5f), 1.3f));
			Assert.Equal(VillageLife.HopOutcome.FellBack, VillageLife.HopResult(face, Vector3.right, new Vector3(10f, 0f, 8f), 1.3f));
		}

		/// <summary>
		/// Checks the words each way a jump over a fence can end is logged with
		/// </summary>
		[Fact]
		public void NamesHowJumpsEnd()
		{
			Assert.Equal("got over", VillageLife.HopOutcomeName(VillageLife.HopOutcome.Over));
			Assert.Equal("ended up on top of it", VillageLife.HopOutcomeName(VillageLife.HopOutcome.OnTop));
			Assert.Equal("fell back", VillageLife.HopOutcomeName(VillageLife.HopOutcome.FellBack));
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
		/// Checks that what the test log says a viking does about a tough monster matches whether it keeps clear - it
		/// charges or fights it anyway only when it doesn't
		/// </summary>
		/// <param name="needed">How many vikings it takes</param>
		/// <param name="gathered">How many have gathered</param>
		/// <param name="called">How many are within call</param>
		/// <param name="shooters">Whether someone near can shoot it</param>
		/// <param name="expected">What it's written as doing</param>
		[Theory]
		[InlineData(3, 3, 3, false, "charges it together")]
		[InlineData(3, 5, 6, true, "charges it together")]
		[InlineData(3, 1, 3, false, "keeps clear while the rest gather")]
		[InlineData(4, 1, 2, true, "keeps clear while it's shot")]
		[InlineData(4, 1, 2, false, "fights it anyway, with too few and no one to shoot it")]
		public void WritesWhatItDoesAboutAToughMonster(int needed, int gathered, int called, bool shooters, string expected)
		{
			Assert.Equal(expected, VillageLife.ToughDecision(needed, gathered, called, shooters));
			bool fights = (expected.StartsWith("charges") || expected.StartsWith("fights"));
			Assert.Equal(!fights, VillageLife.KeepsClear(needed, gathered, called, shooters));
		}

		/// <summary>
		/// Checks that a viking kites a poison blob only when it doesn't resist poison and someone can shoot the blob
		/// </summary>
		[Fact]
		public void KitesPoisonBlobsWithoutResistance()
		{
			Assert.True(VillageLife.KitesBlob(true, true, HitData.DamageModifier.Normal, true));
			Assert.True(VillageLife.KitesBlob(true, true, HitData.DamageModifier.Weak, true));
			Assert.False(VillageLife.KitesBlob(true, true, HitData.DamageModifier.Normal, false));
			Assert.False(VillageLife.KitesBlob(true, true, HitData.DamageModifier.Resistant, true));
			Assert.False(VillageLife.KitesBlob(true, false, HitData.DamageModifier.Normal, true));
			Assert.False(VillageLife.KitesBlob(false, true, HitData.DamageModifier.Normal, true));
		}

		/// <summary>
		/// Checks which poison damage modifiers count as resisting poison
		/// </summary>
		/// <param name="poison">The modifier</param>
		/// <param name="expected">Whether it resists</param>
		[Theory]
		[InlineData(HitData.DamageModifier.Normal, false)]
		[InlineData(HitData.DamageModifier.Weak, false)]
		[InlineData(HitData.DamageModifier.VeryWeak, false)]
		[InlineData(HitData.DamageModifier.SlightlyWeak, false)]
		[InlineData(HitData.DamageModifier.SlightlyResistant, true)]
		[InlineData(HitData.DamageModifier.Resistant, true)]
		[InlineData(HitData.DamageModifier.VeryResistant, true)]
		[InlineData(HitData.DamageModifier.Immune, true)]
		[InlineData(HitData.DamageModifier.Ignore, true)]
		public void ResistsPoison(HitData.DamageModifier poison, bool expected)
		{
			Assert.Equal(expected, VillageLife.ResistsPoison(poison));
		}

		/// <summary>
		/// Checks that the game's blobs are told apart by their prefab names
		/// </summary>
		/// <param name="prefab">The prefab name</param>
		/// <param name="expected">Whether it's a blob</param>
		[Theory]
		[InlineData("Blob", true)]
		[InlineData("BlobElite", true)]
		[InlineData("BlobTar", true)]
		[InlineData("Draugr", false)]
		[InlineData("blob", false)]
		[InlineData(null, false)]
		public void TellsBlobs(string prefab, bool expected)
		{
			Assert.Equal(expected, VillageLife.IsBlob(prefab));
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
		/// Checks that a viking walking to the spot it stands at to use a chest is near enough the chest once it stops -
		/// within the 1.5m chest reach when the spot is near the chest, or right at the spot when the spot is farther,
		/// like 1.6m off past a bed, where it would have stopped short before
		/// </summary>
		[Theory]
		[InlineData(0.3f)]
		[InlineData(1f)]
		[InlineData(1.6f)]
		[InlineData(2.5f)]
		public void StoppingAtTheChestStandIsWithinReach(float offset)
		{
			float reach = VillageLife.StandReach(offset, 1.5f);
			Assert.True(VillageLife.InReach(offset + reach, reach, 1.5f));
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
		/// Checks that food counts as scarce with less than a food for each viking, so the village sends vikings out for
		/// more, and that a village with no vikings loaded counts as one
		/// </summary>
		[Theory]
		[InlineData(7, 8, true)]
		[InlineData(8, 8, false)]
		[InlineData(20, 8, false)]
		[InlineData(0, 0, true)]
		[InlineData(1, 0, false)]
		public void FoodScarceBelowOneEach(int food, int vikings, bool expected)
		{
			Assert.Equal(expected, VillageLife.FoodScarceFor(food, vikings));
		}

		/// <summary>
		/// Checks that there's food enough to go around with three foods for every viking, and not with fewer, and that a
		/// village with no vikings loaded still needs three
		/// </summary>
		[Theory]
		[InlineData(18, 6, true)]
		[InlineData(30, 6, true)]
		[InlineData(17, 6, false)]
		[InlineData(0, 6, false)]
		[InlineData(3, 0, true)]
		[InlineData(2, 0, false)]
		public void FoodEnoughForThreeEach(int food, int vikings, bool expected)
		{
			Assert.Equal(expected, VillageLife.FoodEnough(food, vikings));
		}

		/// <summary>
		/// Checks that the snapshot's food line shows how far short of three foods each the chests are, and says when no
		/// viking is loaded to look for wild prey
		/// </summary>
		[Fact]
		public void FoodLineShowsHowShortTheChestsAre()
		{
			Assert.Equal("Food: 14 in the chests for 7 vikings loaded, low, 7 short of 21, 0 raw, 1 livestock, no wild prey that drops food near", VillageLife.DescribeFoodFor(14, 0, 7, 0, 1, false));
			Assert.Equal("Food: 30 in the chests for 6 vikings loaded, enough, 4 raw, 0 livestock, wild prey that drops food near", VillageLife.DescribeFoodFor(30, 0, 6, 4, 0, true));
			Assert.Equal("Food: 0 in the chests for 0 vikings loaded, low, 3 short of 3, 0 raw, 2 livestock, no viking loaded to look for wild prey", VillageLife.DescribeFoodFor(0, 0, 0, 0, 2, null));
		}

		/// <summary>
		/// Checks that the snapshot's food line says how much food was left out in chests out of reach, and counts the
		/// village as short of food without it
		/// </summary>
		[Fact]
		public void FoodLineShowsFoodOutOfReach()
		{
			Assert.Equal("Food: 0 in the chests (10 more left out, in chests a viking couldn't get to lately) for 1 vikings loaded, low, 3 short of 3, 0 raw, 1 livestock, no wild prey that drops food near", VillageLife.DescribeFoodFor(0, 10, 1, 0, 1, false));
		}

		/// <summary>
		/// Checks that a chest a viking couldn't get to stays out of reach for its village's food for ten minutes after
		/// </summary>
		/// <param name="since">How long ago a viking couldn't get to it, in seconds</param>
		/// <param name="expected">Whether it's still out of reach</param>
		[Theory]
		[InlineData(0f, true)]
		[InlineData(599f, true)]
		[InlineData(600f, false)]
		[InlineData(3600f, false)]
		public void ChestOutOfReachForTenMinutes(float since, bool expected)
		{
			Assert.Equal(expected, VillageLife.OutOfReach(1000f, 1000f + since));
		}

		/// <summary>
		/// Checks that a viking only gets food as day work while food is low and it may hunt, hunting wild prey that drops
		/// food when it can, and slaughtering livestock only with no such prey about for any of the village's hunters
		/// </summary>
		/// <param name="foodLow">Whether the chests hold too little food</param>
		/// <param name="mayHunt">Whether the viking may hunt</param>
		/// <param name="preyNear">Whether there's prey that drops food the viking hunts</param>
		/// <param name="wildFoodNear">Whether there's prey that drops food for any of the village's hunters</param>
		/// <param name="livestock">Whether there's livestock to slaughter</param>
		/// <param name="expected">The name of what it does, since the test can't take the internal enum itself</param>
		[Theory]
		[InlineData(false, true, true, true, true, "None")]
		[InlineData(true, false, true, true, true, "None")]
		[InlineData(true, true, true, true, true, "Hunt")]
		[InlineData(true, true, true, true, false, "Hunt")]
		[InlineData(true, true, false, false, true, "Slaughter")]
		[InlineData(true, true, false, true, true, "None")]
		[InlineData(true, true, false, false, false, "None")]
		public void FoodWorkHuntsFirstThenSlaughters(bool foodLow, bool mayHunt, bool preyNear, bool wildFoodNear, bool livestock, string expected)
		{
			Assert.Equal(expected, VillageLife.PickFoodWork(foodLow, mayHunt, preyNear, wildFoodNear, livestock).ToString());
		}

		/// <summary>
		/// Checks that a cook only sets off for a chest for more raw food with nothing on its stations done or still
		/// cooking, room on them, none carried, no chest tried since it last took food off, and by day
		/// </summary>
		/// <param name="done">Whether done food is on its stations</param>
		/// <param name="carries">Whether it carries raw food</param>
		/// <param name="cooking">Whether food is still cooking on its stations</param>
		/// <param name="room">Whether a station has room</param>
		/// <param name="tried">Whether it has tried a chest</param>
		/// <param name="evening">Whether it's dusk or night</param>
		/// <param name="expected">Whether it fetches</param>
		[Theory]
		[InlineData(false, false, false, true, false, false, true)]
		[InlineData(false, false, true, true, false, false, false)]
		[InlineData(true, false, false, true, false, false, false)]
		[InlineData(false, true, false, true, false, false, false)]
		[InlineData(false, false, false, false, false, false, false)]
		[InlineData(false, false, false, true, true, false, false)]
		[InlineData(false, false, false, true, false, true, false)]
		public void CookFetchesOnlyWithNothingCooking(bool done, bool carries, bool cooking, bool room, bool tried, bool evening, bool expected)
		{
			Assert.Equal(expected, VillageLife.FetchesRaw(done, carries, cooking, room, tried, evening));
		}

		/// <summary>
		/// Checks that a cook that never left its stations for a chest says so, with how long it has held them
		/// </summary>
		[Fact]
		public void CookTripSaysWhenItNeverLeft()
		{
			Assert.Equal("it hasn't left its stations for a chest, holding them for 42s", VillageLife.DescribeCookTrip(42f, -1f, 0f, 0f, -1f));
		}

		/// <summary>
		/// Checks that a cook's last trip to a chest gives how long ago it set off, how far it got and when it turned back
		/// </summary>
		[Fact]
		public void CookTripGivesWhenAndHowFar()
		{
			Assert.Equal("it last left its stations for a chest 53.8m off 74s ago, getting 41.2m from them and turning back after 52s, holding them for 120s", VillageLife.DescribeCookTrip(120f, 74f, 53.8f, 41.2f, 52f));
		}

		/// <summary>
		/// Checks that a cook still on its way to a chest says so
		/// </summary>
		[Fact]
		public void CookTripSaysWhenStillOnItsWay()
		{
			Assert.EndsWith("getting 10.0m from them and still on its way, holding them for 30s", VillageLife.DescribeCookTrip(30f, 12f, 20f, 10f, -1f));
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

		/// <summary>
		/// Checks that a smelter is given as much ore as it has room for, and the coal to smelt that and what it already
		/// holds, as much as it has room for
		/// </summary>
		/// <param name="wanted">How much ore is wanted smelted</param>
		/// <param name="queued">How much it holds already</param>
		/// <param name="fuel">The fuel it holds</param>
		/// <param name="expectedOre">How much ore goes in</param>
		/// <param name="expectedFuel">How much fuel goes in</param>
		[Theory]
		[InlineData(5, 0, 0f, 5, 10)]
		[InlineData(20, 0, 0f, 10, 20)]
		[InlineData(20, 4, 0f, 6, 20)]
		[InlineData(3, 0, 4f, 3, 2)]
		[InlineData(3, 0, 1.5f, 3, 5)]
		[InlineData(0, 3, 0f, 0, 6)]
		[InlineData(0, 3, 6f, 0, 0)]
		[InlineData(0, 10, 15f, 0, 5)]
		public void LoadsASmelterWithTheFuelForItsOre(int wanted, int queued, float fuel, int expectedOre, int expectedFuel)
		{
			VillageLife.SmeltLoad(wanted, queued, 10, fuel, 20, 2, out int ore, out int fuelAdd);
			Assert.Equal(expectedOre, ore);
			Assert.Equal(expectedFuel, fuelAdd);
		}

		/// <summary>
		/// Checks that a kiln, which burns nothing, is given only what it smelts
		/// </summary>
		[Fact]
		public void LoadsAKilnWithNoFuel()
		{
			VillageLife.SmeltLoad(30, 5, 25, 0f, 0, 0, out int ore, out int fuelAdd);
			Assert.Equal(20, ore);
			Assert.Equal(0, fuelAdd);
		}

		/// <summary>
		/// Checks that every village keeps axes, and only one that can go mining keeps pickaxes
		/// </summary>
		[Fact]
		public void KeepsPickaxesOnlyWhereThereIsOre()
		{
			Assert.Equal(VillageLife.AxesWanted, VillageLife.ToolsWanted(ToolKind.Axe, ResourceBook.Meadows));
			Assert.Equal(0, VillageLife.ToolsWanted(ToolKind.Pickaxe, ResourceBook.Meadows));
			Assert.Equal(VillageLife.PickaxesWanted, VillageLife.ToolsWanted(ToolKind.Pickaxe, ResourceBook.BlackForest));
			Assert.Equal(VillageLife.PickaxesWanted, VillageLife.ToolsWanted(ToolKind.Pickaxe, ResourceBook.AshLands));
			Assert.Equal(0, VillageLife.ToolsWanted(ToolKind.None, ResourceBook.Plains));
		}

		/// <summary>
		/// Checks how much more of something its vikings don't use a village keeps - up to its two stacks less what it
		/// holds, and none once it holds that many, as with the deer hides hunts brought home only to have them dropped
		/// </summary>
		[Theory]
		[InlineData(0, 2, 50, 100)]
		[InlineData(60, 2, 50, 40)]
		[InlineData(100, 2, 50, 0)]
		[InlineData(150, 2, 50, 0)]
		[InlineData(0, 2, 1, 2)]
		public void KeepRoomIsWhatTheStacksHaveLeft(int held, int stacksKept, int maxStack, int expected)
		{
			Assert.Equal(expected, VillageLife.KeepRoomFor(held, stacksKept, maxStack));
		}

		/// <summary>
		/// Checks that the village keeps one stack of each trophy, as many axes and pickaxes as it wants, and two stacks
		/// of anything else its vikings don't use
		/// </summary>
		[Fact]
		public void KeepsOneStackOfATrophy()
		{
			Assert.Equal(1, VillageLife.StacksKeptFor(true, false, false));
			Assert.Equal(VillageLife.PickaxesWanted, VillageLife.StacksKeptFor(false, true, false));
			Assert.Equal(VillageLife.AxesWanted, VillageLife.StacksKeptFor(false, false, true));
			Assert.Equal(2, VillageLife.StacksKeptFor(false, false, false));
		}

		/// <summary>
		/// Checks that a viking jumps fences and low walls, but not roofs or beams, which it would end up stuck on top of
		/// </summary>
		[Theory]
		[InlineData("wood_fence", true)]
		[InlineData("stone_wall_1x1", true)]
		[InlineData("wood_roof_45", false)]
		[InlineData("wood_roof_top_45", false)]
		[InlineData("wood_wall_roof_45", false)]
		[InlineData("wood_beam_45", false)]
		public void JumpsFencesButNotRoofs(string name, bool expected)
		{
			Assert.Equal(expected, VillageLife.HoppableName(name));
		}

		/// <summary>
		/// Checks that a viking counts as stuck on top of something only well above the next point of its path and close
		/// to it, as on the roof of the house at Black Forest's workbench, 4m above its path's next point, and not when
		/// it's level with it, or the point is far off
		/// </summary>
		[Theory]
		[InlineData(1481.6f, 39.6f, 52.3f, 1483.1f, 35.5f, 52.8f, true)]
		[InlineData(1447.1f, 40.0f, 71.0f, 1445.3f, 37.5f, 71.2f, true)]
		[InlineData(1430.7f, 36.0f, 81.8f, 1430.7f, 36.4f, 81.3f, false)]
		[InlineData(1447.1f, 40.0f, 71.0f, 1441.0f, 37.5f, 71.0f, false)]
		[InlineData(0f, 1f, 0f, 0f, 0f, 0f, false)]
		public void StrandedOnlyWellAboveAndNearItsPath(float atX, float atY, float atZ, float nextX, float nextY, float nextZ, bool expected)
		{
			Assert.Equal(expected, VillageLife.Stranded(new Vector3(atX, atY, atZ), new Vector3(nextX, nextY, nextZ)));
		}

		/// <summary>
		/// Checks that a viking on a roof climbs straight down when the ground found for it is right beside it seen from
		/// above, as on the low edge of the Swamp farmhouse's roof, or well below it close by, as on its ridge over the
		/// floor inside, and walks when the ground is further off
		/// </summary>
		[Theory]
		[InlineData(5279.5f, 31.9f, 1642.0f, 5279.5f, 31.1f, 1641.2f, true)]
		[InlineData(5277.5f, 35.0f, 1646.5f, 5276.6f, 31.3f, 1645.6f, true)]
		[InlineData(5280.0f, 34.7f, 1644.7f, 5279.5f, 31.3f, 1646.4f, true)]
		[InlineData(5277.5f, 35.0f, 1646.5f, 5273.0f, 31.1f, 1646.0f, false)]
		[InlineData(5284.0f, 32.5f, 1648.0f, 5284.0f, 31.1f, 1650.5f, false)]
		public void ClimbsOffRoofOnlyRightOverOrWellAboveTheGround(float atX, float atY, float atZ, float exitX, float exitY, float exitZ, bool expected)
		{
			Assert.Equal(expected, VillageLife.ClimbsOffRoof(new Vector3(atX, atY, atZ), new Vector3(exitX, exitY, exitZ)));
		}

		/// <summary>
		/// Checks that the Hungry line names the chests with food a viking leaves alone, by why, with the nearest - and
		/// says nothing more when there are none
		/// </summary>
		/// <param name="avoidedChests">How many it couldn't get to lately</param>
		/// <param name="heldChests">How many a player holds</param>
		/// <param name="openChests">How many are open for another viking</param>
		/// <param name="expected">The description</param>
		[Theory]
		[InlineData(0, 0, 0, "")]
		[InlineData(1, 0, 0, " - 1 more does, which it leaves alone (1 it couldn't get to lately), at (5284, 31, 1648)")]
		[InlineData(2, 0, 1, " - 3 more do, which it leaves alone (2 it couldn't get to lately, 1 open for another viking), the nearest at (5284, 31, 1648)")]
		[InlineData(0, 1, 1, " - 2 more do, which it leaves alone (1 held by a player, 1 open for another viking), the nearest at (5284, 31, 1648)")]
		public void DescribesFoodLeftAloneByWhy(int avoidedChests, int heldChests, int openChests, string expected)
		{
			Assert.Equal(expected, VillageLife.DescribeLeftFood(avoidedChests, heldChests, openChests, "(5284, 31, 1648)"));
		}

		/// <summary>
		/// Checks that stacks of the same item are counted together in the order each kind first comes
		/// </summary>
		[Fact]
		public void DescribesStacksByKind()
		{
			List<KeyValuePair<string, int>> stacks = new List<KeyValuePair<string, int>>
			{
				new KeyValuePair<string, int>("Wolf Meat", 3),
				new KeyValuePair<string, int>("Leather Scraps", 2),
				new KeyValuePair<string, int>("Wolf Meat", 1)
			};
			Assert.Equal("Wolf Meat x4, Leather Scraps x2", VillageLife.DescribeStacks(stacks));
		}

		/// <summary>
		/// Checks that no stacks describe as an empty string
		/// </summary>
		[Fact]
		public void DescribesNoStacksAsEmpty()
		{
			Assert.Equal("", VillageLife.DescribeStacks(new List<KeyValuePair<string, int>>()));
		}

		/// <summary>
		/// Checks what's counted as keeping a viking from a spot - off its navmesh before anything, no whole path to a
		/// spot in its own room before having no way out to the open ground, which a shut door causes, and the rest in
		/// their order
		/// </summary>
		/// <param name="offMesh">Whether the viking stands off its navmesh</param>
		/// <param name="toOpen">Whether it has a whole path to the open ground</param>
		/// <param name="outsideHouse">Whether the spot lies outside the thing's house</param>
		/// <param name="toSpot">Whether it has a whole path to the spot</param>
		/// <param name="oneRoom">Whether it and the spot share a room</param>
		/// <param name="expected">What's counted</param>
		[Theory]
		[InlineData(true, false, true, false, true, "the viking off its navmesh")]
		[InlineData(false, false, false, false, true, "no whole path to the spot")]
		[InlineData(false, false, false, false, false, "the viking with no way to the open ground")]
		[InlineData(false, false, false, true, true, "the viking with no way to the open ground")]
		[InlineData(false, true, true, false, false, "the spot outside the house")]
		[InlineData(false, true, false, false, false, "no whole path to the spot")]
		[InlineData(false, true, false, true, true, "a whole path to the spot")]
		public void RanksWhatKeptAVikingFromASpot(bool offMesh, bool toOpen, bool outsideHouse, bool toSpot, bool oneRoom, string expected)
		{
			Assert.Equal(expected, VillageLife.MissTrouble(offMesh, toOpen, outsideHouse, toSpot, oneRoom));
		}
	}
}
