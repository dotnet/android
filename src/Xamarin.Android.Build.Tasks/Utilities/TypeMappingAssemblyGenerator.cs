using System;
using System.IO;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	abstract class TypeMappingAssemblyGenerator : NativeAssemblyComposer
	{
		protected string TypemapsIncludeFile { get; }
		protected string SharedIncludeFile { get; }
		public string MainSourceFile { get; }

		protected TypeMappingAssemblyGenerator (AndroidTargetArch arch, string baseFilePath, bool sharedIncludeUsesAbiPrefix)
			: base (arch)
		{
			if (String.IsNullOrEmpty (baseFilePath)) {
				throw new ArgumentException ("must not be null or empty", nameof (baseFilePath));
			}

			string abiName = NativeAssemblyGenerator.GetAbiName (arch);

			if (sharedIncludeUsesAbiPrefix) {
				SharedIncludeFile = $"{baseFilePath}.{abiName}-shared.inc";
			} else {
				SharedIncludeFile = $"{baseFilePath}.shared.inc";
			}
			TypemapsIncludeFile = $"{baseFilePath}.{abiName}-managed.inc";
			MainSourceFile = $"{baseFilePath}.{abiName}.s";
		}
	}
}
