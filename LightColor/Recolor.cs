using System.Collections.Generic;
using UnityEngine;

namespace LightColor
{
	/// <summary>
	/// Shifts colors, gradients and emissive materials to a target color, keeping their own brightness and shading
	/// </summary>
	/// <remarks>
	/// A color takes the target's hue, its saturation is scaled by the target's saturation and its brightness by the
	/// target's brightness, so an orange-to-red flame becomes a light-to-dark blue one rather than flat blue. Grays,
	/// like smoke, are left alone
	/// </remarks>
	internal static class Recolor
	{
		private const float MinSaturation = 0.1f;

		/// <summary>
		/// The weakest a replaced glow gets, for a black or nearly black color - 5 out of F
		/// </summary>
		private const float MinReplaceStrength = 5f / 15f;
		private const int MaxTextureSize = 512;
		private const string EmissionKeyword = "_EMISSION";

		private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
		private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
		private static readonly int BaseColorId = Shader.PropertyToID("_Color");
		private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
		private static readonly int[] ColorIds = { BaseColorId, EmissionColorId, TintColorId };

		/// <summary>
		/// Recolored copies of emissive materials, by original material and target color, shared between pieces
		/// </summary>
		private static readonly Dictionary<long, Material> materials = new Dictionary<long, Material>();

		/// <summary>
		/// Recolored copies of emission textures, by original texture and target color
		/// </summary>
		private static readonly Dictionary<long, Texture2D> textures = new Dictionary<long, Texture2D>();

		/// <summary>
		/// Shifts a color to the target's hue, leaving grays unchanged
		/// </summary>
		/// <param name="color">The color, which may be HDR</param>
		/// <param name="target">The color to shift toward</param>
		/// <returns>The shifted color with the original alpha</returns>
		public static Color Shift(Color color, Color target)
		{
			Color.RGBToHSV(color, out float h, out float s, out float v);
			if (s < MinSaturation)
			{
				return(color);
			}
			Color.RGBToHSV(target, out float targetH, out float targetS, out float targetV);
			Color shifted = Color.HSVToRGB(targetH, s * targetS, v * targetV, true);
			shifted.a = color.a;
			return(shifted);
		}

		/// <summary>
		/// Replaces a glow color with the target color's hue, at a strength set by how bright the target is, for glows
		/// that are white or gray to begin with, like the stone portal's, which Shift would leave alone
		/// </summary>
		/// <remarks>
		/// The target's brightest channel, from 0 to F, is mapped onto 5 to F, so a dark target like #010 still glows
		/// like #050 rather than looking black, while #0F0 gets the glow's full strength. Black has no hue, so it glows gray
		/// </remarks>
		/// <param name="glow">The glow color, which may be HDR</param>
		/// <param name="target">The color to use</param>
		/// <returns>The new glow, with the glow's alpha</returns>
		public static Color Replace(Color glow, Color target)
		{
			float targetMax = target.maxColorComponent;
			Color hue = ((targetMax > 0f) ? target / targetMax : Color.white);
			float strength = MinReplaceStrength + (1f - MinReplaceStrength) * Mathf.Clamp01(targetMax);
			Color replaced = hue * (strength * glow.maxColorComponent);
			replaced.a = glow.a;
			return(replaced);
		}

		/// <summary>
		/// Shifts the colors or gradients of a particle color setting, whichever its mode uses
		/// </summary>
		/// <param name="value">The particle color setting, which is left unchanged</param>
		/// <param name="target">The color to shift toward</param>
		/// <returns>A new setting in the same mode with its own gradients</returns>
		public static ParticleSystem.MinMaxGradient Shift(ParticleSystem.MinMaxGradient value, Color target)
		{
			return(Map(value, (color) => Shift(color, target)));
		}

		/// <summary>
		/// Copies a particle color setting, including its gradients
		/// </summary>
		/// <remarks>
		/// The gradients read from a particle system can be the system's own, so one kept to restore later would
		/// change along with the system unless it is copied
		/// </remarks>
		/// <param name="value">The particle color setting</param>
		/// <returns>A new setting in the same mode with its own gradients</returns>
		public static ParticleSystem.MinMaxGradient Copy(ParticleSystem.MinMaxGradient value)
		{
			return(Map(value, (color) => color));
		}

		/// <summary>
		/// Builds a particle color setting in the same mode with every color passed through a function
		/// </summary>
		/// <param name="value">The particle color setting, which is left unchanged</param>
		/// <param name="map">Returns the new color for each color</param>
		/// <returns>A new setting with its own gradients</returns>
		private static ParticleSystem.MinMaxGradient Map(ParticleSystem.MinMaxGradient value, System.Func<Color, Color> map)
		{
			ParticleSystem.MinMaxGradient mapped;
			switch (value.mode)
			{
				case ParticleSystemGradientMode.Color:
					mapped = new ParticleSystem.MinMaxGradient(map(value.color));
					break;
				case ParticleSystemGradientMode.TwoColors:
					mapped = new ParticleSystem.MinMaxGradient(map(value.colorMin), map(value.colorMax));
					break;
				case ParticleSystemGradientMode.TwoGradients:
					mapped = new ParticleSystem.MinMaxGradient(Map(value.gradientMin, map), Map(value.gradientMax, map));
					break;
				default:
					mapped = new ParticleSystem.MinMaxGradient(Map(value.gradient, map));
					break;
			}
			mapped.mode = value.mode;
			return(mapped);
		}

