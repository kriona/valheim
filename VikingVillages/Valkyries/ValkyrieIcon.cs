using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A valkyrie's icon for the mount panel - a portrait of her head and shoulders, taken once from a copy of her own
	/// model the first time it's needed, since the game has no picture of her
	/// </summary>
	/// <remarks>
	/// The copy is lit and seen alone, on a layer nothing else in the world is on, by a camera of its own, far from
	/// anything, and both are gone once it's taken. The world's ambient light and fog are set aside while it's taken, so
	/// it looks the same by day or night, and put back after
	/// </remarks>
	internal static class ValkyrieIcon
	{
		/// <summary>
		/// How many pixels wide and high the icon is
		/// </summary>
		private const int Pixels = 256;

		/// <summary>
		/// Where the copy stands while it's taken, far below anything in the world
		/// </summary>
		private static readonly Vector3 Studio = new Vector3(0f, -5000f, 0f);

		/// <summary>
		/// How the portrait's framed, as shares of the way from her head down to her pelvis - its middle that far down
		/// from her head, and half its height that long - and how far around from in front of her and down onto her it's
		/// seen from, in degrees
		/// </summary>
		internal const float FrameCenter = 0.3f;
		internal const float FrameHalf = 0.6f;
		internal const float Yaw = 25f;
		internal const float Pitch = 10f;

		private static Sprite icon;
		private static bool tried;

		/// <summary>
		/// Gets her icon, taking it the first time
		/// </summary>
		/// <returns>The icon, or null when it couldn't be taken</returns>
		public static Sprite Get()
		{
			if (!tried)
			{
				tried = true;
				try
				{
					icon = Take();
				}
				catch (System.Exception e)
				{
					Plugin.Log.LogWarning("Valkyrie: couldn't take her icon - " + e.Message);
				}
			}
			return(icon);
		}

		/// <summary>
		/// Takes her portrait from a copy of her prefab's model
		/// </summary>
		/// <returns>The icon, or null when there's no model to take it from</returns>
		private static Sprite Take()
		{
			GameObject prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(ValkyriePrefab.PrefabName) : null);
			ValkyrieModel source = ((prefab != null) ? prefab.GetComponentInChildren<ValkyrieModel>(true) : null);
			int layer = FreeLayer();
			if (source == null || layer < 0)
			{
				return(null);
			}
			// Copied under an inactive holder, so none of its parts wake before they're taken off
			GameObject holder = new GameObject("ValkyrieIconHolder");
			holder.SetActive(false);
			GameObject model = Object.Instantiate(source.gameObject, holder.transform, false);
			GameObject cameraObject = null;
			GameObject lightObject = null;
			Color ambient = RenderSettings.ambientLight;
			UnityEngine.Rendering.AmbientMode ambientMode = RenderSettings.ambientMode;
			bool fog = RenderSettings.fog;
			RenderTexture target = null;
			RenderTexture active = RenderTexture.active;
			try
			{
				Strip(model);
				model.transform.SetParent(null, false);
				model.transform.position = Studio;
				model.transform.rotation = Quaternion.identity;
				model.transform.localScale = Vector3.one;
				foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
				{
					part.gameObject.layer = layer;
				}
				model.SetActive(true);
				Transform head = Utils.FindChild(model.transform, "head");
				Transform pelvis = Utils.FindChild(model.transform, "pelvis");
				if (head == null || pelvis == null)
				{
					return(null);
				}
				Vector3 center = Vector3.Lerp(head.position, pelvis.position, FrameCenter);
				float half = Vector3.Distance(head.position, pelvis.position) * FrameHalf;
				// Her model faces back along her body's forward, so she faces the world's back here
				Vector3 toward = Quaternion.Euler(Pitch, Yaw, 0f) * Vector3.back;
				cameraObject = new GameObject("ValkyrieIconCamera");
				Camera camera = cameraObject.AddComponent<Camera>();
				camera.enabled = false;
				camera.orthographic = true;
				camera.orthographicSize = half;
				camera.aspect = 1f;
				camera.clearFlags = CameraClearFlags.SolidColor;
				camera.cullingMask = 1 << layer;
				camera.renderingPath = RenderingPath.Forward;
				camera.allowHDR = false;
				camera.allowMSAA = false;
				camera.nearClipPlane = 0.1f;
				camera.farClipPlane = 200f;
				camera.transform.position = center + toward * 50f;
				camera.transform.rotation = Quaternion.LookRotation(-toward, Vector3.up);
				lightObject = new GameObject("ValkyrieIconLight");
				Light light = lightObject.AddComponent<Light>();
				light.type = LightType.Directional;
				light.intensity = 1.2f;
				light.color = Color.white;
				light.shadows = LightShadows.None;
				light.cullingMask = 1 << layer;
				lightObject.transform.rotation = Quaternion.LookRotation(Quaternion.Euler(35f, Yaw - 30f, 0f) * -Vector3.back, Vector3.up);
				RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
				RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f, 1f);
				RenderSettings.fog = false;
				target = RenderTexture.GetTemporary(Pixels, Pixels, 24, RenderTextureFormat.ARGB32);
				camera.targetTexture = target;
				// Taken over black and over white - what changes between them is the background, and how much shows how
				// see-through each pixel is, since the game's creature shaders don't write it themselves
				Color[] overBlack = Render(camera, target, Color.black);
				Color[] overWhite = Render(camera, target, Color.white);
				Color[] pixels = new Color[overBlack.Length];
				int drawn = 0;
				for (int i = 0; i < pixels.Length; i++)
				{
					pixels[i] = Unmatte(overBlack[i], overWhite[i]);
					if (pixels[i].a > 0.1f)
					{
						drawn++;
					}
				}
				Plugin.Log.LogInfo("Valkyrie: her icon has " + drawn + " of " + pixels.Length + " pixels drawn, framed " + (half * 2f).ToString("0.00") + " m high around " + (center - Studio).ToString("F2") + " from her feet");
				if (drawn == 0)
				{
					return(null);
				}
				Texture2D texture = new Texture2D(Pixels, Pixels, TextureFormat.RGBA32, false);
				texture.SetPixels(pixels);
				texture.Apply();
				texture.name = "ValkyrieIcon";
				Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Pixels, Pixels), new Vector2(0.5f, 0.5f), 100f);
				sprite.name = "ValkyrieIcon";
				return(sprite);
			}
			finally
			{
				RenderTexture.active = active;
				RenderSettings.ambientMode = ambientMode;
				RenderSettings.ambientLight = ambient;
				RenderSettings.fog = fog;
				if (target != null)
				{
					RenderTexture.ReleaseTemporary(target);
				}
				Object.Destroy(model);
				Object.Destroy(holder);
				if (cameraObject != null)
				{
					Object.Destroy(cameraObject);
				}
				if (lightObject != null)
				{
					Object.Destroy(lightObject);
				}
			}
		}

		/// <summary>
		/// Renders the camera's view over a plain background and reads it back
		/// </summary>
		/// <param name="camera">The camera, drawing into target</param>
		/// <param name="target">What it draws into</param>
		/// <param name="background">The background's color</param>
		/// <returns>The pixels, row by row from the bottom</returns>
		private static Color[] Render(Camera camera, RenderTexture target, Color background)
		{
			camera.backgroundColor = background;
			camera.Render();
			RenderTexture.active = target;
			Texture2D read = new Texture2D(Pixels, Pixels, TextureFormat.RGBA32, false);
			read.ReadPixels(new Rect(0f, 0f, Pixels, Pixels), 0, 0);
			read.Apply();
			Color[] pixels = read.GetPixels();
			Object.Destroy(read);
			return(pixels);
		}

		/// <summary>
		/// Works out a pixel's own color and how see-through it is from how it looks over black and over white - as much
		/// of the background as shows through it changes between them, and none of it is solid
		/// </summary>
		/// <param name="overBlack">The pixel over black</param>
		/// <param name="overWhite">The pixel over white</param>
		/// <returns>Its color, with how solid it is as alpha</returns>
		internal static Color Unmatte(Color overBlack, Color overWhite)
		{
			float through = ((overWhite.r - overBlack.r) + (overWhite.g - overBlack.g) + (overWhite.b - overBlack.b)) / 3f;
			float alpha = Mathf.Clamp01(1f - through);
			if (alpha <= 0.001f)
			{
				return(new Color(0f, 0f, 0f, 0f));
			}
			// Over black, what shows is its own color times how solid it is
			return(new Color(Mathf.Clamp01(overBlack.r / alpha), Mathf.Clamp01(overBlack.g / alpha), Mathf.Clamp01(overBlack.b / alpha), alpha));
		}

		/// <summary>
		/// Takes everything off a copy of her model but what draws it - her own parts, the game's, and particles - so
		/// nothing in it runs
		/// </summary>
		/// <param name="model">The copy</param>
		private static void Strip(GameObject model)
		{
			foreach (MonoBehaviour behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))
			{
				Object.DestroyImmediate(behaviour);
			}
			foreach (Cloth cloth in model.GetComponentsInChildren<Cloth>(true))
			{
				Object.DestroyImmediate(cloth);
			}
			foreach (ParticleSystem particles in model.GetComponentsInChildren<ParticleSystem>(true))
			{
				Object.DestroyImmediate(particles.gameObject);
			}
			foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
			{
				skin.updateWhenOffscreen = true;
			}
		}

		/// <summary>
		/// Finds a layer nothing in the game uses, for her copy alone to be on
		/// </summary>
		/// <returns>The layer, or -1 when there's none</returns>
		private static int FreeLayer()
		{
			for (int layer = 31; layer >= 8; layer--)
			{
				if (string.IsNullOrEmpty(LayerMask.LayerToName(layer)))
				{
					return(layer);
				}
			}
			return(-1);
		}
	}
}
