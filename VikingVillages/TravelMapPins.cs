using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Pins every group of vikings on the road or out on a quest on the large map in debug mode, where it is now -
	/// labeled with how many, what brings them, their names and what they're doing - so they can be followed while
	/// they're out of the world
	/// </summary>
	/// <remarks>
	/// Only the server knows where the groups are (see VillageTravel.MapSpots), so a client asks it every
	/// RefreshInterval seconds while the large map is open, and the server answers with every group's spot. The pins are
	/// never saved, and come off as the map closes or debug mode is turned off
	/// </remarks>
	internal static class TravelMapPins
	{
		/// <summary>
		/// Where a group is now, and its pin's label
		/// </summary>
		internal struct Spot
		{
			public Vector3 Position;
			public string Label;
		}

		/// <summary>
		/// The routed RPCs - a client asking the server for every group's spot, and the server's answer
		/// </summary>
		private const string RequestRpc = Plugin.PluginGuid + ".MapSpotsRequest";
		private const string AnswerRpc = Plugin.PluginGuid + ".MapSpotsAnswer";

		/// <summary>
		/// How often the pins are moved to where the groups are now while the large map is open, in seconds
		/// </summary>
		private const float RefreshInterval = 2f;

		/// <summary>
		/// The map's icon the pins show - its plain dot
		/// </summary>
		private const Minimap.PinType PinIcon = Minimap.PinType.Icon3;

		/// <summary>
		/// The pins on the map now, and when they're next moved, in Time.time seconds
		/// </summary>
		private static readonly List<Minimap.PinData> pins = new List<Minimap.PinData>();
		private static float nextRefresh;

		/// <summary>
		/// Registers the RPCs with a world's routed RPC, forgetting the pins of the world being left, which went with its
		/// map
		/// </summary>
		public static void Register()
		{
			ZRoutedRpc.instance.Register(RequestRpc, OnRequest);
			ZRoutedRpc.instance.Register<ZPackage>(AnswerRpc, OnAnswer);
			pins.Clear();
			nextRefresh = 0f;
		}

		/// <summary>
		/// Shows the pins while debug mode is on and the large map is open, moving them every RefreshInterval seconds,
		/// and takes them off otherwise - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (!Showing())
			{
				Clear();
				nextRefresh = 0f;
				return;
			}
			if (Time.time < nextRefresh)
			{
				return;
			}
			nextRefresh = Time.time + RefreshInterval;
			if (ZNet.instance.IsServer())
			{
				Show(VillageTravel.MapSpots());
			}
			else
			{
				ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), RequestRpc);
			}
		}

		/// <summary>
		/// Checks whether the pins show - debug mode is on and the large map is open in a world
		/// </summary>
		/// <returns>True when they do</returns>
		private static bool Showing()
		{
			Minimap map = Minimap.instance;
			return(Player.m_debugMode && map != null && map.m_mode == Minimap.MapMode.Large && ZNet.instance != null && ZRoutedRpc.instance != null);
		}

		/// <summary>
		/// Labels a group's pin - how many, what brings them, their names and what they're doing
		/// </summary>
		/// <param name="arrival">The group</param>
		/// <returns>The label, like "3 vikings out on MineCopper for CopperOre (Astrid, Ulf), at work"</returns>
		internal static string Label(VillageArrivals.Arrival arrival)
		{
			string names = ((arrival.Names.Count > 0) ? " (" + string.Join(", ", arrival.Names) + ")" : "");
			string doing = ((arrival.Where.Length > 0) ? ", " + arrival.Where : "");
			return(VillageArrivals.Vikings(arrival.Count) + " " + arrival.What + names + doing);
		}

		/// <summary>
		/// Answers a client on the server with every group's spot
		/// </summary>
		/// <param name="sender">The client</param>
		private static void OnRequest(long sender)
		{
			if (ZNet.instance == null || !ZNet.instance.IsServer())
			{
				return;
			}
			List<Spot> spots = VillageTravel.MapSpots();
			ZPackage pkg = new ZPackage();
			pkg.Write(spots.Count);
			foreach (Spot spot in spots)
			{
				pkg.Write(spot.Position);
				pkg.Write(spot.Label);
			}
			ZRoutedRpc.instance.InvokeRoutedRPC(sender, AnswerRpc, pkg);
		}

		/// <summary>
		/// Pins the spots the server answered with, while the pins still show
		/// </summary>
		/// <param name="sender">The server</param>
		/// <param name="pkg">The spots</param>
		private static void OnAnswer(long sender, ZPackage pkg)
		{
			int count = pkg.ReadInt();
			List<Spot> spots = new List<Spot>();
			for (int i = 0; i < count; i++)
			{
				Vector3 position = pkg.ReadVector3();
				spots.Add(new Spot { Position = position, Label = pkg.ReadString() });
			}
			if (Showing())
			{
				Show(spots);
			}
		}

		/// <summary>
		/// Puts a pin on the map at each spot, in place of the pins there were
		/// </summary>
		/// <param name="spots">The spots</param>
		private static void Show(List<Spot> spots)
		{
			Clear();
			Minimap map = Minimap.instance;
			if (map == null)
			{
				return;
			}
			foreach (Spot spot in spots)
			{
				pins.Add(map.AddPin(spot.Position, PinIcon, spot.Label, false, false));
			}
		}

		/// <summary>
		/// Takes the pins off the map
		/// </summary>
		private static void Clear()
		{
			if (pins.Count == 0)
			{
				return;
			}
			Minimap map = Minimap.instance;
			if (map != null)
			{
				foreach (Minimap.PinData pin in pins)
				{
					map.RemovePin(pin);
				}
			}
			pins.Clear();
		}
	}
}
