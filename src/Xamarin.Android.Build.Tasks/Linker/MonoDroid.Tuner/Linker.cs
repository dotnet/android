using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.XPath;
using Mono.Linker;
using Mono.Linker.Steps;
using Mono.Cecil.Mdb;
using Mono.Tuner;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoTouch.Tuner;

namespace MonoDroid.Tuner
{
	class Linker
	{
		public static void Process (LinkerOptions options, ILogger logger, out LinkContext context)
		{
			var pipeline = CreatePipeline (options);

			pipeline.PrependStep (new ResolveFromAssemblyStep (options.MainAssembly));
			if (options.RetainAssemblies != null)
				foreach (var assembly in options.RetainAssemblies)
					pipeline.PrependStep (new ResolveFromAssemblyStep (assembly));

			context = CreateLinkContext (options, pipeline, logger);
			context.Resolver.AddSearchDirectory (options.OutputDirectory);

			Run (pipeline, context);
		}

		static void Run (Pipeline pipeline, LinkContext context)
		{
			pipeline.Process (context);
		}

		static LinkContext CreateLinkContext (LinkerOptions options, Pipeline pipeline, ILogger logger)
		{
			var context = new AndroidLinkContext (pipeline, options.Resolver);
			if (options.DumpDependencies) {
				var prepareDependenciesDump = context.Annotations.GetType ().GetMethod ("PrepareDependenciesDump", new Type[] {});
				if (prepareDependenciesDump != null)
					prepareDependenciesDump.Invoke (context.Annotations, null);
			}
			context.LogMessages = true;
			context.Logger = logger;
			context.CoreAction = AssemblyAction.Link;
			context.UserAction = AssemblyAction.Link;
			context.LinkSymbols = true;
			context.SymbolReaderProvider = new DefaultSymbolReaderProvider (true);
			context.SymbolWriterProvider = new DefaultSymbolWriterProvider ();
			context.OutputDirectory = options.OutputDirectory;
			context.PreserveJniMarshalMethods = options.PreserveJniMarshalMethods;
			return context;
		}

		static Pipeline CreatePipeline (LinkerOptions options)
		{
			var pipeline = new Pipeline ();

			if (options.LinkNone) {
				pipeline.AppendStep (new FixAbstractMethodsStep ());
				pipeline.AppendStep (new OutputStep ());
				return pipeline;
			}

			pipeline.AppendStep (new LoadReferencesStep ());

			if (options.I18nAssemblies != I18nAssemblies.None)
				pipeline.AppendStep (new LoadI18nAssemblies (options.I18nAssemblies));

			pipeline.AppendStep (new BlacklistStep ());
			
			foreach (var desc in options.LinkDescriptions)
				pipeline.AppendStep (new ResolveFromXmlStep (new XPathDocument (desc)));

			pipeline.AppendStep (new CustomizeActions (options.LinkSdkOnly, options.SkippedAssemblies));

			pipeline.AppendStep (new TypeMapStep ());

			// monodroid tuner steps
			pipeline.AppendStep (new SubStepDispatcher {
				new ApplyPreserveAttribute (),
			});
			pipeline.AppendStep (new SubStepDispatcher {
				new PreserveExportedTypes (),
				new RemoveSecurity (),
				new MarkJavaObjects (),
				new PreserveJavaExceptions (),
				new PreserveJavaTypeRegistrations (),
				new PreserveApplications (),
				new RemoveAttributes (),
				new PreserveDynamicTypes (),
				new PreserveHttpAndroidClientHandler { HttpClientHandlerType = options.HttpClientHandlerType },
				new PreserveTlsProvider { TlsProvider = options.TlsProvider },
				new PreserveSoapHttpClients (),
				new PreserveTypeConverters (),
				new PreserveLinqExpressions (),
				new PreserveRuntimeSerialization (),
			});

			pipeline.AppendStep (new PreserveCrypto ());
			pipeline.AppendStep (new PreserveCode ());

			pipeline.AppendStep (new RemoveResources (options.I18nAssemblies)); // remove collation tables
			// end monodroid specific

			pipeline.AppendStep (new FixAbstractMethodsStep ());
			pipeline.AppendStep (new MonoDroidMarkStep ());
			pipeline.AppendStep (new SweepStep ());
			pipeline.AppendStep (new CleanStep ());
			// monodroid tuner steps
			if (!string.IsNullOrWhiteSpace (options.ProguardConfiguration))
				pipeline.AppendStep (new GenerateProguardConfiguration (options.ProguardConfiguration));
			pipeline.AppendStep (new StripEmbeddedLibraries ());
			// end monodroid specific
			pipeline.AppendStep (new RegenerateGuidStep ());
			pipeline.AppendStep (new OutputStep ());

			return pipeline;
		}

		static List<string> ListAssemblies (LinkContext context)
		{
			var list = new List<string> ();
			foreach (var assembly in context.GetAssemblies ()) {
				if (context.Annotations.GetAction (assembly) == AssemblyAction.Delete)
					continue;

				list.Add (GetFullyQualifiedName (assembly));
			}

			return list;
		}

		static string GetFullyQualifiedName (AssemblyDefinition assembly)
		{
			return assembly.MainModule.FullyQualifiedName;
		}

		public static I18nAssemblies ParseI18nAssemblies (string i18n)
		{
			if (string.IsNullOrWhiteSpace (i18n))
				return I18nAssemblies.None;

			var assemblies = I18nAssemblies.None;

			foreach (var part in i18n.Split (new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)) {
				var assembly = part.Trim ();
				if (string.IsNullOrEmpty (assembly))
					continue;

				try {
					assemblies |= (I18nAssemblies) Enum.Parse (typeof (I18nAssemblies), assembly, true);
				} catch {
					throw new FormatException ("Unknown value for i18n: " + assembly);
				}
			}

			return assemblies;
		}
	}
}
