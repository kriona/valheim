using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests what players are told as a village is started or disbanded, and which vikings are sent away as it's
	/// disbanded
	/// </summary>
	public class VillageFoundingTests
	{
		/// <summary>
		/// Where the village's anchor stands in the tests
		/// </summary>
		private static readonly Vector3 Farm = new Vector3(200f, 30f, -400f);

		/// <summary>
		/// Checks that a village started with no vikings coming back says how to bring some, and one with vikings coming
		/// back says where they go, for one and for several
		/// </summary>
		/// <param name="returning">How many vikings are coming back</param>
		/// <param name="expected">The message</param>
		[Theory]
		[InlineData(0, "Build unclaimed beds near the fire, under a roof, for Vikings to move in")]
		[InlineData(1, "The Viking who left comes back to the first unclaimed bed near the fire")]
		[InlineData(4, "The 4 Vikings who left come back to the unclaimed beds near the fire, one to each")]
		public void TellsWhoComesBack(int returning, string expected)
		{
			Assert.Equal(expected, VillageFounding.ReturningMessage(returning));
		}

		/// <summary>
		/// Checks that the popup before a village is disbanded says what becomes of its vikings, for none, one and
		/// several
		/// </summary>
		/// <param name="count">How many vikings the village has</param>
		/// <param name="expected">The text</param>
		[Theory]
		[InlineData(0, "It has no Vikings yet. The bonfire stays.")]
		[InlineData(1, "Its Viking says goodbye and leaves with everything they have. Start a village at this bonfire again, and they come back to its beds. The bonfire stays.")]
		[InlineData(7, "Its 7 Vikings say goodbye and leave with everything they have. Start a village at this bonfire again, and they come back to its beds. The bonfire stays.")]
		public void AsksBeforeDisbanding(int count, string expected)
		{
			Assert.Equal(expected, VillageFounding.DisbandText(count));
		}

		/// <summary>
		/// Checks that the message once a village is disbanded says how many of its vikings are leaving, for none, one and
		/// several
		/// </summary>
		/// <param name="left">How many are leaving</param>
		/// <param name="expected">The message</param>
		[Theory]
		[InlineData(0, "The village is disbanded")]
		[InlineData(1, "The village is disbanded, and its Viking is leaving")]
		[InlineData(2, "The village is disbanded, and its 2 Vikings are leaving")]
		public void TellsWhoIsLeaving(int left, string expected)
		{
			Assert.Equal(expected, VillageFounding.DisbandedMessage(left));
		}

		/// <summary>
		/// Checks that a living viking noting a village up to 16 meters from the village's anchor across the ground is
		/// sent away, however high it was noted, and one noting a village farther off isn't
		/// </summary>
		/// <param name="east">How far east of the anchor its noted village is, in meters</param>
		/// <param name="up">How far above it, in meters</param>
		/// <param name="expected">Whether it's sent away</param>
		[Theory]
		[InlineData(0f, 0f, true)]
		[InlineData(16f, 0f, true)]
		[InlineData(16.1f, 0f, false)]
		[InlineData(-16f, 0f, true)]
		[InlineData(0f, 500f, true)]
		[InlineData(30f, 0f, false)]
		public void SendsAwayTheVillagesVikings(float east, float up, bool expected)
		{
			Vector3 noted = Farm + new Vector3(east, up, 0f);
			Assert.Equal(expected, VillageFounding.BelongsTo(VikingCore.LivingPrefabHash, noted, Farm, false));
		}

		/// <summary>
		/// Checks that a viking already leaving, one noting no village, and anything but a living viking aren't sent away
		/// </summary>
		[Fact]
		public void LeavesOutTheRest()
		{
			Assert.False(VillageFounding.BelongsTo(VikingCore.LivingPrefabHash, Farm, Farm, true));
			Assert.False(VillageFounding.BelongsTo(VikingCore.LivingPrefabHash, Vector3.zero, Farm, false));
			Assert.False(VillageFounding.BelongsTo(VikingCore.ZombiePrefabHash, Farm, Farm, false));
			Assert.False(VillageFounding.BelongsTo("Boar".GetStableHashCode(), Farm, Farm, false));
		}

		/// <summary>
		/// Checks that a village with its anchor at zero has no vikings sent away, since a viking noting zero notes no
		/// village
		/// </summary>
		[Fact]
		public void LeavesOutAVillageAtZero()
		{
			Assert.False(VillageFounding.BelongsTo(VikingCore.LivingPrefabHash, Vector3.zero, Vector3.zero, false));
			Assert.True(VillageFounding.BelongsTo(VikingCore.LivingPrefabHash, new Vector3(1f, 0f, 0f), Vector3.zero, false));
		}
	}
}
