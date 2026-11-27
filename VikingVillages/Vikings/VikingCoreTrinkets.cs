using System.Collections.Generic;
using UnityEngine;
using VikingVillages;

namespace Kriona.Vikings
{
	/// <summary>
	/// Something a village's PC vikings get before its NPC vikings - a dverger circlet, a strength belt or pockets
	/// </summary>
	internal enum Trinket
	{
		Circlet,
		Belt,
		Pockets
	}

	/// <summary>
	/// A village's viking as its trinkets are shared out (see VikingCore.ShareTrinkets)
	/// </summary>
	internal struct TrinketHolder
	{
		/// <summary>
		/// Whether it's an NPC viking, whether it has the trinket, and whether it can hand it on or take it
		/// </summary>
		public bool NPC;
		public bool Has;
		public bool Movable;
	}

	/// <summary>
	/// How a PC or NPC viking just rolled up shares out its village's trinkets, so its PC vikings have them before its
	/// NPC vikings
	/// </summary>
	internal partial class VikingCore
	{
		/// <summary>
		/// Whether the viking was rolled up as it last loaded, so it shares out its village's trinkets once it's loaded
		/// </summary>
		private bool justRolled;

		/// <summary>
		/// How near another viking's village has to be noted to the viking's own for the two to be of one village, in meters
		/// </summary>
		private const float SameVillage = 1f;

		/// <summary>
		/// Shares out the village's trinkets once a PC or NPC viking just rolled up has loaded
		/// </summary>
		partial void AfterLoad()
		{
			if (!justRolled)
			{
				return;
			}
			justRolled = false;
			if (IsPlayerLikeViking(humanoid) && nview.IsOwner())
			{
				ShareTrinkets();
			}
		}

		/// <summary>
		/// Shares out a village's dverger circlets and strength belts as a PC or NPC viking is rolled up, so its PC
		/// vikings have them before its NPC vikings - a PC viking rolled without one takes an NPC viking's, and an NPC
		/// viking rolled with one hands it to a PC viking without. Pockets, which are sewn in, only change hands between
		/// the vikings a village starts with, as they're rolled up together, while the rows they'd take are empty
		/// </summary>
		/// <remarks>
		/// Only the village's vikings this client has loaded and owns take part, which as a village is first populated is
		/// every one already rolled up - each viking rolled after does its own share
		/// </remarks>
		private void ShareTrinkets()
		{
			Vector3 farm = nview.GetZDO().GetVec3(Village.VillageKey, Vector3.zero);
			if (farm == Vector3.zero)
			{
				return;
			}
			List<VikingCore> fellows = new List<VikingCore>();
			foreach (Character character in Character.GetAllCharacters())
			{
				VikingCore other = ((character != null && character != humanoid && IsPlayerLikeViking(character) && !character.IsDead()) ? character.GetComponent<VikingCore>() : null);
				if (other != null && other.loaded && other.nview.IsOwner() && Utils.DistanceXZ(other.nview.GetZDO().GetVec3(Village.VillageKey, Vector3.zero), farm) <= SameVillage)
				{
					fellows.Add(other);
				}
			}
			bool starter = nview.GetZDO().GetBool(VillageRespawn.StarterKey);
			foreach (Trinket trinket in new[] { Trinket.Circlet, Trinket.Belt, Trinket.Pockets })
			{
				List<VikingCore> sharing = ((trinket == Trinket.Pockets) ? fellows.FindAll((VikingCore other) => starter && other.nview.GetZDO().GetBool(VillageRespawn.StarterKey)) : fellows);
				List<TrinketHolder> holders = sharing.ConvertAll((VikingCore other) => other.Holder(trinket));
				int partner = TrinketShare.Partner(Holder(trinket), holders);
				if (partner < 0)
				{
					continue;
				}
				VikingCore with = sharing[partner];
				bool npc = IsNPCViking(humanoid);
				VikingCore giver = (npc ? this : with);
				VikingCore taker = (npc ? with : this);
				giver.HandOver(trinket, taker);
			}
		}

		/// <summary>
		/// Describes the viking as a holder of a trinket - whether it's an NPC viking, whether it has the trinket, and
		/// whether it can hand it over or take one: pockets only while the rows they add are empty
		/// </summary>
		/// <param name="trinket">The trinket</param>
		/// <returns>The holder</returns>
		private TrinketHolder Holder(Trinket trinket)
		{
			TrinketHolder holder = new TrinketHolder { NPC = IsNPCViking(humanoid), Movable = true };
			if (trinket == Trinket.Circlet)
			{
				holder.Has = (FindCirclet(null) != null);
			}
			else if (trinket == Trinket.Belt)
			{
				holder.Has = (OwnBelt() != null);
			}
			else
			{
				holder.Has = (GetData() != null && data.Height > VikingUpgrades.PlainRows);
				holder.Movable = (!holder.Has || PocketsEmpty());
			}
			return(holder);
		}

		/// <summary>
		/// Finds the viking's own strength belt - worn, or in its pack, not carried for a chest
		/// </summary>
		/// <returns>The belt, or null when it has none</returns>
		private ItemDrop.ItemData OwnBelt()
		{
			return(humanoid.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => item.m_dropPrefab != null && item.m_dropPrefab.name == VikingUpgrades.BeltPrefab && !VikingItems.IsCarried(item) && !VikingItems.IsProp(item)));
		}

		/// <summary>
		/// Checks whether the rows the viking's pockets add to its inventory are empty
		/// </summary>
		/// <returns>True when they are</returns>
		private bool PocketsEmpty()
		{
			return(!humanoid.GetInventory().GetAllItems().Exists((ItemDrop.ItemData item) => item.m_gridPos.y >= VikingUpgrades.PlainRows));
		}

