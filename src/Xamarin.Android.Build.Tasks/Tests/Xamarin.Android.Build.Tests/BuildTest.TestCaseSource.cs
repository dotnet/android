using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	public partial class BuildTest : BaseTest
	{
		static readonly object [] DotNetBuildSource = new object [] {
			new object [] {
				/* runtimeIdentifiers */ "android-arm64",
				/* isRelease */          true,
				/* usesAssemblyStore */  true,
				/* runtime */            AndroidRuntime.CoreCLR,
			},
			new object [] {
				/* runtimeIdentifiers */ "android-arm64",
				/* isRelease */          false,
				/* usesAssemblyStore */  true,
				/* runtime */            AndroidRuntime.CoreCLR,
			},
			new object [] {
				/* runtimeIdentifiers */ "android-arm64",
				/* isRelease */          true,
				/* usesAssemblyStore */  false,
				/* runtime */            AndroidRuntime.NativeAOT,
			},
		};

	}
}
