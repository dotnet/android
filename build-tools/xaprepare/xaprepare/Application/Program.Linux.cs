using System;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	abstract class LinuxProgram : Program
	{
		public override bool NeedsSudoToInstall => true;

		public string PackageName { get; }

		public LinuxProgram (string packageName, string executableName)
		{
			if (String.IsNullOrEmpty (packageName))
				throw new ArgumentException ("must not be null or empty", nameof (packageName));
			PackageName = packageName;
			Name = packageName;
			ExecutableName = executableName;
		}

		protected override async Task<bool> DetermineCurrentVersion ()
		{
			bool ret = await base.DetermineCurrentVersion ();
			if (ret)
				return true;

			Log.DebugLine ($"Getting {Name} version from package manager");
			return DeterminePackageVersion ();
		}

		protected abstract bool DeterminePackageVersion ();
	}
}
