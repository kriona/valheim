using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Finds the empty houses near a village its vikings take in and the farms near it, from the world's locations,
	/// which the game only lists on the server - so a client that owns a village's generator asks the server
	/// </summary>
	/// <remarks>
	/// A client asks about a village at most every AskInterval seconds and keeps the server's answer. Until an answer
	/// with every location generated has come, the village waits, the same as it waits for a house near it that hasn't
	/// been generated yet
	/// </remarks>
	internal static class VillageLocations
	{
		/// <summary>
		/// The routed RPCs - a client asking the server about a village, with its generator and where it stands, and the
		/// server's answer, with the generator and what it found
		/// </summary>
		private const string AskRpc = Plugin.PluginGuid + ".VillageLocationsAsk";
		private const string AnswerRpc = Plugin.PluginGuid + ".VillageLocationsAnswer";

		/// <summary>
		/// Seconds between a client's questions about the same village
		/// </summary>
		private const float AskInterval = 5f;

		/// <summary>
		/// How far from a farm's planned spot its generator can stand, in meters
		/// </summary>
		private const float GeneratorReach = 16f;

		/// <summary>
		/// What the server found around a village - its empty houses and the farms near it, each with whether every
		/// location they're found among has been generated
		/// </summary>
		private struct Answer
		{
			public bool OutpostsPlaced;
			public List<VillagePaths.House> Outposts;
			public bool FarmsPlaced;
			public Vector3 Self;
			public List<Vector3> Farms;
		}

		/// <summary>
		/// The server's answers about each village, by its generator, on a client
		/// </summary>
		private static readonly Dictionary<ZDOID, Answer> answers = new Dictionary<ZDOID, Answer>();

		/// <summary>
		/// When this client last asked about each village, by its generator, by Time.time
		/// </summary>
		private static readonly Dictionary<ZDOID, float> askedAt = new Dictionary<ZDOID, float>();

		/// <summary>
		/// Registers the RPCs with a world's routed RPC
		/// </summary>
		public static void Register()
		{
			if (ZRoutedRpc.instance == null)
			{
				return;
			}
			ZRoutedRpc.instance.Register<ZDOID, Vector3>(AskRpc, OnAsk);
			ZRoutedRpc.instance.Register<ZDOID, ZPackage>(AnswerRpc, OnAnswer);
		}

		/// <summary>
		/// Forgets the server's answers when leaving a world
		/// </summary>
		public static void Forget()
		{
			answers.Clear();
			askedAt.Clear();
		}

		/// <summary>
		/// Adds the empty houses within reach of a farm, which its living vikings have taken in, to a village's houses -
		/// found on the server, or from the server's answer on a client
		/// </summary>
		/// <param name="generator">The farm's generator</param>
		/// <param name="farm">Where the generator stands</param>
		/// <param name="houses">The village's houses, added to</param>
		/// <returns>False when a house near the farm hasn't been generated yet, or the server hasn't answered</returns>
		public static bool FindOutposts(ZDO generator, Vector3 farm, List<VillagePaths.House> houses)
		{
			if (ZNet.instance.IsServer())
			{
				return(VikingSpawns.FindOutposts(farm, houses));
			}
			if (!Known(generator, farm, out Answer answer) || !answer.OutpostsPlaced)
			{
				return(false);
			}
			houses.AddRange(answer.Outposts);
			return(true);
		}

		/// <summary>
		/// Finds a farm's own planned spot and the other abandoned farms near it, whose villages a path joins it to -
		/// found on the server, or from the server's answer on a client
		/// </summary>
		/// <param name="generator">The farm's generator</param>
		/// <param name="farm">Where the generator stands</param>
		/// <param name="self">The farm's planned spot</param>
		/// <param name="farms">The other farms' planned spots</param>
		/// <returns>False when one of them hasn't been generated yet, or the server hasn't answered</returns>
		public static bool FindNearbyFarms(ZDO generator, Vector3 farm, out Vector3 self, out List<Vector3> farms)
		{
			if (ZNet.instance.IsServer())
			{
				return(VikingSpawns.FindNearbyFarms(farm, GeneratorReach, out self, out farms));
			}
			self = farm;
			farms = new List<Vector3>();
			if (!Known(generator, farm, out Answer answer) || !answer.FarmsPlaced)
			{
				return(false);
			}
			self = answer.Self;
			farms.AddRange(answer.Farms);
			return(true);
		}

		/// <summary>
		/// Finds the server's answer about a village on a client, asking the server again when there's none with every
		/// location generated and it wasn't asked within AskInterval seconds
		/// </summary>
		/// <param name="generator">The farm's generator</param>
		/// <param name="farm">Where the generator stands</param>
		/// <param name="answer">The answer</param>
		/// <returns>False when the server hasn't answered yet</returns>
		private static bool Known(ZDO generator, Vector3 farm, out Answer answer)
		{
			ZDOID id = generator.m_uid;
			bool known = answers.TryGetValue(id, out answer);
			if (known && answer.OutpostsPlaced && answer.FarmsPlaced)
			{
				return(true);
			}
			if (ZRoutedRpc.instance != null && (!askedAt.TryGetValue(id, out float asked) || Time.time - asked >= AskInterval))
			{
				askedAt[id] = Time.time;
				ZRoutedRpc.instance.InvokeRoutedRPC(AskRpc, id, farm);
			}
			return(known);
		}

		/// <summary>
		/// Answers a client asking about a village, on the server - with its empty houses and the farms near it
		/// </summary>
		/// <param name="sender">The client</param>
		/// <param name="generator">The village's generator</param>
		/// <param name="farm">Where the generator stands</param>
		private static void OnAsk(long sender, ZDOID generator, Vector3 farm)
		{
			if (ZNet.instance == null || !ZNet.instance.IsServer() || ZoneSystem.instance == null || ZRoutedRpc.instance == null)
			{
				return;
			}
			List<VillagePaths.House> outposts = new List<VillagePaths.House>();
			bool outpostsPlaced = VikingSpawns.FindOutposts(farm, outposts);
			bool farmsPlaced = VikingSpawns.FindNearbyFarms(farm, GeneratorReach, out Vector3 self, out List<Vector3> farms);
			ZPackage pkg = new ZPackage();
			pkg.Write(outpostsPlaced);
			pkg.Write(outposts.Count);
			foreach (VillagePaths.House house in outposts)
			{
				pkg.Write(house.Center);
				pkg.Write(house.HalfSize);
			}
			pkg.Write(farmsPlaced);
			pkg.Write(self);
			pkg.Write(farms.Count);
			foreach (Vector3 spot in farms)
			{
				pkg.Write(spot);
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(sender, AnswerRpc, generator, pkg);
		}

		/// <summary>
		/// Keeps the server's answer about a village, on a client
		/// </summary>
		/// <param name="sender">The server</param>
		/// <param name="generator">The village's generator</param>
		/// <param name="pkg">What the server found around it</param>
		private static void OnAnswer(long sender, ZDOID generator, ZPackage pkg)
		{
			if (ZNet.instance == null || ZNet.instance.IsServer())
			{
				return;
			}
			try
			{
				Answer answer = new Answer();
				answer.OutpostsPlaced = pkg.ReadBool();
				int outposts = pkg.ReadInt();
				answer.Outposts = new List<VillagePaths.House>();
				for (int i = 0; i < outposts; i++)
				{
					Vector3 center = pkg.ReadVector3();
					float halfSize = pkg.ReadSingle();
					answer.Outposts.Add(new VillagePaths.House { Center = center, HalfSize = halfSize });
				}
				answer.FarmsPlaced = pkg.ReadBool();
				answer.Self = pkg.ReadVector3();
				int farms = pkg.ReadInt();
				answer.Farms = new List<Vector3>();
				for (int i = 0; i < farms; i++)
				{
					answer.Farms.Add(pkg.ReadVector3());
				}
				answers[generator] = answer;
			}
			catch (System.Exception exception)
			{
				Plugin.Log.LogWarning("Couldn't read the server's answer about the village with the generator " + generator + ": " + exception.Message);
			}
		}
	}
}
