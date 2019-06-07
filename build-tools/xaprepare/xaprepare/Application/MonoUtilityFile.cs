using System;
using System.IO;

namespace Xamarin.Android.Prepare
{
	sealed class MonoUtilityFile
	{
		public string SourcePath    { get; }
		public string TargetName    { get; }
		public bool RemapCecil      { get; }
		public bool IgnoreDebugInfo { get; }

		public string DebugSymbolsPath => Utilities.GetDebugSymbolsPath (SourcePath);

		public MonoUtilityFile (string name, bool remap = false, string targetName = null, bool ignoreDebugInfo = false)
		{
			name = name?.Trim ();
			if (String.IsNullOrEmpty (name))
				throw new ArgumentException ("must not be null or empty", nameof (name));

			SourcePath = Path.GetFullPath (Path.Combine (Configurables.Paths.MonoProfileToolsDir, name));
			RemapCecil = remap;
			TargetName = targetName;
			IgnoreDebugInfo = ignoreDebugInfo;
		}
	}
}
