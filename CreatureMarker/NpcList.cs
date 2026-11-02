using System.Collections.Generic;
using UnityEngine;

namespace CreatureMarker
{
	/// <summary>
	/// A trader, like Haldor, or a trader's pet, like Halstein - neither is a Character, so the game keeps no list of them
	/// </summary>
	internal class NPC
	{
		/// <summary>
		/// The Trader or Petable
		/// </summary>
		public Component source;

		/// <summary>
		/// The prefab name, which is the key of its line in the config file
		/// </summary>
		public string key;

		/// <summary>
		/// The prefab name's hash, matching the creature line overrides
		/// </summary>
		public int hash;

		/// <summary>
		/// The name token, localized by the marker when it shows the name
		/// </summary>
		public string nameToken;

		/// <summary>
		/// How far the top of the NPC sits above its position, or -1 until it has been measured
		/// </summary>
		private float height = -1f;

		/// <summary>
		/// How far the top of the NPC sits above its position, measured from its colliders, or its renderers when it
		/// has no solid colliders, the first time it is needed
		/// </summary>
		/// <remarks>
		/// Measuring waits until the NPC is first marked so its colliders and renderers have their final bounds
		/// </remarks>
		/// <returns>The height in meters</returns>
		public float GetHeight()
		{
			if (height >= 0f)
			{
				return(height);
			}
			float bottom = source.transform.position.y;
			float top = float.MinValue;
			foreach (Collider collider in source.GetComponentsInChildren<Collider>())
			{
				if (!collider.isTrigger)
				{
					top = Mathf.Max(top, collider.bounds.max.y);
				}
			}
			if (top == float.MinValue)
			{
				foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>())
				{
					top = Mathf.Max(top, renderer.bounds.max.y);
				}
			}
			height = ((top == float.MinValue) ? 2f : Mathf.Max(0f, top - bottom));
			return(height);
		}
	}

	/// <summary>
	/// The traders and traders' pets in the world, gathered as they are created
	/// </summary>
	internal static class NpcList
	{
		private static readonly List<NPC> npcs = new List<NPC>();

		/// <summary>
		/// The NPCs that still exist
		/// </summary>
		/// <returns>The list, with destroyed NPCs removed</returns>
		public static List<NPC> Get()
		{
			npcs.RemoveAll((NPC npc) => (npc.source == null));
			return(npcs);
		}

		/// <summary>
		/// Adds a trader or pet, and gives it a line in the config file when it doesn't have one yet
		/// </summary>
		/// <param name="source">The Trader or Petable</param>
		/// <param name="nameToken">Its name token</param>
		public static void Add(Component source, string nameToken)
		{
			foreach (NPC existing in npcs)
			{
				if (existing.source == source)
				{
					return;
				}
			}
			NPC npc = new NPC();
			npc.source = source;
			npc.key = Utils.GetPrefabName(source.gameObject);
			npc.hash = npc.key.GetStableHashCode();
			npc.nameToken = nameToken;
			npcs.Add(npc);
			CreatureConfig.AddNPC(npc.key, nameToken);
		}
	}
}
