using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace CreatureMarker
{
	/// <summary>
	/// Hooks into PortalPreview, when it is installed, so traders and pets are marked in its portal views
	/// </summary>
	/// <remarks>
	/// PortalPreview's classes are internal and it may not be installed, so its methods are found by name and patched
	/// by hand rather than through attributes
	/// </remarks>
	internal static class PortalPreviewPatches
	{
		public const string PortalPreviewGuid = "ru.ivest.portalpreview";

		/// <summary>
		/// Patches PortalPreview's capture and view drawing, logging a warning when its methods can't be found
		/// </summary>
		/// <param name="harmony">The plugin's Harmony instance</param>
		public static void Apply(Harmony harmony)
		{
			if (!Chainloader.PluginInfos.ContainsKey(PortalPreviewGuid))
			{
				return;
			}
			if (!PortalCaptureStepsPatch.Apply(harmony) || !PortalViewRenderPatch.Apply(harmony))
			{
				Debug.LogWarning("[" + Plugin.PluginName + "] Couldn't hook into PortalPreview, so traders and pets won't be marked in its portal views");
			}
		}
	}

	/// <summary>
	/// Records the traders and pets around a portal as PortalPreview starts capturing its surroundings
	/// </summary>
	internal static class PortalCaptureStepsPatch
	{
		/// <summary>
		/// Patches PortalSnapshot.CaptureSteps
		/// </summary>
		/// <param name="harmony">The plugin's Harmony instance</param>
		/// <returns>Whether the method was found and patched</returns>
		public static bool Apply(Harmony harmony)
		{
			MethodInfo method = AccessTools.Method("PortalPreview.PortalSnapshot:CaptureSteps");
			if (method == null)
			{
				return(false);
			}
			harmony.Patch(method, prefix: new HarmonyMethod(typeof(PortalCaptureStepsPatch), nameof(Prefix)));
			return(true);
		}

		/// <summary>
		/// Records the traders and pets around the portal
		/// </summary>
		/// <param name="position">The captured portal's position</param>
		/// <param name="rotation">The captured portal's rotation</param>
		private static void Prefix(Vector3 position, Quaternion rotation)
		{
			PortalNpcs.Record(position, rotation);
		}
	}

	/// <summary>
	/// Notes each portal PortalPreview draws a view in, with the portal the view shows and the opening's shape
	/// </summary>
	internal static class PortalViewRenderPatch
	{
		private static FieldInfo shapeField;
		private static FieldInfo destinationField;
		private static FieldInfo outlineField;
		private static FieldInfo centerHeightField;
		private static FieldInfo frontPlaneField;
		private static FieldInfo backPlaneField;

		/// <summary>
		/// Finds the fields the patch reads and patches PortalView.Render
		/// </summary>
		/// <param name="harmony">The plugin's Harmony instance</param>
		/// <returns>Whether the method and fields were found and the method patched</returns>
		public static bool Apply(Harmony harmony)
		{
			MethodInfo method = AccessTools.Method("PortalPreview.PortalView:Render");
			shapeField = AccessTools.Field(AccessTools.TypeByName("PortalPreview.PortalView"), "_shape");
			destinationField = AccessTools.Field(AccessTools.TypeByName("PortalPreview.PortalSnapshot"), "Position");
			System.Type shapeType = AccessTools.TypeByName("PortalPreview.PortalShape");
			outlineField = AccessTools.Field(shapeType, "Outline");
			centerHeightField = AccessTools.Field(shapeType, "CenterHeight");
			frontPlaneField = AccessTools.Field(shapeType, "FrontPlane");
			backPlaneField = AccessTools.Field(shapeType, "BackPlane");
			if (method == null || shapeField == null || destinationField == null || outlineField == null || centerHeightField == null || frontPlaneField == null || backPlaneField == null)
			{
				return(false);
			}
			harmony.Patch(method, postfix: new HarmonyMethod(typeof(PortalViewRenderPatch), nameof(Postfix)));
			return(true);
		}

		/// <summary>
		/// Notes the portal when the view was drawn
		/// </summary>
		/// <param name="__instance">The PortalView</param>
		/// <param name="portal">The portal the view is drawn in</param>
		/// <param name="snapshot">The PortalSnapshot of the destination's surroundings</param>
		/// <param name="__result">Whether the view was drawn</param>
		private static void Postfix(object __instance, Transform portal, object snapshot, bool __result)
		{
			if (!__result || portal == null || snapshot == null)
			{
				return;
			}
			// The front side's shape - PortalPreview mirrors its outline for the back, which comes out the same in the world
			object shape = shapeField.GetValue(__instance);
			Vector2[] outline = ((shape != null) ? (Vector2[])outlineField.GetValue(shape) : null);
			if (outline == null || outline.Length < 3)
			{
				return;
			}
			Vector3 destination = (Vector3)destinationField.GetValue(snapshot);
			PortalNpcs.Shown(portal, destination, outline, (float)centerHeightField.GetValue(shape), (float)frontPlaneField.GetValue(shape), (float)backPlaneField.GetValue(shape));
		}
	}
}
