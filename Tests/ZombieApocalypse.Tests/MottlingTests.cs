using UnityEngine;

namespace ZombieApocalypse.Tests
{
	/// <summary>
	/// Tests how a zombie's textures are darkened in patches and washed out, and which strength step its look is at
	/// </summary>
	public class MottlingTests
	{
		/// <summary>
		/// Checks that a seed's low and high halves give the noise's offsets across and down
		/// </summary>
		/// <param name="seed">The seed</param>
		/// <param name="expectedX">The offset across</param>
		/// <param name="expectedY">The offset down</param>
		[Theory]
		[InlineData(0, 0f, 0f)]
		[InlineData(0x00030002, 2f / 97f, 3f / 89f)]
		[InlineData(-1, 65535f / 97f, 65535f / 89f)]
		[InlineData(int.MinValue, 0f, 32768f / 89f)]
		public void NoiseOffsetsComeFromTheSeedsHalves(int seed, float expectedX, float expectedY)
		{
			Mottling.NoiseOffsets(seed, out float offsetX, out float offsetY);
			Assert.Equal(expectedX, offsetX, 4);
			Assert.Equal(expectedY, offsetY, 4);
		}

		/// <summary>
		/// Checks that a negative seed, as many names hash to, still gives offsets that aren't negative and stay within
		/// what 16 bits allow
		/// </summary>
		[Fact]
		public void NegativeSeedGivesOffsetsInRange()
		{
			foreach (int seed in new[] { -1, -2, -65536, -123456789, int.MinValue + 1 })
			{
				Mottling.NoiseOffsets(seed, out float offsetX, out float offsetY);
				Assert.InRange(offsetX, 0f, 65535f / 97f);
				Assert.InRange(offsetY, 0f, 65535f / 89f);
			}
		}

		/// <summary>
		/// Checks that noise up to where the patches start leaves a pixel as it is
		/// </summary>
		/// <param name="noise">The noise at the pixel</param>
		[Theory]
		[InlineData(0f)]
		[InlineData(0.3f)]
		[InlineData(0.42f)]
		public void NoiseBelowThePatchesDoesNotDarken(float noise)
		{
			Assert.Equal(1f, Mottling.Shade(noise, 0.8f), 4);
		}

		/// <summary>
		/// Checks that noise from where the patches reach full strength darkens a pixel by the full strength
		/// </summary>
		/// <param name="noise">The noise at the pixel</param>
		[Theory]
		[InlineData(0.68f)]
		[InlineData(0.9f)]
		[InlineData(1f)]
		public void NoiseAboveThePatchesDarkensFully(float noise)
		{
			Assert.Equal(0.2f, Mottling.Shade(noise, 0.8f), 4);
		}

		/// <summary>
		/// Checks that noise halfway into a patch darkens a pixel by half the strength
		/// </summary>
		[Fact]
		public void NoiseHalfwayDarkensByHalf()
		{
			Assert.Equal(0.6f, Mottling.Shade(0.55f, 0.8f), 4);
		}

		/// <summary>
		/// Checks that a saturation of -1 turns a pixel the gray of its brightness, keeping its alpha
		/// </summary>
		[Fact]
		public void FullWashOutGivesGray()
		{
			// 0.299 * 200 + 0.587 * 100 + 0.114 * 50 = 124.2
			Color32 pixel = Mottling.Tint(new Color32(200, 100, 50, 77), 1f, -1f);
			Assert.Equal((byte)124, pixel.r);
			Assert.Equal((byte)124, pixel.g);
			Assert.Equal((byte)124, pixel.b);
			Assert.Equal((byte)77, pixel.a);
		}

		/// <summary>
		/// Checks that strengthening a color keeps each channel between 0 and 255
		/// </summary>
		[Fact]
		public void StrengtheningClampsTheChannels()
		{
			Color32 pixel = Mottling.Tint(new Color32(250, 10, 10, 255), 1f, 1f);
			Assert.Equal((byte)255, pixel.r);
			Assert.Equal((byte)0, pixel.g);
			Assert.Equal((byte)0, pixel.b);
		}

		/// <summary>
		/// Checks that with no washing out, a pixel is only darkened, each channel rounded down
		/// </summary>
		[Fact]
		public void ShadeAloneDarkens()
		{
			Color32 pixel = Mottling.Tint(new Color32(200, 101, 50, 9), 0.5f, 0f);
			Assert.Equal((byte)100, pixel.r);
			Assert.Equal((byte)50, pixel.g);
			Assert.Equal((byte)25, pixel.b);
			Assert.Equal((byte)9, pixel.a);
		}

		/// <summary>
		/// Checks which of the 4 strength steps a look is at as it comes on - none only at none, and full only at full
		/// </summary>
		/// <param name="amount">How far the look has come on</param>
		/// <param name="expected">The step</param>
		[Theory]
		[InlineData(0f, 0)]
		[InlineData(0.01f, 1)]
		[InlineData(0.25f, 1)]
		[InlineData(0.26f, 2)]
		[InlineData(0.5f, 2)]
		[InlineData(0.75f, 3)]
		[InlineData(0.76f, 4)]
		[InlineData(1f, 4)]
		public void StepRoundsUp(float amount, int expected)
		{
			Assert.Equal(expected, Mottling.StepFor(amount));
		}
	}
}
