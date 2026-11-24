using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// What a quest that takes no tool brings back - what it was for, as many as the village wanted
	/// </summary>
	internal static class QuestLoot
	{
		/// <summary>
		/// How much a viking's weapon skill rises for each enemy it kills on a quest, in hits
		/// </summary>
		public const float HitsPerKill = 4f;

		/// <summary>
		/// Works out what a quest that takes no tool brings back, scaled by the world's resource rate
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="record">The quest</param>
		/// <param name="members">Its vikings</param>
		/// <param name="found">What it brings back, added to</param>
		public static void Roll(Village village, QuestRecord record, List<Humanoid> members, Dictionary<string, int> found)
		{
			if (record.Item.Length > 0)
			{
				QuestSim.Add(found, record.Item, QuestGame.Scale(record.Item, Mathf.Max(1, record.Amount)));
			}
		}
	}
}
