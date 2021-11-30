using System;
using System.IO;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	abstract class TypeMappingAssemblyGenerator
	{
		AndroidTargetArch arch;

		protected string TypemapsIncludeFile { get; }
		protected string SharedIncludeFile { get; }
		public string MainSourceFile { get; }

		protected TypeMappingAssemblyGenerator (AndroidTargetArch arch, string baseFilePath, bool sharedIncludeUsesAbiPrefix)
		{
			if (String.IsNullOrEmpty (baseFilePath)) {
				throw new ArgumentException ("must not be null or empty", nameof (baseFilePath));
			}

			this.arch = arch;
			string abiName = NativeAssemblyGenerator.GetAbiName (arch);

			if (sharedIncludeUsesAbiPrefix) {
				SharedIncludeFile = $"{baseFilePath}.{abiName}-shared.inc";
			} else {
				SharedIncludeFile = $"{baseFilePath}.shared.inc";
			}
			TypemapsIncludeFile = $"{baseFilePath}.{abiName}-managed.inc";
			MainSourceFile = $"{baseFilePath}.{abiName}.s";
		}

		public void Write (StreamWriter output, string fileName)
		{
			Write (NativeAssemblyGenerator.Create (arch, output, fileName));
		}

		protected abstract void Write (NativeAssemblyGenerator generator);
	}
}
