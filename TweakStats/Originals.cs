using System.Collections.Generic;
using System.Reflection;

namespace TweakStats
{
	/// <summary>
	/// Sets the fields tweaks change, keeping each field's value from before its first change so the game's own values
	/// can be put back before the config file is applied again
	/// </summary>
	/// <remarks>
	/// Lists and arrays are always replaced with a changed copy rather than changed in place, so putting back the
	/// original list also puts back its entries
	/// </remarks>
	public static class Originals
	{
		/// <summary>
		/// The original value of each changed field, by the object it belongs to
		/// </summary>
		private static readonly Dictionary<object, Dictionary<FieldInfo, object>> values = new Dictionary<object, Dictionary<FieldInfo, object>>(ReferenceComparer.Instance);

		/// <summary>
		/// Sets a field, first keeping its original value if this is its first change
		/// </summary>
		/// <param name="owner">The object the field belongs to, which must not be a struct</param>
		/// <param name="field">The field</param>
		/// <param name="value">The new value</param>
		public static void Set(object owner, FieldInfo field, object value)
		{
			if (!values.TryGetValue(owner, out Dictionary<FieldInfo, object> fields))
			{
				fields = new Dictionary<FieldInfo, object>();
				values.Add(owner, fields);
			}
			if (!fields.ContainsKey(field))
			{
				fields.Add(field, field.GetValue(owner));
			}
			field.SetValue(owner, value);
		}

		/// <summary>
		/// Puts back the original value of every changed field
		/// </summary>
		public static void Restore()
		{
			foreach (KeyValuePair<object, Dictionary<FieldInfo, object>> owner in values)
			{
				foreach (KeyValuePair<FieldInfo, object> field in owner.Value)
				{
					field.Key.SetValue(owner.Key, field.Value);
				}
			}
			values.Clear();
		}
	}
}
