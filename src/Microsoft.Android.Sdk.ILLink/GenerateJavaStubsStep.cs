#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Java.Interop.Tools.Diagnostics;
using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.JavaCallableWrappers.Adapters;
using Microsoft.Android.Build.Tasks;
using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

#if ILLINK
using Resources = Microsoft.Android.Sdk.ILLink.Properties.Resources;
#else
using Resources = Xamarin.Android.Tasks.Properties.Resources;
#endif

namespace Microsoft.Android.Sdk.ILLink;

/// <summary>
/// A trimmer step for generating *.java stubs
/// </summary>
public class GenerateJavaStubsStep : BaseStep
{
#if ILLINK
	bool IsInitialized => cache is not null && resolver is not null;

	void Initialize ()
	{
		cache = Context;
		resolver = (IAssemblyResolver)Context;
		if (Context.TryGetCustomData ("ApplicationJavaClass", out string applicationJavaClass)) {
			ApplicationJavaClass = applicationJavaClass;
		}
		if (Context.TryGetCustomData ("AndroidUseMarshalMethods", out string mm) &&
				bool.TryParse (mm, out bool androidUseMarshalMethods)) {
			EnableMarshalMethods = androidUseMarshalMethods;
		}
		if (Context.TryGetCustomData ("AndroidIntermediateOutputPath", out string androidIntermediateOutputPath)) {
			OutputDirectory = Path.Combine (androidIntermediateOutputPath, "android", "src");
		}
		if (Context.TryGetCustomData ("AndroidNativeAot", out string naot) &&
				bool.TryParse (naot, out bool androidNativeAot)) {
			NativeAot = androidNativeAot;
		}
		if (Context.TryGetCustomData ("RuntimeIdentifier", out string runtimeIdentifier)) {
			AndroidTargetArch = MonoAndroidHelper.RidToArch (runtimeIdentifier);
		}
	}
#else // !ILLINK
	public GenerateJavaStubsStep (IAssemblyResolver resolver, IMetadataResolver cache)
	{
		this.resolver = resolver;
		this.cache = cache;
	}
#endif  // !ILLINK

#if !ILLINK
	readonly
#endif
	IMetadataResolver cache;

#if !ILLINK
	readonly
#endif
	IAssemblyResolver resolver;

	public string ApplicationJavaClass { get; set; } = "";

	public bool EnableMarshalMethods { get; set; }

	public AndroidTargetArch AndroidTargetArch { get; set; }

	public bool NativeAot { get; set; }

	public string OutputDirectory { get; set; } = "";

	protected override void ProcessAssembly (AssemblyDefinition assembly)
	{
#if ILLINK
		// Call Initialize() on first assembly
		if (!IsInitialized)
			Initialize ();
#endif

		if (Annotations?.GetAction (assembly) == AssemblyAction.Delete)
			return;
		if (!assembly.MainModule.HasTypeReference ("Java.Lang.Object"))
			return;

		GenerateJavaStubs (assembly);
	}

	public void GenerateJavaStubs (AssemblyDefinition assembly)
	{
		foreach (var type in assembly.MainModule.Types) {
			ProcessType (assembly, type);
		}
	}

	void ProcessType (AssemblyDefinition assembly, TypeDefinition type)
	{
		if (!HasJavaPeer (type, cache))
			return;
		if (JavaTypeScanner.ShouldSkipJavaCallableWrapperGeneration (type, cache))
			return;

		// Interfaces are in typemap but they shouldn't have JCW generated for them
		if (type.IsInterface)
			return;

		JavaCallableMethodClassifier? classifier = null;
		if (EnableMarshalMethods) {
			var arch = 
			classifier = new MarshalMethodsClassifier (AndroidTargetArch, cache, resolver, LogMessage, LogWarning);
		}

		var reader_options = new CallableWrapperReaderOptions {
			DefaultApplicationJavaClass         = ApplicationJavaClass,
			DefaultGenerateOnCreateOverrides    = false, // this was used only when targetting Android API <= 10, which is no longer supported
			DefaultMonoRuntimeInitialization    = "mono.MonoPackageManager.LoadApplication (context);",
			MethodClassifier                    = classifier,
		};

		var writer_options = new CallableWrapperWriterOptions {
			CodeGenerationTarget  = NativeAot ? JavaPeerStyle.JavaInterop1 : JavaPeerStyle.XAJavaInterop1
		};

		var generator = CecilImporter.CreateType (type, cache, reader_options);
		using var writer = MemoryStreamPool.Shared.CreateStreamWriter ();

		try {
			generator.Generate (writer, writer_options);

			string path = generator.GetDestinationPath (OutputDirectory);
			Files.CopyIfStreamChanged (writer.BaseStream, path);

			if (generator.HasExport && !assembly.MainModule.AssemblyReferences.Any (r => r.Name == "Mono.Android.Export")) {
				Diagnostic.Error (4210, Resources.XA4210);
			}
		} catch (XamarinAndroidException xae) {
			Diagnostic.Error (xae.Code, xae.MessageWithoutCode, []);
		} catch (DirectoryNotFoundException ex) {
			if (OS.IsWindows) {
				Diagnostic.Error (5301, Resources.XA5301, type.FullName, ex);
			} else {
				Diagnostic.Error (4209, Resources.XA4209, type.FullName, ex);
			}
		} catch (Exception ex) {
			Diagnostic.Error (4209, Resources.XA4209, type.FullName, ex);
		}
	}

	static bool HasJavaPeer (TypeDefinition type, IMetadataResolver resolver)
	{
		if (type.IsInterface && ImplementsInterface (type, "Java.Interop.IJavaPeerable", resolver))
			return true;

		foreach (var t in GetTypeAndBaseTypes (type, resolver)) {
			switch (t.FullName) {
				case "Java.Lang.Object":
				case "Java.Lang.Throwable":
				case "Java.Interop.JavaObject":
				case "Java.Interop.JavaException":
					return true;
				default:
					break;
			}
		}
		return false;
	}

	static bool ImplementsInterface (TypeDefinition type, string interfaceName, IMetadataResolver resolver)
	{
		foreach (var t in GetTypeAndBaseTypes (type, resolver)) {
			foreach (var i in t.Interfaces) {
				if (i.InterfaceType.FullName == interfaceName) {
					return true;
				}
			}
		}
		return false;
	}

	static IEnumerable<TypeDefinition> GetTypeAndBaseTypes (TypeDefinition type, IMetadataResolver resolver)
	{
		TypeDefinition? t = type;

		while (t != null) {
			yield return t;
			t = GetBaseType (t, resolver);
		}
	}

	static TypeDefinition? GetBaseType (TypeDefinition type, IMetadataResolver resolver)
	{
		var bt = type.BaseType;
		if (bt == null)
			return null;
		return resolver.Resolve (bt);
	}

	public virtual void LogMessage (string message) => Context.LogMessage (message);

	public virtual void LogWarning (string message) =>
#if ILLINK
		Context.LogMessage (MessageContainer.CreateCustomWarningMessage (Context, message, 6200, new MessageOrigin (), WarnVersion.ILLink5));
#else   // !ILLINK
		Context.LogMessage (MessageImportance.High, message);
#endif  // !ILLINK
}
