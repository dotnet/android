using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_RestorePackagesLocally: Step
	{
		public Step_RestorePackagesLocally ()
			: base ("Restoring NuGet packages to local directory")
		{ }

		protected override async Task<bool> Execute (Context context)
		{
			// Ensure NUnit.ConsoleRunner and other 'tools' packages are additionally installed to a local 'packages' path.
			var csproj = Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "src", "Xamarin.Android.Build.Tasks", "Tests",
				"Xamarin.Android.Build.Tests", "Xamarin.Android.Build.Tests.csproj");
			var nuget = new NuGetRunner (context);

			if (!await nuget.Restore (csproj, Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "packages"))) {
				Log.ErrorLine ($"NuGet restore for {csproj} failed");
				return false;
			}

			return true;
		}
	}
}
