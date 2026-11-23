using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;

namespace Kriona.Vikings
{
	/// <summary>
	/// Reaches the other mod of the pair - Zombie Apocalypse from Viking Villages, and Viking Villages from Zombie
	/// Apocalypse - through the public static methods of its Link class, so neither needs the other installed (see
	/// ZombieLink and VillageLink)
	/// </summary>
	/// <remarks>
	/// Each method is found by its name and parameter types the first time it's asked for, and one the other mod doesn't
	/// have - because it isn't installed, or because it's an older version than the call was written for - gives the
	/// caller's fallback instead, so any versions of the two mods work together. A new version can add Link methods, but
	/// never remove one or change its name, parameters or return type, since older versions of the other mod still call
	/// it
	/// </remarks>
	internal class ModLink
	{
		private readonly string guid;
		private readonly string typeName;

		/// <summary>
		/// Whether the other mod has been found loaded, and its Link class, or null for a version without one
		/// </summary>
		private bool loaded;
		private Type link;

		/// <summary>
		/// The Link methods asked for so far, by name, each null when the other mod has no such method
		/// </summary>
		private readonly Dictionary<string, Delegate> bound = new Dictionary<string, Delegate>();

		/// <summary>
		/// Sets up the link to a mod
		/// </summary>
		/// <param name="guid">The mod's GUID</param>
		/// <param name="typeName">The full name of its Link class</param>
		public ModLink(string guid, string typeName)
		{
			this.guid = guid;
			this.typeName = typeName;
		}

		/// <summary>
		/// Finds one of the other mod's Link methods, as a delegate whose parameters and return type match it
		/// </summary>
		/// <typeparam name="TDelegate">The delegate, like Func&lt;Vector3, bool&gt;</typeparam>
		/// <param name="name">The method's name</param>
		/// <returns>The method, or null when the other mod isn't installed or has no such method</returns>
		public TDelegate Get<TDelegate>(string name) where TDelegate : Delegate
		{
			if (bound.TryGetValue(name, out Delegate found))
			{
				return(found as TDelegate);
			}
			if (!FindLink())
			{
				// Not remembered, since the other mod may only not have loaded yet
				return(null);
			}
			Delegate made = null;
			MethodInfo invoke = typeof(TDelegate).GetMethod("Invoke");
			if (link != null)
			{
				Type[] parameters = invoke.GetParameters().Select((ParameterInfo parameter) => parameter.ParameterType).ToArray();
				MethodInfo method = link.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
				if (method != null && method.ReturnType == invoke.ReturnType)
				{
					made = Delegate.CreateDelegate(typeof(TDelegate), method, false);
				}
			}
			if (made == null)
			{
				VikingMod.Log?.LogInfo("The installed version of " + guid + " has no " + name + " for " + VikingMod.Name + " to use");
			}
			bound[name] = made;
			return(made as TDelegate);
		}

		/// <summary>
		/// Finds the other mod's Link class, once the mod is loaded
		/// </summary>
		/// <returns>True once the mod is loaded, whether or not it has a Link class</returns>
		private bool FindLink()
		{
			if (loaded)
			{
				return(true);
			}
			if (!Chainloader.PluginInfos.TryGetValue(guid, out PluginInfo info) || info.Instance == null)
			{
				return(false);
			}
			loaded = true;
			link = info.Instance.GetType().Assembly.GetType(typeName);
			return(true);
		}
	}
}
