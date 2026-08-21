// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using NUnit.Framework;

namespace Xamarin.Android.Tools.Tests
{
	[TestFixture]
	public class ProcessUtilsTests
	{
		[Test]
		public void CreateProcessStartInfo_SetsFileName ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("myapp");
			Assert.AreEqual ("myapp", psi.FileName);
		}

		[Test]
		public void CreateProcessStartInfo_SetsShellAndWindow ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("myapp");
			Assert.IsFalse (psi.UseShellExecute, "UseShellExecute should be false");
			Assert.IsTrue (psi.CreateNoWindow, "CreateNoWindow should be true");
		}

		[Test]
		public void CreateProcessStartInfo_NoArgs ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("myapp");
			Assert.AreEqual (0, psi.ArgumentList.Count);
		}

		[Test]
		public void CreateProcessStartInfo_SingleArg ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("myapp", "--version");
			Assert.AreEqual (1, psi.ArgumentList.Count);
			Assert.AreEqual ("--version", psi.ArgumentList [0]);
		}

		[Test]
		public void CreateProcessStartInfo_MultipleArgs ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("tar", "-xzf", "archive.tar.gz", "-C", "/tmp/output");
			Assert.AreEqual (4, psi.ArgumentList.Count);
			Assert.AreEqual ("-xzf", psi.ArgumentList [0]);
			Assert.AreEqual ("archive.tar.gz", psi.ArgumentList [1]);
			Assert.AreEqual ("-C", psi.ArgumentList [2]);
			Assert.AreEqual ("/tmp/output", psi.ArgumentList [3]);
		}

		[Test]
		public void CreateProcessStartInfo_ArgWithSpaces ()
		{
			var psi = ProcessUtils.CreateProcessStartInfo ("cmd", "/c", "path with spaces");
			Assert.AreEqual (2, psi.ArgumentList.Count);
			Assert.AreEqual ("path with spaces", psi.ArgumentList [1]);
		}

		[Test]
		public void IsElevated_DoesNotThrow ()
		{
			// Smoke test: just verify it returns without crashing
			bool result = ProcessUtils.IsElevated ();
			Assert.That (result, Is.TypeOf<bool> ());
		}

		[Test]
		public void JoinArguments_WindowsPathIsNotDoubleEscaped ()
		{
			// Regression test: escaping every backslash produced `C:\\dir\\file.dll`, which
			// made `adb push` fail with "failed to read all of ...: Invalid argument".
			Assert.AreEqual (@"C:\Users\me\obj\a.dll", ProcessUtils.JoinArguments (@"C:\Users\me\obj\a.dll"));
		}

		[Test]
		public void JoinArguments_PathWithSpacesIsQuotedButNotDoubleEscaped ()
		{
			Assert.AreEqual ("\"C:\\path with spaces\\a.dll\"", ProcessUtils.JoinArguments (@"C:\path with spaces\a.dll"));
		}

		[Test]
		public void JoinArguments_EmbeddedQuoteIsEscaped ()
		{
			Assert.AreEqual ("\"he said \\\"hi\\\"\"", ProcessUtils.JoinArguments ("he said \"hi\""));
		}

		[Test]
		public void JoinArguments_BackslashBeforeQuoteIsDoubled ()
		{
			Assert.AreEqual ("\"a\\\\\\\"b\"", ProcessUtils.JoinArguments ("a\\\"b"));
		}

		[Test]
		public void JoinArguments_TrailingBackslashWithSpacesIsDoubled ()
		{
			// The trailing backslash precedes the closing quote, so it must be doubled.
			Assert.AreEqual ("\"C:\\a b\\\\\"", ProcessUtils.JoinArguments (@"C:\a b\"));
		}

		[Test]
		public void JoinArguments_TrailingBackslashWithoutSpacesNeedsNoQuoting ()
		{
			Assert.AreEqual (@"C:\dir\", ProcessUtils.JoinArguments (@"C:\dir\"));
		}

		[Test]
		public void JoinArguments_EmptyArgument ()
		{
			Assert.AreEqual ("\"\"", ProcessUtils.JoinArguments (""));
		}

		[Test]
		public void JoinArguments_NullArgument ()
		{
			Assert.AreEqual ("\"\"", ProcessUtils.JoinArguments (default (string)));
		}

		[Test]
		public void JoinArguments_NoArguments ()
		{
			Assert.AreEqual ("", ProcessUtils.JoinArguments ());
		}

		[Test]
		public void JoinArguments_MultipleArgumentsAreSpaceSeparated ()
		{
			Assert.AreEqual (
				"push -z any \"C:\\a b\\x.dll\" C:\\y.dll /data/local/tmp",
				ProcessUtils.JoinArguments ("push", "-z", "any", @"C:\a b\x.dll", @"C:\y.dll", "/data/local/tmp"));
		}

		[Test]
		public void JoinArguments_RoundTripsThroughArgumentParsing ()
		{
			var args = new [] {
				@"C:\Users\me\obj\Debug\net11.0-android\a.dll",
				@"C:\path with spaces\b.dll",
				"he said \"hi\"",
				@"C:\dir\",
				@"C:\a b\",
				"plain",
			};
			var parsed = SplitCommandLine (ProcessUtils.JoinArguments (args));
			CollectionAssert.AreEqual (args, parsed);
		}

		/// <summary>
		/// Arguments that contain no whitespace or quotes are now emitted bare rather than
		/// wrapped in quotes. That is transparent to the child process (the quotes were always
		/// stripped by argument parsing), but <see cref="AdbRunner"/> passes many such arguments,
		/// so assert the shapes it uses still arrive unchanged.
		/// </summary>
		[TestCase ("devices")]
		[TestCase ("-l")]
		[TestCase ("-s")]
		[TestCase ("58230DLCR0013R")]
		[TestCase ("emulator-5554")]
		[TestCase ("tcp:5555")]
		[TestCase ("localabstract:org.example_debug")]
		[TestCase ("--remove-all")]
		[TestCase ("ro.product.cpu.abilist")]
		[TestCase ("getprop")]
		public void JoinArguments_AdbArgumentShapesRoundTrip (string argument)
		{
			CollectionAssert.AreEqual (new [] { argument }, SplitCommandLine (ProcessUtils.JoinArguments (argument)));
		}

		[Test]
		public void JoinArguments_AdbShellCommandRoundTrips ()
		{
			// `AdbRunner.RunShellCommandAsync` passes an entire shell command as one argument.
			var args = new [] { "-s", "58230DLCR0013R", "shell", "echo \"remote=$(cat /data/local/tmp/x)\"" };
			CollectionAssert.AreEqual (args, SplitCommandLine (ProcessUtils.JoinArguments (args)));
		}

		/// <summary>
		/// Whitespace detection matches the BCL's <c>PasteArguments</c>, which uses
		/// <see cref="char.IsWhiteSpace(char)"/> rather than just space and tab. Quoting an
		/// argument is always safe, so erring towards quoting keeps the two implementations
		/// in agreement.
		/// </summary>
		[TestCase ("a\rb")]
		[TestCase ("a\fb")]
		[TestCase ("a\nb")]
		[TestCase ("a\vb")]
		[TestCase ("a\u00a0b")]
		public void JoinArguments_AllWhitespaceIsQuoted (string argument)
		{
			var joined = ProcessUtils.JoinArguments (argument);
			Assert.AreEqual ($"\"{argument}\"", joined);
		}

		/// <summary>
		/// Minimal implementation of the <c>CommandLineToArgvW</c> parsing rules, used to verify
		/// that <see cref="ProcessUtils.JoinArguments"/> round-trips.
		/// </summary>
		static List<string> SplitCommandLine (string commandLine)
		{
			var results = new List<string> ();
			var current = new StringBuilder ();
			bool inQuotes = false, hasArgument = false;

			for (int i = 0; i < commandLine.Length; i++) {
				char c = commandLine [i];
				if (c == '\\') {
					int backslashes = 0;
					while (i < commandLine.Length && commandLine [i] == '\\') {
						backslashes++;
						i++;
					}
					if (i < commandLine.Length && commandLine [i] == '"') {
						current.Append ('\\', backslashes / 2);
						if (backslashes % 2 == 0) {
							inQuotes = !inQuotes;
						} else {
							current.Append ('"');
						}
						hasArgument = true;
					} else {
						current.Append ('\\', backslashes);
						i--;
					}
					continue;
				}
				if (c == '"') {
					inQuotes = !inQuotes;
					hasArgument = true;
					continue;
				}
				if (!inQuotes && (c == ' ' || c == '\t')) {
					if (hasArgument || current.Length > 0) {
						results.Add (current.ToString ());
						current.Clear ();
						hasArgument = false;
					}
					continue;
				}
				current.Append (c);
				hasArgument = true;
			}

			if (hasArgument || current.Length > 0) {
				results.Add (current.ToString ());
			}
			return results;
		}
	}
}
