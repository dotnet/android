using Mono.Cecil;
using MonoDroid.Tuner;

namespace Microsoft.Android.Sdk.ILLink
{
	public abstract class Profile
	{
		static Profile current;

		public static Profile Current {
			get {
				if (current == null)
					current = new MonoDroidProfile ();

				return current;
			}
		}

		public static bool IsSdkAssembly (AssemblyDefinition assembly)
		{
			return Current.IsSdk (assembly);
		}

		public static bool IsSdkAssembly (string assemblyName)
		{
			return Current.IsSdk (assemblyName);
		}

		public static bool IsProductAssembly (AssemblyDefinition assembly)
		{
			return Current.IsProduct (assembly);
		}

		public static bool IsProductAssembly (string assemblyName)
		{
			return Current.IsProduct (assemblyName);
		}

		protected virtual bool IsSdk (AssemblyDefinition assembly)
		{
			return IsSdk (assembly.Name.Name);
		}

		protected virtual bool IsProduct (AssemblyDefinition assembly)
		{
			return IsProduct (assembly.Name.Name);
		}

		protected abstract bool IsSdk (string assemblyName);
		protected abstract bool IsProduct (string assemblyName);
	}
}
