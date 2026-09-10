extern alias xamarinbuildtasks;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Android.Sdk.TrimmableTypeMap;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using FieldRvaTable = xamarinbuildtasks::Xamarin.Android.Tasks.JniRemapping.FieldRvaTable;
using JniAssemblyRewriter = xamarinbuildtasks::Xamarin.Android.Tasks.JniRemapping.JniAssemblyRewriter;
using JniRewriteResult = xamarinbuildtasks::Xamarin.Android.Tasks.JniRemapping.JniRewriteResult;
using R8Mapping = xamarinbuildtasks::Xamarin.Android.Tasks.JniRemapping.R8Mapping;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Parallelizable (ParallelScope.Children)]
public class JniAssemblyRewriterTypeMapTests
{
	[Test]
	public void RewritesGeneratedTypeMapWithOwnerSpecificMethodNames ()
	{
		byte [] source = GenerateTypeMapWithSharedMethodName ();
		var warnings = new List<BuildWarningEventArgs> ();

		JniRewriteResult result = Rewrite (source, Mapping (
			"test.First -> a.b.First:\n" +
			"    void n_Run() -> a\n" +
			"test.Second -> a.b.Second:\n" +
			"    void n_Run() -> b\n"), warnings);

		CollectionAssert.AreEquivalent (new [] { "a", "b", "()V" }, ReadUtf8Values (result.Image));
		CollectionAssert.DoesNotContain (warnings.Select (warning => warning.Code).ToArray (), "XA4326");
	}

	[Test]
	public void RewritesGeneratedTypeMapWithMappedAndUnmappedMethodNames ()
	{
		byte [] source = GenerateTypeMapWithSharedMethodName ();
		var warnings = new List<BuildWarningEventArgs> ();

		JniRewriteResult result = Rewrite (source, Mapping (
			"test.First -> a.b.First:\n" +
			"    void n_Run() -> a\n" +
			"test.Second -> test.Second:\n"), warnings);

		CollectionAssert.AreEquivalent (new [] { "a", "n_Run", "()V" }, ReadUtf8Values (result.Image));
		CollectionAssert.DoesNotContain (warnings.Select (warning => warning.Code).ToArray (), "XA4326");
	}

	static JniRewriteResult Rewrite (byte [] sourceImage, R8Mapping mapping, IList<BuildWarningEventArgs> warnings)
	{
		var engine = new MockBuildEngine (TestContext.Out, warnings: warnings);
		var log = new TaskLoggingHelper (engine, nameof (JniAssemblyRewriterTypeMapTests));
		return JniAssemblyRewriter.Rewrite (sourceImage, mapping, log);
	}

	static R8Mapping Mapping (string text) => R8Mapping.Parse (new StringReader (text));

	static byte [] GenerateTypeMapWithSharedMethodName ()
	{
		var peers = new [] {
			CreatePeer ("test/First", "Test.First"),
			CreatePeer ("test/Second", "Test.Second"),
		};
		using var stream = new MemoryStream ();
		new TypeMapAssemblyGenerator (new Version (11, 0, 0, 0)).Generate (peers, stream, "OwnerSpecificNames");
		return stream.ToArray ();
	}

	static JavaPeerInfo CreatePeer (string javaName, string managedName)
	{
		int separator = managedName.LastIndexOf ('.');
		return new JavaPeerInfo {
			JavaName = javaName,
			CompatJniName = javaName,
			ManagedTypeName = managedName,
			ManagedTypeNamespace = managedName.Substring (0, separator),
			ManagedTypeShortName = managedName.Substring (separator + 1),
			AssemblyName = "TestAsm",
			DoNotGenerateAcw = false,
			ActivationCtor = new ActivationCtorInfo {
				DeclaringTypeName = managedName,
				DeclaringAssemblyName = "TestAsm",
				Style = ActivationCtorStyle.XamarinAndroid,
			},
			MarshalMethods = [
				new MarshalMethodInfo {
					JniName = "run",
					NativeCallbackName = "n_Run",
					JniSignature = "()V",
					ManagedMethodName = "Run",
				},
			],
		};
	}

	static string [] ReadUtf8Values (byte [] image)
	{
		using var peReader = new PEReader (ImmutableArray.Create (image));
		MetadataReader reader = peReader.GetMetadataReader ();
		return FieldRvaTable.Read (peReader, reader).Entries
			.Select (entry => entry.Utf8Value)
			.OfType<string> ()
			.ToArray ();
	}
}
