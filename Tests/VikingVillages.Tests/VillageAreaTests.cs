using System.Collections.Generic;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests a village's ring - how many marks it's drawn with, and how a village notes the players who have it shown
	/// </summary>
	public class VillageAreaTests
	{
		/// <summary>
		/// Checks that a ring has enough marks to be no farther apart than the spacing, rounding up, and never fewer
		/// than 8
		/// </summary>
		/// <param name="radius">The ring's radius, in meters</param>
		/// <param name="expected">How many marks</param>
		[Theory]
		// 2 pi r / 1.5 - 41.89 at 10 meters, 8.38 at 2 and 7.96 at 1.9
		[InlineData(10f, 42)]
		[InlineData(2f, 9)]
		[InlineData(1.9f, 8)]
		[InlineData(1f, 8)]
		[InlineData(0f, 8)]
		public void SegmentsAreNoFartherApartThanTheSpacing(float radius, int expected)
		{
			Assert.Equal(expected, VillageArea.Segments(radius, 1.5f));
		}

		/// <summary>
		/// Checks that a ring's marks are spaced its circumference over how many there are
		/// </summary>
		[Fact]
		public void MarkSpacingIsTheCircumferenceShared()
		{
			// 2 pi 5 / 20 is pi / 2
			Assert.Equal(1.5708f, VillageArea.MarkSpacing(5f, 20), 4);
		}

		/// <summary>
		/// Checks that a ring with no size or no marks gives the fallback spacing
		/// </summary>
		/// <param name="radius">The ring's radius, in meters</param>
		/// <param name="segments">How many marks it has</param>
		[Theory]
		[InlineData(0f, 20)]
		[InlineData(-1f, 20)]
		[InlineData(5f, 0)]
		[InlineData(5f, -3)]
		public void MarkSpacingFallsBack(float radius, int segments)
		{
			Assert.Equal(1.5f, VillageArea.MarkSpacing(radius, segments));
		}

		/// <summary>
		/// Checks that a player showing the ring again moves to the end, as the latest to show it
		/// </summary>
		[Fact]
		public void ShowingAgainMovesToTheEnd()
		{
			Assert.Equal(new List<long> { 2L, 3L, 1L }, VillageArea.UpdateShown(new List<long> { 1L, 2L, 3L }, 1L, true, 64));
		}

		/// <summary>
		/// Checks that a player hiding the ring is taken out, and one who never showed it changes nothing
		/// </summary>
		[Fact]
		public void HidingTakesThePlayerOut()
		{
			Assert.Equal(new List<long> { 1L, 3L }, VillageArea.UpdateShown(new List<long> { 1L, 2L, 3L }, 2L, false, 64));
			Assert.Equal(new List<long> { 1L, 2L, 3L }, VillageArea.UpdateShown(new List<long> { 1L, 2L, 3L }, 9L, false, 64));
		}

		/// <summary>
		/// Checks that a player showing the ring past the most kept drops the earliest to show it
		/// </summary>
		[Fact]
		public void OverTheMostDropsTheEarliest()
		{
			Assert.Equal(new List<long> { 2L, 3L, 4L }, VillageArea.UpdateShown(new List<long> { 1L, 2L, 3L }, 4L, true, 3));
			Assert.Equal(new List<long> { 3L, 5L, 6L }, VillageArea.UpdateShown(new List<long> { 1L, 2L, 3L, 4L, 5L, 6L }, 4L, false, 3));
		}

		/// <summary>
		/// Checks that the players read back as they were written, in order
		/// </summary>
		[Fact]
		public void ShownRoundTrips()
		{
			List<long> players = new List<long> { -5L, 0L, 123456789012345L };
			Assert.Equal(players, VillageArea.DecodeShown(VillageArea.EncodeShown(players)));
			Assert.Empty(VillageArea.DecodeShown(VillageArea.EncodeShown(new List<long>())));
		}

		/// <summary>
		/// Checks that no bytes, bytes too short to hold a count, and bytes cut off partway through a player all read as
		/// no players rather than failing
		/// </summary>
		[Fact]
		public void CorruptShownReadsAsNone()
		{
			Assert.Empty(VillageArea.DecodeShown(null));
			Assert.Empty(VillageArea.DecodeShown(new byte[] { 1 }));
			byte[] bytes = VillageArea.EncodeShown(new List<long> { 1L, 2L });
			Assert.Empty(VillageArea.DecodeShown(bytes[..(bytes.Length - 3)]));
		}
	}
}
