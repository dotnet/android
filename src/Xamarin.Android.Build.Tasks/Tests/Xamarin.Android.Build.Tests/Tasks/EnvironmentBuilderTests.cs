using System.IO;

using Microsoft.Build.Utilities;
using NUnit.Framework;

using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class EnvironmentBuilderTests : BaseTest
{
	[Test]
	public void PreservesDotNetDiagnosticsSettings ()
	{
		string environmentFile = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
		try {
			File.WriteAllLines (environmentFile, [
				"DOTNET_EnableDiagnostics=1",
				"debug.dotnet.log=gref",
			]);

			var builder = new EnvironmentBuilder ();
			builder.Read ([new TaskItem (environmentFile)]);

			Assert.AreEqual ("1", builder.EnvironmentVariables ["DOTNET_EnableDiagnostics"]);
			Assert.AreEqual ("gref", builder.SystemProperties ["debug.dotnet.log"]);
			Assert.IsFalse (builder.EnvironmentVariables.ContainsKey ("MONO_LOG_LEVEL"));
			Assert.IsFalse (builder.EnvironmentVariables.ContainsKey ("MONO_DEBUG"));
			Assert.IsFalse (builder.EnvironmentVariables.ContainsKey ("MONO_GC_PARAMS"));
		} finally {
			File.Delete (environmentFile);
		}
	}
}
