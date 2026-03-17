using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class AcwMapWriterTests : FixtureTestBase
{
	static string WriteToString (IEnumerable<JavaPeerInfo> peers)
	{
		using var writer = new StringWriter ();
		AcwMapWriter.Write (writer, peers);
		return writer.ToString ();
	}

	[Fact]
	public void Write_SingleMcwType_ProducesThreeLines ()
	{
		var peer = MakeMcwPeer ("android/app/Activity", "Android.App.Activity", "Mono.Android");

		var output = WriteToString (new [] { peer });
		var lines = output.TrimEnd ().Split (new [] { '\n' }, StringSplitOptions.None);

		Assert.Equal (3, lines.Length);
		// Line 1: PartialAssemblyQualifiedName;JavaKey
		Assert.Equal ("Android.App.Activity, Mono.Android;android.app.Activity", lines [0]);
		// Line 2: ManagedKey;JavaKey
		Assert.Equal ("Android.App.Activity;android.app.Activity", lines [1]);
		// Line 3: CompatJniName;JavaKey
		Assert.Equal ("android.app.Activity;android.app.Activity", lines [2]);
	}

	[Fact]
	public void Write_UserType_SlashesConvertedToDots ()
	{
		var peer = new JavaPeerInfo {
			JavaName = "crc64abcdef/MyActivity",
			CompatJniName = "my.namespace/MyActivity",
			ManagedTypeName = "My.Namespace.MyActivity",
			ManagedTypeNamespace = "My.Namespace",
			ManagedTypeShortName = "MyActivity",
			AssemblyName = "MyApp",
		};

		var output = WriteToString (new [] { peer });
		var lines = output.TrimEnd ().Split (new [] { '\n' }, StringSplitOptions.None);

		Assert.Equal (3, lines.Length);
		Assert.Equal ("My.Namespace.MyActivity, MyApp;crc64abcdef.MyActivity", lines [0]);
		Assert.Equal ("My.Namespace.MyActivity;crc64abcdef.MyActivity", lines [1]);
		Assert.Equal ("my.namespace.MyActivity;crc64abcdef.MyActivity", lines [2]);
	}

	[Fact]
	public void Write_MultipleTypes_OrderedByManagedName ()
	{
		var peers = new [] {
			MakeMcwPeer ("android/widget/TextView", "Android.Widget.TextView", "Mono.Android"),
			MakeMcwPeer ("android/app/Activity", "Android.App.Activity", "Mono.Android"),
			MakeMcwPeer ("android/content/Context", "Android.Content.Context", "Mono.Android"),
		};

		var output = WriteToString (peers);
		var lines = output.TrimEnd ().Split (new [] { '\n' }, StringSplitOptions.None);

		// 3 types × 3 lines each = 9 lines
		Assert.Equal (9, lines.Length);

		// First type alphabetically: Android.App.Activity
		Assert.StartsWith ("Android.App.Activity, Mono.Android;", lines [0]);
		// Second: Android.Content.Context
		Assert.StartsWith ("Android.Content.Context, Mono.Android;", lines [3]);
		// Third: Android.Widget.TextView
		Assert.StartsWith ("Android.Widget.TextView, Mono.Android;", lines [6]);
	}

	[Fact]
	public void Write_EmptyList_ProducesEmptyOutput ()
	{
		var output = WriteToString (Array.Empty<JavaPeerInfo> ());
		Assert.Equal ("", output);
	}

	[Fact]
	public void Write_MatchesExpectedAcwMapFormat ()
	{
		// Verify the format matches what LoadMapFile expects:
		// each line is "key;value" where LoadMapFile splits on ';'
		var peer = MakeMcwPeer ("android/app/Activity", "Android.App.Activity", "Mono.Android");

		var output = WriteToString (new [] { peer });

		foreach (var line in output.TrimEnd ().Split (new [] { '\n' }, StringSplitOptions.None)) {
			var parts = line.Split (new [] { ';' }, count: 2);
			Assert.Equal (2, parts.Length);
			Assert.False (string.IsNullOrWhiteSpace (parts [0]), "Key should not be empty");
			Assert.False (string.IsNullOrWhiteSpace (parts [1]), "Value should not be empty");
		}
	}

	[Fact]
	public void Write_FromScannedFixtures_ProducesValidOutput ()
	{
		var peers = ScanFixtures ();
		Assert.NotEmpty (peers);

		var output = WriteToString (peers);

		foreach (var line in output.TrimEnd ().Split (new [] { '\n' }, StringSplitOptions.None)) {
			var parts = line.Split (new [] { ';' }, count: 2);
			Assert.Equal (2, parts.Length);
			// No slashes in the output — they should all be converted to dots
			Assert.DoesNotContain ("/", parts [1]);
		}
	}
}
