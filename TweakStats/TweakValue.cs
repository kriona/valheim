using System;
using System.Globalization;

namespace TweakStats
{
	/// <summary>
	/// How a value changes the stat it is applied to
	/// </summary>
	public enum TweakOperation
	{
		Set,
		Add,
		Multiply
	}

	/// <summary>
	/// A value from the config file - "50" sets, "+50" and "-50" add, "1.5x" multiplies and "=-50" sets a negative
	/// number, while anything that isn't a number is text to set, such as true, Resistant or a prefab name
	/// </summary>
	public class TweakValue
	{
		public TweakOperation Operation;

		/// <summary>
		/// The number to set, add or multiply by, when IsNumber is true
		/// </summary>
		public double Number;
		public bool IsNumber;

		/// <summary>
		/// The value as written, without a leading "="
		/// </summary>
		public string Text;

		/// <summary>
		/// Reads a value from the config file
		/// </summary>
		/// <param name="text">The text after the "=" on a config line</param>
		/// <param name="value">The value read</param>
		/// <param name="error">Why the value couldn't be read</param>
		/// <returns>True when the value was read</returns>
		public static bool TryParse(string text, out TweakValue value, out string error)
		{
			value = new TweakValue();
			error = null;
			text = text.Trim();
			if (text.StartsWith("="))
			{
				text = text.Substring(1).Trim();
				value.Operation = TweakOperation.Set;
				value.IsNumber = TryParseNumber(text, out value.Number);
			}
			else if (text.Length > 1 && (text.EndsWith("x") || text.EndsWith("X")) && TryParseNumber(text.Substring(0, text.Length - 1), out value.Number))
			{
				value.Operation = TweakOperation.Multiply;
				value.IsNumber = true;
			}
			else if ((text.StartsWith("+") || text.StartsWith("-")) && TryParseNumber(text, out value.Number))
			{
				value.Operation = TweakOperation.Add;
				value.IsNumber = true;
			}
			else
			{
				value.Operation = TweakOperation.Set;
				value.IsNumber = TryParseNumber(text, out value.Number);
			}
			value.Text = text;
			if (text == "")
			{
				error = "there's no value after the =";
				return(false);
			}
			return(true);
		}

		/// <summary>
		/// Works out the new value of a stat
		/// </summary>
		/// <param name="current">The stat's current value</param>
		/// <param name="type">The stat's type</param>
		/// <param name="result">The stat's new value</param>
		/// <param name="error">Why the value can't be applied to this stat</param>
		/// <returns>True when the new value was worked out</returns>
		public bool TryApply(object current, Type type, out object result, out string error)
		{
			result = current;
			error = null;
			if (IsNumeric(type))
			{
				if (!IsNumber)
				{
					error = "it needs a number, not \"" + Text + "\"";
					return(false);
				}
				double number = Convert.ToDouble(current, CultureInfo.InvariantCulture);
				if (Operation == TweakOperation.Add)
				{
					number += Number;
				}
				else if (Operation == TweakOperation.Multiply)
				{
					number *= Number;
				}
				else
				{
					number = Number;
				}
				return(TryConvertNumber(number, type, out result, out error));
			}
			if (Operation != TweakOperation.Set)
			{
				error = "only numbers can be added to or multiplied, so it needs a value like " + ExampleValue(type);
				return(false);
			}
			if (type == typeof(bool))
			{
				if (Text.Equals("true", StringComparison.OrdinalIgnoreCase))
				{
					result = true;
					return(true);
				}
				if (Text.Equals("false", StringComparison.OrdinalIgnoreCase))
				{
					result = false;
					return(true);
				}
				error = "it needs true or false, not \"" + Text + "\"";
				return(false);
			}
			if (type.IsEnum)
			{
				return(TryParseEnum(Text, type, out result, out error));
			}
			if (type == typeof(string))
			{
				result = Text;
				return(true);
			}
			if (typeof(UnityEngine.Object).IsAssignableFrom(type))
			{
				if (Text.Equals("none", StringComparison.OrdinalIgnoreCase))
				{
					result = null;
					return(true);
				}
				UnityEngine.Object found = Prefabs.Find(Text, type);
				if (found == null)
				{
					error = "there's no " + Prefabs.Describe(type) + " named \"" + Text + "\"";
					return(false);
				}
				result = found;
				return(true);
			}
			error = "TweakStats can't change a " + type.Name;
			return(false);
		}

