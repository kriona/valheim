using System.Collections.Generic;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a farm is sized at random as it's generated - its fence, fence sections and how many more rooms it gets
	/// </summary>
	public class VillageFarmhousesTests
	{
		/// <summary>
		/// The range the game's own fence stands from a farm's middle in, and its own number of fence sections, in the
		/// tests
		/// </summary>
		private const float OwnRadiusMin = 18f;
		private const float OwnRadiusMax = 32f;
		private const int OwnSections = 10;

		/// <summary>
		/// How much a distance can be off from float rounding, in meters
		/// </summary>
		private const float Tolerance = 0.0001f;

		/// <summary>
		/// Checks that a farm's seed always gives it the same size
		/// </summary>
		/// <param name="seed">The farm's seed</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1234)]
		[InlineData(-98765)]
		public void SameSeedGivesTheSameSize(int seed)
		{
			VillageFarmhouses.FarmSize first = VillageFarmhouses.Size(seed, false, OwnRadiusMin, OwnRadiusMax, OwnSections);
			VillageFarmhouses.FarmSize second = VillageFarmhouses.Size(seed, false, OwnRadiusMin, OwnRadiusMax, OwnSections);
			Assert.Equal(first.RadiusMin, second.RadiusMin);
			Assert.Equal(first.RadiusMax, second.RadiusMax);
			Assert.Equal(first.Sections, second.Sections);
			Assert.Equal(first.RoomScale, second.RoomScale);
		}

		/// <summary>
		/// Checks that farms with different seeds come in different sizes
		/// </summary>
		[Fact]
		public void DifferentSeedsGiveDifferentSizes()
		{
			HashSet<float> sizes = new HashSet<float>();
			for (int seed = 0; seed < 20; seed++)
			{
				sizes.Add(VillageFarmhouses.Size(seed, false, OwnRadiusMin, OwnRadiusMax, OwnSections).RadiusMin);
			}
			Assert.True(sizes.Count > 10);
		}

		/// <summary>
		/// Checks that a farm's fence always stands between 24 and 44 meters out, its generator free to put it within 2
		/// meters either side of the size picked
		/// </summary>
		[Fact]
		public void FenceStaysInRange()
		{
			for (int seed = 0; seed < 500; seed++)
			{
				VillageFarmhouses.FarmSize size = VillageFarmhouses.Size(seed, false, OwnRadiusMin, OwnRadiusMax, OwnSections);
				Assert.True(size.RadiusMin >= VillageFarmhouses.FenceRadiusMin);
				Assert.True(size.RadiusMax <= VillageFarmhouses.FenceRadiusMax);
				Assert.InRange(size.RadiusMax - size.RadiusMin, 2f - Tolerance, 4f + Tolerance);
			}
		}

		/// <summary>
		/// Checks that a farm by the Black Forest is one of the larger ones, picked from 36 meters up, and that a farm
		/// elsewhere can be smaller
		/// </summary>
		[Fact]
		public void ForestFarmIsLarger()
		{
			bool smaller = false;
			for (int seed = 0; seed < 200; seed++)
			{
				VillageFarmhouses.FarmSize forest = VillageFarmhouses.Size(seed, true, OwnRadiusMin, OwnRadiusMax, OwnSections);
				Assert.True(forest.RadiusMin >= 34f - Tolerance);
				Assert.True(forest.RadiusMax >= 38f - Tolerance);
				Assert.True(forest.RadiusMax <= VillageFarmhouses.FenceRadiusMax);
				if (VillageFarmhouses.Size(seed, false, OwnRadiusMin, OwnRadiusMax, OwnSections).RadiusMin < 34f)
				{
					smaller = true;
				}
			}
			Assert.True(smaller);
		}

		/// <summary>
		/// Checks that a farm gets more rooms for the more ground it takes in than the game's own, but never fewer than
		/// the game's own or more than three times as many
		/// </summary>
		[Fact]
		public void RoomScaleIsClamped()
		{
			for (int seed = 0; seed < 50; seed++)
			{
				// Far larger than the game's own, so its rooms are capped at three times as many
				Assert.Equal(3f, VillageFarmhouses.Size(seed, false, 10f, 10f, OwnSections).RoomScale);
				// Far smaller, so it keeps the game's own count
				Assert.Equal(1f, VillageFarmhouses.Size(seed, false, 100f, 100f, OwnSections).RoomScale);
				// Up to (44 + 42) / (18 + 32) longer, so up to 2.96 times the ground
				Assert.InRange(VillageFarmhouses.Size(seed, false, OwnRadiusMin, OwnRadiusMax, OwnSections).RoomScale, 1f, 2.96f);
			}
		}

		/// <summary>
		/// Checks that a longer fence gets as many more sections as it's longer, rounded up, so even a fence far shorter
		/// than the game's keeps a section
		/// </summary>
		[Fact]
		public void SectionsRoundUp()
		{
			for (int seed = 0; seed < 50; seed++)
			{
				Assert.Equal(1, VillageFarmhouses.Size(seed, false, 500f, 500f, 1).Sections);
				Assert.Equal(0, VillageFarmhouses.Size(seed, false, OwnRadiusMin, OwnRadiusMax, 0).Sections);
				// Between 50 / 50 and 86 / 50 times as long
				Assert.InRange(VillageFarmhouses.Size(seed, false, OwnRadiusMin, OwnRadiusMax, OwnSections).Sections, 10, 18);
			}
		}
	}
}
