using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Mono.Cecil;
using MonoDroid.Generation;
using Xamarin.AndroidTools.AnnotationSupport;
using Xamarin.Android.Tools.ApiXmlAdjuster;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.Diagnostics;
using Java.Interop.Tools.TypeNameMappings;
using MonoDroid.Generation.Utilities;

namespace Xamarin.Android.Binder
{
	public class CodeGenerator
	{
		public static int Main (string[] args)
		{
			var options = CodeGeneratorOptions.Parse (args);
			if (options == null)
				return 1;

			Run (options);
			return 0;
		}

		public static void Run (CodeGeneratorOptions options)
		{
			if (options == null)
				throw new ArgumentNullException ("options");

			using (var resolver = new DirectoryAssemblyResolver (Diagnostic.CreateConsoleLogger (), loadDebugSymbols: false)) {
				Run (options, resolver);
			}
		}

		static void Run (CodeGeneratorOptions options, DirectoryAssemblyResolver resolver)
		{
			string assemblyQN       = options.AssemblyQualifiedName;
			string api_level        = options.ApiLevel;
			int product_version     = options.ProductVersion;
			bool preserve_enums     = options.PreserveEnums;
			string csdir            = options.ManagedCallableWrapperSourceOutputDirectory ?? "cs";
			string javadir          = "java";
			string enumdir          = options.EnumOutputDirectory ?? "enum";
			string enum_metadata    = options.EnumMetadataOutputFile ?? "enummetadata";
			var references          = options.AssemblyReferences;
			string enum_fields_map  = options.EnumFieldsMapFile;
			string enum_flags       = options.EnumFlagsFile;
			string enum_methods_map = options.EnumMethodsMapFile;
			var fixups              = options.FixupFiles;
			string api_versions_xml = options.ApiVersionsXmlFile;
			var annotations_zips    = options.AnnotationsZipFiles;
			string filename         = options.ApiDescriptionFile;
			string mapping_file     = options.MappingReportFile;
			bool only_xml_adjuster  = options.OnlyRunApiXmlAdjuster;
			string api_xml_adjuster_output = options.ApiXmlAdjusterOutput;
			var apiSource           = "";
			var opt                 = new CodeGenerationOptions () {
				CodeGenerationTarget  = options.CodeGenerationTarget,
				UseGlobal             = options.GlobalTypeNames,
				IgnoreNonPublicType   = true,
				UseShortFileNames     = options.UseShortFileNames,
				ProductVersion        = options.ProductVersion,
				SupportInterfaceConstants = options.SupportInterfaceConstants,
				SupportDefaultInterfaceMethods = options.SupportDefaultInterfaceMethods,
			};

			// Load reference libraries

			foreach (var lib in options.LibraryPaths) {
				resolver.SearchDirectories.Add (lib);
			}
			foreach (var reference in references) {
				resolver.SearchDirectories.Add (Path.GetDirectoryName (reference));
			}

			// Figure out if this is class-parse
			string apiXmlFile = filename;
			string apiSourceAttr = null;

			using (var xr = XmlReader.Create (filename)) {
				xr.MoveToContent ();
				apiSourceAttr = xr.GetAttribute ("api-source");
			}

			// We don't use shallow referenced types with class-parse because the Adjuster process
			// enumerates every ctor/method/property/field to build its model, so we will need
			// every type to be fully populated.
			opt.UseShallowReferencedTypes = apiSourceAttr != "class-parse";

			foreach (var reference in references) {
				try {
					Report.Verbose (0, "resolving assembly {0}.", reference);
					var assembly    = resolver.Load (reference);
					foreach (var md in assembly.Modules)
						foreach (var td in md.Types) {
							// FIXME: at some stage we want to import generic types.
							// For now generator fails to load generic types that have conflicting type e.g.
							// AdapterView`1 and AdapterView cannot co-exist.
							// It is mostly because generator primarily targets jar (no real generics land).
							var nonGenericOverload  = td.HasGenericParameters
								? md.GetType (td.FullName.Substring (0, td.FullName.IndexOf ('`')))
								: null;
							if (BindSameType (td, nonGenericOverload))
								continue;
							ProcessReferencedType (td, opt);
						}
				} catch (Exception ex) {
					Report.Warning (0, Report.WarningCodeGenerator + 0, ex, "failed to parse assembly {0}: {1}", reference, ex.Message);
				}
			}

			// For class-parse API description, transform it to jar2xml style.
			if (apiSourceAttr == "class-parse") {
				apiXmlFile = api_xml_adjuster_output ?? Path.Combine (Path.GetDirectoryName (filename), Path.GetFileName (filename) + ".adjusted");
				new Adjuster ().Process (filename, opt, opt.SymbolTable.AllRegisteredSymbols (opt).OfType<GenBase> ().ToArray (), apiXmlFile, Report.Verbosity ?? 0);
			}
			if (only_xml_adjuster)
				return;

			// load XML API definition with fixups.

			Dictionary<string, EnumMappings.EnumDescription> enums = null;

			EnumMappings enummap = null;

			if (enum_fields_map != null || enum_methods_map != null) {
				enummap = new EnumMappings (enumdir, enum_metadata, api_level, preserve_enums);
				enums = enummap.Process (enum_fields_map, enum_flags, enum_methods_map);
				fixups.Add (enum_metadata);
			}

			Parser p = new Parser (opt);
			List<GenBase> gens = p.Parse (apiXmlFile, fixups, api_level, product_version);
			if (gens == null) {
				return;
			}
			apiSource = p.ApiSource;

			// disable interface default methods here, especially before validation.
			gens = gens.Where (g => !g.IsObfuscated && g.Visibility != "private").ToList ();
			foreach (var gen in gens) {
				gen.StripNonBindables (opt);
				if (gen.IsGeneratable)
					AddTypeToTable (opt, gen);
			}

			Validate (gens, opt, new CodeGeneratorContext ());

			if (api_versions_xml != null)
				ApiVersionsSupport.AssignApiLevels (gens, api_versions_xml);

			foreach (GenBase gen in gens)
				gen.FillProperties ();

			var cache = new AncestorDescendantCache (gens);

			foreach (var gen in gens)
				gen.UpdateEnums (opt, cache);

			foreach (GenBase gen in gens)
				gen.FixupMethodOverrides (opt);

			foreach (GenBase gen in gens)
				gen.FixupExplicitImplementation ();

			GenerateAnnotationAttributes (gens, annotations_zips);

			//SymbolTable.Dump ();

			GenerationInfo gen_info = new GenerationInfo (csdir, javadir, assemblyQN);
			opt.AssemblyName = gen_info.Assembly;

			if (mapping_file != null)
				GenerateMappingReportFile (gens, mapping_file);

			new NamespaceMapping (gens).Generate (opt, gen_info);

			foreach (IGeneratable gen in gens)
				if (gen.IsGeneratable)
					gen.Generate (opt, gen_info);


			ClassGen.GenerateTypeRegistrations (opt, gen_info);
			ClassGen.GenerateEnumList (gen_info);

			// Create the .cs files for the enums
			var enumFiles = enums == null
				? null
				: enummap.WriteEnumerations (enumdir, enums, FlattenNestedTypes (gens).ToArray (), opt.UseShortFileNames);

			gen_info.GenerateLibraryProjectFile (options, enumFiles);
		}

