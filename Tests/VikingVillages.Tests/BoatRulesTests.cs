using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the rules of the boats vikings build - how many a group takes, who sits where, how they steer, and how long
	/// a viking swims before it drowns
	/// </summary>
	public class BoatRulesTests
	{
		[Theory]
		[InlineData(0, 4, 0)]
		[InlineData(1, 4, 1)]
		[InlineData(4, 4, 1)]
		[InlineData(5, 4, 2)]
		[InlineData(9, 2, 5)]
		public void GroupTakesAsManyBoatsAsItFills(int vikings, int crew, int expected)
		{
			Assert.Equal(expected, BoatRules.BoatsFor(vikings, crew));
		}

		[Fact]
		public void CrewsAreSharedOutEvenly()
		{
			List<string> ids = new List<string> { "e", "a", "d", "b", "c" };
			List<List<string>> crews = BoatRules.Crews(ids, 4);
			Assert.Equal(2, crews.Count);
			Assert.Equal(new List<string> { "a", "b", "c" }, crews[0]);
			Assert.Equal(new List<string> { "d", "e" }, crews[1]);
		}

		[Fact]
		public void OneBoatTakesTheWholeGroupWhenItFits()
		{
			List<List<string>> crews = BoatRules.Crews(new List<string> { "b", "a" }, 8);
			Assert.Single(crews);
			Assert.Equal("a", crews[0][0]);
		}

		[Fact]
		public void TheHardestBiomeBuildsTheBoats()
		{
			Assert.Equal(1, BoatRules.Builder(new List<string> { "a", "b", "c" }, new List<int> { 1, 3, 2 }));
			// A tie goes to the lowest ID
			Assert.Equal(2, BoatRules.Builder(new List<string> { "c", "b", "a" }, new List<int> { 3, 1, 3 }));
			Assert.Equal(-1, BoatRules.Builder(new List<string>(), new List<int>()));
		}

		[Fact]
		public void RudderTurnsTowardTheWaypoint()
		{
			Assert.Equal(0f, BoatRules.Rudder(Vector3.forward, Vector3.forward), 3);
			Assert.Equal(1f, BoatRules.Rudder(Vector3.forward, Vector3.right), 3);
			Assert.Equal(-1f, BoatRules.Rudder(Vector3.forward, Vector3.left), 3);
			Assert.Equal(0.5f, BoatRules.Rudder(Vector3.forward, new Vector3(Mathf.Tan(22.5f * Mathf.Deg2Rad), 0f, 1f)), 2);
			Assert.Equal(0f, BoatRules.Rudder(Vector3.forward, Vector3.zero), 3);
		}

		[Fact]
		public void SwimmingLastsAsLongAsAPlayersStamina()
		{
			// 75 base stamina at 5 a second with no skill
			Assert.Equal(15f, BoatRules.SwimSeconds(0f, 0f, 1f), 3);
			// More with food and skill
			Assert.Equal(125f / 2f, BoatRules.SwimSeconds(50f, 1f, 1f), 3);
			// A world that uses stamina at double the rate halves it
			Assert.Equal(7.5f, BoatRules.SwimSeconds(0f, 0f, 2f), 3);
		}

		[Theory]
		[InlineData(100f, 5f)]
		[InlineData(110f, 6f)]
		[InlineData(0f, 0f)]
		public void DrowningTakesAShareOfMaxHealth(float maxHealth, float expected)
		{
			Assert.Equal(expected, BoatRules.DrownDamage(maxHealth), 3);
		}

		[Fact]
		public void SeaStretchEndsAtTheFirstWaypointAshore()
		{
			TravelRoad road = new TravelRoad();
			road.Add(Vector3.zero, TravelLeg.Foot);
			road.Add(new Vector3(10f, 0f, 0f), TravelLeg.Foot);
			road.Add(new Vector3(70f, 0f, 0f), TravelLeg.Sea);
			road.Add(new Vector3(130f, 0f, 0f), TravelLeg.Sea);
			road.Add(new Vector3(150f, 0f, 0f), TravelLeg.Foot);
			Assert.Equal(4, BoatRules.Ashore(road, 2, out int lastSea));
			Assert.Equal(3, lastSea);
			road = new TravelRoad();
			road.Add(Vector3.zero, TravelLeg.Foot);
			road.Add(new Vector3(70f, 0f, 0f), TravelLeg.Sea);
			Assert.Equal(2, BoatRules.Ashore(road, 1, out lastSea));
			Assert.Equal(1, lastSea);
		}

		[Theory]
		[InlineData(0, 2)]
		[InlineData(1, 4)]
		[InlineData(3, 8)]
		[InlineData(6, 10)]
		public void BiggerBoatsCarryMore(int biome, int expected)
		{
			Assert.Equal(expected, TravelTime.BoatCrew(biome));
		}
	}
}
