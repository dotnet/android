#nullable enable
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Build.Tasks;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests.Tasks;

[TestFixture]
public class LlvmIrWriterTests
{
	[Test]
	public void QuotesUtf8AndReservedBytes ()
	{
		byte[] bytes = Encoding.UTF8.GetBytes ("c/D\"\\path\u00e9");

		Assert.That (LlvmIrWriter.QuoteBytes (bytes, nullTerminated: true), Is.EqualTo ("c\"c/D\\22\\5Cpath\\C3\\A9\\00\""));
		Assert.That (LlvmIrWriter.QuoteBytes (bytes, nullTerminated: false), Is.EqualTo ("c\"c/D\\22\\5Cpath\\C3\\A9\""));
	}

	[Test]
	public void StringBlobDeduplicatesStringsByOffset ()
	{
		var blob = new LlvmIrStringBlob ();

		Assert.That (blob.Add ("a"), Is.EqualTo (0));
		Assert.That (blob.Add ("\u00e9"), Is.EqualTo (2));
		Assert.That (blob.Add ("a"), Is.EqualTo (0));
		Assert.That (blob.Size, Is.EqualTo (5));
		Assert.That (blob.Value (), Is.EqualTo ("c\"a\\00\\C3\\A9\\00\""));
	}

	[Test]
	public void CommentsCannotInjectLinesIntoIr ()
	{
		using var output = new StringWriter ();
		var writer = new LlvmIrWriter (output, LlvmIrTarget.Arm64, emitComments: true);

		Assert.That (writer.Comment (" first\nsecond"), Does.Not.Contain ("\n"));
		Assert.That (writer.TrailingComment (" first\rsecond"), Does.Not.Contain ("\r"));
		writer.WriteCommentLine (" first\nsecond");
		Assert.That (output.ToString ().Split ('\n'), Has.Length.EqualTo (2));
	}

	[Test]
	public void NativeAotJniInitDeduplicatesHandlers ()
	{
		var log = new TaskLoggingHelper (new MockBuildEngine (TestContext.Out), "test");
		var generator = new NativeAotJniInitNativeAssemblyGenerator (
			log,
			["AndroidCryptoNative_InitLibraryOnLoad"],
			["AndroidCryptoNative_InitLibraryOnLoad", "My_OnLoad"]
		);

		foreach (AndroidTargetArch arch in new [] { AndroidTargetArch.Arm, AndroidTargetArch.Arm64, AndroidTargetArch.X86, AndroidTargetArch.X86_64 }) {
			using var writer = new StringWriter ();
			generator.Generate (arch, writer, "jni_init_funcs.ll");
			string output = writer.ToString ();

			Assert.That (output, Does.Contain ("@__jni_on_load_handler_count = dso_local local_unnamed_addr constant i32 2"), arch.ToString ());
			Assert.That (output, Does.Contain ("@__jni_on_load_handlers = dso_local local_unnamed_addr constant [2 x ptr]"), arch.ToString ());
			Assert.That (output, Does.Contain ("@__jni_on_load_handler_names = dso_local local_unnamed_addr constant [2 x ptr]"), arch.ToString ());
			Assert.That (Regex.Matches (output, "declare void @AndroidCryptoNative_InitLibraryOnLoad\\(\\)").Count, Is.EqualTo (1), arch.ToString ());
			Assert.That (Regex.Matches (output, "declare void @My_OnLoad\\(\\)").Count, Is.EqualTo (1), arch.ToString ());
		}
	}
}
