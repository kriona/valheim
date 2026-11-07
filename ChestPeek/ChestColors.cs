using System.Collections.Generic;
using UnityEngine;

namespace ChestPeek
{
	/// <summary>
	/// Recolors the metal fittings of an empty or completely full chest - the wood chest's rope rings and front circle, and on the
	/// other chests the parts their metallic mask marks as metal
	/// </summary>
	/// <remarks>
	/// Each chest material gets a copy whose main texture is tinted where the fittings are, shared by every chest
	/// in the same state, and the chest's renderers are switched between the copy and the original. Tinting the
	/// texture rather than the material's _MetalColor keeps the result independent of how the shader uses it, and
	/// swapping whole materials leaves the per-object property blocks that MaterialMan sets for damage highlights
	/// and snow untouched
	/// </remarks>
	internal static class ChestColors
	{
		private enum Fill
		{
			Partial,
			Empty,
			Full
		}

		/// <summary>
		/// The chests that are recolored, by prefab name
		/// </summary>
		private static readonly HashSet<string> Chests = new HashSet<string>
		{
			"piece_chest_wood",
			"piece_chest",
			"piece_chest_private",
			"piece_chest_barrel",
			"piece_chest_blackmetal",
			"piece_chest_grausten",
			"piece_chest_warderobe"
		};

		/// <summary>
		/// Texture areas to tint for materials without a metallic mask, as polygons in UV coordinates - the wood
		/// chest's rope rings are mapped onto the pale patch at the top of its texture, which nothing else uses, and
		/// the circle on its front onto the outline of its own faces, with the front panel's faces right around it
		/// </summary>
		private static readonly Dictionary<string, Vector2[][]> Regions = new Dictionary<string, Vector2[][]>
		{
			{
				"woodchest", new Vector2[][]
				{
					new Vector2[] { new Vector2(0.46f, 0.77f), new Vector2(0.76f, 0.77f), new Vector2(0.76f, 1f), new Vector2(0.46f, 1f) },
					new Vector2[] { new Vector2(0.0095f, 0.6895f), new Vector2(0.0116f, 0.6054f), new Vector2(0.012f, 0.5919f), new Vector2(0.0138f, 0.5888f), new Vector2(0.0491f, 0.599f), new Vector2(0.0692f, 0.6237f), new Vector2(0.0689f, 0.6613f), new Vector2(0.045f, 0.6858f), new Vector2(0.0113f, 0.6931f) }
				}
			}
		};

		/// <summary>
		/// How many times larger than the original a texture tinted by Regions is made, so the tint's edge follows
		/// the polygon closely instead of spreading a whole texel onto the faces around it
		/// </summary>
		private const int RegionScale = 4;

		/// <summary>
		/// Mask values below this aren't tinted, and values above MaskFull are tinted completely
		/// </summary>
		private const float MaskStart = 0.25f;
		private const float MaskFull = 0.5f;

		/// <summary>
		/// How bright the average tinted pixel is relative to the chosen color, with the texture's detail kept around it
		/// </summary>
		private const float TintBrightness = 0.75f;

		private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
		private static readonly int MetallicTexId = Shader.PropertyToID("_MetallicTex");
		private static readonly int MetalColorId = Shader.PropertyToID("_MetalColor");

		/// <summary>
		/// The tinted copy of each material for an empty and a full chest, or null when the material has nothing to tint
		/// </summary>
		private static readonly Dictionary<Material, Material> emptyMaterials = new Dictionary<Material, Material>();
		private static readonly Dictionary<Material, Material> fullMaterials = new Dictionary<Material, Material>();

		/// <summary>
		/// The original material of each tinted copy
		/// </summary>
		private static readonly Dictionary<Material, Material> originals = new Dictionary<Material, Material>();

		private static Color? emptyColor;
		private static Color? fullColor;

