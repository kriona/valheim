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
		/// Finds cart work for the viking, every CartCheckInterval seconds - taking down an old cart of the village's,
		/// fetching the village's cart back from outside it, or building one when the village has none and has what it
		/// takes - each claimed so only one viking does it
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
			if (status.State == CartRules.CartState.Missing && VillageCarts.MayBuild(village))
			{
				ArrowPlan plan = PlanCart(out string missing);
				bool woodLeft = (plan != null && (Time.time >= noWoodUntil || CarriedWood() >= plan.Wood || FindWoodChest() != null));
				if (woodLeft && ClaimWork(CartBuildKey(village)))
				{
					return(new CartBuildJob(this, plan));
				}
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
		/// Works out what building a cart takes from the game's own piece - its wood, gathered like any piece's, and the
		/// rest, like bronze nails, which the viking carries or the village's chests have to hold
		/// </summary>
		/// <param name="missing">Set to what the village is short of, or null</param>
		/// <returns>What it takes, or null when the village is short of something or the game has no cart</returns>
		private ArrowPlan PlanCart(out string missing)
		{
			missing = null;
			GameObject prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(VillageCarts.CartPrefab) : null);
			Piece piece = ((prefab != null) ? prefab.GetComponent<Piece>() : null);
			if (piece == null)
			{
				return(null);
			}
			ArrowPlan plan = new ArrowPlan { Prefab = VillageCarts.CartPrefab, Batch = 1 };
			foreach (Piece.Requirement requirement in piece.m_resources)
			{
				if (requirement == null || requirement.m_resItem == null || requirement.m_amount <= 0)
				{
					continue;
				}
				string name = requirement.m_resItem.gameObject.name;
				if (WoodItems.Contains(name))
				{
					plan.Wood += requirement.m_amount;
					continue;
				}
				if (CarriedCraft(name) + ChestCount(name) < requirement.m_amount)
				{
					missing = name;
					return(null);
				}
				plan.Others.Add(new KeyValuePair<string, int>(name, requirement.m_amount));
			}
			return(plan);
		}

		/// <summary>
		/// Counts an item in the village's chests the viking may use, leaving out what's put away for a viking
		/// </summary>
		/// <param name="name">The item's prefab name</param>
		/// <returns>How many</returns>
		private int ChestCount(string name)
		{
			int count = 0;
			foreach (Container chest in UsableChests())
			{
				foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
				{
					if (item.m_dropPrefab != null && item.m_dropPrefab.name == name && !VikingItems.IsCarried(item))
					{
						count += item.m_stack;
					}
				}
			}
			return(count);
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
				if (cart == null || cart.m_nview == null || !cart.m_nview.IsValid() || CartHitch.PlayerUsing(cart))
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
			/// Unhooks the cart, if the viking has it hooked up
			/// </summary>
			public void Release()
			{
				if (Hitched())
				{
					CartHitch.Unhitch(cart);
				}
			}
		}

		/// <summary>
		/// Building the village's cart once it has none - getting what it takes besides wood, like bronze nails, from the
		/// village's chests, gathering its wood, then swinging a hammer where it's parked beside a house (see
		/// VillageCarts.FindSpot) and putting it up
		/// </summary>
		private class CartBuildJob : Job
		{
			private readonly ArrowPlan plan;
			private readonly HammerWork work;
			private WoodRun run;
			private Container chest;
			private float actAt = -1f;
			private bool hasSpot;
			private Vector3 spot;
			private Quaternion rotation;

			/// <summary>
			/// Makes the job, with the building already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="cart">What the cart takes</param>
			public CartBuildJob(VillageLife owner, ArrowPlan cart) : base(owner)
			{
				plan = cart;
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
			/// Gets what the cart takes, then builds it
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's built, the village has one again, there's no room for it, what it takes is gone, it
			/// can't get there or it's dusk</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk() || VillageCarts.Of(life.village).State != CartRules.CartState.Missing)
				{
					return(true);
				}
				// Where it goes is picked first, so nothing is gathered for a cart there's no room for
				if (!hasSpot && !PickSpot())
				{
					return(true);
				}
				if (life.MissingCraft(plan, out string ingredient, out int needed))
				{
					return(Fetch(dt, ingredient, needed));
				}
				if (!life.HaveWood(plan.Wood, ref run, dt, out bool done))
				{
					return(done);
				}
				if (!work.Tick(dt, spot))
				{
					return(life.moveFailed);
				}
				if (life.CarriedWood() < plan.Wood || !life.UseCraft(plan))
				{
					return(true);
				}
				life.UseWood(plan.Wood);
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
			/// Walks to the nearest chest holding what the cart takes and takes it - giving up when no chest holds it
			/// any more
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="ingredient">Its prefab name</param>
			/// <param name="needed">How many more it needs</param>
			/// <returns>True when it gives up</returns>
			private bool Fetch(float dt, string ingredient, int needed)
			{
				if (chest == null || !life.Usable(chest))
				{
					chest = life.ChestWithCraft(ingredient);
					actAt = -1f;
					if (chest == null)
					{
						return(true);
					}
				}
				if (Utils.DistanceXZ(chest.transform.position, life.transform.position) > ChestReach)
				{
					life.Go(dt, chest.transform.position, ChestReach, false);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						chest = null;
					}
					actAt = -1f;
					return(false);
				}
				if (actAt < 0f)
				{
					life.Face(chest.transform.position);
					VikingPoses.Interact(life.humanoid);
					life.ShowChestOpen(chest);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (Time.time < actAt)
				{
					return(false);
				}
				actAt = -1f;
				if (life.TakeCraft(chest, ingredient, needed) <= 0)
				{
					life.Avoid(chest.m_nview);
				}
				chest = null;
				return(false);
			}

			/// <summary>
			/// Checks whether the wood the viking is after lies outside the village
			/// </summary>
			/// <returns>True when it does</returns>
			public override bool Outing()
			{
				return(run != null && run.Outing());
			}

			/// <summary>
			/// Lets go of the building and the wood it was after, and carries what it took for the cart back to a chest
			/// </summary>
			public override void End()
			{
				if (run != null)
				{
					run.Release();
				}
				life.ReturnCraft();
				life.ReleaseWork(CartBuildKey(life.village));
			}
		}

		/// <summary>
		/// Fetching the village's cart back from outside it - walking to it, hooking it up, pulling it home and parking it
		/// beside a house (see VillageCarts.FindSpot), nearest where it stood
		/// </summary>
		private class CartFetchJob : Job
		{
			private readonly Vagon cart;
			private readonly ZDOID id;
			private readonly CartPull pull;
			private bool hasSpot;
			private Vector3 spot;
			private Quaternion rotation;
			private Vector3 stand;

			/// <summary>
			/// Makes the job, with the cart already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="astray">The cart</param>
			public CartFetchJob(VillageLife owner, Vagon astray) : base(owner)
			{
				cart = astray;
				id = astray.m_nview.GetZDO().m_uid;
				pull = new CartPull(owner, astray);
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
			/// <returns>True once it's parked, gone, used by a player or out of reach, or it's dusk</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk() || cart == null || cart.m_nview == null || !cart.m_nview.IsValid())
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
		/// Taking down an old cart of the village's that turned up again after it built another - walking to it and
		/// swinging a hammer at it, which drops its wood, nails and whatever it held for the vikings to put away
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
			/// <returns>True once it's down, gone, used by a player or out of reach, or it's dusk</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk() || cart == null || cart.m_nview == null || !cart.m_nview.IsValid() || CartHitch.PlayerUsing(cart))
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
