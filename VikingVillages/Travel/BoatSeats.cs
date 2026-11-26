using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// Where the crew of each of the game's boats sits (see VikingBoat), in the order they're given out - the helm first,
	/// then the ship's own seats, then spots on its deck where there's room to sit on the boards, crowded on the smaller
	/// boats - as many as the boat carries (see TravelTime.BoatCrew)
	/// </summary>
	/// <remarks>
	/// A seat of the ship's own is its helm or a chair by the name of its object - a stool, the mast's holdfast or the
	/// figurehead's - sat in the way the ship has it sat in (see Chair.m_attachAnimation), or a seat in line with a
	/// chair, moved across the ship - the drakkar's steps are sat on that way, evenly spaced along the line of its
	/// benches. A spot on the deck is where it stands from the ship's middle, in meters across, up and along it, forward
	/// being toward the bow, sat on with the floor sit facing the bow - its height found on whatever is under it as the
	/// boat loads, the deck or the longship's cargo, with the height here kept for when nothing is found. The spots were
	/// picked from each ship's layout to keep clear of its mast, chest, ladders and stools
	/// </remarks>
	internal static class BoatSeats
	{
		/// <summary>
		/// A seat - a chair of the ship's by its object's name, or Helm for the helm, or else a spot on the deck - and for a
		/// seat beside a chair, in line with it, sat in the same way, that it's moved across the ship to X
		/// </summary>
		internal struct Plan
		{
			public string Chair;
			public float X;
			public float Y;
			public float Z;
			public bool Beside;

			/// <summary>
			/// Checks whether the seat is a spot on the deck rather than one of the ship's own
			/// </summary>
			public bool OnDeck
			{
				get
				{
					return(string.IsNullOrEmpty(Chair));
				}
			}
		}

		/// <summary>
		/// The name a plan gives the helm by
		/// </summary>
		public const string Helm = "helm";

		/// <summary>
		/// How far above a deck spot's height the deck under it is looked for from, in meters
		/// </summary>
		public const float ProbeAbove = 3f;

		/// <summary>
		/// The seats of each of the game's boats, by its prefab
		/// </summary>
		private static readonly Dictionary<string, Plan[]> Plans = new Dictionary<string, Plan[]>
		{
			{
				"Raft", new[]
				{
					Seat(Helm),
					Seat("mast"),
					// On the left side, between the one at the back and the one up front on the left
					Deck(-0.75f, 0.48f, 0.2f),
					// In the back on the left, clear of the ladder, and two up front
					Deck(-0.9f, 0.48f, -1.7f),
					Deck(-0.55f, 0.48f, 2.1f),
					Deck(0.55f, 0.48f, 2.1f)
				}
			},
			{
				"Karve", new[]
				{
					Seat(Helm),
					Seat("mast"),
					Seat("sit_box_fr"),
					Seat("sit_box_fl"),
					// Behind the mast, a little to the left, clear of the one holding on to it on its right
					Deck(-0.1f, -0.05f, -0.85f),
					// To the left of the mast, between the one behind it and the one beside the chest, clear of the ladder
					Deck(-0.65f, -0.05f, 0.1f),
					// Between the mast and the forward stools, either side of the chest
					Deck(-0.8f, -0.05f, 1.1f),
					Deck(0.8f, -0.05f, 1.1f),
					// Between the helm and the mast
					Deck(-0.65f, -0.05f, -1.9f),
					Deck(0.65f, -0.05f, -1.9f)
				}
			},
			{
				"VikingShip", new[]
				{
					Seat(Helm),
					Seat("mast"),
					Seat("sit_box"),
					Seat("sit_box (1)"),
					Seat("sit_box (2)"),
					Seat("sit_box (3)"),
					// Holding on at the figurehead
					Seat("front"),
					// In front of the forward stools
					Deck(0f, 0.54f, 6.6f),
					// To the left of the mast, on the cargo stowed there
					Deck(-1f, 0.54f, -0.5f),
					Deck(-1.7f, 0.54f, 0.5f),
					// Behind the cargo on the left, to the right of the mast past the one holding on to it, and just in
					// front of the mast
					Deck(-1.32f, 0.63f, -1.93f),
					Deck(1.66f, 0.63f, 0.48f),
					Deck(-0.17f, 0.66f, 1.83f),
					// Between the mast and the forward stools, either side of the chest
					Deck(1.25f, 0.54f, 1.9f),
					Deck(1.3f, 0.54f, 3.0f),
					Deck(1.2f, 0.54f, 4.4f),
					Deck(-1.3f, 0.54f, 2.4f),
					Deck(-1.3f, 0.54f, 3.4f),
					Deck(-1.15f, 0.54f, 4.5f),
					// Between the mast and the rear stools, on the right, the cargo being on the left
					Deck(1.35f, 0.54f, -1.2f),
					Deck(0.85f, 0.54f, -2.1f),
					// Between the helm and the rear stools
					Deck(-0.85f, 0.54f, -4.3f),
					Deck(0.85f, 0.54f, -4.3f),
					Deck(-0.65f, 0.54f, -5.6f),
					Deck(0.65f, 0.54f, -5.6f)
				}
			},
			{
				"VikingShip_Ashlands", new[]
				{
					Seat(Helm),
					Seat("mast"),
					Seat("sit_box (1)"),
					Seat("sit_box (2)"),
					Seat("sit_box (3)"),
					Seat("sit_box (4)"),
					Seat("sit_box (5)"),
					Seat("sit_box (6)"),
					Seat("sit_box (7)"),
					Seat("sit_box (8)"),
					// On the forecastle
					Deck(0f, 3.5f, 11f),
					// On the deck between the stools, the cargo by the mast being on the right
					Deck(-1.3f, 1.8f, -5.2f),
					Deck(1.3f, 1.8f, -5.2f),
					Deck(-1.3f, 1.8f, -4f),
					Deck(1.3f, 1.8f, -4f),
					Deck(-1.3f, 1.8f, -2.8f),
					Deck(1.3f, 1.8f, -2.8f),
					Deck(-1.2f, 1.8f, -1.3f),
					Deck(-1.2f, 1.8f, 1f),
					// Forward and to the right of the one holding on to the mast, past the cargo there
					Deck(1.5f, 1.8f, 0.1f),
					// Between the rearmost stools and the ones in front of them, and the foremost and the ones behind them,
					// on both sides, a little in from the stools' line
					Deck(-2.75f, 1.8f, -4.3f),
					Deck(2.75f, 1.8f, -4.3f),
					Deck(2.75f, 1.8f, 3.6f),
					Deck(-2.75f, 1.8f, 3.65f),
					Deck(-1.3f, 1.8f, 2.9f),
					Deck(1.3f, 1.8f, 2.9f),
					Deck(-1.3f, 1.8f, 4.1f),
					Deck(1.3f, 1.8f, 4.1f),
					Deck(-1.3f, 1.8f, 5.3f),
					Deck(1.3f, 1.8f, 5.3f),
					// On the deck at the foot of the steps up to the helm, and up to the forecastle, either side of the chest
					Deck(-0.6f, 1.8f, -7f),
					Deck(0.6f, 1.8f, -7f),
					Deck(-1.35f, 1.8f, 7.1f),
					Deck(1.35f, 1.8f, 7.1f),
					// Beside the helm, and at the very back of the aftcastle
					Deck(-0.2f, 3.8f, -11f),
					Deck(0f, 3.8f, -12.2f),
					// On the steps up to the helm, and up to the forecastle, evenly spaced along the line of their benches
					Beside("sit_bench", -1.8f),
					Beside("sit_bench", -0.6f),
					Beside("sit_bench", 0.6f),
					Beside("sit_bench", 1.8f),
					Beside("sit_bench (4)", -1.8f),
					Beside("sit_bench (4)", -0.6f),
					Beside("sit_bench (4)", 0.6f),
					Beside("sit_bench (4)", 1.8f)
				}
			}
		};

		/// <summary>
		/// Makes a seat of the ship's own
		/// </summary>
		/// <param name="chair">The name of the chair's object, or Helm</param>
		/// <returns>The seat</returns>
		private static Plan Seat(string chair)
		{
			return(new Plan { Chair = chair });
		}

		/// <summary>
		/// Makes a seat in line with one of the ship's chairs, moved across the ship, sat in the same way
		/// </summary>
		/// <param name="chair">The name of the chair's object</param>
		/// <param name="x">How far across the ship from its middle it is, in meters, to the right</param>
		/// <returns>The seat</returns>
		private static Plan Beside(string chair, float x)
		{
			return(new Plan { Chair = chair, X = x, Beside = true });
		}

		/// <summary>
		/// Makes a spot on the deck, facing the bow
		/// </summary>
		/// <param name="x">How far across the ship from its middle, in meters, to the right</param>
		/// <param name="y">The deck's height there when it can't be found, in meters</param>
		/// <param name="z">How far along the ship from its middle, in meters, toward the bow</param>
		/// <returns>The seat</returns>
		private static Plan Deck(float x, float y, float z)
		{
			return(new Plan { X = x, Y = y, Z = z });
		}

		/// <summary>
		/// Gets the seats of a boat
		/// </summary>
		/// <param name="prefab">The boat's prefab name</param>
		/// <returns>The seats, or null for a boat not of the game's</returns>
		public static Plan[] For(string prefab)
		{
			return(Plans.TryGetValue(prefab, out Plan[] plan) ? plan : null);
		}
	}
}