		/// <summary>
		/// Chests whose contents changed since the last Update
		/// </summary>
		/// <remarks>
		/// A chest is recolored in the next Update rather than as its contents change, since the inventory isn't
		/// settled yet at that point - its change event fires partway through loading, and other mods can patch the
		/// load too, like TweakStats widening the inventory while it loads and narrowing it again afterwards
		/// </remarks>
		private static readonly HashSet<Container> changed = new HashSet<Container>();

		/// <summary>
		/// Recolors a chest each time its contents change, which includes loading them when it first appears and
		/// whenever another player changes them
		/// </summary>
		/// <param name="container">A container that has just woken up</param>
		public static void Track(Container container)
		{
			if (container.m_inventory == null || !IsChest(container))
			{
				return;
			}
			container.m_inventory.m_onChanged += () => changed.Add(container);
		}

		/// <summary>
		/// Recolors a chest after it has read its saved contents again
		/// </summary>
		/// <remarks>
		/// A chest with nothing saved yet, like one that has just been placed, skips loading its inventory, so the
		/// inventory's change event Track listens for never fires for it
		/// </remarks>
		/// <param name="container">A container whose saved contents were just read</param>
		public static void Loaded(Container container)
		{
			if (container.m_inventory != null && IsChest(container))
			{
				changed.Add(container);
			}
		}

		/// <summary>
		/// Recolors the chests whose contents changed since the last call
		/// </summary>
		public static void Update()
		{
			if (changed.Count == 0)
			{
				return;
			}
			foreach (Container container in changed)
			{
				Apply(container);
			}
			changed.Clear();
		}

		/// <summary>
		/// Reads the colors from the settings again and recolors every chest, replacing the tinted materials made
		/// with the old colors
		/// </summary>
		public static void Refresh()
		{
			emptyColor = ParseColor(Plugin.EmptyColor.Value, Plugin.EmptyColor.Definition.Key);
			fullColor = ParseColor(Plugin.FullColor.Value, Plugin.FullColor.Definition.Key);
			List<Material> oldCopies = new List<Material>(originals.Keys);
			emptyMaterials.Clear();
			fullMaterials.Clear();
			foreach (Container container in Object.FindObjectsByType<Container>(FindObjectsSortMode.None))
			{
				if (container.m_inventory != null && IsChest(container))
				{
					Apply(container);
				}
			}
			foreach (Material copy in oldCopies)
			{
				originals.Remove(copy);
				Object.Destroy(copy.mainTexture);
				Object.Destroy(copy);
			}
		}

		/// <summary>
		/// Whether a container is one of the chests that are recolored, rather than a cart, ship or another mod's container
		/// </summary>
		/// <param name="container">The container to check</param>
		/// <returns>True for a recolored chest</returns>
		private static bool IsChest(Container container)
		{
			return(container.m_rootObjectOverride == null && Chests.Contains(Utils.GetPrefabName(container.gameObject)));
		}

		/// <summary>
		/// Gives a chest's renderers the materials for how full it is
		/// </summary>
		/// <param name="container">The chest to recolor</param>
		private static void Apply(Container container)
		{
			if (container == null)
			{
				return;
			}
			Fill fill = GetFill(container.m_inventory);
			Color? color = ((fill == Fill.Empty) ? emptyColor : ((fill == Fill.Full) ? fullColor : null));
			foreach (Renderer renderer in container.GetComponentsInChildren<Renderer>(true))
			{
				Material[] materials = renderer.sharedMaterials;
				bool changed = false;
				for (int i = 0; i < materials.Length; i++)
				{
					if (materials[i] == null)
					{
						continue;
					}
					Material original = (originals.TryGetValue(materials[i], out Material found) ? found : materials[i]);
					Material wanted = original;
					if (color.HasValue)
					{
						wanted = GetTinted(original, (fill == Fill.Empty) ? emptyMaterials : fullMaterials, color.Value) ?? original;
					}
					if (wanted != materials[i])
					{
						materials[i] = wanted;
						changed = true;
					}
				}
				if (changed)
				{
					renderer.sharedMaterials = materials;
				}
			}
		}

