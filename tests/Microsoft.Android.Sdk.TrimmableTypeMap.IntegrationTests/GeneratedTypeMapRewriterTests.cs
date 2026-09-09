using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;
using Xamarin.Android.Tasks.JniRemapping;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.IntegrationTests;

public class GeneratedTypeMapRewriterTests
{
	[Fact]
	public void RewritesGeneratedTypeMapWithOwnerSpecificMethodNames ()
	{
		byte [] source = GenerateTypeMapWithSharedMethodName ();
		var warnings = new List<BuildWarningEventArgs> ();

		JniRewriteResult result = Rewrite (source, """
			test.First -> a.b.First:
			    void n_Run() -> a
			test.Second -> a.b.Second:
			    void n_Run() -> b
			""", warnings);

		Assert.Equal (new [] { "()V", "a", "b" }, ReadUtf8Values (result.Image).OrderBy (value => value, StringComparer.Ordinal));
		Assert.DoesNotContain (warnings, warning => warning.Code == "XA4326");
	}

	[Fact]
	public void RewritesGeneratedTypeMapWithMappedAndUnmappedMethodNames ()
	{
		byte [] source = GenerateTypeMapWithSharedMethodName ();
		var warnings = new List<BuildWarningEventArgs> ();

		JniRewriteResult result = Rewrite (source, """
			test.First -> a.b.First:
			    void n_Run() -> a
			test.Second -> test.Second:
			""", warnings);

		Assert.Equal (new [] { "()V", "a", "n_Run" }, ReadUtf8Values (result.Image).OrderBy (value => value, StringComparer.Ordinal));
		Assert.DoesNotContain (warnings, warning => warning.Code == "XA4326");
	}

	static JniRewriteResult Rewrite (byte [] sourceImage, string mappingText, IList<BuildWarningEventArgs> warnings)
	{
		using var reader = new StringReader (mappingText);
		var log = new TaskLoggingHelper (new MockBuildEngine (warnings), nameof (GeneratedTypeMapRewriterTests));
		return JniAssemblyRewriter.Rewrite (sourceImage, R8Mapping.Parse (reader), log);
	}

	static byte [] GenerateTypeMapWithSharedMethodName ()
	{
		var peers = new [] {
			CreatePeer ("test/First", "Test.First"),
			CreatePeer ("test/Second", "Test.Second"),
		};
		using var stream = new MemoryStream ();
		new TypeMapAssemblyGenerator (new Version (11, 0, 0, 0)).Generate (peers, stream, "OwnerSpecificNames");
		return stream.ToArray ();

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
