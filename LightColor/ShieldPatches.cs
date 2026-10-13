using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace LightColor
{
	/// <summary>
	/// Colors a shield generator's lights, energy particles and glow, which the generator sets itself several times a
	/// second from a color that shows how much fuel it has
	/// </summary>
	[HarmonyPatch(typeof(ShieldGenerator), nameof(ShieldGenerator.UpdateShield))]
	public static class ShieldGeneratorUpdateShieldPatch
	{
		private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

		/// <summary>
		/// Gives the lights the stored color, shifts the particles and glow the generator just set toward it so they
		/// keep the fuel level's shading, and scales the glow by the stored brightness, leaving the generator's own
		/// colors when there are no stored settings
		/// </summary>
		/// <param name="__instance">The shield generator</param>
		private static void Postfix(ShieldGenerator __instance)
		{
			if (!__instance.m_nview.IsValid() || __instance.m_propertyBlock == null)
			{
				return;
			}
			LightTint tint = __instance.GetComponent<LightTint>();
			if (tint == null || (!tint.Settings.HasColor && tint.Settings.Brightness == 1f))
			{
				return;
			}
			Color domeColor = ShieldDomeImageEffect.GetDomeColor(__instance.m_lastFuel);
			if (tint.Settings.HasColor)
			{
				Color color = tint.Settings.Color;
				foreach (Light light in __instance.m_coloredLights)
				{
					light.color = color;
				}
				foreach (ParticleSystem system in __instance.m_energyParticles)
				{
					ParticleSystem.MainModule main = system.main;
					main.startColor = Recolor.Shift(domeColor, color);
				}
				if (__instance.m_energyParticlesFlare != null)
				{
					Color flare = domeColor * Mathf.Pow(__instance.m_lastFuel, 0.5f);
					__instance.m_energyParticlesFlare.customData.SetColor(ParticleSystemCustomData.Custom1, Recolor.Shift(flare, color));
				}
			}
			Color glow = domeColor * 2f;
			Color emission = (tint.Settings.HasColor ? Recolor.Shift(glow, tint.Settings.Color) : glow);
			float alpha = emission.a;
			emission *= tint.Settings.Brightness;
			emission.a = alpha;
			__instance.m_propertyBlock.SetColor(EmissionColorId, emission);
			foreach (MeshRenderer renderer in __instance.m_meshRenderers)
			{
				renderer.SetPropertyBlock(__instance.m_propertyBlock);
			}
		}
	}

	/// <summary>
	/// Draws the shield domes of colored shield generators in their colors
	/// </summary>
	/// <remarks>
	/// Every dome is drawn in one pass over the screen, taking its color from a single gradient by fuel level, so
	/// the domes are drawn in a pass per color instead, each with only its own domes and a copy of the gradient
	/// shifted to its color. Domes of different colors that overlap are drawn over each other rather than blended
	/// into one shape
	/// </remarks>
	[HarmonyPatch(typeof(ShieldDomeImageEffect), nameof(ShieldDomeImageEffect.OnRenderImage))]
	public static class ShieldDomeRenderPatch
	{
		/// <summary>
		/// Set while the passes run so they draw with the game's own render
		/// </summary>
		private static bool rendering;

		/// <summary>
		/// Shifted copies of the dome gradient, by original gradient and color
		/// </summary>
		private static readonly Dictionary<long, Texture2D> gradients = new Dictionary<long, Texture2D>();

		/// <summary>
		/// The dome buffer of each pass, kept so a pass doesn't make a new one every frame
		/// </summary>
		private static readonly List<ComputeBuffer> buffers = new List<ComputeBuffer>();

		/// <summary>
		/// Draws the domes in a pass per color when any shield generator has a stored color
		/// </summary>
		/// <param name="__instance">The camera's dome effect</param>
		/// <param name="src">The image to draw over</param>
		/// <param name="dest">Where to draw the result</param>
		/// <returns>False to skip the game's single pass when the passes run</returns>
		private static bool Prefix(ShieldDomeImageEffect __instance, RenderTexture src, RenderTexture dest)
		{
			if (rendering || __instance.m_shieldDomes == null || __instance.m_shieldDomes.Length == 0 || __instance.m_gradientTex == null)
			{
				return(true);
			}
			List<ShieldDomeImageEffect.ShieldDome> plain = new List<ShieldDomeImageEffect.ShieldDome>();
			Dictionary<long, List<ShieldDomeImageEffect.ShieldDome>> colored = new Dictionary<long, List<ShieldDomeImageEffect.ShieldDome>>();
			Dictionary<long, Texture2D> passGradients = new Dictionary<long, Texture2D>();
			foreach (KeyValuePair<ShieldGenerator, ShieldDomeImageEffect.ShieldDome> entry in __instance.m_shieldDomeData)
			{
				LightTint tint = ((entry.Key != null) ? entry.Key.GetComponent<LightTint>() : null);
				if (tint == null || !tint.Settings.HasColor)
				{
					plain.Add(entry.Value);
					continue;
				}
				Color color = tint.Settings.Color;
				long key = Recolor.GetKey(__instance.m_gradientTex, color);
				if (!colored.TryGetValue(key, out List<ShieldDomeImageEffect.ShieldDome> domes))
				{
					domes = new List<ShieldDomeImageEffect.ShieldDome>();
					colored[key] = domes;
					passGradients[key] = GetGradient(__instance.m_gradientTex, color, key);
				}
				domes.Add(entry.Value);
			}
			if (colored.Count == 0)
			{
				return(true);
			}
			List<ShieldDomeImageEffect.ShieldDome[]> passDomes = new List<ShieldDomeImageEffect.ShieldDome[]>();
			List<Texture2D> passTextures = new List<Texture2D>();
			if (plain.Count > 0)
			{
				passDomes.Add(plain.ToArray());
				passTextures.Add(__instance.m_gradientTex);
			}
			foreach (KeyValuePair<long, List<ShieldDomeImageEffect.ShieldDome>> entry in colored)
			{
				passDomes.Add(entry.Value.ToArray());
				passTextures.Add(passGradients[entry.Key]);
			}
			ShieldDomeImageEffect.ShieldDome[] allDomes = __instance.m_shieldDomes;
			Texture2D gradient = __instance.m_gradientTex;
			ComputeBuffer buffer = __instance.m_shieldDomeBuffer;
			RenderTexture source = src;
			rendering = true;
			try
			{
				for (int i = 0; i < passDomes.Count; i++)
				{
					RenderTexture target = ((i == passDomes.Count - 1) ? dest : RenderTexture.GetTemporary(src.descriptor));
					if (buffers.Count <= i)
					{
						buffers.Add(null);
					}
					__instance.m_shieldDomes = passDomes[i];
					__instance.m_gradientTex = passTextures[i];
					__instance.m_shieldDomeBuffer = buffers[i];
					__instance.OnRenderImage(source, target);
					// The render makes a new buffer when the pass's dome count changes
					buffers[i] = __instance.m_shieldDomeBuffer;
					if (source != src)
					{
						RenderTexture.ReleaseTemporary(source);
					}
					source = target;
				}
			}
			finally
			{
				__instance.m_shieldDomes = allDomes;
				__instance.m_gradientTex = gradient;
				__instance.m_shieldDomeBuffer = buffer;
				rendering = false;
			}
			return(false);
		}

		/// <summary>
		/// A copy of the dome gradient with every color shifted toward a color, made once per gradient and color
		/// </summary>
		/// <param name="original">The game's dome gradient</param>
		/// <param name="color">The color to shift toward</param>
		/// <param name="key">The cache key for the gradient and color</param>
		/// <returns>The shared shifted copy</returns>
		private static Texture2D GetGradient(Texture2D original, Color color, long key)
		{
			if (gradients.TryGetValue(key, out Texture2D copy) && copy != null)
			{
				return(copy);
			}
			copy = new Texture2D(original.width, original.height, TextureFormat.RGB24, false);
			copy.name = original.name + " (" + Plugin.PluginName + ")";
			copy.wrapMode = original.wrapMode;
			Color[] pixels = original.GetPixels();
			for (int i = 0; i < pixels.Length; i++)
			{
				pixels[i] = Recolor.Shift(pixels[i], color);
			}
			copy.SetPixels(pixels);
			copy.Apply();
			gradients[key] = copy;
			return(copy);
		}
	}
}
