using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Crafting arrows (see ArrowJob) - the arrows a viking's bow shoots, the best it can make and fill its quiver with
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Finds arrows for the viking to craft - when it has a bow and wants a batch of the best arrows the village can
		/// make (see MissingArrows and PlanArrows), no other viking is working at the station that makes them, and there's
		/// wood it can get. While it still has arrows to shoot, it first gets what the best arrows its station allows are
		/// short of, like flint or feathers, when there's a way to (see GatherFor)
		/// </summary>
		/// <returns>The job, or null when there's nothing to craft</returns>
		private Job FindArrowWork()
		{
			ArrowPlan plan = PlanArrows();
			if (plan == null || !MissingArrows(plan.Batch, plan.Wood))
			{
				return(null);
			}
			// A viking with arrows still to shoot gets what the best arrows it could make take first, rather than
			// making worse ones
			string ammoType = BowAmmo();
			if (ammoType != null && OwnArrows(ammoType, out float _) > 0)
			{
				Job gather = GatherFor(BestArrowsMissing(ammoType));
				if (gather != null)
				{
					return(gather);
				}
			}
			bool woodLeft = (Time.time >= noWoodUntil || CarriedWood() >= plan.Wood || FindWoodChest() != null);
			if (!woodLeft || !ClaimObject(plan.Station.GetComponent<ZNetView>()))
			{
				return(null);
			}
			return(new ArrowJob(this, plan));
		}

		/// <summary>
		/// Checks whether the viking has a bow and wants a batch of arrows crafted - it's missing at least a batch from
		/// its quiver, and it has fewer than QuiverImportant or the village has SpareWood more than the batch takes
		/// </summary>
		/// <param name="batch">How many arrows a craft makes</param>
		/// <param name="wood">The wood a craft takes</param>
		/// <returns>True when it wants a batch</returns>
		private bool MissingArrows(int batch, int wood)
		{
			string ammoType = BowAmmo();
			if (ammoType == null)
			{
				return(false);
			}
			int own = OwnArrows(ammoType, out float _);
			if (own > QuiverFull - batch)
			{
				return(false);
			}
			return(own < QuiverImportant || VillageWood() >= wood + SpareWood);
		}

		/// <summary>
		/// Checks whether the viking has a bow and fewer arrows than it can do without (see QuiverCritical)
		/// </summary>
		/// <returns>True when it has</returns>
		private bool CriticalArrows()
		{
			string ammoType = BowAmmo();
			return(ammoType != null && OwnArrows(ammoType, out float _) < QuiverCritical);
		}

		/// <summary>
		/// Works out the best arrows the viking can craft for its bow - the ones that deal the most damage of those the
		/// game has a recipe for that the village has a station of the recipe's level for, and whose ingredients other
		/// than wood the viking carries for crafting or the village's chests hold, since wood can always be gathered.
		/// Wood arrows need only wood, so there are always some. Silver arrows are left out, being crafted apart (see
		/// PlanSilverArrows)
		/// </summary>
		/// <returns>The arrows, or null when it has no bow, or the village has no station for any</returns>
		private ArrowPlan PlanArrows()
		{
			string ammoType = BowAmmo();
			if (ammoType == null || ObjectDB.instance == null)
			{
				return(null);
			}
			List<Container> chests = UsableChests();
			Dictionary<int, CraftingStation> stations = new Dictionary<int, CraftingStation>();
			ArrowPlan best = null;
			foreach (Recipe recipe in ObjectDB.instance.m_recipes)
			{
				if (!Crafts(recipe) || recipe.m_craftingStation == null)
				{
					continue;
				}
				ItemDrop.ItemData arrow = recipe.m_item.m_itemData;
				if (arrow.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo || arrow.m_shared.m_ammoType != ammoType || VikingSilver.IsSilverArrows(arrow))
				{
					continue;
				}
				int stationHash = recipe.m_craftingStation.gameObject.name.GetStableHashCode();
				if (!stations.TryGetValue(stationHash, out CraftingStation station))
				{
					station = FindStation(stationHash);
					stations[stationHash] = station;
				}
				if (station == null || station.GetLevel() < recipe.m_minStationLevel)
				{
					continue;
				}
				ArrowPlan plan = new ArrowPlan { Prefab = recipe.m_item.gameObject.name, Batch = Mathf.Max(1, recipe.m_amount), Station = station, Damage = AmmoDamage(arrow) };
				bool makeable = true;
				foreach (Piece.Requirement requirement in recipe.m_resources)
				{
					int amount = CraftAmount(requirement, 1);
					if (amount <= 0)
					{
						continue;
					}
					string name = requirement.m_resItem.gameObject.name;
					if (WoodItems.Contains(name))
					{
						plan.Wood += amount;
					}
					else if (CraftStock(name, chests) >= amount)
					{
						plan.Others.Add(new KeyValuePair<string, int>(name, amount));
					}
					else
					{
						makeable = false;
						break;
					}
				}
				if (makeable && (best == null || plan.Damage > best.Damage))
				{
					best = plan;
				}
			}
			return(best);
		}

		/// <summary>
		/// Finds what the best arrows the viking could make for its bow are short of - the ones that deal the most damage
		/// of those the village has a station of the recipe's level for, whatever they take - from what it carries for
		/// crafting and the village's chests
		/// </summary>
		/// <param name="ammoType">The ammo its bow takes</param>
		/// <returns>The first ingredient other than wood they're short of, or null when they're short of nothing</returns>
		private string BestArrowsMissing(string ammoType)
		{
			if (ObjectDB.instance == null)
			{
				return(null);
			}
			Recipe best = null;
			float bestDamage = float.MinValue;
			foreach (Recipe recipe in ObjectDB.instance.m_recipes)
			{
				if (!Crafts(recipe) || recipe.m_craftingStation == null)
				{
					continue;
				}
				ItemDrop.ItemData arrow = recipe.m_item.m_itemData;
				if (arrow.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo || arrow.m_shared.m_ammoType != ammoType || VikingSilver.IsSilverArrows(arrow))
				{
					continue;
				}
				CraftingStation station = FindStation(recipe.m_craftingStation.gameObject.name.GetStableHashCode());
				float damage = AmmoDamage(arrow);
				if (station != null && station.GetLevel() >= recipe.m_minStationLevel && damage > bestDamage)
				{
					best = recipe;
					bestDamage = damage;
				}
			}
			if (best == null)
			{
				return(null);
			}
			List<Container> chests = UsableChests();
			foreach (Piece.Requirement requirement in best.m_resources)
			{
				int amount = CraftAmount(requirement, 1);
				if (amount <= 0)
				{
					continue;
				}
				string name = requirement.m_resItem.gameObject.name;
				if (!WoodItems.Contains(name) && CraftStock(name, chests) < amount)
				{
					return(name);
				}
			}
			return(null);
		}

		/// <summary>
		/// Finds the ammo type of the bow or crossbow the viking has of its own
		/// </summary>
		/// <returns>The ammo type, or null when it has none</returns>
		private string BowAmmo()
		{
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (!VikingItems.IsCarried(item) && !VikingItems.IsProp(item) && item.IsWeapon() && !string.IsNullOrEmpty(item.m_shared.m_ammoType) && item.m_shared.m_attack != null && item.m_shared.m_attack.m_attackType == Attack.AttackType.Projectile)
				{
					return(item.m_shared.m_ammoType);
				}
			}
			return(null);
		}

		/// <summary>
		/// Checks whether an item is ammo of a type for the viking's quiver, of its own or in a chest - silver arrows
		/// being kept apart from it, for the dead (see FindSilverArrowWork)
		/// </summary>
		/// <param name="item">The item</param>
		/// <param name="ammoType">The ammo type</param>
		/// <returns>True when it is</returns>
		private static bool IsAmmoOf(ItemDrop.ItemData item, string ammoType)
		{
			return(item != null && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo && item.m_shared.m_ammoType == ammoType && !VikingItems.IsCarried(item) && !VikingItems.IsProp(item) && !VikingSilver.IsSilverArrows(item));
		}

		/// <summary>
		/// Adds up the damage one of a kind of ammo deals
		/// </summary>
		/// <param name="ammo">The ammo</param>
		/// <returns>Its damage</returns>
		private static float AmmoDamage(ItemDrop.ItemData ammo)
		{
			return(ammo.GetDamage().GetTotalDamage());
		}

		/// <summary>
		/// Counts the viking's own arrows of its bow's ammo type and finds how much its best deal
		/// </summary>
		/// <param name="ammoType">The ammo type</param>
		/// <param name="best">How much one of its best arrows deals, or -1 when it has none</param>
		/// <returns>How many it has</returns>
		private int OwnArrows(string ammoType, out float best)
		{
			int count = 0;
			best = -1f;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (IsAmmoOf(item, ammoType))
				{
					count += item.m_stack;
					best = Mathf.Max(best, AmmoDamage(item));
				}
			}
			return(count);
		}

		/// <summary>
		/// Finds the nearest of the village's chests with arrows for an archer - any of its bow's ammo type while it's
		/// missing at least a batch from its quiver, or ones better than its best otherwise
		/// </summary>
		/// <returns>The chest, or null when there's none, or the viking isn't an archer</returns>
		private Container FindArrowChest()
		{
			string ammoType = BowAmmo();
			if (ammoType == null)
			{
				return(null);
			}
			ArrowRecipe(out int batch, out int _);
			int count = OwnArrows(ammoType, out float best);
			bool missing = (count <= QuiverFull - batch);
			return(Nearest(UsableChests().FindAll((Container chest) => !Avoided(chest.m_nview.GetZDO().m_uid) && chest.GetInventory().GetAllItems().Exists((ItemDrop.ItemData item) => IsAmmoOf(item, ammoType) && (missing || AmmoDamage(item) > best)))));
		}

		/// <summary>
		/// Fills an archer's quiver from a chest it has reached with the best arrows among its own and the chest's, up
		/// to QuiverFull, putting back in the chest its own that don't make the cut while the chest has room, then
		/// readies its best
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>False when it took and put back nothing, or the chest couldn't be used</returns>
		private bool TakeArrows(Container chest)
		{
			string ammoType = BowAmmo();
			if (ammoType == null || !OpenChest(chest))
			{
				return(false);
			}
			Inventory own = humanoid.GetInventory();
			Inventory store = chest.GetInventory();
			List<ArrowStack> pool = new List<ArrowStack>();
			foreach (ItemDrop.ItemData item in own.m_inventory.FindAll((ItemDrop.ItemData stored) => IsAmmoOf(stored, ammoType)))
			{
				pool.Add(new ArrowStack(item, AmmoDamage(item), true, item.m_stack));
			}
			foreach (ItemDrop.ItemData item in store.m_inventory.FindAll((ItemDrop.ItemData stored) => IsAmmoOf(stored, ammoType)))
			{
				pool.Add(new ArrowStack(item, AmmoDamage(item), false, item.m_stack));
			}
			FillQuiver(pool, QuiverFull);
			bool changed = false;
			foreach (ArrowStack arrows in pool)
			{
				ItemDrop.ItemData item = arrows.Item;
				int keep = arrows.Keep;
				if (arrows.Own)
				{
					int extra = item.m_stack - keep;
					if (extra > 0)
					{
						ItemDrop.ItemData back = item.Clone();
						back.m_stack = extra;
						back.m_equipped = false;
						if (store.CanAddItem(back) && store.AddItem(back))
						{
							own.RemoveItem(item, extra);
							changed = true;
						}
					}
					continue;
				}
				if (keep <= 0)
				{
					continue;
				}
				ItemDrop.ItemData taken = item.Clone();
				taken.m_stack = keep;
				taken.m_equipped = false;
				if (own.CanAddItem(taken) && own.AddItem(taken))
				{
					store.RemoveItem(item, keep);
					changed = true;
				}
			}
			if (changed)
			{
				own.Changed();
				ReadyBestArrows();
				Plugin.Log.LogInfo(LogName() + " filled its quiver from a chest, " + OwnArrows(ammoType, out float _) + " arrows now");
			}
			return(changed);
		}

		/// <summary>
		/// A stack of arrows an archer weighs filling its quiver from, its own or a chest's
		/// </summary>
		internal class ArrowStack
		{
			/// <summary>
			/// The arrows, or null when only the numbers are weighed
			/// </summary>
			public readonly ItemDrop.ItemData Item;

			/// <summary>
			/// How much damage each arrow does
			/// </summary>
			public readonly float Damage;

			/// <summary>
			/// Whether the archer carries them, rather than the chest holding them
			/// </summary>
			public readonly bool Own;

			/// <summary>
			/// How many arrows the stack holds
			/// </summary>
			public readonly int Stack;

			/// <summary>
			/// How many of them go in the quiver (see FillQuiver)
			/// </summary>
			public int Keep;

			/// <summary>
			/// Makes a stack of arrows to weigh
			/// </summary>
			/// <param name="item">The arrows, or null</param>
			/// <param name="damage">How much damage each does</param>
			/// <param name="own">Whether the archer carries them</param>
			/// <param name="stack">How many the stack holds</param>
			public ArrowStack(ItemDrop.ItemData item, float damage, bool own, int stack)
			{
				Item = item;
				Damage = damage;
				Own = own;
				Stack = stack;
			}
		}

		/// <summary>
		/// Sorts an archer's arrows and a chest's best first, and of two as good its own before the chest's so it
		/// doesn't swap like for like, and works out how many of each go in its quiver, filling it from the best down -
		/// any of its own left over go back in the chest
		/// </summary>
		/// <param name="stacks">The stacks, sorted in place, each given how many it keeps</param>
		/// <param name="quiver">How many arrows the quiver holds</param>
		internal static void FillQuiver(List<ArrowStack> stacks, int quiver)
		{
			stacks.Sort((ArrowStack a, ArrowStack b) =>
			{
				int better = b.Damage.CompareTo(a.Damage);
				return((better != 0) ? better : b.Own.CompareTo(a.Own));
			});
			int room = quiver;
			foreach (ArrowStack arrows in stacks)
			{
				arrows.Keep = Mathf.Min(room, arrows.Stack);
				room -= arrows.Keep;
			}
		}

		/// <summary>
		/// Readies an archer's best arrows, so its bow shoots them before the rest - in place of silver arrows it readied
		/// against the dead, which it readies again when it next fights them (see VikingCore.BestAmmo)
		/// </summary>
		private void ReadyBestArrows()
		{
			string ammoType = (GearHeld() ? null : BowAmmo());
			if (ammoType == null)
			{
				return;
			}
			ItemDrop.ItemData best = null;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (IsAmmoOf(item, ammoType) && (best == null || AmmoDamage(item) > AmmoDamage(best)))
				{
					best = item;
				}
			}
			ItemDrop.ItemData readied = humanoid.GetAmmoItem();
			if (best != null && readied != best && (readied == null || !humanoid.GetInventory().ContainsItem(readied) || VikingSilver.IsSilverArrows(readied) || AmmoDamage(best) > AmmoDamage(readied)))
			{
				humanoid.EquipItem(best, false);
			}
		}

		/// <summary>
		/// Reads how many arrows the game's recipe makes and how much wood it takes
		/// </summary>
		/// <param name="batch">How many arrows a craft makes</param>
		/// <param name="wood">The wood it takes</param>
		private static void ArrowRecipe(out int batch, out int wood)
		{
			batch = ArrowBatchDefault;
			wood = ArrowWoodDefault;
			ItemDrop.ItemData arrow = ItemOf(ArrowPrefab);
			Recipe recipe = ((arrow != null && ObjectDB.instance != null) ? ObjectDB.instance.GetRecipe(arrow) : null);
			if (recipe == null)
			{
				return;
			}
			batch = Mathf.Max(1, recipe.m_amount);
			foreach (Piece.Requirement requirement in recipe.m_resources)
			{
				if (requirement.m_resItem != null && requirement.m_resItem.gameObject.name == RecipeWood)
				{
					wood = Mathf.Max(1, requirement.m_amount);
				}
			}
		}

		/// <summary>
		/// Adds arrows the viking crafted to its quiver, and readies the best it has
		/// </summary>
		/// <param name="prefab">The arrows' prefab name</param>
		/// <param name="count">How many</param>
		private void AddArrows(string prefab, int count)
		{
			humanoid.GetInventory().AddItem(prefab, count, 1, 0, 0L, "", false);
			ReadyBestArrows();
		}

		/// <summary>
		/// Crafting arrows - the best the village can make (see PlanArrows) - at the station their recipe needs: getting
		/// what they take besides wood from the village's chests, getting a batch's wood when it carries too little,
		/// walking to the station and working at it with the player's crafting animation and the station's sounds, then
		/// trading what it carries for the arrows, a batch at a time until its quiver is nearly full - the game's
		/// crafting isn't run. Once it's done, what it carries for crafting goes back to a chest
		/// </summary>
		private class ArrowJob : Job
		{
			private ArrowPlan plan;
			private readonly CraftingStation bench;
			private WoodRun run;
			private Container chest;
			private float actAt = -1f;
			private float craftUntil = -1f;
			private float nextRenew;

			/// <summary>
			/// Makes the job, with the station already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="arrows">The arrows it crafts</param>
			public ArrowJob(VillageLife owner, ArrowPlan arrows) : base(owner)
			{
				plan = arrows;
				bench = arrows.Station;
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name and the arrows it crafts</returns>
			public override string Describe()
			{
				return("Arrow (" + plan.Prefab + ")");
			}

			/// <summary>
			/// Gets the ingredients and wood, walks to the station, works at it and crafts a batch of arrows
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it isn't missing a batch, what it takes is gone, there's no wood left to get, or it can't
			/// get to the station</returns>
			public override bool Tick(float dt)
			{
				if (bench == null)
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.ClaimObject(bench.GetComponent<ZNetView>());
				}
				if (craftUntil >= 0f)
				{
					life.Face(bench.transform.position);
					if (Time.time < craftUntil)
					{
						return(false);
					}
					craftUntil = -1f;
					life.SetCrafting(0);
					if (life.CarriedWood() >= plan.Wood && life.UseCraft(plan))
					{
						life.UseWood(plan.Wood);
						int made = plan.Batch + life.CraftBonus(bench);
						life.AddArrows(plan.Prefab, made);
						TestLog.Count("Crafted", TestLog.ItemName(ItemOf(plan.Prefab)), made);
						VillageSignals.SendEffect(VillageSignals.CraftDoneEffect, bench.GetComponent<ZNetView>().GetZDO().m_uid);
					}
					// The next batch is the best the village can make now, at this station
					ArrowPlan next = (plan.Silver ? life.PlanSilverArrows() : life.PlanArrows());
					if (next == null || next.Station != bench)
					{
						return(true);
					}
					plan = next;
					return(false);
				}
				if (!(plan.Silver ? life.MissingSilverArrows(plan.Batch) : life.MissingArrows(plan.Batch, plan.Wood)))
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
				if (Utils.DistanceXZ(bench.transform.position, life.transform.position) > CraftReach)
				{
					life.Go(dt, bench.transform.position, CraftReach, false);
					return(life.moveFailed);
				}
				life.Face(bench.transform.position);
				life.SetCrafting(Mathf.Max(1, bench.m_useAnimation));
				VillageSignals.SendEffect(VillageSignals.CraftEffect, bench.GetComponent<ZNetView>().GetZDO().m_uid);
				craftUntil = Time.time + CraftTime;
				return(false);
			}

			/// <summary>
			/// Walks to the nearest chest holding an ingredient it needs and takes it - giving up when no chest holds it
			/// any more, or it can't get to the chest
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="ingredient">The ingredient's prefab name</param>
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
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						chest = null;
					}
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
				// A chest it took nothing from - no room to carry it, say - is left alone for a while
				if (life.TakeCraft(chest, ingredient, needed) <= 0)
				{
					life.Avoid(chest.m_nview);
				}
				chest = null;
				actAt = -1f;
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
			/// Stops working at the station and lets go of it and the wood it was after, carrying what it carries for
			/// crafting back to a chest
			/// </summary>
			public override void End()
			{
				life.SetCrafting(0);
				if (run != null)
				{
					run.Release();
				}
				if (bench != null)
				{
					life.ReleaseObject(bench.GetComponent<ZNetView>());
				}
				life.ReturnCraft();
			}
		}
	}
}
