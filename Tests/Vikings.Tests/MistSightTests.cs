using Xunit;

namespace Kriona.Vikings.Tests
{
	/// <summary>
	/// Tests when the Mistlands' mist keeps a viking from seeing something
	/// </summary>
	public class MistSightTests
	{
		/// <summary>
		/// Checks that the mist only blocks sight past its range, with either end in it and neither within reach of
		/// something that pushes it back
		/// </summary>
		[Theory]
		[InlineData(20f, true, false, false, false, true)]
		[InlineData(20f, false, true, false, false, true)]
		[InlineData(20f, true, true, false, false, true)]
		[InlineData(20f, false, false, false, false, false)]
		[InlineData(MistSight.Range, true, true, false, false, false)]
		[InlineData(10f, true, true, false, false, false)]
		[InlineData(20f, true, true, true, false, false)]
		[InlineData(20f, true, true, false, true, false)]
		public void BlocksOnlyPastItsRangeInTheDarkMist(float distance, bool viewerInMist, bool targetInMist, bool viewerLit, bool targetLit, bool expected)
		{
			Assert.Equal(expected, MistSight.Blocked(distance, viewerInMist, targetInMist, viewerLit, targetLit));
		}
	}
}
