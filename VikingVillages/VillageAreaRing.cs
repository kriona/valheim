using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Hides the marks of a village's ring that fall inside another of the village's rings (see VillageArea), so rings
	/// that overlap show as one outline around all of them rather than circles crossing each other
	/// </summary>
	/// <remarks>
	/// The ring's marks are placed around it every frame by its CircleProjector, turning slowly as they go, so they're
	/// looked at again each frame once it has placed them
	/// </remarks>
	internal class VillageAreaRing : MonoBehaviour
	{
		/// <summary>
		/// How far inside another ring a mark has to be before it's hidden, in meters, so marks right where the rings
		/// cross stay shown and the outline doesn't break there
		/// </summary>
		private const float InsideMargin = 0.25f;

		/// <summary>
		/// The ring, and the village's other zones its marks are hidden inside
		/// </summary>
		private CircleProjector ring;
		private readonly List<Village.Zone> others = new List<Village.Zone>();

		/// <summary>
		/// Finds the ring
		/// </summary>
		private void Awake()
		{
			ring = GetComponent<CircleProjector>();
		}

		/// <summary>
		/// Sets the village's other zones, whose rings this ring's marks are hidden inside
		/// </summary>
		/// <param name="zones">The village's zones</param>
		/// <param name="own">This ring's own zone in them</param>
		public void SetOthers(List<Village.Zone> zones, int own)
		{
			others.Clear();
			for (int i = 0; i < zones.Count; i++)
			{
				if (i != own)
				{
					others.Add(zones[i]);
				}
			}
		}

		/// <summary>
		/// Shows the marks outside every other ring and hides the rest, once the ring has placed them this frame
		/// </summary>
		private void LateUpdate()
		{
			if (ring == null)
			{
				return;
			}
			foreach (GameObject mark in ring.m_segments)
			{
				if (mark == null)
				{
					continue;
				}
				Vector3 position = mark.transform.position;
				bool inside = others.Exists((Village.Zone zone) => Utils.DistanceXZ(position, zone.Center) < zone.Radius - InsideMargin);
				if (mark.activeSelf == inside)
				{
					mark.SetActive(!inside);
				}
			}
		}
	}
}
