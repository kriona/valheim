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
		[InlineData(0, 6)]
		[InlineData(1, 10)]
		[InlineData(3, 25)]
		[InlineData(6, 44)]
		public void BiggerBoatsCarryMore(int biome, int expected)
		{
			Assert.Equal(expected, TravelTime.BoatCrew(biome));
		}

		[Fact]
		public void TheDrakkarsStepsAreSatOnEvenlySpaced()
		{
			foreach (string bench in new[] { "sit_bench", "sit_bench (4)" })
			{
				List<float> across = new List<BoatSeats.Plan>(BoatSeats.For("VikingShip_Ashlands")).FindAll((BoatSeats.Plan seat) => seat.Beside && seat.Chair == bench).ConvertAll((BoatSeats.Plan seat) => seat.X);
				across.Sort();
				Assert.Equal(4, across.Count);
				Assert.Equal(-across[0], across[across.Count - 1], 3);
				for (int i = 2; i < across.Count; i++)
				{
					Assert.Equal(across[1] - across[0], across[i] - across[i - 1], 3);
				}
			}
		}

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		[InlineData(3)]
		[InlineData(6)]
		public void EveryBoatHasASeatForEachOfItsCrewTheHelmFirst(int biome)
		{
			BoatSeats.Plan[] plan = BoatSeats.For(TravelTime.BoatPrefab(biome));
			Assert.NotNull(plan);
			Assert.Equal(TravelTime.BoatCrew(biome), plan.Length);
			Assert.Equal(BoatSeats.Helm, plan[0].Chair);
			Assert.Single(plan, (BoatSeats.Plan seat) => seat.Chair == BoatSeats.Helm);
			Assert.Single(plan, (BoatSeats.Plan seat) => seat.Chair == "mast");
		}

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		[InlineData(3)]
		[InlineData(6)]
		public void SpotsOnTheDeckKeepApart(int biome)
		{
			List<BoatSeats.Plan> deck = new List<BoatSeats.Plan>(BoatSeats.For(TravelTime.BoatPrefab(biome))).FindAll((BoatSeats.Plan seat) => seat.OnDeck);
			for (int i = 0; i < deck.Count; i++)
			{
				for (int j = i + 1; j < deck.Count; j++)
				{
					float apart = Mathf.Sqrt((deck[i].X - deck[j].X) * (deck[i].X - deck[j].X) + (deck[i].Z - deck[j].Z) * (deck[i].Z - deck[j].Z));
					Assert.True(apart >= 0.9f, "Two spots on the deck are only " + apart + " m apart");
				}
			}
		}

		[Fact]
		public void BoatsAreFoundByName()
		{
			Assert.Equal(0, TravelTime.BiomeOfBoat("raft"));
			Assert.Equal(1, TravelTime.BiomeOfBoat("Karve"));
			Assert.Equal(3, TravelTime.BiomeOfBoat("LONGSHIP"));
			Assert.Equal(6, TravelTime.BiomeOfBoat("drakkar"));
			Assert.Equal(-1, TravelTime.BiomeOfBoat("canoe"));
			Assert.Equal(3, TravelTime.BiomeOfBoat("VikingShip"));
			Assert.Equal(6, TravelTime.BiomeOfBoat("vikingship_ashlands"));
			Assert.Equal(new[] { "Raft", "Karve", "VikingShip", "VikingShip_Ashlands" }, TravelTime.BoatPrefabNames());
			Assert.Equal(new[] { "raft", "karve", "longship", "drakkar" }, TravelTime.BoatNames());
		}
	}
}
