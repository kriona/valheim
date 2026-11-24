using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests a viking's road - how long the rest of it takes on foot and by sea, and how it's written
	/// </summary>
	public class TravelRoadTests
	{
		/// <summary>
		/// Makes a road of 50 m on foot, 40 m by sea and 10 m on foot
		/// </summary>
		/// <returns>The road</returns>
		private static TravelRoad Mixed()
		{
			TravelRoad road = new TravelRoad();
			road.Add(Vector3.zero, false);
			road.Add(new Vector3(50f, 0f, 0f), false);
			road.Add(new Vector3(50f, 0f, 40f), true);
			road.Add(new Vector3(60f, 0f, 40f), false);
			return(road);
		}

		[Fact]
		public void SecondsTakeEachLegAtItsSpeed()
		{
			TravelRoad road = Mixed();
			Assert.Equal(50f / TravelTime.Speed + 40f / 8f + 10f / TravelTime.Speed, road.TotalSeconds(8f), 3);
			// From 20 m along the first leg
			Assert.Equal(30f / TravelTime.Speed + 40f / 8f + 10f / TravelTime.Speed, road.Seconds(1, new Vector3(20f, 3f, 0f), 8f), 3);
			Assert.Equal(0f, road.Seconds(4, Vector3.zero, 8f), 3);
		}

		[Fact]
		public void MetersCountEveryLegAlike()
		{
			Assert.Equal(100f, Mixed().Meters(0, Vector3.zero), 3);
			Assert.Equal(50f, Mixed().Meters(2, new Vector3(50f, 0f, 0f)), 3);
		}

		[Fact]
		public void TheFirstWaypointIsNeverASeaLeg()
		{
			TravelRoad road = new TravelRoad();
			road.Add(Vector3.zero, true);
			Assert.False(road.SeaLeg(0));
			Assert.True(Mixed().SeaLeg(2));
			Assert.False(Mixed().SeaLeg(3));
			Assert.False(Mixed().SeaLeg(9));
		}

		[Fact]
		public void ApproachIsTheWaypointBeforeTheEnd()
		{
			Assert.Equal(new Vector3(50f, 0f, 40f), Mixed().Approach());
			Assert.Equal(Vector3.zero, new TravelRoad().Approach());
		}

		[Fact]
		public void RoadSurvivesBeingWrittenAndRead()
		{
			TravelRoad road = Mixed();
			TravelRoad read = TravelRoad.Decode(road.Encode());
			Assert.Equal(road.Points, read.Points);
			Assert.Equal(road.Sea, read.Sea);
			Assert.Equal(0, TravelRoad.Decode(new byte[] { 1, 2 }).Count);
			Assert.Equal(0, TravelRoad.Decode(null).Count);
			Assert.Equal(0, TravelRoad.Decode(new byte[0]).Count);
		}
	}
}
