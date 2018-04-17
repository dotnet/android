using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.CSharp;
using Mono.Cecil;
using Mono.Options;
using MonoDroid.Generation;
using Xamarin.Android.Binder;
using Xamarin.AndroidTools.AnnotationSupport;
using Xamarin.Android.Tools.ApiXmlAdjuster;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.TypeNameMappings;

namespace Xamarin.Android.Binder {

	public enum CodeGenerationTarget {
		XamarinAndroid,
		XAJavaInterop1,
		JavaInterop1,
	}

	public class CodeGeneratorOptions {

		public CodeGeneratorOptions ()
		{
			AssemblyReferences  = new Collection<string> ();
			FixupFiles          = new Collection<string> ();
			LibraryPaths        = new Collection<string> ();
			AnnotationsZipFiles = new Collection<string> ();
		}

		public string               ApiLevel {get; set;}
		public CodeGenerationTarget CodeGenerationTarget {get; set;}
		public string               ManagedCallableWrapperSourceOutputDirectory {get; set;}
		public string               AssemblyQualifiedName {get; set;}
		public Collection<string>   AssemblyReferences {get; private set;}
		public Collection<string>   FixupFiles {get; private set;}
		public Collection<string>   LibraryPaths {get; private set;}
		public bool                 GlobalTypeNames {get; set;}
		public bool                 OnlyBindPublicTypes {get; set;}
		public string               ApiDescriptionFile {get; set;}
		public string               ApiVersionsXmlFile {get; set;}
		public Collection<string>   AnnotationsZipFiles {get; set;}
		public string               EnumFieldsMapFile {get; set;}
		public string               EnumFlagsFile {get; set;}
		public string               EnumMethodsMapFile {get; set;}
		public string               EnumOutputDirectory {get; set;}
		public string               EnumMetadataOutputFile {get; set;}
		public bool                 PreserveEnums {get; set;}
		public bool                 UseShortFileNames {get; set;}
		public int                  ProductVersion { get; set; }
		public string               MappingReportFile { get; set; }
		public bool                 OnlyRunApiXmlAdjuster { get; set; }
		public string               ApiXmlAdjusterOutput { get; set; }

		public static CodeGeneratorOptions Parse (string[] args)
		{
			var opts = new CodeGeneratorOptions ();

			bool show_help = false;

			var parser = new OptionSet {
				"Usage: generator.exe OPTIONS+ API_DESCRIPTION",
				"",
				"Generates C# source files to bind Java code described by API_DESCRIPTION.",
				"",
				"Copyright 2012 Xamarin, Inc.",
				"",
				"Options:",
				{ "assembly=",
					"Fully Qualified Assembly Name ({FQAN}) of the eventual assembly (.dll) that will be built.",
					v => opts.AssemblyQualifiedName = v },
				{ "codegen-target=",
					"{STYLE} of Binding Assembly to generate",
					v => opts.CodeGenerationTarget = ParseCodeGenerationTarget (v) },
				{ "fixup=",
					"XML {FILE} controlling the generated API.\n" +
					"http://www.mono-project.com/GAPI#Altering_and_Extending_the_API_File",
					v => opts.FixupFiles.Add (v) },
				{ "global",
					"Prefix type names with `global::`.",
					v => opts.GlobalTypeNames = v != null },
				{ "javadir=",
					"Ignored; for compatibility.",
					v => {} },
				{ "L=",
					"{PATH} to look for referenced assemblies..",
					v => opts.LibraryPaths.Add (v) },
				{ "o|csdir=",
					"{DIRECTORY} to place C# source into.",
					v => opts.ManagedCallableWrapperSourceOutputDirectory = v },
				{ "public",
					"Obsolete option. It binds only public types now",
					v => opts.OnlyBindPublicTypes = v != null },
				{ "r|ref=",
					"{ASSEMBLY} to reference.",
					v => opts.AssemblyReferences.Add (v) },
				{ "sdk-platform|api-level=",
					"SDK Platform {VERSION}/API level.",
					v => opts.ApiLevel = v },
				{ "preserve-enums",
					"For internal use.",
					v => opts.PreserveEnums = v != null },
				{ "use-short-file-names",
					"Generates short file name.",
					v => opts.UseShortFileNames = v != null },
				{ "product-version=",
					"Xamarin.Android Major Product Version",
					(int? v) => opts.ProductVersion = v.HasValue ? v.Value : 0 },
				{ "v:",
					"Logging Verbosity",
					(int? v) => Report.Verbosity = v.HasValue ? v.Value : (Report.Verbosity ?? 0) + 1 },
				{ "type-map-report=",
					"Java-Managed Mapping report file.",
					v => opts.MappingReportFile = v },
				{ "only-xml-adjuster",
					"Run only API XML adjuster for class-parse input.",
					v => opts.OnlyRunApiXmlAdjuster = v != null },
				{ "xml-adjuster-output=",
					"specify API XML adjuster output XML for class-parse input.",
					v => opts.ApiXmlAdjusterOutput = v },
				{ "h|?|help",
					"Show this message and exit.",
					v => show_help = v != null },
				"",
				"C# Enumeration Support:",
				{ "enumdir=",
					"{DIRECTORY} to write enumeration declarations.",
					v => opts.EnumOutputDirectory = v },
				{ "enumfields=",
					"For internal use.",
					v => opts.EnumFieldsMapFile = v },
				{ "enumflags=",
					"For internal use.",
					v => opts.EnumFlagsFile = v },
				{ "enummetadata=",
					"XML {FILENAME} to create.",
					v => opts.EnumMetadataOutputFile = v },
				{ "enummethods=",
					"For internal use.",
					v => opts.EnumMethodsMapFile = v },
				{ "apiversions=",
					"For internal use.",
					v => opts.ApiVersionsXmlFile = v },
				{ "annotations=",
					"For internal use.",
					v => opts.AnnotationsZipFiles.Add (v) },
			};

			var apis = parser.Parse (args);

			if (args.Length < 2 || show_help) {
				parser.WriteOptionDescriptions (Console.Out);
				return null;
			}

			if (apis.Count == 0)
				throw new InvalidOperationException ("A .xml file must be specified.");
			if (apis.Count != 1) {
				Console.Error.WriteLine ("generator: Found {0} API descriptions; only one is supported", apis.Count);
				foreach (var api in apis) {
					Console.Error.WriteLine ("generator:   API description: {0}", api);
				}
				throw new InvalidOperationException ("Only one .xml file may be specified.");
			}

			opts.ApiDescriptionFile = apis [0];

			return opts;
		}

