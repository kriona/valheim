using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace TweakStats
{
	/// <summary>
	/// Keeps tweaked numbers inside the range the game can handle without hanging, crashing or damaging saved data,
	/// noting each number it changes so it can be reported
	/// </summary>
	/// <remarks>
	/// The limits are well past anything playable, so extreme values can still be tried - they only stop the values that
	/// break the game rather than just making it silly
	/// </remarks>
	public static class Limits
	{
		/// <summary>
		/// The range one stat has to stay in, and why
		/// </summary>
		private class Limit
		{
			public FieldInfo Field;
			public double Min;
			public double Max;
			public string Reason;

			/// <summary>
			/// Makes the limit for one field
			/// </summary>
			/// <param name="type">The type declaring the field</param>
			/// <param name="name">The field's name</param>
			/// <param name="min">The smallest value allowed</param>
			/// <param name="max">The largest value allowed</param>
			/// <param name="reason">What goes wrong past the limit</param>
			public Limit(Type type, string name, double min, double max, string reason)
			{
				Field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				Min = min;
				Max = max;
				Reason = reason;
			}
		}

		/// <summary>
		/// The largest size any number can be made, which keeps the game's sums of several stats from overflowing
		/// </summary>
		private const double MaxMagnitude = 1000000000;

		private static readonly List<Limit> limits = new List<Limit>
		{
			new Limit(typeof(ItemDrop.ItemData.SharedData), nameof(ItemDrop.ItemData.SharedData.m_maxStackSize), 1, ushort.MaxValue, "the game can't add items to a stack of 0, and saves stacks as numbers up to 65535"),
			new Limit(typeof(Recipe), nameof(Recipe.m_amount), 0, 1000, "the game makes each crafted stack separately, all at once"),
			new Limit(typeof(Attack), nameof(Attack.m_projectiles), 0, 100, "the game makes every projectile of a shot at once"),
			new Limit(typeof(Attack), nameof(Attack.m_attackAngle), -360, 360, "the game checks for hits every 4 degrees of a swing"),
			new Limit(typeof(Container), nameof(Container.m_width), 1, 100, "the game makes a slot in the window for each slot in the chest"),
			new Limit(typeof(Container), nameof(Container.m_height), 1, 100, "the game makes a slot in the window for each slot in the chest"),
			new Limit(typeof(Beehive), nameof(Beehive.m_maxHoney), 0, 1000, "the game drops each honey separately, all at once"),
			new Limit(typeof(SapCollector), nameof(SapCollector.m_maxLevel), 0, 1000, "the game drops each sap separately, all at once")
		};

		/// <summary>
		/// Notes about the numbers changed since they were last taken
		/// </summary>
		private static readonly List<string> notes = new List<string>();

		/// <summary>
		/// Keeps a stat's new value inside the limits for that stat
		/// </summary>
		/// <param name="field">The stat's field</param>
		/// <param name="result">The stat's new value</param>
		/// <returns>The new value, moved inside the limits when it was outside them</returns>
		public static object Clamp(FieldInfo field, object result)
		{
			if (result == null || !TweakValue.IsNumeric(field.FieldType))
			{
				return(result);
			}
			double value = Convert.ToDouble(result, CultureInfo.InvariantCulture);
			double clamped = value;
			string reason = null;
			foreach (Limit limit in limits)
			{
				if (limit.Field == null || limit.Field.DeclaringType != field.DeclaringType || limit.Field.Name != field.Name)
				{
					continue;
				}
				if (clamped > limit.Max)
				{
					clamped = limit.Max;
					reason = limit.Reason;
				}
				else if (clamped < limit.Min)
				{
					clamped = limit.Min;
					reason = limit.Reason;
				}
			}
			if (reason == null)
			{
				return(result);
			}
			AddNote(value, clamped, reason);
			return(Convert.ChangeType(clamped, field.FieldType, CultureInfo.InvariantCulture));
		}

		/// <summary>
		/// Keeps a worked out number from being too large for the game's math, unless the stat was already that large
		/// </summary>
		/// <param name="value">The worked out number</param>
		/// <param name="original">The stat's value before the tweak</param>
		/// <returns>The number, moved inside the limit when it was outside it</returns>
		public static double ClampMagnitude(double value, double original)
		{
			if (Math.Abs(value) <= MaxMagnitude || Math.Abs(value) <= Math.Abs(original))
			{
				return(value);
			}
			double clamped = Math.Sign(value) * Math.Max(MaxMagnitude, Math.Abs(original));
			AddNote(value, clamped, "larger numbers can overflow the game's math");
			return(clamped);
		}

		/// <summary>
		/// Notes that a number was changed to keep it inside a limit
		/// </summary>
		/// <param name="value">The number before</param>
		/// <param name="clamped">The number after</param>
		/// <param name="reason">What goes wrong past the limit</param>
		private static void AddNote(double value, double clamped, string reason)
		{
			notes.Add(Describe(value) + " is outside what the game can handle, so it's " + Describe(clamped) + " - " + reason);
		}

		/// <summary>
		/// Writes a number for a note, in scientific notation when it's very large
		/// </summary>
		/// <param name="number">The number</param>
		/// <returns>The number as text</returns>
		private static string Describe(double number)
		{
			string format = ((Math.Abs(number) < 1e15) ? "0.####" : "0.###e+0");
			return(number.ToString(format, CultureInfo.InvariantCulture));
		}

		/// <summary>
		/// The notes about the numbers changed since they were last taken, without repeats, and clears them
		/// </summary>
		/// <returns>The notes</returns>
		public static List<string> TakeNotes()
		{
			List<string> taken = new List<string>();
			foreach (string note in notes)
			{
				if (!taken.Contains(note))
				{
					taken.Add(note);
				}
			}
			notes.Clear();
			return(taken);
		}
	}
}
