using System.Globalization;
using UnityEngine;

namespace LightColor
{
	/// <summary>
	/// The color, brightness and range typed for a piece, like "blue 150% 20m"
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
		/// Reads settings from text made of a color, a brightness ending in % and a range ending in m, in any order and
		/// all optional, separated by spaces or commas
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
		/// <returns>True when a color, brightness or range was typed</returns>
		public bool IsDefault()
		{
			return(!HasColor && Brightness == 1f && Range == 0f);
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
