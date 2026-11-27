using UnityEngine;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a PC viking's step off a ledge is turned aside and what a step lands on
	/// </summary>
	public class LedgesTests
	{
		/// <summary>
		/// Checks that a heading turns toward the right for a positive turn and the left for a negative one, the way
		/// Quaternion.Euler(0, degrees, 0) turns it, keeping its length and staying flat
		/// </summary>
		/// <param name="degrees">How far to turn forward</param>
		/// <param name="x">The x of the heading expected</param>
		/// <param name="z">The z of the heading expected</param>
		[Theory]
		[InlineData(0f, 0f, 1f)]
		[InlineData(90f, 1f, 0f)]
		[InlineData(-90f, -1f, 0f)]
		[InlineData(45f, 0.7071f, 0.7071f)]
		[InlineData(-45f, -0.7071f, 0.7071f)]
		[InlineData(180f, 0f, -1f)]
		public void TurnsForward(float degrees, float x, float z)
		{
			Vector3 turned = Ledges.Turn(Vector3.forward, degrees);
			Assert.Equal(x, turned.x, 3);
			Assert.Equal(0f, turned.y, 3);
			Assert.Equal(z, turned.z, 3);
		}

		/// <summary>
		/// Checks that turning right a quarter turn takes right to back
		/// </summary>
		[Fact]
		public void TurnsRightToBack()
		{
			Vector3 turned = Ledges.Turn(Vector3.right, 90f);
			Assert.Equal(0f, turned.x, 3);
			Assert.Equal(-1f, turned.z, 3);
		}

		/// <summary>
		/// Checks that a step down of up to SafeDrop is footing whatever is under it, and a deeper one is a fall unless
		/// it lands in water deep enough to swim in
		/// </summary>
		/// <param name="drop">How far down the ground is, in meters</param>
		/// <param name="deepWater">Whether the water there is deep enough to break a fall</param>
		/// <param name="expected">The name of what the step lands on, since the test can't take the internal enum</param>
		[Theory]
		[InlineData(-1f, false, "Footing")]
		[InlineData(0.5f, false, "Footing")]
		[InlineData(2f, false, "Footing")]
		[InlineData(2f, true, "Footing")]
		[InlineData(2.5f, false, "Fall")]
		[InlineData(2.5f, true, "Water")]
		[InlineData(40f, false, "Fall")]
		[InlineData(40f, true, "Water")]
		public void LandsOn(float drop, bool deepWater, string expected)
		{
			Assert.Equal(expected, Ledges.LandingOf(drop, deepWater).ToString());
		}
	}
}
