using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Makes zombie copies of creature textures, washed out by ZombieSaturation and darkened in patches with Perlin noise
	/// </summary>
	/// <remarks>
	/// The game's textures can't be read directly, so each one is drawn into a render texture on the GPU and read back.
	/// The colors are washed out in the copy rather than through the creature shader's _Saturation, which leaves some
	/// color at -1 and which other shaders (e.g., the Standard shader on an asksvin's teeth) don't have. The noise is laid
	/// over the texture image, offset by the texture's name so different creatures get different patches. Each copy is
	/// made once for each texture and strength step and shared by every zombie that uses it, and all of them are made
	/// again when ZombieMottling, ZombieMottleSize or ZombieSaturation changes
	/// </remarks>
	internal static class Mottling
	{
		/// <summary>
		/// How many strength steps a corpse darkens through as it turns, the last being full strength
		/// </summary>
		public const int Steps = 4;

		/// <summary>
		/// The noise values the darkening starts and reaches full strength at, giving patches with soft edges
		/// </summary>
		private const float PatchStart = 0.42f;
		private const float PatchFull = 0.68f;

		private static readonly Dictionary<Texture, Texture2D[]> copies = new Dictionary<Texture, Texture2D[]>();

		/// <summary>
		/// The ZombieMottling, ZombieMottleSize and ZombieSaturation the copies were made with
		/// </summary>
		private static float madeStrength = -1f;
		private static float madeSize = -1f;
		private static float madeSaturation = float.NaN;

		/// <summary>
		/// Gets the zombie copy of a texture at a strength step, making it the first time
		/// </summary>
		/// <param name="source">The creature's texture</param>
		/// <param name="step">The strength step, from 1 to Steps</param>
		/// <returns>The copy, or the source when there's nothing to change or the texture can't be copied</returns>
		public static Texture Get(Texture source, int step)
		{
			float strength = Plugin.ZombieMottling.Value;
			float size = Plugin.ZombieMottleSize.Value;
			float saturation = Plugin.ZombieSaturation.Value;
			if (strength != madeStrength || size != madeSize || saturation != madeSaturation)
			{
				Clear();
				madeStrength = strength;
				madeSize = size;
				madeSaturation = saturation;
			}
			if ((strength <= 0f && saturation == 0f) || step <= 0 || !(source is Texture2D) || source.width <= 0 || source.height <= 0)
			{
				return(source);
			}
			step = Mathf.Min(step, Steps);
			if (!copies.TryGetValue(source, out Texture2D[] steps))
			{
				steps = new Texture2D[Steps];
				copies[source] = steps;
			}
			if (steps[step - 1] == null)
			{
				float fraction = (float)step / Steps;
				steps[step - 1] = Make((Texture2D)source, strength * fraction, size, saturation * fraction);
			}
			return(steps[step - 1]);
		}

		/// <summary>
		/// Removes every copy, for when the settings change
		/// </summary>
		private static void Clear()
		{
			foreach (Texture2D[] steps in copies.Values)
			{
				foreach (Texture2D copy in steps)
				{
					if (copy != null)
					{
						Object.Destroy(copy);
					}
				}
			}
			copies.Clear();
		}

		/// <summary>
		/// Copies a texture through the GPU, darkens it in Perlin noise patches and washes out its colors
		/// </summary>
		/// <param name="source">The texture to copy</param>
		/// <param name="strength">How dark the darkest patches are, from 0 for not at all to 1 for black</param>
		/// <param name="size">How many patches fit across the texture</param>
		/// <param name="saturation">How much to wash out the colors, from 0 for not at all to -1 for gray, or above 0 to
		/// strengthen them</param>
		/// <returns>The copy</returns>
		private static Texture2D Make(Texture2D source, float strength, float size, float saturation)
		{
			int width = source.width;
			int height = source.height;
			Texture2D copy = SpiritWhite.ReadableCopy(source);
			Color32[] pixels = copy.GetPixels32();
			// Offsets the noise by the texture's name so each creature gets its own patches
			NoiseOffsets(source.name.GetStableHashCode(), out float offsetX, out float offsetY);
			for (int y = 0; y < height; y++)
			{
				float v = (float)y / height * size;
				for (int x = 0; x < width; x++)
				{
					float u = (float)x / width * size;
					float noise = 0.65f * Mathf.PerlinNoise(u + offsetX, v + offsetY) + 0.35f * Mathf.PerlinNoise(u * 2.3f + offsetY, v * 2.3f + offsetX);
					int index = y * width + x;
					pixels[index] = Tint(pixels[index], Shade(noise, strength), saturation);
				}
			}
			copy.SetPixels32(pixels);
			copy.Apply(true, true);
			copy.name = source.name + " (zombie)";
			return(copy);
		}

		/// <summary>
		/// Works out how far a texture's noise is offset, from a seed, so each texture gets its own patches
		/// </summary>
		/// <param name="seed">The seed, from the texture's name</param>
		/// <param name="offsetX">How far the noise is offset across</param>
		/// <param name="offsetY">How far the noise is offset down</param>
		internal static void NoiseOffsets(int seed, out float offsetX, out float offsetY)
		{
			offsetX = (seed & 0xFFFF) / 97f;
			offsetY = ((seed >> 16) & 0xFFFF) / 89f;
		}

		/// <summary>
		/// Works out how much a pixel is darkened for the noise there - not at all below PatchStart, fully at PatchFull and
		/// above, and smoothly between
		/// </summary>
		/// <param name="noise">The noise at the pixel</param>
		/// <param name="strength">How dark the darkest patches are, from 0 for not at all to 1 for black</param>
		/// <returns>What the pixel's color is multiplied by, from 1 for unchanged</returns>
		internal static float Shade(float noise, float strength)
		{
			float patch = Mathf.Clamp01((noise - PatchStart) / (PatchFull - PatchStart));
			return(1f - strength * patch * patch * (3f - 2f * patch));
		}

		/// <summary>
		/// Washes out or strengthens a pixel's color and darkens it, keeping its alpha
		/// </summary>
		/// <param name="pixel">The pixel</param>
		/// <param name="shade">What its color is multiplied by (see Shade)</param>
		/// <param name="saturation">How much to wash out the color, from 0 for not at all to -1 for gray, or above 0 to
		/// strengthen it</param>
		/// <returns>The changed pixel</returns>
		internal static Color32 Tint(Color32 pixel, float shade, float saturation)
		{
			float r = pixel.r;
			float g = pixel.g;
			float b = pixel.b;
			if (saturation != 0f)
			{
				// Moves each channel toward or away from the pixel's gray, by its brightness as the eye sees it
				float gray = 0.299f * r + 0.587f * g + 0.114f * b;
				float keep = 1f + saturation;
				r = gray + (r - gray) * keep;
				g = gray + (g - gray) * keep;
				b = gray + (b - gray) * keep;
			}
			pixel.r = (byte)Mathf.Clamp(r * shade, 0f, 255f);
			pixel.g = (byte)Mathf.Clamp(g * shade, 0f, 255f);
			pixel.b = (byte)Mathf.Clamp(b * shade, 0f, 255f);
			return(pixel);
		}

		/// <summary>
		/// Works out which strength step a zombie's look is at
		/// </summary>
		/// <param name="amount">How far the look has come on, from 0 to 1</param>
		/// <returns>The step, from 0 for none to Steps for full strength</returns>
		internal static int StepFor(float amount)
		{
			return(Mathf.CeilToInt(amount * Steps));
		}
	}
}
