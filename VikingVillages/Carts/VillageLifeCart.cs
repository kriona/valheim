using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The village's cart work - building the cart when the village has none, fetching it back when it stands outside
	/// the village, and taking down an old cart of the village's that turned up again after it built another (see
	/// VillageCarts) - and pulling a cart, which every job that takes one along does the same way (see CartPull)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Seconds between a viking's looks for cart work
		/// </summary>
		private const float CartCheckInterval = 10f;

		/// <summary>
		/// How near a viking gets to a cart's handle to hook it up, how near the spot it's parked at it pulls it, how far
		/// a cart can stand from that spot and still be straightened into it, and how long a cart can be snagged or come
		/// loose before it's set down behind the viking again, in meters and seconds
		/// </summary>
		private const float HitchReach = 1.2f;
		private const float ParkReach = 1f;
		private const float ParkSnap = 5f;
		private const float SnagTime = 3f;

		/// <summary>
		/// How far the handle can be from a viking pulling it before the cart counts as snagged, in meters - the game
		/// lets it come loose at 2
		/// </summary>
		private const float SnagGap = 1.6f;

		/// <summary>
		/// How far apart the points along a viking's way that a cart is set down on are, and how many are kept, in
		/// meters
		/// </summary>
		private const float TrailStep = 0.5f;
		private const int TrailMax = 12;

		/// <summary>
		/// When the viking next looks for cart work, in Time.time seconds
		/// </summary>
		private float nextCartCheck;

		/// <summary>
		/// On a PC viking, the village's cart it came home from a quest pulling, which it pulls into the village before
		/// anything else, even at night
		/// </summary>
		private static readonly KeyValuePair<int, int> QuestCartKey = ZDO.GetHashZDOID("kriona.VikingVillages.vikingQuestCart");

		/// <summary>
		/// The village's cart the viking is pulling out of the village on a quest, which goes along with it as it leaves
		/// (see LeaveOnQuest)
		/// </summary>
		private Vagon questCart;

		/// <summary>
		/// How much longer a quest's first viking has to get out of the village when it pulls the village's cart along, in
		/// seconds
		/// </summary>
		private const float CartLeaveExtra = 60f;

		/// <summary>
		/// Finds the job pulling the village's cart into the village, for a viking that came home from a quest or the road
		/// pulling it - until the cart stands in the village, or is gone
		/// </summary>
		/// <returns>The job, or null when it brought no cart home or the cart is home</returns>
		private Job FindQuestCartWork()
		{
			ZDO zdo = nview.GetZDO();
			// One that came off the road straight into the village has the cart it took along put back behind it
			if (zdo.GetBool(RoadCarts.KeptKey) && village != null)
			{
				Vagon kept = RoadCarts.PutBack(humanoid, village);
				RoadCarts.Forget(zdo);
				if (kept != null)
				{
					VillageCarts.Adopt(village, kept);
					zdo.Set(QuestCartKey, kept.m_nview.GetZDO().m_uid);
					NoteChestCheck(village);
				}
			}
			ZDOID id = zdo.GetZDOID(QuestCartKey);
			if (id.IsNone())
			{
				return(null);
			}
			ZDO cartZdo = ZDOMan.instance.GetZDO(id);
			ZNetView view = ((cartZdo != null && ZNetScene.instance != null) ? ZNetScene.instance.FindInstance(cartZdo) : null);
			Vagon cart = ((view != null) ? view.GetComponent<Vagon>() : null);
			bool home = (cart != null && village != null && (village.Contains(cart.transform.position) || village.InHouse(cart.transform.position, 0.5f)) && !CartHitch.IsHitched(cart, humanoid));
			if (cart == null || village == null || home)
			{
				zdo.Set(QuestCartKey, ZDOID.None);
				return(null);
			}
			return(ClaimWork(id) ? new CartFetchJob(this, cart, true) : null);
		}

		/// <summary>
		/// Finds cart work for the viking, every CartCheckInterval seconds - putting away what a cart brought home from a
		/// quest, taking down an old cart of the village's, fetching the village's cart back from outside it, or building
		/// one when the village has none - each claimed so only one viking does it
		/// </summary>
		/// <returns>The job, or null when there's none</returns>
		private Job FindCartWork()
		{
			if (Time.time < nextCartCheck)
			{
				return(null);
			}
			nextCartCheck = Time.time + CartCheckInterval;
			VillageCarts.Status status = VillageCarts.Of(village);
			foreach (Vagon full in status.Unload)
			{
				ZNetView view = ((full != null) ? full.m_nview : null);
				if (view != null && view.IsValid() && !Avoided(view.GetZDO().m_uid) && ClaimWork(view.GetZDO().m_uid))
				{
					return(new CartUnloadJob(this, full));
				}
			}
			foreach (Vagon extra in status.Extras)
			{
				ZNetView view = ((extra != null) ? extra.m_nview : null);
				if (view != null && view.IsValid() && !Warded(extra.transform.position) && !HammerAvoided(view.GetZDO().m_uid) && ClaimWork(view.GetZDO().m_uid))
				{
					return(new CartTakeDownJob(this, extra));
				}
			}
			if (status.State == CartRules.CartState.Astray && status.Cart != null && !Warded(status.Cart.transform.position) && !HammerAvoided(status.Id) && ClaimWork(status.Id))
			{
				return(new CartFetchJob(this, status.Cart));
			}
			if (status.State == CartRules.CartState.Missing && VillageCarts.MayBuild(village) && ClaimWork(CartBuildKey(village)))
			{
				return(new CartBuildJob(this));
			}
			return(null);
		}

		/// <summary>
		/// The claim on building a village's cart, so one viking builds it
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The claim's key</returns>
		private static string CartBuildKey(Village village)
		{
			return("cart " + village.Zdo.m_uid);
		}

		/// <summary>
		/// Pulling a cart - walking to its handle and hooking it up the way a player does, then walking somewhere with it
		/// in tow. When it snags on something - its handle left more than SnagGap behind - or comes loose for SnagTime
		/// seconds, it's set down on the viking's way behind it and hooked up again, so it never holds the viking up
		/// </summary>
		private class CartPull
		{
			private readonly VillageLife life;
			private readonly Vagon cart;
			private readonly List<Vector3> trail = new List<Vector3>();
			private bool wasHitched;
			private float troubleSince = -1f;

			/// <summary>
			/// How far the viking has walked with the cart hooked up and for how long, in meters and seconds, and where it
			/// was last noted, for the test log's note of how fast vikings pull carts by their load (see LogSpeed)
			/// </summary>
			private float pulledMeters;
			private float pulledSeconds;
			private Vector3 lastAt;
			private bool hasLastAt;

			/// <summary>
			/// Whether the viking couldn't get to the cart, or the cart is gone
			/// </summary>
			public bool Failed { get; private set; }

			/// <summary>
			/// Starts pulling a cart
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="pulled">The cart</param>
			public CartPull(VillageLife owner, Vagon pulled)
			{
				life = owner;
				cart = pulled;
			}

			/// <summary>
			/// Checks whether this is the pull of a cart
			/// </summary>
			/// <param name="other">The cart</param>
			/// <returns>True when it is</returns>
			public bool Pulls(Vagon other)
			{
				return(cart == other);
			}

			/// <summary>
			/// Checks whether the cart is hooked up to the viking
			/// </summary>
			/// <returns>True when it is</returns>
			public bool Hitched()
			{
				return(CartHitch.IsHitched(cart, life.humanoid));
			}

			/// <summary>
			/// Hooks the cart up, then walks with it toward a spot
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="target">The spot</param>
			/// <param name="reach">How near it has to get</param>
			/// <returns>True once it's there with the cart, or it failed (see Failed)</returns>
			public bool Tick(float dt, Vector3 target, float reach)
			{
				if (cart == null || cart.m_nview == null || !cart.m_nview.IsValid())
				{
					Failed = true;
					return(true);
				}
				if (!Hitched())
				{
					return(Hitch(dt));
				}
				wasHitched = true;
				NoteTrail();
				Vector3 at = life.transform.position;
				if (hasLastAt)
				{
					pulledMeters += Utils.DistanceXZ(at, lastAt);
					pulledSeconds += dt;
				}
				lastAt = at;
				hasLastAt = true;
				bool there = life.Go(dt, target, reach, false);
				if (life.moveFailed)
				{
					Failed = true;
					return(true);
				}
				float gap = Vector3.Distance(life.transform.position + cart.m_attachOffset, cart.m_attachPoint.position);
				if (gap > SnagGap || cart.transform.up.y < 0.5f)
				{
					if (troubleSince < 0f)
					{
						troubleSince = Time.time;
					}
					else if (Time.time - troubleSince >= SnagTime)
					{
						SetDownBehind();
					}
				}
				else
				{
					troubleSince = -1f;
				}
				return(there);
			}

			/// <summary>
			/// Walks to the cart's handle and hooks it up - or, for a cart that came loose on the way and hasn't been hooked
			/// up again within SnagTime seconds, sets it down behind the viking and hooks it up there
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True when the viking can't get to the cart</returns>
			private bool Hitch(float dt)
			{
				if (wasHitched)
				{
					if (troubleSince < 0f)
					{
						troubleSince = Time.time;
					}
					else if (Time.time - troubleSince >= SnagTime)
					{
						SetDownBehind();
						return(false);
					}
				}
				Vector3 handle = cart.m_attachPoint.position;
				if (Utils.DistanceXZ(handle, life.transform.position) > HitchReach)
				{
					life.Go(dt, handle, HitchReach * 0.8f, false);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						Failed = true;
						return(true);
					}
					return(false);
				}
				life.ai.StopMoving();
				VikingPoses.Interact(life.humanoid);
				Vector3 behind = cart.transform.position - life.transform.position;
				if (!CartHitch.Hitch(cart, life.humanoid, behind))
				{
					Failed = true;
					return(true);
				}
				troubleSince = -1f;
				trail.Clear();
				return(false);
			}

			/// <summary>
			/// Notes the viking's way as it walks, a point every TrailStep meters, for the cart to be set down on
			/// </summary>
			private void NoteTrail()
			{
				Vector3 at = life.transform.position;
				if (trail.Count == 0 || Utils.DistanceXZ(trail[trail.Count - 1], at) >= TrailStep)
				{
					trail.Add(at);
					if (trail.Count > TrailMax)
					{
						trail.RemoveAt(0);
					}
				}
			}

			/// <summary>
			/// Sets the cart down behind the viking, along the way it came, and hooks it up again
			/// </summary>
			private void SetDownBehind()
			{
				Vector3 at = life.transform.position;
				Vector3 behind = -life.transform.forward;
				for (int i = trail.Count - 1; i >= 0; i--)
				{
					if (Utils.DistanceXZ(trail[i], at) >= 1f)
					{
						behind = trail[i] - at;
						break;
					}
				}
				CartHitch.Unhitch(cart);
				if (!cart.m_nview.IsOwner())
				{
					cart.m_nview.ClaimOwnership();
				}
				CartHitch.SetDown(cart, life.humanoid, behind);
				CartHitch.Hitch(cart, life.humanoid, behind);
				troubleSince = -1f;
				TestLog.Count("Carts", "set down again after snagging");
			}

			/// <summary>
			/// Unhooks the cart, if the viking has it hooked up, and notes how fast it pulled it
			/// </summary>
			public void Release()
			{
				LogSpeed();
				if (Hitched())
				{
					CartHitch.Unhitch(cart);
				}
			}

			/// <summary>
			/// How far a viking has to have pulled a cart for its speed to be noted, in meters
			/// </summary>
			private const float SpeedNoteMeters = 15f;

			/// <summary>
			/// Notes in the log how fast the viking pulled the cart and the weight it held, in a test run or debug mode, once
			/// it has pulled it far enough to tell - for putting CartRules.FullWeight and FullSlowdown right
			/// </summary>
			private void LogSpeed()
			{
				if ((!TestLog.On && !Player.m_debugMode) || pulledMeters < SpeedNoteMeters || pulledSeconds <= 0f || cart == null)
				{
					return;
				}
				float load = ((cart.m_container != null && cart.m_container.GetInventory() != null) ? cart.m_container.GetInventory().GetTotalWeight() : 0f);
				Plugin.Log.LogInfo("Cart pull: " + TestLog.Name(life.humanoid, "F0") + " pulled a cart holding " + load.ToString("F0") + " weight " + pulledMeters.ToString("F0") + "m in " + pulledSeconds.ToString("F0") + "s, " + (pulledMeters / pulledSeconds).ToString("F2") + " m/s");
				pulledMeters = 0f;
				pulledSeconds = 0f;
			}
		}

		/// <summary>
		/// Building the village's cart once it has none - from nothing, like the vikings' boats, with one swing of a
		/// hammer where it's parked beside a house (see VillageCarts.FindSpot)
		/// </summary>
		private class CartBuildJob : Job
		{
			private readonly HammerWork work;
			private bool hasSpot;
			private Vector3 spot;
			private Quaternion rotation;

			/// <summary>
			/// Makes the job, with the building already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			public CartBuildJob(VillageLife owner) : base(owner)
			{
				work = new HammerWork(owner, RebuildSwings);
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name</returns>
			public override string Describe()
			{
				return("Building the village's cart");
			}

			/// <summary>
			/// Walks to where the cart goes and builds it
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's built, the village has one again, there's no room for it, it can't get there or it's
			/// dusk</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk() || VillageCarts.Of(life.village).State != CartRules.CartState.Missing)
				{
					return(true);
				}
				if (!hasSpot && !PickSpot())
				{
					return(true);
				}
				if (!work.Tick(dt, spot))
				{
					return(life.moveFailed);
				}
				VillageCarts.Spawn(life.village, spot, rotation);
				TestLog.Count("Work done", "carts built");
				return(true);
			}

			/// <summary>
			/// Picks where the cart goes beside a house (see VillageCarts.FindSpot), noting when there's no room, so the
			/// village doesn't try again for a while
			/// </summary>
			/// <returns>False when there's no room</returns>
			private bool PickSpot()
			{
				if (!VillageCarts.FindSpot(life.village, life.transform.position, null, out spot, out rotation))
				{
					Plugin.Log.LogWarning("Cart: " + TestLog.Name(life.humanoid, "F0") + " found no room to build the village's cart beside any house");
					VillageCarts.NoteNoRoom(life.village);
					return(false);
				}
				hasSpot = true;
				return(true);
			}

			/// <summary>
			/// Lets go of the building
			/// </summary>
			public override void End()
			{
				life.ReleaseWork(CartBuildKey(life.village));
			}
		}

		/// <summary>
		/// Fetching the village's cart back from outside it, or bringing it home from a quest - walking to it, hooking it
		/// up, pulling it home and parking it beside a house (see VillageCarts.FindSpot), nearest where it stood
		/// </summary>
		private class CartFetchJob : Job
		{
			private readonly Vagon cart;
			private readonly ZDOID id;
			private readonly CartPull pull;
			private readonly bool anyHour;
			private bool hasSpot;
			private Vector3 spot;
			private Quaternion rotation;
			private Vector3 stand;

			/// <summary>
			/// Makes the job, with the cart already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="astray">The cart</param>
			/// <param name="homecoming">True when it's bringing the cart home from a quest, which it does at any hour,
			/// rather than fetching it by day</param>
			public CartFetchJob(VillageLife owner, Vagon astray, bool homecoming = false) : base(owner)
			{
				cart = astray;
				id = astray.m_nview.GetZDO().m_uid;
				pull = new CartPull(owner, astray);
				anyHour = homecoming;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name</returns>
			public override string Describe()
			{
				return("Fetching the village's cart");
			}

			/// <summary>
			/// Checks whether the cart lies outside the village
			/// </summary>
			/// <returns>True while the viking heads out for it</returns>
			public override bool Outing()
			{
				return(!pull.Hitched());
			}

			/// <summary>
			/// Pulls the cart to where it's parked, then lets go of it there
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's parked, gone or out of reach, or for one fetched by day, it's
			/// dusk</returns>
			public override bool Tick(float dt)
			{
				if ((!anyHour && (IsNight() || IsDusk())) || cart == null || cart.m_nview == null || !cart.m_nview.IsValid())
				{
					return(true);
				}
				if (!hasSpot)
				{
					if (!VillageCarts.FindSpot(life.village, cart.transform.position, cart, out spot, out rotation))
					{
						Plugin.Log.LogWarning("Cart: " + TestLog.Name(life.humanoid, "F0") + " found no room to park the village's cart beside any house");
						return(true);
					}
					// The viking stands where the handle is once the cart is parked, so the cart comes to rest about there
					Vector3 local = cart.transform.InverseTransformPoint(cart.m_attachPoint.position);
					stand = spot + rotation * new Vector3(local.x, 0f, local.z);
					hasSpot = true;
				}
				if (!pull.Tick(dt, stand, ParkReach))
				{
					return(false);
				}
				if (pull.Failed)
				{
					life.Avoid(cart.m_nview);
					return(true);
				}
				pull.Release();
				if (Vector3.Distance(cart.transform.position, spot) <= ParkSnap && cart.m_nview.IsOwner())
				{
					CartHitch.Move(cart, spot + Vector3.up * 0.1f, rotation);
				}
				TestLog.Count("Work done", "carts fetched back");
				return(true);
			}

			/// <summary>
			/// Unhooks the cart and lets go of it
			/// </summary>
			public override void End()
			{
				pull.Release();
				life.ReleaseWork(id);
			}
		}

		/// <summary>
		/// Putting away what a cart brought home from a quest - walking to it, reaching in the way a player opens it and
		/// taking out as much as the viking can carry, which it puts away in the chests like any loot it carries
		/// </summary>
		private class CartUnloadJob : Job
		{
			private readonly Vagon cart;
			private readonly ZDOID id;
			private float actAt = -1f;

			/// <summary>
			/// Makes the job, with the cart already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="full">The cart</param>
			public CartUnloadJob(VillageLife owner, Vagon full) : base(owner)
			{
				cart = full;
				id = full.m_nview.GetZDO().m_uid;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name</returns>
			public override string Describe()
			{
				return("Unloading the village's cart");
			}

			/// <summary>
			/// Walks to the cart and takes out what it can carry
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has taken what it can, or the cart is gone, pulled or out of reach</returns>
			public override bool Tick(float dt)
			{
				if (cart == null || cart.m_nview == null || !cart.m_nview.IsValid() || cart.IsAttached())
				{
					return(true);
				}
				Vector3 box = cart.m_container.transform.position;
				if (Utils.DistanceXZ(box, life.transform.position) > ChestReach + 0.5f)
				{
					life.Go(dt, box, ChestReach, false);
					if (life.moveFailed)
					{
						life.Avoid(cart.m_nview);
						return(true);
					}
					actAt = -1f;
					return(false);
				}
				if (actAt < 0f)
				{
					life.Face(box);
					VikingPoses.Interact(life.humanoid);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (Time.time < actAt)
				{
					return(false);
				}
				if (!cart.m_nview.IsOwner())
				{
					cart.m_nview.ClaimOwnership();
				}
				Inventory store = cart.m_container.GetInventory();
				int took = 0;
				foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(store.GetAllItems()))
				{
					int carried = life.Carry(item, item.m_stack, CarryLoot);
					if (carried > 0)
					{
						store.RemoveItem(item, carried);
						took += carried;
					}
					if (carried < item.m_stack)
					{
						break;
					}
				}
				if (store.NrOfItems() == 0)
				{
					cart.m_nview.GetZDO().Set(VillageCarts.UnloadKey, false);
				}
				else if (took == 0)
				{
					// With no room to carry anything, it leaves the cart for a while
					life.Avoid(cart.m_nview);
				}
				// What it takes out may fill the chests
				NoteChestCheck(life.village);
				TestLog.Count("Work done", "items taken out of carts", took);
				return(true);
			}

			/// <summary>
			/// Lets go of the cart
			/// </summary>
			public override void End()
			{
				life.ReleaseWork(id);
			}
		}

		/// <summary>
		/// Taking down an old cart of the village's that turned up again after it built another - walking to it and
		/// swinging a hammer at it, which drops nothing but what it held, for the vikings to put away
		/// </summary>
		private class CartTakeDownJob : Job
		{
			private readonly Vagon cart;
			private readonly ZDOID id;
			private readonly HammerWork work;

			/// <summary>
			/// Makes the job, with the cart already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="old">The cart</param>
			public CartTakeDownJob(VillageLife owner, Vagon old) : base(owner)
			{
				cart = old;
				id = old.m_nview.GetZDO().m_uid;
				work = new HammerWork(owner, RebuildSwings);
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name</returns>
			public override string Describe()
			{
				return("Taking down an old cart");
			}

			/// <summary>
			/// Walks to the cart, swings at it and takes it down
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's down, gone or out of reach, or it's dusk</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk() || cart == null || cart.m_nview == null || !cart.m_nview.IsValid())
				{
					return(true);
				}
				if (!work.Tick(dt, cart.transform.position))
				{
					return(life.moveFailed);
				}
				WearNTear wear = cart.GetComponent<WearNTear>();
				if (wear != null)
				{
					if (!cart.m_nview.IsOwner())
					{
						cart.m_nview.ClaimOwnership();
					}
					wear.Remove();
					TestLog.Count("Work done", "old carts taken down");
				}
				VillageCarts.Refresh(life.village);
				return(true);
			}

			/// <summary>
			/// Lets go of the cart
			/// </summary>
			public override void End()
			{
				life.ReleaseWork(id);
			}
		}
	}
}
