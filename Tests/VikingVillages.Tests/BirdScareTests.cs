using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how near a viking has to be to scare off a bird
	/// </summary>
	public class BirdScareTests
	{
		[Fact]
		public void AVikingInsideTheDangerDistanceScaresABird()
		{
			List<Vector3> vikings = new List<Vector3> { new Vector3(10f, 0f, 0f), new Vector3(3f, 0f, 0f) };
			Assert.True(BirdScare.AnyWithin(vikings, Vector3.zero, 4f));
		}

		[Fact]
		public void AVikingAtOrPastTheDangerDistanceDoesnt()
		{
			Assert.False(BirdScare.AnyWithin(new List<Vector3> { new Vector3(4f, 0f, 0f) }, Vector3.zero, 4f));
			Assert.False(BirdScare.AnyWithin(new List<Vector3> { new Vector3(0f, 5f, 0f) }, Vector3.zero, 4f));
			Assert.False(BirdScare.AnyWithin(new List<Vector3>(), Vector3.zero, 4f));
		}
	}
}
