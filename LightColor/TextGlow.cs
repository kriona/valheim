using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LightColor
{
	/// <summary>
	/// Builds copies of a sign's text material that glow, with a halo, lit letters or an outline in a color
	/// </summary>
	/// <remarks>
	/// Sign text uses TextMeshPro's surface shader, which is lit by the scene, so its letters go dark at night, and the
	/// game was built without that shader's glow. The copies use TextMeshPro's unlit shader instead, which draws the
	/// letters at their own color whatever the lighting, and has an outline and an underlay - a soft copy of the letters
	/// behind them, which makes the halo
	/// </remarks>
	internal static class TextGlow
	{
		private const string ShaderName = "TextMeshPro/Distance Field";
		private const string UnderlayKeyword = "UNDERLAY_ON";

		/// <summary>
		/// How far the halo spreads past the letters and how soft its edge is, from 0 to 1
		/// </summary>
		private const float HaloDilate = 0.3f;
		private const float HaloSoftness = 0.6f;

		/// <summary>
		/// How thick the outline is, from 0 to 1 - the letters are grown by the same amount so the outline sits
		/// outside them rather than eating into them
		/// </summary>
		private const float OutlineWidth = 0.125f;

		/// <summary>
		/// What a typed brightness is multiplied by, so 100% glows softly enough to read the letters
		/// </summary>
		private const float BrightnessScale = 0.6f;

		private static readonly int FaceColorId = Shader.PropertyToID("_FaceColor");
		private static readonly int FaceDilateId = Shader.PropertyToID("_FaceDilate");
		private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
		private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
		private static readonly int OutlineSoftnessId = Shader.PropertyToID("_OutlineSoftness");
		private static readonly int UnderlayColorId = Shader.PropertyToID("_UnderlayColor");
		private static readonly int UnderlayDilateId = Shader.PropertyToID("_UnderlayDilate");
		private static readonly int UnderlaySoftnessId = Shader.PropertyToID("_UnderlaySoftness");
		private static readonly int UnderlayOffsetXId = Shader.PropertyToID("_UnderlayOffsetX");
		private static readonly int UnderlayOffsetYId = Shader.PropertyToID("_UnderlayOffsetY");

		/// <summary>
		/// Glowing copies of text materials, by original material, styles, color and brightness, shared between signs
		/// </summary>
		private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

		private static Shader shader;
		private static bool searched;

		/// <summary>
		/// The styles a sign shows for its settings - a halo when none were typed
		/// </summary>
		/// <param name="settings">The sign's settings</param>
		/// <returns>The styles to show</returns>
		public static TextStyle GetStyles(LightSettings settings)
		{
			return(((settings.Styles == TextStyle.None) ? TextStyle.Halo : settings.Styles));
		}

		/// <summary>
		/// The color a sign glows in for its settings - white when no color was typed
		/// </summary>
		/// <param name="settings">The sign's settings</param>
		/// <returns>The opaque color</returns>
		public static Color GetColor(LightSettings settings)
		{
			return((settings.HasColor ? settings.Color : Color.white));
		}

		/// <summary>
		/// A copy of a text material that glows the way the settings ask, made once for each material and settings
		/// </summary>
		/// <param name="material">The text's own material</param>
		/// <param name="settings">The sign's settings</param>
		/// <returns>The glowing copy, or null when the unlit shader can't be found</returns>
		public static Material GetMaterial(Material material, LightSettings settings)
		{
			Shader unlit = FindShader();
			if (unlit == null)
			{
				return(null);
			}
			TextStyle styles = GetStyles(settings);
			Color color = GetColor(settings);
			string key = material.GetInstanceID() + " " + (int)styles + " " + ColorUtility.ToHtmlStringRGB(color) + " " + settings.Brightness;
			if (materials.TryGetValue(key, out Material copy) && copy != null)
			{
				return(copy);
			}
			// Copying keeps the font atlas and its distance field scale, which the unlit shader reads by the same names
			copy = new Material(material);
			copy.name = material.name + " (" + Plugin.PluginName + ")";
			copy.shader = unlit;
			float brightness = settings.Brightness * BrightnessScale;
			Color glow = color * brightness;
			glow.a = 1f;
			// The letters' own color comes from the text, which this multiplies - lit letters take the glow color
			// through the text, so color tags typed on the sign still show
			Color face = (((styles & TextStyle.Lit) != 0) ? Color.white * brightness : Color.white);
			face.a = 1f;
			copy.SetColor(FaceColorId, face);
			bool outline = ((styles & TextStyle.Outline) != 0);
			copy.SetColor(OutlineColorId, glow);
			copy.SetFloat(OutlineWidthId, (outline ? OutlineWidth : 0f));
			copy.SetFloat(OutlineSoftnessId, 0f);
			copy.SetFloat(FaceDilateId, (outline ? OutlineWidth : 0f));
			if ((styles & TextStyle.Halo) != 0)
			{
				copy.EnableKeyword(UnderlayKeyword);
				copy.SetColor(UnderlayColorId, glow);
				copy.SetFloat(UnderlayDilateId, HaloDilate);
				copy.SetFloat(UnderlaySoftnessId, HaloSoftness);
				copy.SetFloat(UnderlayOffsetXId, 0f);
				copy.SetFloat(UnderlayOffsetYId, 0f);
			}
			else
			{
				copy.DisableKeyword(UnderlayKeyword);
			}
			// The shader scales the outline and halo down to fit the room the font atlas leaves around each letter
			ShaderUtilities.UpdateShaderRatios(copy);
			materials[key] = copy;
			return(copy);
		}

		/// <summary>
		/// Finds TextMeshPro's unlit shader, which the game keeps in its resources, logging once when it's missing
		/// </summary>
		/// <returns>The shader, or null when it can't be found</returns>
		private static Shader FindShader()
		{
			if (searched)
			{
				return(shader);
			}
			searched = true;
			shader = Shader.Find(ShaderName);
			if (shader == null)
			{
				foreach (Shader loaded in Resources.FindObjectsOfTypeAll<Shader>())
				{
					if (loaded.name == ShaderName)
					{
						shader = loaded;
						break;
					}
				}
			}
			if (shader == null)
			{
				Debug.LogWarning(Plugin.PluginName + ": couldn't find the " + ShaderName + " shader, so sign text can't glow");
			}
			return(shader);
		}
	}
}
