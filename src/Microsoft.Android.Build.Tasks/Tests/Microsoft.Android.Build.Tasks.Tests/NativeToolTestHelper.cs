using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Android.Tasks;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

static class NativeToolTestHelper
{
	public static AndroidNdkTools GetNdk (TaskLoggingHelper log)
	{
		string? path = typeof (NativeToolTestHelper).Assembly.GetCustomAttributes<AssemblyMetadataAttribute> ()
			.Single (attribute => attribute.Key == "AndroidNdkDirectory").Value;
		if (!Directory.Exists (path)) {
			Assert.Ignore ("An Android NDK is required for native tool validation. Set AndroidNdkDirectory when building this test project.");
		}
		var ndk = AndroidNdkTools.Create (path, log);
		Assert.IsNotNull (ndk, "The configured NDK must contain the host's LLVM toolchain.");
		return ndk;
	}
}
