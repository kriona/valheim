using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the sphere a player is within for the name of the village they're in to show at the top of the screen
	/// </summary>
	public class VillageNameHudTests
	{
		/// <summary>
		/// Checks points on the ground and in the air around a village of radius 50 whose middle is at 30 m
		/// </summary>
		/// <param name="x">How far across from the middle the point is</param>
		/// <param name="y">The point's height</param>
		/// <param name="within">Whether it's within the sphere</param>
		[Theory]
		[InlineData(0f, 30f, true)]
		[InlineData(45f, 30f, true)]
		[InlineData(0f, 75f, true)]
		[InlineData(0f, 85f, false)]
		[InlineData(40f, 65f, false)]
		[InlineData(51f, 30f, false)]
		[InlineData(30f, 0f, true)]
		public void OnlyTheAirJustOverTheVillageCounts(float x, float y, bool within)
		{
			Assert.Equal(within, VillageNameHud.InSphere(new Vector3(x, y, 0f), new Vector3(0f, 30f, 0f), 50f));
		}
	}
}
