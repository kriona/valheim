using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A viking's cart on the road - pulling it along, letting go of it for a fight and going back for it after,
	/// building it back from what it dropped when it's destroyed, and putting it back as the viking comes back into the
	/// world (see RoadCarts) - and a disbanded village's vikings emptying its own chests into their packs and carts
	/// before they set out (see UpdateDisbandLoot)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// What a viking carries an item for while it empties a disbanded village's chests into its cart
		/// </summary>
		private const string CarryCartLoad = "cartload";

		/// <summary>
		/// How far from a disbanded village's fire its chests are emptied and its cart looked for, how far from where its
		/// cart broke a viking picks up what it dropped, how far from its cart a viking can be before it's lost, how far
		/// from the fire a cart built for a disbanded village is put up, and how near a cart a viking loads it from, in
		/// meters
		/// </summary>
		private const float DisbandLootReach = 60f;
		private const float SpillReach = 5f;
		private const float CartLostReach = 80f;
		private const float DisbandCartReach = 7f;
		private const float LoadReach = 2.5f;

		/// <summary>
		/// The items that count as wood and stone, which a disbanded village's vikings take after food and before
		/// everything else (see LootRank)
		/// </summary>
		private static readonly HashSet<string> WoodAndStone = new HashSet<string> { "Wood", "FineWood", "RoundLog", "ElderBark", "YggdrasilWood", "Blackwood", "Stone", "Grausten" };

		/// <summary>
		/// The cart the viking is pulling along the road, and where its cart was last seen, for picking up what it dropped
		/// when it broke
		/// </summary>
		private CartPull roadCartPull;
		private Vector3 roadCartSeen;
		private bool roadCartSeenSet;

		/// <summary>
		/// Where the viking is at in emptying a disbanded village's chests - whether it's done, the chests it empties (see
		/// VillageChestUse.DisbandChests), read the first time it looks, the chest it's at, when its reach into it lands,
		/// the cart it loads, and the hammer swing that builds one
		/// </summary>
		private bool disbandLootDone;
		private HashSet<ZDOID> disbandChests;
		private Container lootChest;
		private float lootActAt = -1f;
		private Vagon lootCart;
		private HammerWork lootSwing;
		private bool lootCartSpot;
		private Vector3 lootCartAt;

		/// <summary>
		/// Walks the viking toward a spot on its road, pulling its cart when it has one (see CartPull)
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="target">The spot</param>
		/// <param name="reach">How near it has to get</param>
		/// <returns>True once it's there, or when it gave up, which sets moveFailed</returns>
		private bool RoadGo(float dt, Vector3 target, float reach)
		{
			ZDO zdo = nview.GetZDO();
			Vagon cart = RoadCarts.Of(zdo);
			if (cart == null)
			{
				roadCartPull = null;
				return(Go(dt, target, reach, true));
			}
			roadCartSeen = cart.transform.position;
			roadCartSeenSet = true;
			if (roadCartPull == null || !roadCartPull.Pulls(cart))
			{
				roadCartPull = new CartPull(this, cart);
			}
			bool there = roadCartPull.Tick(dt, target, reach);
			if (roadCartPull.Failed)
			{
				roadCartPull = null;
				moveFailed = true;
				return(true);
			}
			return(there);
		}

		/// <summary>
		/// The hammer swing that takes the viking's cart down before it steps through a portal or sets sail
		/// </summary>
		private HammerWork packSwing;

		/// <summary>
		/// Takes the viking's cart down before it steps through a portal or sets sail - letting go of it, walking up to it
		/// and swinging a hammer at it, which keeps what it holds on the viking to build it back with on the other side
		/// (see RoadCarts.PackUp)
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True while it's taking its cart down</returns>
		private bool PackRoadCart(float dt, ZDO zdo)
		{
			Vagon cart = RoadCarts.Of(zdo);
			if (cart == null)
			{
				packSwing = null;
				return(false);
			}
			ReleaseRoadCart();
			mover = "taking its cart down";
			if (packSwing == null)
			{
				packSwing = new HammerWork(this, RebuildSwings);
			}
			if (!packSwing.Tick(dt, cart.transform.position))
			{
				if (!moveFailed)
				{
					return(true);
				}
				moveFailed = false;
			}
			packSwing = null;
			HoldHammer(false);
			RoadCarts.PackUp(zdo, cart);
			TestLog.Count("Carts", "taken down to cross");
			Plugin.Log.LogInfo(LogName() + " took its cart down at " + transform.position.ToString("F0") + " to cross, keeping what it held");
			return(false);
		}

		/// <summary>
		/// Checks whether the viking or its cart carries something a portal won't take that its road was planned without,
		/// in a world whose portals don't take everything - noting it, so its road is planned again without such portals
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True when its road has to be planned again</returns>
		private bool PortalRefuses(ZDO zdo)
		{
			if (zdo.GetBool(VillageTravel.UnteleportableKey) || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.TeleportAll)))
			{
				return(false);
			}
			if (!TravelPlanner.Unteleportable(humanoid.GetInventory()) && !(RoadCarts.Has(zdo) && RoadCarts.CarriesUnteleportable(humanoid)))
			{
				return(false);
			}
			zdo.Set(VillageTravel.UnteleportableKey, true);
			Plugin.Log.LogInfo(LogName() + " carries what a portal won't take, and plans its road again without the portal at " + transform.position.ToString("F0"));
			return(true);
		}

		/// <summary>
		/// Lets go of the viking's cart where it stands, as a fight starts - it goes back for it once it's over
		/// </summary>
		private void ReleaseRoadCart()
		{
			if (roadCartPull != null)
			{
				roadCartPull.Release();
				roadCartPull = null;
			}
		}

		/// <summary>
		/// Looks after the viking's cart on the road before it goes on - putting its kept cart back behind it as it comes
		/// back into the world, leaving behind a cart it's lost track of, and when its cart was destroyed, picking up what
		/// spilled from it, most useful first, and building it back from nothing, putting what it picked up back in
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="zdo">Its ZDO</param>
		/// <returns>True while it's busy with its cart</returns>
		private bool UpdateRoadCart(float dt, ZDO zdo)
		{
			if (zdo.GetBool(RoadCarts.KeptKey))
			{
				RoadCarts.PutBack(humanoid, null);
				return(false);
			}
			ZDOID id = zdo.GetZDOID(RoadCarts.CartKey);
			if (id.IsNone())
			{
				return(false);
			}
			ZDO cart = ZDOMan.instance.GetZDO(id);
			if (cart != null)
			{
				if (RoadCarts.Of(zdo) == null && Utils.DistanceXZ(cart.GetPosition(), transform.position) > CartLostReach)
				{
					Plugin.Log.LogInfo(LogName() + " lost track of its cart, " + Utils.DistanceXZ(cart.GetPosition(), transform.position).ToString("F0") + "m behind it, and goes on without it");
					RoadCarts.Forget(zdo);
				}
				return(false);
			}
			// Its cart was destroyed - what it held lies where it broke
			if (!roadCartSeenSet)
			{
				RoadCarts.Forget(zdo);
				return(false);
			}
			ZNetView spill = NearestSpill(roadCartSeen);
			if (spill != null)
			{
				mover = "picking up what its broken cart dropped";
				if (Utils.DistanceXZ(spill.transform.position, transform.position) > ChestReach)
				{
					Go(dt, spill.transform.position, ChestReach * 0.8f, false);
					if (moveFailed)
					{
						moveFailed = false;
						Avoid(spill);
					}
					return(true);
				}
				if (!PickUpDrop(spill))
				{
					Avoid(spill);
				}
				return(true);
			}
			RebuildRoadCart(zdo);
			roadCartSeenSet = false;
			return(false);
		}

		/// <summary>
		/// Finds the most useful item lying near where a viking's cart broke that it hasn't given up on (see LootRank)
		/// </summary>
		/// <param name="at">Where the cart broke</param>
		/// <returns>The item, or null when none is left</returns>
		private ZNetView NearestSpill(Vector3 at)
		{
			ZNetView best = null;
			int bestRank = int.MaxValue;
			foreach (ItemDrop drop in LoadedDrops())
			{
				ZNetView view = ((drop != null) ? drop.m_nview : null);
				if (view == null || !view.IsValid() || Avoided(view.GetZDO().m_uid) || Utils.DistanceXZ(drop.transform.position, at) > SpillReach || ItemOf(drop) == null || WeightRoom(ItemOf(drop)) <= 0)
				{
					continue;
				}
				int rank = LootRank(drop.m_itemData);
				if (rank < bestRank)
				{
					best = view;
					bestRank = rank;
				}
			}
			return(best);
		}

		/// <summary>
		/// Builds a viking's broken cart back from nothing with one swing once it has picked up what spilled from it,
		/// putting what it picked up back in
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		private void RebuildRoadCart(ZDO zdo)
		{
			RoadCarts.Forget(zdo);
			VikingPoses.Swing(humanoid);
			Vagon cart = RoadCarts.Spawn(humanoid, null);
			if (cart == null)
			{
				return;
			}
			// What it picked up goes back in, what it carries for itself stays with it
			LoadIntoCart(cart, (ItemDrop.ItemData item) => IsCarriedFor(item, CarryLoot));
			RoadCarts.Take(zdo, cart);
			TestLog.Count("Carts", "built back on the road");
			Plugin.Log.LogInfo(LogName() + " built its broken cart back at " + transform.position.ToString("F0"));
		}

		/// <summary>
		/// Moves what the viking carries of some kind into a cart, as much as the cart has room for, up to its full load
		/// (see CartRules.FullWeight)
		/// </summary>
		/// <param name="cart">The cart</param>
		/// <param name="moves">Which items go in</param>
		/// <returns>How many went in</returns>
		private int LoadIntoCart(Vagon cart, System.Predicate<ItemDrop.ItemData> moves)
		{
			if (!cart.m_nview.IsOwner())
			{
				cart.m_nview.ClaimOwnership();
			}
			Inventory store = cart.m_container.GetInventory();
			Inventory inventory = humanoid.GetInventory();
			int moved = 0;
			foreach (ItemDrop.ItemData item in inventory.GetAllItems().FindAll(moves))
			{
				int room = Mathf.FloorToInt(Mathf.Max(0f, CartRules.FullWeight - store.GetTotalWeight()) / Mathf.Max(0.01f, item.m_shared.m_weight));
				int count = Mathf.Min(item.m_stack, room);
				if (count <= 0)
				{
					continue;
				}
				ItemDrop.ItemData copy = item.Clone();
				copy.m_stack = count;
				copy.m_customData.Remove(VikingItems.CarryKey);
				if (!store.CanAddItem(copy, count) || !store.AddItem(copy))
				{
					continue;
				}
				inventory.RemoveItem(item, count);
				moved += count;
			}
			return(moved);
		}

		/// <summary>
		/// Ranks an item by how much a disbanded village's vikings want it - food first, then wood and stone, then the
		/// rest, then trophies last (see CartRules.LootRank)
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>The rank, lowest first</returns>
		private static int LootRank(ItemDrop.ItemData item)
		{
			string name = ((item.m_dropPrefab != null) ? item.m_dropPrefab.name : "");
			return(CartRules.LootRank(VikingFood.IsFood(item), WoodAndStone.Contains(name), item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy));
		}

		/// <summary>
		/// Empties a disbanded village's own chests near its fire - the ones its vikings used, a player's included unless
		/// they were kept out of it (see DisbandChest) - as the vikings gather, before they set out: taking what they can
		/// carry, most useful first (see LootRank), and when the chests hold more than that, getting a cart - the
		/// village's own when it has one at home, or else building one beside the fire from nothing, one cart each at
		/// most - and carrying the rest to it an armful at a time, up to its full load. A Meadows or Ashlands village's
		/// vikings take no cart (see CartRules.Allowed)
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="zdo">Its ZDO</param>
		/// <param name="fire">The bonfire's ZDO</param>
		/// <returns>True while it's busy emptying the chests</returns>
		private bool UpdateDisbandLoot(float dt, ZDO zdo, ZDO fire)
		{
			if (disbandLootDone)
			{
				return(false);
			}
			Vector3 middle = fire.GetPosition();
			mover = "emptying the chests of a disbanded village";
			if (lootCart != null && humanoid.GetInventory().GetAllItems().Exists((ItemDrop.ItemData item) => IsCarriedFor(item, CarryCartLoad)))
			{
				return(LoadLootCart(dt));
			}
			if (lootSwing != null)
			{
				return(BuildLootCart(dt, zdo, middle));
			}
			if (disbandChests == null)
			{
				disbandChests = VillageChestUse.DisbandChests(fire);
			}
			if (lootChest == null || !DisbandChest(lootChest, middle) || lootChest.GetInventory().NrOfItems() == 0)
			{
				lootChest = NextLootChest(middle);
				lootActAt = -1f;
			}
			if (lootChest == null)
			{
				FinishDisbandLoot(zdo);
				return(false);
			}
			if (!ReachLootChest(dt, lootChest))
			{
				return(true);
			}
			int took = TakeLoot(lootChest, lootCart != null);
			if (took > 0)
			{
				return(true);
			}
			// Nothing more fits in its pack - with no cart yet, it gets one when it may
			if (lootCart == null && CartRules.Allowed(VikingGear.IndexOf(WorldGenerator.instance.GetBiome(middle))))
			{
				lootCart = VillageCartAt(middle);
				if (lootCart != null)
				{
					Plugin.Log.LogInfo(LogName() + " takes the cart of the disbanded village at " + middle.ToString("F0") + " for what's left in its chests");
					return(true);
				}
				lootSwing = new HammerWork(this, RebuildSwings);
				return(true);
			}
			FinishDisbandLoot(zdo);
			return(false);
		}

		/// <summary>
		/// Notes the viking is done emptying the chests, taking its cart along on the road when it has one
		/// </summary>
		/// <param name="zdo">Its ZDO</param>
		private void FinishDisbandLoot(ZDO zdo)
		{
			disbandLootDone = true;
			lootChest = null;
			if (lootCart != null)
			{
				RoadCarts.Take(zdo, lootCart);
			}
		}

		/// <summary>
		/// Checks whether a chest is one of a disbanded village's own the viking empties - one its vikings used when it was
		/// disbanded (see VillageChestUse.NoteDisbandChests) that no player has kept them out of since, public, near its
		/// fire, not a cart's, a ship's, a tombstone or a treasure chest, and not open
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="middle">Where the village's fire stands</param>
		/// <returns>True when it is</returns>
		private bool DisbandChest(Container chest, Vector3 middle)
		{
			if (chest == null || chest.m_nview == null || !chest.m_nview.IsValid() || chest.m_privacy != Container.PrivacySetting.Public || disbandChests == null || !disbandChests.Contains(chest.m_nview.GetZDO().m_uid) || VillageChestUse.KeptOut(chest))
			{
				return(false);
			}
			if (chest.m_wagon != null || chest.m_rootObjectOverride != null || chest.m_autoDestroyEmpty || IsTreasureChest(chest) || chest.GetComponent<TombStone>() != null || chest.GetComponentInParent<Ship>() != null)
			{
				return(false);
			}
			ZDO zdo = chest.m_nview.GetZDO();
			return(Utils.DistanceXZ(chest.transform.position, middle) <= DisbandLootReach && !Avoided(zdo.m_uid) && !HeldByPlayer(chest) && zdo.GetInt(ZDOVars.s_inUse) == 0 && !chest.IsInUse());
		}

		/// <summary>
		/// Finds the nearest of a disbanded village's chests the viking empties that holds something
		/// </summary>
		/// <param name="middle">Where the village's fire stands</param>
		/// <returns>The chest, or null when none is left</returns>
		private Container NextLootChest(Vector3 middle)
		{
			return(Nearest(LoadedChests().FindAll((Container chest) => DisbandChest(chest, middle) && chest.GetInventory().NrOfItems() > 0)));
		}

		/// <summary>
		/// Walks to a chest and reaches into it, the way a player opens one - leaving a chest it can't get to alone for a
		/// while
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="chest">The chest</param>
		/// <returns>True once it has reached into it</returns>
		private bool ReachLootChest(float dt, Container chest)
		{
			if (Utils.DistanceXZ(chest.transform.position, transform.position) > ChestReach)
			{
				Go(dt, chest.transform.position, ChestReach, false);
				if (moveFailed)
				{
					moveFailed = false;
					Avoid(chest.m_nview);
					lootChest = null;
				}
				lootActAt = -1f;
				return(false);
			}
			if (lootActAt < 0f)
			{
				Face(chest.transform.position);
				VikingPoses.Interact(humanoid);
				lootActAt = Time.time + ReachTime;
				return(false);
			}
			if (Time.time < lootActAt)
			{
				return(false);
			}
			lootActAt = -1f;
			if (!chest.m_nview.IsOwner())
			{
				chest.m_nview.ClaimOwnership();
			}
			chest.Load();
			return(true);
		}

		/// <summary>
		/// Takes what the viking can out of a chest it reached, most useful first (see LootRank) - into its pack as loot
		/// as far as it can carry, or with a cart, an armful of one stack for the cart
		/// </summary>
		/// <param name="chest">The chest, owned by this client</param>
		/// <param name="forCart">True to take an armful for its cart</param>
		/// <returns>How many it took</returns>
		private int TakeLoot(Container chest, bool forCart)
		{
			Inventory store = chest.GetInventory();
			List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>(store.GetAllItems());
			items.Sort((ItemDrop.ItemData a, ItemDrop.ItemData b) => LootRank(a).CompareTo(LootRank(b)));
			int took = 0;
			foreach (ItemDrop.ItemData item in items)
			{
				if (item.m_dropPrefab == null)
				{
					continue;
				}
				int carried = (forCart ? CarryArmful(item, item.m_stack, CarryCartLoad) : Carry(item, item.m_stack, CarryLoot));
				if (carried > 0)
				{
					store.RemoveItem(item, carried);
					took += carried;
					if (forCart)
					{
						break;
					}
				}
			}
			if (took > 0)
			{
				TestLog.Count("Disbanding", "items taken from the chests", took);
			}
			return(took);
		}

		/// <summary>
		/// Takes up some of an item as an armful in an empty slot, carried for a purpose, whatever it weighs
		/// </summary>
		/// <param name="item">The item</param>
		/// <param name="amount">How many, no more than its stack</param>
		/// <param name="purpose">What it's carried for, like CarryCartLoad</param>
		/// <returns>How many it took, or 0 with no empty slot</returns>
		private int CarryArmful(ItemDrop.ItemData item, int amount, string purpose)
		{
			Inventory inventory = humanoid.GetInventory();
			Vector2i slot = inventory.FindEmptySlot(false);
			if (slot.x < 0 || amount <= 0)
			{
				return(0);
			}
			ItemDrop.ItemData copy = item.Clone();
			copy.m_stack = Mathf.Min(amount, item.m_stack);
			copy.m_gridPos = slot;
			copy.m_equipped = false;
			copy.m_customData[VikingItems.CarryKey] = purpose;
			inventory.m_inventory.Add(copy);
			inventory.Changed();
			return(copy.m_stack);
		}

		/// <summary>
		/// Carries the armful the viking took for its cart to the cart and puts it in - or, with the cart full, puts what
		/// doesn't fit back in the chest it came from, or keeps it as loot when it's gone, and is done
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True, as it takes the update over</returns>
		private bool LoadLootCart(float dt)
		{
			if (lootCart == null || lootCart.m_nview == null || !lootCart.m_nview.IsValid())
			{
				lootCart = null;
				return(true);
			}
			Vector3 box = lootCart.m_container.transform.position;
			if (Utils.DistanceXZ(box, transform.position) > LoadReach)
			{
				Go(dt, box, LoadReach * 0.8f, false);
				moveFailed = false;
				return(true);
			}
			Face(box);
			VikingPoses.Interact(humanoid);
			int moved = LoadIntoCart(lootCart, (ItemDrop.ItemData item) => IsCarriedFor(item, CarryCartLoad));
			bool full = false;
			Inventory inventory = humanoid.GetInventory();
			foreach (ItemDrop.ItemData left in inventory.GetAllItems().FindAll((ItemDrop.ItemData item) => IsCarriedFor(item, CarryCartLoad)))
			{
				full = true;
				left.m_customData.Remove(VikingItems.CarryKey);
				if (lootChest != null && lootChest.m_nview != null && lootChest.m_nview.IsValid() && lootChest.GetInventory().CanAddItem(left, left.m_stack))
				{
					inventory.RemoveItem(left);
					lootChest.GetInventory().AddItem(left);
				}
				else
				{
					left.m_customData[VikingItems.CarryKey] = CarryLoot;
				}
			}
			inventory.Changed();
			if (full)
			{
				FinishDisbandLoot(nview.GetZDO());
			}
			TestLog.Count("Disbanding", "items loaded into carts", moved);
			return(true);
		}

		/// <summary>
		/// Finds a disbanded village's own cart standing near its fire for the viking to take - empty, with no one pulling
		/// it and no other viking taking it
		/// </summary>
		/// <param name="middle">Where the village's fire stands</param>
		/// <returns>The cart, or null when there's none to take</returns>
		private Vagon VillageCartAt(Vector3 middle)
		{
			foreach (Vagon cart in Vagon.m_instances)
			{
				ZDO zdo = ((cart != null && cart.m_nview != null && cart.m_nview.IsValid()) ? cart.m_nview.GetZDO() : null);
				if (zdo == null || zdo.GetVec3(VillageCarts.VillageKey, Vector3.zero) == Vector3.zero || Utils.DistanceXZ(cart.transform.position, middle) > DisbandLootReach)
				{
					continue;
				}
				if (cart.IsAttached() || VillageCarts.HoldsAnything(cart) || !ClaimWork(zdo.m_uid))
				{
					continue;
				}
				return(cart);
			}
			return(null);
		}

		/// <summary>
		/// Builds a cart beside a disbanded village's fire from nothing, with one swing, and goes on emptying the chests
		/// into it
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="zdo">Its ZDO</param>
		/// <param name="middle">Where the village's fire stands</param>
		/// <returns>True, as it takes the update over</returns>
		private bool BuildLootCart(float dt, ZDO zdo, Vector3 middle)
		{
			if (!lootCartSpot)
			{
				Vector3 outward = transform.position - middle;
				outward.y = 0f;
				outward = ((outward.sqrMagnitude > 0.01f) ? outward.normalized : Vector3.forward);
				lootCartAt = middle + outward * DisbandCartReach;
				lootCartAt.y = ZoneSystem.instance.GetGroundHeight(lootCartAt);
				lootCartSpot = true;
			}
			if (!lootSwing.Tick(dt, lootCartAt))
			{
				if (moveFailed)
				{
					moveFailed = false;
					lootSwing = null;
					FinishDisbandLoot(zdo);
				}
				return(true);
			}
			lootSwing = null;
			lootCartSpot = false;
			lootCart = RoadCarts.Spawn(humanoid, null);
			HoldHammer(false);
			if (lootCart == null)
			{
				FinishDisbandLoot(zdo);
				return(true);
			}
			TestLog.Count("Disbanding", "carts built");
			Plugin.Log.LogInfo(LogName() + " built a cart beside the fire of the disbanded village at " + middle.ToString("F0") + " for what's left in its chests");
			return(true);
		}
	}
}
