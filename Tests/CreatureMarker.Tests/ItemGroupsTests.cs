using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace CreatureMarker.Tests
{
	/// <summary>
	/// Tests how items lying on the ground are grouped into one marker - only far off, only of the same kind and only
	/// lying together - and where a group's marker goes
	/// </summary>
	public class ItemGroupsTests
	{
		[Fact]
		public void FarItemsOfAKindLyingTogetherShareAMarker()
		{
			List<Vector3> positions = new List<Vector3> { new Vector3(30f, 0f, 0f), new Vector3(31f, 0f, 0f), new Vector3(30f, 0f, 2f) };
			List<List<int>> groups = ItemGroups.Group(positions, new List<string> { "$item_wood", "$item_wood", "$item_wood" }, Vector3.zero, 15f);
			Assert.Single(groups);
			Assert.Equal(new List<int> { 0, 1, 2 }, groups[0]);
		}

		[Fact]
		public void NearItemsKeepTheirOwnMarkers()
		{
			List<Vector3> positions = new List<Vector3> { new Vector3(5f, 0f, 0f), new Vector3(6f, 0f, 0f) };
			List<List<int>> groups = ItemGroups.Group(positions, new List<string> { "$item_wood", "$item_wood" }, Vector3.zero, 15f);
			Assert.Equal(2, groups.Count);
		}

		[Fact]
		public void ItemsOfAnotherKindOrFartherApartAreLeftOut()
		{
			List<Vector3> positions = new List<Vector3> { new Vector3(30f, 0f, 0f), new Vector3(31f, 0f, 0f), new Vector3(30f, 0f, 5f) };
			List<List<int>> groups = ItemGroups.Group(positions, new List<string> { "$item_wood", "$item_stone", "$item_wood" }, Vector3.zero, 15f);
			Assert.Equal(3, groups.Count);
		}

		[Fact]
		public void ARowOfItemsDoesntChainIntoOneGroup()
		{
			// Each 2 meters from the last, so only the first two lie within reach of the first
			List<Vector3> positions = new List<Vector3> { new Vector3(30f, 0f, 0f), new Vector3(32f, 0f, 0f), new Vector3(34f, 0f, 0f), new Vector3(36f, 0f, 0f) };
			List<List<int>> groups = ItemGroups.Group(positions, new List<string> { "a", "a", "a", "a" }, Vector3.zero, 15f);
			Assert.Equal(new List<int> { 0, 1 }, groups[0]);
			Assert.Equal(new List<int> { 2, 3 }, groups[1]);
		}

		[Fact]
		public void NoGroupingAtZero()
		{
			List<Vector3> positions = new List<Vector3> { new Vector3(30f, 0f, 0f), new Vector3(31f, 0f, 0f) };
			Assert.Equal(2, ItemGroups.Group(positions, new List<string> { "a", "a" }, Vector3.zero, 0f).Count);
		}

		[Fact]
		public void AFarItemDoesntJoinANearOne()
		{
			// The first lies within the distance and the second just past it, a meter apart
			List<Vector3> positions = new List<Vector3> { new Vector3(14.5f, 0f, 0f), new Vector3(15.5f, 0f, 0f) };
			Assert.Equal(2, ItemGroups.Group(positions, new List<string> { "a", "a" }, Vector3.zero, 15f).Count);
		}

		[Fact]
		public void AGroupsMarkerGoesInItsMiddle()
		{
			List<Vector3> positions = new List<Vector3> { new Vector3(30f, 0f, 0f), new Vector3(99f, 0f, 0f), new Vector3(32f, 2f, 2f) };
			Assert.Equal(new Vector3(31f, 1f, 1f), ItemGroups.Middle(positions, new List<int> { 0, 2 }));
		}
	}
}
