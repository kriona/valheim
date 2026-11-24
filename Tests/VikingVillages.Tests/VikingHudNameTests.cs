using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how far a living viking's name over its health bar is lifted while it runs to more than one line
	/// </summary>
	public class VikingHudNameTests
	{
		[Theory]
		[InlineData(38f, 19f, 9.5f)]
		[InlineData(19f, 19f, 0f)]
		[InlineData(57f, 19f, 19f)]
		[InlineData(10f, 19f, 0f)]
		public void NameIsLiftedSoItsLastLineSitsWhereOneLineWould(float height, float firstLine, float lift)
		{
			Assert.Equal(lift, VikingHudName.Lift(height, firstLine), 3);
		}
	}
}
