#nullable enable

using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// This task provides warnings when an assembly references the obsolete PreserveAttribute.
	/// The PreserveAttribute used to indicate to the linker that a type or member should not be trimmed.
	/// It had similar functionality to the newer DynamicDependencyAttribute, but was Android-specific and is now obsolete.
	/// </summary>
	public class CheckForObsoletePreserveAttribute : AndroidTask
	{
		public override string TaskPrefix => "COPA";

		public ITaskItem[] Assemblies { get; set; } = [];

		public override bool RunTask ()
		{
			foreach (var assembly in Assemblies) {
				if (HasObsoletePreserveAttribute (assembly.ItemSpec, out string? assemblyName)) {
					Log.LogCodedWarning ("IL6001", $"Assembly '{assemblyName}' contains reference to obsolete attribute 'Android.Runtime.PreserveAttribute'. Members with this attribute may be trimmed. Please use System.Diagnostics.CodeAnalysis.DynamicDependencyAttribute instead");
				}
			}
			return true; // Warnings don't fail the build
		}

		static bool HasObsoletePreserveAttribute (string assemblyPath, out string? assemblyName)
		{
			assemblyName = null;

			try {
				using var stream = File.OpenRead (assemblyPath);
				using var pe = new PEReader (stream);

				if (!pe.HasMetadata)
					return false;

				var reader = pe.GetMetadataReader ();
				assemblyName = reader.GetString (reader.GetAssemblyDefinition ().Name);

				foreach (var handle in reader.TypeReferences) {
					var typeRef = reader.GetTypeReference (handle);
					var ns = reader.GetString (typeRef.Namespace);
					var name = reader.GetString (typeRef.Name);

					if (ns == "Android.Runtime" && name == "PreserveAttribute")
						return true;
				}
				return false;
			} catch (BadImageFormatException) {
				return false;
			}
		}
	}
}
