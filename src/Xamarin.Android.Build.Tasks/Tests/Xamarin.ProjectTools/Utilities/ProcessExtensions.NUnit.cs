using System.Diagnostics;
using NUnit.Framework;

namespace Xamarin.ProjectTools
{
	public static partial class ProcessExtensions
	{
		public static void SetEnvironmentVariable (this ProcessStartInfo psi, string key, string value)
		{
			psi.SetEnvironmentVariable (key, value, message => Assert.Inconclusive (message));
		}
	}
}
