using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Prepare
{
	sealed class BclFile
	{
		readonly Dictionary<BclFileTarget, string> bclProfileSourceDirs = new Dictionary<BclFileTarget, string> () {
			{ BclFileTarget.Android,         Configurables.Paths.BCLAssembliesSourceDir },
			{ BclFileTarget.DesignerHost,    Configurables.Paths.BCLHostAssembliesSourceDir },
			{ BclFileTarget.DesignerWindows, Configurables.Paths.BCLWindowsAssembliesSourceDir },
		};

		readonly Dictionary<BclFileTarget, string> bclFacadeSourceDirs = new Dictionary<BclFileTarget, string> () {
			{ BclFileTarget.Android,         Configurables.Paths.BCLFacadeAssembliesSourceDir },
			{ BclFileTarget.DesignerHost,    Configurables.Paths.BCLHostFacadeAssembliesSourceDir },
			{ BclFileTarget.DesignerWindows, Configurables.Paths.BCLWindowsFacadeAssembliesSourceDir },
		};

		public string Name              { get; }
		public BclFileType Type         { get; }
		public BclFileTarget Target     { get; }
		public bool ExcludeDebugSymbols { get; }
		public string SourcePath        { get; }
		public string Version           { get; }
		public string DebugSymbolsPath  {
			get {
				if (ExcludeDebugSymbols)
					return null;

				return Utilities.GetDebugSymbolsPath (SourcePath);
			}
		}

		public BclFile (string name, BclFileType type, bool excludeDebugSymbols = false, string version = null, BclFileTarget target = BclFileTarget.Android)
		{
			name = name?.Trim ();
			if (String.IsNullOrEmpty (name))
				throw new ArgumentException ("must not be null or empty", nameof (name));

			Name = name;
			Type = type;
			ExcludeDebugSymbols = excludeDebugSymbols;
			Version = version;
			Target = target;

			string sourceDir;
			switch (type) {
				case BclFileType.ProfileAssembly:
					sourceDir = bclProfileSourceDirs [target];
					break;

				case BclFileType.FacadeAssembly:
					sourceDir = bclFacadeSourceDirs [target];
					break;

				default:
					throw new InvalidOperationException ($"Unsupported BCL file type {Type} for file {Name}");
			}

			SourcePath = Path.GetFullPath (Path.Combine (sourceDir, name));
		}
	}
}
