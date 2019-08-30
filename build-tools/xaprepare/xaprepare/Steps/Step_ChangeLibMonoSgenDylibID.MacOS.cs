using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_ChangeLibMonoSgenDylibID : Step
	{
		public Step_ChangeLibMonoSgenDylibID ()
			: base ("Changing Mono dynamic library ID")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			string xcrun = context.OS.Which ("xcrun");

			var libs = new string[] {
				Path.Combine (Configurables.Paths.HostRuntimeDir, Configurables.Paths.UnstrippedLibMonoSgenName),
				Path.Combine (Configurables.Paths.HostRuntimeDir, Configurables.Paths.StrippedLibMonoSgenName),
			};

			bool result = true;
			Log.StatusLine ("Changing id for:");
			foreach (string libPath in libs) {
				if (!Utilities.FileExists (libPath)) {
					Log.StatusLine ("    not found", ConsoleColor.Magenta);
					continue;
				}

				if (!ChangeID (libPath)) {
					Log.StatusLine ("    failed", ConsoleColor.Magenta);
					result = false;
				}
			}

			return result;

			bool ChangeID (string path)
			{
				Log.DebugLine ($"Changing dylib id for {path}");
				Log.StatusLine ($"  {context.Characters.Bullet} {Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, path)}");
				var runner = new ProcessRunner (xcrun, "install_name_tool", "-id", "@loader_path/libmonosgen-2.0.dylib", path);
				return runner.Run ();
			}
		}
	}
}
