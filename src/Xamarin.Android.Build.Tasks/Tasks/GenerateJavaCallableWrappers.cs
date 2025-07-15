
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.JavaCallableWrappers.CallableWrapperMembers;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using PackageNamingPolicyEnum = Java.Interop.Tools.TypeNameMappings.PackageNamingPolicy;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Transforms the XML files of JLOs found in the linker step into JavaCallableWrappers.
/// </summary>
public class GenerateJavaCallableWrappers : AndroidTask
{
	public override string TaskPrefix => "JCW";

	[Required]
	public string CodeGenerationTarget { get; set; } = string.Empty;

	[Required]
	public string OutputDirectory { get; set; } = string.Empty;

	[Required]
	public string PackageNamingPolicy { get; set; } = string.Empty;

	[Required]
	public ITaskItem [] ResolvedAssemblies { get; set; } = [];

	[Required]
	public string [] SupportedAbis { get; set; } = [];

	[Output]
	public ITaskItem [] GeneratedJavaFilesOutput { get; set; } = [];

	public override bool RunTask ()
	{
		JavaNativeTypeManager.PackageNamingPolicy = Enum.TryParse (PackageNamingPolicy, out PackageNamingPolicyEnum pnp) ? pnp : PackageNamingPolicyEnum.LowercaseCrc64;

		// Get the set of assemblies for the "first" ABI. JavaCallableWrappers are
		// not ABI-specific, so we can use any ABI to generate the wrappers.
		var allAssembliesPerArch = MonoAndroidHelper.GetPerArchAssemblies (ResolvedAssemblies, SupportedAbis, validate: true);
		var singleArchAssemblies = allAssembliesPerArch.First ().Value.Values.ToList ();

		GenerateWrappers (singleArchAssemblies);

		return !Log.HasLoggedErrors;
	}

	void GenerateWrappers (List<ITaskItem> assemblies)
	{
		Directory.CreateDirectory (OutputDirectory);

		// Deserialize JavaCallableWrappers XML files
		var wrappers = new List<CallableWrapperType> ();
		var sw = Stopwatch.StartNew ();

		foreach (var assembly in assemblies) {
			var wrappersPath = JavaObjectsXmlFile.GetJavaObjectsXmlFilePath (assembly.ItemSpec);

			if (!File.Exists (wrappersPath)) {
				Log.LogError ($"'{wrappersPath}' not found.");
				return;
			}

			var xml = JavaObjectsXmlFile.Import (wrappersPath, JavaObjectsXmlFileReadType.JavaCallableWrappers);

			if (xml.JavaCallableWrappers.Count == 0) {
				Log.LogDebugMessage ($"'{wrappersPath}' is empty, skipping.");
				continue;
			}

			wrappers.AddRange (xml.JavaCallableWrappers);
		}

		Log.LogDebugMessage ($"Deserialized {wrappers.Count} Java callable wrappers in {sw.ElapsedMilliseconds}ms");
		sw.Restart ();

		// Write JavaCallableWrappers to Java files
		var writer_options = new CallableWrapperWriterOptions {
			CodeGenerationTarget = MonoAndroidHelper.ParseCodeGenerationTarget (CodeGenerationTarget)
		};

		var generated_files = new List<ITaskItem> ();

		foreach (var generator in wrappers) {
			using var writer = MemoryStreamPool.Shared.CreateStreamWriter ();

			generator.Generate (writer, writer_options);
			writer.Flush ();

			var path = generator.GetDestinationPath (OutputDirectory);

			var changed = Files.CopyIfStreamChanged (writer.BaseStream, path);
			Log.LogDebugMessage ($"*NEW* Generated Java callable wrapper code: '{path}' (changed: {changed})");

			generated_files.Add (new TaskItem (path));
		}

		Log.LogDebugMessage ($"Generated {generated_files.Count} Java callable wrapper files in {sw.ElapsedMilliseconds}ms");
		GeneratedJavaFilesOutput = generated_files.ToArray ();
	}
}
