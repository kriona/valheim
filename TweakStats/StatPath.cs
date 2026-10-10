using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace TweakStats
{
	/// <summary>
	/// Follows a stat's dotted path, e.g. attack.attackStamina or resources.Iron, through an object's fields to change
	/// the stat, and lists every stat an object has
	/// </summary>
	/// <remarks>
	/// A path names fields without their m_ prefix, ignoring case. A list is followed by the name of one of its
	/// entries, and a group of same-typed numbers or choices, like damages, can be changed all at once
	/// </remarks>
	public static class StatPath
	{
		/// <summary>
		/// How the entries of one kind of list are named in a path
		/// </summary>
		private class ListEntry
		{
			public Type ElementType;

			/// <summary>
			/// The field holding each entry's name, which isn't listed as a stat of its own
			/// </summary>
			public string KeyField;

			/// <summary>
			/// The field a value changes when the path ends at the entry, e.g. the amount for resources.Iron = 5
			/// </summary>
			public string DefaultField;

			/// <summary>
			/// Whether setting the default field to 0 removes the entry
			/// </summary>
			public bool RemoveWhenZero;

			/// <summary>
			/// Reads an entry's name
			/// </summary>
			public Func<object, string> Key;

			/// <summary>
			/// Makes a new entry with the given name, or returns null with an error when nothing has that name
			/// </summary>
			public Func<string, (object entry, string error)> Create;
		}

		/// <summary>
		/// Deepest path TweakStats lists stats for, which keeps it out of loops between objects
		/// </summary>
		private const int MaxDepth = 5;

		private static readonly List<ListEntry> listEntries = new List<ListEntry>
		{
			new ListEntry
			{
				ElementType = typeof(Piece.Requirement),
				KeyField = "m_resItem",
				DefaultField = "m_amount",
				RemoveWhenZero = true,
				Key = entry => ((Piece.Requirement)entry).m_resItem?.gameObject.name,
				Create = CreateRequirement
			},
			new ListEntry
			{
				ElementType = typeof(HitData.DamageModPair),
				KeyField = "m_type",
				DefaultField = "m_modifier",
				RemoveWhenZero = false,
				Key = entry => ((HitData.DamageModPair)entry).m_type.ToString(),
				Create = CreateDamageModPair
			}
		};

		private static readonly Dictionary<Type, List<FieldInfo>> fieldCache = new Dictionary<Type, List<FieldInfo>>();

		/// <summary>
		/// Changes the stat at a path
		/// </summary>
		/// <param name="root">The object the path starts from, e.g. an item's shared data or a recipe</param>
		/// <param name="path">The stat's dotted path</param>
		/// <param name="value">The value to apply</param>
		/// <param name="error">Why the stat couldn't be changed, or null when it was changed or the object doesn't have
		/// that part of the path filled in, like attack on an item that can't attack</param>
		/// <returns>True when nothing went wrong</returns>
		public static bool TryApply(object root, string path, TweakValue value, out string error)
		{
			string[] parts = path.Split('.');
			for (int i = 0; i < parts.Length; i++)
			{
				parts[i] = parts[i].Trim();
				if (parts[i] == "")
				{
					error = "\"" + path + "\" has an empty part between its dots";
					return(false);
				}
			}
			return(TryApplyToField(root, parts, 0, value, out error));
		}

		/// <summary>
		/// Lists every stat an object has with its current value, as config lines
		/// </summary>
		/// <param name="root">The object, e.g. an item's shared data or a recipe</param>
		/// <param name="lines">The list to add "path = value" lines to</param>
		public static void List(object root, List<string> lines)
		{
			ListFields(root, "", lines, 0, null);
		}

		/// <summary>
		/// The fields of a type that hold stats - public or Unity-serialized instance fields
		/// </summary>
		/// <param name="type">The type</param>
		/// <returns>The fields, in the order they are declared</returns>
		private static List<FieldInfo> GetFields(Type type)
		{
			if (fieldCache.TryGetValue(type, out List<FieldInfo> fields))
			{
				return(fields);
			}
			fields = new List<FieldInfo>();
			foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (field.IsNotSerialized || field.Name.Contains("<"))
				{
					continue;
				}
				if (field.IsPublic || field.IsDefined(typeof(SerializeField), true))
				{
					fields.Add(field);
				}
			}
			fieldCache.Add(type, fields);
			return(fields);
		}

		/// <summary>
		/// The name a field has in a path
		/// </summary>
		/// <param name="field">The field</param>
		/// <returns>The field's name without its m_ prefix</returns>
		private static string PathName(FieldInfo field)
		{
			return(field.Name.StartsWith("m_") ? field.Name.Substring(2) : field.Name);
		}

		/// <summary>
		/// Finds a field by its path name, ignoring case and an m_ prefix
		/// </summary>
		/// <param name="type">The type holding the field</param>
		/// <param name="name">The name from the path</param>
		/// <returns>The field, or null</returns>
		private static FieldInfo FindField(Type type, string name)
		{
			foreach (FieldInfo field in GetFields(type))
			{
				if (PathName(field).Equals(name, StringComparison.OrdinalIgnoreCase) || field.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					return(field);
				}
			}
			return(null);
		}

		/// <summary>
		/// Changes the field a path part names on an object, following the rest of the path when there is more
		/// </summary>
		/// <param name="owner">The object holding the field - a struct is boxed and its caller writes it back</param>
		/// <param name="parts">The path's parts</param>
		/// <param name="index">The part naming the field</param>
		/// <param name="value">The value to apply</param>
		/// <param name="error">Why the stat couldn't be changed</param>
		/// <returns>True when nothing went wrong</returns>
		private static bool TryApplyToField(object owner, string[] parts, int index, TweakValue value, out string error)
		{
			error = null;
			FieldInfo field = FindField(owner.GetType(), parts[index]);
			if (field == null)
			{
				error = "there's no stat \"" + JoinPath(parts, index + 1) + "\"" + Suggest(owner.GetType(), parts[index]);
				return(false);
			}
			Type type = field.FieldType;
			object current = field.GetValue(owner);
			bool isLast = (index == parts.Length - 1);
			object result;
			if (IsList(type))
			{
				if (isLast)
				{
					error = JoinPath(parts, index + 1) + " is a list, so it needs the name of an entry, e.g. " + JoinPath(parts, index + 1) + "." + ExampleKey(type);
					return(false);
				}
				if (!TryApplyToList(current, type, parts, index + 1, value, out result, out error))
				{
					return(false);
				}
			}
			else if (isLast)
			{
				if (TweakValue.CanSet(type))
				{
					if (!value.TryApply(current, type, out result, out error))
					{
						return(false);
					}
				}
				else if (IsUniformGroup(type))
				{
					result = current;
					foreach (FieldInfo member in GetFields(type))
					{
						if (!value.TryApply(member.GetValue(result), member.FieldType, out object memberResult, out error))
						{
							return(false);
						}
						member.SetValue(result, memberResult);
					}
				}
				else
				{
					error = JoinPath(parts, index + 1) + " is a group of stats, so it needs the name of one" + ExampleField(type, JoinPath(parts, index + 1));
					return(false);
				}
			}
			else
			{
				if (typeof(UnityEngine.Object).IsAssignableFrom(type))
				{
					error = JoinPath(parts, index + 1) + " points to another object, which can't be changed from here";
					return(false);
				}
				if (!IsGroup(type))
				{
					error = JoinPath(parts, index + 1) + " is a single stat, so it can't be followed by ." + parts[index + 1];
					return(false);
				}
				if (current == null)
				{
					return(true);
				}
				if (!TryApplyToField(current, parts, index + 1, value, out error))
				{
					return(false);
				}
				if (!type.IsValueType)
				{
					return(true);
				}
				result = current;
			}
			Write(owner, field, result);
			return(true);
		}

		/// <summary>
		/// Changes the entries of a list named by a path part, adding an entry when none has that name and removing one
		/// set to 0 when its kind of list allows it
		/// </summary>
		/// <param name="current">The list or array</param>
		/// <param name="listType">The list's type</param>
		/// <param name="parts">The path's parts</param>
		/// <param name="index">The part naming the entry</param>
		/// <param name="value">The value to apply</param>
		/// <param name="result">A changed copy of the list</param>
		/// <param name="error">Why the list couldn't be changed</param>
		/// <returns>True when nothing went wrong</returns>
		private static bool TryApplyToList(object current, Type listType, string[] parts, int index, TweakValue value, out object result, out string error)
		{
			result = current;
			error = null;
			Type elementType = ElementType(listType);
			ListEntry info = FindListEntry(elementType);
			if (info == null)
			{
				error = "TweakStats can't change the entries of " + JoinPath(parts, index);
				return(false);
			}
			List<object> entries = new List<object>();
			if (current != null)
			{
				foreach (object entry in (IEnumerable)current)
				{
					entries.Add(entry);
				}
			}
			string key = parts[index];
			bool isLast = (index == parts.Length - 1);
			bool remove = (isLast && info.RemoveWhenZero && value.Operation == TweakOperation.Set && value.IsNumber && value.Number == 0);
			List<int> matches = new List<int>();
			for (int i = 0; i < entries.Count; i++)
			{
				if (entries[i] != null && string.Equals(info.Key(entries[i]), key, StringComparison.OrdinalIgnoreCase))
				{
					matches.Add(i);
				}
			}
			if (matches.Count == 0)
			{
				if (remove)
				{
					return(true);
				}
				(object entry, string createError) = info.Create(key);
				if (entry == null)
				{
					error = createError;
					return(false);
				}
				entries.Add(entry);
				matches.Add(entries.Count - 1);
			}
			if (remove)
			{
				for (int i = matches.Count - 1; i >= 0; i--)
				{
					entries.RemoveAt(matches[i]);
				}
			}
			else
			{
				foreach (int i in matches)
				{
					object entry = entries[i];
					if (isLast)
					{
						FieldInfo field = elementType.GetField(info.DefaultField);
						if (!value.TryApply(field.GetValue(entry), field.FieldType, out object fieldResult, out error))
						{
							return(false);
						}
						Write(entry, field, fieldResult);
					}
					else if (!TryApplyToField(entry, parts, index + 1, value, out error))
					{
						return(false);
					}
					entries[i] = entry;
				}
			}
			result = MakeList(listType, elementType, entries);
			return(true);
		}

		/// <summary>
		/// Sets a field, keeping the original value when the field belongs to an object rather than a boxed struct that
		/// its caller writes back
		/// </summary>
		/// <param name="owner">The object or boxed struct holding the field</param>
		/// <param name="field">The field</param>
		/// <param name="value">The new value</param>
		private static void Write(object owner, FieldInfo field, object value)
		{
			if (owner.GetType().IsValueType)
			{
				field.SetValue(owner, value);
			}
			else
			{
				Originals.Set(owner, field, value);
			}
		}

		/// <summary>
		/// Adds a "path = value" line for each stat of an object, following groups and named list entries
		/// </summary>
		/// <param name="owner">The object</param>
		/// <param name="prefix">The path to the object, ending in a dot, or empty for the root</param>
		/// <param name="lines">The list to add lines to</param>
		/// <param name="depth">How many groups deep the object is</param>
		/// <param name="skip">Fields to leave out, or null - a list entry's name and default fields</param>
		private static void ListFields(object owner, string prefix, List<string> lines, int depth, ICollection<string> skip)
		{
			foreach (FieldInfo field in GetFields(owner.GetType()))
			{
				if (skip != null && skip.Contains(field.Name))
				{
					continue;
				}
				string path = prefix + PathName(field);
				Type type = field.FieldType;
				object value = field.GetValue(owner);
				if (TweakValue.CanSet(type))
				{
					lines.Add(path + " = " + TweakValue.Format(value));
				}
				else if (IsList(type))
				{
					ListEntry info = FindListEntry(ElementType(type));
					if (info == null || value == null)
					{
						continue;
					}
					string[] entrySkip = new string[] { info.KeyField, info.DefaultField };
					foreach (object entry in (IEnumerable)value)
					{
						string key = (entry != null) ? info.Key(entry) : null;
						if (string.IsNullOrEmpty(key))
						{
							continue;
						}
						lines.Add(path + "." + key + " = " + TweakValue.Format(info.ElementType.GetField(info.DefaultField).GetValue(entry)));
						ListFields(entry, path + "." + key + ".", lines, depth + 1, entrySkip);
					}
				}
				else if (IsGroup(type) && value != null && depth < MaxDepth)
				{
					ListFields(value, path + ".", lines, depth + 1, null);
				}
			}
		}

		/// <summary>
		/// Whether a type is a list or array
		/// </summary>
		/// <param name="type">The type</param>
		/// <returns>True for arrays and List&lt;T&gt;</returns>
		private static bool IsList(Type type)
		{
			return(type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)));
		}

		/// <summary>
		/// The type of a list's entries
		/// </summary>
		/// <param name="listType">The list or array type</param>
		/// <returns>The entry type</returns>
		private static Type ElementType(Type listType)
		{
			return(listType.IsArray ? listType.GetElementType() : listType.GetGenericArguments()[0]);
		}

		/// <summary>
		/// Makes a list or array of the given type holding the given entries
		/// </summary>
		/// <param name="listType">The list or array type</param>
		/// <param name="elementType">The entry type</param>
		/// <param name="entries">The entries</param>
		/// <returns>The new list or array</returns>
		private static object MakeList(Type listType, Type elementType, List<object> entries)
		{
			if (listType.IsArray)
			{
				Array array = Array.CreateInstance(elementType, entries.Count);
				for (int i = 0; i < entries.Count; i++)
				{
					array.SetValue(entries[i], i);
				}
				return(array);
			}
			IList list = (IList)Activator.CreateInstance(listType);
			foreach (object entry in entries)
			{
				list.Add(entry);
			}
			return(list);
		}

		/// <summary>
		/// How entries of a type are named in a path
		/// </summary>
		/// <param name="elementType">The entry type</param>
		/// <returns>The naming rules, or null when TweakStats can't name entries of that type</returns>
		private static ListEntry FindListEntry(Type elementType)
		{
			foreach (ListEntry info in listEntries)
			{
				if (info.ElementType == elementType)
				{
					return(info);
				}
			}
			return(null);
		}

		/// <summary>
		/// Whether a path can go into a type to name one of its fields
		/// </summary>
		/// <param name="type">The type</param>
		/// <returns>True for serializable classes and structs that aren't Unity objects, lists or single values</returns>
		private static bool IsGroup(Type type)
		{
			if (type.IsPrimitive || type.IsEnum || type == typeof(string) || IsList(type) || typeof(UnityEngine.Object).IsAssignableFrom(type))
			{
				return(false);
			}
			return(type.IsSerializable);
		}

		/// <summary>
		/// Whether a type is a struct of same-typed stats that a value can change all at once, like the damage types
		/// in damages
		/// </summary>
		/// <param name="type">The type</param>
		/// <returns>True when every field has the same type and that type can be set</returns>
		private static bool IsUniformGroup(Type type)
		{
			if (!type.IsValueType || !IsGroup(type))
			{
				return(false);
			}
			List<FieldInfo> fields = GetFields(type);
			if (fields.Count < 2 || !TweakValue.CanSet(fields[0].FieldType))
			{
				return(false);
			}
			foreach (FieldInfo field in fields)
			{
				if (field.FieldType != fields[0].FieldType)
				{
					return(false);
				}
			}
			return(true);
		}

		/// <summary>
		/// The first parts of a path, joined with dots, for messages
		/// </summary>
		/// <param name="parts">The path's parts</param>
		/// <param name="count">How many parts to join</param>
		/// <returns>The partial path</returns>
		private static string JoinPath(string[] parts, int count)
		{
			return(string.Join(".", parts, 0, count));
		}

		/// <summary>
		/// Suggests stats with names close to one that doesn't exist
		/// </summary>
		/// <param name="type">The type that was searched</param>
		/// <param name="name">The name that wasn't found</param>
		/// <returns>" - did you mean ...?", or empty when nothing is close</returns>
		private static string Suggest(Type type, string name)
		{
			List<string> close = new List<string>();
			foreach (FieldInfo field in GetFields(type))
			{
				string fieldName = PathName(field);
				bool contains = name.Length >= 3 && fieldName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0;
				if (contains || Distance(fieldName.ToLowerInvariant(), name.ToLowerInvariant()) <= 2)
				{
					close.Add(fieldName);
				}
			}
			if (close.Count == 0 || close.Count > 5)
			{
				return("");
			}
			return(" - did you mean " + string.Join(" or ", close) + "?");
		}

		/// <summary>
		/// The number of single-letter edits between two words
		/// </summary>
		/// <param name="a">The first word</param>
		/// <param name="b">The second word</param>
		/// <returns>The edit distance</returns>
		private static int Distance(string a, string b)
		{
			int[] previous = new int[b.Length + 1];
			int[] row = new int[b.Length + 1];
			for (int j = 0; j <= b.Length; j++)
			{
				previous[j] = j;
			}
			for (int i = 1; i <= a.Length; i++)
			{
				row[0] = i;
				for (int j = 1; j <= b.Length; j++)
				{
					int cost = ((a[i - 1] == b[j - 1]) ? 0 : 1);
					row[j] = Math.Min(Math.Min(row[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
				}
				int[] swap = previous;
				previous = row;
				row = swap;
			}
			return(previous[b.Length]);
		}

		/// <summary>
		/// An example entry name for a list, for messages
		/// </summary>
		/// <param name="listType">The list type</param>
		/// <returns>An entry name that list type uses</returns>
		private static string ExampleKey(Type listType)
		{
			Type elementType = ElementType(listType);
			if (elementType == typeof(HitData.DamageModPair))
			{
				return("Fire");
			}
			return("Wood");
		}

		/// <summary>
		/// An example of naming one stat in a group, for messages
		/// </summary>
		/// <param name="type">The group's type</param>
		/// <param name="path">The path to the group</param>
		/// <returns>", e.g. path.field", or empty when the group has no stat that can be set</returns>
		private static string ExampleField(Type type, string path)
		{
			foreach (FieldInfo field in GetFields(type))
			{
				if (TweakValue.CanSet(field.FieldType))
				{
					return(", e.g. " + path + "." + PathName(field));
				}
			}
			return("");
		}

		/// <summary>
		/// Makes a new crafting or building ingredient that costs nothing until its amounts are set
		/// </summary>
		/// <param name="name">The ingredient's item prefab name</param>
		/// <returns>The new ingredient, or an error when there's no such item</returns>
		private static (object entry, string error) CreateRequirement(string name)
		{
			ItemDrop item = (ObjectDB.instance != null) ? Prefabs.FindItem(name) : null;
			if (item == null)
			{
				return((null, "there's no item named \"" + name + "\""));
			}
			Piece.Requirement requirement = new Piece.Requirement
			{
				m_resItem = item,
				m_amount = 0,
				m_amountPerLevel = 0,
				m_recover = true
			};
			return((requirement, null));
		}

		/// <summary>
		/// Makes a new resistance or weakness entry, starting at Normal
		/// </summary>
		/// <param name="name">The damage type's name, e.g. Fire</param>
		/// <returns>The new entry, or an error when there's no such damage type</returns>
		private static (object entry, string error) CreateDamageModPair(string name)
		{
			foreach (string typeName in Enum.GetNames(typeof(HitData.DamageType)))
			{
				if (typeName.Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					HitData.DamageModPair pair = new HitData.DamageModPair
					{
						m_type = (HitData.DamageType)Enum.Parse(typeof(HitData.DamageType), typeName),
						m_modifier = HitData.DamageModifier.Normal
					};
					return((pair, null));
				}
			}
			return((null, "\"" + name + "\" isn't a damage type - use one of " + string.Join(", ", Enum.GetNames(typeof(HitData.DamageType)))));
		}
	}
}
