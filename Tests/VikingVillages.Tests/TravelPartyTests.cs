using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests what a group of vikings can do on the road together, the places a quest could go, and how a road is
	/// described for the log
	/// </summary>
	public class TravelPartyTests
	{
		[Fact]
		public void PartyKeepsToItsEasiestAndSailsItsHardest()
		{
			TravelParty party = new TravelParty();
			party.Add(3, false, true);
			party.Add(1, false, false);
			party.Add(6, false, false);
			Assert.Equal(1, party.Easiest);
			Assert.Equal(6, party.Hardest);
			Assert.False(party.Unteleportable);
		}

		[Fact]
		public void OneVikingWithOreHoldsTheWholePartyBack()
		{
			TravelParty party = new TravelParty();
			party.Add(2, false, true);
			party.Add(2, true, false);
			party.Add(2, false, false);
			Assert.True(party.Unteleportable);
		}

		[Fact]
		public void TheFirstVikingSetsBothBiomes()
		{
			TravelParty party = new TravelParty();
			party.Add(4, false, true);
			Assert.Equal(4, party.Easiest);
			Assert.Equal(4, party.Hardest);
		}

		[Fact]
		public void PartySwimsAsFarAsItsWeakestSwimmer()
		{
			TravelParty party = new TravelParty();
			party.AddSwimmer(80f, true);
			party.AddSwimmer(45f, false);
			party.AddSwimmer(120f, false);
			Assert.Equal(45f, party.SwimMeters);
		}

		[Fact]
		public void PartySurvivesBeingWrittenAndRead()
		{
			TravelParty party = new TravelParty { Easiest = 1, Hardest = 5, Unteleportable = true, SwimMeters = 62.5f };
			ZPackage pkg = new ZPackage();
			party.Write(pkg);
			TravelParty read = TravelParty.Read(new ZPackage(pkg.GetArray()));
			Assert.Equal(1, read.Easiest);
			Assert.Equal(5, read.Hardest);
			Assert.True(read.Unteleportable);
			Assert.Equal(62.5f, read.SwimMeters);
		}

		[Fact]
		public void QuestPlacesAreTheNearestFew()
		{
			List<Vector3> spots = new List<Vector3> { new Vector3(500f, 0f, 0f), new Vector3(100f, 0f, 0f), new Vector3(0f, 0f, 300f), new Vector3(-900f, 0f, 0f) };
			List<Vector3> nearest = QuestTargets.Nearest(spots, Vector3.zero, 3);
			Assert.Equal(new List<Vector3> { new Vector3(100f, 0f, 0f), new Vector3(0f, 0f, 300f), new Vector3(500f, 0f, 0f) }, nearest);
			Assert.Equal(2, QuestTargets.Nearest(spots, Vector3.zero, 2).Count);
			Assert.Empty(QuestTargets.Nearest(new List<Vector3>(), Vector3.zero, 3));
		}

		[Theory]
		[InlineData(0, 16, 8, 0)]
		[InlineData(1, 16, 8, 0)]
		[InlineData(2, 16, 8, 1)]
		[InlineData(15, 16, 8, 7)]
		[InlineData(5, 10, 8, 4)]
		public void SpotsOnARingFallInEqualSlices(int index, int spots, int sectors, int expected)
		{
			Assert.Equal(expected, QuestTargets.SectorOf(index, spots, sectors));
		}

		[Fact]
		public void RoadIsDescribedByHowItsCrossed()
		{
			TravelRoad road = new TravelRoad();
			road.Add(Vector3.zero, TravelLeg.Foot);
			road.Add(new Vector3(10f, 0f, 0f), TravelLeg.Foot);
			Assert.Equal("road", TravelPlanner.DescribeRoad(road));
			road.Add(new Vector3(20f, 0f, 0f), TravelLeg.Sea);
			Assert.Equal("road and sea", TravelPlanner.DescribeRoad(road));
			road.Add(new Vector3(900f, 0f, 0f), TravelLeg.Portal);
			road.Add(new Vector3(1900f, 0f, 0f), TravelLeg.Portal);
			Assert.Equal("road, sea and 2 portals", TravelPlanner.DescribeRoad(road));
		}
	}
}