		static void AddTypeToTable (CodeGenerationOptions opt, GenBase gb)
		{
			opt.SymbolTable.AddType (gb);
			foreach (var nt in gb.NestedTypes)
				AddTypeToTable (opt, nt);
		}

		static bool BindSameType (TypeDefinition a, TypeDefinition b)
		{
			if (a == null || b == null)
				return false;
			if (!a.ImplementsInterface ("Android.Runtime.IJavaObject") || !b.ImplementsInterface ("Android.Runtime.IJavaObject"))
				return false;
			return JavaNativeTypeManager.ToJniName (a) == JavaNativeTypeManager.ToJniName (b);
		}

		static IEnumerable<GenBase> FlattenNestedTypes (IEnumerable<GenBase> gens)
		{
			foreach (var g in gens) {
				yield return g;
				foreach (var gg in FlattenNestedTypes (g.NestedTypes))
					yield return gg;
			}
		}

		static void Validate (List<GenBase> gens, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			//int cycle = 1;
			List<GenBase> removed = new List<GenBase> ();
			// This loop is required because we cannot really split type validation and member
			// validation apart (unlike C# compiler), because invalidated members will result
			// in the entire interface invalidation (since we cannot implement it), and use of
			// those invalidated interfaces must be eliminated in members in turn again.
			do {
				//Console.WriteLine ("Validation cycle " + cycle++);
				removed.Clear ();
				foreach (GenBase gen in gens)
					gen.ResetValidation ();
				foreach (GenBase gen in gens)
					gen.FixupAccessModifiers (opt);
				foreach (GenBase gen in gens)
					if ((opt.IgnoreNonPublicType &&
					    (gen.RawVisibility != "public" && gen.RawVisibility != "internal"))
					    || !gen.Validate (opt, null, context)) {
						foreach (GenBase nest in gen.NestedTypes) {
							foreach (var nt in nest.Invalidate ())
								removed.Add (nt);
						}
						removed.Add (gen);
					}

				foreach (GenBase gen in removed)
					gens.Remove (gen);
			} while (removed.Count > 0);
		}

#if HAVE_CECIL
		internal static void ProcessReferencedType (TypeDefinition td, CodeGenerationOptions opt)
		{
			if (!td.IsPublic && !td.IsNested)
				return;

			// We want to exclude "IBlahInvoker" types from this type registration.
			if (td.Name.EndsWith ("Invoker")) {
				string n = td.FullName;
				n = n.Substring (0, n.Length - 7);
				var types = td.DeclaringType != null ? td.DeclaringType.Resolve ().NestedTypes : td.Module.Types;
				if (types.Any (t => t.FullName == n))
					return;
				//Console.Error.WriteLine ("WARNING: " + td.FullName + " survived");
			}
			if (td.Name.EndsWith ("Implementor")) {
				string n = td.FullName;
				n = n.Substring (0, n.Length - 11);
				var types = td.DeclaringType != null ? td.DeclaringType.Resolve ().NestedTypes : td.Module.Types;
				if (types.Any (t => t.FullName == n))
					return;
				//Console.Error.WriteLine ("WARNING: " + td.FullName + " survived");
			}

			ISymbol gb = td.IsEnum ? (ISymbol)new EnumSymbol (td.FullNameCorrected ()) : td.IsInterface ? (ISymbol)CecilApiImporter.CreateInterface (td, opt) : CecilApiImporter.CreateClass (td, opt);
			opt.SymbolTable.AddType (gb);

			foreach (var nt in td.NestedTypes)
				ProcessReferencedType (nt, opt);
		}
#endif  // HAVE_CECIL

