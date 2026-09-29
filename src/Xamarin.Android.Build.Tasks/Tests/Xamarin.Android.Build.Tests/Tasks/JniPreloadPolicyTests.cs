using System.Collections.Generic;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class JniPreloadPolicyTests : BaseTest
{
	[Test]
	public void AlwaysPreloadOverridesNeverPreloadRegardlessOfFileNameCase ()
	{
		var task = new GenerateJavaApplicationConfig {
			BuildEngine = new MockBuildEngine (TestContext.Out),
		};
		ITaskItem alwaysPreload = new TaskItem ("crypto.so", new Dictionary<string, string> {
			["ArchiveFileName"] = "libCrypto.so",
		});
		ITaskItem neverPreload = new TaskItem ("libcrypto.so", new Dictionary<string, string> {
			["ArchiveFileName"] = "LIBCRYPTO.SO",
		});
		ITaskItem otherLibrary = new TaskItem ("libOther.so", new Dictionary<string, string> {
			["ArchiveFileName"] = "libOther.so",
		});

		ICollection<string> ignore = JniPreloadPolicy.MakeIgnoreCollection (
			task.Log, [alwaysPreload], [neverPreload, otherLibrary]
		);

		Assert.IsFalse (JniPreloadPolicy.ShouldIgnore (task.Log, ignore, alwaysPreload));
		Assert.IsTrue (JniPreloadPolicy.ShouldIgnore (task.Log, ignore, otherLibrary));
	}
}
