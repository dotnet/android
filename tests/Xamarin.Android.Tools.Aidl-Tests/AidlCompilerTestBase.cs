//#define GENERATE_OUTPUT
// This define will overwrite the files in /bin/TestData.
// You will still need to copy them back to the original /TestData.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Xamarin.Android.Tools.Aidl;

namespace Xamarin.Android.Tools.Aidl_Tests
{
	public class AidlCompilerTestBase
	{
		protected void RunTest (string name)
		{
			var root = Path.GetDirectoryName (Assembly.GetExecutingAssembly ().Location);
			var file = Path.Combine (root, "TestData", name + ".txt");
			var text = File.ReadAllText (file);

			(var input, var expected_output) = SplitTestFile (text);

			var compiler = new AidlCompiler ();
			var results = compiler.Run (input, out var output);

			Assert.False (results.LogMessages.Any ());

#if GENERATE_OUTPUT
			using (var sw = new StreamWriter (file)) {
				sw.WriteLine (input);
				sw.WriteLine ();
				sw.WriteLine ("####");
				sw.WriteLine ();
				sw.WriteLine (output);
			}

			Assert.Ignore ("Generating output for this test.");
#endif

			Assert.AreEqual (StripLineEndings (expected_output), StripLineEndings (output));
		}

		(string input, string output) SplitTestFile (string text)
		{
			var index = text.IndexOf ("####");

			if (index < 0) {
#if GENERATE_OUTPUT
				return (text, null);
#endif
				throw new Exception ("No expected output found.");
			}

			var input = text.Substring (0, index).Trim ();
			var output = text.Substring (index + 4).Trim ();

			return (input, output);
		}

		string StripLineEndings (string input) => input.Replace ("\n", "").Replace ("\r", "").Trim ();
	}
}
