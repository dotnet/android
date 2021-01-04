using System;
using System.Collections.ObjectModel;
using Mono.Cecil;
using Mono.Options;

using Java.Interop.Tools.JavaSource;

using MonoDroid.Generation;

namespace Xamarin.Android.Binder
{
	public class CodeGeneratorOptions
	{
		public CodeGeneratorOptions ()
		{
			ApiVersionsXmlFiles = new Collection<string> ();
			AssemblyReferences  = new Collection<string> ();
			FixupFiles          = new Collection<string> ();
			LibraryPaths        = new Collection<string> ();
			AnnotationsZipFiles = new Collection<string> ();
			JavadocXmlFiles     = new Collection<string> ();
		}

		public string               ApiLevel {get; set;}
		public CodeGenerationTarget CodeGenerationTarget {get; set;}
		public string               ManagedCallableWrapperSourceOutputDirectory {get; set;}
		public string               AssemblyQualifiedName {get; set;}
		public Collection<string>   AssemblyReferences {get; private set;}
		public Collection<string>   FixupFiles {get; private set;}
		public Collection<string>   LibraryPaths {get; private set;}
		public Collection<string>   JavadocXmlFiles {get; private set;}
		public bool                 GlobalTypeNames {get; set;}
		public bool                 OnlyBindPublicTypes {get; set;}
		public string               ApiDescriptionFile {get; set;}
		[Obsolete ("Use ApiVersionsXmlFiles")]
		public string               ApiVersionsXmlFile {get; set;}
		public Collection<string>   ApiVersionsXmlFiles {get; set;}
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
		public bool                 SupportInterfaceConstants { get; set; }
		public bool		    SupportDefaultInterfaceMethods { get; set; }
		public bool		    SupportNestedInterfaceTypes { get; set; }
		public bool		    SupportNullableReferenceTypes { get; set; }

		public XmldocStyle		    XmldocStyle { get; set; } = XmldocStyle.IntelliSense;

		public static CodeGeneratorOptions Parse (string[] args)
		{
			var opts = new CodeGeneratorOptions ();

			bool show_help = false;

			var parser = new OptionSet {
				"Usage: generator.exe OPTIONS+ API_DESCRIPTION [@RESPONSE-FILES]",
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
				{ "lang-features=",
					"For internal use. (Flags: interface-constants,default-interface-methods,nullable-reference-types)",
					v => {
						opts.SupportInterfaceConstants = v?.Contains ("interface-constants") == true;
						opts.SupportDefaultInterfaceMethods = v?.Contains ("default-interface-methods") == true;
						opts.SupportNestedInterfaceTypes = v?.Contains ("nested-interface-types") == true;
						opts.SupportNullableReferenceTypes = v?.Contains ("nullable-reference-types") == true;
						}},
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
				"Javadoc to C# Documentation Comments Support:",
				{ "doc-comment-verbosity=",
					"{STYLE} of C# documentation comments to emit.\n" +
					"Defaults to `full`.  {STYLE} may be:\n" +
					"  * `intellisense`: emit <summary>, <param>,\n" +
					"    <returns>, <exception>.\n" +
					"  * `full`: plus <remarks>, <seealso>, ...",
					v => opts.XmldocStyle = ParseXmldocStyle (v) },
				{ "with-javadoc-xml=",
					"{PATH} to `api.xml` containing Javadoc docs in\n`<javadoc/>` elements",
					v => opts.JavadocXmlFiles.Add (v) },
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
					v => opts.ApiVersionsXmlFiles.Add (v) },
				{ "annotations=",
					"For internal use.",
					v => opts.AnnotationsZipFiles.Add (v) },
				new ResponseFileSource (),
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

			if (opts.SupportDefaultInterfaceMethods && opts.CodeGenerationTarget == CodeGenerationTarget.XamarinAndroid) {
				Console.Error.WriteLine (Report.FormatCodedMessage (true, Report.ErrorInvalidDIMArgument));
				return null;
			}

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

		static XmldocStyle ParseXmldocStyle (string style) => style?.ToLowerInvariant () switch {
			"intellisense" => XmldocStyle.IntelliSense,
			"full" => XmldocStyle.Full,
			_ => XmldocStyle.Full,
		};
	}
}
