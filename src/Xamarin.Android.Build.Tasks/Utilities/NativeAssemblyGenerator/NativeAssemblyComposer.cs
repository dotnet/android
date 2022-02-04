using System.IO;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	abstract class NativeAssemblyComposer
	{
		protected AndroidTargetArch TargetArch { get; }

		protected NativeAssemblyComposer (AndroidTargetArch arch)
		{
			TargetArch = arch;
		}

		public void Write (StreamWriter output, string fileName)
		{
			NativeAssemblyGenerator generator = NativeAssemblyGenerator.Create (TargetArch, output, fileName);

			generator.WriteFileTop ();
			Write (generator);
			generator.WriteFileEnd ();
		}

		protected abstract void Write (NativeAssemblyGenerator generator);
	}
}
