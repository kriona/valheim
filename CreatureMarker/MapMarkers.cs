using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CreatureMarker
{
	/// <summary>
	/// Colored dots on the minimap and the large map for marked creatures
	/// </summary>
	/// <remarks>
	/// The dots live under each map's pin root, so they move with the map and hide along with it when it is closed
	/// </remarks>
	public class MapMarkers
	{
		private const float SmallDotSize = 10f;
		private const float LargeDotSize = 15f;

		/// <summary>
		/// The pooled dots on one of the two maps
		/// </summary>
		private class DotLayer
		{
			public readonly List<Image> dots = new List<Image>();
			public readonly float size;
			public RectTransform root;

			/// <summary>
			/// Creates an empty layer
			/// </summary>
			/// <param name="size">Width and height of the layer's dots</param>
			public DotLayer(float size)
			{
				this.size = size;
			}
		}

		private readonly DotLayer small = new DotLayer(SmallDotSize);
		private readonly DotLayer large = new DotLayer(LargeDotSize);
		private Sprite sprite;

		/// <summary>
		/// Shows a dot for each marked creature inside the open map's view and hides the spare ones, showing none
		/// while the player is in a dungeon
		/// </summary>
		/// <param name="marked">The living marked creatures within range</param>
		/// <param name="player">The local player, for the dot colors</param>
		public void Update(List<Character> marked, Player player)
		{
			Minimap minimap = Minimap.instance;
			if (minimap == null)
			{
				return;
			}
			// Dungeons sit high above their entrances, so their creatures would show on the map around the entrance
			bool show = (CreatureConfig.ShowOnMap && !player.InInterior());
			UpdateLayer(small, minimap.m_pinRootSmall, minimap.m_mapImageSmall, (show && minimap.m_mode == Minimap.MapMode.Small), minimap, marked, player);
			UpdateLayer(large, minimap.m_pinRootLarge, minimap.m_mapImageLarge, (show && minimap.m_mode == Minimap.MapMode.Large), minimap, marked, player);
		}

		/// <summary>
		/// Removes the dots and the generated sprite
		/// </summary>
		public void Destroy()
		{
			DestroyLayer(small);
			DestroyLayer(large);
			if (sprite != null)
			{
				Object.Destroy(sprite.texture);
				Object.Destroy(sprite);
			}
		}

		/// <summary>
		/// Places a dot on one map for each marked creature in its view, or hides all of the layer's dots when the map isn't shown
		/// </summary>
		/// <param name="layer">The map's dots</param>
		/// <param name="root">The map's pin root</param>
		/// <param name="map">The map's image, for its view and size</param>
		/// <param name="show">Whether the map is open and dots are turned on</param>
		/// <param name="minimap">The minimap, for converting world positions</param>
		/// <param name="marked">The living marked creatures within range</param>
		/// <param name="player">The local player, for the dot colors</param>
		private void UpdateLayer(DotLayer layer, RectTransform root, RawImage map, bool show, Minimap minimap, List<Character> marked, Player player)
		{
			if (layer.root != root)
			{
				// The dots were destroyed along with the previous minimap
				layer.root = root;
				layer.dots.Clear();
			}
			int used = 0;
			if (show)
			{
				foreach (Character character in marked)
				{
					Vector3 position = character.transform.position;
					if (!minimap.IsPointVisible(position, map))
					{
						continue;
					}
					Image dot = GetDot(layer, used);
					used++;
					dot.color = CreatureConfig.GetColor(character, player);
					minimap.WorldToMapPoint(position, out float mx, out float my);
					dot.rectTransform.anchoredPosition = minimap.MapPointToLocalGuiPos(mx, my, map);
				}
			}
			for (int i = used; i < layer.dots.Count; i++)
			{
				MarkerHud.SetActive(layer.dots[i].gameObject, false);
			}
		}

		/// <summary>
		/// Destroys a layer's dots
		/// </summary>
		/// <param name="layer">The map's dots</param>
		private static void DestroyLayer(DotLayer layer)
		{
			foreach (Image dot in layer.dots)
			{
				if (dot != null)
				{
					Object.Destroy(dot.gameObject);
				}
			}
			layer.dots.Clear();
		}

		/// <summary>
		/// Returns the layer's pooled dot at the index, creating it if needed, active and drawn above the map's pins
		/// </summary>
		/// <param name="layer">The map's dots</param>
		/// <param name="index">Pool index</param>
		/// <returns>The dot</returns>
		private Image GetDot(DotLayer layer, int index)
		{
			if (index >= layer.dots.Count)
			{
				layer.dots.Add(CreateDot(layer));
			}
			Image dot = layer.dots[index];
			MarkerHud.SetActive(dot.gameObject, true);
			// The map recreates its pins as they scroll into view, which would put them on top of the dots
			dot.transform.SetAsLastSibling();
			return(dot);
		}

		/// <summary>
		/// Creates a dot image under the layer's pin root, anchored to the bottom left like the game's pins
		/// </summary>
		/// <param name="layer">The map's dots</param>
		/// <returns>The dot's image</returns>
		private Image CreateDot(DotLayer layer)
		{
			GameObject go = new GameObject("CreatureMarkerDot", typeof(RectTransform));
			go.transform.SetParent(layer.root, false);
			Image image = go.AddComponent<Image>();
			image.sprite = GetSprite();
			image.raycastTarget = false;
			RectTransform rect = image.rectTransform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.zero;
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(layer.size, layer.size);
			return(image);
		}

		/// <summary>
		/// Draws an outlined circle into a texture the first time it is needed
		/// </summary>
		/// <remarks>
		/// The fill is white so the image color tints it, while the dark outline stays dark under any tint
		/// </remarks>
		/// <returns>The dot sprite</returns>
		private Sprite GetSprite()
		{
			if (sprite != null)
			{
				return(sprite);
			}
			const int size = 32;
			const float outline = 5f;
			float radius = size * 0.5f;
			Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
			texture.wrapMode = TextureWrapMode.Clamp;
			texture.filterMode = FilterMode.Bilinear;
			Color[] pixels = new Color[size * size];
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
					Color color = Color.clear;
					if (distance <= radius - outline)
					{
						color = Color.white;
					}
					else if (distance <= radius)
					{
						color = MarkerHud.OutlineColor;
					}
					pixels[y * size + x] = color;
				}
			}
			texture.SetPixels(pixels);
			texture.Apply();
			sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
			return(sprite);
		}
	}
}
