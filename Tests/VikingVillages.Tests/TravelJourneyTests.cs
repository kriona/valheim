using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the trips the server keeps groups on - where a group is along one, the rest of it from a point, how it's
	/// written, and finding a portal on it that's gone
	/// </summary>
	public class TravelJourneyTests
	{
		/// <summary>
		/// A boat's speed, for the tests, in meters a second
		/// </summary>
		private const float Boat = 10f;

		/// <summary>
		/// Makes a road of straight legs, each on foot unless given
		/// </summary>
		/// <param name="points">The waypoints</param>
		/// <returns>The road</returns>
		private static TravelRoad Road(params Vector3[] points)
		{
			TravelRoad road = new TravelRoad();
			foreach (Vector3 point in points)
			{
				road.Add(point, TravelLeg.Foot);
			}
			return(road);
		}

		/// <summary>
		/// Makes a quest's trip - 100 m out on foot, 60 s of work, and 50 m back
		/// </summary>
		/// <returns>The trip</returns>
		private static TravelJourney Quest()
		{
			TravelJourney journey = new TravelJourney { BoatSpeed = Boat };
			journey.AddRoad(Road(Vector3.zero, new Vector3(100f, 0f, 0f)), false, false);
			journey.AddStay(60f);
			journey.AddRoad(Road(new Vector3(100f, 0f, 0f), new Vector3(100f, 0f, 50f)), true, true);
			return(journey);
		}

		[Fact]
		public void TripTakesEveryStageInTurn()
		{
			Assert.Equal(100f / TravelTime.Speed + 60f + 50f / TravelTime.Speed, Quest().TotalSeconds(), 3);
		}

		[Fact]
		public void GroupIsAlongTheLegItsOn()
		{
			TravelJourney.Place place = Quest().At(10f, Vector3.zero);
			Assert.Equal(0, place.Stage);
			Assert.Equal(1, place.Waypoint);
			Assert.Equal(10f * TravelTime.Speed, place.Position.x, 3);
		}

		[Fact]
		public void GroupAtWorkStaysWhereTheRoadEnded()
		{
			float out1 = 100f / TravelTime.Speed;
			TravelJourney.Place place = Quest().At(out1 + 30f, Vector3.zero);
			Assert.Equal(1, place.Stage);
			Assert.Equal(new Vector3(100f, 0f, 0f), place.Position);
		}

		[Fact]
		public void GroupPastTheEndIsWhereTheTripEnds()
		{
			TravelJourney.Place place = Quest().At(100000f, Vector3.zero);
			Assert.Equal(3, place.Stage);
			Assert.Equal(new Vector3(100f, 0f, 50f), place.Position);
			Assert.Equal(new Vector3(100f, 0f, 50f), Quest().End(Vector3.zero));
			Assert.Equal(new Vector3(100f, 0f, 0f), Quest().Approach(Vector3.zero));
		}

		[Fact]
		public void GroupGoingThroughAPortalIsAtItsNearEnd()
		{
			TravelRoad road = Road(Vector3.zero, new Vector3(10f, 0f, 0f));
			road.Add(new Vector3(5000f, 0f, 0f), TravelLeg.Portal);
			TravelJourney journey = TravelJourney.Along(road, Boat, false);
			TravelJourney.Place place = journey.At(10f / TravelTime.Speed + TravelTime.PortalSeconds / 2f, Vector3.zero);
			Assert.Equal(TravelLeg.Portal, place.Leg);
			Assert.Equal(new Vector3(10f, 0f, 0f), place.Position);
		}

		[Fact]
		public void RestOfTheTripStartsWhereTheGroupIs()
		{
			TravelJourney rest = Quest().From(10f);
			Assert.Equal(3, rest.Stages.Count);
			Assert.Equal(new Vector3(10f * TravelTime.Speed, 0f, 0f), rest.Stages[0].Road.Points[0]);
			Assert.Equal(Quest().TotalSeconds() - 10f, rest.TotalSeconds(), 3);
		}

		[Fact]
		public void RestOfAStayIsWhatsLeftOfIt()
		{
			float out1 = 100f / TravelTime.Speed;
			TravelJourney rest = Quest().From(out1 + 45f);
			Assert.Equal(2, rest.Stages.Count);
			Assert.Null(rest.Stages[0].Road);
			Assert.Equal(15f, rest.Stages[0].Stay, 3);
			Assert.True(rest.Stages[1].Homeward);
		}

		[Fact]
		public void RestOfAFinishedTripIsEmpty()
		{
			Assert.Empty(Quest().From(100000f).Stages);
		}

		[Fact]
		public void FirstHomewardStageIsFound()
		{
			Assert.Equal(2, Quest().FirstHomeward());
			Assert.Equal(0, TravelJourney.Along(Road(Vector3.zero, Vector3.one), Boat, false).FirstHomeward());
		}

		[Fact]
		public void FootLegsLeaveOutTheSea()
		{
			TravelRoad road = Road(Vector3.zero, new Vector3(50f, 0f, 0f));
			road.Add(new Vector3(150f, 0f, 0f), TravelLeg.Sea);
			TravelJourney journey = TravelJourney.Along(road, Boat, false);
			float total = 0f;
			int legs = 0;
			journey.FootLegs(0, (Vector3 middle, float seconds) =>
			{
				legs++;
				total += seconds;
				Assert.Equal(25f, middle.x, 3);
			});
			Assert.Equal(1, legs);
			Assert.Equal(50f / TravelTime.Speed, total, 3);
		}

		[Fact]
		public void TripSurvivesBeingWrittenAndRead()
		{
			TravelJourney journey = Quest();
			TravelJourney read = TravelJourney.Decode(journey.Encode());
			Assert.Equal(journey.Stages.Count, read.Stages.Count);
			Assert.Equal(journey.BoatSpeed, read.BoatSpeed);
			Assert.Equal(journey.TotalSeconds(), read.TotalSeconds(), 3);
			Assert.Null(read.Stages[1].Road);
			Assert.True(read.Stages[2].Unteleportable);
			Assert.True(read.Stages[2].Homeward);
			Assert.Null(TravelJourney.Decode(null));
			Assert.Null(TravelJourney.Decode(new byte[] { 9 }));
		}

		[Fact]
		public void APortalThatsGoneBreaksTheStageItsOn()
		{
			TravelRoad road = Road(Vector3.zero, new Vector3(10f, 0f, 0f));
			road.Add(new Vector3(5000f, 0f, 0f), TravelLeg.Portal);
			TravelJourney journey = new TravelJourney { BoatSpeed = Boat };
			journey.AddRoad(Road(Vector3.zero, Vector3.one), false, false);
			journey.AddRoad(road, true, true);
			TravelRoute.Portal portal = new TravelRoute.Portal { Entry = new Vector3(11f, 0f, 1f), Exit = new Vector3(5001f, 0f, 0f) };
			List<TravelRoute.Portal> none = new List<TravelRoute.Portal>();
			List<TravelRoute.Portal> standing = new List<TravelRoute.Portal> { portal };
			// The second stage carries ore, so it needs a portal that takes it
			Assert.Equal(-1, TravelPlanner.BrokenStage(journey, none, standing));
			Assert.Equal(1, TravelPlanner.BrokenStage(journey, standing, none));
			Assert.Equal(-1, TravelPlanner.BrokenStage(Quest(), none, none));
		}

		[Fact]
		public void RestOfARoadStartsWhereTheVikingIs()
		{
			TravelRoad road = Road(Vector3.zero, new Vector3(50f, 0f, 0f), new Vector3(100f, 0f, 0f));
			TravelRoad rest = VillageTravel.Rest(road, 2, new Vector3(60f, 0f, 0f));
			Assert.Equal(new List<Vector3> { new Vector3(60f, 0f, 0f), new Vector3(100f, 0f, 0f) }, rest.Points);
		}
	}
}
