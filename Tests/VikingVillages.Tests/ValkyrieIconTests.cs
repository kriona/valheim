using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a valkyrie's icon works out each pixel's color and how see-through it is from it taken over black and
	/// over white
	/// </summary>
	public class ValkyrieIconTests
	{
		[Fact]
		public void TheBackgroundIsSeeThrough()
		{
			Assert.Equal(0f, ValkyrieIcon.Unmatte(Color.black, Color.white).a, 3);
		}

		[Fact]
		public void SheIsSolidWhereSheLooksTheSameOverBoth()
		{
			Color feather = new Color(0.8f, 0.7f, 0.6f, 0f);
			Color pixel = ValkyrieIcon.Unmatte(feather, feather);
			Assert.Equal(1f, pixel.a, 3);
			Assert.Equal(0.8f, pixel.r, 3);
			Assert.Equal(0.6f, pixel.b, 3);
		}

		[Fact]
		public void HerEdgesAreHalfSeeThroughWithTheirOwnColor()
		{
			// A pixel half covered by a color of 0.6, over black and over white
			Color pixel = ValkyrieIcon.Unmatte(new Color(0.3f, 0.3f, 0.3f), new Color(0.8f, 0.8f, 0.8f));
			Assert.Equal(0.5f, pixel.a, 3);
			Assert.Equal(0.6f, pixel.r, 3);
		}
	}
}