		/// <summary>
		/// Hands a trinket over to another of the village's vikings - a circlet for the torch the other carries, when it
		/// has one and this viking would go without light, a belt to wear where the other's utility slot is free, or the
		/// rows its pockets add
		/// </summary>
		/// <param name="trinket">The trinket</param>
		/// <param name="taker">The viking it goes to</param>
		private void HandOver(Trinket trinket, VikingCore taker)
		{
			if (trinket == Trinket.Pockets)
			{
				int rows = data.Height;
				SetRows(VikingUpgrades.PlainRows);
				taker.SetRows(rows);
				LogHandOver(trinket, taker);
				return;
			}
			ItemDrop.ItemData item = ((trinket == Trinket.Circlet) ? FindCirclet(null) : OwnBelt());
			if (item == null)
			{
				return;
			}
			Inventory inventory = humanoid.GetInventory();
			Inventory takerInventory = taker.humanoid.GetInventory();
			if (!takerInventory.HaveEmptySlot())
			{
				return;
			}
			if (humanoid.IsItemEquiped(item))
			{
				humanoid.UnequipItem(item, false);
			}
			ItemDrop.ItemData given = item.Clone();
			given.m_equipped = false;
			inventory.RemoveItem(item);
			takerInventory.AddItem(given);
			if (trinket == Trinket.Belt && taker.humanoid.m_utilityItem == null)
			{
				taker.humanoid.EquipItem(given, false);
			}
			// The one who gave up a circlet takes the torch the other no longer needs, so it isn't left in the dark
			ItemDrop.ItemData torch = ((trinket == Trinket.Circlet && FindTorch() == null) ? taker.FindTorch() : null);
			if (torch != null && inventory.HaveEmptySlot())
			{
				if (taker.humanoid.IsItemEquiped(torch))
				{
					taker.humanoid.UnequipItem(torch, false);
				}
				ItemDrop.ItemData lit = torch.Clone();
				lit.m_equipped = false;
				takerInventory.RemoveItem(torch);
				inventory.AddItem(lit);
			}
			SaveInventory();
			taker.SaveInventory();
			LogHandOver(trinket, taker);
		}

		/// <summary>
		/// Takes a new Dverger circlet or strength belt another of the village's vikings hands it - a circlet kept for when
		/// it wants light (see UpdateTorch), and a belt put on when its utility slot is free
		/// </summary>
		/// <param name="prefab">The item's prefab name</param>
		/// <returns>False when it has no room for it, or the game has no such item</returns>
		public bool Receive(string prefab)
		{
			Inventory inventory = humanoid.GetInventory();
			if (!loaded || !inventory.HaveEmptySlot())
			{
				return(false);
			}
			ItemDrop.ItemData item = inventory.AddItem(prefab, 1, 1, 0, 0L, "", false);
			if (item == null)
			{
				return(false);
			}
			if (prefab == VikingUpgrades.BeltPrefab && humanoid.m_utilityItem == null)
			{
				humanoid.EquipItem(item, false);
			}
			SaveInventory();
			return(true);
		}

		/// <summary>
		/// Gives the viking's inventory a number of rows, saving them with its data, where every client and a zombie
		/// viking risen from it read them
		/// </summary>
		/// <param name="rows">The rows</param>
		private void SetRows(int rows)
		{
			if (GetData() == null)
			{
				return;
			}
			data.Height = rows;
			nview.GetZDO().Set(DataKey, data.ToBytes());
			Inventory inventory = humanoid.GetInventory();
			inventory.m_height = rows;
			inventory.Changed();
		}

		/// <summary>
		/// Notes a trinket handed over in the log during a test run or in debug mode
		/// </summary>
		/// <param name="trinket">The trinket</param>
		/// <param name="taker">The viking it went to</param>
		private void LogHandOver(Trinket trinket, VikingCore taker)
		{
			TestLog.Count("Vikings", "trinket handed to a PC viking");
			if (TestLog.On || Player.m_debugMode)
			{
				Plugin.Log.LogInfo("Trinket: " + TestLog.Name(humanoid) + " handed " + trinket + " to " + TestLog.Name(taker.humanoid));
			}
		}
	}

	/// <summary>
	/// Works out who shares a trinket with a viking just rolled up, so a village's PC vikings have it before its NPC
	/// vikings
	/// </summary>
	internal static class TrinketShare
	{
		/// <summary>
		/// Finds the viking a viking just rolled up shares a trinket with - for a PC viking without it, an NPC viking with
		/// it, who hands it over, and for an NPC viking with it, a PC viking without it, who takes it. Each has to be
		/// able to hand it over or take it
		/// </summary>
		/// <param name="self">The viking just rolled up</param>
		/// <param name="others">The rest of its village</param>
		/// <returns>The index of the one it shares with, or -1 for none</returns>
		internal static int Partner(TrinketHolder self, List<TrinketHolder> others)
		{
			if (!self.Movable)
			{
				return(-1);
			}
			bool takes = (!self.NPC && !self.Has);
			bool gives = (self.NPC && self.Has);
			if (!takes && !gives)
			{
				return(-1);
			}
			for (int i = 0; i < others.Count; i++)
			{
				TrinketHolder other = others[i];
				if (!other.Movable)
				{
					continue;
				}
				if ((takes && other.NPC && other.Has) || (gives && !other.NPC && !other.Has))
				{
					return(i);
				}
			}
			return(-1);
		}
	}
}
