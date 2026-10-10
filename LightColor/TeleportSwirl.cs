using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LightColor
{
	/// <summary>
	/// Colors the swirl the loading screen shows while the player travels through a portal
	/// </summary>
	/// <remarks>
	/// The swirl is a stack of spinning images whose sprites carry its orange colors, so each image is given a
	/// recolored copy of its sprite, and the original sprite is put back for a portal with no color
	/// </remarks>
	internal static class TeleportSwirl
	{
		/// <summary>
		/// The HUD the original sprites were read from, since a new HUD is made each time a world is joined
		/// </summary>
		private static Hud hud;

		/// <summary>
		/// The original sprite of each image in the swirl
		/// </summary>
		private static readonly Dictionary<Image, Sprite> originals = new Dictionary<Image, Sprite>();

		/// <summary>
		/// Sets the swirl to a color, or back to its own colors
		/// </summary>
		/// <param name="color">The color to shift the swirl toward, or null for its own colors</param>
		public static void Apply(Color? color)
		{
			Hud current = Hud.instance;
			if (current == null || current.m_teleportingProgress == null)
			{
				return;
			}
			if (current != hud)
			{
				hud = current;
				originals.Clear();
			}
			foreach (Image image in current.m_teleportingProgress.GetComponentsInChildren<Image>(true))
			{
				if (!originals.TryGetValue(image, out Sprite original))
				{
					original = image.sprite;
					if (original == null)
					{
						continue;
					}
					originals[image] = original;
				}
				image.sprite = (color.HasValue ? Recolor.GetSprite(original, color.Value) : original);
			}
		}
	}
}
