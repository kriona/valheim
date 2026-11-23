using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Kriona.Tests
{
	/// <summary>
	/// Loads the game's and BepInEx's DLLs from the folders the build found them in, since they aren't copied beside
	/// the tests, so a test can touch a mod's types without listing every DLL they pull in
	/// </summary>
	/// <remarks>
	/// The mods reach the game's private members through the IgnoresAccessChecksTo attribute the publicizer adds, which
	/// .NET honors, so the game's own DLLs work as they are. Code that calls into Unity's engine (Debug.Log, Random,
	/// Quaternion.Euler, Animator.StringToHash and the like) can't run outside the game, so tests cover the code that
	/// only works things out
	/// </remarks>
	internal static class GameAssemblies
	{
		/// <summary>
		/// The folders searched, from the GameFolder metadata the build writes into the test assembly
		/// </summary>
		private static readonly List<string> folders = new List<string>();

		/// <summary>
		/// Hooks up the search for the game's DLLs as the test assembly loads, before any test runs
		/// </summary>
		[ModuleInitializer]
		internal static void Initialize()
		{
			foreach (AssemblyMetadataAttribute metadata in typeof(GameAssemblies).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
			{
				if (metadata.Key == "GameFolder" && !string.IsNullOrEmpty(metadata.Value))
				{
					folders.Add(metadata.Value);
				}
			}
			AssemblyLoadContext.Default.Resolving += Resolve;
		}

		/// <summary>
		/// Finds a DLL the tests don't have a copy of in the game's folders
		/// </summary>
		/// <param name="context">The load context looking for it</param>
		/// <param name="name">The assembly's name</param>
		/// <returns>The assembly, or null when neither folder has it</returns>
		private static Assembly Resolve(AssemblyLoadContext context, AssemblyName name)
		{
			foreach (string folder in folders)
			{
				string path = Path.Combine(folder, name.Name + ".dll");
				if (File.Exists(path))
				{
					return(context.LoadFromAssemblyPath(path));
				}
			}
			return(null);
		}
	}
}
