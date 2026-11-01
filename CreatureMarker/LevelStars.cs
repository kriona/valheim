using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CreatureMarker
{
	/// <summary>
	/// The text for one of a creature's stars in a marker label - the enemy health bar's own star when its sprites can
	/// be used, or else a star character the label font can draw
	/// </summary>
	/// <remarks>
	/// The health bar draws each star as a black outline sprite with a smaller fill sprite on top. A sprite asset holding
	/// both lets the label draw them inline, with the outline taking up no room so the fill lands on top of it, and the
	/// fill tinted in the marker color
	/// </remarks>
	internal class LevelStars
	{
		private const string OutlinePath = "level_2/star";
		private const string FillName = "star (1)";
		private const float OutlineSize = 15f;
		private const float FillSize = 12f;
		private const float StarAdvance = 16f;

		/// <summary>
		/// Star characters to try when the sprites can't be used, in order, using the first one the font can draw
		/// </summary>
		private static readonly char[] StarCandidates = { '⭐', '★', '*' };

		private Material material;

		/// <summary>
		/// The outline's sprite tag in the health bar's outline color, or null when the sprites aren't used
		/// </summary>
		private string outlineTag;

		/// <summary>
		/// The star character used when the sprites aren't, or null until Create has run
		/// </summary>
		private string character;

		/// <summary>
		/// The sprite asset holding the star sprites, or null when the label falls back to a star character
		/// </summary>
		public TMP_SpriteAsset Asset { get; private set; }

		/// <summary>
		/// Builds the star sprite asset from the enemy HUD's star, or picks a star character when that fails, the first
		/// time it is called
		/// </summary>
		/// <param name="enemyHud">The enemy HUD whose health bar template holds the star</param>
		/// <param name="font">The label font, for lining the star up with the text and picking a fallback character</param>
		/// <param name="fontSize">The label font size</param>
		public void Create(EnemyHud enemyHud, TMP_FontAsset font, float fontSize)
		{
			if (outlineTag != null || character != null)
			{
				return;
			}
			outlineTag = CreateSpriteStar(enemyHud, font, fontSize);
			if (outlineTag == null)
			{
				character = PickCharacter(font);
			}
		}

		/// <summary>
		/// The text for one star with its fill in a color - the star character takes the label's own color instead
		/// </summary>
		/// <param name="fill">The fill color, normally the marker color</param>
		/// <returns>The star text, or "*" when Create hasn't run</returns>
		public string GetStar(Color fill)
		{
			if (outlineTag == null)
			{
				return((character ?? "*"));
			}
			return(outlineTag + "<sprite=1 color=#" + ColorUtility.ToHtmlStringRGBA(fill) + ">");
		}

		/// <summary>
		/// Removes the sprite asset and its material
		/// </summary>
		public void Destroy()
		{
			if (material != null)
			{
				Object.Destroy(material);
			}
			if (Asset != null)
			{
				Object.Destroy(Asset);
			}
			material = null;
			Asset = null;
			outlineTag = null;
			character = null;
		}

		/// <summary>
		/// Builds a sprite asset from the health bar's star outline and fill, centered on the font's capital letters
		/// </summary>
		/// <param name="enemyHud">The enemy HUD whose health bar template holds the star</param>
		/// <param name="font">The label font</param>
		/// <param name="fontSize">The label font size</param>
		/// <returns>The outline's sprite tag, or null when the star can't be found or used</returns>
		private string CreateSpriteStar(EnemyHud enemyHud, TMP_FontAsset font, float fontSize)
		{
			Transform outlineTransform = enemyHud.m_baseHud.transform.Find(OutlinePath);
			Transform fillTransform = ((outlineTransform != null) ? outlineTransform.Find(FillName) : null);
			if (font == null || fillTransform == null || !outlineTransform.TryGetComponent(out Image outline) || !fillTransform.TryGetComponent(out Image fill))
			{
				return(null);
			}
			Sprite outlineSprite = outline.sprite;
			Sprite fillSprite = fill.sprite;
			// The asset has one texture, so both sprites have to be packed into the same one
			if (outlineSprite == null || fillSprite == null || outlineSprite.texture == null || outlineSprite.texture != fillSprite.texture)
			{
				return(null);
			}
			Rect outlineRect;
			Rect fillRect;
			try
			{
				outlineRect = outlineSprite.textureRect;
				fillRect = fillSprite.textureRect;
			}
			catch (Exception)
			{
				// Tightly packed sprites have no rectangle in their texture
				return(null);
			}
			Shader shader = Shader.Find("TextMeshPro/Sprite");
			if (shader == null)
			{
				shader = Shader.Find("UI/Default");
			}
			if (shader == null)
			{
				return(null);
			}
			FaceInfo fontFace = font.faceInfo;
			float fontScale = fontSize / fontFace.pointSize * fontFace.scale;
			float center = fontFace.capLine * fontScale * 0.5f;
			Asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
			Asset.name = "CreatureMarkerStars";
			// Without a version, TextMesh Pro treats the asset as an old one and clears its tables to rebuild them
			AccessTools.Field(typeof(TMP_Asset), "m_Version").SetValue(Asset, "1.1.0");
			// Glyph sizes are in label units, since the asset's point size is the label's font size
			FaceInfo face = new FaceInfo();
			face.pointSize = fontSize;
			face.scale = 1f;
			face.ascentLine = fontFace.ascentLine * fontScale;
			face.descentLine = fontFace.descentLine * fontScale;
			Asset.faceInfo = face;
			Asset.spriteSheet = outlineSprite.texture;
			material = new Material(shader);
			material.mainTexture = outlineSprite.texture;
			Asset.material = material;
			AddSprite(0, outlineRect, OutlineSize, 0f, center, 0f);
			AddSprite(1, fillRect, FillSize, (OutlineSize - FillSize) * 0.5f, center, StarAdvance);
			Asset.UpdateLookupTables();
			return("<sprite=0 color=#" + ColorUtility.ToHtmlStringRGBA(outline.color) + ">");
		}

		/// <summary>
		/// Adds a square sprite to the asset, vertically centered on a height above the baseline
		/// </summary>
		/// <param name="index">The sprite's index, used in its sprite tag</param>
		/// <param name="rect">The sprite's rectangle in the texture, in pixels</param>
		/// <param name="size">Width and height to draw it at</param>
		/// <param name="bearingX">How far right of the current position to draw it</param>
		/// <param name="center">Height above the baseline to center it on</param>
		/// <param name="advance">How far it moves the position on to the next character</param>
		private void AddSprite(uint index, Rect rect, float size, float bearingX, float center, float advance)
		{
			GlyphMetrics metrics = new GlyphMetrics(size, size, bearingX, center + size * 0.5f, advance);
			GlyphRect glyphRect = new GlyphRect(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height));
			TMP_SpriteGlyph glyph = new TMP_SpriteGlyph(index, metrics, glyphRect, 1f, 0);
			Asset.spriteGlyphTable.Add(glyph);
			// 0xFFFE marks a sprite without a character, reached only by its index
			TMP_SpriteCharacter character = new TMP_SpriteCharacter(0xFFFE, Asset, glyph);
			character.name = "star" + index;
			Asset.spriteCharacterTable.Add(character);
		}

		/// <summary>
		/// The first of the star candidates the font, or one of its fallbacks, can draw
		/// </summary>
		/// <param name="font">The label font, or null</param>
		/// <returns>The star text</returns>
		private static string PickCharacter(TMP_FontAsset font)
		{
			if (font != null)
			{
				foreach (char candidate in StarCandidates)
				{
					if (font.HasCharacter(candidate, true))
					{
						return(candidate.ToString());
					}
				}
			}
			return("*");
		}
	}
}