		/// <summary>
		/// Builds a gradient with every color key passed through a function, keeping its alpha keys and blend mode
		/// </summary>
		/// <param name="gradient">The gradient, which is left unchanged</param>
		/// <param name="map">Returns the new color for each color key</param>
		/// <returns>A new gradient, or null when the gradient is null</returns>
		private static Gradient Map(Gradient gradient, System.Func<Color, Color> map)
		{
			if (gradient == null)
			{
				return(null);
			}
			GradientColorKey[] colorKeys = gradient.colorKeys;
			for (int i = 0; i < colorKeys.Length; i++)
			{
				colorKeys[i].color = map(colorKeys[i].color);
			}
			Gradient mapped = new Gradient();
			mapped.mode = gradient.mode;
			mapped.colorSpace = gradient.colorSpace;
			mapped.SetKeys(colorKeys, gradient.alphaKeys);
			return(mapped);
		}

		/// <summary>
		/// Whether a renderer's material should be swapped for a recolored copy
		/// </summary>
		/// <remarks>
		/// Particle materials, like portal flames, are recolored when they carry a color of their own, since they only
		/// ever draw glows and flames. Mesh materials are only recolored when they glow, so a copper sconce stays copper
		/// </remarks>
		/// <param name="material">The material</param>
		/// <param name="particles">Whether the material is on a particle renderer</param>
		/// <returns>True when the material should be recolored</returns>
		public static bool ShouldRecolor(Material material, bool particles)
		{
			if (material == null)
			{
				return(false);
			}
			if (!particles)
			{
				return(IsEmissive(material));
			}
			return(IsTinted(material, BaseColorId) || IsTinted(material, EmissionColorId) || IsTinted(material, TintColorId));
		}

		/// <summary>
		/// Whether a material has a color property that Shift would change
		/// </summary>
		/// <param name="material">The material</param>
		/// <param name="id">The color property</param>
		/// <returns>True when the property exists and its color isn't a gray</returns>
		private static bool IsTinted(Material material, int id)
		{
			if (!material.HasProperty(id))
			{
				return(false);
			}
			Color.RGBToHSV(material.GetColor(id), out float h, out float s, out float v);
			return(s >= MinSaturation && v > 0.01f);
		}

		/// <summary>
		/// Whether a material glows through its emission color, so it can be recolored
		/// </summary>
		/// <param name="material">The material</param>
		/// <returns>True when emission is on and its color isn't black</returns>
		public static bool IsEmissive(Material material)
		{
			if (material == null || !material.HasProperty(EmissionColorId) || !material.IsKeywordEnabled(EmissionKeyword))
			{
				return(false);
			}
			return(material.GetColor(EmissionColorId).maxColorComponent > 0.01f);
		}

		/// <summary>
		/// A copy of a material with its base, emission and tint colors and its emission texture shifted, made once per
		/// material and color
		/// </summary>
		/// <param name="material">The original material</param>
		/// <param name="target">The color to shift toward</param>
		/// <returns>The shared recolored copy</returns>
		public static Material GetMaterial(Material material, Color target)
		{
			long key = GetKey(material, target);
			if (materials.TryGetValue(key, out Material copy) && copy != null)
			{
				return(copy);
			}
			copy = new Material(material);
			copy.name = material.name + " (" + Plugin.PluginName + ")";
			// Glowing gems like the Dverger circlet's are tinted through their base color as well as their glow, and
			// particle materials through any of the three
			foreach (int id in ColorIds)
			{
				if (material.HasProperty(id))
				{
					copy.SetColor(id, Shift(material.GetColor(id), target));
				}
			}
			if (material.HasProperty(EmissionMapId))
			{
				Texture texture = material.GetTexture(EmissionMapId);
				if (texture != null)
				{
					copy.SetTexture(EmissionMapId, GetTexture(texture, target));
				}
			}
			materials[key] = copy;
			return(copy);
		}

		/// <summary>
		/// A copy of a texture with every pixel shifted, made once per texture and color
		/// </summary>
		/// <remarks>
		/// Game textures can't be read directly, so the texture is drawn into a render texture and read back, scaled
		/// down to at most MaxTextureSize on its longest side
		/// </remarks>
		/// <param name="texture">The original texture</param>
		/// <param name="target">The color to shift toward</param>
		/// <returns>The shared recolored copy</returns>
		private static Texture2D GetTexture(Texture texture, Color target)
		{
			long key = GetKey(texture, target);
			if (textures.TryGetValue(key, out Texture2D copy) && copy != null)
			{
				return(copy);
			}
			int width = texture.width;
			int height = texture.height;
			while (width > MaxTextureSize || height > MaxTextureSize)
			{
				width = Mathf.Max(1, width / 2);
				height = Mathf.Max(1, height / 2);
			}
			RenderTexture render = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
			RenderTexture previous = RenderTexture.active;
			Graphics.Blit(texture, render);
			RenderTexture.active = render;
			copy = new Texture2D(width, height, TextureFormat.RGBA32, true);
			copy.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
			RenderTexture.active = previous;
			RenderTexture.ReleaseTemporary(render);
			Color32[] pixels = copy.GetPixels32();
			for (int i = 0; i < pixels.Length; i++)
			{
				pixels[i] = Shift(pixels[i], target);
			}
			copy.SetPixels32(pixels);
			copy.wrapMode = texture.wrapMode;
			copy.filterMode = texture.filterMode;
			copy.name = texture.name + " (" + Plugin.PluginName + ")";
			copy.Apply(true, true);
			textures[key] = copy;
			return(copy);
		}

		/// <summary>
		/// A cache key combining an object and a color
		/// </summary>
		/// <param name="obj">The material or texture</param>
		/// <param name="target">The target color</param>
		/// <returns>The object's instance ID in the high half and the color's RGBA bytes in the low half</returns>
		private static long GetKey(Object obj, Color target)
		{
			Color32 color = target;
			uint packed = ((uint)color.r << 24) | ((uint)color.g << 16) | ((uint)color.b << 8) | color.a;
			return(((long)obj.GetInstanceID() << 32) | packed);
		}
	}
}
