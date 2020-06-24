using System;

namespace Xamarin.ProjectTools
{
	public static class KnownTargets
	{
		public const string LinkAssembliesNoShrink = "_LinkAssembliesNoShrink";

		public static string LinkAssembliesShrink => Builder.UseDotNet ? "ILLink" : "_LinkAssembliesShrink";
	}
}
