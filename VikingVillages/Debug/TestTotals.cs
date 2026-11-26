using System.Collections.Generic;
using System.Globalization;

namespace VikingVillages
{
	/// <summary>
	/// Running totals for the test log (see TestLog) - amounts by category and by what's counted in it, like the jobs
	/// started by kind of job or the items looted by item - written as one line per category, in the order each
	/// category was first counted
	/// </summary>
	internal class TestTotals
	{
		/// <summary>
		/// How many of a category's counts its line lists by name, the largest first, before the rest are summed up
		/// </summary>
		public const int ListedMax = 25;

		/// <summary>
		/// The categories in the order they were first counted
		/// </summary>
		private readonly List<string> order = new List<string>();

		/// <summary>
		/// Each category's amounts, by what's counted
		/// </summary>
		private readonly Dictionary<string, Dictionary<string, double>> totals = new Dictionary<string, Dictionary<string, double>>();

		/// <summary>
		/// Adds an amount to what's counted in a category
		/// </summary>
		/// <param name="category">The category, like "Jobs started"</param>
		/// <param name="key">What's counted, like the kind of job</param>
		/// <param name="amount">How much to add</param>
		public void Add(string category, string key, double amount)
		{
			if (!totals.TryGetValue(category, out Dictionary<string, double> counts))
			{
				counts = new Dictionary<string, double>();
				totals[category] = counts;
				order.Add(category);
			}
			counts.TryGetValue(key, out double total);
			counts[key] = total + amount;
		}

		/// <summary>
		/// Gets the amount counted for something in a category
		/// </summary>
		/// <param name="category">The category</param>
		/// <param name="key">What's counted</param>
		/// <returns>The amount, 0 when nothing was counted</returns>
		public double Get(string category, string key)
		{
			if (totals.TryGetValue(category, out Dictionary<string, double> counts) && counts.TryGetValue(key, out double total))
			{
				return(total);
			}
			return(0.0);
		}

		/// <summary>
		/// Writes each category's line - its total, then what's counted in it, the largest first and alphabetically
		/// among equals, up to ListedMax of them with the rest summed up
		/// </summary>
		/// <returns>The lines, like "Jobs started: 12 - Stroll 7, Fuel 5"</returns>
		public List<string> Lines()
		{
			List<string> lines = new List<string>();
			foreach (string category in order)
			{
				List<KeyValuePair<string, double>> counts = new List<KeyValuePair<string, double>>(totals[category]);
				counts.Sort((KeyValuePair<string, double> a, KeyValuePair<string, double> b) => ((a.Value != b.Value) ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key)));
				double sum = 0.0;
				List<string> parts = new List<string>();
				double rest = 0.0;
				int others = 0;
				foreach (KeyValuePair<string, double> count in counts)
				{
					sum += count.Value;
					if (parts.Count < ListedMax)
					{
						parts.Add(count.Key + " " + Format(count.Value));
					}
					else
					{
						rest += count.Value;
						others++;
					}
				}
				if (others > 0)
				{
					parts.Add(others + " more " + Format(rest));
				}
				lines.Add(category + ": " + Format(sum) + " - " + string.Join(", ", parts));
			}
			return(lines);
		}

		/// <summary>
		/// Writes an amount - a whole number as it is, and anything else to one decimal place
		/// </summary>
		/// <param name="value">The amount</param>
		/// <returns>The text</returns>
		internal static string Format(double value)
		{
			double rounded = System.Math.Round(value, 1);
			if (rounded == System.Math.Floor(rounded))
			{
				return(rounded.ToString("F0", CultureInfo.InvariantCulture));
			}
			return(rounded.ToString("F1", CultureInfo.InvariantCulture));
		}
	}
}
