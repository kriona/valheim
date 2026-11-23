using System.Collections.Generic;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the news vikings tell - how events are added, merged and replaced, which are firsts, which a viking tells a
	/// player, how telling one uses it up, how old news goes, and how the news reads back
	/// </summary>
	public class VikingNewsTests
	{
		/// <summary>
		/// Makes a list of players
		/// </summary>
		/// <param name="players">Their IDs</param>
		/// <returns>The list</returns>
		private static List<long> Players(params long[] players)
		{
			return(new List<long>(players));
		}

		/// <summary>
		/// Checks that a first is only news to the players who haven't had it, and is noted for them
		/// </summary>
		[Fact]
		public void FirstsOnlyGoToNewPlayers()
		{
			VikingNews news = new VikingNews();
			Assert.NotNull(news.Add(VikingNews.Biome, "Swamp", "", Players(1L), true, 100L, 3));
			VikingNews.Event second = news.Add(VikingNews.Biome, "Swamp", "", Players(1L, 2L), true, 200L, 3);
			Assert.NotNull(second);
			Assert.Equal(Players(2L), second.Players);
			Assert.Null(news.Add(VikingNews.Biome, "Swamp", "", Players(1L, 2L), true, 300L, 3));
			Assert.Contains(VikingNews.FirstKey(1L, VikingNews.Biome, "Swamp"), news.Firsts);
			Assert.Contains(VikingNews.FirstKey(2L, VikingNews.Biome, "Swamp"), news.Firsts);
		}

		/// <summary>
		/// Checks that a boss killed again takes the place of its last news, keeping the players of both
		/// </summary>
		[Fact]
		public void BossKillsMergePlayers()
		{
			VikingNews news = new VikingNews();
			VikingNews.Event first = news.Add(VikingNews.Boss, "Eikthyr", "", Players(1L), false, 100L, 3);
			news.Mention(first.Id, "viking-a", 7);
			news.Add(VikingNews.Boss, "gd_king", "", Players(1L), false, 150L, 3);
			VikingNews.Event again = news.Add(VikingNews.Boss, "Eikthyr", "", Players(2L), false, 200L, 4);
			Assert.Equal(2, news.Events.Count);
			Assert.Null(news.Find(first.Id));
			Assert.Equal(200L, again.Time);
			Assert.Equal(4, again.Remaining);
			Assert.Empty(again.Mentioned);
			Assert.Contains(1L, again.Players);
			Assert.Contains(2L, again.Players);
		}

		/// <summary>
		/// Checks that an event that happens again and again replaces the player's last one of its kind, whatever it was
		/// about, and leaves other players' alone
		/// </summary>
		[Fact]
		public void RepeatsReplaceThePlayersLast()
		{
			VikingNews news = new VikingNews();
			news.Add(VikingNews.Death, "troll", "", Players(1L), false, 100L, 3);
			news.Add(VikingNews.Death, "hit/Fall", "", Players(2L), false, 150L, 3);
			news.Add(VikingNews.Death, "hit/Drowning", "", Players(1L), false, 200L, 3);
			Assert.Equal(2, news.Events.Count);
			Assert.Equal("hit/Fall", news.Events[0].Subject);
			Assert.Equal("hit/Drowning", news.Events[1].Subject);
		}

		/// <summary>
		/// Checks that a player is taken out of a shared event of the same kind they're in again, which stays for the rest
		/// </summary>
		[Fact]
		public void RepeatsTakeThePlayerOutOfSharedNews()
		{
			VikingNews news = new VikingNews();
			VikingNews.Event shared = news.Add(VikingNews.Serpent, "Serpent", "", Players(1L, 2L), false, 100L, 3);
			news.Add(VikingNews.Serpent, "Serpent", "", Players(1L), false, 200L, 3);
			Assert.Equal(Players(2L), shared.Players);
			Assert.Equal(2, news.Events.Count);
		}

		/// <summary>
		/// Checks that events of the same kind about different things stand side by side
		/// </summary>
		[Fact]
		public void DifferentSubjectsStay()
		{
			VikingNews news = new VikingNews();
			news.Add(VikingNews.Skill, "Swords", "", Players(1L), true, 100L, 3);
			news.Add(VikingNews.Skill, "Bows", "", Players(1L), true, 200L, 3);
			Assert.Equal(2, news.Events.Count);
		}

		/// <summary>
		/// Checks that telling news uses up a viking, notes the line, drops the event once no vikings are left, and that a
		/// viking can't tell it twice
		/// </summary>
		[Fact]
		public void MentionsUseUpTheNews()
		{
			VikingNews news = new VikingNews();
			VikingNews.Event added = news.Add(VikingNews.Leviathan, "", "", Players(1L), false, 100L, 2);
			Assert.True(news.Mention(added.Id, "viking-a", 11));
			Assert.False(news.Mention(added.Id, "viking-a", 12));
			Assert.Equal(1, added.Remaining);
			Assert.Equal(new List<int> { 11 }, added.UsedLines);
			Assert.True(news.Mention(added.Id, "viking-b", 12));
			Assert.Empty(news.Events);
			Assert.False(news.Mention(added.Id, "viking-c", 13));
		}

		/// <summary>
		/// Checks that a viking tells a player only their latest news from after it last talked to them, no older than
		/// the most age, and not news it told already
		/// </summary>
		[Fact]
		public void NewsForPicksTheLatestSinceTheLastTalk()
		{
			VikingNews news = new VikingNews();
			VikingNews.Event old = news.Add(VikingNews.Skill, "Swords", "", Players(1L), true, 100L, 3);
			VikingNews.Event latest = news.Add(VikingNews.Skill, "Bows", "", Players(1L), true, 300L, 3);
			news.Add(VikingNews.Skill, "Axes", "", Players(2L), true, 400L, 3);
			Assert.Same(latest, news.NewsFor(1L, "viking-a", 0L, 500L, 1000L));
			Assert.Null(news.NewsFor(1L, "viking-a", 300L, 500L, 1000L));
			Assert.Same(latest, news.NewsFor(1L, "viking-a", 0L, 1250L, 1000L));
			Assert.Null(news.NewsFor(1L, "viking-a", 0L, 2000L, 1000L));
			news.Mention(latest.Id, "viking-a", 1);
			Assert.Same(old, news.NewsFor(1L, "viking-a", 0L, 500L, 1000L));
			Assert.Same(latest, news.NewsFor(1L, "viking-b", 0L, 500L, 1000L));
			Assert.Null(news.NewsFor(3L, "viking-a", 0L, 500L, 1000L));
		}

		/// <summary>
		/// Checks that news older than the most age goes
		/// </summary>
		[Fact]
		public void OldNewsExpires()
		{
			VikingNews news = new VikingNews();
			news.Add(VikingNews.Skill, "Swords", "", Players(1L), true, 100L, 3);
			news.Add(VikingNews.Skill, "Bows", "", Players(1L), true, 900L, 3);
			Assert.Equal(1, news.Expire(1200L, 500L));
			Assert.Single(news.Events);
			Assert.Equal("Bows", news.Events[0].Subject);
		}

		/// <summary>
		/// Checks that only the latest MaxEvents events are kept
		/// </summary>
		[Fact]
		public void KeepsTheLatestEvents()
		{
			VikingNews news = new VikingNews();
			for (int i = 0; i < VikingNews.MaxEvents + 5; i++)
			{
				news.Add(VikingNews.Skill, "Skill" + i, "", Players(1L), true, i, 3);
			}
			Assert.Equal(VikingNews.MaxEvents, news.Events.Count);
			Assert.Equal("Skill5", news.Events[0].Subject);
		}

		/// <summary>
		/// Checks that the news reads back as it was written, with its firsts only when they were written
		/// </summary>
		[Fact]
		public void RoundTrips()
		{
			VikingNews news = new VikingNews();
			VikingNews.Event added = news.Add(VikingNews.Death, "greydwarf", "$enemy_greydwarf", Players(1L, 2L), false, 123L, 4);
			news.Mention(added.Id, "viking-a", 42);
			news.Add(VikingNews.Biome, "Plains", "", Players(1L), true, 456L, 2);
			VikingNews read = VikingNews.Decode(news.Encode(true));
			Assert.Equal(news.NextId, read.NextId);
			Assert.Equal(2, read.Events.Count);
			VikingNews.Event first = read.Events[0];
			Assert.Equal(added.Id, first.Id);
			Assert.Equal(VikingNews.Death, first.Kind);
			Assert.Equal("greydwarf", first.Subject);
			Assert.Equal("$enemy_greydwarf", first.Detail);
			Assert.Equal(123L, first.Time);
			Assert.Equal(3, first.Remaining);
			Assert.Equal(Players(1L, 2L), first.Players);
			Assert.Equal(new List<string> { "viking-a" }, first.Mentioned);
			Assert.Equal(new List<int> { 42 }, first.UsedLines);
			Assert.Contains(VikingNews.FirstKey(1L, VikingNews.Biome, "Plains"), read.Firsts);
			Assert.Empty(VikingNews.Decode(news.Encode(false)).Firsts);
		}

		/// <summary>
		/// Checks that nothing, or news written another way, reads as no news
		/// </summary>
		[Fact]
		public void ReadsNothingAsEmpty()
		{
			Assert.Empty(VikingNews.Decode(null).Events);
			ZPackage other = new ZPackage();
			other.Write(99);
			Assert.Empty(VikingNews.Decode(other.GetArray()).Events);
		}

		/// <summary>
		/// Checks that a viking's talks read back as written, move a player it talks to again to the end, and forget the
		/// one it talked to longest ago past the most it remembers
		/// </summary>
		[Fact]
		public void TalksAreRemembered()
		{
			List<VikingIdentity.Talk> talks = new List<VikingIdentity.Talk>();
			VikingIdentity.Note(talks, 1L, 100L);
			VikingIdentity.Note(talks, 2L, 200L);
			VikingIdentity.Note(talks, 1L, 300L);
			List<VikingIdentity.Talk> read = VikingIdentity.Decode(VikingIdentity.Encode(talks));
			Assert.Equal(2, read.Count);
			Assert.Equal(2L, read[0].Player);
			Assert.Equal(1L, read[1].Player);
			Assert.Equal(300L, read[1].Time);
			for (long i = 10L; i < 100L; i++)
			{
				VikingIdentity.Note(talks, i, i);
			}
			Assert.Equal(64, talks.Count);
			Assert.Equal(99L, talks[talks.Count - 1].Player);
		}
	}
}