		/// <summary>
		/// Whether an inventory holds nothing, has every slot filled with a full stack, or is in between
		/// </summary>
		/// <param name="inventory">The chest's inventory</param>
		/// <returns>How full the inventory is</returns>
		private static Fill GetFill(Inventory inventory)
		{
			if (inventory.NrOfItems() == 0)
			{
				return(Fill.Empty);
			}
			if (inventory.GetEmptySlots() > 0)
			{
				return(Fill.Partial);
			}
			foreach (ItemDrop.ItemData item in inventory.GetAllItems())
			{
				if (item.m_stack < item.m_shared.m_maxStackSize)
				{
					return(Fill.Partial);
				}
			}
			return(Fill.Full);
		}

		/// <summary>
		/// Reads a color setting, logging a warning when it can't be read
		/// </summary>
		/// <param name="text">A hex color like #FF4040, a color name, or blank</param>
		/// <param name="setting">The setting's name for the warning</param>
		/// <returns>The color, or null when the setting is blank or can't be read</returns>
		private static Color? ParseColor(string text, string setting)
		{
			text = (text ?? "").Trim();
			if (text.Length == 0)
			{
				return(null);
			}
			if (!text.StartsWith("#") && text.Length == 6 && ColorUtility.TryParseHtmlString("#" + text, out Color hex))
			{
				return(hex);
			}
			if (ColorUtility.TryParseHtmlString(text, out Color color))
			{
				return(color);
			}
			Plugin.Log.LogWarning(setting + " \"" + text + "\" isn't a color - use a hex color like #FF4040 or a name like red, or leave it blank");
			return(null);
		}

		/// <summary>
		/// The tinted copy of a material for one fill state, made the first time it's needed
		/// </summary>
		/// <param name="original">The chest's own material</param>
		/// <param name="cache">The copies made for this fill state</param>
		/// <param name="color">The color for this fill state</param>
		/// <returns>The tinted copy, or null when the material has nothing to tint</returns>
		private static Material GetTinted(Material original, Dictionary<Material, Material> cache, Color color)
		{
			if (cache.TryGetValue(original, out Material tinted))
			{
				return(tinted);
			}
			tinted = MakeTinted(original, color);
			cache[original] = tinted;
			if (tinted != null)
			{
				originals[tinted] = original;
			}
			return(tinted);
		}

		/// <summary>
		/// Copies a material with its main texture tinted where its fittings are
		/// </summary>
		/// <remarks>
		/// The copy's _MetalColor is white - the blackmetal chest's is a green tint that would otherwise be multiplied
		/// into the chosen color
		/// </remarks>
		/// <param name="original">The chest's own material</param>
		/// <param name="color">The color to tint the fittings</param>
		/// <returns>The tinted copy, or null when the material has no main texture or nothing marked to tint</returns>
		private static Material MakeTinted(Material original, Color color)
		{
			if (!original.HasProperty(MainTexId) || !(original.GetTexture(MainTexId) is Texture2D main))
			{
				return(null);
			}
			int scale = (Regions.ContainsKey(original.name) ? RegionScale : 1);
			int width = main.width * scale;
			int height = main.height * scale;
			float[] weights = GetWeights(original, width, height);
			if (weights == null)
			{
				return(null);
			}
			Color32[] pixels = ReadPixels(main, width, height, false);
			float total = 0f;
			float weightedLuminance = 0f;
			for (int i = 0; i < pixels.Length; i++)
			{
				total += weights[i];
				weightedLuminance += Luminance(pixels[i]) * weights[i];
			}
			if (total < 1f)
			{
				return(null);
			}
			float average = Mathf.Max(weightedLuminance / total, 0.01f);
			for (int i = 0; i < pixels.Length; i++)
			{
				if (weights[i] <= 0f)
				{
					continue;
				}
				float brightness = Mathf.Min(1f, Luminance(pixels[i]) / average * TintBrightness);
				Color tint = color * brightness;
				tint.a = pixels[i].a / 255f;
				pixels[i] = Color.Lerp(pixels[i], tint, weights[i]);
			}
			Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, true, false);
			texture.name = main.name + " (" + Plugin.PluginName + ")";
			texture.filterMode = main.filterMode;
			texture.wrapMode = main.wrapMode;
			texture.anisoLevel = main.anisoLevel;
			texture.SetPixels32(pixels);
			texture.Apply(true, true);
			Material tinted = new Material(original);
			tinted.name = original.name + " (" + Plugin.PluginName + ")";
			tinted.SetTexture(MainTexId, texture);
			if (tinted.HasProperty(MetalColorId))
			{
				tinted.SetColor(MetalColorId, Color.white);
			}
			return(tinted);
		}

