using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Xml;
using Mono.Cecil;
using Mono.Options;
using MonoDroid.Generation;
using Xamarin.Android.Binder;
using Xamarin.AndroidTools.AnnotationSupport;
using Xamarin.Android.Tools.ApiXmlAdjuster;

using Java.Interop.Tools.Cecil;

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
			if (apis.Count != 1)
				throw new InvalidOperationException ("Only one .xml file may be specified.");

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

			string assembly         = options.AssemblyQualifiedName;
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
			var apiSource           = "";
			var opt                 = new CodeGenerationOptions () {
				CodeGenerationTarget  = options.CodeGenerationTarget,
				UseGlobal             = options.GlobalTypeNames,
				IgnoreNonPublicType   = true,
				UseShortFileNames     = options.UseShortFileNames,
				ProductVersion        = options.ProductVersion
			};

			// Load reference libraries

			var resolver = new DirectoryAssemblyResolver (Console.WriteLine, loadDebugSymbols: false);
			foreach (var lib in options.LibraryPaths) {
				resolver.SearchDirectories.Add (lib);
			}
			foreach (var reference in references) {
				resolver.SearchDirectories.Add (Path.GetDirectoryName (reference));
			}
			foreach (var reference in references) {
				try {
					Report.Verbose (0, "resolving assembly {0}.", reference);
					var ass = resolver.Load (reference);
					foreach (var md in ass.Modules)
						foreach (var td in md.Types) {
							// FIXME: at some stage we want to import generic types.
							// For now generator fails to load generic types that have conflicting type e.g.
							// AdapterView`1 and AdapterView cannot co-exist.
							// It is mostly because generator primarily targets jar (no real generics land).
							if (td.HasGenericParameters &&
							    md.GetType (td.FullName.Substring (0, td.FullName.IndexOf ('`'))) != null)
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
				apiXmlFile = Path.Combine (Path.GetDirectoryName (filename), Path.GetFileName (filename) + ".adjusted");
				new Adjuster ().Process (filename, SymbolTable.AllRegisteredSymbols ().OfType<GenBase> ().ToArray (), apiXmlFile);
			}

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

			Validate (gens, opt);

			if (api_versions_xml != null)
				ApiVersionsSupport.AssignApiLevels (gens, api_versions_xml, api_level);

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

			GenerationInfo gen_info = new GenerationInfo (csdir, javadir, assembly);
			opt.AssemblyName = gen_info.Assembly;

			if (mapping_file != null)
				GenerateMappingReportFile (gens, mapping_file);

			new NamespaceMapping (gens).Generate (opt, gen_info);

			foreach (IGeneratable gen in gens)
				gen.Generate (opt, gen_info);

			ClassGen.GenerateTypeRegistrations (opt, gen_info);
			ClassGen.GenerateEnumList (gen_info);

			// Create the .cs files for the enums
			var enumFiles = enums == null
				? null
				: enummap.WriteEnumerations (enumdir, enums, FlattenNestedTypes (gens).ToArray (), opt.UseShortFileNames);

			gen_info.GenerateLibraryProjectFile (options, enumFiles);
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

#if USE_CECIL
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
#endif

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
				return "params " + GetOutputName (s.Substring (6));
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

		public string GetSafeIdentifier (string name)
		{
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

		internal    abstract    void    WriteClassHandle (ClassGen type,    StreamWriter sw,    string indent,  CodeGenerationOptions opt,  bool    requireNew);

		internal    abstract    void    WriteClassHandle (InterfaceGen type,            StreamWriter sw,    string indent,  CodeGenerationOptions opt,  string  declaringType);

		internal    abstract    void    WriteClassInvokerHandle (ClassGen type,         StreamWriter sw,    string indent,  CodeGenerationOptions opt,  string  declaringType);
		internal    abstract    void    WriteInterfaceInvokerHandle (InterfaceGen type, StreamWriter sw,    string indent,  CodeGenerationOptions opt,  string  declaringType);

		internal    abstract    void    WriteConstructorIdField (Ctor ctor, StreamWriter sw,    string indent,  CodeGenerationOptions opt);
		internal    abstract    void    WriteConstructorBody (Ctor ctor,    StreamWriter sw,    string indent,  CodeGenerationOptions opt, StringCollection call_cleanup);

		internal    abstract    void    WriteMethodIdField (Method method,  StreamWriter sw,    string indent,  CodeGenerationOptions opt);
		internal    abstract    void    WriteMethodBody (Method method,     StreamWriter sw,    string indent,  CodeGenerationOptions opt);

		internal    abstract    void    WriteFieldIdField (Field field,     StreamWriter sw,    string indent,  CodeGenerationOptions opt);
		internal    abstract    void    WriteFieldGetBody (Field field,     StreamWriter sw,    string indent,  CodeGenerationOptions opt);
		internal    abstract    void    WriteFieldSetBody (Field field,     StreamWriter sw,    string indent,  CodeGenerationOptions opt);
	}
}

