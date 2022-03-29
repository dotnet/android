using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Mono.Cecil;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers;

namespace Xamarin.Android.Tools
{
	class mgen
	{
		static void Usage ()
		{
			Console.WriteLine ("Usage: mgen ASSEMBLY_PATH [ASSEMBLY_PATH...]");
			Console.WriteLine ();
		}

		static void Die (string message, bool withUsage)
		{
			Console.WriteLine (message);
			if (withUsage) {
				Console.WriteLine ();
				Usage ();
			}
			Environment.Exit (1);
		}

		static void Logger (TraceLevel level, string message)
		{
			Console.WriteLine ($"[level] {message}");
		}

		static void ProcessAssemblies (string[] assemblyPaths)
		{
			var resolver = new DirectoryAssemblyResolver (Logger, loadDebugSymbols: true);
			var assemblyDirs = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			foreach (string assemblyPath in assemblyPaths) {
				string dir = Path.GetDirectoryName (assemblyPath);
				if (assemblyDirs.Contains (dir)) {
					continue;
				}

				assemblyDirs.Add (dir);
				resolver.SearchDirectories.Add (dir);
			}
			resolver.SearchDirectories.Add (Constants.FrameworkLibsDir);
			resolver.SearchDirectories.Add (Constants.FrameworkArchLibsDir);

			var tdCache = new TypeDefinitionCache ();
			var scanner = new JavaTypeScanner (Logger, tdCache);
			List<TypeDefinition> types = scanner.GetJavaTypes (assemblyPaths, resolver);
			foreach (TypeDefinition type in types) {
				if (JavaTypeScanner.ShouldSkipJavaCallableWrapperGeneration (type, tdCache)) {
					continue;
				}
				Console.WriteLine ($"Interesting type: {type.FullName}");
			}
		}

		static int Main (string[] args)
		{
			if (args.Length == 0) {
				Die ("Not enough arguments", withUsage: true);
			}
			ProcessAssemblies (args);

			return 0;
		}
	}
}
