using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Claims on things vikings work at or use, so no two go for the same one, released when the claimant is done or dead
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Claims a bed or fire for the viking in the object's ZDO, so no other viking takes it - renewing a claim it
		/// already holds
		/// </summary>
		/// <param name="view">The object</param>
		/// <returns>False when another viking holds it</returns>
		private bool ClaimObject(ZNetView view)
		{
			if (view == null || !view.IsValid())
			{
				return(false);
			}
			ZDO zdo = view.GetZDO();
			ZDOID me = nview.GetZDO().m_uid;
			ZDOID holder = zdo.GetZDOID(ClaimKey);
			long now = NetTime.Now();
			if (!ClaimFree(holder, me, zdo.GetLong(ClaimUntilKey), now, ClaimantAlive(holder)))
			{
				return(false);
			}
			view.ClaimOwnership();
			zdo.Set(ClaimKey, me);
			zdo.Set(ClaimUntilKey, now + NetTime.Ticks(ClaimTime));
			return(true);
		}

		/// <summary>
		/// Checks whether no other viking holds a bed, chair or fire
		/// </summary>
		/// <param name="view">The object</param>
		/// <returns>True when it's free</returns>
		private bool IsFree(ZNetView view)
		{
			if (view == null || !view.IsValid())
			{
				return(false);
			}
			ZDO zdo = view.GetZDO();
			ZDOID holder = zdo.GetZDOID(ClaimKey);
			return(ClaimFree(holder, nview.GetZDO().m_uid, zdo.GetLong(ClaimUntilKey), NetTime.Now(), ClaimantAlive(holder)));
		}

		/// <summary>
		/// Checks whether a viking may take a bed, chair or fire from who holds its claim - when no one does, the
		/// viking does itself, the claim has run out or its holder has died
		/// </summary>
		/// <param name="holder">The ZDO of the viking holding the claim, or ZDOID.None</param>
		/// <param name="me">The viking's own ZDO</param>
		/// <param name="until">When the claim runs out, in world ticks</param>
		/// <param name="now">The world time, in ticks</param>
		/// <param name="alive">Whether the holder is still in the world</param>
		/// <returns>True when it's free for the viking</returns>
		internal static bool ClaimFree(ZDOID holder, ZDOID me, long until, long now, bool alive)
		{
			return(holder == ZDOID.None || holder == me || until <= now || !alive);
		}

		/// <summary>
		/// Lets go of the viking's claim on a bed or fire
		/// </summary>
		/// <param name="view">The object</param>
		private void ReleaseObject(ZNetView view)
		{
			if (view == null || !view.IsValid() || nview == null || !nview.IsValid())
			{
				return;
			}
			ZDO zdo = view.GetZDO();
			if (zdo.GetZDOID(ClaimKey) == nview.GetZDO().m_uid)
			{
				view.ClaimOwnership();
				zdo.Set(ClaimKey, ZDOID.None);
				zdo.Set(ClaimUntilKey, 0L);
			}
		}

		/// <summary>
		/// Checks whether a viking holding a claim is still in the world - a viking whose ZDO is gone has died
		/// </summary>
		/// <param name="holder">The viking's ZDO</param>
		/// <returns>True when it's still there</returns>
		private static bool ClaimantAlive(ZDOID holder)
		{
			return(ZDOMan.instance == null || ZDOMan.instance.GetZDO(holder) != null);
		}

		/// <summary>
		/// Claims a piece or branch for the viking among those this client's vikings work on
		/// </summary>
		/// <param name="key">The piece's or branch's ZDO, or where a missing piece stood</param>
		/// <returns>False when another viking has claimed it</returns>
		private bool ClaimWork(object key)
		{
			if (workClaims.TryGetValue(key, out VillageLife holder) && holder != null && holder != this && holder.job != null)
			{
				return(false);
			}
			workClaims[key] = this;
			return(true);
		}

		/// <summary>
		/// Lets go of a piece or branch the viking claimed
		/// </summary>
		/// <param name="key">The piece's or branch's ZDO, or where a missing piece stood</param>
		private void ReleaseWork(object key)
		{
			if (workClaims.TryGetValue(key, out VillageLife holder) && holder == this)
			{
				workClaims.Remove(key);
			}
		}

		/// <summary>
		/// Lets go of everything the viking claimed among the pieces and branches
		/// </summary>
		private void ReleaseWork()
		{
			List<object> held = new List<object>();
			foreach (KeyValuePair<object, VillageLife> claim in workClaims)
			{
				if (claim.Value == this || claim.Value == null)
				{
					held.Add(claim.Key);
				}
			}
			foreach (object key in held)
			{
				workClaims.Remove(key);
			}
		}

		/// <summary>
		/// The key a missing piece is claimed by - where it stood, to the nearest 10 centimeters
		/// </summary>
		/// <param name="record">The piece</param>
		/// <returns>The key</returns>
		private static Vector3Int RecordKey(PieceRecord record)
		{
			return(Vector3Int.RoundToInt(record.Position * 10f));
		}
	}
}
