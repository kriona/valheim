using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Xunit;

namespace VikingVillages.Tests
{
	/// <summary>
	/// Tests the server's answer to a quest's road - that each of its arguments goes through the game's routed RPC
	/// packing and comes back out as the handler's parameters
	/// </summary>
	public class TravelPlannerTests
	{
		/// <summary>
		/// Packs the answer's arguments the way ZRoutedRpc.InvokeRoutedRPC does and unpacks them for OnQuestRouted the way
		/// its RoutedMethod does
		/// </summary>
		/// <param name="arguments">The arguments after the sender</param>
		/// <returns>The values unpacked, the sender first, and the handler's parameter count</returns>
		private static (List<object> values, int count) RoundTrip(object[] arguments)
		{
			ZPackage pkg = new ZPackage();
			ZRpc.Serialize(arguments, ref pkg);
			pkg.SetPos(0);
			ParameterInfo[] parameters = typeof(TravelPlanner).GetMethod(nameof(TravelPlanner.OnQuestRouted), BindingFlags.Static | BindingFlags.NonPublic).GetParameters();
			List<object> values = new List<object> { 7L };
			ZRpc.Deserialize(parameters, pkg, ref values);
			return((values, parameters.Length));
		}

		/// <summary>
		/// Checks that an answer with a sea road unpacks a value for each of OnQuestRouted's parameters, each as it was sent
		/// </summary>
		[Fact]
		public void TheAnswerUnpacksAValueForEveryParameter()
		{
			TravelRoad road = new TravelRoad();
			road.Add(new Vector3(1f, 2f, 3f), false);
			road.Add(new Vector3(40f, 0f, 5f), true);
			(List<object> values, int count) = RoundTrip(new object[] { 1234567890123L, 27.5f, 31f, 3, new ZPackage(road.Encode()) });
			Assert.Equal(count, values.Count);
			Assert.Equal(1234567890123L, values[1]);
			Assert.Equal(27.5f, values[2]);
			Assert.Equal(31f, values[3]);
			Assert.Equal(3, values[4]);
			TravelRoad sea = TravelRoad.Decode(((ZPackage)values[5]).GetArray());
			Assert.Equal(road.Points, sea.Points);
			Assert.Equal(road.Legs, sea.Legs);
		}

		/// <summary>
		/// Checks that an answer going on foot, with an empty package for its sea, unpacks to a road with no waypoints
		/// </summary>
		[Fact]
		public void AnAnswerOnFootUnpacksAnEmptySea()
		{
			(List<object> values, int count) = RoundTrip(new object[] { 5L, 300f, 300f, -1, new ZPackage() });
			Assert.Equal(count, values.Count);
			Assert.Equal(-1, values[4]);
			Assert.Empty(TravelRoad.Decode(((ZPackage)values[5]).GetArray()).Points);
		}
	}
}
