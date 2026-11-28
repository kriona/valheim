using System;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests which creatures a valkyrie can grab, how hard a drop hurts, and how long a dropped creature is let be
	/// </summary>
	public class ValkyrieGrabTests
	{
		[Fact]
		public void SheGrabsCreaturesUpToAFenringsHeight()
		{
			Assert.True(ValkyrieGrab.Grabbable("Greydwarf", 1.8f, 0.4f, false, false));
			Assert.True(ValkyrieGrab.Grabbable("Greydwarf_Elite", 2.62f, 0.64f, false, false));
			Assert.True(ValkyrieGrab.Grabbable("Deer", 1.78f, 0.8f, false, false));
			Assert.True(ValkyrieGrab.Grabbable("Fenring", 3f, 0.5f, false, false));
			Assert.False(ValkyrieGrab.Grabbable("ElakingMole", 3.03f, 0.4f, false, false));
			Assert.False(ValkyrieGrab.Grabbable("Troll", 6.61f, 1f, false, false));
			Assert.False(ValkyrieGrab.Grabbable("Lox_Calf", 1f, 1f, false, false));
		}

		[Fact]
		public void SheNeverGrabsRootsOrTendrils()
		{
			Assert.False(ValkyrieGrab.Grabbable("TentaRoot", 3f, 0.4f, false, false));
			Assert.False(ValkyrieGrab.Grabbable("TentaRoot_wild", 3f, 0.4f, false, false));
			Assert.False(ValkyrieGrab.Grabbable("Aspect_TentaRoot", 3f, 0.4f, false, false));
			Assert.False(ValkyrieGrab.Grabbable("Tendril", 3f, 0.4f, false, false));
			Assert.False(ValkyrieGrab.Grabbable("Tendril_back", 3f, 0.4f, false, false));
		}

		[Fact]
		public void SheNeverGrabsFlyersBossesOozesSerpentsOrTheDummy()
		{
			Assert.False(ValkyrieGrab.Grabbable("Deathsquito", 1f, 0.5f, true, false));
			Assert.False(ValkyrieGrab.Grabbable("Eikthyr", 2f, 0.5f, false, true));
			Assert.False(ValkyrieGrab.Grabbable("Blob", 1.5f, 0.78f, false, false));
			Assert.False(ValkyrieGrab.Grabbable("BlobElite", 1f, 0.8f, false, false));
			Assert.False(ValkyrieGrab.Grabbable("Serpent", 1f, 0.8f, false, false));
			Assert.False(ValkyrieGrab.Grabbable("TrainingDummy", 1.85f, 0.4f, false, false));
		}

		[Fact]
		public void SheNeverGrabsGhostsOrTheseFoes()
		{
			Assert.False(ValkyrieGrab.Grabbable("Ghost", 1.85f, 0.35f, false, false));
			Assert.False(ValkyrieGrab.Grabbable("Ghost_Void", 1.85f, 0.35f, false, false));
			Assert.False(ValkyrieGrab.Grabbable("Ghost_sleeping", 1.85f, 0.35f, false, false));
			Assert.False(ValkyrieGrab.Grabbable("ShadowPerson", 1.85f, 0.4f, false, false));
			Assert.False(ValkyrieGrab.Grabbable("Fenring_Cultist_Hildir", 2.5f, 0.5f, false, false));
			Assert.False(ValkyrieGrab.Grabbable("Fenring_Cultist_Hildir_nochest", 2.5f, 0.5f, false, false));
			Assert.True(ValkyrieGrab.Grabbable("Fenring_Cultist", 2.5f, 0.5f, false, false));
			Assert.False(ValkyrieGrab.Grabbable("Goblin_Gem", 1.45f, 0.4f, false, false));
			Assert.False(ValkyrieGrab.Grabbable("GoblinShaman_Hildir", 1.45f, 0.4f, false, false));
			Assert.False(ValkyrieGrab.Grabbable("GoblinShaman_Hildir_nochest", 1.45f, 0.4f, false, false));
			Assert.False(ValkyrieGrab.Grabbable("Skeleton_Hildir", 2.2f, 0.4f, false, false));
			Assert.False(ValkyrieGrab.Grabbable("Charred_Melee_Dyrnwyn", 2.54f, 0.4f, false, false));
			// Other fulings, skeletons and charred are still grabbed
			Assert.True(ValkyrieGrab.Grabbable("Goblin", 1.45f, 0.4f, false, false));
			Assert.True(ValkyrieGrab.Grabbable("GoblinShaman", 1.45f, 0.4f, false, false));
			Assert.True(ValkyrieGrab.Grabbable("Skeleton", 1.85f, 0.4f, false, false));
			Assert.True(ValkyrieGrab.Grabbable("Charred_Melee", 2.54f, 0.4f, false, false));
		}

		[Fact]
		public void AFallHurtsTheWayAPlayersDoes()
		{
			Assert.Equal(0f, ValkyrieGrab.FallDamage(3f));
			Assert.Equal(0f, ValkyrieGrab.FallDamage(4f));
			Assert.Equal(50f, ValkyrieGrab.FallDamage(12f), 4);
			Assert.Equal(100f, ValkyrieGrab.FallDamage(20f), 4);
			Assert.Equal(100f, ValkyrieGrab.FallDamage(40f), 4);
		}

		[Fact]
		public void ADroppedCreatureIsLetBeForAMinute()
		{
			long now = TimeSpan.FromHours(10).Ticks;
			Assert.False(ValkyrieGrab.DroppedLately(now, 0L));
			Assert.True(ValkyrieGrab.DroppedLately(now, now - TimeSpan.FromSeconds(30).Ticks));
			Assert.False(ValkyrieGrab.DroppedLately(now, now - TimeSpan.FromSeconds(61).Ticks));
		}

		[Fact]
		public void SheWatchesWhatSheDroppedFallUntilItLands()
		{
			Assert.True(ValkyrieGrab.StillFalling(1f, false, false));
			Assert.False(ValkyrieGrab.StillFalling(1f, false, true));
			Assert.True(ValkyrieGrab.StillFalling(0.1f, false, true));
		}

		[Fact]
		public void SheStopsWatchingOnceItsDeadOrTooLong()
		{
			Assert.False(ValkyrieGrab.StillFalling(1f, true, false));
			Assert.False(ValkyrieGrab.StillFalling(ValkyrieGrab.WatchTime, false, false));
		}
	}
}
