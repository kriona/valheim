using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A valkyrie's color - picked at random as she's first spawned, each of ValkyrieColors' as likely, kept in her ZDO
	/// and shown on every client (see ValkyrieColors.Apply)
	/// </summary>
	internal class ValkyrieColor : MonoBehaviour
	{
		/// <summary>
		/// The ZDO key holding her color, by its index in ValkyrieColors
		/// </summary>
		public static readonly int ColorKey = "kri_vv_valkyrie_color".GetStableHashCode();

		/// <summary>
		/// How often her ZDO is checked for a color she doesn't show yet, in seconds
		/// </summary>
		private const float CheckInterval = 1f;

		private ZNetView nview;
		private Transform model;

		/// <summary>
		/// Her renderers' own materials, the color she shows, or -1 before she shows one, and when her ZDO is next checked
		/// </summary>
		private readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
		private int shown = -1;
		private float nextCheck;

		/// <summary>
		/// Gets the color a valkyrie's ZDO holds
		/// </summary>
		/// <param name="zdo">Her ZDO</param>
		/// <returns>The color's index, or -1 when she has none yet</returns>
		public static int Get(ZDO zdo)
		{
			int color = ((zdo != null) ? zdo.GetInt(ColorKey, -1) : -1);
			return(ValkyrieColors.IsColor(color) ? color : -1);
		}

		/// <summary>
		/// Finds her model, and on the client that spawns her, picks her color
		/// </summary>
		private void Awake()
		{
			nview = GetComponent<ZNetView>();
			ValkyrieModel posing = GetComponentInChildren<ValkyrieModel>(true);
			model = ((posing != null) ? posing.transform : null);
			if (nview == null || !nview.IsValid() || !nview.IsOwner() || Get(nview.GetZDO()) >= 0)
			{
				return;
			}
			int color = Random.Range(0, ValkyrieColors.Targets.Length);
			nview.GetZDO().Set(ColorKey, color);
		}

		/// <summary>
		/// Shows her color once she's awake
		/// </summary>
		private void Start()
		{
			Show();
		}

		/// <summary>
		/// Shows the color her ZDO holds every CheckInterval seconds, once it's there, or when it's changed
		/// </summary>
		private void Update()
		{
			if (Time.time < nextCheck)
			{
				return;
			}
			nextCheck = Time.time + CheckInterval;
			Show();
		}

		/// <summary>
		/// Shows the color her ZDO holds, unless she shows it already
		/// </summary>
		private void Show()
		{
			if (nview == null || !nview.IsValid())
			{
				return;
			}
			int color = Get(nview.GetZDO());
			if (color < 0 || color == shown)
			{
				return;
			}
			shown = color;
			ValkyrieColors.Apply(model, originals, color);
		}
	}
}
