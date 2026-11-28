using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// Turns an effect the bright white of spirit - its particles, materials, textures and lights - for the spirit
	/// spells Zombie Apocalypse's dvergr mages cast and the spirit nova Viking Villages' valkyries banish the undead with
	/// </summary>
	internal static class SpiritWhite
	{
		private static readonly int ColorId = Shader.PropertyToID("_Color");
		private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
		private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
		private static readonly int EmisColorId = Shader.PropertyToID("_EmisColor");
		private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

		/// <summary>
		/// The gray copies of effects' textures, by the texture they're copied from
		/// </summary>
		private static readonly Dictionary<Texture, Texture2D> grayTextures = new Dictionary<Texture, Texture2D>();

		/// <summary>
		/// Turns every particle, material, texture and light in an effect white at the same brightness - its textures
		/// gray, since some are colored themselves - with its glow and lights brighter by a factor
		/// </summary>
		/// <param name="effect">The effect</param>
		/// <param name="glowBoost">How much brighter its glow and lights are made</param>
		public static void Whiten(GameObject effect, float glowBoost)
		{
			foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
			{
				ParticleSystem.MainModule main = particles.main;
				main.startColor = Whiten(main.startColor);
				ParticleSystem.ColorOverLifetimeModule overLifetime = particles.colorOverLifetime;
				overLifetime.color = Whiten(overLifetime.color);
			}
			Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
			foreach (Renderer renderer in effect.GetComponentsInChildren<Renderer>(true))
			{
				Material[] materials = renderer.sharedMaterials;
				for (int i = 0; i < materials.Length; i++)
				{
					if (materials[i] == null)
					{
						continue;
					}
					if (!copies.TryGetValue(materials[i], out Material copy))
					{
						copy = WhiteMaterial(materials[i], glowBoost);
						copies[materials[i]] = copy;
					}
					materials[i] = copy;
				}
				renderer.sharedMaterials = materials;
			}
			foreach (Light light in effect.GetComponentsInChildren<Light>(true))
			{
				light.color = Color.white;
				light.intensity *= glowBoost;
			}
		}

		/// <summary>
		/// Copies a texture through the GPU into one whose pixels can be read and changed, since the game's textures
		/// can't be read directly
		/// </summary>
		/// <param name="source">The texture to copy</param>
		/// <returns>The copy, with mipmaps for when it's applied</returns>
		public static Texture2D ReadableCopy(Texture2D source)
		{
			int width = source.width;
			int height = source.height;
			RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
			Graphics.Blit(source, target);
			RenderTexture previous = RenderTexture.active;
			RenderTexture.active = target;
			Texture2D copy = new Texture2D(width, height, TextureFormat.RGBA32, true, false);
			copy.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
			RenderTexture.active = previous;
			RenderTexture.ReleaseTemporary(target);
			copy.filterMode = source.filterMode;
			copy.wrapMode = source.wrapMode;
			copy.anisoLevel = source.anisoLevel;
			return(copy);
		}

		/// <summary>
		/// Copies a material with its colors turned white, its glow brighter by a factor, and its texture gray
		/// </summary>
		/// <param name="source">The material</param>
		/// <param name="glowBoost">How much brighter its glow is made</param>
		/// <returns>The copy</returns>
		private static Material WhiteMaterial(Material source, float glowBoost)
		{
			Material copy = new Material(source);
			copy.name = source.name + " (spirit)";
			WhitenProperty(copy, ColorId, 1f);
			WhitenProperty(copy, TintColorId, 1f);
			WhitenProperty(copy, EmissionColorId, glowBoost);
			WhitenProperty(copy, EmisColorId, glowBoost);
			Texture texture = (copy.HasProperty(MainTexId) ? copy.GetTexture(MainTexId) : null);
			if (texture is Texture2D)
			{
				copy.SetTexture(MainTexId, GrayTexture((Texture2D)texture));
			}
			return(copy);
		}

		/// <summary>
		/// Gets a copy of a texture with each pixel turned gray at the brightness of its brightest channel, making it the
		/// first time
		/// </summary>
		/// <param name="source">The texture</param>
		/// <returns>The gray copy</returns>
		private static Texture2D GrayTexture(Texture2D source)
		{
			if (grayTextures.TryGetValue(source, out Texture2D gray))
			{
				return(gray);
			}
			gray = ReadableCopy(source);
			Color32[] pixels = gray.GetPixels32();
			for (int i = 0; i < pixels.Length; i++)
			{
				Color32 pixel = pixels[i];
				byte brightness = Math.Max(pixel.r, Math.Max(pixel.g, pixel.b));
				pixels[i] = new Color32(brightness, brightness, brightness, pixel.a);
			}
			gray.SetPixels32(pixels);
			gray.Apply(true, true);
			gray.name = source.name + " (spirit)";
			grayTextures[source] = gray;
			return(gray);
		}

		/// <summary>
		/// Turns a material's color property white at the same brightness, when it has one
		/// </summary>
		/// <param name="material">The material</param>
		/// <param name="id">The property</param>
		/// <param name="boost">How much brighter to make it</param>
		private static void WhitenProperty(Material material, int id, float boost)
		{
			if (material.HasProperty(id))
			{
				material.SetColor(id, Whiten(material.GetColor(id), boost));
			}
		}

		/// <summary>
		/// Turns a color white at the brightness of its brightest channel, keeping its alpha
		/// </summary>
		/// <param name="color">The color</param>
		/// <param name="boost">How much brighter to make it</param>
		/// <returns>The white color</returns>
		internal static Color Whiten(Color color, float boost = 1f)
		{
			float brightness = Mathf.Max(color.r, Mathf.Max(color.g, color.b)) * boost;
			return(new Color(brightness, brightness, brightness, color.a));
		}

		/// <summary>
		/// Turns a particle color setting white, whether it's a color, a gradient or a pair of either
		/// </summary>
		/// <param name="value">The setting</param>
		/// <returns>The white setting</returns>
		private static ParticleSystem.MinMaxGradient Whiten(ParticleSystem.MinMaxGradient value)
		{
			switch (value.mode)
			{
				case ParticleSystemGradientMode.Color:
					return(new ParticleSystem.MinMaxGradient(Whiten(value.color)));
				case ParticleSystemGradientMode.TwoColors:
					return(new ParticleSystem.MinMaxGradient(Whiten(value.colorMin), Whiten(value.colorMax)));
				case ParticleSystemGradientMode.Gradient:
					return(new ParticleSystem.MinMaxGradient(Whiten(value.gradient)));
				case ParticleSystemGradientMode.TwoGradients:
					return(new ParticleSystem.MinMaxGradient(Whiten(value.gradientMin), Whiten(value.gradientMax)));
				default:
					return(value);
			}
		}

		/// <summary>
		/// Copies a gradient with its colors turned white, keeping its alpha
		/// </summary>
		/// <param name="gradient">The gradient, or null</param>
		/// <returns>The white gradient, or null</returns>
		private static Gradient Whiten(Gradient gradient)
		{
			if (gradient == null)
			{
				return(null);
			}
			GradientColorKey[] keys = gradient.colorKeys;
			for (int i = 0; i < keys.Length; i++)
			{
				keys[i].color = Whiten(keys[i].color);
			}
			Gradient white = new Gradient();
			white.mode = gradient.mode;
			white.SetKeys(keys, gradient.alphaKeys);
			return(white);
		}
	}
}
