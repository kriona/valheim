using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// How a viking gets to a waypoint of its road from the one before - on foot, by boat across the sea, or through a
	/// portal from the one before, which stands at the portal it steps into
	/// </summary>
	internal enum TravelLeg
	{
		Foot,
		Sea,
		Portal
	}

	/// <summary>
	/// A viking's road to a village - its waypoints, from where it set out to the village, and how each leg between them
	/// is crossed, on foot, by boat or through a portal
	/// </summary>
	internal class TravelRoad
	{
		/// <summary>
		/// The waypoints, and for each, how the leg to it is crossed - always on foot for the first, which is where the
		/// road starts
		/// </summary>
		public List<Vector3> Points = new List<Vector3>();
		public List<TravelLeg> Legs = new List<TravelLeg>();

		/// <summary>
		/// The version of how a road is written, which a reader turns down a road written another way by, and the version
		/// that wrote whether each leg is by sea, which it still reads
		/// </summary>
		private const int FormatVersion = 2;
		private const int SeaOnlyVersion = 1;

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
			Add(point, (sea ? TravelLeg.Sea : TravelLeg.Foot));
		}

		/// <summary>
		/// Adds a waypoint
		/// </summary>
		/// <param name="point">The waypoint</param>
		/// <param name="leg">How the leg to it is crossed</param>
		public void Add(Vector3 point, TravelLeg leg)
		{
			Points.Add(point);
			Legs.Add((Points.Count > 1) ? leg : TravelLeg.Foot);
		}

		/// <summary>
		/// Gets how the leg to a waypoint is crossed
		/// </summary>
		/// <param name="index">The waypoint's index</param>
		/// <returns>The leg, on foot for the first waypoint or one past the end</returns>
		public TravelLeg LegTo(int index)
		{
			return((index > 0 && index < Legs.Count) ? Legs[index] : TravelLeg.Foot);
		}

		/// <summary>
		/// Checks whether the leg to a waypoint is by sea
		/// </summary>
		/// <param name="index">The waypoint's index</param>
		/// <returns>True when it is</returns>
		public bool SeaLeg(int index)
		{
			return(LegTo(index) == TravelLeg.Sea);
		}

		/// <summary>
		/// Checks whether the leg to a waypoint is through a portal
		/// </summary>
		/// <param name="index">The waypoint's index</param>
		/// <returns>True when it is</returns>
		public bool PortalLeg(int index)
		{
			return(LegTo(index) == TravelLeg.Portal);
		}

		/// <summary>
		/// Counts the portals the road goes through
		/// </summary>
		/// <returns>How many</returns>
		public int PortalCount()
		{
			return(Legs.FindAll((TravelLeg leg) => leg == TravelLeg.Portal).Count);
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
		/// along the rest, on foot at TravelTime.Speed, by sea at a boat's speed and through a portal in
		/// TravelTime.PortalSeconds
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
			float seconds = LegSeconds(Flat(position, Points[next]), LegTo(next), boatSpeed);
			for (int i = next + 1; i < Points.Count; i++)
			{
				seconds += LegSeconds(Flat(Points[i - 1], Points[i]), LegTo(i), boatSpeed);
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
		/// Works out how long the road is from a point on it, on foot and by sea alike - a portal covering no ground
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
			float meters = (PortalLeg(next) ? 0f : Flat(position, Points[next]));
			for (int i = next + 1; i < Points.Count; i++)
			{
				if (!PortalLeg(i))
				{
					meters += Flat(Points[i - 1], Points[i]);
				}
			}
			return(meters);
		}

		/// <summary>
		/// Works out how long a leg takes
		/// </summary>
		/// <param name="meters">How long it is, in meters</param>
		/// <param name="leg">How it's crossed</param>
		/// <param name="boatSpeed">How fast the boat goes, in meters a second</param>
		/// <returns>How long it takes, in seconds</returns>
		private static float LegSeconds(float meters, TravelLeg leg, float boatSpeed)
		{
			switch (leg)
			{
				case TravelLeg.Sea:
					return(meters / Mathf.Max(0.1f, boatSpeed));
				case TravelLeg.Portal:
					return(TravelTime.PortalSeconds);
				default:
					return(TravelTime.Seconds(meters));
			}
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
				pkg.Write((int)Legs[i]);
			}
			return(pkg.GetArray());
		}

		/// <summary>
		/// Reads a road from the bytes it's written as - one written with only whether each leg is by sea too
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
				int version = pkg.ReadInt();
				if (version != FormatVersion && version != SeaOnlyVersion)
				{
					return(road);
				}
				int count = pkg.ReadInt();
				for (int i = 0; i < count; i++)
				{
					Vector3 point = pkg.ReadVector3();
					if (version == SeaOnlyVersion)
					{
						road.Add(point, pkg.ReadBool());
						continue;
					}
					int leg = pkg.ReadInt();
					road.Add(point, ((leg >= (int)TravelLeg.Foot && leg <= (int)TravelLeg.Portal) ? (TravelLeg)leg : TravelLeg.Foot));
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
