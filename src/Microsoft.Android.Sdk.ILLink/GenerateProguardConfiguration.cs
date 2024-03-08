//
// GenerateProguardConfiguration.cs
//
// Author:
//	Atsushi Eno <atsushi@xamarin.com>
//
// (C) 2014 Xamarin Inc. (http://www.xamarin.com)
//

using System;
using System.IO;
using System.Linq;

using Mono.Cecil;

namespace Mono.Linker.Steps {
	public class GenerateProguardConfiguration : BaseStep
	{
		string filename;
		TextWriter writer;

		protected override void Process ()
		{
			if (Context.TryGetCustomData ("ProguardConfiguration", out string proguardPath))
				filename = proguardPath;
			var dir = Path.GetDirectoryName (filename);
			if (!Directory.Exists (dir))
				Directory.CreateDirectory (dir);

			writer = File.CreateText (filename);
		}

		protected override void EndProcess ()
		{
			writer.Close ();
		}

		protected override void ProcessAssembly (AssemblyDefinition assembly)
		{
			// Those assemblies that do not reference Mono.Android.dll (such as System.*
			// assemblies and Mono.Android.dll itself) can be skipped.
			// (Mono.Android.dll is special; android.jar is not part of classes.dex).
			//
			// FIXME: Those non-embedded jar bindings could visit here too, and they don't have to
			// be part of proguard configuration. But they don't break (they will be NOTEd though).
			if (!assembly.MainModule.AssemblyReferences.Any (r => r.Name == "Mono.Android"))
				return;
			
			writer.WriteLine ("# ACW for " + assembly.Name.Name);
			foreach (var type in assembly.MainModule.Types)
				ProcessType (type);
		}

		void ProcessType (TypeDefinition type)
		{
			foreach (var nt in type.NestedTypes)
				ProcessType (nt);
			if (!type.IsClass)
				return;
			var ra = type.CustomAttributes.FirstOrDefault (a => a.AttributeType.FullName == "Android.Runtime.RegisterAttribute");
			if (ra == null)
				return;
			var jtype = ra.ConstructorArguments.First ().Value.ToString ().Replace ('/', '.');
			writer.WriteLine ("-keep class " + jtype);
			writer.WriteLine ("-keepclassmembers class " + jtype + " {");
			foreach (var m in type.Methods)
				ProcessMethod (m);
			writer.WriteLine ("}");
			writer.WriteLine ();
		}

		void ProcessMethod (MethodDefinition method)
		{
			var ra = method.CustomAttributes.FirstOrDefault (a => a.AttributeType.FullName == "Android.Runtime.RegisterAttribute");
			if (ra == null)
				return;
			var jname = ra.ConstructorArguments.First ().Value.ToString ();
			var jargs = ra.ConstructorArguments [1].Value.ToString ();
			var pargs = jargs.StartsWith ("()", StringComparison.Ordinal) ? string.Empty : "***";
			// FIXME: do not preserve all overroads.
			if (jname == ".ctor")
				writer.WriteLine ("   <init>(...);", pargs);
			else
				writer.WriteLine ("   *** {0}(...);", jname, pargs);
		}
	}
}
