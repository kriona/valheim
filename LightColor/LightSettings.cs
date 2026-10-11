using System.Globalization;
using UnityEngine;

namespace LightColor
{
	/// <summary>
	/// The ways a sign's text can glow, which can be combined
	/// </summary>
	[System.Flags]
	public enum TextStyle
	{
		None = 0,

		/// <summary>
		/// A soft glow in the color behind the letters
		/// </summary>
		Halo = 1,

		/// <summary>
		/// Letters drawn in the color and lit from within
		/// </summary>
		Lit = 2,

		/// <summary>
		/// A crisp edge in the color around the letters
		/// </summary>
		Outline = 4
	}

	/// <summary>
	/// The color, brightness and range typed for a piece, like "blue 150% 20m", or the color, brightness and text
	/// styles typed for a sign, like "blue 150% halo lit"
	/// </summary>
	public class LightSettings
	{
		public const float MinBrightness = 0f;
		public const float MaxBrightness = 500f;
		public const float MinRange = 1f;
		public const float MaxRange = 50f;

		private const string DefaultText = "default";

		/// <summary>
		/// The settings that leave a piece as it is - its own color, brightness and range
		/// </summary>
		public static readonly LightSettings Default = new LightSettings();

		/// <summary>
		/// Whether a color was typed, rather than keeping the piece's own colors
		/// </summary>
		public bool HasColor { get; private set; }

		/// <summary>
		/// The typed color, opaque, when HasColor is true
		/// </summary>
		public Color Color { get; private set; } = Color.white;

		/// <summary>
		/// What the piece's light brightness is multiplied by, 1 when none was typed
		/// </summary>
		public float Brightness { get; private set; } = 1f;

		/// <summary>
		/// The range in meters for the piece's largest light, or 0 to keep the lights' own ranges
		/// </summary>
		public float Range { get; private set; }

		/// <summary>
		/// The text styles typed for a sign, None when none were typed
		/// </summary>
		public TextStyle Styles { get; private set; }

		/// <summary>
		/// Settings with only a color, for effects that take a piece's color but not its brightness or range
		/// </summary>
		/// <param name="color">The opaque color</param>
		/// <returns>The settings</returns>
		public static LightSettings ColorOnly(Color color)
		{
			LightSettings settings = new LightSettings();
			settings.HasColor = true;
			settings.Color = color;
			return(settings);
		}

		/// <summary>
		/// Reads settings from text made of a color, a brightness ending in %, a range ending in m and the text styles
		/// halo, lit and outline, in any order and all optional, separated by spaces or commas
		/// </summary>
		/// <param name="text">The text, where blank or "default" means the piece's own settings</param>
		/// <param name="settings">The settings read, or Default when the text can't be read</param>
		/// <param name="error">Why the text can't be read, or null</param>
		/// <returns>True when the text could be read</returns>
		public static bool TryParse(string text, out LightSettings settings, out string error)
		{
			settings = Default;
			error = null;
			LightSettings parsed = new LightSettings();
			foreach (string part in text.Split(new char[] { ' ', ',' }, System.StringSplitOptions.RemoveEmptyEntries))
			{
				string token = part.ToLowerInvariant();
				if (token == DefaultText)
				{
					continue;
				}
				if (TryParseStyle(token, out TextStyle style))
				{
					parsed.Styles |= style;
					continue;
				}
				if (token.EndsWith("%"))
				{
					if (!TryParseNumber(token.Substring(0, token.Length - 1), out float percent) || percent < MinBrightness || percent > MaxBrightness)
					{
						error = "Brightness must be from " + MinBrightness + "% to " + MaxBrightness + "%";
						return(false);
					}
					parsed.Brightness = percent / 100f;
					continue;
				}
				if (token.EndsWith("m") && TryParseNumber(token.Substring(0, token.Length - 1), out float meters))
				{
					if (meters < MinRange || meters > MaxRange)
					{
						error = "Range must be from " + MinRange + "m to " + MaxRange + "m";
						return(false);
					}
					parsed.Range = meters;
					continue;
				}
				if (parsed.HasColor)
				{
					error = "Only one color can be used";
					return(false);
				}
				if (!ColorUtility.TryParseHtmlString(part, out Color color))
				{
					error = "Couldn't read the color " + part;
					return(false);
				}
				color.a = 1f;
				parsed.HasColor = true;
				parsed.Color = color;
			}
			settings = parsed;
			return(true);
		}

		/// <summary>
		/// Whether the settings change anything about a piece
		/// </summary>
		/// <returns>True when a color, brightness, range or text style was typed</returns>
		public bool IsDefault()
		{
			return(!HasColor && Brightness == 1f && Range == 0f && Styles == TextStyle.None);
		}

		/// <summary>
		/// Reads a text style name
		/// </summary>
		/// <param name="token">The lowercase word</param>
		/// <param name="style">The style, or None when the word isn't one</param>
		/// <returns>True when the word is a text style</returns>
		private static bool TryParseStyle(string token, out TextStyle style)
		{
			switch (token)
			{
				case "halo":
					style = TextStyle.Halo;
					return(true);
				case "lit":
					style = TextStyle.Lit;
					return(true);
				case "outline":
					style = TextStyle.Outline;
					return(true);
			}
			style = TextStyle.None;
			return(false);
		}

		/// <summary>
		/// Parses a number with a period as the decimal separator, whatever the system's language
		/// </summary>
		/// <param name="text">The number text</param>
		/// <param name="value">The number</param>
		/// <returns>True when the text is a number</returns>
		private static bool TryParseNumber(string text, out float value)
		{
			return(float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value));
		}
	}
}
