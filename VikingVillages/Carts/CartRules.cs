using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The rules of a village's cart - which villages keep one, what becomes of it wherever it ends up, and which sides
	/// of a house it's parked beside - worked out apart from the game so they can be tested
	/// </summary>
	internal static class CartRules
	{
		/// <summary>
		/// What a village's cart is up to, as its village sees it (see VillageCarts)
		/// </summary>
		internal enum CartState
		{
			/// <summary>
			/// The village keeps no cart - a Meadows or Ashlands village
			/// </summary>
			None,

			/// <summary>
			/// It has none, or it was destroyed or has been gone too long, so the vikings build one
			/// </summary>
			Missing,

			/// <summary>
			/// It stands in the village
			/// </summary>
			Home,

			/// <summary>
			/// It stands loaded outside the village, so a viking fetches it back
			/// </summary>
			Astray,

			/// <summary>
			/// It's out of reach - somewhere not loaded - and comes back on its own or is built anew once it has been
			/// gone a game day
			/// </summary>
			Away,

			/// <summary>
			/// A player is pulling it or has it open, so the vikings leave it be
			/// </summary>
			InUse
		}

		/// <summary>
		/// Checks whether a village of a biome keeps a cart - not in the Meadows, where it has no bronze for the nails,
		/// nor in the Ashlands
		/// </summary>
		/// <param name="biome">The village's biome's index (see ResourceBook)</param>
		/// <returns>True when it does</returns>
		internal static bool Allowed(int biome)
		{
			return(biome != ResourceBook.Meadows && biome != ResourceBook.AshLands);
		}

		/// <summary>
		/// Works out what a village's cart is up to from what its village can see of it
		/// </summary>
		/// <param name="allowed">Whether the village keeps a cart (see Allowed)</param>
		/// <param name="recorded">Whether the village has noted a cart as its own</param>
		/// <param name="known">Whether this client knows the cart's ZDO</param>
		/// <param name="lastSeenLoaded">Whether where the cart was last seen is loaded here, so a cart that isn't known
		/// there was destroyed</param>
		/// <param name="loaded">Whether the cart itself is loaded here</param>
		/// <param name="playerUsing">Whether a player is pulling it or has it open</param>
		/// <param name="inside">Whether it stands in the village</param>
		/// <param name="awaySeconds">How long it has been out of reach, in seconds, or 0 when it hasn't</param>
		/// <param name="daySeconds">How long a game day lasts, in seconds</param>
		/// <returns>What it's up to</returns>
		internal static CartState Decide(bool allowed, bool recorded, bool known, bool lastSeenLoaded, bool loaded, bool playerUsing, bool inside, double awaySeconds, double daySeconds)
		{
			if (!allowed)
			{
				return(CartState.None);
			}
			if (!recorded || (!known && lastSeenLoaded))
			{
				return(CartState.Missing);
			}
			if (!known || !loaded)
			{
				return((awaySeconds >= daySeconds) ? CartState.Missing : CartState.Away);
			}
			if (playerUsing)
			{
				return(CartState.InUse);
			}
			return(inside ? CartState.Home : CartState.Astray);
		}

		/// <summary>
		/// Lists the sides of a house a cart is parked beside, in the order they're tried - the side opposite its door
		/// first, then the sides to its left and right, never the side its door is on. A house with no door found has
		/// all four sides tried, starting with its back
		/// </summary>
		/// <param name="front">Which way the house's door faces out from its middle, seen from above, or zero when it
		/// has none</param>
		/// <param name="leftFirst">True to try the left side before the right</param>
		/// <returns>Each side's way out from the house's middle, seen from above, one meter long</returns>
		internal static List<Vector2> Sides(Vector2 front, bool leftFirst)
		{
			bool noDoor = (front.sqrMagnitude < 0.0001f);
			Vector2 forward = (noDoor ? new Vector2(0f, 1f) : front.normalized);
			Vector2 back = -forward;
			Vector2 left = new Vector2(-forward.y, forward.x);
			Vector2 right = -left;
			List<Vector2> sides = new List<Vector2> { back };
			sides.Add(leftFirst ? left : right);
			sides.Add(leftFirst ? right : left);
			if (noDoor)
			{
				sides.Add(forward);
			}
			return(sides);
		}

		/// <summary>
		/// Works out how far a cart beside a house stands from the house's middle - past its walls, the gap kept from
		/// them and half the cart's own width
		/// </summary>
		/// <param name="halfSize">How far the house's walls are from its middle, in meters</param>
		/// <param name="wallGap">How far the cart keeps from the walls, in meters</param>
		/// <param name="halfWidth">Half the cart's width across, in meters</param>
		/// <returns>The distance, in meters</returns>
		internal static float SideDistance(float halfSize, float wallGap, float halfWidth)
		{
			return(halfSize + wallGap + halfWidth);
		}

		/// <summary>
		/// Checks whether a spot is too near a door - within some distance of any door or doorstep - for a cart to stand
		/// there, so it never blocks the way in
		/// </summary>
		/// <param name="spot">The spot, seen from above</param>
		/// <param name="doors">The doors and doorsteps, seen from above</param>
		/// <param name="clearance">How far from each the cart keeps, in meters</param>
		/// <returns>True when it's too near one</returns>
		internal static bool BlocksDoor(Vector2 spot, List<Vector2> doors, float clearance)
		{
			foreach (Vector2 door in doors)
			{
				if ((door - spot).magnitude < clearance)
				{
					return(true);
				}
			}
			return(false);
		}
	}
}