		static void GenerateAnnotationAttributes (List<GenBase> gens, IEnumerable<string> zips)
		{
			if (zips == null || !zips.Any ())
				return;
			var annotations = new AndroidAnnotationsSupport ();
			annotations.Extensions.Add (new ManagedTypeFinderGeneratorTypeSystem (gens.ToArray ()));
			foreach (var z in zips)
				annotations.Load (z);

			foreach (var item in annotations.Data.SelectMany (d => d.Value)) {
				if (!item.Annotations.Any (a => a.Name == "RequiresPermission"))
					continue;
				var cx = item.GetExtension<RequiresPermissionExtension> ();
				if (cx == null)
					continue;
				string annotation = null;
				foreach (var value in cx.Values)
					annotation += string.Format ("[global::Android.Runtime.RequiresPermission (\"{0}\")]", value);

				AddAnnotationTo (item, annotation);
			}

			foreach (var item in annotations.Data.SelectMany (d => d.Value)) {
				if (!item.Annotations.Any (a => a.Name == "IntDef" || a.Name == "StringDef"))
					continue;
				foreach (var ann in item.Annotations) {
					var cx = ann.GetExtension<ConstantDefinitionExtension> ();
					if (cx == null || cx.IsTargetAlreadyEnumified)
						continue;

					var groups = cx.ManagedConstants.GroupBy (m => m.Type.FullName);
					string annotation = null;
					// Generate [IntDef(Type = ..., Fields = ...)] possibly multiple times for each type of the fields, grouped
					// (mostly 1, except for things like PendingIntent flags, which is not covered here because it's already enumified).
					// ditto for StringDef.
					foreach (var grp in groups)
						annotation += "[global::Android.Runtime." + ann.Name + " (" + (cx.Flag ? "Flag = true, " : null) +
							"Type = \"" + grp.Key +
							"\", Fields = new string [] {" + string.Join (", ", grp.Select (mav => '"' + mav.MemberName + '"')) + "})]";

					AddAnnotationTo (item, annotation);
				}
			}
		}

