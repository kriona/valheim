using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;

namespace ChestPeek
{
	/// <summary>
	/// Hovering a container lists its contents below the game's hover text, with stacks of the same item combined
	/// </summary>
	/// <remarks>
	/// The container's inventory is reloaded from its ZDO every second, so the list is current without opening it
	/// </remarks>
	[HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
	public static class ContainerContentsPatch
	{
		private const int MaxLines = 12;

		/// <summary>
		/// Appends one line per item with its total count, largest first
		/// </summary>
		/// <param name="__instance">The container being hovered</param>
		/// <param name="__result">The hover text built by the game</param>
		private static void Postfix(Container __instance, ref string __result)
		{
			if (__instance.m_checkGuardStone && !PrivateArea.CheckAccess(__instance.transform.position, 0f, false))
			{
				return;
			}
			Dictionary<string, int> totals = new Dictionary<string, int>();
			foreach (ItemDrop.ItemData item in __instance.m_inventory.GetAllItems())
			{
				string name = Localization.instance.Localize(item.m_shared.m_name);
				totals[name] = (totals.TryGetValue(name, out int count) ? count : 0) + item.m_stack;
			}
			if (totals.Count == 0)
			{
				return;
			}
			List<KeyValuePair<string, int>> sorted = totals.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).ToList();
			int shown = ((sorted.Count > MaxLines) ? MaxLines - 1 : sorted.Count);
			StringBuilder text = new StringBuilder("\n");
			for (int i = 0; i < shown; i++)
			{
				text.Append("\n" + sorted[i].Key + " <color=orange>x" + sorted[i].Value + "</color>");
			}
			if (shown < sorted.Count)
			{
				text.Append("\n<color=#a0a0a0>+" + (sorted.Count - shown) + " more</color>");
			}
			__result += text.ToString();
		}
	}
}
