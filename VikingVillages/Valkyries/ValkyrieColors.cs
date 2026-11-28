using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The colors a valkyrie can be - red, the game's own, or the red of her body's texture painted over in white,
	/// yellow, sky blue, pink, black, purple or orange (see ValkyrieColor). Each recolored texture is made once, the first time a
	/// valkyrie of that color loads, and shared by every valkyrie of it
	/// </summary>
	internal static class ValkyrieColors
	{
		/// <summary>
		/// Each color's name, for the log, and what the red is painted over in, by the index kept in a valkyrie's ZDO - in
		/// a fixed order, since that index is saved. Red is left as it is
		/// </summary>
		internal static readonly string[] Names = { "red", "white", "yellow", "sky blue", "pink", "black", "purple", "orange" };
		internal static readonly Color32[] Targets =
		{
			new Color32(255, 0, 0, 255),
			new Color32(255, 255, 255, 255),
			new Color32(255, 235, 0, 255),
			new Color32(0x33, 0xCC, 0xFF, 255),
			new Color32(255, 105, 180, 255),
			new Color32(22, 22, 24, 255),
			new Color32(150, 60, 230, 255),
			new Color32(255, 130, 0, 255)
		};

		/// <summary>
		/// The index of red, which needs no painting over
		/// </summary>
		internal const int Red = 0;

		/// <summary>
		/// The hues that count as red, in degrees - wholly red up to RedHueFull on the orange side and from MagentaHueFull
		/// on the magenta side, fading out to RedHueEdge and MagentaHueEdge. The magenta side reaches farther, since the
		/// red's edges blend into the texture's dark blue-black, and the orange side stops short of its skin tones and
		/// browns
		/// </summary>
		private const float RedHueFull = 8f;
		private const float RedHueEdge = 13f;
		private const float MagentaHueFull = 330f;
		private const float MagentaHueEdge = 318f;

		/// <summary>
		/// How saturated a color has to be to start counting as red, and to count wholly - leaving out the grays and her
		/// skin, which are only tinged with it
		/// </summary>
		private const float SaturationStart = 0.3f;
		private const float SaturationFull = 0.55f;

		/// <summary>
		/// How bright the texture's red is at its brightest, as a share of full - painted over as the full new color, and
		/// darker reds darker in proportion - and how far past the new color a red brighter than that goes
		/// </summary>
		private const float RedBrightness = 0.6f;
		private const float MostBrightness = 1.3f;

		/// <summary>
		/// The recolored copies of her materials, by the original's instance ID and the color's index
		/// </summary>
		private static readonly Dictionary<long, Material> materials = new Dictionary<long, Material>();

		/// <summary>
		/// Checks whether a color index is one of the colors
		/// </summary>
		/// <param name="color">The index</param>
		/// <returns>True when it is</returns>
		internal static bool IsColor(int color)
		{
			return(color >= 0 && color < Targets.Length);
		}

		/// <summary>
		/// Works out a pixel's hue, in degrees
		/// </summary>
		/// <param name="pixel">The pixel</param>
		/// <returns>Its hue from 0 up to 360, or 0 for a gray</returns>
		internal static float Hue(Color32 pixel)
		{
			float r = pixel.r / 255f;
			float g = pixel.g / 255f;
			float b = pixel.b / 255f;
			float max = Mathf.Max(r, Mathf.Max(g, b));
			float min = Mathf.Min(r, Mathf.Min(g, b));
			float range = max - min;
			if (range <= 0f)
			{
				return(0f);
			}
			float hue;
			if (max == r)
			{
				hue = (g - b) / range;
			}
			else if (max == g)
			{
				hue = 2f + (b - r) / range;
			}
			else
			{
				hue = 4f + (r - g) / range;
			}
			hue *= 60f;
			return((hue < 0f) ? hue + 360f : hue);
		}

		/// <summary>
		/// Works out how much a pixel counts as red - by its hue and saturation (see RedHueFull and SaturationStart)
		/// </summary>
		/// <param name="pixel">The pixel</param>
		/// <returns>From 0, not red at all, to 1, wholly red</returns>
		internal static float Redness(Color32 pixel)
		{
			int max = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
			if (max == 0)
			{
				return(0f);
			}
			float saturation = (max - Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b))) / (float)max;
			float bySaturation = Mathf.Clamp01((saturation - SaturationStart) / (SaturationFull - SaturationStart));
			float hue = Hue(pixel);
			float byHue;
			if (hue <= RedHueFull || hue >= MagentaHueFull)
			{
				byHue = 1f;
			}
			else if (hue < RedHueEdge)
			{
				byHue = (RedHueEdge - hue) / (RedHueEdge - RedHueFull);
			}
			else if (hue > MagentaHueEdge)
			{
				byHue = (hue - MagentaHueEdge) / (MagentaHueFull - MagentaHueEdge);
			}
			else
			{
				byHue = 0f;
			}
			return(byHue * bySaturation);
		}

		/// <summary>
		/// Paints a pixel's red over in a new color - the new color as bright as the red is next to the texture's
		/// brightest (see RedBrightness), blended in as far as the pixel counts as red (see Redness)
		/// </summary>
		/// <param name="pixel">The pixel</param>
		/// <param name="target">The new color</param>
		/// <returns>The painted pixel, keeping its alpha</returns>
		internal static Color32 Recolor(Color32 pixel, Color32 target)
		{
			float redness = Redness(pixel);
			if (redness <= 0f)
			{
				return(pixel);
			}
			float brightness = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) / 255f;
			float scale = Mathf.Clamp(brightness / RedBrightness, 0f, MostBrightness);
			return(new Color32(Blend(pixel.r, target.r * scale, redness), Blend(pixel.g, target.g * scale, redness), Blend(pixel.b, target.b * scale, redness), pixel.a));
		}

		/// <summary>
		/// Blends one channel of a pixel toward a new value
		/// </summary>
		/// <param name="from">The pixel's value</param>
		/// <param name="to">The new value, which may be past 255</param>
		/// <param name="amount">How far toward it, from 0 to 1</param>
		/// <returns>The blended value</returns>
		private static byte Blend(byte from, float to, float amount)
		{
			return((byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(from, Mathf.Min(to, 255f), amount)), 0, 255));
		}

		/// <summary>
		/// Gives a valkyrie's model a color - each of its renderers' materials swapped for a copy whose main texture has its
		/// red painted over (see Recolor), or put back to her own for red
		/// </summary>
		/// <param name="model">Her model</param>
		/// <param name="originals">Each renderer's own materials, noted the first time she's colored, and filled in then</param>
		/// <param name="color">The color's index</param>
		public static void Apply(Transform model, Dictionary<Renderer, Material[]> originals, int color)
		{
			if (model == null || !IsColor(color) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
			{
				return;
			}
			foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
			{
				if (renderer is ParticleSystemRenderer)
				{
					continue;
				}
				if (!originals.TryGetValue(renderer, out Material[] own))
				{
					own = renderer.sharedMaterials;
					originals[renderer] = own;
				}
				Material[] colored = new Material[own.Length];
				for (int i = 0; i < own.Length; i++)
				{
					colored[i] = ((color == Red) ? own[i] : Colored(own[i], color));
				}
				renderer.sharedMaterials = colored;
			}
		}

		/// <summary>
		/// Gets the copy of a material in a color, making it the first time
		/// </summary>
		/// <param name="material">The material</param>
		/// <param name="color">The color's index</param>
		/// <returns>The copy, or the material itself when it has no main texture or it couldn't be copied</returns>
		private static Material Colored(Material material, int color)
		{
			if (material == null || material.mainTexture == null)
			{
				return(material);
			}
			long key = ((long)material.GetInstanceID() << 8) | (long)color;
			if (materials.TryGetValue(key, out Material made) && made != null)
			{
				return(made);
			}
			Texture2D texture = RecoloredTexture(material.mainTexture, Targets[color]);
			if (texture == null)
			{
				return(material);
			}
			texture.name = material.mainTexture.name + " " + Names[color];
			made = new Material(material);
			made.name = material.name + " " + Names[color];
			made.mainTexture = texture;
			materials[key] = made;
			Plugin.Log.LogInfo("Made the " + Names[color] + " valkyrie's " + material.mainTexture.name);
			return(made);
		}

		/// <summary>
		/// Makes a copy of a texture with its red painted over in a new color - drawn into a render texture and read back,
		/// since the game's textures can't be read directly
		/// </summary>
		/// <param name="source">The texture</param>
		/// <param name="target">The new color</param>
		/// <returns>The copy, or null when it couldn't be made</returns>
		private static Texture2D RecoloredTexture(Texture source, Color32 target)
		{
			int width = source.width;
			int height = source.height;
			if (width <= 0 || height <= 0)
			{
				return(null);
			}
			RenderTexture render = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
			RenderTexture previous = RenderTexture.active;
			Graphics.Blit(source, render);
			RenderTexture.active = render;
			Texture2D copy = new Texture2D(width, height, TextureFormat.RGBA32, true, false);
			copy.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
			RenderTexture.active = previous;
			RenderTexture.ReleaseTemporary(render);
			Color32[] pixels = copy.GetPixels32();
			for (int i = 0; i < pixels.Length; i++)
			{
				pixels[i] = Recolor(pixels[i], target);
			}
			copy.SetPixels32(pixels);
			copy.wrapMode = source.wrapMode;
			copy.filterMode = source.filterMode;
			copy.anisoLevel = source.anisoLevel;
			copy.Apply(true, true);
			return(copy);
		}
	}
}
