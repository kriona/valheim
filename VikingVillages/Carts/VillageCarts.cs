using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Keeps track of each village's cart - the one parked beside a house when the village is generated, or built by its
	/// vikings - noting it on the village's generator, and working out what it's up to for the vikings (see
	/// VillageLife.FindCartWork): whether it's home, standing loaded outside the village to fetch back, out of reach,
	/// or missing - destroyed, or gone a game day - to build again. A Meadows or Ashlands village keeps none
	/// </summary>
	/// <remarks>
	/// Each cart notes its village's farm, so a cart of the village's that isn't the one it notes - an old one that
	/// turned up again after the village built another - is taken down. A cart this client doesn't know, where it was
	/// last seen is loaded, was destroyed; one where it isn't loaded is only out of reach
	/// </remarks>
	internal static class VillageCarts
	{
		/// <summary>
		/// The cart's prefab
		/// </summary>
		internal const string CartPrefab = "Cart";

		/// <summary>
		/// On a farm's generator, its village's cart, where it was last seen, and since when in ZNet time ticks it has
		/// been out of reach, or 0 while it isn't
		/// </summary>
		private static readonly KeyValuePair<int, int> CartKey = ZDO.GetHashZDOID("kriona.VikingVillages.villageCart");
		private static readonly int SeenKey = "kriona.VikingVillages.villageCartSeen".GetStableHashCode();
		private static readonly int AwaySinceKey = "kriona.VikingVillages.villageCartAwaySince".GetStableHashCode();

		/// <summary>
		/// On a farm's generator, that its village has been given its cart as it was generated, which happens once
		/// </summary>
		internal static readonly int PlacedKey = "kriona.VikingVillages.villageCartPlaced".GetStableHashCode();

		/// <summary>
		/// On a cart, the farm of the village it belongs to
		/// </summary>
		internal static readonly int VillageKey = "kriona.VikingVillages.cartVillage".GetStableHashCode();

		/// <summary>
		/// Seconds between looks at each village's cart, and how long a village has to have been loaded before a cart
		/// it doesn't know counts as destroyed, so its ZDO has had time to arrive
		/// </summary>
		private const float CheckInterval = 5f;
		private const float SettleTime = 30f;

		/// <summary>
		/// How far a parked cart keeps from a house's walls, and from every door and doorstep, past its own half
		/// length, how far the ground under it can rise, how far along the wall from its middle it's tried, and how far
		/// above the ground it's put down, in meters
		/// </summary>
		private const float WallGap = 0.6f;
		private const float DoorClearance = 2.5f;
		private const float Flatness = 0.5f;
		private static readonly float[] Shifts = { 0f, 2f, -2f, 4f, -4f };
		private const float Lift = 0.1f;

		/// <summary>
		/// How far outside the village an old cart of its own is still taken down, in meters
		/// </summary>
		private const float ExtraReach = 40f;

		/// <summary>
		/// What a village's cart is up to - its state, the cart when it's loaded, and the village's old carts loaded
		/// near it to take down
		/// </summary>
		internal class Status
		{
			public CartRules.CartState State;
			public Vagon Cart;
			public ZDOID Id;
			public readonly List<Vagon> Extras = new List<Vagon>();
		}

		/// <summary>
		/// Each loaded village's cart, by its generator, as last looked at
		/// </summary>
		private static readonly Dictionary<ZDOID, Status> statuses = new Dictionary<ZDOID, Status>();

		/// <summary>
		/// When each village was first seen loaded, in Time.time seconds, by its generator
		/// </summary>
		private static readonly Dictionary<ZDOID, float> loadedSince = new Dictionary<ZDOID, float>();

		/// <summary>
		/// When the carts are next looked at, in Time.time seconds
		/// </summary>
		private static float nextCheck;

		/// <summary>
		/// Until when each village, by its generator, isn't given a cart again after no room was found for one, in
		/// Time.time seconds, and how long that lasts
		/// </summary>
		private static readonly Dictionary<ZDOID, float> noRoomUntil = new Dictionary<ZDOID, float>();
		private const float NoRoomWait = 600f;

		/// <summary>
		/// Notes that no room was found for a village's cart, so its vikings don't try to build one again for a while
		/// </summary>
		/// <param name="village">The village</param>
		public static void NoteNoRoom(Village village)
		{
			noRoomUntil[village.Zdo.m_uid] = Time.time + NoRoomWait;
		}

		/// <summary>
		/// Checks whether a village's vikings may try building its cart - not for a while after no room was found for one
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>True when they may</returns>
		public static bool MayBuild(Village village)
		{
			return(!noRoomUntil.TryGetValue(village.Zdo.m_uid, out float until) || Time.time >= until);
		}

		/// <summary>
		/// Looks at the cart of each loaded village every CheckInterval seconds
		/// </summary>
		public static void Update()
		{
			if (Time.time < nextCheck || ZNetScene.instance == null || ZDOMan.instance == null || ZNet.instance == null || ZoneSystem.instance == null)
			{
				return;
			}
			nextCheck = Time.time + CheckInterval;
			HashSet<ZDOID> seen = new HashSet<ZDOID>();
			foreach (Village village in Village.Loaded())
			{
				if (village == null || village.Zdo == null)
				{
					continue;
				}
				ZDOID id = village.Zdo.m_uid;
				seen.Add(id);
				if (!loadedSince.ContainsKey(id))
				{
					loadedSince[id] = Time.time;
				}
				statuses[id] = Check(village);
			}
			foreach (ZDOID gone in new List<ZDOID>(statuses.Keys))
			{
				if (!seen.Contains(gone))
				{
					statuses.Remove(gone);
					loadedSince.Remove(gone);
				}
			}
		}

		/// <summary>
		/// Finds what a village's cart is up to, as last looked at
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>What it's up to</returns>
		public static Status Of(Village village)
		{
			if (village == null || village.Zdo == null)
			{
				return(new Status { State = CartRules.CartState.None });
			}
			if (!statuses.TryGetValue(village.Zdo.m_uid, out Status status))
			{
				status = Check(village);
				statuses[village.Zdo.m_uid] = status;
			}
			return(status);
		}

		/// <summary>
		/// Looks at a village's cart again at once, after it was built or taken down
		/// </summary>
		/// <param name="village">The village</param>
		public static void Refresh(Village village)
		{
			if (village != null && village.Zdo != null)
			{
				statuses[village.Zdo.m_uid] = Check(village);
			}
		}

		/// <summary>
		/// Checks whether a village keeps a cart (see CartRules.Allowed)
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>True when it does</returns>
		public static bool Allowed(Village village)
		{
			return(village != null && CartRules.Allowed(VillageCooking.VillageBiome(village)));
		}

		/// <summary>
		/// Checks whether a cart belongs to a village
		/// </summary>
		/// <param name="cart">The cart's ZDO</param>
		/// <param name="village">The village</param>
		/// <returns>True when it does</returns>
		public static bool IsVillageCart(ZDO cart, Village village)
		{
			if (cart == null || village == null)
			{
				return(false);
			}
			Vector3 farm = cart.GetVec3(VillageKey, Vector3.zero);
			return(farm != Vector3.zero && Utils.DistanceXZ(farm, village.Farm) < 1f);
		}

		/// <summary>
		/// Works out what a village's cart is up to - adopting a cart of its own loaded here when it has none noted, and
		/// on the client that keeps track of the village, noting where its cart was last seen and since when it has
		/// been out of reach, and forgetting it once it counts as missing
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>What it's up to</returns>
		private static Status Check(Village village)
		{
			Status status = new Status();
			bool allowed = Allowed(village);
			bool owner = village.IsOwner();
			ZDOID id = village.Zdo.GetZDOID(CartKey);
			bool recorded = !id.IsNone();
			foreach (Vagon cart in Vagon.m_instances)
			{
				ZDO zdo = ((cart != null && cart.m_nview != null && cart.m_nview.IsValid()) ? cart.m_nview.GetZDO() : null);
				if (zdo == null || !IsVillageCart(zdo, village))
				{
					continue;
				}
				if (recorded && zdo.m_uid == id)
				{
					status.Cart = cart;
					continue;
				}
				if (!recorded && allowed && owner)
				{
					Record(village, zdo);
					id = zdo.m_uid;
					recorded = true;
					status.Cart = cart;
					continue;
				}
				if (village.DistanceOutside(cart.transform.position) <= ExtraReach && !CartHitch.PlayerUsing(cart))
				{
					status.Extras.Add(cart);
				}
			}
			status.Id = id;
			ZDO known = (recorded ? ZDOMan.instance.GetZDO(id) : null);
			Vector3 seen = village.Zdo.GetVec3(SeenKey, Vector3.zero);
			bool settled = (loadedSince.TryGetValue(village.Zdo.m_uid, out float since) && Time.time - since >= SettleTime);
			bool lastSeenLoaded = (settled && recorded && known == null && ZoneSystem.instance != null && ZoneSystem.instance.IsZoneLoaded(seen));
			bool loaded = (status.Cart != null);
			Vector3 position = (loaded ? status.Cart.transform.position : ((known != null) ? known.GetPosition() : seen));
			if (owner && known != null)
			{
				village.Zdo.Set(SeenKey, position);
			}
			double awaySeconds = 0;
			if (recorded && !loaded)
			{
				long awaySince = village.Zdo.GetLong(AwaySinceKey, 0L);
				if (awaySince == 0L && owner)
				{
					awaySince = NetTime.Now();
					village.Zdo.Set(AwaySinceKey, awaySince);
				}
				awaySeconds = ((awaySince > 0L) ? (NetTime.Now() - awaySince) / (double)System.TimeSpan.TicksPerSecond : 0);
			}
			else if (owner && village.Zdo.GetLong(AwaySinceKey, 0L) != 0L)
			{
				village.Zdo.Set(AwaySinceKey, 0L);
			}
			bool playerUsing = (loaded && CartHitch.PlayerUsing(status.Cart));
			bool inside = (loaded && (village.Contains(position) || village.InHouse(position, 0.5f)));
			double day = ((EnvMan.instance != null) ? EnvMan.instance.m_dayLengthSec : 1800.0);
			status.State = CartRules.Decide(allowed, recorded, known != null, lastSeenLoaded, loaded, playerUsing, inside, awaySeconds, day);
			if (status.State == CartRules.CartState.Missing && recorded && owner)
			{
				Plugin.Log.LogInfo("Cart: the village at " + village.Farm.ToString("F0") + " has lost its cart, last seen at " + seen.ToString("F0") + " - " + ((known == null) ? "destroyed" : "gone " + (awaySeconds / 60.0).ToString("F0") + " minutes"));
				Forget(village);
				status.Id = ZDOID.None;
				status.Cart = null;
			}
			return(status);
		}

		/// <summary>
		/// Notes a cart as a village's own
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="cart">The cart's ZDO</param>
		private static void Record(Village village, ZDO cart)
		{
			village.Zdo.Set(CartKey, cart.m_uid);
			village.Zdo.Set(SeenKey, cart.GetPosition());
			village.Zdo.Set(AwaySinceKey, 0L);
		}

		/// <summary>
		/// Forgets a village's cart, so its vikings build another - and the old one, should it turn up again, is taken
		/// down
		/// </summary>
		/// <param name="village">The village</param>
		private static void Forget(Village village)
		{
			village.Zdo.Set(CartKey, ZDOID.None);
			village.Zdo.Set(AwaySinceKey, 0L);
		}

		/// <summary>
		/// Parks a cart beside a house of a village as it's generated, once, when it keeps one - its vikings build any
		/// later one themselves
		/// </summary>
		/// <param name="village">The village, whose generator this client owns, with everything across it loaded</param>
		public static void PlaceGenerated(Village village)
		{
			if (village.Zdo.GetBool(PlacedKey))
			{
				return;
			}
			village.Zdo.Set(PlacedKey, true);
			if (!Allowed(village))
			{
				return;
			}
			if (!FindSpot(village, village.Farm, null, out Vector3 spot, out Quaternion rotation))
			{
				Plugin.Log.LogWarning("Cart: found no room for a cart beside any house of the village at " + village.Farm.ToString("F0"));
				return;
			}
			Spawn(village, spot, rotation);
		}

		/// <summary>
		/// Puts up a cart for a village, noting it as the village's own
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="spot">Where it stands, on the ground</param>
		/// <param name="rotation">Which way it faces</param>
		/// <returns>The cart, or null when the game has none</returns>
		public static Vagon Spawn(Village village, Vector3 spot, Quaternion rotation)
		{
			GameObject prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(CartPrefab) : null);
			if (prefab == null)
			{
				return(null);
			}
			GameObject made = Object.Instantiate(prefab, spot + Vector3.up * Lift, rotation);
			ZNetView view = made.GetComponent<ZNetView>();
			if (view == null || !view.IsValid())
			{
				return(null);
			}
			view.GetZDO().Set(VillageKey, village.Farm);
			WearNTear wear = made.GetComponent<WearNTear>();
			if (wear != null)
			{
				wear.OnPlaced();
			}
			Record(village, view.GetZDO());
			Plugin.Log.LogInfo("Cart: put up the cart of the village at " + village.Farm.ToString("F0") + " at " + spot.ToString("F1"));
			Vagon cart = made.GetComponent<Vagon>();
			Refresh(village);
			return(cart);
		}

		/// <summary>
		/// Finds where a village's cart is parked - outside beside a house of its core, nearest a point first: on the side
		/// opposite its door, or else to the door's left or right, never the side the door is on, keeping clear of every
		/// door and doorstep in the village, on fairly flat ground with nothing in the way, inside the village and away
		/// from the fire - off painted ground like a path where it can, or else on it. A village with no house of its
		/// own, like one a player started, parks it at the open spot nearest the point that fits
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="near">The point</param>
		/// <param name="ignore">A cart that's in the way of nothing, since it's the one being parked, or null</param>
		/// <param name="spot">Set to where the cart's origin stands, on the ground</param>
		/// <param name="rotation">Set to which way it faces - along the house's wall</param>
		/// <returns>False when there's no room anywhere</returns>
		public static bool FindSpot(Village village, Vector3 near, Vagon ignore, out Vector3 spot, out Quaternion rotation)
		{
			spot = Vector3.zero;
			rotation = Quaternion.identity;
			if (!VillageFurnishing.Footprint(CartPrefab, out Bounds size))
			{
				return(false);
			}
			List<Vector2> doors = DoorPoints(village);
			List<VillagePaths.House> houses = village.Houses.FindAll((VillagePaths.House house) => village.IsCore(house));
			houses.Sort((VillagePaths.House a, VillagePaths.House b) => Utils.DistanceXZ(a.Center, near).CompareTo(Utils.DistanceXZ(b.Center, near)));
			Collider[] own = ((ignore != null) ? ignore.GetComponentsInChildren<Collider>() : new Collider[0]);
			SetEnabled(own, false);
			try
			{
				for (int pass = 0; pass < 2; pass++)
				{
					foreach (VillagePaths.House house in houses)
					{
						if (BesideHouse(village, house, size, doors, pass == 1, out spot, out rotation))
						{
							return(true);
						}
					}
				}
				Quaternion turned = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
				float found = 0f;
				System.Predicate<Vector3> fits = (Vector3 point) => Fits(village, point, turned, size, doors, true, out found);
				if (houses.Count == 0 && village.OpenSpotNear(near, out Vector3 open, fits) && Fits(village, open, turned, size, doors, true, out found))
				{
					spot = new Vector3(open.x, found, open.z);
					rotation = turned;
					return(true);
				}
				return(false);
			}
			finally
			{
				SetEnabled(own, true);
			}
		}

		/// <summary>
		/// Turns a cart's colliders off or on, so it isn't in the way of the spot it's parked in
		/// </summary>
		/// <param name="colliders">The colliders</param>
		/// <param name="enabled">True to turn them on</param>
		private static void SetEnabled(Collider[] colliders, bool enabled)
		{
			foreach (Collider collider in colliders)
			{
				if (collider != null)
				{
					collider.enabled = enabled;
				}
			}
			if (colliders.Length > 0)
			{
				Physics.SyncTransforms();
			}
		}

		/// <summary>
		/// Finds a spot for a cart beside a house - along each side not facing its door, in the order CartRules.Sides
		/// gives, trying along the wall from its middle out
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="house">The house</param>
		/// <param name="size">The cart's size, around its origin</param>
		/// <param name="doors">Every door and doorstep in the village, seen from above</param>
		/// <param name="painted">True when painted ground like a path is fine</param>
		/// <param name="spot">Set to where the cart's origin stands, on the ground</param>
		/// <param name="rotation">Set to which way it faces</param>
		/// <returns>False when no side has room</returns>
		private static bool BesideHouse(Village village, VillagePaths.House house, Bounds size, List<Vector2> doors, bool painted, out Vector3 spot, out Quaternion rotation)
		{
			spot = Vector3.zero;
			rotation = Quaternion.identity;
			Vector2 front = Vector2.zero;
			foreach (Vector3 door in Village.Doors(house))
			{
				front += new Vector2(door.x - house.Center.x, door.z - house.Center.z);
			}
			float distance = CartRules.SideDistance(house.HalfSize, WallGap, size.extents.x);
			foreach (Vector2 side in CartRules.Sides(front, Random.value < 0.5f))
			{
				Vector3 outward = new Vector3(side.x, 0f, side.y);
				Vector3 along = new Vector3(-side.y, 0f, side.x);
				Quaternion turned = Quaternion.LookRotation(along);
				Vector3 middle = house.Center + outward * distance;
				foreach (float shift in Shifts)
				{
					Vector3 center = middle + along * shift;
					// The cart's own middle may be off its origin, so its origin is put where its middle comes out there
					Vector3 origin = center - turned * new Vector3(size.center.x, 0f, size.center.z);
					if (Fits(village, origin, turned, size, doors, painted, out float ground))
					{
						spot = new Vector3(origin.x, ground, origin.z);
						rotation = turned;
						return(true);
					}
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether a cart fits at a spot - inside the village, clear of every door and doorstep, away from the
		/// fire's flames, on open ground (see VillageFurnishing.OpenGround)
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="origin">Where the cart's origin would stand</param>
		/// <param name="rotation">Which way it would face</param>
		/// <param name="size">Its size, around its origin</param>
		/// <param name="doors">Every door and doorstep in the village, seen from above</param>
		/// <param name="painted">True when painted ground like a path is fine</param>
		/// <param name="ground">Set to the highest ground under it</param>
		/// <returns>True when it fits</returns>
		private static bool Fits(Village village, Vector3 origin, Quaternion rotation, Bounds size, List<Vector2> doors, bool painted, out float ground)
		{
			ground = origin.y;
			Vector3 center = origin + rotation * size.center;
			Vector2 flat = new Vector2(center.x, center.z);
			float reach = DoorClearance + Mathf.Max(size.extents.x, size.extents.z);
			if (CartRules.BlocksDoor(flat, doors, reach) || !village.Contains(center) || Village.NearFlames(center))
			{
				return(false);
			}
			return(VillageFurnishing.OpenGround(village, origin, rotation, size, Flatness, painted, out ground));
		}

		/// <summary>
		/// Lists every door and doorstep of a village's houses, seen from above
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The points</returns>
		private static List<Vector2> DoorPoints(Village village)
		{
			List<Vector2> points = new List<Vector2>();
			foreach (VillagePaths.House house in village.Houses)
			{
				foreach (Vector3 door in Village.Doors(house))
				{
					points.Add(new Vector2(door.x, door.z));
				}
				foreach (Vector3 step in Village.Doorsteps(house))
				{
					points.Add(new Vector2(step.x, step.z));
				}
			}
			return(points);
		}

		/// <summary>
		/// Describes a village's cart for the debug hover text of its fire
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The line, ending in a line break, or "" for a village that keeps none</returns>
		public static string Describe(Village village)
		{
			Status status = Of(village);
			if (status.State == CartRules.CartState.None)
			{
				return("");
			}
			string where = ((status.Cart != null) ? " at " + status.Cart.transform.position.ToString("F0") : "");
			string extras = ((status.Extras.Count > 0) ? ", " + status.Extras.Count + " old to take down" : "");
			return("Cart: " + status.State.ToString().ToLowerInvariant() + where + extras + "\n");
		}
	}
}
