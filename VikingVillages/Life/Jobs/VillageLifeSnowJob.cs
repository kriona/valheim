using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Clearing the snow off a piece of the village in the Deep North (see SnowJob)
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Clearing the snow built up on a piece of the village in the Deep North - walking to it and swinging at it, the
		/// way it repairs one, which knocks all of its snow off
		/// </summary>
		private class SnowJob : Job
		{
			private readonly WearNTear piece;
			private readonly ZDOID pieceId;
			private readonly HammerWork work;

			/// <summary>
			/// The part of the piece it works on, where it walks to reach it, and whether they've been picked
			/// </summary>
			private Vector3 aim;
			private Vector3 stand;
			private bool hasAim;

			/// <summary>
			/// Whether the spot it walks to was found on the navmesh, rather than being the ground under the aim
			/// </summary>
			private bool standOnMesh;

			/// <summary>
			/// When it gives up on the piece if it hasn't got within reach of it
			/// </summary>
			private readonly float giveUpAt;

			/// <summary>
			/// Makes the job, with the piece already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="target">The piece</param>
			/// <param name="id">Its ZDO</param>
			public SnowJob(VillageLife owner, WearNTear target, ZDOID id) : base(owner)
			{
				piece = target;
				pieceId = id;
				work = new HammerWork(owner, RepairSwings);
				giveUpAt = Time.time + RepairGiveUpTime;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>The description</returns>
			public override string Describe()
			{
				return("Clear snow" + ((piece != null) ? " off " + Utils.GetPrefabName(piece.gameObject) + ", " + Utils.DistanceXZ(piece.transform.position, life.transform.position).ToString("F1") + "m off" : ""));
			}

			/// <summary>
			/// Walks to the piece, swings at it and clears its snow off
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once its snow is off, or it's gone, or it can't get there</returns>
			public override bool Tick(float dt)
			{
				if (piece == null || VillageSnow.Snow(piece) < VillageSnow.ClearAt)
				{
					return(true);
				}
				if (!work.Started && Time.time >= giveUpAt)
				{
					life.LogHammerGiveUp("Snow", piece, false, hasAim, aim, stand, standOnMesh);
					life.ai.StopMoving();
					life.AvoidHammerWork(piece.m_nview);
					return(true);
				}
				// Picked once and walked to on the ground under it, the way a piece is repaired
				if (!hasAim)
				{
					aim = NearestPoint(piece, life.transform.position);
					Vector3 ground = aim;
					ground.y = ZoneSystem.instance.GetGroundHeight(aim);
					standOnMesh = NavmeshPoints.FindValidPoint(out Vector3 valid, ground, WorkReach, Pathfinding.AgentType.Humanoid);
					stand = (standOnMesh ? valid : ground);
					hasAim = true;
				}
				if (!work.Tick(dt, aim, stand))
				{
					if (life.moveFailed)
					{
						life.LogHammerGiveUp("Snow", piece, true, hasAim, aim, stand, standOnMesh);
						life.AvoidHammerWork(piece.m_nview);
						return(true);
					}
					return(false);
				}
				piece.ChangeSnow(-1f);
				return(true);
			}

			/// <summary>
			/// Lets go of the piece
			/// </summary>
			public override void End()
			{
				life.ReleaseWork(pieceId);
			}
		}
	}
}