		/// <summary>
		/// Whether a stat of this type can be changed by a value from the config file
		/// </summary>
		/// <param name="type">The stat's type</param>
		/// <returns>True for numbers, true/false, choices, text and references to prefabs TweakStats can look up</returns>
		public static bool CanSet(Type type)
		{
			return(IsNumeric(type) || type == typeof(bool) || type.IsEnum || type == typeof(string) || Prefabs.CanFind(type));
		}

		/// <summary>
		/// Writes a stat's value the way it would be written in the config file
		/// </summary>
		/// <param name="value">The stat's value</param>
		/// <returns>The value as config text</returns>
		public static string Format(object value)
		{
			if (value == null)
			{
				return("none");
			}
			if (value is UnityEngine.Object unityObject)
			{
				return((unityObject == null) ? "none" : Prefabs.NameOf(unityObject));
			}
			if (value is bool flag)
			{
				return(flag ? "true" : "false");
			}
			if (value is float || value is double)
			{
				return(Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("0.####", CultureInfo.InvariantCulture));
			}
			if (value is IFormattable formattable)
			{
				return(formattable.ToString(null, CultureInfo.InvariantCulture));
			}
			return(value.ToString());
		}

		/// <summary>
		/// Whether a type holds a number
		/// </summary>
		/// <param name="type">The type to check</param>
		/// <returns>True for float, double and the integer types</returns>
		public static bool IsNumeric(Type type)
		{
			return(type == typeof(float) || type == typeof(double) || type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) || type == typeof(uint) || type == typeof(ushort));
		}

		/// <summary>
		/// Reads a number written with a "." for decimals, whatever the system's language
		/// </summary>
		/// <param name="text">The text to read</param>
		/// <param name="number">The number read</param>
		/// <returns>True when the text is a number</returns>
		private static bool TryParseNumber(string text, out double number)
		{
			return(double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) && !double.IsNaN(number) && !double.IsInfinity(number));
		}

		/// <summary>
		/// Converts a worked out number to the stat's type, rounding for whole-number stats
		/// </summary>
		/// <param name="number">The number</param>
		/// <param name="type">The stat's type</param>
		/// <param name="result">The number as the stat's type</param>
		/// <param name="error">Why the number doesn't fit the stat</param>
		/// <returns>True when the number fits</returns>
		private static bool TryConvertNumber(double number, Type type, out object result, out string error)
		{
			result = null;
			error = null;
			if (type == typeof(float))
			{
				result = (float)number;
				return(true);
			}
			if (type == typeof(double))
			{
				result = number;
				return(true);
			}
			double rounded = Math.Round(number, MidpointRounding.AwayFromZero);
			try
			{
				result = Convert.ChangeType(rounded, type, CultureInfo.InvariantCulture);
				return(true);
			}
			catch (OverflowException)
			{
				error = rounded.ToString(CultureInfo.InvariantCulture) + " is too large or small for it";
				return(false);
			}
		}

		/// <summary>
		/// Reads one of an enum's names, ignoring case
		/// </summary>
		/// <param name="text">The name to read</param>
		/// <param name="type">The enum type</param>
		/// <param name="result">The enum value</param>
		/// <param name="error">Why the name couldn't be read, listing the names that can be used</param>
		/// <returns>True when the name was read</returns>
		private static bool TryParseEnum(string text, Type type, out object result, out string error)
		{
			result = null;
			error = null;
			foreach (string name in Enum.GetNames(type))
			{
				if (name.Equals(text, StringComparison.OrdinalIgnoreCase))
				{
					result = Enum.Parse(type, name);
					return(true);
				}
			}
			error = "\"" + text + "\" isn't one of " + string.Join(", ", Enum.GetNames(type));
			return(false);
		}

		/// <summary>
		/// An example value for an error message about a stat that can only be set
		/// </summary>
		/// <param name="type">The stat's type</param>
		/// <returns>An example config value</returns>
		private static string ExampleValue(Type type)
		{
			if (type == typeof(bool))
			{
				return("true");
			}
			if (type.IsEnum)
			{
				return(Enum.GetNames(type)[0]);
			}
			return("a name");
		}
	}
}
