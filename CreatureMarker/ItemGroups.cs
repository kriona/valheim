using System.Collections.Generic;
using UnityEngine;

namespace CreatureMarker
{
	/// <summary>
	/// Groups items lying on the ground into one marker - items of the same kind past Item Group Distance from the
	/// player and within Reach of the first of them - worked out apart from the game so it can be tested
	/// </summary>
	internal static class ItemGroups
	{
		/// <summary>
		/// How near the first item of a group another of the same kind has to lie to join it, in meters
		/// </summary>
		internal const float Reach = 3f;

		/// <summary>
		/// Groups items - each within beyond of the player on its own, and each farther off with the first group of its
		/// kind whose first item lies within Reach of it, or else starting a group of its own. Going by the first item
		/// rather than the nearest of the group keeps a row of items from chaining into one long group
		/// </summary>
		/// <param name="positions">Where each item lies</param>
		/// <param name="kinds">Each item's kind, like its name token, in the same order</param>
		/// <param name="player">Where the player is</param>
		/// <param name="beyond">The distance from the player beyond which items are grouped, in meters, 0 for never</param>
		/// <returns>The groups, each the items' places in the lists, in the order their first items come</returns>
		internal static List<List<int>> Group(List<Vector3> positions, List<string> kinds, Vector3 player, float beyond)
		{
			List<List<int>> groups = new List<List<int>>();
			float beyondSquared = beyond * beyond;
			float reachSquared = Reach * Reach;
			for (int i = 0; i < positions.Count; i++)
			{
				List<int> joined = null;
				if (beyond > 0f && Vector3.SqrMagnitude(positions[i] - player) > beyondSquared)
				{
					foreach (List<int> group in groups)
					{
						int first = group[0];
						if (kinds[first] == kinds[i] && Vector3.SqrMagnitude(positions[first] - player) > beyondSquared && Vector3.SqrMagnitude(positions[first] - positions[i]) <= reachSquared)
						{
							joined = group;
							break;
						}
					}
				}
				if (joined != null)
				{
					joined.Add(i);
				}
				else
				{
					groups.Add(new List<int> { i });
				}
			}
			return(groups);
		}

		/// <summary>
		/// Works out the middle of a group's items, where its marker goes
		/// </summary>
		/// <param name="positions">Where each item lies</param>
		/// <param name="group">The group's items, by their places in the list</param>
		/// <returns>The middle</returns>
		internal static Vector3 Middle(List<Vector3> positions, List<int> group)
		{
			Vector3 sum = Vector3.zero;
			foreach (int i in group)
			{
				sum += positions[i];
			}
			return((group.Count > 0) ? sum / group.Count : Vector3.zero);
		}
	}
}
