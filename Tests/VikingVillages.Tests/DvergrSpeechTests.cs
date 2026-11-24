using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the dvergr vikings' lines - plain text, with only the tokens each table fills in
	/// </summary>
	public class DvergrSpeechTests
	{
		/// <summary>
		/// The tables of lines, with the most format arguments each fills in - none for lines said as they are
		/// </summary>
		public static TheoryData<string, int> Tables()
		{
			return(new TheoryData<string, int>
			{
				{ "DayFirstGreetings", 0 },
				{ "NightFirstGreetings", 0 },
				{ "Introductions", 1 },
				{ "DayGreetings", 1 },
				{ "NightGreetings", 1 },
				{ "DayChat", 1 },
				{ "NightChat", 1 },
				{ "BusyChat", 0 },
				{ "SpotLines", 1 },
				{ "HelpLines", 0 },
				{ "ProudLines", 2 },
				{ "PowerUseLines", 1 },
				{ "PowerLines", 2 }
			});
		}

		/// <summary>
		/// Checks that every line of a table is plain text, with no dash but the hyphen and no ellipsis character, fills
		/// in only the arguments the table has, and formats without throwing
		/// </summary>
		/// <param name="name">The table's field name</param>
		/// <param name="arguments">How many arguments it fills in</param>
		[Theory]
		[MemberData(nameof(Tables))]
		public void LinesAreCleanAndFormat(string name, int arguments)
		{
			FieldInfo field = typeof(DvergrSpeech).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
			Assert.NotNull(field);
			string[] lines = (string[])field.GetValue(null);
			Assert.True(lines.Length >= 3, name + " has fewer than 3 lines");
			Regex token = new Regex("\\{([^}]*)\\}");
			foreach (string line in lines)
			{
				foreach (char c in line)
				{
					Assert.True(c < 128, name + " has a character that isn't plain text: " + line);
				}
				foreach (Match match in token.Matches(line))
				{
					Assert.True(int.TryParse(match.Groups[1].Value, out int index) && index < arguments, name + " fills in " + match.Value + ": " + line);
				}
				if (arguments > 0)
				{
					string formatted = string.Format(line, "Astrid", "Forsaken");
					Assert.DoesNotContain("{", formatted);
				}
			}
		}

		/// <summary>
		/// Checks that every introduction a viking adds to a stranger's greeting gives its name
		/// </summary>
		[Fact]
		public void IntroductionsNameTheViking()
		{
			FieldInfo field = typeof(DvergrSpeech).GetField("Introductions", BindingFlags.NonPublic | BindingFlags.Static);
			string[] introductions = (string[])field.GetValue(null);
			foreach (string line in introductions)
			{
				Assert.Contains("{0}", line);
			}
		}
	}
}