		/// <summary>
		/// How much to tint each pixel of a material's main texture, from its texture area for a material listed in
		/// Regions, otherwise from its metallic mask
		/// </summary>
		/// <param name="material">The chest's own material</param>
		/// <param name="width">Width of the tinted texture</param>
		/// <param name="height">Height of the tinted texture</param>
		/// <returns>A weight from 0 to 1 per pixel, bottom row first, or null when there's nothing to tint</returns>
		private static float[] GetWeights(Material material, int width, int height)
		{
			float[] weights = new float[width * height];
			if (Regions.TryGetValue(material.name, out Vector2[][] polygons))
			{
				for (int y = 0; y < height; y++)
				{
					for (int x = 0; x < width; x++)
					{
						Vector2 uv = new Vector2((x + 0.5f) / width, (y + 0.5f) / height);
						foreach (Vector2[] polygon in polygons)
						{
							if (Contains(polygon, uv))
							{
								weights[y * width + x] = 1f;
								break;
							}
						}
					}
				}
				return(weights);
			}
			if (!material.HasProperty(MetallicTexId))
			{
				return(null);
			}
			Texture mask = material.GetTexture(MetallicTexId);
			if (mask == null)
			{
				return(null);
			}
			Color32[] maskPixels = ReadPixels(mask, width, height, true);
			for (int i = 0; i < maskPixels.Length; i++)
			{
				weights[i] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(MaskStart, MaskFull, maskPixels[i].r / 255f));
			}
			return(weights);
		}

		/// <summary>
		/// Whether a point is inside a polygon, by counting how many of its edges a line to the right of the point crosses
		/// </summary>
		/// <param name="polygon">The polygon's corners in order</param>
		/// <param name="point">The point to check</param>
		/// <returns>True when the point is inside</returns>
		private static bool Contains(Vector2[] polygon, Vector2 point)
		{
			bool inside = false;
			for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
			{
				Vector2 a = polygon[i];
				Vector2 b = polygon[j];
				if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
				{
					inside = !inside;
				}
			}
			return(inside);
		}

		/// <summary>
		/// Reads a texture's pixels through a render texture, since the game's textures can't be read directly
		/// </summary>
		/// <param name="source">The texture to read</param>
		/// <param name="width">Width to read it at</param>
		/// <param name="height">Height to read it at</param>
		/// <param name="linear">True for data like a mask, false for a color texture</param>
		/// <returns>The pixels, bottom row first</returns>
		private static Color32[] ReadPixels(Texture source, int width, int height, bool linear)
		{
			RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, (linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB));
			RenderTexture previous = RenderTexture.active;
			Graphics.Blit(source, target);
			RenderTexture.active = target;
			Texture2D copy = new Texture2D(width, height, TextureFormat.RGBA32, false, linear);
			copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
			RenderTexture.active = previous;
			RenderTexture.ReleaseTemporary(target);
			Color32[] pixels = copy.GetPixels32();
			Object.Destroy(copy);
			return(pixels);
		}

		/// <summary>
		/// The perceived brightness of a pixel
		/// </summary>
		/// <param name="pixel">The pixel</param>
		/// <returns>Brightness from 0 to 1</returns>
		private static float Luminance(Color32 pixel)
		{
			return((0.299f * pixel.r + 0.587f * pixel.g + 0.114f * pixel.b) / 255f);
		}
	}
}