		static CodeGenerationTarget ParseCodeGenerationTarget (string value)
		{
			switch (value.ToLowerInvariant ()) {
			case "xamarinandroid":
				return CodeGenerationTarget.XamarinAndroid;
			case "xajavainterop1":
				return CodeGenerationTarget.XAJavaInterop1;
			case "javainterop1":
				return CodeGenerationTarget.JavaInterop1;
			}
			throw new NotSupportedException ($"Don't know how to convert '{value}' to a CodeGenerationTarget value!");
		}
	}

	public class CodeGenerator  {

		static void ShowUsage ()
		{
				Console.Error.WriteLine ("Usage: generator [--api-level=<num>] [--product-version=<num>] [--csdir=<dir>] [--javadir=<dir>] [--assembly=<fullqualassmname>] [--ref=<assembly>] [--global] [--public] [--external] [--fixup=xmlfile] [--enumfields=map.csv] [--enummethods=methodmap.csv] [--enumdir=dir] [--enummetadata=file] <filename>");
		}

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

			using (var resolver = new DirectoryAssemblyResolver (Console.WriteLine, loadDebugSymbols: false)) {
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
				ProductVersion        = options.ProductVersion
			};

			// Load reference libraries

			foreach (var lib in options.LibraryPaths) {
				resolver.SearchDirectories.Add (lib);
			}
			foreach (var reference in references) {
				resolver.SearchDirectories.Add (Path.GetDirectoryName (reference));
			}
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
			string apiXmlFile = filename;

