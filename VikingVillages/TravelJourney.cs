using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The rest of a trip a group of vikings out of every player's reach makes, as the server keeps it - its stages one
	/// after another, each a road (see TravelRoad) or a stay in one place, like the work of a quest - so where they are
	/// and how long they have left can be worked out at any time from when it started
	/// </summary>
	internal class TravelJourney
	{
		/// <summary>
		/// A stage of the trip - a road, or a stay of a number of seconds where the road before it ended, whether a portal
		/// on its road has to take what they carry by then, and whether they're on their way home from a quest
		/// </summary>
		internal class Stage
		{
			public TravelRoad Road;
			public float Stay;
			public bool Unteleportable;
			public bool Homeward;
		}

		/// <summary>
		/// Where a group is on its trip at a time - the stage, the waypoint of its road they're heading for, or 0 on a
		/// stay, where they are, and how the leg they're on is crossed
		/// </summary>
		internal struct Place
		{
			public int Stage;
			public int Waypoint;
			public Vector3 Position;
			public TravelLeg Leg;
		}

		/// <summary>
		/// The stages, and how fast the group's boat goes, in meters a second
		/// </summary>
		public List<Stage> Stages = new List<Stage>();
		public float BoatSpeed = 1f;

		/// <summary>
		/// The version of how a trip is written, which a reader turns down a trip written another way by
		/// </summary>
		private const int FormatVersion = 1;

		/// <summary>
		/// Makes a trip along one road
		/// </summary>
		/// <param name="road">The road</param>
		/// <param name="boatSpeed">How fast the group's boat goes, in meters a second</param>
		/// <param name="unteleportable">Whether they carry something a portal won't take</param>
		/// <returns>The trip</returns>
		public static TravelJourney Along(TravelRoad road, float boatSpeed, bool unteleportable)
		{
			TravelJourney journey = new TravelJourney { BoatSpeed = boatSpeed };
			journey.AddRoad(road, unteleportable, false);
			return(journey);
		}

		/// <summary>
		/// Adds a road to the end of the trip
		/// </summary>
		/// <param name="road">The road</param>
		/// <param name="unteleportable">Whether they carry something a portal won't take on it</param>
		/// <param name="homeward">Whether it's the way home from a quest</param>
		public void AddRoad(TravelRoad road, bool unteleportable, bool homeward)
		{
			Stages.Add(new Stage { Road = road, Unteleportable = unteleportable, Homeward = homeward });
		}

		/// <summary>
		/// Adds a stay where the trip has got to
		/// </summary>
		/// <param name="seconds">How long it is, in seconds</param>
		public void AddStay(float seconds)
		{
			Stages.Add(new Stage { Stay = Mathf.Max(0f, seconds) });
		}

		/// <summary>
		/// Works out how long a stage takes
		/// </summary>
		/// <param name="index">The stage's index</param>
		/// <returns>How long it takes, in seconds</returns>
		public float StageSeconds(int index)
		{
			Stage stage = Stages[index];
			return((stage.Road != null) ? stage.Road.TotalSeconds(BoatSpeed) : stage.Stay);
		}

		/// <summary>
		/// Works out how long the whole trip takes
		/// </summary>
		/// <returns>How long it takes, in seconds</returns>
		public float TotalSeconds()
		{
			float total = 0f;
			for (int i = 0; i < Stages.Count; i++)
			{
				total += StageSeconds(i);
			}
			return(total);
		}

		/// <summary>
		/// Gets where the trip ends - the last point of its last road
		/// </summary>
		/// <param name="fallback">Where to say when it has no road</param>
		/// <returns>The point</returns>
		public Vector3 End(Vector3 fallback)
		{
			for (int i = Stages.Count - 1; i >= 0; i--)
			{
				TravelRoad road = Stages[i].Road;
				if (road != null && road.Count > 0)
				{
					return(road.Points[road.Count - 1]);
				}
			}
			return(fallback);
		}

		/// <summary>
		/// Gets the waypoint a group arriving at the end of the trip comes from (see TravelRoad.Approach)
		/// </summary>
		/// <param name="fallback">Where to say when it has no road</param>
		/// <returns>The point</returns>
		public Vector3 Approach(Vector3 fallback)
		{
			for (int i = Stages.Count - 1; i >= 0; i--)
			{
				TravelRoad road = Stages[i].Road;
				if (road != null && road.Count > 0)
				{
					return(road.Approach());
				}
			}
			return(fallback);
		}

		/// <summary>
		/// Works out where a group is a number of seconds into the trip - along the leg they're on, at the far end of a
		/// portal once they've stepped through, or where the trip ends once it's over
		/// </summary>
		/// <param name="elapsed">Seconds since the trip started</param>
		/// <param name="fallback">Where to say when the trip has no road</param>
		/// <returns>Where they are</returns>
		public Place At(float elapsed, Vector3 fallback)
		{
			Vector3 last = fallback;
			float left = Mathf.Max(0f, elapsed);
			for (int i = 0; i < Stages.Count; i++)
			{
				TravelRoad road = Stages[i].Road;
				if (road == null)
				{
					if (left < Stages[i].Stay)
					{
						return(new Place { Stage = i, Waypoint = 0, Position = last, Leg = TravelLeg.Foot });
					}
					left -= Stages[i].Stay;
					continue;
				}
				if (road.Count == 0)
				{
					continue;
				}
				last = road.Points[0];
				for (int w = 1; w < road.Count; w++)
				{
					TravelLeg leg = road.LegTo(w);
					float seconds = road.LegSeconds(w, BoatSpeed);
					if (left < seconds)
					{
						Vector3 position = ((leg == TravelLeg.Portal) ? road.Points[w - 1] : Vector3.Lerp(road.Points[w - 1], road.Points[w], (seconds > 0f) ? left / seconds : 1f));
						return(new Place { Stage = i, Waypoint = w, Position = position, Leg = leg });
					}
					left -= seconds;
					last = road.Points[w];
				}
			}
			return(new Place { Stage = Stages.Count, Waypoint = 0, Position = last, Leg = TravelLeg.Foot });
		}

		/// <summary>
		/// Makes the rest of the trip from a number of seconds into it - the road they're on from where they are now, the
		/// stay they're in for as long as it has left, and every stage after
		/// </summary>
		/// <param name="elapsed">Seconds since the trip started</param>
		/// <returns>The rest of the trip, as a trip of its own</returns>
		public TravelJourney From(float elapsed)
		{
			Place place = At(elapsed, Vector3.zero);
			TravelJourney rest = new TravelJourney { BoatSpeed = BoatSpeed };
			if (place.Stage >= Stages.Count)
			{
				return(rest);
			}
			Stage now = Stages[place.Stage];
			if (now.Road == null)
			{
				float before = 0f;
				for (int i = 0; i < place.Stage; i++)
				{
					before += StageSeconds(i);
				}
				rest.Stages.Add(new Stage { Stay = Mathf.Max(0f, now.Stay - (elapsed - before)), Homeward = now.Homeward });
			}
			else
			{
				TravelRoad road = new TravelRoad();
				// Partway through a portal, they're still at its near end
				road.Add(place.Position, TravelLeg.Foot);
				for (int w = place.Waypoint; w < now.Road.Count; w++)
				{
					road.Add(now.Road.Points[w], now.Road.LegTo(w));
				}
				rest.Stages.Add(new Stage { Road = road, Unteleportable = now.Unteleportable, Homeward = now.Homeward });
			}
			for (int i = place.Stage + 1; i < Stages.Count; i++)
			{
				rest.Stages.Add(Stages[i]);
			}
			return(rest);
		}

		/// <summary>
		/// Works out how many seconds of the trip are spent on foot in each place along it, a leg at a time, from a stage
		/// on - for what grows along the way (see TripForage)
		/// </summary>
		/// <param name="first">The first stage to count</param>
		/// <param name="add">Takes the middle of each leg on foot and how long it takes, in seconds</param>
		public void FootLegs(int first, System.Action<Vector3, float> add)
		{
			for (int i = Mathf.Max(0, first); i < Stages.Count; i++)
			{
				TravelRoad road = Stages[i].Road;
				if (road == null)
				{
					continue;
				}
				for (int w = 1; w < road.Count; w++)
				{
					if (road.LegTo(w) == TravelLeg.Foot)
					{
						add(Vector3.Lerp(road.Points[w - 1], road.Points[w], 0.5f), road.LegSeconds(w, BoatSpeed));
					}
				}
			}
		}

		/// <summary>
		/// Finds the index of the first stage on the way home from a quest
		/// </summary>
		/// <returns>The index, or 0 when there's none</returns>
		public int FirstHomeward()
		{
			int index = Stages.FindIndex((Stage stage) => stage.Homeward);
			return((index >= 0) ? index : 0);
		}

		/// <summary>
		/// Writes the trip as bytes
		/// </summary>
		/// <returns>The bytes</returns>
		public byte[] Encode()
		{
			ZPackage pkg = new ZPackage();
			pkg.Write(FormatVersion);
			pkg.Write(BoatSpeed);
			pkg.Write(Stages.Count);
			foreach (Stage stage in Stages)
			{
				pkg.Write(stage.Road != null);
				pkg.Write((stage.Road != null) ? stage.Road.Encode() : new byte[0]);
				pkg.Write(stage.Stay);
				pkg.Write(stage.Unteleportable);
				pkg.Write(stage.Homeward);
			}
			return(pkg.GetArray());
		}

		/// <summary>
		/// Reads a trip from the bytes it's written as
		/// </summary>
		/// <param name="bytes">The bytes, or null</param>
		/// <returns>The trip, or null when there's none or it can't be read</returns>
		public static TravelJourney Decode(byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0)
			{
				return(null);
			}
			try
			{
				ZPackage pkg = new ZPackage(bytes);
				if (pkg.ReadInt() != FormatVersion)
				{
					return(null);
				}
				TravelJourney journey = new TravelJourney();
				journey.BoatSpeed = pkg.ReadSingle();
				int count = pkg.ReadInt();
				for (int i = 0; i < count; i++)
				{
					bool hasRoad = pkg.ReadBool();
					byte[] road = pkg.ReadByteArray();
					Stage stage = new Stage();
					stage.Road = (hasRoad ? TravelRoad.Decode(road) : null);
					stage.Stay = pkg.ReadSingle();
					stage.Unteleportable = pkg.ReadBool();
					stage.Homeward = pkg.ReadBool();
					journey.Stages.Add(stage);
				}
				return(journey);
			}
			catch (System.Exception)
			{
				return(null);
			}
		}
	}
}
