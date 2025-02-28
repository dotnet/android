#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.JavaCallableWrappers.Adapters;
using Java.Interop.Tools.JavaCallableWrappers.CallableWrapperMembers;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
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

	[Required]
	public ITaskItem [] LegacyGeneratedJavaFiles { get; set; } = [];

	List<string> GeneratedJavaFiles = [];

	public override bool RunTask ()
	{
		JavaNativeTypeManager.PackageNamingPolicy = Enum.TryParse (PackageNamingPolicy, out PackageNamingPolicyEnum pnp) ? pnp : PackageNamingPolicyEnum.LowercaseCrc64;

		// Get the set of assemblies for the "first" ABI. JavaCallableWrappers are
		// not ABI-specific, so we can use any ABI to generate the wrappers.
		var allAssembliesPerArch = MonoAndroidHelper.GetPerArchAssemblies (ResolvedAssemblies, SupportedAbis, validate: true);
		var singleArchAssemblies = allAssembliesPerArch.First ().Value.Values.ToList ();

		GenerateWrappers (singleArchAssemblies);

		//EnsureSameFilesWritten ();

		return !Log.HasLoggedErrors;
	}

	void GenerateWrappers (List<ITaskItem> assemblies)
	{
		Directory.CreateDirectory (OutputDirectory);

		var sw = Stopwatch.StartNew ();
		// Deserialize JavaCallableWrappers
		var wrappers = new List<CallableWrapperType> ();

		foreach (var assembly in assemblies) {
			var assemblyPath = assembly.ItemSpec;
			var assemblyName = Path.GetFileNameWithoutExtension (assemblyPath);
			var wrappersPath = Path.Combine (Path.GetDirectoryName (assemblyPath), $"{assemblyName}.jlo.xml");

			if (!File.Exists (wrappersPath)) {
				Log.LogError ($"'{wrappersPath}' not found.");
				return;
			}

			wrappers.AddRange (XmlImporter.Import (wrappersPath, out var _));
		}
		Log.LogDebugMessage ($"Deserialized Java callable wrappers in: '{sw.ElapsedMilliseconds}ms'");

		sw.Restart ();
		var writer_options = new CallableWrapperWriterOptions {
			CodeGenerationTarget = MonoAndroidHelper.ParseCodeGenerationTarget (CodeGenerationTarget)
		};

		foreach (var generator in wrappers) {
			using var writer = MemoryStreamPool.Shared.CreateStreamWriter ();

			generator.Generate (writer, writer_options);
			writer.Flush ();


			var path = generator.GetDestinationPath (OutputDirectory);

			//if (Files.HasStreamChanged (writer.BaseStream, path)) {
			//	Files.CopyIfStreamChanged (writer.BaseStream, path.Replace (@"\src\", @"\src2\"));
			//	Log.LogError ($"Java callable wrapper code changed: '{path}'");
			//	continue;
			//}

			var changed = Files.CopyIfStreamChanged (writer.BaseStream, path);
			Log.LogDebugMessage ($"*NEW* Generated Java callable wrapper code: '{path}' (changed: {changed})");

			//if (changed)
			//	Log.LogError ($"Java callable wrapper code changed: '{path}'");

			GeneratedJavaFiles.Add (path);
		}
		Log.LogDebugMessage ($"Wrote Java callable wrappers in: '{sw.ElapsedMilliseconds}ms'");
	}

	void EnsureSameFilesWritten ()
	{
		var new_generated_java_files = GeneratedJavaFiles.Select (f => f.Replace ("src2", "src")).ToList ();
		var old_generated_java_files = LegacyGeneratedJavaFiles.Select (f => f.ItemSpec).ToList ();

		var extra_new_files = new_generated_java_files.Except (old_generated_java_files).ToList ();

		if (extra_new_files.Count > 0)
			Log.LogWarning ($"The following Java files were generated but not previously generated: {string.Join (", ", extra_new_files)}");

		var missing_old_files = old_generated_java_files.Except (new_generated_java_files).ToList ();

		if (missing_old_files.Count > 0)
			Log.LogWarning ($"The following Java files were previously generated but not generated this time: {string.Join (", ", missing_old_files)}");

		if (extra_new_files.Count > 0 || missing_old_files.Count > 0) {
			Log.LogError ($"New JCW gen ({new_generated_java_files.Count}) mismatch with old JCW gen ({old_generated_java_files.Count})");
		}
	}
}
