using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.Diagnostics;
using Java.Interop.Tools.JavaCallableWrappers;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class JCWGeneratorContext
{
	public bool UseMarshalMethods                    { get; }
	public AndroidTargetArch Arch                    { get; }
	public TypeDefinitionCache TypeDefinitionCache   { get; }
	public XAAssemblyResolverNew Resolver            { get; }
	public IList<JavaType> JavaTypes                 { get; }
	public ICollection<ITaskItem> ResolvedAssemblies { get; }

	public JCWGeneratorContext (AndroidTargetArch arch, XAAssemblyResolverNew res, ICollection<ITaskItem> resolvedAssemblies, List<JavaType> javaTypesForJCW, TypeDefinitionCache tdCache, bool useMarshalMethods)
	{
		Arch = arch;
		Resolver = res;
		ResolvedAssemblies = resolvedAssemblies;
		JavaTypes = javaTypesForJCW.AsReadOnly ();
		TypeDefinitionCache = tdCache;
		UseMarshalMethods = useMarshalMethods;
	}
}

class JCWGenerator
{
	readonly TaskLoggingHelper log;
	readonly JCWGeneratorContext context;

	public MarshalMethodsClassifier? Classifier { get; private set; }

	public JCWGenerator (TaskLoggingHelper log, JCWGeneratorContext context)
	{
		this.log = log;
		this.context = context;
	}

	/// <summary>
	/// Performs marshal method classification, if marshal methods are used, but does not generate any code.
	/// If marshal methods are used, this method will set the <see cref="Classifier"/> property to a valid
	/// classifier instance on return.  If marshal methods are disabled, this call is a no-op but it will
	/// return <c>true</c>.
	/// </summary>
	public bool Classify (string androidSdkPlatform)
	{
		if (!context.UseMarshalMethods) {
			return true;
		}

		Classifier = new MarshalMethodsClassifier (context.TypeDefinitionCache, context.Resolver, log);
		return ProcessTypes (
			generateCode: false,
			androidSdkPlatform,
			Classifier,
			outputPath: null,
			applicationJavaClass: null
		);
	}

	public bool GenerateAndClassify (string androidSdkPlatform, string outputPath, string applicationJavaClass)
	{
		if (context.UseMarshalMethods) {
			Classifier = new MarshalMethodsClassifier (context.TypeDefinitionCache, context.Resolver, log);
		}

		return ProcessTypes (
			generateCode: true,
			androidSdkPlatform,
			Classifier,
			outputPath,
			applicationJavaClass
		);
	}

	bool ProcessTypes (bool generateCode, string androidSdkPlatform, MarshalMethodsClassifier? classifier, string? outputPath, string? applicationJavaClass)
	{
		if (generateCode && String.IsNullOrEmpty (outputPath)) {
			throw new ArgumentException ("must not be null or empty", nameof (outputPath));
		}

		string monoInit = GetMonoInitSource (androidSdkPlatform);
		bool hasExportReference = context.ResolvedAssemblies.Any (assembly => Path.GetFileName (assembly.ItemSpec) == "Mono.Android.Export.dll");
		bool ok = true;

		foreach (JavaType jt in context.JavaTypes) {
			TypeDefinition type = jt.Type; // JCW generator doesn't care about ABI-specific types or token ids
			if (type.IsInterface) {
				// Interfaces are in typemap but they shouldn't have JCW generated for them
				continue;
			}

			JavaCallableWrapperGenerator generator = CreateGenerator (type, classifier, monoInit, hasExportReference, applicationJavaClass);
			if (!generateCode) {
				continue;
			}

			if (!GenerateCode (generator, type, outputPath, hasExportReference, classifier)) {
				ok = false;
			}
		}

		return ok;
	}

	bool GenerateCode (JavaCallableWrapperGenerator generator, TypeDefinition type, string outputPath, bool hasExportReference, MarshalMethodsClassifier? classifier)
	{
		bool ok = true;
		using var writer = MemoryStreamPool.Shared.CreateStreamWriter ();

		try {
			generator.Generate (writer);
			if (context.UseMarshalMethods) {
				if (classifier.FoundDynamicallyRegisteredMethods (type)) {
					log.LogWarning ($"Type '{type.GetAssemblyQualifiedName (context.TypeDefinitionCache)}' will register some of its Java override methods dynamically. This may adversely affect runtime performance. See preceding warnings for names of dynamically registered methods.");
				}
			}
			writer.Flush ();

			string path = generator.GetDestinationPath (outputPath);
			Files.CopyIfStreamChanged (writer.BaseStream, path);
			if (generator.HasExport && !hasExportReference) {
				Diagnostic.Error (4210, Properties.Resources.XA4210);
			}
		} catch (XamarinAndroidException xae) {
			ok = false;
			log.LogError (
				subcategory: "",
				errorCode: "XA" + xae.Code,
				helpKeyword: string.Empty,
				file: xae.SourceFile,
				lineNumber: xae.SourceLine,
				columnNumber: 0,
				endLineNumber: 0,
				endColumnNumber: 0,
				message: xae.MessageWithoutCode,
				messageArgs: Array.Empty<object> ()
			);
		} catch (DirectoryNotFoundException ex) {
			ok = false;
			if (OS.IsWindows) {
				Diagnostic.Error (5301, Properties.Resources.XA5301, type.FullName, ex);
			} else {
				Diagnostic.Error (4209, Properties.Resources.XA4209, type.FullName, ex);
			}
		} catch (Exception ex) {
			ok = false;
			Diagnostic.Error (4209, Properties.Resources.XA4209, type.FullName, ex);
		}

		return ok;
	}

	JavaCallableWrapperGenerator CreateGenerator (TypeDefinition type, MarshalMethodsClassifier? classifier, string monoInit, bool hasExportReference, string? applicationJavaClass)
	{
		return new JavaCallableWrapperGenerator (type, log.LogWarning, context.TypeDefinitionCache, classifier) {
			GenerateOnCreateOverrides = false, // this was used only when targetting Android API <= 10, which is no longer supported
			ApplicationJavaClass = applicationJavaClass,
			MonoRuntimeInitialization = monoInit,
		};
	}

	static string GetMonoInitSource (string androidSdkPlatform)
	{
		if (String.IsNullOrEmpty (androidSdkPlatform)) {
			throw new ArgumentException ("must not be null or empty", nameof (androidSdkPlatform));
		}

		// Lookup the mono init section from MonoRuntimeProvider:
		// Mono Runtime Initialization {{{
		// }}}
		var builder = new StringBuilder ();
		var runtime = "Bundled";
		var api = "";
		if (int.TryParse (androidSdkPlatform, out int apiLevel) && apiLevel < 21) {
			api = ".20";
		}

		var assembly = Assembly.GetExecutingAssembly ();
		using var s = assembly.GetManifestResourceStream ($"MonoRuntimeProvider.{runtime}{api}.java");
		using var reader = new StreamReader (s);
		bool copy = false;
		string? line;
		while ((line = reader.ReadLine ()) != null) {
			if (string.CompareOrdinal ("\t\t// Mono Runtime Initialization {{{", line) == 0) {
				copy = true;
			}

			if (copy) {
				builder.AppendLine (line);
			}

			if (string.CompareOrdinal ("\t\t// }}}", line) == 0) {
				break;
			}
		}

		return builder.ToString ();
	}
}
