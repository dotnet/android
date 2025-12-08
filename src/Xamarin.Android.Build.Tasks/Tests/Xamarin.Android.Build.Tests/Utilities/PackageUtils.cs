using System;
using System.Runtime.CompilerServices;
using System.Text;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

class PackageUtils
{
	/// <summary>
	/// Constructs Android package name parameter for the <see cref="XamarinAndroidApplicationProject" /> which includes
	/// the runtime used to build and run the application. A unique per-runtime package name is necessary so that elements
	/// of different runtimes don't mix when running the same test for several of them.
	/// </summary>
	public static string MakePackageName (AndroidRuntime runtime, [CallerMemberName] string packageName = "")
	{
		if (String.IsNullOrEmpty (packageName)) {
			throw new ArgumentException ("Must not be null or empty", nameof (packageName));
		}

		var sb = new StringBuilder (packageName);
		sb.Append ('_');
		sb.Append (runtime.ToString ().ToLowerInvariant ());

		return sb.ToString ();
	}
}
