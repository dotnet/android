#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

// Linked into the legacy tasks as well: keep these assembly-list helpers netstandard2.0 compatible.
partial class MonoAndroidHelper
{
	public static string GetAssemblyAbi (ITaskItem asmItem)
	{
		string? abi = asmItem.GetMetadata ("Abi");
		if (String.IsNullOrEmpty (abi)) {
			throw new InvalidOperationException ($"Internal error: assembly '{asmItem}' lacks ABI metadata");
		}

		return abi;
	}

	public static AndroidTargetArch GetTargetArch (ITaskItem asmItem) => AbiToTargetArch (GetAssemblyAbi (asmItem));

	/// <summary>
	/// Groups assemblies by architecture, filtering to the supported ABIs and optionally
	/// checking that all architectures contain the same assembly names.
	/// </summary>
	public static Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> GetPerArchAssemblies (IEnumerable<ITaskItem> input, ICollection<string> supportedAbis, bool validate, Func<ITaskItem, bool>? shouldSkip = null)
	{
		var supportedTargetArches = new HashSet<AndroidTargetArch> ();
		foreach (string abi in supportedAbis) {
			supportedTargetArches.Add (AbiToTargetArch (abi));
		}

		return GetPerArchAssemblies (
			input,
			supportedTargetArches,
			validate,
			shouldSkip
		);
	}

	public static string GetAssemblyNameWithCulture (ITaskItem assemblyItem)
	{
		string name = Path.GetFileNameWithoutExtension (assemblyItem.ItemSpec);
		string? culture = assemblyItem.GetMetadata ("Culture");
		if (!String.IsNullOrEmpty (culture)) {
			return $"{culture}/{name}";
		}
		return name;
	}

	static Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> GetPerArchAssemblies (IEnumerable<ITaskItem> input, HashSet<AndroidTargetArch> supportedTargetArches, bool validate, Func<ITaskItem, bool>? shouldSkip = null)
	{
		bool filterByTargetArches = supportedTargetArches.Count > 0;
		var assembliesPerArch = new Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> ();
		foreach (ITaskItem assembly in input) {
			if (shouldSkip != null && shouldSkip (assembly)) {
				continue;
			}

			AndroidTargetArch arch = GetTargetArch (assembly);
			if (filterByTargetArches && !supportedTargetArches.Contains (arch)) {
				continue;
			}

			if (!assembliesPerArch.TryGetValue (arch, out var assemblies)) {
				assemblies = new Dictionary<string, ITaskItem> (StringComparer.OrdinalIgnoreCase);
				assembliesPerArch.Add (arch, assemblies);
			}

			assemblies.Add (GetAssemblyNameWithCulture (assembly), assembly);
		}

		// Empty assembly lists are valid, e.g. ResolvedUserAssemblies in GenerateJavaStubs.
		if (assembliesPerArch.Count == 0 || !validate) {
			return assembliesPerArch;
		}

		Dictionary<string, ITaskItem>? firstArchAssemblies = null;
		foreach (var kvp in assembliesPerArch) {
			if (firstArchAssemblies == null) {
				firstArchAssemblies = kvp.Value;
				continue;
			}

			if (kvp.Value.Count != firstArchAssemblies.Count) {
				throw new InvalidOperationException ($"Internal error: architecture '{kvp.Key}' should have {firstArchAssemblies.Count} assemblies, however it has {kvp.Value.Count}");
			}

			foreach (string name in firstArchAssemblies.Keys) {
				if (!kvp.Value.ContainsKey (name)) {
					throw new InvalidOperationException ($"Internal error: architecture '{kvp.Key}' does not have assembly '{name}'");
				}
			}
		}

		return assembliesPerArch;
	}
}
