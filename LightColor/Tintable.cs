using System.Collections.Generic;
using UnityEngine;

namespace LightColor
{
	/// <summary>
	/// The lights, particle systems and glowing materials under an object, with their own colors, so they can be set
	/// to a color and put back
	/// </summary>
	public class Tintable
	{
		/// <summary>
		/// A particle system's own color settings
		/// </summary>
		private class ParticleColors
		{
			public ParticleSystem system;
			public ParticleSystem.MinMaxGradient startColor;
			public ParticleSystem.MinMaxGradient lifetimeColor;
			public bool hasCustom1;
			public ParticleSystem.MinMaxGradient custom1;
			public bool hasCustom2;
			public ParticleSystem.MinMaxGradient custom2;
		}

		/// <summary>
		/// A renderer with at least one material to recolor, and its own materials
		/// </summary>
		private class RendererMaterials
		{
			public Renderer renderer;
			public Material[] materials;
			public bool particles;
		}

		private readonly Light[] lights;
		private readonly Color[] originalColors;
		private readonly List<ParticleColors> particles = new List<ParticleColors>();
		private readonly List<RendererMaterials> renderers = new List<RendererMaterials>();

		/// <summary>
		/// Records the lights, particle systems and recolorable renderers under an object, including inactive ones,
		/// with their own colors
		/// </summary>
		/// <param name="root">The object to search</param>
		public Tintable(GameObject root)
		{
			lights = root.GetComponentsInChildren<Light>(true);
			originalColors = new Color[lights.Length];
			for (int i = 0; i < lights.Length; i++)
			{
				originalColors[i] = lights[i].color;
			}
			foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
			{
				ParticleColors colors = new ParticleColors();
				colors.system = system;
				colors.startColor = Recolor.Copy(system.main.startColor);
				colors.lifetimeColor = Recolor.Copy(system.colorOverLifetime.color);
				ParticleSystem.CustomDataModule custom = system.customData;
				colors.hasCustom1 = (custom.enabled && custom.GetMode(ParticleSystemCustomData.Custom1) == ParticleSystemCustomDataMode.Color);
				colors.hasCustom2 = (custom.enabled && custom.GetMode(ParticleSystemCustomData.Custom2) == ParticleSystemCustomDataMode.Color);
				if (colors.hasCustom1)
				{
					colors.custom1 = Recolor.Copy(custom.GetColor(ParticleSystemCustomData.Custom1));
				}
				if (colors.hasCustom2)
				{
					colors.custom2 = Recolor.Copy(custom.GetColor(ParticleSystemCustomData.Custom2));
				}
				particles.Add(colors);
			}
			foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
			{
				bool isParticles = (renderer is ParticleSystemRenderer);
				if (!isParticles && !(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
				{
					continue;
				}
				Material[] materials = renderer.sharedMaterials;
				foreach (Material material in materials)
				{
					if (Recolor.ShouldRecolor(material, isParticles))
					{
						RendererMaterials entry = new RendererMaterials();
						entry.renderer = renderer;
						entry.materials = materials;
						entry.particles = isParticles;
						renderers.Add(entry);
						break;
					}
				}
			}
		}

		/// <summary>
		/// Whether anything recorded is left to color - a light, a particle system or a recolorable material
		/// </summary>
		/// <remarks>
		/// Checks for destroyed objects, since an item stand destroys its shown item when the item is taken off
		/// </remarks>
		public bool HasEffects
		{
			get
			{
				foreach (Light light in lights)
				{
					if (light != null)
					{
						return(true);
					}
				}
				foreach (ParticleColors colors in particles)
				{
					if (colors.system != null)
					{
						return(true);
					}
				}
				foreach (RendererMaterials entry in renderers)
				{
					if (entry.renderer != null)
					{
						return(true);
					}
				}
				return(false);
			}
		}

		/// <summary>
		/// Sets everything recorded to the color, or back to its own colors
		/// </summary>
		/// <remarks>
		/// Lights get the exact color, while particles and materials are shifted toward it so they keep their own shading
		/// </remarks>
		/// <param name="hasColor">Whether to use the color rather than restore the own colors</param>
		/// <param name="color">The opaque color</param>
		public void Apply(bool hasColor, Color color)
		{
			for (int i = 0; i < lights.Length; i++)
			{
				if (lights[i] != null)
				{
					lights[i].color = (hasColor ? color : originalColors[i]);
				}
			}
			foreach (ParticleColors colors in particles)
			{
				ApplyParticles(colors, hasColor, color);
			}
			foreach (RendererMaterials entry in renderers)
			{
				ApplyMaterials(entry, hasColor, color);
			}
		}

		/// <summary>
		/// Sets a particle system's colors shifted toward the color, or back to its own colors
		/// </summary>
		/// <param name="colors">The particle system and its own colors</param>
		/// <param name="hasColor">Whether to shift the colors rather than restore them</param>
		/// <param name="color">The color to shift toward</param>
		private static void ApplyParticles(ParticleColors colors, bool hasColor, Color color)
		{
			if (colors.system == null)
			{
				return;
			}
			ParticleSystem.MainModule main = colors.system.main;
			// The kept colors are copied again on the way back so the system never holds the copies kept here
			ParticleSystem.MinMaxGradient startColor = (hasColor ? Recolor.Shift(colors.startColor, color) : Recolor.Copy(colors.startColor));
			main.startColor = startColor;
			SetLiveStartColors(colors.system, startColor);
			ParticleSystem.ColorOverLifetimeModule lifetime = colors.system.colorOverLifetime;
			lifetime.color = (hasColor ? Recolor.Shift(colors.lifetimeColor, color) : Recolor.Copy(colors.lifetimeColor));
			ParticleSystem.CustomDataModule custom = colors.system.customData;
			if (colors.hasCustom1)
			{
				custom.SetColor(ParticleSystemCustomData.Custom1, (hasColor ? Recolor.Shift(colors.custom1, color) : Recolor.Copy(colors.custom1)));
			}
			if (colors.hasCustom2)
			{
				custom.SetColor(ParticleSystemCustomData.Custom2, (hasColor ? Recolor.Shift(colors.custom2, color) : Recolor.Copy(colors.custom2)));
			}
		}

		/// <summary>
		/// Gives the particles already alive a start color from the new setting
		/// </summary>
		/// <remarks>
		/// A particle keeps the start color it was emitted with for its whole life, so long-lived ones, like the soft
		/// glow around a sconce, would keep the old color until they die. Settings that pick between two colors or along
		/// a gradient use each particle's random seed to pick its point
		/// </remarks>
		/// <param name="system">The particle system</param>
		/// <param name="startColor">The system's new start color setting</param>
		private static void SetLiveStartColors(ParticleSystem system, ParticleSystem.MinMaxGradient startColor)
		{
			int count = system.particleCount;
			if (count == 0)
			{
				return;
			}
			ParticleSystem.Particle[] live = new ParticleSystem.Particle[count];
			count = system.GetParticles(live);
			for (int i = 0; i < count; i++)
			{
				float pick = (live[i].randomSeed % 1000) / 1000f;
				live[i].startColor = startColor.Evaluate(pick, pick);
			}
			system.SetParticles(live, count);
		}

		/// <summary>
		/// Swaps a renderer's recolorable materials for copies recolored toward the color, or back to its own materials
		/// </summary>
		/// <param name="entry">The renderer and its own materials</param>
		/// <param name="hasColor">Whether to recolor the materials rather than restore them</param>
		/// <param name="color">The color to shift toward</param>
		private static void ApplyMaterials(RendererMaterials entry, bool hasColor, Color color)
		{
			if (entry.renderer == null)
			{
				return;
			}
			if (!hasColor)
			{
				entry.renderer.sharedMaterials = entry.materials;
				return;
			}
			Material[] materials = new Material[entry.materials.Length];
			for (int i = 0; i < materials.Length; i++)
			{
				Material material = entry.materials[i];
				materials[i] = (Recolor.ShouldRecolor(material, entry.particles) ? Recolor.GetMaterial(material, color) : material);
			}
			entry.renderer.sharedMaterials = materials;
		}
	}
}
