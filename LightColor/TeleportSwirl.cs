using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LightColor
{
	/// <summary>
	/// Colors the swirl the loading screen shows while the player travels through a portal, fading it from the color
	/// of the portal entered to the color of the portal at the far end one ring at a time, from the center outward
	/// </summary>
	/// <remarks>
	/// The swirl is a stack of spinning images, drawn from the starry center out to the widest ring, whose sprites
	/// carry its orange colors. Each image shows a recolored copy of its sprite in the entry color, with an overlay
	/// image added as its child showing the far end's color, so the overlay spins with it and draws just above it.
	/// Added to the HUD's teleporting screen, so it only updates while that screen is shown
	/// </remarks>
	public class TeleportSwirl : MonoBehaviour
	{
		/// <summary>
		/// Seconds into the teleport the center starts fading, when the game moves the player to the far end
		/// </summary>
		private const float BlendStart = 2f;

		/// <summary>
		/// Seconds between one ring starting to fade and the next ring out
		/// </summary>
		private const float LayerDelay = 0.6f;

		/// <summary>
		/// Seconds each ring takes to fade, so the widest ring finishes before the shortest teleport of 8 seconds ends
		/// </summary>
		private const float LayerFade = 1.5f;

		/// <summary>
		/// One image in the swirl with its overlay and its own sprite and color
		/// </summary>
		private class Layer
		{
			public Image Image;
			public Image Overlay;
			public Sprite Original;
			public Color Color;
		}

		/// <summary>
		/// The swirl's images, from the center outward
		/// </summary>
		private readonly List<Layer> layers = new List<Layer>();

		/// <summary>
		/// The color of the portal entered, or null for the swirl's own colors
		/// </summary>
		private Color? from;

		/// <summary>
		/// The color of the portal at the far end, once read, or null for the swirl's own colors
		/// </summary>
		private Color? to;

		/// <summary>
		/// The portal at the far end, read when the fade starts so a copy requested when the teleport started has time to arrive
		/// </summary>
		private ZDOID target;

		/// <summary>
		/// Whether the far end's color has been read and set on the overlays
		/// </summary>
		private bool targetRead;

		/// <summary>
		/// When the teleport started
		/// </summary>
		private float startTime;

		/// <summary>
		/// Starts the swirl in the entry portal's color, to fade to the far end's color
		/// </summary>
		/// <param name="from">The color of the portal entered, or null for the swirl's own colors</param>
		/// <param name="target">The portal at the far end, or ZDOID.None to keep the entry color</param>
		public static void Begin(Color? from, ZDOID target)
		{
			Hud hud = Hud.instance;
			if (hud == null || hud.m_teleportingProgress == null)
			{
				return;
			}
			TeleportSwirl swirl = hud.m_teleportingProgress.GetComponent<TeleportSwirl>();
			if (swirl == null)
			{
				swirl = hud.m_teleportingProgress.AddComponent<TeleportSwirl>();
				swirl.FindLayers();
			}
			swirl.from = from;
			swirl.to = from;
			swirl.target = target;
			swirl.targetRead = (target == ZDOID.None);
			swirl.startTime = Time.time;
			foreach (Layer layer in swirl.layers)
			{
				layer.Image.sprite = GetSprite(layer, from);
				layer.Image.color = layer.Color;
				layer.Overlay.gameObject.SetActive(false);
			}
			swirl.UpdateFade();
		}

		/// <summary>
		/// Records the swirl's images in drawing order and gives each an overlay
		/// </summary>
		/// <remarks>
		/// The images sit under a child named Swirl, falling back to every image on the teleporting screen
		/// </remarks>
		private void FindLayers()
		{
			Transform root = transform.Find("Swirl");
			if (root == null)
			{
				root = transform;
			}
			foreach (Image image in root.GetComponentsInChildren<Image>(true))
			{
				if (image.sprite == null)
				{
					continue;
				}
				layers.Add(new Layer { Image = image, Overlay = CreateOverlay(image), Original = image.sprite, Color = image.color });
			}
		}

		/// <summary>
		/// Adds an image covering another image as its child, hidden until the fade needs it
		/// </summary>
		/// <param name="image">The image to cover</param>
		/// <returns>The overlay image</returns>
		private static Image CreateOverlay(Image image)
		{
			GameObject overlay = new GameObject(Plugin.PluginName + " overlay", typeof(RectTransform));
			overlay.layer = image.gameObject.layer;
			overlay.SetActive(false);
			RectTransform rect = (RectTransform)overlay.transform;
			rect.SetParent(image.transform, false);
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			Image copy = overlay.AddComponent<Image>();
			copy.type = image.type;
			copy.preserveAspect = image.preserveAspect;
			copy.raycastTarget = false;
			return(copy);
		}

		/// <summary>
		/// A layer's sprite in a color
		/// </summary>
		/// <param name="layer">The layer</param>
		/// <param name="color">The color to shift the sprite toward, or null for its own colors</param>
		/// <returns>The recolored copy, or the layer's own sprite</returns>
		private static Sprite GetSprite(Layer layer, Color? color)
		{
			return((color.HasValue ? Recolor.GetSprite(layer.Original, color.Value) : layer.Original));
		}

		/// <summary>
		/// Whether two colors would draw the swirl the same way
		/// </summary>
		/// <param name="a">A color, or null for the swirl's own colors</param>
		/// <param name="b">Another color, or null for the swirl's own colors</param>
		/// <returns>True when both are null or both are the same color</returns>
		private static bool SameColor(Color? a, Color? b)
		{
			if (a.HasValue != b.HasValue)
			{
				return(false);
			}
			return(!a.HasValue || a.Value == b.Value);
		}

		/// <summary>
		/// Advances the fade while the teleporting screen is shown
		/// </summary>
		private void Update()
		{
			UpdateFade();
		}

		/// <summary>
		/// Reads the far end's color once the fade starts, then fades each ring from the entry color to it
		/// </summary>
		/// <remarks>
		/// An image with soft edges drawn over a copy of itself looks more solid than either, so the image underneath
		/// fades out as its overlay fades in - slowly at first, so the ring doesn't look thinner partway through, and
		/// fully by the end, so the ring finishes looking exactly like the far end's sprite
		/// </remarks>
		private void UpdateFade()
		{
			float elapsed = Time.time - startTime;
			if (!targetRead)
			{
				if (elapsed < BlendStart)
				{
					return;
				}
				targetRead = true;
				// A far end that never arrived keeps the entry color
				ZDO zdo = ZDOMan.instance.GetZDO(target);
				if (zdo != null)
				{
					LightSettings settings = LightTint.GetStoredSettings(zdo);
					to = (settings.HasColor ? settings.Color : (Color?)null);
				}
				if (!SameColor(from, to))
				{
					foreach (Layer layer in layers)
					{
						layer.Overlay.sprite = GetSprite(layer, to);
						layer.Overlay.gameObject.SetActive(true);
					}
				}
			}
			if (SameColor(from, to))
			{
				return;
			}
			for (int i = 0; i < layers.Count; i++)
			{
				Layer layer = layers[i];
				float t = Mathf.SmoothStep(0f, 1f, (elapsed - BlendStart - i * LayerDelay) / LayerFade);
				Color under = layer.Color;
				under.a *= 1f - t * t * t * t;
				layer.Image.color = under;
				Color over = layer.Color;
				over.a *= t;
				layer.Overlay.color = over;
			}
		}
	}
}
