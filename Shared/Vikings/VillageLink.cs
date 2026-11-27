using System;
using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// What Zombie Apocalypse asks Viking Villages about the world's villages, through Viking Villages' Link class -
	/// with the mod missing, there are none
	/// </summary>
	/// <remarks>
	/// Each method names a Link method that Viking Villages keeps for as long as it's released (see ModLink), and answers
	/// its fallback when the installed version doesn't have it
	/// </remarks>
	internal static class VillageLink
	{
		/// <summary>
		/// Viking Villages' GUID, which its plugin is found by
		/// </summary>
		public const string Guid = "kriona.VikingVillages";

		private static readonly ModLink link = new ModLink(Guid, "VikingVillages.Link");

		/// <summary>
		/// Checks whether the PC vikings take in an abandoned house being generated, as one near an abandoned farm,
		/// so it's left for them - asked as the house is placed, once Viking Villages has looked at it
		/// </summary>
		/// <param name="position">Where the house stands</param>
		/// <returns>True when they do</returns>
		public static bool ClaimsHouse(Vector3 position)
		{
			Func<Vector3, bool> claims = link.Get<Func<Vector3, bool>>("ClaimsHouse");
			return(claims != null && claims(position));
		}
	}
}
