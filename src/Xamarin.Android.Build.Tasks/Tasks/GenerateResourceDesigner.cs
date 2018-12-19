// Copyright (C) 2011 Xamarin, Inc. All rights reserved.
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Monodroid;

using Java.Interop.Tools.Cecil;

namespace Xamarin.Android.Tasks
{
	public class GenerateResourceDesigner : Task
	{
		[Required]
		public string NetResgenOutputFile { get; set; }

		public string JavaResgenInputFile { get; set; }

		public string Namespace { get; set; }

		[Required]
		public string ProjectDir { get; set; }

		[Required]
		public ITaskItem[] Resources { get; set; }

		[Required]
		public string ResourceDirectory { get; set; }
		
		public ITaskItem[] AdditionalResourceDirectories { get; set; }

		[Required]
		public bool IsApplication { get; set; }

		public ITaskItem[] References { get; set; }

		[Required]
		public bool UseManagedResourceGenerator { get; set; }

		[Required]
		public bool DesignTimeBuild { get; set; }

		private Dictionary<string, string> resource_fixup = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);

		public override bool Execute ()
		{
			// In Xamarin Studio, if the project name isn't a valid C# identifier
			// then $(RootNamespace) is not set, and the generated Activity is
			// placed into the "Application" namespace. VS just munges the project
			// name to be a valid C# identifier.
			// Use "Application" as the default namespace name to work with XS.
			Namespace = Namespace ?? "Application";

			Log.LogDebugMessage ("GenerateResourceDesigner Task");
			Log.LogDebugMessage ("  NetResgenOutputFile: {0}", NetResgenOutputFile);
			Log.LogDebugMessage ("  JavaResgenInputFile: {0}", JavaResgenInputFile);
			Log.LogDebugMessage ("  Namespace: {0}", Namespace);
			Log.LogDebugMessage ("  ResourceDirectory: {0}", ResourceDirectory);
			Log.LogDebugTaskItemsAndLogical ("  AdditionalResourceDirectories:", AdditionalResourceDirectories);
			Log.LogDebugMessage ("  IsApplication: {0}", IsApplication);
			Log.LogDebugMessage ("  UseManagedResourceGenerator: {0}", UseManagedResourceGenerator);
			Log.LogDebugTaskItemsAndLogical ("  Resources:", Resources);
			Log.LogDebugTaskItemsAndLogical ("  References:", References);

			if (!File.Exists (JavaResgenInputFile) && !UseManagedResourceGenerator)
				return true;

			// ResourceDirectory may be a relative path, and
			// we need to compare it to absolute paths
			ResourceDirectory = Path.GetFullPath (ResourceDirectory);

			// Create our capitalization maps so we can support mixed case resources
			foreach (var item in Resources) {
				if (!item.ItemSpec.StartsWith (ResourceDirectory))
					continue;

				var name = item.ItemSpec.Substring (ResourceDirectory.Length);
				var logical_name = item.GetMetadata ("LogicalName").Replace ('\\', '/');

				AddRename (name.Replace ('/', Path.DirectorySeparatorChar), logical_name.Replace ('/', Path.DirectorySeparatorChar));
			}
			if (AdditionalResourceDirectories != null) {
				foreach (var additionalDir in AdditionalResourceDirectories) {
					var file = Path.Combine (ProjectDir, Path.GetDirectoryName (additionalDir.ItemSpec), "__res_name_case_map.txt");
					if (File.Exists (file)) {
						foreach (var line in File.ReadAllLines (file).Where (l => !string.IsNullOrEmpty (l))) {
							string [] tok = line.Split (';');
							AddRename (tok [1].Replace ('/', Path.DirectorySeparatorChar), tok [0].Replace ('/', Path.DirectorySeparatorChar));
						}
					}
				}
			}

			// Parse out the resources from the R.java file
			CodeTypeDeclaration resources;
			if (UseManagedResourceGenerator) {
				var parser = new ManagedResourceParser () { Log = Log };
				resources = parser.Parse (ResourceDirectory, AdditionalResourceDirectories?.Select (x => x.ItemSpec), IsApplication, resource_fixup);
			} else {
				var parser = new JavaResourceParser () { Log = Log };
				resources = parser.Parse (JavaResgenInputFile, IsApplication, resource_fixup);
			}
			
			var extension = Path.GetExtension (NetResgenOutputFile);
			var language = string.Compare (extension, ".fs", StringComparison.OrdinalIgnoreCase) == 0 ? "F#" : CodeDomProvider.GetLanguageFromExtension (extension);
			bool isVB = string.Equals (extension, ".vb", StringComparison.OrdinalIgnoreCase);
			bool isFSharp = string.Equals (language, "F#", StringComparison.OrdinalIgnoreCase);
			bool isCSharp = string.Equals (language, "C#", StringComparison.OrdinalIgnoreCase);

			// Let VB put this in the default namespace
			if (isVB)
				Namespace = string.Empty;

			// Create static resource overwrite methods for each Resource class in libraries.
			var assemblyNames = new List<string> ();
			if (IsApplication && References != null && References.Any ()) {
				// FIXME: should this be unified to some better code with ResolveLibraryProjectImports?
				using (var resolver = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: false)) {
					foreach (var assemblyName in References) {
						var suffix = assemblyName.ItemSpec.EndsWith (".dll") ? String.Empty : ".dll";
						string hintPath = assemblyName.GetMetadata ("HintPath").Replace (Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
						string fileName = assemblyName.ItemSpec + suffix;
						string fullPath = Path.GetFullPath (assemblyName.ItemSpec);
						// Skip non existing files in DesignTimeBuild
						if (!File.Exists (fullPath) && DesignTimeBuild) {
							Log.LogDebugMessage ("Skipping non existant dependancy '{0}' due to design time build.", fullPath);
							continue;
						}
						resolver.Load (fullPath);
						if (!String.IsNullOrEmpty (hintPath) && !File.Exists (hintPath)) // ignore invalid HintPath
							hintPath = null;
						string assemblyPath = String.IsNullOrEmpty (hintPath) ? fileName : hintPath;
						if (MonoAndroidHelper.IsFrameworkAssembly (fileName) && !MonoAndroidHelper.FrameworkEmbeddedJarLookupTargets.Contains (Path.GetFileName (fileName)))
							continue;
						Log.LogDebugMessage ("Scan assembly {0} for resource generator", fileName);
						assemblyNames.Add (assemblyPath);
					}
					var assemblies = assemblyNames.Select (assembly => resolver.GetAssembly (assembly));
					new ResourceDesignerImportGenerator (Namespace, resources, Log)
						.CreateImportMethods (assemblies);
				}
			}

			AdjustConstructor (isFSharp, resources);
			foreach (var member in resources.Members)
				if (member is CodeTypeDeclaration)
					AdjustConstructor (isFSharp, (CodeTypeDeclaration) member);

			// Write out our Resources.Designer.cs file

			WriteFile (NetResgenOutputFile, resources, language, isFSharp, isCSharp);

			return !Log.HasLoggedErrors;
		}

