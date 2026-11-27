using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Foraging by day for food and flint growing near the village (see ForageJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// The food each pickable's prefab gives, by the prefab's name, or null for one that gives no food
		/// </summary>
		private static readonly Dictionary<string, ItemDrop.ItemData> forageFoods = new Dictionary<string, ItemDrop.ItemData>();

		/// <summary>
		/// What each creature's or bird's prefab drops when it's killed, by the prefab's name (see Drops)
		/// </summary>
		private static readonly Dictionary<string, HashSet<string>> dropsByPrefab = new Dictionary<string, HashSet<string>>();

		/// <summary>
		/// What the vikings gather besides food, by prefab name, whether it grows back or not - flint, for arrows, and
		/// stone, only gathered for a hoe (see FindPathWork)
		/// </summary>
		private static readonly HashSet<string> ForageMaterials = new HashSet<string> { "Flint", StonePrefab };
		private const string StonePrefab = "Stone";

		/// <summary>
		/// Finds what a pickable gives that the viking gathers - food that grows back, like berries and mushrooms, or what
		/// a cauldron's recipe takes that grows back, like thistle (see FindForage), but not crops a player planted, or
		/// flint (see ForageMaterials)
		/// </summary>
		/// <param name="pickable">The pickable</param>
		/// <returns>The item, or null when it gives nothing the viking gathers</returns>
		private static ItemDrop.ItemData ForageFood(Pickable pickable)
		{
			if (pickable.m_itemPrefab == null || ObjectDB.instance == null)
			{
				return(null);
			}
			string name = pickable.m_itemPrefab.name;
			bool material = ForageMaterials.Contains(name);
			if (!material && pickable.m_respawnTimeMinutes <= 0f)
			{
				return(null);
			}
			if (!forageFoods.TryGetValue(name, out ItemDrop.ItemData food))
			{
				food = ItemOf(name);
				if (!material && !VikingFood.IsFood(food) && !VillageCooking.IsCauldronIngredient(name))
				{
					food = null;
				}
				forageFoods[name] = food;
			}
			return(food);
		}

		/// <summary>
		/// Checks whether a pickable is something the viking can forage now - grown back, and giving food or flint
		/// </summary>
		/// <param name="pickable">The pickable</param>
		/// <returns>True when it is</returns>
		private static bool IsForage(Pickable pickable)
		{
			return(pickable != null && pickable.m_nview != null && pickable.m_nview.IsValid() && pickable.CanBePicked() && ForageFood(pickable) != null);
		}

		/// <summary>
		/// Counts how many of an item the viking carries for a chest
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>How many</returns>
		private int CarriedLootOf(ItemDrop.ItemData item)
		{
			int count = 0;
			foreach (ItemDrop.ItemData carried in humanoid.GetInventory().GetAllItems())
			{
				if (StacksWith(carried, item, CarryLoot))
				{
					count += carried.m_stack;
				}
			}
			return(count);
		}

		/// <summary>
		/// Finds the nearest food to forage within ForageReach of the village - one no other viking is after, with no
		/// enemy within ForageSafety of it, that the viking has room to carry and one of the village's chests has room
		/// for along with what it already carries of it - only in the village while the viking may not leave it (see
		/// MayLeave). Stone is only gathered when it's what the viking is after, and what only a cauldron's recipe takes,
		/// like thistle, only by a village with a cauldron
		/// </summary>
		/// <param name="only">The prefab name of the one thing the viking is after, or null for any food or flint</param>
		/// <returns>The pickable, or null when there's none</returns>
		private Pickable FindForage(string only = null)
		{
			List<Container> chests = UsableChests();
			if (chests.Count == 0)
			{
				return(null);
			}
			bool cauldron = (FindStation(VillageCooking.CauldronHash) != null);
			float reach = (MayLeave() ? ForageReach : 0f);
			List<KeyValuePair<float, Pickable>> found = new List<KeyValuePair<float, Pickable>>();
			foreach (Pickable pickable in LoadedPickables())
			{
				ZNetView view = ((pickable != null) ? pickable.m_nview : null);
				if (view == null || !view.IsValid() || village.DistanceOutside(view.transform.position) > reach)
				{
					continue;
				}
				ZDOID id = view.GetZDO().m_uid;
				if (Avoided(id) || !IsForage(pickable))
				{
					continue;
				}
				string gives = pickable.m_itemPrefab.name;
				if ((only != null) ? (gives != only) : (gives == StonePrefab))
				{
					continue;
				}
				if (!cauldron && !ForageMaterials.Contains(gives) && !VikingFood.IsFood(ForageFood(pickable)))
				{
					continue;
				}
				if (workClaims.TryGetValue(id, out VillageLife holder) && holder != this && holder != null && holder.job != null)
				{
					continue;
				}
				found.Add(new KeyValuePair<float, Pickable>(Vector3.Distance(view.transform.position, transform.position), pickable));
			}
			found.Sort((KeyValuePair<float, Pickable> a, KeyValuePair<float, Pickable> b) => a.Key.CompareTo(b.Key));
			int unreachable = 0;
			foreach (KeyValuePair<float, Pickable> entry in found)
			{
				Pickable pickable = entry.Value;
				ItemDrop.ItemData food = ForageFood(pickable).Clone();
				int amount = ForageAmount(pickable);
				food.m_stack = CarriedLootOf(food) + amount;
				if (CarryRoom(food, CarryLoot) < amount || !chests.Exists((Container chest) => HasRoom(chest, food, chests)))
				{
					continue;
				}
				if (DangerNear(pickable.transform.position))
				{
					continue;
				}
				// Food it can't get within reach of is left alone for a while - except in a pen, which has no way in but
				// over its fence, which a forager jumps
				if (!InPen(pickable.m_nview.GetZDO().m_uid, pickable.transform.position) && !CanReach(pickable.transform.position, out Vector3 _))
				{
					Avoid(pickable.m_nview);
					unreachable++;
					if (unreachable >= ReachTries)
					{
						return(null);
					}
					continue;
				}
				return(pickable);
			}
			return(null);
		}

		/// <summary>
		/// Picks food the viking has reached, the same as a player picking it, so it grows back as usual - carried for a
		/// chest rather than dropped
		/// </summary>
		/// <remarks>
		/// It picks as much as a player would - scaled by the world's resource rate, unless the pickable isn't scaled -
		/// and, following Pickable.Interact, gets the pickable's bonus from the skill picking it raises, as often as the
		/// pickable's chance at full skill, scaled by how far up the skill the viking is (see ForageBonus). What raises
		/// no skill, like flint and stone, gives no bonus and raises nothing. Every client shows the bonus above the
		/// viking with the pickable's bonus sound, and the skill rises for each pick, the same as a player's
		/// </remarks>
		/// <param name="pickable">The pickable</param>
		/// <returns>How many it picked</returns>
		private int PickForage(Pickable pickable)
		{
			if (!IsForage(pickable))
			{
				return(0);
			}
			ItemDrop.ItemData food = ForageFood(pickable);
			int amount = ForageAmount(pickable);
			Skills.SkillType skill = pickable.m_pickRaiseSkill;
			float skillFactor = ((skill != Skills.SkillType.None) ? humanoid.GetSkillFactor(skill) : 0f);
			int bonus = ForageBonus(skill, skillFactor, pickable.m_maxLevelBonusChance, pickable.m_bonusYieldAmount, Random.value);
			int room = CarryRoom(food, CarryLoot);
			if (room < amount)
			{
				return(0);
			}
			bonus = Mathf.Min(bonus, room - amount);
			// The bonus goes out before the pick, since picking something that doesn't grow back destroys it and its ZDO
			// at once on its owner
			if (bonus > 0)
			{
				VillageSignals.SendForageBonus(pickable.m_nview.GetZDO().m_uid, nview.GetZDO().m_uid, bonus);
			}
			pickable.m_pickEffector.Create(pickable.transform.position, Quaternion.identity);
			pickable.m_nview.InvokeRPC(ZNetView.Everybody, "RPC_SetPicked", true);
			if (viking != null && skill != Skills.SkillType.None)
			{
				viking.RaiseSkill(skill, 1f);
			}
			int carried = Carry(food, amount + bonus, CarryLoot);
			TestLog.Count("Gathered", TestLog.ItemName(food), carried);
			return(carried);
		}

		/// <summary>
		/// Works out the bonus a pick gives, following Pickable.Interact - only for a pickable whose picking raises a
		/// skill, as often as its chance at full skill, scaled by how far up that skill the picker is
		/// </summary>
		/// <param name="skill">The skill picking it raises, or None</param>
		/// <param name="skillFactor">How far up that skill the picker is, from 0 to 1</param>
		/// <param name="maxLevelBonusChance">The pickable's chance of a bonus at full skill</param>
		/// <param name="bonusYieldAmount">How much more a bonus gives</param>
		/// <param name="roll">A random number from 0 to 1</param>
		/// <returns>How much more it gives, or 0 for no bonus</returns>
		internal static int ForageBonus(Skills.SkillType skill, float skillFactor, float maxLevelBonusChance, int bonusYieldAmount, float roll)
		{
			if (skill == Skills.SkillType.None || roll >= skillFactor * maxLevelBonusChance)
			{
				return(0);
			}
			return(Mathf.Max(0, bonusYieldAmount));
		}

		/// <summary>
		/// Works out how much food picking a pickable gives before any bonus - its amount scaled by the world's resource
		/// rate, the same as for a player, unless it isn't scaled
		/// </summary>
		/// <param name="pickable">The pickable</param>
		/// <returns>How many</returns>
		private static int ForageAmount(Pickable pickable)
		{
			int amount = Mathf.Max(1, pickable.m_amount);
			if (pickable.m_dontScale || Game.instance == null || pickable.m_itemPrefab == null)
			{
				return(amount);
			}
			return(Mathf.Max(1, Mathf.Max(pickable.m_minAmountScaled, Game.instance.ScaleDrops(pickable.m_itemPrefab, amount))));
		}

		/// <summary>
		/// Foraging by day - taking the honey from the beehives built in the village, and picking berries, mushrooms and
		/// other food growing within ForageReach of the village, and flint, away from enemies, until it carries ForageLoad or
		/// there's no more a chest has room for, then going home, where it puts what it gathered away as loot
		/// </summary>
		/// <remarks>
		/// When an enemy comes for it or it's hurt outside the village, UpdateFlee runs it home for help
		/// </remarks>
		private class ForageJob : Job
		{
			private Pickable target;
			private Beehive hive;
			private ZDOID targetId;
			private int picked;
			private float pickAt = -1f;
			private float nextRenew;

			/// <summary>
			/// The prefab name of the one thing the viking gathers, like stone for a hoe, or null for any food or flint
			/// </summary>
			private readonly string only;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="onlyItem">The prefab name of the one thing it gathers, leaving the beehives alone, or null
			/// for any food or flint</param>
			public ForageJob(VillageLife owner, string onlyItem = null) : base(owner)
			{
				only = onlyItem;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name, and the one thing it gathers</returns>
			public override string Describe()
			{
				return((only != null) ? "Forage (" + only + ")" : "Forage");
			}

			/// <summary>
			/// Notes that the viking is foraging, so no other viking of the village starts to
			/// </summary>
			public override void Begin()
			{
				life.SetDoing(ForagingKey, true);
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Checks whether the food the viking is after is outside the village
			/// </summary>
			/// <returns>True when it is</returns>
			public override bool Outing()
			{
				return(target != null && !life.village.Contains(target.transform.position));
			}

			/// <summary>
			/// Finds the nearest food, walks to it and picks it, until it has gathered enough - heading home as soon as
			/// an enemy comes near
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has gathered enough or there's no more to gather, or it's dusk</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk() || picked >= ForageLoad)
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					life.SetDoing(ForagingKey, true);
					nextRenew = Time.time + ClaimRenew;
				}
				if (!life.village.Contains(life.transform.position) && life.DangerNear(life.transform.position))
				{
					life.fleeing = true;
					return(true);
				}
				if (hive != null)
				{
					return(Harvest(dt));
				}
				if (target == null || !IsForage(target))
				{
					Release();
					// The village's beehives come first, since they're close by
					hive = ((only == null) ? life.FindHive() : null);
					if (hive != null)
					{
						targetId = hive.m_nview.GetZDO().m_uid;
						life.ClaimWork(targetId);
						pickAt = -1f;
						return(false);
					}
					target = life.FindForage(only);
					if (target == null)
					{
						return(true);
					}
					targetId = target.m_nview.GetZDO().m_uid;
					life.ClaimWork(targetId);
					life.UpdateOuting(Outing());
					pickAt = -1f;
				}
				if (pickAt >= 0f)
				{
					if (Time.time < pickAt)
					{
						return(false);
					}
					int got = life.PickForage(target);
					// Only food picked counts as food found around the village, not flint, stone or what the cauldron takes
					if (got > 0 && VikingFood.IsFood(ForageFood(target)))
					{
						life.NoteFoodFound();
					}
					picked += got;
					Release();
					target = null;
					return(false);
				}
				if (Utils.DistanceXZ(target.transform.position, life.transform.position) > ItemReach)
				{
					life.Go(dt, target.transform.position, ItemReach, false);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(target.m_nview);
						Release();
						target = null;
					}
					return(false);
				}
				life.Face(target.transform.position);
				VikingPoses.Interact(life.humanoid);
				pickAt = Time.time + ReachTime;
				return(false);
			}

			/// <summary>
			/// Walks to the beehive and takes its honey - leaving a hive it can't get to alone
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>False, so the job goes on</returns>
			private bool Harvest(float dt)
			{
				if (hive == null || hive.GetHoneyLevel() <= 0)
				{
					Release();
					hive = null;
					return(false);
				}
				if (pickAt >= 0f)
				{
					if (Time.time < pickAt)
					{
						return(false);
					}
					picked += life.TakeHoney(hive);
					Release();
					hive = null;
					return(false);
				}
				if (Utils.DistanceXZ(hive.transform.position, life.transform.position) > ChestReach)
				{
					life.Go(dt, hive.transform.position, ChestReach, false);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(hive.m_nview);
						Release();
						hive = null;
					}
					return(false);
				}
				life.Face(hive.transform.position);
				VikingPoses.Interact(life.humanoid);
				pickAt = Time.time + ReachTime;
				return(false);
			}

			/// <summary>
			/// Lets go of the food or beehive it was after
			/// </summary>
			private void Release()
			{
				if (targetId != ZDOID.None)
				{
					life.ReleaseWork(targetId);
					targetId = ZDOID.None;
				}
			}

			/// <summary>
			/// Lets go of the food it was after, keeping what it gathered to put away, and notes it's no longer foraging
			/// </summary>
			public override void End()
			{
				Release();
				life.SetDoing(ForagingKey, false);
			}
		}
	}
}
