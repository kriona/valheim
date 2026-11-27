using System.Collections.Generic;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests what a viking says to a player wearing no clothes - which lines fit where they are, and that every line
	/// fills in the player's name
	/// </summary>
	public class VikingClothesSpeechTests
	{
		[Fact]
		public void TheColdIsLeftOutInTheAshlands()
		{
			string[] ashlands = VikingConcernSpeech.Pool(VikingClothesSpeech.ChatLines, VikingClothesSpeech.ColdChatLines, VikingClothesSpeech.HotChatLines, true);
			string[] elsewhere = VikingConcernSpeech.Pool(VikingClothesSpeech.ChatLines, VikingClothesSpeech.ColdChatLines, VikingClothesSpeech.HotChatLines, false);
			Assert.Equal(VikingClothesSpeech.ChatLines.Length + VikingClothesSpeech.HotChatLines.Length, ashlands.Length);
			Assert.Equal(VikingClothesSpeech.ChatLines.Length + VikingClothesSpeech.ColdChatLines.Length, elsewhere.Length);
			Assert.DoesNotContain(VikingClothesSpeech.HotChatLines[0], elsewhere);
			Assert.DoesNotContain(VikingClothesSpeech.ColdChatLines[0], ashlands);
		}

		[Fact]
		public void LinesForTheAshlandsAndAnywhereDontTalkOfTheCold()
		{
			List<string> lines = new List<string>(VikingClothesSpeech.CallLines);
			lines.AddRange(VikingClothesSpeech.HotCallLines);
			lines.AddRange(VikingClothesSpeech.ChatLines);
			lines.AddRange(VikingClothesSpeech.HotChatLines);
			foreach (string line in lines)
			{
				foreach (string word in new[] { "cold", "chill", "freez", "warm", "fire" })
				{
					Assert.DoesNotContain(word, line.ToLowerInvariant());
				}
			}
		}

		[Fact]
		public void ChatLinesFillInThePlayersName()
		{
			List<string> lines = new List<string>(VikingClothesSpeech.ChatLines);
			lines.AddRange(VikingClothesSpeech.ColdChatLines);
			lines.AddRange(VikingClothesSpeech.HotChatLines);
			foreach (string line in lines)
			{
				string said = string.Format(line, "Kriona");
				Assert.Contains("Kriona", said);
				Assert.DoesNotContain("{", said);
			}
		}
	}
}
