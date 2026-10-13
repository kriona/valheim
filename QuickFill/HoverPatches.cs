using HarmonyLib;
using UnityEngine;

namespace QuickFill
{
	/// <summary>
	/// Looking at the body of a smelter, kiln, blast furnace, oven, shield generator, etc. targets one of its switches,
	/// so the small switch colliders don't have to be hit
	/// </summary>
	/// <remarks>
	/// A smelter's body is split side to side and an oven's top to bottom, at the midpoint between its two switches,
	/// and each half targets the switch on that side, while all of a shield generator targets its fuel switch. The
	/// hovered object itself is replaced, so the hover text, the use key, the fill and using a hotbar item all act on
	/// that switch
	/// </remarks>
	[HarmonyPatch(typeof(Player), nameof(Player.FindHoverObject))]
	public static class StationHoverPatch
	{
		/// <summary>
		/// Replaces a hovered station body with the switch on the side being looked at
		/// </summary>
		/// <param name="__instance">The player looking around</param>
		/// <param name="hover">The object the game found under the crosshair, replaced with the chosen switch</param>
		private static void Postfix(Player __instance, ref GameObject hover)
		{
			if (hover == null || hover.GetComponentInParent<Switch>() != null)
			{
				return;
			}
			Hoverable hoverable = hover.GetComponentInParent<Hoverable>();
			Switch first;
			Switch second;
			bool vertical;
			Smelter smelter = hover.GetComponentInParent<Smelter>();
			CookingStation station = hover.GetComponentInParent<CookingStation>();
			ShieldGenerator generator = hover.GetComponentInParent<ShieldGenerator>();
			if (smelter != null && hoverable == null)
			{
				first = smelter.m_addOreSwitch;
				second = smelter.m_addWoodSwitch;
				vertical = false;
			}
			else if (station != null && station.m_addFoodSwitch != null && ReferenceEquals(hoverable, station))
			{
				first = station.m_addFoodSwitch;
				second = station.m_addFuelSwitch;
				vertical = true;
			}
			else if (generator != null && !generator.m_enableAttack && ReferenceEquals(hoverable, generator))
			{
				// A generator whose body is used to attack keeps its body as the target
				first = generator.m_addFuelSwitch;
				second = null;
				vertical = false;
			}
			else
			{
				return;
			}
			if (!GetHitPoint(__instance, out Vector3 point))
			{
				return;
			}
			Switch target = PickSwitch(first, second, point, hover.transform.up, vertical);
			if (target != null)
			{
				hover = target.gameObject;
			}
		}

		/// <summary>
		/// Finds where the hover ray hit, using the hits the game's FindHoverObject just sorted by distance and
		/// skipping the player's own colliders the same way it does
		/// </summary>
		/// <param name="player">The player looking around</param>
		/// <param name="point">Set to the hit point</param>
		/// <returns>Whether a hit was found</returns>
		private static bool GetHitPoint(Player player, out Vector3 point)
		{
			foreach (RaycastHit hit in player.m_raycastHoverHits)
			{
				if (hit.collider == null)
				{
					break;
				}
				if (hit.collider.attachedRigidbody && hit.collider.attachedRigidbody.gameObject == player.gameObject)
				{
					continue;
				}
				point = hit.point;
				return(true);
			}
			point = Vector3.zero;
			return(false);
		}

		/// <summary>
		/// Picks the switch on the same side of the midpoint between the two switches as the hit point
		/// </summary>
		/// <param name="first">One switch, or null if the station doesn't have it</param>
		/// <param name="second">The other switch, or null if the station doesn't have it</param>
		/// <param name="point">Where the station was hit</param>
		/// <param name="up">The station's up direction</param>
		/// <param name="vertical">True to split top to bottom, false to split side to side</param>
		/// <returns>The chosen switch, or null if the station has no usable switch</returns>
		private static Switch PickSwitch(Switch first, Switch second, Vector3 point, Vector3 up, bool vertical)
		{
			bool hasFirst = IsUsable(first);
			bool hasSecond = IsUsable(second);
			if (!hasFirst || !hasSecond)
			{
				return(hasFirst ? first : (hasSecond ? second : null));
			}
			Vector3 firstPosition = first.transform.position;
			Vector3 secondPosition = second.transform.position;
			Vector3 between = secondPosition - firstPosition;
			Vector3 axis = (vertical ? Vector3.Project(between, up) : Vector3.ProjectOnPlane(between, up));
			// Switches level with each other (or stacked, for a side to side split) still split on the line between them
			if (axis.sqrMagnitude < 0.0001f)
			{
				axis = between;
			}
			Vector3 middle = (firstPosition + secondPosition) / 2f;
			return((Vector3.Dot(point - middle, axis) < 0f) ? first : second);
		}

		/// <summary>
		/// Whether a switch exists and is active
		/// </summary>
		/// <param name="sw">The switch to check</param>
		/// <returns>True when the switch can be targeted</returns>
		private static bool IsUsable(Switch sw)
		{
			return(sw != null && sw.gameObject.activeInHierarchy);
		}
	}
}
