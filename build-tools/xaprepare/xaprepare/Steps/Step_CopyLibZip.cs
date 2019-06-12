using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_CopyLibZip : Step
	{
		const string LibZip = "libzip.dll";

		public Step_CopyLibZip () : base ($"Copying {LibZip}") { }

		protected override Task<bool> Execute (Context context)
		{
			Copy ();
			Copy ("x64");
			return Task.FromResult (true);
		}

		void Copy (string dir = "")
		{
			var src = Path.Combine (Configurables.Paths.InstallMSBuildDir, dir, LibZip);
			var buildBin = Path.Combine (Configurables.Paths.BuildBinDir, dir, LibZip);
			Utilities.CopyFile (src, buildBin, overwriteDestinationFile: true);
			var testBin = Path.Combine (Configurables.Paths.TestBinDir, dir, LibZip);
			Utilities.CopyFile (src, testBin, overwriteDestinationFile: true);
		}
	}
}
