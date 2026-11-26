using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The carts vikings on the road take along - one each at most, noted on the viking. While the viking is in the
	/// world it pulls its cart; as it's taken off the road its cart goes with it, what the cart holds kept on the viking
	/// (see Stow), and the cart is put back behind it, holding the same, as soon as it's back in the world (see
	/// VillageLife.UpdateRoadCart). A cart that reaches a village with its viking becomes one of the village's, to put
	/// away what it holds (see Arrive)
	/// </summary>
	internal static class RoadCarts
	{
		/// <summary>
		/// On a living viking, the cart it takes along on the road, and while it's off the road, what that cart holds, as
		/// the game's container writes it - empty while it has no cart kept
		/// </summary>
		public static readonly KeyValuePair<int, int> CartKey = ZDO.GetHashZDOID("kriona.VikingVillages.vikingRoadCart");
		public static readonly int LoadKey = "kriona.VikingVillages.vikingRoadCartLoad".GetStableHashCode();

		/// <summary>
		/// On a living viking, whether it has a cart kept while it's off the road, which may hold nothing
		/// </summary>
		public static readonly int KeptKey = "kriona.VikingVillages.vikingRoadCartKept".GetStableHashCode();

		/// <summary>
		/// How far behind a viking its cart is put back, and how far above the ground, in meters
		/// </summary>
		private const float BehindDistance = 2.5f;
		private const float Lift = 0.3f;

		/// <summary>
		/// Notes a cart as the one a viking takes along on the road
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <param name="cart">The cart</param>
		public static void Take(ZDO viking, Vagon cart)
		{
			viking.Set(CartKey, cart.m_nview.GetZDO().m_uid);
		}

		/// <summary>
		/// Finds the cart a viking takes along on the road, when it's loaded
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>The cart, or null when it has none or it isn't loaded</returns>
		public static Vagon Of(ZDO viking)
		{
			ZDOID id = viking.GetZDOID(CartKey);
			if (id.IsNone() || ZDOMan.instance == null || ZNetScene.instance == null)
			{
				return(null);
			}
			ZDO cart = ZDOMan.instance.GetZDO(id);
			ZNetView view = ((cart != null) ? ZNetScene.instance.FindInstance(cart) : null);
			return((view != null) ? view.GetComponent<Vagon>() : null);
		}

		/// <summary>
		/// Checks whether a viking has a cart along on the road, in the world or kept
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>True when it has</returns>
		public static bool Has(ZDO viking)
		{
			return(!viking.GetZDOID(CartKey).IsNone() || viking.GetBool(KeptKey));
		}

		/// <summary>
		/// Lets go of the cart a viking takes along on the road, leaving it where it stands
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		public static void Forget(ZDO viking)
		{
			viking.Set(CartKey, ZDOID.None);
			viking.Set(KeptKey, false);
			viking.Set(LoadKey, new byte[0]);
		}

		/// <summary>
		/// Takes the cart a viking takes along out of the world with it, as the server takes it off the road - keeping
		/// what the cart holds on the viking, so its cart comes back with it holding the same (see PutBack)
		/// </summary>
		/// <param name="viking">The viking's ZDO, on the server</param>
		public static void Stow(ZDO viking)
		{
			ZDOID id = viking.GetZDOID(CartKey);
			if (id.IsNone() || ZDOMan.instance == null)
			{
				return;
			}
			ZDO cart = ZDOMan.instance.GetZDO(id);
			viking.Set(CartKey, ZDOID.None);
			if (cart == null)
			{
				// Its cart was destroyed or lost on the way, so it goes on without one
				return;
			}
			byte[] items = cart.GetByteArray(ZDOVars.s_items);
			viking.Set(LoadKey, items ?? new byte[0]);
			viking.Set(KeptKey, true);
			ZDOMan.instance.DestroyZDO(cart);
		}

		/// <summary>
		/// Takes a viking's cart down with one swing of its hammer before it steps through a portal or sets sail - keeping
		/// what the cart holds on the viking, the way the server does as it takes the viking off the road (see Stow), so
		/// the viking builds it back holding the same on the other side (see PutBack)
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <param name="cart">The cart</param>
		public static void PackUp(ZDO viking, Vagon cart)
		{
			CartHitch.Unhitch(cart);
			ZPackage items = new ZPackage();
			cart.m_container.GetInventory().Save(items);
			viking.Set(LoadKey, items.GetArray());
			viking.Set(KeptKey, true);
			viking.Set(CartKey, ZDOID.None);
			// It's taken down whole, so nothing drops - it goes along with the viking
			cart.m_nview.ClaimOwnership();
			cart.m_nview.Destroy();
		}

		/// <summary>
		/// Reads what a viking's kept cart holds
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>What it holds, or null when it has no cart kept or it can't be read</returns>
		private static Inventory KeptLoad(ZDO viking)
		{
			byte[] items = viking.GetByteArray(LoadKey);
			if (!viking.GetBool(KeptKey) || items == null || items.Length == 0)
			{
				return(null);
			}
			try
			{
				Inventory load = new Inventory("", null, 8, 8);
				load.Load(new ZPackage(items));
				return(load);
			}
			catch (System.Exception)
			{
				return(null);
			}
		}

		/// <summary>
		/// Weighs what a viking's kept cart holds
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <returns>The weight, or 0 with no cart kept</returns>
		public static float KeptWeight(ZDO viking)
		{
			Inventory load = KeptLoad(viking);
			return((load != null) ? load.GetTotalWeight() : 0f);
		}

		/// <summary>
		/// Checks whether a viking's cart, in the world or kept, holds something a portal won't take (see
		/// TravelPlanner.Unteleportable)
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <returns>True when it does</returns>
		public static bool CarriesUnteleportable(Humanoid viking)
		{
			ZDO zdo = viking.m_nview.GetZDO();
			Vagon cart = Of(zdo);
			Inventory load = ((cart != null && cart.m_container != null) ? cart.m_container.GetInventory() : KeptLoad(zdo));
			return(TravelPlanner.Unteleportable(load));
		}

		/// <summary>
		/// Puts a viking's kept cart back in the world behind it, holding what it held, as the viking comes back into the
		/// world - built back with one swing of its hammer, hooked up, ready to pull
		/// </summary>
		/// <param name="puller">The viking, owned by this client</param>
		/// <param name="village">The village the cart belongs to as it's put back, or null on the road</param>
		/// <returns>The cart, or null when the viking has none kept or the game has no cart</returns>
		public static Vagon PutBack(Humanoid puller, Village village)
		{
			ZDO viking = puller.m_nview.GetZDO();
			if (!viking.GetBool(KeptKey))
			{
				return(null);
			}
			byte[] items = viking.GetByteArray(LoadKey);
			viking.Set(KeptKey, false);
			viking.Set(LoadKey, new byte[0]);
			VikingPoses.Swing(puller);
			Vagon cart = Spawn(puller, village);
			if (cart == null)
			{
				return(null);
			}
			if (items != null && items.Length > 0)
			{
				cart.m_container.GetInventory().Load(new ZPackage(items));
			}
			Take(viking, cart);
			CartHitch.Hitch(cart, puller, -puller.transform.forward);
			Plugin.Log.LogInfo("Cart: " + TestLog.Name(viking) + " came back into the world at " + puller.transform.position.ToString("F0") + " with its cart, holding " + cart.m_container.GetInventory().NrOfItems() + " stacks");
			return(cart);
		}

		/// <summary>
		/// Puts up a new cart behind a viking, noted as the village's when there is one
		/// </summary>
		/// <param name="puller">The viking</param>
		/// <param name="village">The village the cart belongs to, or null</param>
		/// <returns>The cart, or null when the game has none</returns>
		public static Vagon Spawn(Humanoid puller, Village village)
		{
			GameObject prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(VillageCarts.CartPrefab) : null);
			if (prefab == null)
			{
				return(null);
			}
			Vector3 behind = -puller.transform.forward;
			behind.y = 0f;
			Vector3 spot = puller.transform.position + behind.normalized * BehindDistance;
			spot.y = Mathf.Max(ZoneSystem.instance.GetGroundHeight(spot), puller.transform.position.y - 0.5f) + Lift;
			GameObject made = Object.Instantiate(prefab, spot, Quaternion.LookRotation(puller.transform.forward));
			ZNetView view = made.GetComponent<ZNetView>();
			Vagon cart = made.GetComponent<Vagon>();
			if (view == null || !view.IsValid() || cart == null)
			{
				return(null);
			}
			VillageCarts.MarkViking(view.GetZDO());
			if (village != null)
			{
				view.GetZDO().Set(VillageCarts.VillageKey, village.Farm);
			}
			WearNTear wear = made.GetComponent<WearNTear>();
			if (wear != null)
			{
				wear.OnPlaced();
			}
			return(cart);
		}

		/// <summary>
		/// Makes the cart a viking brought along one of the village it came to - marked to have what it holds put away,
		/// noted as the village's own when the village has none, or else taken down once it's emptied - and lets the
		/// viking go of it
		/// </summary>
		/// <param name="viking">The viking's ZDO</param>
		/// <param name="village">The village</param>
		/// <returns>The cart, or null when it has none loaded</returns>
		public static Vagon Arrive(ZDO viking, Village village)
		{
			Vagon cart = Of(viking);
			viking.Set(CartKey, ZDOID.None);
			if (cart == null)
			{
				return(null);
			}
			VillageCarts.Adopt(village, cart);
			Plugin.Log.LogInfo("Cart: " + TestLog.Name(viking) + " brought a cart holding " + cart.m_container.GetInventory().NrOfItems() + " stacks into the village at " + village.Farm.ToString("F0"));
			return(cart);
		}
	}
}
