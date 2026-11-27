using System.Text.RegularExpressions;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests what a viking says about its village going by its name - when it counts as the village's namesake, and
	/// that its lines fill in every token they use
	/// </summary>
	public class VillageNamesakeSpeechTests
	{
		[Fact]
		public void TheVillageGoesByTheWholeNameOrTheFirst()
		{
			Assert.True(VillageNamesakeSpeech.IsNamesake("Ragna", "Ragna"));
			Assert.True(VillageNamesakeSpeech.IsNamesake("Ragna the Bold", "ragna"));
			Assert.True(VillageNamesakeSpeech.IsNamesake("Ragna the Bold", "Ragna the Bold"));
			Assert.True(VillageNamesakeSpeech.IsNamesake("Bjørn", "Bjorn"));
			Assert.True(VillageNamesakeSpeech.IsNamesake(" Ulf ", "Ulf"));
		}

		[Fact]
		public void OtherNamesAreNoNamesake()
		{
			Assert.False(VillageNamesakeSpeech.IsNamesake("Ragna the Bold", "Ragna the Red"));
			Assert.False(VillageNamesakeSpeech.IsNamesake("Bjorn", "Bjornstead"));
			Assert.False(VillageNamesakeSpeech.IsNamesake("Ulf", "Gamla Uppsala"));
			Assert.False(VillageNamesakeSpeech.IsNamesake("", ""));
			Assert.False(VillageNamesakeSpeech.IsNamesake("Ragna", ""));
			Assert.False(VillageNamesakeSpeech.IsNamesake(null, "Ragna"));
		}

		[Fact]
		public void LinesToAPlayerFillInBothNames()
		{
			int count = VillageNamesakeSpeech.BewilderedLines.Length + VillageNamesakeSpeech.BraggingLines.Length;
			for (int index = 0; index < count; index++)
			{
				string line = VillageNamesakeSpeech.For("Ragna", "Kriona", (int below) => index);
				Assert.Contains("Ragna", line);
				Assert.Contains("Kriona", line);
				Assert.DoesNotContain("{", line);
			}
		}

		[Fact]
		public void NamesThatReadLikeTokensAreSaidAsGiven()
		{
			Assert.Equal("They named the village after me, {village}. {player}. Has a ring to it, doesn't it?", VillageNamesakeSpeech.For("{player}", "{village}", (int below) => VillageNamesakeSpeech.BewilderedLines.Length));
		}

		[Fact]
		public void TalksOnlyUseTokensTheTalkFillsIn()
		{
			foreach (string[] talk in VillageNamesakeSpeech.Talks)
			{
				Assert.Contains(talk, (string line) => line.Contains("{village}") || line.Contains("named after"));
				foreach (string line in talk)
				{
					foreach (Match token in Regex.Matches(line, "\\{[a-z]+\\}"))
					{
						Assert.Contains(token.Value, new[] { "{name}", "{village}" });
					}
				}
			}
		}
	}
}
