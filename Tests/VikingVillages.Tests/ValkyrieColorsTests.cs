using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the colors a valkyrie can be and how her texture's red is painted over in them
	/// </summary>
	public class ValkyrieColorsTests
	{
		/// <summary>
		/// Checks that there are eight colors, each with a name, red first, sky blue being #33CCFF, and purple and orange
		/// last, after the six saved before them
		/// </summary>
		[Fact]
		public void EightColorsRedFirst()
		{
			Assert.Equal(8, ValkyrieColors.Targets.Length);
			Assert.Equal("purple", ValkyrieColors.Names[6]);
			Assert.Equal("orange", ValkyrieColors.Names[7]);
			Assert.Equal(ValkyrieColors.Targets.Length, ValkyrieColors.Names.Length);
			Assert.Equal("red", ValkyrieColors.Names[ValkyrieColors.Red]);
			Color32 blue = ValkyrieColors.Targets[System.Array.IndexOf(ValkyrieColors.Names, "sky blue")];
			Assert.Equal(new byte[] { 0x33, 0xCC, 0xFF }, new byte[] { blue.r, blue.g, blue.b });
		}

		/// <summary>
		/// Checks that only the indexes of the colors are colors
		/// </summary>
		[Fact]
		public void OnlyTheColorsIndexesAreColors()
		{
			Assert.False(ValkyrieColors.IsColor(-1));
			Assert.True(ValkyrieColors.IsColor(0));
			Assert.True(ValkyrieColors.IsColor(7));
			Assert.False(ValkyrieColors.IsColor(8));
		}

		/// <summary>
		/// Checks a pixel's hue in degrees
		/// </summary>
		/// <param name="r">Its red</param>
		/// <param name="g">Its green</param>
		/// <param name="b">Its blue</param>
		/// <param name="hue">Its hue</param>
		[Theory]
		[InlineData(255, 0, 0, 0f)]
		[InlineData(0, 255, 0, 120f)]
		[InlineData(0, 0, 255, 240f)]
		[InlineData(255, 0, 255, 300f)]
		[InlineData(100, 100, 100, 0f)]
		[InlineData(255, 0, 128, 329.9f)]
		public void HueIsInDegrees(byte r, byte g, byte b, float hue)
		{
			Assert.Equal(hue, ValkyrieColors.Hue(new Color32(r, g, b, 255)), 1);
		}

		/// <summary>
		/// Checks that the texture's reds - its cracks, cloth and their edges blending into the dark - count as red, and
		/// its skin, browns, grays and dark blue-black don't
		/// </summary>
		/// <param name="r">The pixel's red</param>
		/// <param name="g">Its green</param>
		/// <param name="b">Its blue</param>
		/// <param name="red">Whether it counts wholly as red, or not at all</param>
		[Theory]
		[InlineData(157, 4, 8, true)]
		[InlineData(123, 17, 19, true)]
		[InlineData(59, 11, 25, true)]
		[InlineData(62, 36, 28, false)]
		[InlineData(230, 190, 160, false)]
		[InlineData(145, 107, 107, false)]
		[InlineData(37, 30, 44, false)]
		[InlineData(0, 0, 0, false)]
		public void OnlyTheRedsCount(byte r, byte g, byte b, bool red)
		{
			Assert.Equal((red ? 1f : 0f), ValkyrieColors.Redness(new Color32(r, g, b, 255)), 3);
		}

		/// <summary>
		/// Checks that a red only tinged gray, or between red and the browns, counts partly
		/// </summary>
		[Fact]
		public void TingedRedsCountPartly()
		{
			float tinged = ValkyrieColors.Redness(new Color32(119, 71, 71, 255));
			Assert.InRange(tinged, 0.1f, 0.9f);
			float orange = ValkyrieColors.Redness(new Color32(200, 30, 0, 255));
			Assert.InRange(orange, 0.1f, 0.9f);
		}

		/// <summary>
		/// Checks that red is painted over in the new color as bright as the red is next to the texture's brightest,
		/// keeping its alpha
		/// </summary>
		[Fact]
		public void RedIsPaintedOverAsBright()
		{
			Color32 white = ValkyrieColors.Recolor(new Color32(157, 4, 8, 200), new Color32(255, 255, 255, 255));
			Assert.Equal(new byte[] { 255, 255, 255, 200 }, new byte[] { white.r, white.g, white.b, white.a });
			Color32 blue = ValkyrieColors.Recolor(new Color32(123, 17, 19, 255), new Color32(0x33, 0xCC, 0xFF, 255));
			Assert.Equal(new byte[] { 41, 164, 205 }, new byte[] { blue.r, blue.g, blue.b });
		}

		/// <summary>
		/// Checks that what isn't red is left as it is
		/// </summary>
		[Fact]
		public void TheRestIsLeftAlone()
		{
			Color32 skin = new Color32(230, 190, 160, 255);
			Assert.Equal(skin, ValkyrieColors.Recolor(skin, new Color32(255, 255, 255, 255)));
			Color32 dark = new Color32(37, 30, 44, 255);
			Assert.Equal(dark, ValkyrieColors.Recolor(dark, new Color32(255, 105, 180, 255)));
		}
	}
}
