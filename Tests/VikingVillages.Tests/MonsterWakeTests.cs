using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests when a sleeping monster wakes for a viking near it - by its wake-up range, or by the viking's noise
	/// </summary>
	public class MonsterWakeTests
	{
		[Fact]
		public void AnAbominationWakesForAVikingWithinItsWakeUpRange()
		{
			Assert.True(MonsterWake.WakesFor(15f, 20f, false, 0f, 50f));
			Assert.False(MonsterWake.WakesFor(25f, 20f, false, 30f, 50f));
		}

		[Fact]
		public void AStoneGolemWakesForAVikingWhoseNoiseReachesIt()
		{
			Assert.True(MonsterWake.WakesFor(1f, 2f, true, 0f, 50f));
			Assert.True(MonsterWake.WakesFor(12f, 2f, true, 15f, 50f));
			Assert.True(MonsterWake.WakesFor(25f, 2f, true, 30f, 50f));
			Assert.False(MonsterWake.WakesFor(20f, 2f, true, 15f, 50f));
			Assert.False(MonsterWake.WakesFor(12f, 2f, true, 0f, 50f));
		}

		[Fact]
		public void NoiseCarriesNoFartherThanTheMonsterHears()
		{
			Assert.False(MonsterWake.WakesFor(40f, 2f, true, 60f, 30f));
			Assert.True(MonsterWake.WakesFor(25f, 2f, true, 60f, 30f));
		}
	}
}
