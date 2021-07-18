using System;
using System.IO;
using System.Collections.Generic;

using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	class NdkToolsWithClangNoBinutils : NdkToolsWithClangNoPlatforms
	{
		public NdkToolsWithClangNoBinutils (string androidNdkPath, NdkVersion version, TaskLoggingHelper? log)
			: base (androidNdkPath, version, log)
		{
			NdkToolNames[NdkToolKind.Linker] = "ld";
			NoBinutils = true;

			throw new NotSupportedException ($"NDK {Version} is not supported by this version of Xamarin.Android");
		}

		public override string GetToolPath (NdkToolKind kind, AndroidTargetArch arch, int apiLevel)
		{
			switch (kind) {
				case NdkToolKind.Assembler:
				case NdkToolKind.Linker:
				case NdkToolKind.Strip:
					return GetEmbeddedToolPath (kind, arch);

				default:
					return base.GetToolPath (kind, arch, apiLevel);
			}
		}

		string GetEmbeddedToolPath (NdkToolKind kind, AndroidTargetArch arch)
		{
			string toolName = GetToolName (kind);
			string triple = GetArchTriple (arch);

			return $"[TODO]/{triple}-{toolName}";
		}
	}
}
