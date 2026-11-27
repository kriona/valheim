using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What a living viking's body works out without its body - when its gear changes start and are done, how much its
	/// rest changes what the game heals it by, how much a fall hurts it, how its weight shows and whose name makes it
	/// immune to frost
	/// </summary>
	internal static class VikingBodyRules
	{
		/// <summary>
		/// Checks whether a viking's name makes it immune to frost - Elsa, whatever its case and byname (see
		/// VikingNames.IsElsa)
		/// </summary>
		/// <param name="name">The viking's name, or null</param>
		/// <returns>True when it does</returns>
		internal static bool FrostImmune(string name)
		{
			return(VikingNames.IsElsa(name));
		}

		/// <summary>
		/// Queues putting on or taking off a piece of gear, taking its equip time from when the last change queued is
		/// done, or from now when nothing is queued. A one-handed weapon or shield joins the last change queued when
		/// that's the same change to the other hand, with nothing joined to it yet, and the change is done when the slower
		/// of the two is
		/// </summary>
		/// <param name="changes">The changes queued, in order, which a new change goes on the end of</param>
		/// <param name="now">The time now, in seconds</param>
		/// <param name="type">The piece's item type</param>
		/// <param name="equipDuration">The piece's equip time, in seconds, where below 0 counts as 0</param>
		/// <param name="putOn">True to put it on, false to take it off</param>
		/// <param name="chosen">Whether a player picked it for the viking to put on</param>
		/// <param name="found">Whether the viking found it as loot or in a chest</param>
		/// <returns>The change the piece is part of - the last one, Paired, when it joined it as the other hand's piece,
		/// or else a new one for it</returns>
		internal static PlayerLikeVikingBody.GearChange QueueChange(List<PlayerLikeVikingBody.GearChange> changes, float now, ItemDrop.ItemData.ItemType type, float equipDuration, bool putOn, bool chosen, bool found)
		{
			float duration = Mathf.Max(0f, equipDuration);
			PlayerLikeVikingBody.GearChange last = ((changes.Count > 0) ? changes[changes.Count - 1] : null);
			if (last != null && !last.Paired && last.PutOn == putOn && last.Chosen == chosen && last.Found == found && HandPair(last.Type, type))
			{
				last.Paired = true;
				last.DoneAt = Mathf.Max(last.DoneAt, last.Start + duration);
				return(last);
			}
			float start = ((last != null) ? last.DoneAt : now);
			PlayerLikeVikingBody.GearChange change = new PlayerLikeVikingBody.GearChange { Type = type, PutOn = putOn, Chosen = chosen, Found = found, Start = start, DoneAt = start + duration };
			changes.Add(change);
			return(change);
		}

		/// <summary>
		/// Checks whether two pieces of gear go in a hand each - a one-handed weapon and a shield - so changing them is
		/// one change, done together
		/// </summary>
		/// <param name="first">One piece's item type</param>
		/// <param name="second">The other's</param>
		/// <returns>True when they do</returns>
		internal static bool HandPair(ItemDrop.ItemData.ItemType first, ItemDrop.ItemData.ItemType second)
		{
			return((first == ItemDrop.ItemData.ItemType.OneHandedWeapon && second == ItemDrop.ItemData.ItemType.Shield) || (first == ItemDrop.ItemData.ItemType.Shield && second == ItemDrop.ItemData.ItemType.OneHandedWeapon));
		}

		/// <summary>
		/// Works out how much more or less the viking heals than the game healed it, by its rest's regen multiplier -
		/// more while resting or rested, less while wet or cold
		/// </summary>
		/// <param name="healed">How much the game healed it</param>
		/// <param name="multiplier">Its regen multiplier, 1 for no change</param>
		/// <returns>The health to add, below 0 for health to take back, or 0 when the game didn't heal it</returns>
		internal static float RegenDelta(float healed, float multiplier)
		{
			return((healed > 0f) ? healed * (multiplier - 1f) : 0f);
		}

		/// <summary>
		/// How far past Character.c_MinFallHeight a fall's damage rises to Character.c_MaxFallDamage, in meters - the
		/// game's own number for a player (see Character.UpdateGroundContact)
		/// </summary>
		private const float FallSpread = 16f;

		/// <summary>
		/// Works out how much a fall hurts the viking, the same as a player - nothing up to Character.c_MinFallHeight,
		/// rising evenly to Character.c_MaxFallDamage at FallSpread meters past it
		/// </summary>
		/// <param name="height">How far it fell, in meters</param>
		/// <returns>The damage, before what its effects take off it</returns>
		internal static float FallDamage(float height)
		{
			if (height <= Character.c_MinFallHeight)
			{
				return(0f);
			}
			return(Mathf.Clamp01((height - Character.c_MinFallHeight) / FallSpread) * Character.c_MaxFallDamage);
		}

		/// <summary>
		/// Writes how much the viking carries and the most it can, like "120/300", with the weight in red while it
		/// carries more than it can and the flash is on
		/// </summary>
		/// <param name="weight">The weight it carries</param>
		/// <param name="max">The most it can carry</param>
		/// <param name="flash">Whether the flash is on right now, as it blinks</param>
		/// <returns>The text</returns>
		internal static string FormatWeight(int weight, int max, bool flash)
		{
			bool red = (flash && weight > max);
			return((red ? "<color=red>" + weight + "</color>" : weight.ToString()) + "/" + max);
		}
	}
}
