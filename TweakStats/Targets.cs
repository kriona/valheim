using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TweakStats
{
	/// <summary>
	/// An object a section changes, e.g. an item's shared data, a recipe or a status effect
	/// </summary>
	public class Target
	{
		/// <summary>
		/// The object stat paths start from, which also tells targets apart
		/// </summary>
		public object Root;

		/// <summary>
		/// The section header that names only this object, e.g. SwordIron or Recipe:SwordIron
		/// </summary>
		public string Header;

		/// <summary>
		/// Whether changing the object needs every player to use the same config - it only applies when this game runs
		/// the world or uses the server's config, since another player's game may run the object with its own values
		/// </summary>
		public bool NeedsServer;

		/// <summary>
		/// Changes the stat at a path
		/// </summary>
		/// <param name="path">The stat's dotted path</param>
		/// <param name="value">The value to apply</param>
		/// <param name="error">Why the stat couldn't be changed, or null</param>
		/// <returns>True when nothing went wrong</returns>
		public virtual bool TryApply(string path, TweakValue value, out string error)
		{
			return(StatPath.TryApply(Root, path, value, out error));
		}

		/// <summary>
		/// Lists every stat with its current value, as config lines
		/// </summary>
		/// <param name="lines">The list to add "path = value" lines to</param>
		public virtual void List(List<string> lines)
		{
			StatPath.List(Root, lines);
		}
	}

	/// <summary>
	/// Finds the objects a section header names
	/// </summary>
	public static class Targets
	{
		/// <summary>
		/// Finds the objects each selector of a section names, without repeats
		/// </summary>
		/// <param name="selectors">The selectors, e.g. SwordIron, Sword*, Skill:Swords or Recipe:SwordIron</param>
		/// <param name="problems">A message for each selector that names nothing</param>
		/// <param name="skipped">Each creature or build piece selector left out because this game doesn't run the
		/// world, or null to look them up anyway</param>
		/// <returns>The objects</returns>
		public static List<Target> Resolve(List<string> selectors, List<string> problems, List<string> skipped)
		{
			List<Target> targets = new List<Target>();
			HashSet<object> seen = new HashSet<object>(ReferenceComparer.Instance);
			foreach (string selector in selectors)
			{
				if (skipped != null && !Tweaks.WorldTweaksApply && IsWorldKind(selector))
				{
					skipped.Add(selector);
					continue;
				}
				List<Target> found = Resolve(selector, out string error);
				foreach (Target target in found)
				{
					if (seen.Add(target.Root))
					{
						targets.Add(target);
					}
				}
				if (error != null)
				{
					problems.Add(error);
				}
			}
			return(targets);
		}

		/// <summary>
		/// Finds the objects one selector names
		/// </summary>
		/// <param name="selector">The selector</param>
		/// <param name="error">Why the selector names nothing, or null</param>
		/// <returns>The objects</returns>
		private static List<Target> Resolve(string selector, out string error)
		{
			List<Target> targets = new List<Target>();
			error = null;
			int colon = selector.IndexOf(':');
			string kind = (colon >= 0) ? selector.Substring(0, colon).Trim() : "";
			string name = (colon >= 0) ? selector.Substring(colon + 1).Trim() : selector.Trim();
			if (kind == "")
			{
				Regex pattern = Pattern(name);
				foreach (ItemDrop item in Prefabs.Items())
				{
					if (pattern.IsMatch(item.gameObject.name))
					{
						targets.Add(ItemTarget(item));
					}
				}
				if (targets.Count == 0)
				{
					error = (IsWildcard(name) ? "no items match \"" + name + "\"" : "there's no item named \"" + name + "\" - the tweakstats find console command looks up an item's name");
				}
			}
			else if (kind.Equals("Skill", StringComparison.OrdinalIgnoreCase))
			{
				if (!TryParseEnum(name, out Skills.SkillType skill))
				{
					error = "\"" + name + "\" isn't a skill - use one of " + string.Join(", ", Enum.GetNames(typeof(Skills.SkillType)));
					return(targets);
				}
				foreach (ItemDrop item in Prefabs.Items())
				{
					if (item.m_itemData.m_shared.m_skillType == skill)
					{
						targets.Add(ItemTarget(item));
					}
				}
			}
			else if (kind.Equals("Type", StringComparison.OrdinalIgnoreCase))
			{
				if (!TryParseEnum(name, out ItemDrop.ItemData.ItemType type))
				{
					error = "\"" + name + "\" isn't an item type - use one of " + string.Join(", ", Enum.GetNames(typeof(ItemDrop.ItemData.ItemType)));
					return(targets);
				}
				foreach (ItemDrop item in Prefabs.Items())
				{
					if (item.m_itemData.m_shared.m_itemType == type)
					{
						targets.Add(ItemTarget(item));
					}
				}
			}
			else if (kind.Equals("Recipe", StringComparison.OrdinalIgnoreCase))
			{
				Regex pattern = Pattern(name);
				foreach (Recipe recipe in Prefabs.Recipes())
				{
					if (pattern.IsMatch(recipe.m_item.gameObject.name) || pattern.IsMatch(recipe.name))
					{
						targets.Add(new Target
						{
							Root = recipe,
							Header = "Recipe:" + recipe.name
						});
					}
				}
				if (targets.Count == 0)
				{
					error = (IsWildcard(name) ? "no recipes match \"" + name + "\"" : "there's no recipe for \"" + name + "\"");
				}
			}
			else if (kind.Equals("Creature", StringComparison.OrdinalIgnoreCase))
			{
				Regex pattern = Pattern(name);
				foreach (GameObject prefab in Prefabs.Creatures())
				{
					if (pattern.IsMatch(prefab.name))
					{
						targets.Add(new PrefabTarget(prefab, "Creature:" + prefab.name));
					}
				}
				if (targets.Count == 0)
				{
					error = (IsWildcard(name) ? "no creatures match \"" + name + "\"" : "there's no creature named \"" + name + "\"");
				}
			}
			else if (kind.Equals("Piece", StringComparison.OrdinalIgnoreCase))
			{
				Regex pattern = Pattern(name);
				foreach (GameObject prefab in Prefabs.Pieces())
				{
					if (pattern.IsMatch(prefab.name))
					{
						targets.Add(new PrefabTarget(prefab, "Piece:" + prefab.name));
					}
				}
				if (targets.Count == 0)
				{
					error = (IsWildcard(name) ? "no build pieces match \"" + name + "\"" : "there's no build piece named \"" + name + "\"");
				}
			}
			else if (kind.Equals("Effect", StringComparison.OrdinalIgnoreCase))
			{
				Regex pattern = Pattern(name);
				HashSet<StatusEffect> hitEffects = Prefabs.HitEffects();
				foreach (StatusEffect effect in Prefabs.Effects())
				{
					if (pattern.IsMatch(effect.name))
					{
						targets.Add(new Target
						{
							Root = effect,
							Header = "Effect:" + effect.name,
							NeedsServer = hitEffects.Contains(effect)
						});
					}
				}
				if (targets.Count == 0)
				{
					error = (IsWildcard(name) ? "no status effects match \"" + name + "\"" : "there's no status effect named \"" + name + "\"");
				}
			}
			else
			{
				error = "\"" + kind + ":\" isn't something TweakStats can change - use an item name, Skill:, Type:, Recipe:, Creature:, Piece: or Effect:";
			}
			return(targets);
		}

		/// <summary>
		/// Whether a selector names creatures or build pieces, whose tweaks need every player to use the same config
		/// </summary>
		/// <param name="selector">The selector</param>
		/// <returns>True for Creature: and Piece: selectors</returns>
		private static bool IsWorldKind(string selector)
		{
			string trimmed = selector.TrimStart();
			return(trimmed.StartsWith("Creature:", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("Piece:", StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// The target for an item's stats
		/// </summary>
		/// <param name="item">The item's ItemDrop on its prefab</param>
		/// <returns>A target rooted at the item's shared data</returns>
		private static Target ItemTarget(ItemDrop item)
		{
			return(new Target
			{
				Root = item.m_itemData.m_shared,
				Header = item.gameObject.name
			});
		}

		/// <summary>
		/// Whether a name has wildcards in it
		/// </summary>
		/// <param name="name">The name</param>
		/// <returns>True when it contains * or ?</returns>
		private static bool IsWildcard(string name)
		{
			return(name.IndexOf('*') >= 0 || name.IndexOf('?') >= 0);
		}

		/// <summary>
		/// Makes a pattern matching a whole name, ignoring case, where * matches any text and ? any one character
		/// </summary>
		/// <param name="name">The name, which may have wildcards</param>
		/// <returns>The pattern</returns>
		private static Regex Pattern(string name)
		{
			string pattern = "^" + Regex.Escape(name).Replace("\\*", ".*").Replace("\\?", ".") + "$";
			return(new Regex(pattern, RegexOptions.IgnoreCase));
		}

		/// <summary>
		/// Reads one of an enum's names, ignoring case
		/// </summary>
		/// <typeparam name="T">The enum type</typeparam>
		/// <param name="name">The name</param>
		/// <param name="value">The value</param>
		/// <returns>True when the name is one of the enum's names</returns>
		private static bool TryParseEnum<T>(string name, out T value) where T : struct
		{
			value = default(T);
			foreach (string enumName in Enum.GetNames(typeof(T)))
			{
				if (enumName.Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					value = (T)Enum.Parse(typeof(T), enumName);
					return(true);
				}
			}
			return(false);
		}
	}
}
