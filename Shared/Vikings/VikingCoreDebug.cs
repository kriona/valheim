using System.Collections.Generic;
using UnityEngine;

namespace Kriona.Vikings
{
	/// <summary>
	/// A viking's debug summary, asked of its owner from a client in debug mode hovering it
	/// </summary>
	internal partial class VikingCore
	{
		/// <summary>
		/// Describes the viking's state and AI for the hover text in debug mode - what kind it is, its health, who runs
		/// it, what it's after and holding, and for a PC viking its village life
		/// </summary>
		/// <remarks>
		/// Most of it is only known on the client that owns the viking, which runs its AI and life, so another client
		/// asks the owner for its summary while the viking is hovered and shows that once it arrives
		/// </remarks>
		/// <returns>The description, in gray text</returns>
		public string DebugSummary()
		{
			if (nview == null || !nview.IsValid() || nview.IsOwner())
			{
				return(OwnSummary("this client"));
			}
			AskOwner();
			if (Time.time - ownerAnsweredAt <= DebugAnswerLife && ownerSummary != null)
			{
				return(ownerSummary);
			}
			return(OwnSummary("another client, asking it for the rest"));
		}

		/// <summary>
		/// Names what the viking is doing, for its name in debug mode - from its own life on its owner, or from the
		/// owner's last answer on another client while the viking is hovered
		/// </summary>
		/// <returns>Its job, or null when it has none or isn't known here</returns>
		public string DebugJob()
		{
			if (nview == null || !nview.IsValid() || nview.IsOwner())
			{
				return(life?.DebugJob());
			}
			if (Time.time - ownerAnsweredAt <= DebugAnswerLife && ownerJob.Length > 0)
			{
				return(ownerJob);
			}
			return(null);
		}

		/// <summary>
		/// Asks the viking's owner for its debug summary, at most once a DebugAskInterval
		/// </summary>
		private void AskOwner()
		{
			if (Time.time - debugAskedAt < DebugAskInterval)
			{
				return;
			}
			debugAskedAt = Time.time;
			nview.InvokeRPC(nview.GetZDO().GetOwner(), DebugAskRpc);
		}

		/// <summary>
		/// Answers a client's ask with this client's debug summary of the viking and what it's doing, on its owner
		/// </summary>
		/// <param name="sender">The client that asked</param>
		private void RPC_DebugAsk(long sender)
		{
			if (!nview.IsOwner())
			{
				return;
			}
			Player player = Player.m_localPlayer;
			string owner = ((player != null) ? player.GetPlayerName() : "the server");
			nview.InvokeRPC(sender, DebugAnswerRpc, OwnSummary(owner), life?.DebugJob() ?? "");
		}

		/// <summary>
		/// Keeps the owner's answer to show in the hover text
		/// </summary>
		/// <param name="sender">The owner</param>
		/// <param name="summary">The owner's debug summary</param>
		/// <param name="job">What the viking is doing, or "" for nothing</param>
		private void RPC_DebugAnswer(long sender, string summary, string job)
		{
			ownerSummary = summary;
			ownerJob = job;
			ownerAnsweredAt = Time.time;
		}

		/// <summary>
		/// Describes the viking as this client knows it, for DebugSummary
		/// </summary>
		/// <param name="owner">Who runs the viking, for the Owner line</param>
		/// <returns>The description, in gray text</returns>
		private string OwnSummary(string owner)
		{
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			string kind = (IsPCViking(humanoid) ? "PC viking" : (IsNPCViking(humanoid) ? "NPC viking" : (IsDvergrViking(humanoid) ? "Dvergr viking" : ((GetData() != null && data.Tombstone) ? "Tombstone zombie viking" : "Zombie viking"))));
			text.Append(kind + ", " + Mathf.CeilToInt(humanoid.GetHealth()) + "/" + Mathf.CeilToInt(humanoid.GetMaxHealth()) + " health\n");
			text.Append("Owner: " + owner + "\n");
			AddDebugLines(text);
			if (ai != null)
			{
				Character target = ai.GetTargetCreature();
				string targetText = ((target != null) ? target.GetHoverName() + " at " + Vector3.Distance(target.transform.position, transform.position).ToString("F1") + "m" : "none");
				text.Append("Target: " + targetText + "\n");
				List<string> flags = new List<string>();
				if (ai.IsAlerted())
				{
					flags.Add("alerted");
				}
				if (ai.IsAggravated())
				{
					flags.Add("aggravated");
				}
				if (ai.IsSleeping())
				{
					flags.Add("asleep");
				}
				if (IsPlayerLikeViking(humanoid))
				{
					flags.Add((InDanger() ? "in danger" : "calm") + ", " + (sheathed ? "sheathed" : "drawn"));
				}
				if (returningSince >= 0f)
				{
					flags.Add("running home");
				}
				if (flags.Count > 0)
				{
					text.Append(string.Join(", ", flags) + "\n");
				}
			}
			ItemDrop.ItemData right = humanoid.m_rightItem;
			ItemDrop.ItemData left = humanoid.m_leftItem;
			string hands = ((right != null) ? right.m_shared.m_name : "-") + " / " + ((left != null) ? left.m_shared.m_name : "-");
			text.Append("Hands: " + Localization.instance.Localize(hands) + "\n");
			if (life != null)
			{
				text.Append(life.DebugSummary());
			}
			return("<color=#C0C0C0>" + text.ToString().TrimEnd('\n') + "</color>");
		}

		/// <summary>
		/// Adds the lines only the viking's kind has to its debug summary, after its owner
		/// </summary>
		/// <param name="text">The summary so far</param>
		partial void AddDebugLines(System.Text.StringBuilder text);

		/// <summary>
		/// Names the viking for the log the way its kind knows it, for a kind whose name isn't the one in its data
		/// </summary>
		/// <param name="label">Its name from its data, set to the name to log</param>
		partial void NameForLog(ref string label);
	}
}
