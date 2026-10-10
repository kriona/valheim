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
		private readonly float[] originalIntensities;
		private readonly float[] originalRanges;

		/// <summary>
		/// The range of the largest light, which a typed range is measured against
		/// </summary>
		private readonly float largestRange;
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
			originalIntensities = new float[lights.Length];
			originalRanges = new float[lights.Length];
			for (int i = 0; i < lights.Length; i++)
			{
				originalColors[i] = lights[i].color;
				originalIntensities[i] = GetBaseIntensity(lights[i]);
				originalRanges[i] = GetBaseRange(lights[i]);
				largestRange = Mathf.Max(largestRange, originalRanges[i]);
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
		/// Sets everything recorded to the settings, putting back anything the settings leave out
		/// </summary>
		/// <remarks>
		/// Lights get the exact color, brightness and range, while particles and materials are only shifted toward the
		/// color so they keep their own shading. A range sets the largest light and scales the others to match, so a
		/// fire's low-fuel light stays smaller
		/// </remarks>
		/// <param name="settings">The settings to show</param>
		public void Apply(LightSettings settings)
		{
			bool hasColor = settings.HasColor;
			Color color = settings.Color;
			float rangeScale = ((settings.Range > 0f && largestRange > 0f) ? settings.Range / largestRange : 1f);
			for (int i = 0; i < lights.Length; i++)
			{
				if (lights[i] != null)
				{
					lights[i].color = (hasColor ? color : originalColors[i]);
					SetIntensity(lights[i], originalIntensities[i] * settings.Brightness);
					SetRange(lights[i], originalRanges[i] * rangeScale);
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
		/// A light's brightness before flickering or fading
		/// </summary>
		/// <remarks>
		/// A flickering or fading light sets its brightness every frame from the base it read when it woke, and has
		/// dimmed the light itself - a fade starts it at 0, and a flicker does for the reduced flashing setting - so the
		/// base is read once it has woken. Lights on inactive objects haven't woken yet
		/// </remarks>
		/// <param name="light">The light</param>
		/// <returns>The base brightness</returns>
		private static float GetBaseIntensity(Light light)
		{
			LightFlicker flicker = light.GetComponent<LightFlicker>();
			if (flicker != null && flicker.m_light != null)
			{
				return(flicker.m_baseIntensity);
			}
			EffectFade fade = GetFade(light);
			if (fade != null)
			{
				return(fade.m_lightBaseIntensity);
			}
			return(light.intensity);
		}

		/// <summary>
		/// The woken fade that sets a light's brightness every frame, like the one on a portal's glow
		/// </summary>
		/// <remarks>
		/// A fade sits on a parent of its light and reads the light's brightness as its base when it wakes, the same way a flicker does
		/// </remarks>
		/// <param name="light">The light</param>
		/// <returns>The fade, or null when the light has none or it hasn't woken</returns>
		private static EffectFade GetFade(Light light)
		{
			EffectFade fade = light.GetComponentInParent<EffectFade>(true);
			return(((fade != null && fade.m_light == light) ? fade : null));
		}

		/// <summary>
		/// A light's full range
		/// </summary>
		/// <remarks>
		/// A light with distance fading starts at range 0 when it wakes and grows back to the base range it read, so the
		/// base is read once it has woken
		/// </remarks>
		/// <param name="light">The light</param>
		/// <returns>The full range</returns>
		private static float GetBaseRange(Light light)
		{
			LightLod lod = light.GetComponent<LightLod>();
			return(((lod != null && lod.m_light != null) ? lod.m_baseRange : light.range));
		}

		/// <summary>
		/// Sets a light's brightness, through its flicker or fade when it has one so they don't undo it
		/// </summary>
		/// <param name="light">The light</param>
		/// <param name="intensity">The brightness before flickering</param>
		private static void SetIntensity(Light light, float intensity)
		{
			LightFlicker flicker = light.GetComponent<LightFlicker>();
			if (flicker != null && flicker.m_light != null)
			{
				flicker.m_baseIntensity = intensity;
				return;
			}
			EffectFade fade = GetFade(light);
			if (fade != null)
			{
				fade.m_lightBaseIntensity = intensity;
				return;
			}
			// A flicker or fade that hasn't woken reads this as its base when it does
			light.intensity = intensity;
		}

		/// <summary>
		/// Sets a light's range, through its distance fading when it has it so the fading grows the light to the new range
		/// </summary>
		/// <param name="light">The light</param>
		/// <param name="range">The full range</param>
		private static void SetRange(Light light, float range)
		{
			LightLod lod = light.GetComponent<LightLod>();
			if (lod != null && lod.m_light != null)
			{
				lod.m_baseRange = range;
				// The fading only grows a light that is on toward its base range and never shrinks it, so a light
				// that is on gets the new range straight away
				if (light.enabled && light.range > 0f)
				{
					light.range = range;
				}
				return;
			}
			// Distance fading that hasn't woken reads this as its base when it does
			light.range = range;
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
