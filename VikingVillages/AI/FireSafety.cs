using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Keeps the PC vikings out of fires' flames - their paths bend around a lit fire, PathMargin clear of its
	/// flames, and they find where flames are, to run out of them and to cook and add fuel from outside them
	/// </summary>
	/// <remarks>
	/// The navmesh every creature shares only keeps a creature about half a meter clear of a bonfire's flames, which
	/// one cutting a corner around it clips, so a viking's own path is bent around the fire as it's found, leaving the
	/// navmesh and other creatures' paths as they are
	/// </remarks>
	internal static class FireSafety
	{
		/// <summary>
		/// How far clear of a fire's flames a viking's body keeps as it walks past, in meters
		/// </summary>
		private const float PathMargin = 1f;

		/// <summary>
		/// How far past a fire's flames a viking stands to cook over it or add fuel, in meters - past where its path
		/// bends around them
		/// </summary>
		private const float CookMargin = 1.75f;

		/// <summary>
		/// How high above a spot on the ground flames are looked for, in meters, since a fire's flames rise from its
		/// middle
		/// </summary>
		private const float LookHeight = 0.5f;

		/// <summary>
		/// The most a bend around a fire turns between its corners, in degrees - the corners stand just far enough out
		/// that the path between them keeps clear too
		/// </summary>
		private const float BendStep = 30f;

		/// <summary>
		/// The fires loaded on this client, and how far each one's flames reach from its middle, in meters
		/// </summary>
		private static readonly List<KeyValuePair<Fireplace, float>> fires = new List<KeyValuePair<Fireplace, float>>();

		/// <summary>
		/// Notes a fire as it wakes, so the vikings' paths bend around it while it burns
		/// </summary>
		/// <param name="fire">The fire</param>
		public static void Register(Fireplace fire)
		{
			float reach = VillageLife.AreaReach(fire, EffectArea.Type.Burning);
			if (reach > 0f)
			{
				fires.Add(new KeyValuePair<Fireplace, float>(fire, reach));
			}
		}

		/// <summary>
		/// Bends a PC viking's path around every lit fire whose flames it would pass within PathMargin of, going round
		/// the fire on the shorter side - stepping straight out from the fire first when a leg starts that near the
		/// flames, and leaving a leg that ends that near them as it is, since the viking is headed for a spot by the
		/// fire that's clear of them, or one that starts in the flames, which the viking runs out of (see
		/// VillageLife.UpdateFlames)
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <param name="path">Its path's corners, from where it stands, bent in place</param>
		public static void BendPath(Character viking, List<Vector3> path)
		{
			if (path.Count == 0)
			{
				return;
			}
			float body = viking.GetRadius();
			fires.RemoveAll((KeyValuePair<Fireplace, float> entry) => entry.Key == null);
			foreach (KeyValuePair<Fireplace, float> entry in fires)
			{
				Fireplace fire = entry.Key;
				if (fire.m_nview == null || !fire.m_nview.IsValid() || !fire.IsBurning())
				{
					continue;
				}
				float clear = entry.Value + body + PathMargin;
				Vector3 middle = fire.transform.position;
				Vector3 from = viking.transform.position;
				for (int i = 0; i < path.Count; i++)
				{
					List<Vector3> bend = Bend(from, path[i], middle, clear, entry.Value);
					from = path[i];
					if (bend != null)
					{
						path.InsertRange(i, bend);
						i += bend.Count;
					}
				}
			}
		}

		/// <summary>
		/// Works out the corners that take a leg of a path around a fire, keeping a distance from its middle - a leg that
		/// starts nearer the fire than its corners stand, outside the flames, steps straight out to them first
		/// </summary>
		/// <param name="start">Where the leg starts</param>
		/// <param name="end">Where it ends</param>
		/// <param name="middle">The fire's middle</param>
		/// <param name="clear">How far from the fire's middle the leg keeps, in meters</param>
		/// <param name="flames">How far the fire's flames reach from its middle, in meters</param>
		/// <returns>The corners to put between the leg's ends, or null when it keeps clear already, heads away from the
		/// fire from nearer than the distance, starts in the flames or ends nearer than the distance</returns>
		internal static List<Vector3> Bend(Vector3 start, Vector3 end, Vector3 middle, float clear, float flames)
		{
			Vector2 a = new Vector2(start.x - middle.x, start.z - middle.z);
			Vector2 b = new Vector2(end.x - middle.x, end.z - middle.z);
			if (a.magnitude < flames || a.sqrMagnitude < 0.0001f || b.magnitude < clear)
			{
				return(null);
			}
			Vector2 leg = b - a;
			bool near = (a.magnitude < clear);
			if (near)
			{
				// Heading away from the fire, it only gets clearer of the flames as it goes
				if (Vector2.Dot(a, leg) >= 0f)
				{
					return(null);
				}
				a = a.normalized * clear;
				leg = b - a;
			}
			List<Vector3> corners = new List<Vector3>();
			float along = ((leg.sqrMagnitude > 0f) ? Mathf.Clamp01(-Vector2.Dot(a, leg) / leg.sqrMagnitude) : 0f);
			// Nearest the fire where it starts, it keeps the distance all along
			if (along <= 0f || (a + leg * along).magnitude >= clear)
			{
				if (!near)
				{
					return(null);
				}
				corners.Add(new Vector3(middle.x + a.x, start.y, middle.z + a.y));
				return(corners);
			}
			float from = Mathf.Atan2(a.y, a.x) * Mathf.Rad2Deg;
			float turn = Mathf.DeltaAngle(from, Mathf.Atan2(b.y, b.x) * Mathf.Rad2Deg);
			int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(turn) / BendStep));
			// Standing out by this much, the straight path between corners keeps the distance all along
			float radius = clear / Mathf.Cos(Mathf.Abs(turn) / steps / 2f * Mathf.Deg2Rad);
			// Starting inside that, the way to the first corner would cut in toward the fire, so it steps straight out
			if (a.magnitude < radius)
			{
				corners.Add(new Vector3(middle.x + Mathf.Cos(from * Mathf.Deg2Rad) * radius, start.y, middle.z + Mathf.Sin(from * Mathf.Deg2Rad) * radius));
			}
			for (int i = 1; i < steps; i++)
			{
				float angle = (from + turn * i / steps) * Mathf.Deg2Rad;
				Vector3 corner = new Vector3(middle.x + Mathf.Cos(angle) * radius, Mathf.Lerp(start.y, end.y, (float)i / steps), middle.z + Mathf.Sin(angle) * radius);
				corners.Add(corner);
			}
			return(corners);
		}

		/// <summary>
		/// Works out how near a fire's middle a viking can walk - outside its flames and where paths bend around them,
		/// with room to spare
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <returns>The distance from its middle, in meters</returns>
		public static float StandOff(Fireplace fire)
		{
			return(VillageLife.AreaReach(fire, EffectArea.Type.Burning) + CookMargin);
		}

		/// <summary>
		/// Finds flames reaching within some distance of a point
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="radius">How far from it to look, in meters</param>
		/// <param name="middle">The middle of the fire the flames are part of, or of the flames when they're no fire's</param>
		/// <param name="reach">How far the fire's flames reach from its middle, in meters</param>
		/// <returns>False when there are none</returns>
		public static bool FlamesNear(Vector3 point, float radius, out Vector3 middle, out float reach)
		{
			middle = point;
			reach = 0f;
			EffectArea area = EffectArea.IsPointInsideArea(point, EffectArea.Type.Burning, radius);
			if (area == null)
			{
				return(false);
			}
			Fireplace fire = area.GetComponentInParent<Fireplace>();
			if (fire != null)
			{
				middle = fire.transform.position;
				reach = VillageLife.AreaReach(fire, EffectArea.Type.Burning);
				return(true);
			}
			middle = area.transform.position;
			Collider collider = area.GetComponent<Collider>();
			reach = ((collider != null) ? Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z) : 0f);
			return(true);
		}

		/// <summary>
		/// Finds where a viking stands to cook at a cooking station - within reach of it, or when it stands over a fire,
		/// outside the fire's flames on the station's side, since a station over a fire is out of reach from outside its
		/// flames
		/// </summary>
		/// <param name="station">Where the station stands</param>
		/// <param name="from">Where the viking stands, the side it goes to when the station is right over the fire's middle</param>
		/// <param name="reach">How near the station a viking cooks from when there's no fire under it, in meters</param>
		/// <param name="spot">Where to stand</param>
		/// <param name="within">How near the spot is near enough, in meters</param>
		public static void CookSpot(Vector3 station, Vector3 from, float reach, out Vector3 spot, out float within)
		{
			spot = station;
			within = reach;
			if (!FlamesNear(station + Vector3.up * LookHeight, reach, out Vector3 middle, out float flames))
			{
				return;
			}
			spot = SpotAround(station, from, middle, flames);
			within = 0.5f;
		}

		/// <summary>
		/// Works out where a viking stands to cook at a cooking station over a fire - CookMargin past the fire's flames on
		/// the station's side, or the viking's when the station is right over the fire's middle, or along the world's
		/// forward axis when the viking is too, at the station's height
		/// </summary>
		/// <param name="station">Where the station stands</param>
		/// <param name="from">Where the viking stands</param>
		/// <param name="middle">The fire's middle</param>
		/// <param name="flames">How far the fire's flames reach from its middle, in meters</param>
		/// <returns>Where to stand</returns>
		internal static Vector3 SpotAround(Vector3 station, Vector3 from, Vector3 middle, float flames)
		{
			Vector3 side = station - middle;
			side.y = 0f;
			if (side.sqrMagnitude < 0.01f)
			{
				side = from - middle;
				side.y = 0f;
			}
			if (side.sqrMagnitude < 0.01f)
			{
				side = Vector3.forward;
			}
			Vector3 spot = middle + side.normalized * (flames + CookMargin);
			spot.y = station.y;
			return(spot);
		}
	}
}
