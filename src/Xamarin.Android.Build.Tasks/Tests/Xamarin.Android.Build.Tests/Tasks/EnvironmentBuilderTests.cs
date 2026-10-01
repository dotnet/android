using System.IO;

using Microsoft.Build.Utilities;
using NUnit.Framework;

using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class EnvironmentBuilderTests : BaseTest
{
	[Test]
	public void NullEnvironmentFilesAreIgnored ()
	{
		var builder = new EnvironmentBuilder ();

		Assert.DoesNotThrow (() => builder.Read (null));
		Assert.AreEqual (0, builder.EnvironmentVariables.Count);
		Assert.AreEqual (0, builder.SystemProperties.Count);
	}

	[Test]
	public void ReadsMultipleEnvironmentFiles ()
	{
		string firstEnvironmentFile = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
		string secondEnvironmentFile = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
		try {
			File.WriteAllText (firstEnvironmentFile, "FOO=BAR");
			File.WriteAllText (secondEnvironmentFile, "BAZ=QUX");

			var builder = new EnvironmentBuilder ();
			builder.Read ([
				new TaskItem (firstEnvironmentFile),
				new TaskItem (secondEnvironmentFile),
			]);

			Assert.AreEqual (2, builder.EnvironmentVariables.Count);
			Assert.AreEqual ("BAR", builder.EnvironmentVariables ["FOO"]);
			Assert.AreEqual ("QUX", builder.EnvironmentVariables ["BAZ"]);
			Assert.AreEqual (0, builder.SystemProperties.Count);
		} finally {
			File.Delete (firstEnvironmentFile);
			File.Delete (secondEnvironmentFile);
		}
	}

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
