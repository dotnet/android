using System.Linq;
using System.Text;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class BuilderTests
	{
		static string GetBuildErrors (params string [] buildOutput)
		{
			var message = new StringBuilder ();
			Builder.AppendBuildErrors (message, buildOutput);
			return message.ToString ();
		}

		[Test]
		public void AppendBuildErrorsReportsDistinctErrors ()
		{
			var message = GetBuildErrors (
				"  1>/tmp/App.csproj(10,5): error XA1234: Something failed [/tmp/App.csproj]",
				"/tmp/App.csproj(10,5): warning XA5678: Not an error [/tmp/App.csproj]",
				"/tmp/App.csproj(10,5): error XA1234: Something failed [/tmp/App.csproj]",
				"MSBUILD : error MSB1008: Only one project can be specified.",
				"clang: fatal error: linker command failed"
			);

			var lines = message.Split ('\n').Select (l => l.Trim ()).Where (l => l.Length > 0).ToArray ();
			CollectionAssert.AreEqual (new [] {
				"Build errors:",
				"/tmp/App.csproj(10,5): error XA1234: Something failed [/tmp/App.csproj]",
				"MSBUILD : error MSB1008: Only one project can be specified.",
				"clang: fatal error: linker command failed",
			}, lines);
		}

		[Test]
		public void AppendBuildErrorsCapsReportedErrors ()
		{
			var output = Enumerable.Range (1, 25).Select (i => $"Foo.cs(1,1): error CS{i:0000}: Error {i}").ToArray ();

			var message = GetBuildErrors (output);

			StringAssert.Contains ("error CS0020: Error 20", message);
			StringAssert.DoesNotContain ("error CS0021: Error 21", message);
			StringAssert.Contains ("... 5 more error(s) omitted.", message);
		}
	}
}
