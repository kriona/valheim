namespace VikingVillages
{
	/// <summary>
	/// How far along crossing the sea a living viking is - gathering with its group at the shore, building the boats,
	/// getting on board, swimming ashore at the far shore, sitting on board, swimming for land with its boat gone,
	/// breaking the boat up, overboard getting back on, or riding along on a ship not its own - noted on its ZDO (see
	/// VikingBoat.BoardKey), so it carries on after it loads again, and read by its boat for who's on board
	/// </summary>
	/// <remarks>
	/// The numbers are what's written to the ZDO, so each keeps its own
	/// </remarks>
	internal enum Crossing
	{
		None = 0,
		Gathering = 1,
		Building = 2,
		Boarding = 3,
		Landing = 4,
		Aboard = 5,
		Wrecked = 6,
		Breaking = 7,
		Overboard = 8,
		Passenger = 9
	}
}
