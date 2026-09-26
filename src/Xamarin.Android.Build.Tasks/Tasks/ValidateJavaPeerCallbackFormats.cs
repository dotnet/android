#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

public class ValidateJavaPeerCallbackFormats : AndroidTask
{
	public override string TaskPrefix => "VPC";

	[Required]
	public ITaskItem [] ResolvedAssemblies { get; set; } = [];

	public override bool RunTask ()
	{
		var validatedPaths = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		var reportedAssemblyNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
		foreach (var item in ResolvedAssemblies) {
			var fullPath = Path.GetFullPath (item.ItemSpec);
			var assemblyName = Path.GetFileName (item.ItemSpec);
			if (!validatedPaths.Add (fullPath) || !File.Exists (fullPath))
				continue;

			using var pe = new PEReader (File.OpenRead (fullPath));
			if (!pe.HasMetadata)
				continue;

			var reader = pe.GetMetadataReader ();
			if (!reader.IsAssembly)
				continue;

			foreach (var attributeHandle in reader.GetAssemblyDefinition ().GetCustomAttributes ()) {
				var attribute = reader.GetCustomAttribute (attributeHandle);
				if (reader.GetCustomAttributeFullName (attribute, Log) != "Java.Interop.JavaPeerCallbackFormatAttribute")
					continue;

				var blob = reader.GetBlobReader (attribute.Value);
				bool supported = blob.RemainingBytes >= 8 &&
					blob.ReadUInt16 () == 1 &&
					blob.ReadInt32 () == 1;
				if (!supported && reportedAssemblyNames.Add (assemblyName))
					Log.LogCodedError ("XA4265", Properties.Resources.XA4265, assemblyName);
				break;
			}
		}

		return !Log.HasLoggedErrors;
	}
}
