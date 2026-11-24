using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A viking's road to a village - its waypoints, from where it set out to the village, and which legs between them
	/// cross the sea by boat rather than on foot
	/// </summary>
	internal class TravelRoad
	{
		/// <summary>
		/// The waypoints, and for each, whether the leg to it is by sea - never the first, which is where the road starts
		/// </summary>
		public List<Vector3> Points = new List<Vector3>();
		public List<bool> Sea = new List<bool>();

		/// <summary>
		/// The version of how a road is written, which a reader turns down a road written another way by
		/// </summary>
		private const int FormatVersion = 1;

		/// <summary>
		/// How many waypoints the road has
		/// </summary>
		public int Count
		{
			get
			{
				return(Points.Count);
			}
		}

		/// <summary>
		/// Adds a waypoint
		/// </summary>
		/// <param name="point">The waypoint</param>
		/// <param name="sea">Whether the leg to it is by sea</param>
		public void Add(Vector3 point, bool sea)
		{
			Points.Add(point);
			Sea.Add(sea && Points.Count > 1);
		}

		/// <summary>
		/// Checks whether the leg to a waypoint is by sea
		/// </summary>
		/// <param name="index">The waypoint's index</param>
		/// <returns>True when it is</returns>
		public bool SeaLeg(int index)
		{
			return(index > 0 && index < Sea.Count && Sea[index]);
		}

		/// <summary>
		/// Gets the waypoint a viking arriving at the end of the road comes from - the one before the last
		/// </summary>
		/// <returns>The waypoint, or the start when there's only the one</returns>
		public Vector3 Approach()
		{
			return((Points.Count >= 2) ? Points[Points.Count - 2] : ((Points.Count > 0) ? Points[0] : Vector3.zero));
		}

		/// <summary>
		/// Works out how long the rest of the road takes from a point on it - from the point to the next waypoint, then on
		/// along the rest, on foot at TravelTime.Speed and by sea at a boat's speed
		/// </summary>
		/// <param name="next">The index of the waypoint the viking is heading for</param>
		/// <param name="position">Where the viking is</param>
		/// <param name="boatSpeed">How fast its boat goes, in meters a second</param>
		/// <returns>How long it has left, in seconds</returns>
		public float Seconds(int next, Vector3 position, float boatSpeed)
		{
			if (Points.Count == 0 || next >= Points.Count)
			{
				return(0f);
			}
			next = Mathf.Max(0, next);
			float seconds = LegSeconds(Flat(position, Points[next]), SeaLeg(next), boatSpeed);
			for (int i = next + 1; i < Points.Count; i++)
			{
				seconds += LegSeconds(Flat(Points[i - 1], Points[i]), SeaLeg(i), boatSpeed);
			}
			return(seconds);
		}

		/// <summary>
		/// Works out how long the whole road takes
		/// </summary>
		/// <param name="boatSpeed">How fast the boat goes, in meters a second</param>
		/// <returns>How long it takes, in seconds</returns>
		public float TotalSeconds(float boatSpeed)
		{
			return((Points.Count > 0) ? Seconds(0, Points[0], boatSpeed) : 0f);
		}

		/// <summary>
		/// Works out how long the road is from a point on it, on foot and by sea alike
		/// </summary>
		/// <param name="next">The index of the waypoint the viking is heading for</param>
		/// <param name="position">Where the viking is</param>
		/// <returns>How far it has left to go, in meters</returns>
		public float Meters(int next, Vector3 position)
		{
			if (Points.Count == 0 || next >= Points.Count)
			{
				return(0f);
			}
			next = Mathf.Max(0, next);
			float meters = Flat(position, Points[next]);
			for (int i = next + 1; i < Points.Count; i++)
			{
				meters += Flat(Points[i - 1], Points[i]);
			}
			return(meters);
		}

		/// <summary>
		/// Works out how long a leg takes
		/// </summary>
		/// <param name="meters">How long it is, in meters</param>
		/// <param name="sea">Whether it's by sea</param>
		/// <param name="boatSpeed">How fast the boat goes, in meters a second</param>
		/// <returns>How long it takes, in seconds</returns>
		private static float LegSeconds(float meters, bool sea, float boatSpeed)
		{
			return(sea ? meters / Mathf.Max(0.1f, boatSpeed) : TravelTime.Seconds(meters));
		}

		/// <summary>
		/// Measures the distance between two points across the ground, leaving out height
		/// </summary>
		/// <param name="a">One point</param>
		/// <param name="b">The other</param>
		/// <returns>The distance, in meters</returns>
		private static float Flat(Vector3 a, Vector3 b)
		{
			float dx = a.x - b.x;
			float dz = a.z - b.z;
			return(Mathf.Sqrt(dx * dx + dz * dz));
		}

		/// <summary>
		/// Writes the road as bytes
		/// </summary>
		/// <returns>The bytes</returns>
		public byte[] Encode()
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(FormatVersion);
			pkg.Write(Points.Count);
			for (int i = 0; i < Points.Count; i++)
			{
				pkg.Write(Points[i]);
				pkg.Write(Sea[i]);
			}
			return(pkg.GetArray());
		}

		/// <summary>
		/// Reads a road from the bytes it's written as
		/// </summary>
		/// <param name="bytes">The bytes, or null</param>
		/// <returns>The road, with no waypoints when it can't be read</returns>
		public static TravelRoad Decode(byte[] bytes)
		{
			TravelRoad road = new TravelRoad();
			if (bytes == null || bytes.Length == 0)
			{
				return(road);
			}
			try
			{
				ZPackage pkg = new ZPackage(bytes);
				if (pkg.ReadInt() != FormatVersion)
				{
					return(road);
				}
				int count = pkg.ReadInt();
				for (int i = 0; i < count; i++)
				{
					Vector3 point = pkg.ReadVector3();
					road.Add(point, pkg.ReadBool());
				}
			}
			catch (System.Exception)
			{
				return(new TravelRoad());
			}
			return(road);
		}
	}
}
