using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests which roofs a valkyrie roosts on
	/// </summary>
	public class ValkyrieRoostTests
	{
		[Fact]
		public void SheRoostsOnlyOnRoofsFlatEnough()
		{
			Assert.True(ValkyrieRoost.FlatEnough(1f));
			Assert.True(ValkyrieRoost.FlatEnough(Mathf.Cos(26f * Mathf.Deg2Rad)));
			Assert.False(ValkyrieRoost.FlatEnough(Mathf.Cos(45f * Mathf.Deg2Rad)));
		}

		[Fact]
		public void AFloorNearTheGroundIsNoRoof()
		{
			Assert.True(ValkyrieRoost.HighEnough(36f, 30f));
			Assert.False(ValkyrieRoost.HighEnough(30.5f, 30f));
		}
	}
}
