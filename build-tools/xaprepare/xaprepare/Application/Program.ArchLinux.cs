using System;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class ArchLinuxProgram : LinuxProgram
	{
		public ArchLinuxProgram (string packageName, string executableName = null)
			: base (packageName, executableName)
		{}

		protected override bool CheckWhetherInstalled ()
		{
			throw new NotImplementedException ();
		}

#pragma warning disable CS1998
		public override async Task<bool> Install ()
		{
			throw new NotImplementedException ();
		}
#pragma warning restore CS1998

		protected override bool DeterminePackageVersion()
		{
			throw new NotImplementedException();
		}
	}
}
