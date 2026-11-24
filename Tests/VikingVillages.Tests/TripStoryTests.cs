using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests what a viking remembers of its last trip, how long it talks about it, and what it says
	/// </summary>
	public class TripStoryTests
	{
		/// <summary>
		/// Names a biome for a line, for the tests
		/// </summary>
		/// <param name="biome">The biome, by VikingGear's order</param>
		/// <returns>The name</returns>
		private static string Biome(int biome)
		{
			return("Biome" + biome);
		}

		[Fact]
		public void TheWorstThingIsTheOneToTell()
		{
			TripStory story = new TripStory { Portals = 1, Boat = "karve", Foraged = 3 };
			Assert.Equal(TripStory.Fact.Portal, story.Notable(0));
			story.Wrecked = true;
			Assert.Equal(TripStory.Fact.Wrecked, story.Notable(0));
			Assert.Equal(TripStory.Fact.None, new TripStory().Notable(0));
			Assert.Equal(TripStory.Fact.Foraged, new TripStory { Foraged = 1 }.Notable(0));
		}

		[Fact]
		public void OnlyHarderBiomesAreWorthTelling()
		{
			TripStory story = new TripStory();
			story.AddBiome(1);
			story.AddBiome(3);
			Assert.False(story.AddBiome(3));
			Assert.Equal(3, story.HardestBiome(1));
			Assert.Equal(-1, story.HardestBiome(3));
			Assert.Equal(TripStory.Fact.None, story.Notable(3));
			Assert.Equal(TripStory.Fact.Biome, story.Notable(2));
		}

		[Theory]
		[InlineData(0L, 100L, false)]
		[InlineData(100L, 50L, false)]
		[InlineData(100L, 100L + 600L * 10L, true)]
		[InlineData(100L, 100L + 910L * 10L, false)]
		public void TalksAboutItForHalfAGameDay(long arrived, long now, bool expected)
		{
			// A game day of 1800 seconds, so 12 game hours are 900 seconds
			Assert.Equal(expected, TripStory.StillTalked(arrived, now, 1800f, 10.0));
		}

		[Fact]
		public void StorySurvivesBeingWrittenAndRead()
		{
			TripStory story = new TripStory { From = "Eikvik", To = "Hrafnheim", ToSpot = new Vector3(1f, 2f, 3f), Portals = 2, Boat = "longship", Wrecked = true, VillageGone = true, Rerouted = true, TurnedHome = true, Foraged = 4, Arrived = true };
			story.AddBiome(2);
			TripStory read = TripStory.Decode(story.Encode());
			Assert.Equal(story.From, read.From);
			Assert.Equal(story.To, read.To);
			Assert.Equal(story.ToSpot, read.ToSpot);
			Assert.Equal(2, read.Portals);
			Assert.Equal("longship", read.Boat);
			Assert.True(read.Wrecked && read.VillageGone && read.Rerouted && read.TurnedHome && read.Arrived);
			Assert.Equal(new List<int> { 2 }, read.Biomes);
			Assert.Equal(4, read.Foraged);
			Assert.Null(TripStory.Decode(null));
			Assert.Null(TripStory.Decode(new byte[] { 7 }));
		}

		[Fact]
		public void LinesAreFilledInFromTheTrip()
		{
			TripStory story = new TripStory { To = "Hrafnheim", Boat = "karve" };
			story.AddBiome(4);
			Assert.Equal("Sailed to Hrafnheim in a karve through the Biome4, Astrid.", TripSpeech.Fill("Sailed to {to} in a {boat} through the {biome}, {player}.", story, 0, "Astrid", Biome));
		}

		[Fact]
		public void LinesNamingAVillageWithNoNameAreLeftOut()
		{
			List<string> fitting = TripSpeech.Fitting(new[] { "We went to {to}.", "Long road." }, new TripStory());
			Assert.Equal(new List<string> { "Long road." }, fitting);
		}

		[Fact]
		public void EveryThingWorthTellingHasLines()
		{
			foreach (TripStory.Fact fact in System.Enum.GetValues(typeof(TripStory.Fact)))
			{
				if (fact == TripStory.Fact.None)
				{
					continue;
				}
				Assert.True(TripSpeech.Answers.ContainsKey(fact), "No answers for " + fact);
				Assert.True(TripSpeech.Talks.ContainsKey(fact), "No talks for " + fact);
				Assert.Contains(TripSpeech.Fitting(TripSpeech.Answers[fact], new TripStory()), (string line) => line.Length > 0);
			}
		}

		[Fact]
		public void NewsIsHeardFromNearbyVillagesLately()
		{
			List<NeighborEvent> all = new List<NeighborEvent>
			{
				new NeighborEvent { Kind = NeighborEvent.Disbanded, Farm = new Vector3(1000f, 0f, 0f), At = 900L },
				new NeighborEvent { Kind = NeighborEvent.Death, Farm = new Vector3(9000f, 0f, 0f), At = 900L },
				new NeighborEvent { Kind = NeighborEvent.Started, Farm = new Vector3(500f, 0f, 0f), At = 100L },
				new NeighborEvent { Kind = NeighborEvent.Newcomer, Farm = Vector3.zero, At = 950L }
			};
			List<NeighborEvent> heard = NeighborNews.Hearable(all, Vector3.zero, 1000L, 500L);
			Assert.Single(heard);
			Assert.Equal(NeighborEvent.Disbanded, heard[0].Kind);
		}

		[Fact]
		public void NewsKeepsOnlyTheLatest()
		{
			List<NeighborEvent> all = new List<NeighborEvent>();
			NeighborNews.Add(all, new NeighborEvent { Kind = "old", At = 0L }, 1000L, 500L);
			Assert.Empty(all);
			for (int i = 0; i < 70; i++)
			{
				NeighborNews.Add(all, new NeighborEvent { Kind = "k" + i, At = 1000L }, 1000L, 500L);
			}
			Assert.Equal(60, all.Count);
			Assert.Equal("k69", all[all.Count - 1].Kind);
		}

		[Fact]
		public void NewsSurvivesBeingWrittenAndRead()
		{
			List<NeighborEvent> all = new List<NeighborEvent> { new NeighborEvent { Kind = NeighborEvent.QuestBack, Farm = new Vector3(1f, 2f, 3f), Village = "Eikvik", Detail = "MineCopper|CopperOre", At = 42L } };
			List<NeighborEvent> read = NeighborNews.Decode(NeighborNews.Encode(all));
			Assert.Single(read);
			Assert.Equal("Eikvik", read[0].Village);
			Assert.Equal("MineCopper|CopperOre", read[0].Detail);
			Assert.Equal(42L, read[0].At);
			Assert.Empty(NeighborNews.Decode(null));
		}

		[Fact]
		public void NewsLinesSayHowWordCameAndWhatHappened()
		{
			System.Func<string[], string> first = (string[] lines) => lines[0];
			NeighborEvent quest = new NeighborEvent { Kind = NeighborEvent.QuestBack, Village = "Eikvik", Detail = "MineCopper|CopperOre" };
			Assert.Equal("A rune stick came from Eikvik: their folk came home from the copper mines with copper ore.", NeighborSpeech.Line(quest, false, true, (string item) => "copper ore", first));
			NeighborEvent gone = new NeighborEvent { Kind = NeighborEvent.Disbanded };
			Assert.Equal("Before I left the next village over, they've left their homes, and the fire's gone cold.", NeighborSpeech.Line(gone, true, false, (string item) => item, first));
			NeighborEvent death = new NeighborEvent { Kind = NeighborEvent.Death, Village = "Eikvik", Detail = "Ragnar" };
			Assert.Equal("A raven brought word from Eikvik: they lost Ragnar. Odin keep them.", NeighborSpeech.Line(death, false, false, (string item) => item, first));
			// Every kind of thing has its lines, with nothing left unfilled
			foreach (string kind in new[] { NeighborEvent.Disbanded, NeighborEvent.Started, NeighborEvent.QuestBack, NeighborEvent.Death, NeighborEvent.Newcomer })
			{
				Assert.True(NeighborSpeech.Bodies.ContainsKey(kind) && NeighborSpeech.Replies.ContainsKey(kind), "No lines for " + kind);
				foreach (string body in NeighborSpeech.Bodies[kind])
				{
					string line = NeighborSpeech.Line(new NeighborEvent { Kind = kind, Village = "V", Detail = "Ragnar" }, false, true, (string item) => item, (string[] lines) => ((lines == NeighborSpeech.Bodies[kind]) ? body : lines[0]));
					Assert.DoesNotContain("{", line);
				}
			}
		}
	}
}
