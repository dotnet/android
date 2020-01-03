using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_InstallPackagesLocally: Step
	{
		public Step_InstallPackagesLocally ()
			: base ("Installing tools packages to local directory")
		{ }

		protected override async Task<bool> Execute (Context context)
		{
			// Ensure NUnit.ConsoleRunner and any other 'tools' packages are additionally installed to a local 'packages' path.
			var nuget = new NuGetRunner (context);

			if (!await nuget.Install ("NUnit.ConsoleRunner", Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "packages"), "3.9.0")) {
				Log.ErrorLine ($"Failed to install 'NUnit.ConsoleRunner'.");
				return false;
			}

			return true;
		}
	}
}
