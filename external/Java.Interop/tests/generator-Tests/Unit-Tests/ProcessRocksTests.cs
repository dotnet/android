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
	public void CaptureDrainsLargeStandardErrorAlongsideStandardOutput ()
	{
		var lines = ProcessRocks.ReadStandardOutput (["/bin/sh", "-c", """
			i=0
			while [ "$i" -lt 2048 ]; do
				printf 'stderr-%s-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\n' "$i" >&2
				printf 'stdout-%s\n' "$i"
				i=$((i + 1))
			done
			"""], printCommandLine: false).ToArray ();
		Assert.That (lines.Length, Is.EqualTo (2048));
		Assert.That (lines [0], Is.EqualTo ("stdout-0"));
		Assert.That (lines [2047], Is.EqualTo ("stdout-2047"));
	}

	[Test]
	public void NonzeroExitIsNotReportedAsSuccess ()
	{
		Assert.That (() => ProcessRocks.ReadStandardOutput ([
			"/bin/sh", "-c", "printf 'failure' >&2; exit 7",
		], printCommandLine: false).ToArray (), Throws.InstanceOf<InvalidOperationException> ());
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
