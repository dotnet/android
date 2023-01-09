using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.IO;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Emits XA1034 is an assembly has a reference to _Microsoft.Android.Resource.Designer.dll
	/// * NOTE: only called when $(AndroidUseDesignerAssembly) is false
	/// </summary>
	public class CheckForInvalidDesignerConfig : AndroidTask
	{
		public override string TaskPrefix => "CIRF";

		public ITaskItem[] Assemblies { get; set; }

		public override bool RunTask ()
		{
			foreach (var assembly in Assemblies) {
				if (HasResourceDesignerAssemblyReference (assembly)) {
					Log.LogCodedError ("XA1034", Properties.Resources.XA1034, assembly);
				}
			}

			return !Log.HasLoggedErrors;
		}

		static bool HasResourceDesignerAssemblyReference (ITaskItem assembly)
		{
			if (!File.Exists (assembly.ItemSpec)) {
				return false;
			}
			using var pe = new PEReader (File.OpenRead (assembly.ItemSpec));
			var reader = pe.GetMetadataReader ();
			return HasResourceDesignerAssemblyReference (reader);
		}

		static bool HasResourceDesignerAssemblyReference (MetadataReader reader)
		{
			foreach (var handle in reader.AssemblyReferences) {
				var reference = reader.GetAssemblyReference (handle);
				var name = reader.GetString (reference.Name);
				if (string.CompareOrdinal (name, "_Microsoft.Android.Resource.Designer") == 0) {
					return true;
				}
			}
			return false;
		}
	}
}