			string apiSourceAttr = null;
			using (var xr = XmlReader.Create (filename)) {
				xr.MoveToContent ();
				apiSourceAttr = xr.GetAttribute ("api-source");
			}
			if (apiSourceAttr == "class-parse") {
				apiXmlFile = api_xml_adjuster_output ?? Path.Combine (Path.GetDirectoryName (filename), Path.GetFileName (filename) + ".adjusted");
				new Adjuster ().Process (filename, SymbolTable.AllRegisteredSymbols ().OfType<GenBase> ().ToArray (), apiXmlFile, Report.Verbosity ?? 0);
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

			Parser p = new Parser ();
			List<GenBase> gens = p.Parse (apiXmlFile, fixups, api_level, product_version);
			if (gens == null) {
				return;
			}
			apiSource = p.ApiSource;
			opt.Gens = gens;

			// disable interface default methods here, especially before validation.
			gens = gens.Where (g => !g.IsObfuscated && g.Visibility != "private").ToList ();
			foreach (var gen in gens) {
				gen.StripNonBindables ();
				if (gen.IsGeneratable)
					AddTypeToTable (gen);
			}

			Validate (gens, opt);

			if (api_versions_xml != null)
				ApiVersionsSupport.AssignApiLevels (gens, api_versions_xml);

			foreach (GenBase gen in gens)
				gen.FillProperties ();

			foreach (var gen in gens)
				gen.UpdateEnums (opt);

			foreach (GenBase gen in gens)
				gen.FixupMethodOverrides ();

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

		static void AddTypeToTable (GenBase gb)
		{
			SymbolTable.AddType (gb);
			foreach (var nt in gb.NestedTypes)
				AddTypeToTable (nt);
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

		static void Validate (List<GenBase> gens, CodeGenerationOptions opt)
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
					gen.FixupAccessModifiers ();
				foreach (GenBase gen in gens)
					if ((opt.IgnoreNonPublicType &&
					    (gen.RawVisibility != "public" && gen.RawVisibility != "internal"))
					    || !gen.Validate (opt, null)) {
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
		static void ProcessReferencedType (TypeDefinition td, CodeGenerationOptions opt)
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

			ISymbol gb = td.IsEnum ? (ISymbol) new EnumSymbol (td.FullNameCorrected ()) : td.IsInterface ? (ISymbol) new ManagedInterfaceGen (td) : new ManagedClassGen (td);
			SymbolTable.AddType (gb);

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

namespace MonoDroid.Generation {
	
	public class CodeGenerationOptions
	{
		Stack<GenBase> context_types = new Stack<GenBase> ();
		public Stack<GenBase> ContextTypes {
			get { return context_types; }
		}
		public List<Method> ContextGeneratedMethods { get; set; } = new List<Method> ();
		public GenBase ContextType {
			get { return context_types.Any () ? context_types.Peek () : null; }
		}
		public Field ContextField { get; set; }
		string ContextFieldString { 
			get { return ContextField != null ? "in field " + ContextField.Name + " " : null; }
		}
		public MethodBase ContextMethod { get; set; }
		string ContextMethodString { 
			get { return ContextMethod != null ? "in method " + ContextMethod.Name + " " : null; }
		}
		string ContextTypeString {
			get { return ContextType != null ? "in managed type " + ContextType.FullName : null; }
		}
		public string ContextString {
			get { return ContextFieldString + ContextMethodString + ContextTypeString; }
		}

		CodeGenerationTarget    codeGenerationTarget;
		public      CodeGenerationTarget    CodeGenerationTarget    {
			get { return codeGenerationTarget; }
			set {
				switch (value) {
				case CodeGenerationTarget.XamarinAndroid:
					CodeGenerator   = new XamarinAndroidCodeGenerator ();
					break;
				case CodeGenerationTarget.XAJavaInterop1:
					CodeGenerator   = new XAJavaInteropCodeGenerator ();
					break;
				case CodeGenerationTarget.JavaInterop1:
					CodeGenerator   = new JavaInteropCodeGenerator ();
					break;
				default:
					throw new NotSupportedException ("Don't know what to do for target '" + value + "'.");
				}
				codeGenerationTarget    = value;
			}
		}

		internal    CodeGenerator           CodeGenerator           {get; private set;} = new XamarinAndroidCodeGenerator ();

		public bool UseGlobal { get; set; }
		public bool IgnoreNonPublicType { get; set; }
		public string AssemblyName { get; set; }
		public bool UseShortFileNames { get; set; }
		public IList<GenBase> Gens {get;set;}
		public int ProductVersion { get; set; }

		public string GetOutputName (string s)
		{
			if (s == "System.Void")
				return "void";
			if (s.StartsWith ("params "))
				return "params " + GetOutputName (s.Substring ("params ".Length));
			if (s.StartsWith ("global::"))
				Report.Error (Report.ErrorCodeGenerator + 0, null,  "Unexpected \"global::\" specification. This error happens if it is specified in the Metadata API fixup for example.");
			if (!UseGlobal)
				return s;
			int idx = s.IndexOf ('<');
			if (idx < 0) {
				if (s.IndexOf ('.') < 0)
					return s; // hack, to prevent things like global::int
				return "global::" + s;
			}
			int idx2 = s.LastIndexOf ('>');
			string sub = s.Substring (idx + 1, idx2 - idx - 1);
			var typeParams = new List<string> ();
			while (true) {
				int idx3 = sub.IndexOf ('<');
				int idx4 = sub.IndexOf (',');
				if (idx4 < 0) {
					typeParams.Add (GetOutputName (sub));
					break;
				} else if (idx3 < 0 || idx4 < idx3) { // more than one type params.
					typeParams.Add (GetOutputName (sub.Substring (0, idx4)));
					if (idx4 + 1 == sub.Length)
						break;
					sub = sub.Substring (idx4 + 1).Trim ();
				} else {
					typeParams.Add (GetOutputName (sub));
					break;
				}
			}
			return GetOutputName (s.Substring (0, idx)) + '<' + String.Join (", ", typeParams.ToArray ()) + '>';
		}

		CSharpCodeProvider code_provider = new CSharpCodeProvider ();

		public string GetSafeIdentifier (string name)
		{
			// NOTE: "partial" differs in behavior on macOS vs. Windows, Windows reports "partial" as a valid identifier
			//	This check ensures the same output on both platforms
			if (name == "partial")
				return name;

			// In the ideal world, it should not be applied twice.
			// Sadly that is not true in reality, so we need to exclude non-symbols
			// when replacing the argument name with a valid identifier.
			// (ReturnValue.ToNative() takes an argument which could be either an expression or mere symbol.)
			if (name.LastOrDefault () != ')' && !name.Contains ('.'))
				name = code_provider.IsValidIdentifier (name) ? name : name + '_';
			return name.Replace ('$', '_');
		}

		Dictionary<string,string> short_file_names = new Dictionary<string, string> ();

		public string GetFileName (string fullName)
		{
			if (!UseShortFileNames)
				return fullName;
			string s;
			if (short_file_names.TryGetValue (fullName, out s))
				return s;
			s = short_file_names.Count.ToString ();
			short_file_names [fullName] = s;
			return s;
		}
	}

	abstract class CodeGenerator {

		protected CodeGenerator ()
		{
		}

		internal    abstract    void    WriteClassHandle (ClassGen type,    TextWriter writer,    string indent,  CodeGenerationOptions opt,  bool    requireNew);

		internal    abstract    void    WriteClassHandle (InterfaceGen type,            TextWriter writer,    string indent,  CodeGenerationOptions opt,  string  declaringType);

		internal    abstract    void    WriteClassInvokerHandle (ClassGen type,         TextWriter writer,    string indent,  CodeGenerationOptions opt,  string  declaringType);
		internal    abstract    void    WriteInterfaceInvokerHandle (InterfaceGen type, TextWriter writer,    string indent,  CodeGenerationOptions opt,  string  declaringType);

		internal    abstract    void    WriteConstructorIdField (Ctor ctor, TextWriter writer,    string indent,  CodeGenerationOptions opt);
		internal    abstract    void    WriteConstructorBody (Ctor ctor,    TextWriter writer,    string indent,  CodeGenerationOptions opt, StringCollection call_cleanup);

		internal    abstract    void    WriteMethodIdField (Method method,  TextWriter writer,    string indent,  CodeGenerationOptions opt);
		internal    abstract    void    WriteMethodBody (Method method,     TextWriter writer,    string indent,  CodeGenerationOptions opt);

		internal    abstract    void    WriteFieldIdField (Field field,     TextWriter writer,    string indent,  CodeGenerationOptions opt);
		internal    abstract    void    WriteFieldGetBody (Field field,     TextWriter writer,    string indent,  CodeGenerationOptions opt, GenBase type);
		internal    abstract    void    WriteFieldSetBody (Field field,     TextWriter writer,    string indent,  CodeGenerationOptions opt, GenBase type);

		internal    virtual     void    WriteField (Field field,            TextWriter writer,    string indent,  CodeGenerationOptions opt, GenBase type)
		{
			if (field.IsEnumified)
				writer.WriteLine ("[global::Android.Runtime.GeneratedEnum]");
			if (field.NeedsProperty) {
				string fieldType = field.Symbol.IsArray ? "IList<" + field.Symbol.ElementType + ">" : opt.GetOutputName (field.Symbol.FullName);
				WriteFieldIdField (field, writer, indent, opt);
				writer.WriteLine ();
				writer.WriteLine ("{0}// Metadata.xml XPath field reference: path=\"{1}/field[@name='{2}']\"", indent, type.MetadataXPathReference, field.JavaName);
				writer.WriteLine ("{0}[Register (\"{1}\"{2})]", indent, field.JavaName, field.AdditionalAttributeString ());
				writer.WriteLine ("{0}{1} {2}{3} {4} {{", indent, field.Visibility, field.IsStatic ? "static " : String.Empty, fieldType, field.Name);
				writer.WriteLine ("{0}\tget {{", indent);
				WriteFieldGetBody (field, writer, indent + "\t\t", opt, type);
				writer.WriteLine ("{0}\t}}", indent);

				if (!field.IsConst) {
					writer.WriteLine ("{0}\tset {{", indent);
					WriteFieldSetBody (field, writer, indent + "\t\t", opt, type);
					writer.WriteLine ("{0}\t}}", indent);
				}
				writer.WriteLine ("{0}}}", indent);
			}
			else {
				writer.WriteLine ("{0}// Metadata.xml XPath field reference: path=\"{1}/field[@name='{2}']\"", indent, type.MetadataXPathReference, field.JavaName);
				writer.WriteLine ("{0}[Register (\"{1}\"{2})]", indent, field.JavaName, field.AdditionalAttributeString ());
				if (field.IsDeprecated)
					writer.WriteLine ("{0}[Obsolete (\"{1}\")]", indent, field.DeprecatedComment);
				if (field.Annotation != null)
					writer.WriteLine ("{0}{1}", indent, field.Annotation);

				// the Value complication is due to constant enum from negative integer value (C# compiler requires explicit parenthesis).
				writer.WriteLine ("{0}{1} const {2} {3} = ({2}) {4};", indent, field.Visibility, opt.GetOutputName (field.Symbol.FullName), field.Name, field.Value.Contains ('-') && field.Symbol.FullName.Contains ('.') ? '(' + field.Value + ')' : field.Value);
			}
		}

		#region "if you're changing this part, also change method in https://github.com/xamarin/xamarin-android/blob/master/src/Mono.Android.Export/CallbackCode.cs"
		public virtual void WriteMethodCallback (Method method, TextWriter writer, string indent, CodeGenerationOptions opt, GenBase type, string property_name, bool as_formatted = false)
		{
			string delegate_type = method.GetDelegateType ();
			writer.WriteLine ("{0}static Delegate {1};", indent, method.EscapedCallbackName);
			writer.WriteLine ("#pragma warning disable 0169");
			writer.WriteLine ("{0}static Delegate {1} ()", indent, method.ConnectorName);
			writer.WriteLine ("{0}{{", indent);
			writer.WriteLine ("{0}\tif ({1} == null)", indent, method.EscapedCallbackName);
			writer.WriteLine ("{0}\t\t{1} = JNINativeWrapper.CreateDelegate (({2}) n_{3});", indent, method.EscapedCallbackName, delegate_type, method.Name + method.IDSignature);
			writer.WriteLine ("{0}\treturn {1};", indent, method.EscapedCallbackName);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
			writer.WriteLine ("{0}static {1} n_{2} (IntPtr jnienv, IntPtr native__this{3})", indent, method.RetVal.NativeType, method.Name + method.IDSignature, method.Parameters.GetCallbackSignature (opt));
			writer.WriteLine ("{0}{{", indent);
			writer.WriteLine ("{0}\t{1} __this = global::Java.Lang.Object.GetObject<{1}> (jnienv, native__this, JniHandleOwnership.DoNotTransfer);", indent, opt.GetOutputName (type.FullName));
			foreach (string s in method.Parameters.GetCallbackPrep (opt))
				writer.WriteLine ("{0}\t{1}", indent, s);
			if (String.IsNullOrEmpty (property_name)) {
				string call = "__this." + method.Name + (as_formatted ? "Formatted" : String.Empty) + " (" + method.Parameters.GetCall (opt) + ")";
				if (method.IsVoid)
					writer.WriteLine ("{0}\t{1};", indent, call);
				else
					writer.WriteLine ("{0}\t{1} {2};", indent, method.Parameters.HasCleanup ? method.RetVal.NativeType + " __ret =" : "return", method.RetVal.ToNative (opt, call));
			} else {
				if (method.IsVoid)
					writer.WriteLine ("{0}\t__this.{1} = {2};", indent, property_name, method.Parameters.GetCall (opt));
				else
					writer.WriteLine ("{0}\t{1} {2};", indent, method.Parameters.HasCleanup ? method.RetVal.NativeType + " __ret =" : "return", method.RetVal.ToNative (opt, "__this." + property_name));
			}
			foreach (string cleanup in method.Parameters.GetCallbackCleanup (opt))
				writer.WriteLine ("{0}\t{1}", indent, cleanup);
			if (!method.IsVoid && method.Parameters.HasCleanup)
				writer.WriteLine ("{0}\treturn __ret;", indent);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ("#pragma warning restore 0169");
			writer.WriteLine ();
		}
		#endregion

		public void WriteMethodCustomAttributes (Method method, TextWriter writer, string indent)
		{
			if (method.GenericArguments != null && method.GenericArguments.Any ())
				writer.WriteLine ("{0}{1}", indent, method.GenericArguments.ToGeneratedAttributeString ());
			if (method.CustomAttributes != null)
				writer.WriteLine ("{0}{1}", indent, method.CustomAttributes);
			if (method.Annotation != null)
				writer.WriteLine ("{0}{1}", indent, method.Annotation);
		}

		public void WriteMethodExplicitInterfaceImplementation (Method method, TextWriter writer, string indent, CodeGenerationOptions opt, GenBase iface)
		{
			//writer.WriteLine ("// explicitly implemented method from " + iface.FullName);
			WriteMethodCustomAttributes (method, writer, indent);
			writer.WriteLine ("{0}{1} {2}.{3} ({4})", indent, opt.GetOutputName (method.RetVal.FullName), opt.GetOutputName (iface.FullName), method.Name, GenBase.GetSignature (method, opt));
			writer.WriteLine ("{0}{{", indent);
			writer.WriteLine ("{0}\treturn {1} ({2});", indent, method.Name, method.Parameters.GetCall (opt));
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		public void WriteMethodExplicitInterfaceInvoker (Method method, TextWriter writer, string indent, CodeGenerationOptions opt, GenBase iface)
		{
			//writer.WriteLine ("\t\t// explicitly implemented invoker method from " + iface.FullName);
			WriteMethodIdField (method, writer, indent, opt);
			writer.WriteLine ("{0}unsafe {1} {2}.{3} ({4})",
					indent, opt.GetOutputName (method.RetVal.FullName), opt.GetOutputName (iface.FullName), method.Name, GenBase.GetSignature (method, opt));
			writer.WriteLine ("{0}{{", indent);
			WriteMethodBody (method, writer, indent + "\t", opt);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		public void WriteMethodAbstractDeclaration (Method method, TextWriter writer, string indent, CodeGenerationOptions opt, InterfaceGen gen, GenBase impl)
		{
			if (method.RetVal.IsGeneric && gen != null) {
				WriteMethodCustomAttributes (method, writer, indent);
				writer.WriteLine ("{0}{1} {2}.{3} ({4})", indent, opt.GetOutputName (method.RetVal.FullName), opt.GetOutputName (gen.FullName), method.Name, GenBase.GetSignature (method, opt));
				writer.WriteLine ("{0}{{", indent);
				writer.WriteLine ("{0}\tthrow new NotImplementedException ();", indent);
				writer.WriteLine ("{0}}}", indent);
				writer.WriteLine ();
			} else {
				bool gen_as_formatted = method.IsReturnCharSequence;
				string name = method.AdjustedName;
				WriteMethodCallback (method, writer, indent, opt, impl, null, gen_as_formatted);
				if (method.DeclaringType.IsGeneratable)
					writer.WriteLine ("{0}// Metadata.xml XPath method reference: path=\"{1}\"", indent, method.GetMetadataXPathReference (method.DeclaringType));
				writer.WriteLine ("{0}[Register (\"{1}\", \"{2}\", \"{3}\"{4})]", indent, method.JavaName, method.JniSignature, method.ConnectorName, method.AdditionalAttributeString ());
				WriteMethodCustomAttributes (method, writer, indent);
				writer.WriteLine ("{0}{1} abstract {2} {3} ({4});", indent, method.Visibility, opt.GetOutputName (method.RetVal.FullName), name, GenBase.GetSignature (method, opt));
				writer.WriteLine ();

				if (gen_as_formatted || method.Parameters.HasCharSequence)
					WriteMethodStringOverload (method, writer, indent, opt);
			}

			WriteMethodAsyncWrapper (method, writer, indent, opt);
		}

		public void WriteMethodDeclaration (Method method, TextWriter writer, string indent, CodeGenerationOptions opt, GenBase type, string adapter)
		{
			if (method.DeclaringType.IsGeneratable)
				writer.WriteLine ("{0}// Metadata.xml XPath method reference: path=\"{1}\"", indent, method.GetMetadataXPathReference (method.DeclaringType));
			if (method.Deprecated != null)
				writer.WriteLine ("[Obsolete (@\"{0}\")]", method.Deprecated.Replace ("\"", "\"\""));
			if (method.IsReturnEnumified)
				writer.WriteLine ("{0}[return:global::Android.Runtime.GeneratedEnum]", indent);
			if (method.IsInterfaceDefaultMethod)
				writer.WriteLine ("{0}[global::Java.Interop.JavaInterfaceDefaultMethod]", indent);
			writer.WriteLine ("{0}[Register (\"{1}\", \"{2}\", \"{3}:{4}\"{5})]", indent, method.JavaName, method.JniSignature, method.ConnectorName, method.GetAdapterName (opt, adapter), method.AdditionalAttributeString ());
			WriteMethodCustomAttributes (method, writer, indent);
			writer.WriteLine ("{0}{1} {2} ({3});", indent, opt.GetOutputName (method.RetVal.FullName), method.AdjustedName, GenBase.GetSignature (method, opt));
			writer.WriteLine ();
		}

		public void WriteMethodEventDelegate (Method method, TextWriter writer, string indent, CodeGenerationOptions opt)
		{
			writer.WriteLine ("{0}public delegate {1} {2}EventHandler ({3});", indent, opt.GetOutputName (method.RetVal.FullName), method.Name, GenBase.GetSignature (method, opt));
			writer.WriteLine ();
		}

		// This is supposed to generate instantiated generic method output, but I don't think it is done yet.
		public void WriteMethodExplicitIface (Method method, TextWriter writer, string indent, CodeGenerationOptions opt, GenericSymbol gen)
		{
			writer.WriteLine ("{0}// This method is explicitly implemented as a member of an instantiated {1}", indent, gen.FullName);
			WriteMethodCustomAttributes (method, writer, indent);
			writer.WriteLine ("{0}{1} {2}.{3} ({4})", indent, opt.GetOutputName (method.RetVal.FullName), opt.GetOutputName (gen.Gen.FullName), method.Name, GenBase.GetSignature (method, opt));
			writer.WriteLine ("{0}{{", indent);
			Dictionary<string, string> mappings = new Dictionary<string, string> ();
			for (int i = 0; i < gen.TypeParams.Length; i++)
				mappings [gen.Gen.TypeParameters [i].Name] = gen.TypeParams [i].FullName;
			WriteMethodGenericBody (method, writer, indent + "\t", opt, null, String.Empty, mappings);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		void WriteMethodGenericBody (Method method, TextWriter writer, string indent, CodeGenerationOptions opt, string property_name, string container_prefix, Dictionary<string, string> mappings)
		{
			if (String.IsNullOrEmpty (property_name)) {
				string call = container_prefix + method.Name + " (" + method.Parameters.GetGenericCall (opt, mappings) + ")";
				writer.WriteLine ("{0}{1}{2};", indent, method.IsVoid ? String.Empty : "return ", method.RetVal.GetGenericReturn (opt, call, mappings));
			} else {
				if (method.IsVoid) // setter
					writer.WriteLine ("{0}{1} = {2};", indent, container_prefix + property_name, method.Parameters.GetGenericCall (opt, mappings));
				else // getter
					writer.WriteLine ("{0}return {1};", indent, method.RetVal.GetGenericReturn (opt, container_prefix + property_name, mappings));
			}
		}

		public void WriteMethodIdField (Method method, TextWriter writer, string indent, CodeGenerationOptions opt, bool invoker = false)
		{
			if (invoker) {
				writer.WriteLine ("{0}IntPtr {1};", indent, method.EscapedIdName);
				return;
			}
			WriteMethodIdField (method, writer, indent, opt);
		}

		public void WriteMethodInvoker (Method method, TextWriter writer, string indent, CodeGenerationOptions opt, GenBase type)
		{
			WriteMethodCallback (method, writer, indent, opt, type, null, method.IsReturnCharSequence);
			WriteMethodIdField (method, writer, indent, opt, invoker: true);
			writer.WriteLine ("{0}public unsafe {1}{2} {3} ({4})",
						  indent, method.IsStatic ? "static " : string.Empty, opt.GetOutputName (method.RetVal.FullName), method.AdjustedName, GenBase.GetSignature (method, opt));
			writer.WriteLine ("{0}{{", indent);
			WriteMethodInvokerBody (method, writer, indent + "\t", opt);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		public void WriteMethodInvokerBody (Method method, TextWriter writer, string indent, CodeGenerationOptions opt)
		{
			writer.WriteLine ("{0}if ({1} == IntPtr.Zero)", indent, method.EscapedIdName);
			writer.WriteLine ("{0}\t{1} = JNIEnv.GetMethodID (class_ref, \"{2}\", \"{3}\");", indent, method.EscapedIdName, method.JavaName, method.JniSignature);
			foreach (string prep in method.Parameters.GetCallPrep (opt))
				writer.WriteLine ("{0}{1}", indent, prep);
			method.Parameters.WriteCallArgs (writer, indent, opt, invoker: true);
			string env_method = "Call" + method.RetVal.CallMethodPrefix + "Method";
			string call = "JNIEnv." + env_method + " (" +
				opt.ContextType.GetObjectHandleProperty ("this") + ", " + method.EscapedIdName + method.Parameters.GetCallArgs (opt, invoker: true) + ")";
			if (method.IsVoid)
				writer.WriteLine ("{0}{1};", indent, call);
			else
				writer.WriteLine ("{0}{1}{2};", indent, method.Parameters.HasCleanup ? opt.GetOutputName (method.RetVal.FullName) + " __ret = " : "return ", method.RetVal.FromNative (opt, call, true));

			foreach (string cleanup in method.Parameters.GetCallCleanup (opt))
				writer.WriteLine ("{0}{1}", indent, cleanup);

			if (!method.IsVoid && method.Parameters.HasCleanup)
				writer.WriteLine ("{0}return __ret;", indent);
		}

		void WriteMethodStringOverloadBody (Method method, TextWriter writer, string indent, CodeGenerationOptions opt, bool haveSelf)
		{
			var call = new System.Text.StringBuilder ();
			foreach (Parameter p in method.Parameters) {
				string pname = p.Name;
				if (p.Type == "Java.Lang.ICharSequence") {
					pname = p.GetName ("jls_");
					writer.WriteLine ("{0}global::Java.Lang.String {1} = {2} == null ? null : new global::Java.Lang.String ({2});", indent, pname, p.Name);
				} else if (p.Type == "Java.Lang.ICharSequence[]" || p.Type == "params Java.Lang.ICharSequence[]") {
					pname = p.GetName ("jlca_");
					writer.WriteLine ("{0}global::Java.Lang.ICharSequence[] {1} = CharSequence.ArrayFromStringArray({2});", indent, pname, p.Name);
				}
				if (call.Length > 0)
					call.Append (", ");
				call.Append (pname);
			}
			writer.WriteLine ("{0}{1}{2}{3} ({4});", indent, method.RetVal.IsVoid ? String.Empty : opt.GetOutputName (method.RetVal.FullName) + " __result = ", haveSelf ? "self." : "", method.AdjustedName, call.ToString ());
			switch (method.RetVal.FullName) {
				case "void":
					break;
				case "Java.Lang.ICharSequence[]":
					writer.WriteLine ("{0}var __rsval = CharSequence.ArrayToStringArray (__result);", indent);
					break;
				case "Java.Lang.ICharSequence":
					writer.WriteLine ("{0}var __rsval = __result?.ToString ();", indent);
					break;
				default:
					writer.WriteLine ("{0}var __rsval = __result;", indent);
					break;
			}
			foreach (Parameter p in method.Parameters) {
				if (p.Type == "Java.Lang.ICharSequence")
					writer.WriteLine ("{0}{1}?.Dispose ();", indent, p.GetName ("jls_"));
				else if (p.Type == "Java.Lang.ICharSequence[]")
					writer.WriteLine ("{0}if ({1} != null) foreach (global::Java.Lang.String s in {1}) s?.Dispose ();", indent, p.GetName ("jlca_"));
			}
			if (!method.RetVal.IsVoid) {
				writer.WriteLine ($"{indent}return __rsval;");
			}
		}

		void WriteMethodStringOverload (Method method, TextWriter writer, string indent, CodeGenerationOptions opt)
		{
			string static_arg = method.IsStatic ? " static" : String.Empty;
			string ret = opt.GetOutputName (method.RetVal.FullName.Replace ("Java.Lang.ICharSequence", "string"));
			if (method.Deprecated != null)
				writer.WriteLine ("{0}[Obsolete (@\"{1}\")]", indent, method.Deprecated.Replace ("\"", "\"\"").Trim ());
			writer.WriteLine ("{0}{1}{2} {3} {4} ({5})", indent, method.Visibility, static_arg, ret, method.Name, GenBase.GetSignature (method, opt).Replace ("Java.Lang.ICharSequence", "string").Replace ("global::string", "string"));
			writer.WriteLine ("{0}{{", indent);
			WriteMethodStringOverloadBody (method, writer, indent + "\t", opt, false);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		public void WriteMethodExtensionOverload (Method method, TextWriter writer, string indent, CodeGenerationOptions opt, string selfType)
		{
			if (!method.CanHaveStringOverload)
				return;

			string ret = opt.GetOutputName (method.RetVal.FullName.Replace ("Java.Lang.ICharSequence", "string"));
			writer.WriteLine ();
			writer.WriteLine ("{0}public static {1} {2} (this {3} self, {4})",
					indent, ret, method.Name, selfType,
				GenBase.GetSignature (method, opt).Replace ("Java.Lang.ICharSequence", "string").Replace ("global::string", "string"));
			writer.WriteLine ("{0}{{", indent);
			WriteMethodStringOverloadBody (method, writer, indent + "\t", opt, true);
			writer.WriteLine ("{0}}}", indent);
		}

		public void WriteMethodAsyncWrapper (Method method, TextWriter writer, string indent, CodeGenerationOptions opt)
		{
			if (!method.Asyncify)
				return;

			string static_arg = method.IsStatic ? " static" : String.Empty;
			string ret;

			if (method.IsVoid)
				ret = "global::System.Threading.Tasks.Task";
			else
				ret = "global::System.Threading.Tasks.Task<" + opt.GetOutputName (method.RetVal.FullName) + ">";

			writer.WriteLine ("{0}{1}{2} {3} {4}Async ({5})", indent, method.Visibility, static_arg, ret, method.AdjustedName, GenBase.GetSignature (method, opt));
			writer.WriteLine ("{0}{{", indent);
			writer.WriteLine ("{0}\treturn global::System.Threading.Tasks.Task.Run (() => {1} ({2}));", indent, method.AdjustedName, method.Parameters.GetCall (opt));
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		public void WriteMethodExtensionAsyncWrapper (Method method, TextWriter writer, string indent, CodeGenerationOptions opt, string selfType)
		{
			if (!method.Asyncify)
				return;

			string ret;

			if (method.IsVoid)
				ret = "global::System.Threading.Tasks.Task";
			else
				ret = "global::System.Threading.Tasks.Task<" + opt.GetOutputName (method.RetVal.FullName) + ">";

			writer.WriteLine ("{0}public static {1} {2}Async (this {3} self{4}{5})", indent, ret, method.AdjustedName, selfType, method.Parameters.Count > 0 ? ", " : string.Empty, GenBase.GetSignature (method, opt));
			writer.WriteLine ("{0}{{", indent);
			writer.WriteLine ("{0}\treturn global::System.Threading.Tasks.Task.Run (() => self.{1} ({2}));", indent, method.AdjustedName, method.Parameters.GetCall (opt));
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();
		}

		public void WriteMethod (Method method, TextWriter writer, string indent, CodeGenerationOptions opt, GenBase type, bool generate_callbacks)
		{
			if (!method.IsValid)
				return;

			bool gen_as_formatted = method.IsReturnCharSequence;
			if (generate_callbacks && method.IsVirtual)
				WriteMethodCallback (method, writer, indent, opt, type, null, gen_as_formatted);

			string name_and_jnisig = method.JavaName + method.JniSignature.Replace ("java/lang/CharSequence", "java/lang/String");
			bool gen_string_overload = !method.IsOverride && method.Parameters.HasCharSequence && !type.ContainsMethod (name_and_jnisig);

			string static_arg = method.IsStatic ? " static" : String.Empty;
			string virt_ov = method.IsOverride ? " override" : method.IsVirtual ? " virtual" : String.Empty;
			if ((string.IsNullOrEmpty (virt_ov) || virt_ov == " virtual") && type.RequiresNew (method.AdjustedName)) {
				virt_ov = " new" + virt_ov;
			}
			string seal = method.IsOverride && method.IsFinal ? " sealed" : null;
			string ret = opt.GetOutputName (method.RetVal.FullName);
			WriteMethodIdField (method, writer, indent, opt);
			if (method.DeclaringType.IsGeneratable)
				writer.WriteLine ("{0}// Metadata.xml XPath method reference: path=\"{1}\"", indent, method.GetMetadataXPathReference (method.DeclaringType));
			if (method.Deprecated != null)
				writer.WriteLine ("{0}[Obsolete (@\"{1}\")]", indent, method.Deprecated.Replace ("\"", "\"\""));
			if (method.IsReturnEnumified)
				writer.WriteLine ("{0}[return:global::Android.Runtime.GeneratedEnum]", indent);
			writer.WriteLine ("{0}[Register (\"{1}\", \"{2}\", \"{3}\"{4})]",
				indent, method.JavaName, method.JniSignature, method.IsVirtual ? method.ConnectorName : String.Empty, method.AdditionalAttributeString ());
			WriteMethodCustomAttributes (method, writer, indent);
			writer.WriteLine ("{0}{1}{2}{3}{4} unsafe {5} {6} ({7})", indent, method.Visibility, static_arg, virt_ov, seal, ret, method.AdjustedName, GenBase.GetSignature (method, opt));
			writer.WriteLine ("{0}{{", indent);
			WriteMethodBody (method, writer, indent + "\t", opt);
			writer.WriteLine ("{0}}}", indent);
			writer.WriteLine ();

			//NOTE: Invokers are the only place false is passed for generate_callbacks, they do not need string overloads
			if (generate_callbacks && (gen_string_overload || gen_as_formatted))
				WriteMethodStringOverload (method, writer, indent, opt);

			WriteMethodAsyncWrapper (method, writer, indent, opt);
		}
	}
}

