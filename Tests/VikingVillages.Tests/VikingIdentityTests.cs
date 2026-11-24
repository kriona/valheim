using System.Collections.Generic;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests what a viking remembers of the players it knows - when it met them, how many times they've talked to it,
	/// and when that makes them old friends
	/// </summary>
	public class VikingIdentityTests
	{
		/// <summary>
		/// The least time between counted talks the tests use, in ticks
		/// </summary>
		private const long Gap = 60L;

		/// <summary>
		/// Checks that a viking keeps the first time it met a player, however often it meets them again
		/// </summary>
		[Fact]
		public void MeetingIsKeptFromTheFirstTime()
		{
			List<VikingIdentity.Acquaintance> known = new List<VikingIdentity.Acquaintance>();
			VikingIdentity.NoteMet(known, 1L, 100L);
			VikingIdentity.NoteMet(known, 1L, 500L);
			Assert.Single(known);
			Assert.Equal(100L, VikingIdentity.Find(known, 1L).Met);
			Assert.Equal(0, VikingIdentity.Find(known, 1L).Talks);
		}

		/// <summary>
		/// Checks that talks count once each, but talks closer together than the gap count only once
		/// </summary>
		[Fact]
		public void TalksCountOncePerGap()
		{
			List<VikingIdentity.Acquaintance> known = new List<VikingIdentity.Acquaintance>();
			VikingIdentity.NoteMet(known, 1L, 100L);
			VikingIdentity.NoteTalk(known, 1L, 100L, 100L, Gap);
			VikingIdentity.NoteTalk(known, 1L, 120L, 100L, Gap);
			VikingIdentity.NoteTalk(known, 1L, 159L, 120L, Gap);
			Assert.Equal(1, VikingIdentity.Find(known, 1L).Talks);
			VikingIdentity.NoteTalk(known, 1L, 160L, 159L, Gap);
			Assert.Equal(2, VikingIdentity.Find(known, 1L).Talks);
			Assert.Equal(160L, VikingIdentity.Find(known, 1L).Counted);
			Assert.Equal(100L, VikingIdentity.Find(known, 1L).Met);
		}

		/// <summary>
		/// Checks that a player a viking knew before it noted meetings is taken to have met it when they last talked, or
		/// at the talk itself when they never had
		/// </summary>
		[Fact]
		public void UnnotedMeetingFallsBackToLastTalk()
		{
			List<VikingIdentity.Acquaintance> known = new List<VikingIdentity.Acquaintance>();
			VikingIdentity.NoteTalk(known, 1L, 900L, 300L, Gap);
			VikingIdentity.NoteTalk(known, 2L, 900L, 0L, Gap);
			Assert.Equal(300L, VikingIdentity.Find(known, 1L).Met);
			Assert.Equal(900L, VikingIdentity.Find(known, 2L).Met);
			Assert.Equal(1, VikingIdentity.Find(known, 1L).Talks);
		}

		/// <summary>
		/// Checks that a viking finds nothing of a player it doesn't know
		/// </summary>
		[Fact]
		public void UnknownPlayerHasNoMeeting()
		{
			VikingIdentity.Acquaintance unknown = VikingIdentity.Find(new List<VikingIdentity.Acquaintance>(), 7L);
			Assert.Equal(7L, unknown.Player);
			Assert.Equal(0L, unknown.Met);
			Assert.Equal(0, unknown.Talks);
			Assert.False(VikingIdentity.OldFriends(unknown, 1000000L, 10L));
		}

		/// <summary>
		/// Checks that a viking and a player are old friends once it has known them long enough, or once they've talked
		/// to it more than FriendTalks times
		/// </summary>
		[Fact]
		public void OldFriendsByTimeOrTalks()
		{
			VikingIdentity.Acquaintance acquaintance = new VikingIdentity.Acquaintance();
			acquaintance.Player = 1L;
			acquaintance.Met = 100L;
			acquaintance.Talks = VikingIdentity.FriendTalks;
			Assert.False(VikingIdentity.OldFriends(acquaintance, 199L, 100L));
			Assert.True(VikingIdentity.OldFriends(acquaintance, 200L, 100L));
			acquaintance.Talks++;
			Assert.True(VikingIdentity.OldFriends(acquaintance, 101L, 100L));
		}

		/// <summary>
		/// Checks that what a viking remembers of the players it knows reads back as it was written, the one it heard from
		/// last at the end, and that it forgets the one it heard from longest ago past 64
		/// </summary>
		[Fact]
		public void AcquaintancesAreRemembered()
		{
			List<VikingIdentity.Acquaintance> known = new List<VikingIdentity.Acquaintance>();
			VikingIdentity.NoteMet(known, 1L, 100L);
			VikingIdentity.NoteMet(known, 2L, 200L);
			VikingIdentity.NoteTalk(known, 1L, 300L, 100L, Gap);
			List<VikingIdentity.Acquaintance> read = VikingIdentity.DecodeMet(VikingIdentity.EncodeMet(known));
			Assert.Equal(2, read.Count);
			Assert.Equal(2L, read[0].Player);
			Assert.Equal(1L, read[1].Player);
			Assert.Equal(100L, read[1].Met);
			Assert.Equal(1, read[1].Talks);
			Assert.Equal(300L, read[1].Counted);
			Assert.Empty(VikingIdentity.DecodeMet(null));
			for (long i = 10L; i < 100L; i++)
			{
				VikingIdentity.NoteMet(known, i, i);
			}
			Assert.Equal(64, known.Count);
			Assert.Equal(99L, known[known.Count - 1].Player);
		}
	}
}
