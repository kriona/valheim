using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Cutting brush trees when the village badly needs wood (see LumberJob), and the axes it cuts them with
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Finds brush trees for the viking to cut down for their wood (see LumberJob) - when the village is out of wood,
		/// none carried, none in the chests and none found lying about lately, while it needs some badly (see
		/// VillageNeedsWood) - one viking at a time, and none while it's badly hurt. A viking with no axe takes one from
		/// the chests; or with none there, leaves the job to a fellow with an axe who's doing nothing that matters, for up
		/// to LumberHandOffWait seconds; or else crafts a stone axe, punching the trees for the wood it takes
		/// </summary>
		/// <returns>The job, or null when there's wood or no pressing need for it, no brush tree to cut, or a fellow with
		/// an axe is taking it on</returns>
		private Job FindLumberWork()
		{
			if (Time.time >= noWoodUntil || Wounded() || OtherDoing(LumberKey) || CarriedWood() > 0 || FindWoodChest() != null)
			{
				return(null);
			}
			if (!VillageNeedsWood())
			{
				return(null);
			}
			TreeBase tree = FindBrushTree();
			if (tree == null)
			{
				return(null);
			}
			Container axeChest = null;
			ArrowPlan axeCraft = null;
			if (OwnAxe() == null)
			{
				axeChest = Nearest(UsableChests().FindAll((Container chest) => !Avoided(chest.m_nview.GetZDO().m_uid) && chest.GetInventory().GetAllItems().Exists((ItemDrop.ItemData item) => !VikingItems.IsCarried(item) && IsAxe(item))));
				if (axeChest == null)
				{
					VillageLife holder = FreeAxeHolder();
					if (holder != null && HandOffLumber(holder))
					{
						return(null);
					}
					axeCraft = PlanCraft(StoneAxePrefab, out string _);
				}
			}
			lumberHandOffSince.Remove(village.Zdo.m_uid);
			return(new LumberJob(this, tree, axeChest, axeCraft));
		}

		/// <summary>
		/// Checks whether the village needs wood badly - one of its vikings on this client is down to fewer than
		/// QuiverCritical arrows, or its workbench or forge needs building back
		/// </summary>
		/// <returns>True when it does</returns>
		private bool VillageNeedsWood()
		{
			if (StationMissing())
			{
				return(true);
			}
			foreach (VillageLife other in All)
			{
				if (other != null && other.village == village && other.humanoid != null && !other.humanoid.IsDead() && other.CriticalArrows())
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// How long a viking with no axe leaves cutting trees to a fellow with one before it takes the job on itself, in
		/// seconds
		/// </summary>
		private const float LumberHandOffWait = 30f;

		/// <summary>
		/// When each village, by its generator's ZDO, started waiting for a viking with an axe to cut its trees, in
		/// Time.time seconds
		/// </summary>
		private static readonly Dictionary<ZDOID, float> lumberHandOffSince = new Dictionary<ZDOID, float>();

		/// <summary>
		/// Finds a fellow of the viking's village on this client with an axe of its own who's free to cut trees - not
		/// fighting, not badly hurt, and only standing about, strolling, talking or petting a boar
		/// </summary>
		/// <returns>The fellow, or null when there's none</returns>
		private VillageLife FreeAxeHolder()
		{
			foreach (VillageLife other in All)
			{
				if (other == null || other == this || other.village != village || !other.active || other.humanoid == null || other.humanoid.IsDead())
				{
					continue;
				}
				bool idle = (other.job == null || other.job is IdleJob || other.job is StrollJob || other.job is TalkJob || other.job is PetJob);
				if (idle && !other.Wounded() && other.OwnAxe() != null)
				{
					return(other);
				}
			}
			return(null);
		}

		/// <summary>
		/// Leaves cutting trees to a fellow with an axe - stopping what it's idly doing, so it looks for work and finds
		/// the job, knowing the village is out of wood - until the village has waited LumberHandOffWait seconds for one
		/// </summary>
		/// <param name="holder">The fellow</param>
		/// <returns>False once the village has waited long enough, so the viking takes the job on itself</returns>
		private bool HandOffLumber(VillageLife holder)
		{
			ZDOID id = village.Zdo.m_uid;
			// A wait long past is a new shortage, waited for afresh
			if (!lumberHandOffSince.TryGetValue(id, out float since) || Time.time - since > LumberHandOffWait * 2f)
			{
				since = Time.time;
				lumberHandOffSince[id] = since;
			}
			if (Time.time - since >= LumberHandOffWait)
			{
				return(false);
			}
			holder.noWoodUntil = Mathf.Max(holder.noWoodUntil, noWoodUntil);
			holder.Interrupt("cutting trees handed to it");
			return(true);
		}

		/// <summary>
		/// Checks whether an item is an axe - a weapon that chops, not held for show
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it is</returns>
		private static bool IsAxe(ItemDrop.ItemData item)
		{
			return(item != null && item.IsWeapon() && !VikingItems.IsProp(item) && item.GetDamage().m_chop > 0f);
		}

		/// <summary>
		/// Checks whether the village's workbench or forge has been destroyed and is waiting to be built back
		/// </summary>
		/// <returns>True when one is</returns>
		private bool StationMissing()
		{
			System.Predicate<PieceRecord> station = (PieceRecord record) => record.Prefab == WorkbenchHash || record.Prefab == ForgeHash;
			return(VillageRecords.Missing(village).Exists(station) || VillageRecords.MissingInRuins(village).Exists(station));
		}

		/// <summary>
		/// Checks whether a tree is a brush tree - a small one that drops its wood when it falls rather than a log to
		/// split, which any tool cuts
		/// </summary>
		/// <param name="tree">The tree, or null</param>
		/// <returns>True when it is</returns>
		private static bool IsBrush(TreeBase tree)
		{
			if (tree == null || tree.m_logPrefab != null || tree.m_minToolTier > 0 || tree.m_dropWhenDestroyed == null)
			{
				return(false);
			}
			return(tree.m_dropWhenDestroyed.m_drops.Exists((DropTable.DropData drop) => drop.m_item != null && WoodItems.Contains(drop.m_item.name)));
		}

		/// <summary>
		/// Finds the nearest brush tree within ForageReach of the village - one no other viking is after or the viking
		/// couldn't get to lately, with no danger near it - only in the village while the viking may not leave it (see
		/// MayLeave)
		/// </summary>
		/// <returns>The tree, or null when there's none</returns>
		private TreeBase FindBrushTree()
		{
			float reach = (MayLeave() ? ForageReach : 0f);
			List<KeyValuePair<float, TreeBase>> found = new List<KeyValuePair<float, TreeBase>>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZNetView view = instance.Value;
				if (view == null || village.DistanceOutside(view.transform.position) > reach || Avoided(instance.Key.m_uid))
				{
					continue;
				}
				TreeBase tree = view.GetComponent<TreeBase>();
				if (!IsBrush(tree))
				{
					continue;
				}
				if (workClaims.TryGetValue(instance.Key.m_uid, out VillageLife holder) && holder != this && holder != null && holder.job != null)
				{
					continue;
				}
				found.Add(new KeyValuePair<float, TreeBase>(Vector3.Distance(view.transform.position, transform.position), tree));
			}
			found.Sort((KeyValuePair<float, TreeBase> a, KeyValuePair<float, TreeBase> b) => a.Key.CompareTo(b.Key));
			foreach (KeyValuePair<float, TreeBase> entry in found)
			{
				if (!DangerNear(entry.Value.transform.position))
				{
					return(entry.Value);
				}
			}
			return(null);
		}

		/// <summary>
		/// Finds the viking's own axe - a weapon of its own that chops
		/// </summary>
		/// <returns>The axe, or null when it has none</returns>
		private ItemDrop.ItemData OwnAxe()
		{
			return(humanoid.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => item.IsWeapon() && !VikingItems.IsCarried(item) && !VikingItems.IsProp(item) && item.GetDamage().m_chop > 0f));
		}

		/// <summary>
		/// Swings at a tree the way a player chops one - its own axe's chopping damage and tier, or a punch's PunchChop
		/// with no axe - raising its WoodCutting skill the way a player's rises
		/// </summary>
		/// <param name="tree">The tree</param>
		private void ChopTree(TreeBase tree)
		{
			ItemDrop.ItemData axe = OwnAxe();
			HitData hit = new HitData();
			hit.m_damage.m_chop = ((axe != null) ? axe.GetDamage().m_chop : PunchChop);
			hit.m_toolTier = (short)((axe != null) ? axe.m_shared.m_toolTier : 0);
			hit.m_point = tree.transform.position + Vector3.up;
			hit.m_dir = (tree.transform.position - transform.position).normalized;
			hit.m_skill = Skills.SkillType.WoodCutting;
			hit.SetAttacker(humanoid);
			tree.Damage(hit);
			RaiseSkill(Skills.SkillType.WoodCutting, 1f);
			TestLog.Count("Work done", "axe swings at trees");
		}

		/// <summary>
		/// Lumberjane - cutting brush trees by day when the village is out of wood and badly needs some (see
		/// FindLumberWork): with no axe of its own, first taking one from a chest, or else crafting a stone axe - and
		/// while it can't, for want of wood, punching the trees instead, trying again with the wood each one drops. Then
		/// walking to the nearest tree, holding its axe, and chopping at it the way a player does until it falls and
		/// drops its wood, up to LumberTrees of them. The wood lies where it fell for the work that needs it to gather (see
		/// WoodRun), and an axe it took from a chest or crafted goes back in one, so the village keeps one in storage.
		/// Heading home as soon as an enemy comes near outside the village
		/// </summary>
		private class LumberJob : Job
		{
			private TreeBase tree;
			private ZDOID treeId;
			private ItemRun axeRun;
			private ItemDrop.ItemData borrowed;
			private int felled;
			private int swings;
			private float hitAt = -1f;
			private float nextSwing;
			private float nextRenew;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="first">The first tree it cuts</param>
			/// <param name="axeChest">The chest it takes an axe from, or null</param>
			/// <param name="axeCraft">What a stone axe takes to craft when it crafts one, or null</param>
			public LumberJob(VillageLife owner, TreeBase first, Container axeChest, ArrowPlan axeCraft) : base(owner)
			{
				Take(first);
				if (axeChest != null || axeCraft != null)
				{
					axeRun = new ItemRun(owner, axeChest, IsAxe, StoneAxePrefab, axeCraft);
				}
			}

			/// <summary>
			/// Notes that the viking is cutting trees, so no other viking of the village starts to
			/// </summary>
			public override void Begin()
			{
				life.SetDoing(LumberKey, true);
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name, and how many trees it has cut</returns>
			public override string Describe()
			{
				return("Lumberjane (" + felled + "/" + LumberTrees + ")");
			}

			/// <summary>
			/// Checks whether the tree the viking is cutting, or the wood it's after for an axe, is outside the village
			/// </summary>
			/// <returns>True when it is</returns>
			public override bool Outing()
			{
				return((tree != null && !life.village.Contains(tree.transform.position)) || (axeRun != null && axeRun.Outing()));
			}

			/// <summary>
			/// Gets an axe when it has none, then walks to the tree and chops at it until it falls - or punches it, with no
			/// axe to be had yet - then goes on to the next
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has cut LumberTrees, there's no tree left to cut, or it's dusk</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk())
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					life.SetDoing(LumberKey, true);
					nextRenew = Time.time + ClaimRenew;
					if (axeRun != null)
					{
						axeRun.Renew();
					}
				}
				if (!life.village.Contains(life.transform.position) && life.DangerNear(life.transform.position))
				{
					life.fleeing = true;
					return(true);
				}
				if (axeRun != null && life.OwnAxe() == null)
				{
					if (!axeRun.Tick(dt, out ItemDrop.ItemData got))
					{
						return(false);
					}
					// With no axe to be had, it punches the trees, and tries crafting one again with the wood they drop
					axeRun.Release();
					axeRun = null;
					borrowed = (got ?? borrowed);
				}
				if (tree == null || tree.m_nview == null || !tree.m_nview.IsValid())
				{
					if (swings > 0)
					{
						felled++;
						TryCraftingAxe();
					}
					Release();
					if (felled >= LumberTrees)
					{
						return(true);
					}
					Take(life.FindBrushTree());
					if (tree == null)
					{
						return(true);
					}
					life.UpdateOuting(Outing());
				}
				Vector3 spot = tree.transform.position;
				if (hitAt >= 0f)
				{
					life.Face(spot);
					if (Time.time < hitAt)
					{
						return(false);
					}
					hitAt = -1f;
					bool punching = (life.OwnAxe() == null);
					life.ChopTree(tree);
					swings++;
					// A tree that won't fall - one it can't hurt, say - is left alone for a while
					if (swings >= (punching ? LumberPunchesMax : LumberSwingsMax))
					{
						life.Avoid(tree.m_nview);
						Release();
						swings = 0;
					}
					return(false);
				}
				if (Utils.DistanceXZ(spot, life.transform.position) > WorkReach)
				{
					life.Go(dt, spot, WorkReach, false);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(tree.m_nview);
						Release();
						swings = 0;
					}
					return(false);
				}
				life.Face(spot);
				ItemDrop.ItemData axe = life.OwnAxe();
				ItemDrop.ItemData held = null;
				if (axe != null && axe.m_dropPrefab != null)
				{
					life.HoldProp(axe.m_dropPrefab.name);
					held = life.humanoid.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => VikingItems.IsProp(item));
				}
				else
				{
					// A punch, with nothing in its hands
					life.HoldHammer(false);
					held = ((life.humanoid.m_unarmedWeapon != null) ? life.humanoid.m_unarmedWeapon.m_itemData : null);
				}
				if (Time.time < nextSwing)
				{
					return(false);
				}
				VikingPoses.Swing(life.humanoid, held);
				hitAt = Time.time + SwingHitTime;
				nextSwing = Time.time + SwingInterval;
				return(false);
			}

			/// <summary>
			/// Tries crafting a stone axe again, when the viking still has none and the village has what one takes
			/// besides the wood it's cutting
			/// </summary>
			private void TryCraftingAxe()
			{
				if (axeRun != null || life.OwnAxe() != null)
				{
					return;
				}
				ArrowPlan plan = life.PlanCraft(StoneAxePrefab, out string _);
				if (plan != null && (plan.Station == null || life.ClaimObject(plan.Station.GetComponent<ZNetView>())))
				{
					axeRun = new ItemRun(life, null, IsAxe, StoneAxePrefab, plan);
				}
			}

			/// <summary>
			/// Starts on a tree, claiming it so no other viking goes for it
			/// </summary>
			/// <param name="next">The tree, or null</param>
			private void Take(TreeBase next)
			{
				tree = next;
				swings = 0;
				hitAt = -1f;
				if (tree != null && tree.m_nview != null && tree.m_nview.IsValid())
				{
					treeId = tree.m_nview.GetZDO().m_uid;
					life.ClaimWork(treeId);
				}
			}

			/// <summary>
			/// Lets go of the tree it was cutting
			/// </summary>
			private void Release()
			{
				if (treeId != ZDOID.None)
				{
					life.ReleaseWork(treeId);
					treeId = ZDOID.None;
				}
				tree = null;
			}

			/// <summary>
			/// Lets go of the tree, puts the axe away - carrying one it took from a chest or crafted back to a chest - and
			/// notes it's no longer cutting trees, and that there may be wood lying about again, so the work that needs it
			/// looks for it
			/// </summary>
			public override void End()
			{
				Release();
				if (axeRun != null)
				{
					axeRun.Release();
					axeRun = null;
				}
				life.HoldHammer(false);
				if (borrowed != null && life.humanoid.GetInventory().ContainsItem(borrowed))
				{
					borrowed.m_customData[VikingItems.CarryKey] = CarryLoot;
					life.humanoid.GetInventory().Changed();
				}
				life.SetDoing(LumberKey, false);
				if (felled > 0)
				{
					life.noWoodUntil = 0f;
				}
			}
		}
	}
}