		// Remove private constructor in F#.
		// Add static constructor. (but ignored in F#)
		void AdjustConstructor (bool isFSharp, CodeTypeDeclaration type)
		{			
			if (isFSharp) {
				foreach (CodeTypeMember tm in type.Members) {
					if (tm is CodeConstructor) {
						type.Members.Remove (tm);
						break;
					}
				}
			}

			var staticCtor = new CodeTypeConstructor () { Attributes = MemberAttributes.Static };
			staticCtor.Statements.Add (
				new CodeExpressionStatement (
				new CodeMethodInvokeExpression (
				new CodeTypeReferenceExpression (
				new CodeTypeReference (
				"Android.Runtime.ResourceIdManager",
				CodeTypeReferenceOptions.GlobalReference)),
				"UpdateIdValues")));
			type.Members.Add (staticCtor);
		}

		private void WriteFile (string file, CodeTypeDeclaration resources, string language, bool isFSharp, bool isCSharp)
		{
			CodeDomProvider provider = 
				isFSharp ? new FSharp.Compiler.CodeDom.FSharpCodeProvider () :
				CodeDomProvider.CreateProvider (language);

			string code = null;
			using (var o = new StringWriter ()) {
				var options = new CodeGeneratorOptions () {
					BracingStyle = "C",
					IndentString = "\t",
				};

				var ns = string.IsNullOrEmpty (Namespace)
					? new CodeNamespace ()
					: new CodeNamespace (Namespace);

				if (resources != null)
					ns.Types.Add (resources);

				var unit = new CodeCompileUnit ();
				unit.Namespaces.Add (ns);

				var resgenatt = new CodeAttributeDeclaration (new CodeTypeReference ("Android.Runtime.ResourceDesignerAttribute", CodeTypeReferenceOptions.GlobalReference));
				resgenatt.Arguments.Add (new CodeAttributeArgument (new CodePrimitiveExpression (Namespace.Length > 0 ? Namespace + ".Resource" : "Resource")));
				resgenatt.Arguments.Add (new CodeAttributeArgument ("IsApplication", new CodePrimitiveExpression (IsApplication)));
				unit.AssemblyCustomAttributes.Add (resgenatt);

				// Add Pragma to disable warnings about no Xml documentation
				if (isCSharp)
					provider.GenerateCodeFromCompileUnit(new CodeSnippetCompileUnit("#pragma warning disable 1591"), o, options);

				provider.CreateGenerator (o).GenerateCodeFromCompileUnit (unit, o, options);

				// Add Pragma to re-enable warnings about no Xml documentation
				if (isCSharp)
					provider.GenerateCodeFromCompileUnit(new CodeSnippetCompileUnit("#pragma warning restore 1591"), o, options);

				code = o.ToString ();

				// post-processing for F#
				if (isFSharp) {
					code = code.Replace ("\r\n", "\n");
					while (true) {
						int skipLen = " = class".Length;
						int idx = code.IndexOf (" = class");
						if (idx < 0)
							break;
						int end = code.IndexOf ("        end");
						string head = code.Substring (0, idx);
						string mid = end < 0 ? code.Substring (idx) : code.Substring (idx + skipLen, end - idx - skipLen);
						string last = end < 0 ? null : code.Substring (end + "        end".Length);
						code = head + @" () =
            static do Android.Runtime.ResourceIdManager.UpdateIdValues()" + mid + "\n" + last;
					}
				}
			}

			MonoAndroidHelper.CopyIfStringChanged (code, file);
		}

