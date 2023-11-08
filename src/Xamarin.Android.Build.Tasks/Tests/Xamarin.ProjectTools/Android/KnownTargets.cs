using System;

namespace Xamarin.ProjectTools
{
	public static class KnownTargets
	{
		public const string LinkAssembliesNoShrink = "_LinkAssembliesNoShrink";

		// _RunILLink is found at: https://github.com/dotnet/sdk/blob/1ed51bc8f9cb06760c5b2f26798ee0278bd75f54/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.ILLink.targets#L69-L72
		public const string LinkAssembliesShrink = "_RunILLink";
	}
}
