#nullable enable
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Properties = Xamarin.Android.Tasks.Properties;

namespace Microsoft.Android.Tasks;

/// <summary>
/// Minimal reference-assembly detection for <see cref="CompressAssemblies"/>. This mirrors
/// <c>MonoAndroidHelper.LogIfReferenceAssembly</c> in Xamarin.Android.Build.Tasks, but is
/// duplicated here to keep this net11.0 assembly self-contained.
/// </summary>
static class ReferenceAssemblyChecker
{
	public static bool LogIfReferenceAssembly (ITaskItem assembly, TaskLoggingHelper log)
	{
		if (IsReferenceAssembly (assembly.ItemSpec, log)) {
			log.LogCodedWarning ("XA0107", assembly.ItemSpec, 0, Properties.Resources.XA0107, assembly.ItemSpec);
			return true;
		}

		return false;
	}

	static bool IsReferenceAssembly (string assembly, TaskLoggingHelper log)
	{
		using var stream = File.OpenRead (assembly);
		using var pe = new PEReader (stream);
		if (!pe.HasMetadata) {
			log.LogDebugMessage ($"Skipping non-.NET assembly: {assembly}");
			return false;
		}

		var reader = pe.GetMetadataReader ();
		var assemblyDefinition = reader.GetAssemblyDefinition ();
		foreach (var handle in assemblyDefinition.GetCustomAttributes ()) {
			var attribute = reader.GetCustomAttribute (handle);
			var attributeName = GetCustomAttributeFullName (reader, attribute);
			if (attributeName == "System.Runtime.CompilerServices.ReferenceAssemblyAttribute")
				return true;
		}

		return false;
	}

	static string? GetCustomAttributeFullName (MetadataReader reader, CustomAttribute attribute)
	{
		switch (attribute.Constructor.Kind) {
			case HandleKind.MemberReference: {
				var ctor = reader.GetMemberReference ((MemberReferenceHandle) attribute.Constructor);
				if (ctor.Parent.Kind != HandleKind.TypeReference)
					return null;
				var type = reader.GetTypeReference ((TypeReferenceHandle) ctor.Parent);
				return reader.GetString (type.Namespace) + "." + reader.GetString (type.Name);
			}
			case HandleKind.MethodDefinition: {
				var ctor = reader.GetMethodDefinition ((MethodDefinitionHandle) attribute.Constructor);
				var type = reader.GetTypeDefinition (ctor.GetDeclaringType ());
				return reader.GetString (type.Namespace) + "." + reader.GetString (type.Name);
			}
			default:
				return null;
		}
	}
}
