using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests how a valkyrie leads her feathers at a moving target
	/// </summary>
	public class ValkyrieVolleyTests
	{
		/// <summary>
		/// Checks that something shot along a way at a speed meets a moving target
		/// </summary>
		/// <param name="from">Where it's shot from</param>
		/// <param name="target">Where the target starts</param>
		/// <param name="velocity">How the target moves</param>
		/// <param name="speed">How fast it flies</param>
		private static void AssertMeets(Vector3 from, Vector3 target, Vector3 velocity, float speed)
		{
			Vector3 way = ValkyrieVolley.Lead(from, target, velocity, speed);
			Assert.Equal(1f, way.magnitude, 3);
			float closest = float.MaxValue;
			for (float t = 0f; t < 5f; t += 0.001f)
			{
				closest = Mathf.Min(closest, Vector3.Distance(from + way * speed * t, target + velocity * t));
			}
			Assert.True(closest < 0.1f, "missed by " + closest);
		}

		[Fact]
		public void SheShootsStraightAtATargetStandingStill()
		{
			Vector3 way = ValkyrieVolley.Lead(new Vector3(0f, 10f, 0f), new Vector3(0f, 0f, 10f), Vector3.zero, 45f);
			Assert.Equal(new Vector3(0f, -1f, 1f).normalized.z, way.z, 3);
			Assert.Equal(new Vector3(0f, -1f, 1f).normalized.y, way.y, 3);
		}

		[Fact]
		public void SheLeadsATargetRunningAcrossHerAim()
		{
			AssertMeets(new Vector3(0f, 8f, 0f), new Vector3(0f, 0f, 12f), new Vector3(7f, 0f, 0f), 45f);
		}

		[Fact]
		public void SheLeadsATargetRunningAtOrAwayFromHer()
		{
			AssertMeets(Vector3.zero, new Vector3(0f, 0f, 15f), new Vector3(0f, 0f, -7f), 45f);
			AssertMeets(Vector3.zero, new Vector3(0f, 0f, 15f), new Vector3(0f, 0f, 7f), 45f);
		}

		[Fact]
		public void SheShootsStraightAtATargetTooFastToCatch()
		{
			Vector3 way = ValkyrieVolley.Lead(Vector3.zero, new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 60f), 45f);
			Assert.Equal(1f, way.z, 3);
		}
	}
}
