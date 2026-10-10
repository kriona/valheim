using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace TweakStats
{
	/// <summary>
	/// Keeps the creatures and build pieces already in the world in step with their tweaked prefabs
	/// </summary>
	/// <remarks>
	/// Unity gives every creature and piece its own copy of its components' fields when it appears, so a tweak to a
	/// prefab only reaches the ones that appear afterwards. After each apply, every field tweaked then or the time
	/// before is copied from the prefab to the copies in the world, which also puts back a field whose tweak was removed
	/// </remarks>
	public static class Instances
	{
		private static readonly FieldInfo characterHealth = typeof(Character).GetField(nameof(Character.m_health));
		private static readonly FieldInfo pieceHealth = typeof(WearNTear).GetField(nameof(WearNTear.m_health));

		/// <summary>
		/// The fields tweaked on each prefab component by the current apply
		/// </summary>
		private static Dictionary<object, HashSet<FieldInfo>> touched = new Dictionary<object, HashSet<FieldInfo>>(ReferenceComparer.Instance);

		/// <summary>
		/// The fields tweaked by the apply before, whose copies need their original values back if they weren't tweaked
		/// again
		/// </summary>
		private static Dictionary<object, HashSet<FieldInfo>> previous = new Dictionary<object, HashSet<FieldInfo>>(ReferenceComparer.Instance);

		/// <summary>
		/// Records that a field of a prefab component was tweaked
		/// </summary>
		/// <param name="component">The component on the prefab</param>
		/// <param name="field">The component's field</param>
		public static void Touch(Component component, FieldInfo field)
		{
			if (!touched.TryGetValue(component, out HashSet<FieldInfo> fields))
			{
				fields = new HashSet<FieldInfo>();
				touched.Add(component, fields);
			}
			fields.Add(field);
		}

		/// <summary>
		/// Starts recording the fields a new apply tweaks, keeping the last apply's to put back
		/// </summary>
		public static void StartApply()
		{
			previous = touched;
			touched = new Dictionary<object, HashSet<FieldInfo>>(ReferenceComparer.Instance);
		}

		/// <summary>
		/// Copies every field tweaked by this apply or the one before from each prefab to its copies in the world
		/// </summary>
		public static void Sync()
		{
			Dictionary<object, HashSet<FieldInfo>> fields = new Dictionary<object, HashSet<FieldInfo>>(ReferenceComparer.Instance);
			Merge(fields, previous);
			Merge(fields, touched);
			if (fields.Count == 0 || ZNetScene.instance == null)
			{
				return;
			}
			Dictionary<System.Type, Dictionary<string, Component>> prefabsByType = new Dictionary<System.Type, Dictionary<string, Component>>();
			foreach (object key in fields.Keys)
			{
				Component prefabComponent = key as Component;
				if (prefabComponent == null)
				{
					continue;
				}
				System.Type type = prefabComponent.GetType();
				if (!prefabsByType.TryGetValue(type, out Dictionary<string, Component> byName))
				{
					byName = new Dictionary<string, Component>();
					prefabsByType.Add(type, byName);
				}
				byName[prefabComponent.gameObject.name] = prefabComponent;
			}
			foreach (KeyValuePair<System.Type, Dictionary<string, Component>> group in prefabsByType)
			{
				foreach (Object found in Object.FindObjectsByType(group.Key, FindObjectsSortMode.None))
				{
					Component instance = (Component)found;
					if (group.Value.TryGetValue(Utils.GetPrefabName(instance.gameObject.name), out Component prefabComponent) && prefabComponent != instance)
					{
						Copy(prefabComponent, instance, fields[prefabComponent]);
					}
				}
			}
		}

		/// <summary>
		/// Gives each player and creature this game owns its equipment's status effects again, so a tweaked
		/// equipStatusEffect, set bonus or effect stat reaches equipment already being worn
		/// </summary>
		/// <remarks>
		/// The game only works out equipment effects when equipment changes, and an active effect is a copy made when it
		/// was added, so the current ones are removed first and added back fresh. Every other player's game does the same
		/// for its own player
		/// </remarks>
		public static void RefreshEquipmentEffects()
		{
			foreach (Character character in Character.s_characters)
			{
				Humanoid humanoid = character as Humanoid;
				if (humanoid == null || humanoid.m_nview == null || !humanoid.m_nview.IsValid() || !humanoid.m_nview.IsOwner())
				{
					continue;
				}
				foreach (StatusEffect effect in humanoid.m_equipmentStatusEffects)
				{
					humanoid.m_seman.RemoveStatusEffect(effect, true);
				}
				humanoid.m_equipmentStatusEffects.Clear();
				humanoid.UpdateEquipmentStatusEffects();
			}
		}

		/// <summary>
		/// Adds the fields of one record to another
		/// </summary>
		/// <param name="into">The record to add to</param>
		/// <param name="from">The record to add</param>
		private static void Merge(Dictionary<object, HashSet<FieldInfo>> into, Dictionary<object, HashSet<FieldInfo>> from)
		{
			foreach (KeyValuePair<object, HashSet<FieldInfo>> entry in from)
			{
				if (!into.TryGetValue(entry.Key, out HashSet<FieldInfo> fields))
				{
					fields = new HashSet<FieldInfo>();
					into.Add(entry.Key, fields);
				}
				fields.UnionWith(entry.Value);
			}
		}

		/// <summary>
		/// Copies fields from a prefab's component to a copy of it in the world, then brings the copy's health in line
		/// </summary>
		/// <param name="prefabComponent">The component on the prefab</param>
		/// <param name="instance">The same component on the copy</param>
		/// <param name="fields">The fields to copy</param>
		private static void Copy(Component prefabComponent, Component instance, HashSet<FieldInfo> fields)
		{
			foreach (FieldInfo field in fields)
			{
				field.SetValue(instance, field.GetValue(prefabComponent));
			}
			if (ContainsField(fields, characterHealth) && instance is Character character)
			{
				ResetMaxHealth(character);
			}
			if (ContainsField(fields, pieceHealth) && instance is WearNTear piece)
			{
				ScalePieceHealth(piece);
			}
		}

		/// <summary>
		/// Whether a set holds a field, however it was looked up - a field found through a subclass, like Humanoid's
		/// m_health, isn't equal to the same field found through the class declaring it
		/// </summary>
		/// <param name="fields">The set</param>
		/// <param name="field">The field</param>
		/// <returns>True when the set holds a field with the same declaring type and name</returns>
		private static bool ContainsField(HashSet<FieldInfo> fields, FieldInfo field)
		{
			foreach (FieldInfo candidate in fields)
			{
				if (candidate.DeclaringType == field.DeclaringType && candidate.Name == field.Name)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Works out a creature's max health again from its new base health, when this game owns it, keeping it at full
		/// health if it was
		/// </summary>
		/// <param name="character">The creature</param>
		private static void ResetMaxHealth(Character character)
		{
			if (character.m_nview == null || !character.m_nview.IsValid() || !character.m_nview.IsOwner())
			{
				return;
			}
			bool wasFull = (character.GetHealth() >= character.GetMaxHealth());
			character.SetupMaxHealth();
			if (wasFull)
			{
				character.SetHealth(character.GetMaxHealth());
			}
		}

		/// <summary>
		/// Adds the world level's extra health to a piece's copied health, as the game does when a piece appears, and
		/// updates how damaged it looks
		/// </summary>
		/// <param name="piece">The piece</param>
		private static void ScalePieceHealth(WearNTear piece)
		{
			if (Game.m_worldLevel > 0 && Game.instance != null)
			{
				piece.m_health += Game.m_worldLevel * Game.instance.m_worldLevelPieceHPMultiplier * piece.m_health;
			}
			if (piece.m_nview != null && piece.m_nview.IsValid())
			{
				piece.m_healthPercentage = Mathf.Clamp01(piece.m_nview.GetZDO().GetFloat(ZDOVars.s_health, piece.m_health) / piece.m_health);
			}
		}
	}
}
