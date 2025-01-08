// Copyright (C) 2011 Xamarin, Inc. All rights reserved.
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class GenerateResourceDesigner : AndroidTask
	{
		public override string TaskPrefix => "GRD";

		[Required]
		public string NetResgenOutputFile { get; set; }

		public string DesignTimeOutputFile { get; set; }

		public string JavaResgenInputFile { get; set; }

		public string RTxtFile { get; set; }

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

		[Required]
		public string JavaPlatformJarPath { get; set; }

		public string ResourceFlagFile { get; set; }

		public string CaseMapFile { get; set; }

		private Dictionary<string, string> resource_fixup = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);

		public override bool RunTask ()
		{
			// In Xamarin Studio, if the project name isn't a valid C# identifier
			// then $(RootNamespace) is not set, and the generated Activity is
			// placed into the "Application" namespace. VS just munges the project
			// name to be a valid C# identifier.
			// Use "Application" as the default namespace name to work with XS.
			Namespace = Namespace ?? "Application";

			if (!File.Exists (JavaResgenInputFile) && !UseManagedResourceGenerator)
				return true;

			// ResourceDirectory may be a relative path, and
			// we need to compare it to absolute paths
			ResourceDirectory = Path.GetFullPath (ResourceDirectory);

			var javaPlatformDirectory = Path.GetDirectoryName (JavaPlatformJarPath);

			resource_fixup = MonoAndroidHelper.LoadMapFile (BuildEngine4, Path.GetFullPath (CaseMapFile), StringComparer.OrdinalIgnoreCase);

			// Parse out the resources from the R.java file
			CodeTypeDeclaration resources;
			if (UseManagedResourceGenerator) {
				var parser = new ManagedResourceParser () { Log = Log, JavaPlatformDirectory = javaPlatformDirectory, ResourceFlagFile = ResourceFlagFile };
				resources = parser.Parse (ResourceDirectory, RTxtFile ?? string.Empty, AdditionalResourceDirectories?.Select (x => x.ItemSpec), IsApplication, resource_fixup);
			} else {
				var parser = new JavaResourceParser () { Log = Log };
				resources = parser.Parse (JavaResgenInputFile, IsApplication, resource_fixup);
			}

			var extension = Path.GetExtension (NetResgenOutputFile);
			var language = string.Compare (extension, ".fs", StringComparison.OrdinalIgnoreCase) == 0 ? "F#" : CodeDomProvider.GetLanguageFromExtension (extension);
			bool isVB = string.Equals (extension, ".vb", StringComparison.OrdinalIgnoreCase);
			bool isFSharp = string.Equals (language, "F#", StringComparison.OrdinalIgnoreCase);
			bool isCSharp = string.Equals (language, "C#", StringComparison.OrdinalIgnoreCase);


			if (isFSharp) {
				language = "C#";
				isCSharp = true;
				NetResgenOutputFile = Path.ChangeExtension (NetResgenOutputFile, ".cs");
			}

			// Let VB put this in the default namespace
			if (isVB)
				Namespace = string.Empty;

			List<string> aliases = new List<string> ();
			// Create static resource overwrite methods for each Resource class in libraries.
			if (IsApplication && References != null && References.Length > 0) {
				var assemblies = new List<ITaskItem> (References.Length);
				foreach (var assembly in References) {
					var assemblyPath = assembly.ItemSpec;
					var fileName = Path.GetFileName (assemblyPath);
					if (!File.Exists (assemblyPath)) {
						Log.LogDebugMessage ($"Skipping non-existent dependency '{assemblyPath}'.");
						continue;
					}
					ITaskItem item = new TaskItem (assemblyPath);
					assembly.CopyMetadataTo (item);
					assemblies.Add (item);
					string aliasMetaData = assembly.GetMetadata ("Aliases");
					if (!string.IsNullOrEmpty (aliasMetaData)) {
						foreach (var alias in aliasMetaData.Split (new [] {','}, StringSplitOptions.RemoveEmptyEntries)) {
							string aliasName = alias.Trim ();
							// don't emit an `extern alias global` as it is implicitly done.
							if (string.Compare ("global", aliasName, StringComparison.Ordinal) == 0)
								continue;
							aliases.Add (aliasName);
							// only add the first alias for each reference.
							break;
						}
					}
					Log.LogDebugMessage ("Scan assembly {0} for resource generator", fileName);
				}
				new ResourceDesignerImportGenerator (Namespace, resources, Log)
					.CreateImportMethods (assemblies);
			}

			AdjustConstructor (resources);
			foreach (var member in resources.Members)
				if (member is CodeTypeDeclaration)
					AdjustConstructor ((CodeTypeDeclaration) member);

			// Write out our Resources.Designer.cs file

			WriteFile (NetResgenOutputFile, resources, language, isCSharp, aliases);

			// During a regular build, write the designtime/Resource.designer.cs file as well

			if (!string.IsNullOrEmpty (DesignTimeOutputFile) && Files.CopyIfChanged (NetResgenOutputFile, DesignTimeOutputFile)) {
				Log.LogDebugMessage ($"Writing to: {DesignTimeOutputFile}");
			}

			return !Log.HasLoggedErrors;
		}

		// Remove private constructor in F#.
		// Add static constructor. (but ignored in F#)
		void AdjustConstructor (CodeTypeDeclaration type)
		{
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

		private void WriteFile (string file, CodeTypeDeclaration resources, string language, bool isCSharp, IEnumerable<string> aliases)
		{
			CodeDomProvider provider = CodeDomProvider.CreateProvider (language);

			using (var o = MemoryStreamPool.Shared.CreateStreamWriter ()) {
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
				if (isCSharp) {
					foreach (var alias in aliases)
						provider.GenerateCodeFromStatement (new CodeSnippetStatement ($"extern alias {alias};"), o, options);
					provider.GenerateCodeFromCompileUnit (new CodeSnippetCompileUnit ("#pragma warning disable 1591"), o, options);
				}

				provider.CreateGenerator (o).GenerateCodeFromCompileUnit (unit, o, options);

				// Add Pragma to re-enable warnings about no Xml documentation
				if (isCSharp)
					provider.GenerateCodeFromCompileUnit(new CodeSnippetCompileUnit("#pragma warning restore 1591"), o, options);

				o.Flush ();
				if (Files.CopyIfStreamChanged (o.BaseStream, file)) {
					Log.LogDebugMessage ($"Writing to: {file}");
				} else {
					Log.LogDebugMessage ($"Up to date: {file}");
				}
			}
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
