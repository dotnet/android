using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class ExtractTypeMapKeysFromDgmlTests : IDisposable
{
	readonly string directory = Path.Combine (Path.GetTempPath (), nameof (ExtractTypeMapKeysFromDgmlTests), Guid.NewGuid ().ToString ("N"));

	const string RetainedGraph = """
		<DirectedGraph xmlns="http://schemas.microsoft.com/vs/2009/dgml">
		  <Nodes>
		    <Node Id="1" Label="Type metadata: [App]Example.Type" />
		  </Nodes>
		</DirectedGraph>
		""";

	const string EmptyGraph = """
		<DirectedGraph xmlns="http://schemas.microsoft.com/vs/2009/dgml">
		  <Nodes />
		</DirectedGraph>
		""";

	[Fact]
	public void Execute_UnionsGraphsAndMatchesOnlyQualifiedMetadata ()
	{
		var (task, errors) = CreateTask ("""
			UnnamedProject.MainActivity, UnnamedProject;crc64a1.MainActivity
			Android.App.Activity, Mono.Android;android.app.Activity
			Duplicate.Type, My.Assembly;my.app.Duplicate
			AndroidX.Activity.Result.Contract.ActivityResultContracts+TakePicture, Xamarin.AndroidX.Activity;androidx.activity.result.contract.ActivityResultContracts$TakePicture
			Duplicate.Type;wrong.Unqualified
			Duplicate.Type, Other.Assembly;wrong.Assembly
			Removed.Type, My.Assembly;removed.Type
			Links.Type, My.Assembly;wrong.Links
			Nested.Type, My.Assembly;wrong.NestedNode
			Foreign.Type, My.Assembly;wrong.ForeignNamespace
			Second.Type, My.Assembly;second.Type
			Second.Type, My.Assembly;second.Type
			Second.Type, My.Assembly;second/Type
			""", true, """
			<DirectedGraph xmlns="http://schemas.microsoft.com/vs/2009/dgml">
			  <Nodes>
			    <Node Id="1" Label="Type metadata: [UnnamedProject]UnnamedProject.MainActivity" />
			    <Node Id="2" Label="Type metadata: [Mono.Android]Android.App.Activity" />
			    <Node Id="3" Label="Type metadata: [My.Assembly]Duplicate.Type" />
			    <Node Id="4" Label="Type metadata: [Xamarin.AndroidX.Activity]AndroidX.Activity.Result.Contract.ActivityResultContracts+TakePicture" />
			    <Node Id="5" Label="Unrelated node" />
			    <Node Id="6" />
			    <Wrapper>
			      <Node Id="7" Label="Type metadata: [My.Assembly]Nested.Type" />
			    </Wrapper>
			    <Node xmlns="urn:other" Id="8" Label="Type metadata: [My.Assembly]Foreign.Type" />
			  </Nodes>
			  <Links>
			    <Node Id="9" Label="Type metadata: [My.Assembly]Links.Type" />
			    <Nodes>
			      <Node Id="10" Label="Type metadata: [My.Assembly]Links.Type" />
			    </Nodes>
			  </Links>
			</DirectedGraph>
			""", """
			<DirectedGraph xmlns="http://schemas.microsoft.com/vs/2009/dgml">
			  <Nodes>
			    <Node Id="1" Label="Type metadata: [My.Assembly]Second.Type" />
			    <Node Id="2" Label="Type metadata: [Mono.Android]Android.App.Activity" />
			    <Node Id="3" Label="Type metadata: [My.Assembly]Second.Type" />
			  </Nodes>
			</DirectedGraph>
			""");

		Assert.True (task.Execute ());
		Assert.Empty (errors);
		const string expected = "android/app/Activity\n" +
			"androidx/activity/result/contract/ActivityResultContracts$TakePicture\n" +
			"crc64a1/MainActivity\n" +
			"my/app/Duplicate\n" +
			"second/Type\n";
		AssertOutput (task, expected);

		Array.Reverse (task.NativeAotDgmlFiles);
		task.NativeAotDgmlFiles = task.NativeAotDgmlFiles.Concat (task.NativeAotDgmlFiles).ToArray ();
		var mapLines = File.ReadAllLines (task.AcwMapFile);
		Array.Reverse (mapLines);
		File.WriteAllLines (task.AcwMapFile, mapLines);
		Assert.True (task.Execute (), "Input ordering and duplicated graphs must not change the retained set.");
		AssertOutput (task, expected);
	}

	[Theory]
	[InlineData ("absent")]
	[InlineData ("missing")]
	[InlineData ("malformed")]
	public void Execute_KeepsAllMappedClassesWithoutReadingGraphsWhenTrimmingDisabled (string graph)
	{
		var (task, errors) = CreateTask ("""
			Example.Type, App;example.Type
			Example.Type;example.Type
			example.Type;example.Type
			Other.Type;other.Type$Nested
			""", false);
		if (graph == "missing") {
			task.NativeAotDgmlFiles = [new TaskItem (Path.Combine (directory, "missing.dgml.xml"))];
		} else if (graph == "malformed") {
			var path = Path.ChangeExtension (task.AcwMapFile, ".dgml.xml");
			File.WriteAllText (path, "not XML");
			task.NativeAotDgmlFiles = [new TaskItem (path)];
		}

		Assert.True (task.Execute ());
		Assert.Empty (errors);
		AssertOutput (task, "example/Type\nother/Type$Nested\n");
	}

	[Fact]
	public void Execute_PreservesUnicodeAndWritesExactOrdinalUtf8Bytes ()
	{
		const string map = "Example.Type, App;com.\u00e9xample.Peer\u0394$Nested\n" +
			"Example.Type, App;com.example.\U00010400Peer\n" +
			"Example.Type, App;com.example.\u2160Peer\n" +
			"Example.Type, App;com.example.A\u0301\n" +
			"Example.Type, App;com.example.a\n" +
			"Example.Type, App;com.example.Z\n";
		var (task, errors) = CreateTask (map, true, RetainedGraph);
		File.WriteAllText (task.AcwMapFile, map, new UTF8Encoding (true));

		Assert.True (task.Execute ());
		Assert.Empty (errors);
		AssertOutput (task, "com/example/A\u0301\ncom/example/Z\ncom/example/a\n" +
			"com/example/\u2160Peer\ncom/example/\U00010400Peer\ncom/\u00e9xample/Peer\u0394$Nested\n");
	}

	[Theory]
	[InlineData (true)]
	[InlineData (false)]
	public void Execute_EmptyMapProducesZeroByteFile (bool trim)
	{
		var (task, errors) = CreateTask ("", trim, RetainedGraph);

		Assert.True (task.Execute ());
		Assert.Empty (errors);
		AssertOutput (task, "");
	}

	[Theory]
	[InlineData (EmptyGraph)]
	[InlineData ("<DirectedGraph />")]
	[InlineData ("<DirectedGraph><Nodes><Node Label=\"Unrelated node\" /></Nodes></DirectedGraph>")]
	public void Execute_ValidGraphWithNoRetainedTypesProducesZeroByteFile (string graph)
	{
		var (task, errors) = CreateTask ("Example.Type, App;example.Type", true, graph);

		Assert.True (task.Execute ());
		Assert.Empty (errors);
		AssertOutput (task, "");
	}

	[Fact]
	public void Execute_AcceptsGraphWithoutNamespace ()
	{
		var (task, errors) = CreateTask ("Example.Type, App;example.Type", true,
			"<DirectedGraph><Nodes><Node Label=\"Type metadata: [App]Example.Type\" /></Nodes></DirectedGraph>");

		Assert.True (task.Execute ());
		Assert.Empty (errors);
		AssertOutput (task, "example/Type\n");
	}

	[Fact]
	public void Execute_EmptyNodesDoNotIncludeNodesFromLinks ()
	{
		var (task, errors) = CreateTask ("Example.Type, App;example.Type", true, """
			<DirectedGraph>
			  <Nodes />
			  <Links>
			    <Node Label="Type metadata: [App]Example.Type" />
			  </Links>
			</DirectedGraph>
			""");

		Assert.True (task.Execute ());
		Assert.Empty (errors);
		AssertOutput (task, "");
	}

	[Fact]
	public void Execute_RewritesOutputEvenWhenUnchangedAndTruncatesAnEmptySet ()
	{
		var (task, errors) = CreateTask ("Example.Type, App;example.Type", true, RetainedGraph);
		Assert.True (task.Execute ());
		AssertOutput (task, "example/Type\n");

		var oldTime = new DateTime (2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		File.SetLastWriteTimeUtc (task.OutputFile, oldTime);
		Assert.True (task.Execute ());
		Assert.True (File.GetLastWriteTimeUtc (task.OutputFile) > oldTime, "Output is an MSBuild incremental sentinel.");
		AssertOutput (task, "example/Type\n");

		File.WriteAllText (task.NativeAotDgmlFiles [0].ItemSpec, EmptyGraph);
		Assert.True (task.Execute ());
		Assert.Empty (errors);
		AssertOutput (task, "");
	}

	[Theory]
	[InlineData ("map", "XA4320")]
	[InlineData ("graphs", "XA4319")]
	[InlineData ("first-graph", "XA4321")]
	[InlineData ("second-graph", "XA4321")]
	public void Execute_ReportsMissingRequiredInput (string missing, string expectedCode)
	{
		var (task, errors) = CreateTask ("Example.Type, App;example.Type", true, RetainedGraph, RetainedGraph);
		switch (missing) {
		case "map":
			File.Delete (task.AcwMapFile);
			break;
		case "graphs":
			task.NativeAotDgmlFiles = [];
			break;
		case "first-graph":
			File.Delete (task.NativeAotDgmlFiles [0].ItemSpec);
			break;
		case "second-graph":
			File.Delete (task.NativeAotDgmlFiles [1].ItemSpec);
			break;
		}

		Assert.False (task.Execute ());
		Assert.Equal (expectedCode, Assert.Single (errors).Code);
		Assert.False (File.Exists (task.OutputFile));
	}

	[Fact]
	public void Execute_RequiresMapWhenTrimmingDisabled ()
	{
		var (task, errors) = CreateTask ("", false);
		File.Delete (task.AcwMapFile);

		Assert.False (task.Execute ());
		Assert.Equal ("XA4320", Assert.Single (errors).Code);
		Assert.False (File.Exists (task.OutputFile));
	}

	public static IEnumerable<object []> InvalidGraphs ()
	{
		yield return [""];
		yield return ["not XML"];
		yield return ["<DirectedGraph><Nodes><Node"];
		yield return ["<DirectedGraph><Nodes></Nodes><Links>"];
		yield return [EmptyGraph + "<"];
		yield return ["<Project><Nodes /></Project>"];
		yield return ["<DirectedGraph xmlns=\"urn:unknown\"><Nodes /></DirectedGraph>"];
		yield return ["""
			<!DOCTYPE DirectedGraph [<!ENTITY retained "Type metadata: [App]Example.Type">]>
			<DirectedGraph><Nodes><Node Label="&retained;" /></Nodes></DirectedGraph>
			"""];
		yield return ["""
			<!DOCTYPE DirectedGraph SYSTEM "https://example.invalid/graph.dtd">
			<DirectedGraph><Nodes /></DirectedGraph>
			"""];
	}

	[Theory]
	[MemberData (nameof (InvalidGraphs))]
	public void Execute_RejectsMalformedOrUnsupportedGraph (string graph)
	{
		var (task, errors) = CreateTask ("Example.Type, App;example.Type", true, RetainedGraph, graph);

		Assert.False (task.Execute ());
		Assert.Equal ("XA4327", Assert.Single (errors).Code);
		Assert.Contains (task.NativeAotDgmlFiles [1].ItemSpec, errors [0].Message);
		Assert.False (File.Exists (task.OutputFile), "A valid first graph must not result in partial output.");
	}

	[Theory]
	[InlineData ("Type metadata: [App")]
	[InlineData ("Type metadata: []Example.Type")]
	[InlineData ("Type metadata: [App]")]
	[InlineData ("Type metadata: [ ]Example.Type")]
	[InlineData ("Type metadata: [App] ")]
	[InlineData ("Type metadata: Example.Type")]
	public void Execute_RejectsMalformedTypeMetadata (string label)
	{
		var (task, errors) = CreateTask ("Example.Type, App;example.Type", true,
			$"<DirectedGraph><Nodes><Node Label=\"{label}\" /></Nodes></DirectedGraph>");

		Assert.False (task.Execute ());
		Assert.Equal ("XA4327", Assert.Single (errors).Code);
		Assert.False (File.Exists (task.OutputFile));
	}

	[Fact]
	public void Execute_ProhibitsLocalExternalEntities ()
	{
		var (task, errors) = CreateTask ("Example.Type, App;example.Type", true, RetainedGraph);
		var externalFile = Path.ChangeExtension (task.AcwMapFile, ".external.xml");
		const string externalContent = "external-content-must-not-be-read";
		File.WriteAllText (externalFile, externalContent);
		File.WriteAllText (task.NativeAotDgmlFiles [0].ItemSpec, $"""
			<!DOCTYPE DirectedGraph [<!ENTITY external SYSTEM "{new Uri (externalFile).AbsoluteUri}">]>
			<DirectedGraph><Nodes>&external;</Nodes></DirectedGraph>
			""");

		Assert.False (task.Execute ());
		Assert.Equal ("XA4327", Assert.Single (errors).Code);
		Assert.DoesNotContain (externalContent, errors [0].Message);
		Assert.False (File.Exists (task.OutputFile));
	}

	[Theory]
	[InlineData ("missing separator")]
	[InlineData (";example.Type")]
	[InlineData (" ;example.Type")]
	[InlineData ("Example.Type, App;")]
	[InlineData ("Example.Type, App;example.Type;extra")]
	[InlineData ("Example.Type, App;example.Type[1]")]
	[InlineData ("Example.Type, App;example.Type[]")]
	[InlineData ("Example.Type, App;.example.Type")]
	[InlineData ("Example.Type, App;example..Type")]
	[InlineData ("Example.Type, App;example/Type/")]
	[InlineData ("Example.Type, App;example. Type")]
	[InlineData ("Example.Type, App;example.Type\u200b")]
	[InlineData ("Example.Type, App;example.*")]
	[InlineData ("Example.Type, App;-keep class example.Type")]
	public void Execute_RejectsMalformedMapEntries (string map)
	{
		var (task, errors) = CreateTask (map, true, EmptyGraph);

		Assert.False (task.Execute (), "Invalid map data must fail even if the graph retains no types.");
		Assert.Equal ("XA4327", Assert.Single (errors).Code);
		Assert.Contains (task.AcwMapFile, errors [0].Message);
		Assert.False (File.Exists (task.OutputFile));
	}

	[Fact]
	public void Execute_RejectsNumericAliasesWhenTrimmingDisabled ()
	{
		var (task, errors) = CreateTask ("Example.Type;example.Type[1]", false);

		Assert.False (task.Execute ());
		Assert.Equal ("XA4327", Assert.Single (errors).Code);
		Assert.False (File.Exists (task.OutputFile));
	}

	[Theory]
	[InlineData (false)]
	[InlineData (true)]
	public void Execute_RejectsInvalidMapEncoding (bool byteOrderMark)
	{
		var (task, errors) = CreateTask ("", false);
		File.WriteAllBytes (task.AcwMapFile, byteOrderMark ? [0xef, 0xbb, 0xbf, 0xc3, 0x28] : [0xc3, 0x28]);

		Assert.False (task.Execute ());
		Assert.Equal ("XA4327", Assert.Single (errors).Code);
		Assert.False (File.Exists (task.OutputFile));
	}

	(ExtractTypeMapKeysFromDgml Task, List<BuildErrorEventArgs> Errors) CreateTask (string map, bool trim, params string [] graphs)
	{
		Directory.CreateDirectory (directory);
		var acwMapFile = Path.Combine (directory, "acw-map.txt");
		File.WriteAllText (acwMapFile, map);
		var graphItems = new List<ITaskItem> ();
		for (int i = 0; i < graphs.Length; i++) {
			var path = Path.Combine (directory, $"{i}.dgml.xml");
			File.WriteAllText (path, graphs [i]);
			graphItems.Add (new TaskItem (path));
		}
		var outputFile = Path.Combine (directory, "keys", "retained-java-types.txt");
		if (File.Exists (outputFile)) {
			File.Delete (outputFile);
		}
		var engine = new TypeMapTaskBuildEngine ();
		return (new ExtractTypeMapKeysFromDgml {
			BuildEngine = engine,
			NativeAotDgmlFiles = graphItems.ToArray (),
			AcwMapFile = acwMapFile,
			TrimJavaCallableWrappers = trim,
			OutputFile = outputFile,
		}, engine.Errors);
	}

	static void AssertOutput (ExtractTypeMapKeysFromDgml task, string expected)
	{
		Assert.True (File.Exists (task.OutputFile), "Successful extraction must create a real output file.");
		Assert.Equal (new UTF8Encoding (false, true).GetBytes (expected), File.ReadAllBytes (task.OutputFile));
	}

	public void Dispose ()
	{
		if (Directory.Exists (directory)) {
			Directory.Delete (directory, recursive: true);
		}
	}
}