		static void AddAnnotationTo (AnnotatedItem item, string annotation)
		{
			if (item.ManagedInfo.PropertyObject != null)
				item.ManagedInfo.PropertyObject.Value ().Annotation += annotation;
			else if (item.ManagedInfo.MethodObject != null) {
				if (item.ParameterIndex < 0)
					item.ManagedInfo.MethodObject.Value ().Annotation += annotation;
				else
					item.ManagedInfo.MethodObject.Value ().Parameters [item.ParameterIndex].Annotation += annotation;
			}
		}

		// generate mapping report file.
		static void GenerateMappingReportFile (List<GenBase> gens, string mapping_file)
		{
			using (var fs = File.CreateText (mapping_file)) {
				foreach (var gen in gens.OrderBy (g => g.JniName)) {
					fs.Write (gen.JniName.Substring (1, gen.JniName.Length - 2)); // skip 'L' and ';' at head and tail.
					fs.Write (" = ");
					fs.WriteLine (gen.FullName);

					var cls = gen as ClassGen;
					if (cls != null) {
						foreach (var m in cls.Ctors.OrderBy (_ => _.JniSignature)) {
							fs.Write ("  ");
							fs.Write ("<init>");
							fs.Write (m.JniSignature);
							fs.Write (" = ");
							fs.Write (".ctor");
							fs.WriteLine ("(" + string.Join (", ", m.Parameters.Select (p => p.Type)) + ")");
						}
					}

					foreach (var f in gen.Fields.OrderBy (f => f.Name)) {
						fs.Write ("  ");
						fs.Write (f.JavaName);
						fs.Write (" = ");
						fs.WriteLine (f.Name);
					}

					foreach (var p in gen.Properties.OrderBy (_ => _.Name)) {
						if (p.Getter != null) {
							fs.Write ("  ");
							fs.Write (p.Getter.JavaName);
							fs.Write (p.Getter.JniSignature);
							fs.Write (" = ");
							fs.WriteLine (p.Name);
						}
						if (p.Setter != null) {
							fs.Write ("  ");
							fs.Write (p.Setter.JavaName);
							fs.Write (p.Setter.JniSignature);
							fs.Write (" = ");
							fs.WriteLine (p.Name);
						}
					}

					foreach (var m in gen.Methods.OrderBy (_ => _.AdjustedName)) {
						fs.Write ("  ");
						fs.Write (m.JavaName);
						fs.Write (m.JniSignature);
						fs.Write (" = ");
						fs.Write (m.AdjustedName);
						fs.WriteLine ("(" + string.Join (", ", m.Parameters.Select (p => p.Type)) + ")");
					}
				}
			}
		}
	}
}