		private void AddRename (string android, string user)
		{
			var from = android;
			var to = user;

			if (from.Contains ('.'))
				from = from.Substring (0, from.LastIndexOf ('.'));
			if (to.Contains ('.'))
				to = to.Substring (0, to.LastIndexOf ('.'));

			from = NormalizeAlternative (from);
			to = NormalizeAlternative (to);

			string curTo;

			if (resource_fixup.TryGetValue (from, out curTo)) {
				if (string.Compare (to, curTo, StringComparison.OrdinalIgnoreCase) != 0) {
					var ext = Path.GetExtension (android);
					var dir = Path.GetDirectoryName (user);

					Log.LogDebugMessage ("Resource target names differ; got '{0}', expected '{1}'.",
						Path.Combine (dir, Path.GetFileName (to) + ext),
						Path.Combine (dir, Path.GetFileName (curTo) + ext));
				}

				return;
			}

			resource_fixup.Add (from, to);
		}

		static string NormalizeAlternative (string value)
		{
			int s = value.IndexOfAny (new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
			
			if (s < 0)
				return value;

			int a = value.IndexOf ('-');

			return
				ResourceParser.GetNestedTypeName (value.Substring (0, (a < 0 || a >= s) ? s : a)).ToLowerInvariant () +
				value.Substring (s);
		}
	}
}
