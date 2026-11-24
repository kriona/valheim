using System.Collections.Generic;

namespace VikingVillages
{
	/// <summary>
	/// Where the crew of each of the game's boats sits (see VikingBoat), in the order they're given out - the helm first,
	/// then the ship's own seats, then spots on its deck where there's room to sit on the boards, crowded on the smaller
	/// boats - as many as the boat carries (see TravelTime.BoatCrew)
	/// </summary>
	/// <remarks>
	/// A seat of the ship's own is its helm or a chair by the name of its object - a stool, the mast's holdfast, or a
	/// bench on the drakkar's steps - sat in the way the ship has it sat in (see Chair.m_attachAnimation). A spot on the
	/// deck is where it stands from the ship's middle, in meters across, up and along it, forward being toward the bow,
	/// sat on with the floor sit facing the bow - its height found on the deck under it as the boat loads, with the
	/// height here kept for when nothing is found. The spots were picked from each ship's layout to keep clear of its
	/// mast, chest, cargo, ladders and stools
	/// </remarks>
	internal static class BoatSeats
	{
		/// <summary>
		/// A seat - a chair of the ship's by its object's name, or Helm for the helm, or else a spot on the deck
		/// </summary>
		internal struct Plan
		{
			public string Chair;
			public float X;
			public float Y;
			public float Z;

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
					// Between the mast and the forward stools, either side of the chest
					Deck(-0.95f, -0.05f, 1.1f),
					Deck(0.95f, -0.05f, 1.1f),
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
					// In front of the forward stools
					Deck(0f, 0.54f, 6.6f),
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
					Deck(-1.3f, 1.8f, 2.9f),
					Deck(1.3f, 1.8f, 2.9f),
					Deck(-1.3f, 1.8f, 4.1f),
					Deck(1.3f, 1.8f, 4.1f),
					Deck(-1.3f, 1.8f, 5.3f),
					Deck(1.3f, 1.8f, 5.3f),
					// Beside the helm
					Deck(-0.2f, 3.8f, -11f),
					// On the steps up to the helm, and up to the forecastle, facing the deck
					Seat("sit_bench (1)"),
					Seat("sit_bench (2)"),
					Seat("sit_bench (3)"),
					Seat("sit_bench (4)"),
					Seat("sit_bench (5)"),
					Seat("sit_bench (6)")
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
