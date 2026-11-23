using System.Collections.Generic;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how the test log writes the game's time of day, names a run's file, and tells one kind of exception from
	/// another
	/// </summary>
	public class TestLogTests
	{
		/// <summary>
		/// Checks that midnight, noon and a time in between are written as hours and minutes
		/// </summary>
		[Fact]
		public void ClockWritesHoursAndMinutes()
		{
			Assert.Equal("00:00", TestLog.Clock(0f));
			Assert.Equal("12:00", TestLog.Clock(0.5f));
			Assert.Equal("06:30", TestLog.Clock(6.5f / 24f));
		}

		/// <summary>
		/// Checks that a fraction at or past the end of the day wraps around to the next one
		/// </summary>
		[Fact]
		public void ClockWrapsPastMidnight()
		{
			Assert.Equal("00:00", TestLog.Clock(1f));
			Assert.Equal("23:59", TestLog.Clock(0.9999f));
			Assert.Equal("12:00", TestLog.Clock(1.5f));
		}

		/// <summary>
		/// Checks that the key is the message and the first line of the stack trace, so the same exception thrown from
		/// the same place counts as one kind
		/// </summary>
		[Fact]
		public void ExceptionKeyKeepsMessageAndFirstFrame()
		{
			string key = TestLog.ExceptionKey("NullReferenceException: Object reference not set ", "VillageLife.Update ()\nUnityEngine.Something ()\n");
			Assert.Equal("NullReferenceException: Object reference not set @ VillageLife.Update ()", key);
		}

		/// <summary>
		/// Checks that the same message thrown from two places counts as two kinds
		/// </summary>
		[Fact]
		public void ExceptionKeyTellsPlacesApart()
		{
			string first = TestLog.ExceptionKey("Boom", "A.Run ()\nB.Run ()");
			string second = TestLog.ExceptionKey("Boom", "C.Run ()\nB.Run ()");
			Assert.NotEqual(first, second);
		}

		/// <summary>
		/// Checks that a run's file is named after its biome, and numbered from 2 when that name is taken, skipping every
		/// number already taken
		/// </summary>
		[Fact]
		public void FreeFileNumbersPastTakenNames()
		{
			string folder = "runs";
			HashSet<string> taken = new HashSet<string>();
			string first = TestLog.FreeFile(folder, "Meadows", taken.Contains);
			Assert.Equal(System.IO.Path.Combine(folder, "Meadows.log"), first);
			taken.Add(first);
			taken.Add(System.IO.Path.Combine(folder, "Meadows-2.log"));
			Assert.Equal(System.IO.Path.Combine(folder, "Meadows-3.log"), TestLog.FreeFile(folder, "Meadows", taken.Contains));
			Assert.Equal(System.IO.Path.Combine(folder, "Swamp.log"), TestLog.FreeFile(folder, "Swamp", taken.Contains));
		}

		/// <summary>
		/// Checks that a line starting with the time the way Stamp writes it counts as stamped, and others don't, so
		/// the run's file stamps only the lines that came without one
		/// </summary>
		[Fact]
		public void IsStampedSpotsTheTime()
		{
			Assert.True(TestLog.IsStamped("[14:02:37 day 41 22:10] Death: Astrid"));
			Assert.False(TestLog.IsStamped("Skill: Astrid reached Run 12"));
			Assert.False(TestLog.IsStamped("[Info] something"));
			Assert.False(TestLog.IsStamped("[14:02] short"));
			Assert.False(TestLog.IsStamped(""));
			Assert.False(TestLog.IsStamped(null));
		}

		/// <summary>
		/// Checks that an exception with no stack trace is keyed by its message alone
		/// </summary>
		[Fact]
		public void ExceptionKeyWithoutStackTrace()
		{
			Assert.Equal("Boom", TestLog.ExceptionKey("Boom", ""));
			Assert.Equal("Boom", TestLog.ExceptionKey("Boom", null));
		}
	}
}
