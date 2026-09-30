#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

partial class MonoAndroidHelper
{
	public static string GetAssemblyAbi (ITaskItem assembly)
	{
		string abi = assembly.GetMetadata ("Abi");
		if (string.IsNullOrEmpty (abi)) {
			throw new InvalidOperationException ($"Internal error: assembly '{assembly}' lacks ABI metadata");
		}
		return abi;
	}

	public static AndroidTargetArch GetTargetArch (ITaskItem assembly) => AbiToTargetArch (GetAssemblyAbi (assembly));

	public static string GetAssemblyNameWithCulture (ITaskItem assembly)
	{
		string name = Path.GetFileNameWithoutExtension (assembly.ItemSpec);
		string culture = assembly.GetMetadata ("Culture");
		return string.IsNullOrEmpty (culture) ? name : $"{culture}/{name}";
	}

	public static Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> GetPerArchAssemblies (
		IEnumerable<ITaskItem> input, ICollection<string> supportedAbis, bool validate, Func<ITaskItem, bool>? shouldSkip = null)
	{
		var supported = new HashSet<AndroidTargetArch> ();
		foreach (string abi in supportedAbis) {
			supported.Add (AbiToTargetArch (abi));
		}

		var result = new Dictionary<AndroidTargetArch, Dictionary<string, ITaskItem>> ();
		foreach (var assembly in input) {
			if (shouldSkip != null && shouldSkip (assembly)) {
				continue;
			}
			var arch = GetTargetArch (assembly);
			if (supported.Count > 0 && !supported.Contains (arch)) {
				continue;
			}
			if (!result.TryGetValue (arch, out var assemblies)) {
				assemblies = new Dictionary<string, ITaskItem> (StringComparer.OrdinalIgnoreCase);
				result.Add (arch, assemblies);
			}
			assemblies.Add (GetAssemblyNameWithCulture (assembly), assembly);
		}

		Dictionary<string, ITaskItem>? first = null;
		if (validate) {
			foreach (var entry in result) {
				if (first == null) {
					first = entry.Value;
					continue;
				}
				if (entry.Value.Count != first.Count) {
					throw new InvalidOperationException ($"Internal error: architecture '{entry.Key}' should have {first.Count} assemblies, however it has {entry.Value.Count}");
				}
				foreach (string name in first.Keys) {
					if (!entry.Value.ContainsKey (name)) {
						throw new InvalidOperationException ($"Internal error: architecture '{entry.Key}' does not have assembly '{name}'");
					}
				}
			}
		}
		return result;
	}
}
