using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace TweakStats
{
	/// <summary>
	/// A creature or build piece, whose stats are spread over the components on its prefab
	/// </summary>
	/// <remarks>
	/// A path's first part names a stat on any of the components, searched in order - the Character or Piece first,
	/// then WearNTear, the AI and the drops - or names a component by its type to reach a stat another component
	/// shadows, e.g. MonsterAI.viewRange. A creature's attacks.damages changes every weapon it attacks with, and
	/// attacks.troll_groundslam.damages one of them
	/// </remarks>
	public class PrefabTarget : Target
	{
		/// <summary>
		/// Component types whose stats come first, in this order - the rest follow in the prefab's own order
		/// </summary>
		private static readonly Type[] mainTypes = new Type[] { typeof(Character), typeof(Piece), typeof(WearNTear), typeof(BaseAI), typeof(CharacterDrop), typeof(Tameable) };

		/// <summary>
		/// Item types a creature attacks with, as opposed to armor and shields it wears
		/// </summary>
		private static readonly ItemDrop.ItemData.ItemType[] attackTypes = new ItemDrop.ItemData.ItemType[]
		{
			ItemDrop.ItemData.ItemType.OneHandedWeapon,
			ItemDrop.ItemData.ItemType.TwoHandedWeapon,
			ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft,
			ItemDrop.ItemData.ItemType.Bow,
			ItemDrop.ItemData.ItemType.Torch,
			ItemDrop.ItemData.ItemType.Attach_Atgeir
		};

		private readonly List<Component> components;

		/// <summary>
		/// The weapons a creature attacks with, or null for a build piece or a creature that can't carry items
		/// </summary>
		private readonly List<ItemDrop> attacks;

		/// <summary>
		/// Gathers the components and attacks of a creature or build piece prefab
		/// </summary>
		/// <param name="prefab">The prefab</param>
		/// <param name="header">The section header naming only this prefab, e.g. Creature:Troll</param>
		public PrefabTarget(GameObject prefab, string header)
		{
			Root = prefab;
			Header = header;
			NeedsServer = true;
			components = Components(prefab);
			attacks = Attacks(prefab);
		}

		/// <summary>
		/// Changes the stat at a path, on whichever component or attack it names
		/// </summary>
		/// <param name="path">The stat's dotted path</param>
		/// <param name="value">The value to apply</param>
		/// <param name="error">Why the stat couldn't be changed, or null</param>
		/// <returns>True when nothing went wrong</returns>
		public override bool TryApply(string path, TweakValue value, out string error)
		{
			string first = FirstPart(path);
			string rest = RestOfPath(path);
			if (attacks != null && first.Equals("attacks", StringComparison.OrdinalIgnoreCase))
			{
				return(TryApplyToAttacks(rest, value, out error));
			}
			Component named = FindComponent(first);
			if (named != null)
			{
				if (rest == "")
				{
					error = first + " is a part of the prefab, so it needs the name of one of its stats, e.g. " + first + ".<stat>";
					return(false);
				}
				return(TryApplyToComponent(named, rest, value, out error));
			}
			foreach (Component component in components)
			{
				if (StatPath.FindField(component.GetType(), first) != null)
				{
					return(TryApplyToComponent(component, path, value, out error));
				}
			}
			error = "there's no stat \"" + first + "\"" + StatPath.Suggest(components.ConvertAll(component => component.GetType()), first);
			return(false);
		}

		/// <summary>
		/// Lists every stat of every component, then of every attack, with its current value
		/// </summary>
		/// <param name="lines">The list to add "path = value" lines to</param>
		public override void List(List<string> lines)
		{
			HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (Component component in components)
			{
				List<string> componentLines = new List<string>();
				StatPath.List(component, componentLines);
				HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach (string line in componentLines)
				{
					string name = FirstPart(line.Substring(0, line.IndexOf(" = ", StringComparison.Ordinal)));
					names.Add(name);
					lines.Add((used.Contains(name) ? component.GetType().Name + "." : "") + line);
				}
				used.UnionWith(names);
			}
			if (attacks == null)
			{
				return;
			}
			foreach (ItemDrop attack in attacks)
			{
				List<string> attackLines = new List<string>();
				StatPath.List(attack.m_itemData.m_shared, attackLines);
				foreach (string line in attackLines)
				{
					lines.Add("attacks." + attack.gameObject.name + "." + line);
				}
			}
		}

		/// <summary>
		/// Changes a stat on one component, recording which of its fields changed so the copies in the world can be
		/// updated
		/// </summary>
		/// <param name="component">The component on the prefab</param>
		/// <param name="path">The stat's path within the component</param>
		/// <param name="value">The value to apply</param>
		/// <param name="error">Why the stat couldn't be changed, or null</param>
		/// <returns>True when nothing went wrong</returns>
		private static bool TryApplyToComponent(Component component, string path, TweakValue value, out string error)
		{
			if (!StatPath.TryApply(component, path, value, out error))
			{
				return(false);
			}
			FieldInfo field = StatPath.FindField(component.GetType(), FirstPart(path));
			if (field != null)
			{
				Instances.Touch(component, field);
			}
			return(true);
		}

		/// <summary>
		/// Changes a stat on every weapon the creature attacks with, or on the one the path names first
		/// </summary>
		/// <param name="path">The path after attacks., e.g. damages or troll_groundslam.damages</param>
		/// <param name="value">The value to apply</param>
		/// <param name="error">Why the stat couldn't be changed, or null</param>
		/// <returns>True when nothing went wrong</returns>
		private bool TryApplyToAttacks(string path, TweakValue value, out string error)
		{
			error = null;
			List<ItemDrop> chosen = attacks;
			string first = FirstPart(path);
			foreach (ItemDrop attack in attacks)
			{
				if (attack.gameObject.name.Equals(first, StringComparison.OrdinalIgnoreCase))
				{
					chosen = new List<ItemDrop> { attack };
					path = RestOfPath(path);
					break;
				}
			}
			if (path == "")
			{
				error = "attacks needs the name of a stat, e.g. attacks.damages, or an attack and a stat, e.g. attacks." + ((attacks.Count > 0) ? attacks[0].gameObject.name : "<attack>") + ".damages";
				return(false);
			}
			foreach (ItemDrop attack in chosen)
			{
				if (!StatPath.TryApply(attack.m_itemData.m_shared, path, value, out error))
				{
					return(false);
				}
			}
			return(true);
		}

		/// <summary>
		/// Finds a component by the name of its type or one of the types it inherits, ignoring case
		/// </summary>
		/// <param name="name">The type's name, e.g. MonsterAI or BaseAI</param>
		/// <returns>The component, or null</returns>
		private Component FindComponent(string name)
		{
			foreach (Component component in components)
			{
				for (Type type = component.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
				{
					if (type.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
					{
						return(component);
					}
				}
			}
			return(null);
		}

		/// <summary>
		/// The components on a prefab's root object whose stats can be changed, with the main ones first
		/// </summary>
		/// <param name="prefab">The prefab</param>
		/// <returns>The components, leaving out the ones that network the object</returns>
		private static List<Component> Components(GameObject prefab)
		{
			List<Component> rest = new List<Component>();
			foreach (MonoBehaviour component in prefab.GetComponents<MonoBehaviour>())
			{
				if (component == null || component is ZNetView || component.GetType().Name.StartsWith("ZSync", StringComparison.Ordinal))
				{
					continue;
				}
				rest.Add(component);
			}
			List<Component> ordered = new List<Component>();
			foreach (Type type in mainTypes)
			{
				foreach (Component component in rest)
				{
					if (type.IsInstanceOfType(component) && !ordered.Contains(component))
					{
						ordered.Add(component);
					}
				}
			}
			foreach (Component component in rest)
			{
				if (!ordered.Contains(component))
				{
					ordered.Add(component);
				}
			}
			return(ordered);
		}

		/// <summary>
		/// The weapons a creature can be given, from its default and random items
		/// </summary>
		/// <param name="prefab">The creature's prefab</param>
		/// <returns>The weapons without repeats, or null when the prefab isn't a creature that carries items</returns>
		private static List<ItemDrop> Attacks(GameObject prefab)
		{
			Humanoid humanoid = prefab.GetComponent<Humanoid>();
			if (humanoid == null)
			{
				return(null);
			}
			List<GameObject> candidates = new List<GameObject>();
			AddRange(candidates, humanoid.m_defaultItems);
			AddRange(candidates, humanoid.m_randomWeapon);
			if (humanoid.m_randomSets != null)
			{
				foreach (Humanoid.ItemSet set in humanoid.m_randomSets)
				{
					AddRange(candidates, set.m_items);
				}
			}
			if (humanoid.m_randomItems != null)
			{
				foreach (Humanoid.RandomItem item in humanoid.m_randomItems)
				{
					candidates.Add(item.m_prefab);
				}
			}
			List<ItemDrop> weapons = new List<ItemDrop>();
			foreach (GameObject candidate in candidates)
			{
				ItemDrop item = (candidate != null) ? candidate.GetComponent<ItemDrop>() : null;
				if (item != null && !weapons.Contains(item) && Array.IndexOf(attackTypes, item.m_itemData.m_shared.m_itemType) >= 0)
				{
					weapons.Add(item);
				}
			}
			return(weapons);
		}

		/// <summary>
		/// Adds the prefabs in an array to a list, when there's an array
		/// </summary>
		/// <param name="list">The list</param>
		/// <param name="prefabs">The prefabs, or null</param>
		private static void AddRange(List<GameObject> list, GameObject[] prefabs)
		{
			if (prefabs != null)
			{
				list.AddRange(prefabs);
			}
		}

		/// <summary>
		/// The first part of a dotted path
		/// </summary>
		/// <param name="path">The path</param>
		/// <returns>The text before the first dot, trimmed</returns>
		private static string FirstPart(string path)
		{
			int dot = path.IndexOf('.');
			return(((dot >= 0) ? path.Substring(0, dot) : path).Trim());
		}

		/// <summary>
		/// A dotted path without its first part
		/// </summary>
		/// <param name="path">The path</param>
		/// <returns>The text after the first dot, or empty when there's no dot</returns>
		private static string RestOfPath(string path)
		{
			int dot = path.IndexOf('.');
			return((dot >= 0) ? path.Substring(dot + 1).Trim() : "");
		}
	}
}
