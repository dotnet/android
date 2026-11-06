#nullable enable
using System;
using System.Diagnostics;
using System.Linq;
using MonoDroid.Utils;
using NUnit.Framework;

namespace generatortests;

[TestFixture]
[Platform (Exclude = "Win")]
public class ProcessRocksTests
{
	[Test]
	public void StructuredArgumentsPreserveQuotesAndEmptyValues ()
	{
		var lines = ProcessRocks.ReadStandardOutput ([
			"/usr/bin/printf", "%s\n", "with spaces", "quoted \"value\"", "back\\slash", "",
		], printCommandLine: false).ToArray ();
		string [] expected = ["with spaces", "quoted \"value\"", "back\\slash", ""];
		Assert.That (lines, Is.EqualTo (expected));
	}

	[Test]
	public void CommandFailureIncludesExitCodeAndStandardError ()
	{
		var error = Assert.Throws<CommandFailedException> (() => ProcessRocks.ReadStandardOutput ([
			"/bin/sh", "-c", "printf 'failure' >&2; exit 7",
		], printCommandLine: false).ToArray ()) ?? throw new InvalidOperationException ("Expected command failure.");
		Assert.That (error.ExitCode, Is.EqualTo (7));
		Assert.That (error.ErrorLog, Is.EqualTo ("failure" + Environment.NewLine));
		Assert.That (error.FileName, Is.EqualTo ("/bin/sh"));
	}

	[Test]
	public void DisposingEnumerationTerminatesItsChild ()
	{
		using var output = ProcessRocks.ReadStandardOutput ([
			"/bin/sh", "-c", "echo $$; exec sleep 60",
		], printCommandLine: false).GetEnumerator ();
		Assert.That (output.MoveNext (), Is.True);
		using var child = Process.GetProcessById (int.Parse (output.Current));
		try {
			output.Dispose ();
			Assert.That (child.WaitForExit (5000), Is.True);
		} finally {
			if (!child.HasExited) {
				child.Kill ();
				child.WaitForExit (5000);
			}
		}
	}
}
