using System;
using System.IO;
using NUnit.Framework;

namespace Xamarin.Android.Tools.BytecodeTests
{
	[TestFixture]
	public class ParameterFixupTests : ClassFileFixture {

		[Test]
		public void XmlDeclaration_FixedUpFromOtherClasses ()
		{
			AssertXmlDeclaration (new [] {"IParameterInterface.class","ParameterAbstractClass.class", "ParameterClass.class", "ParameterClass2.class"}, "ParameterFixup.xml");
		}

		[Test]
		public void XmlDeclaration_FixedUpFromDocumentation()
		{
			var androidSdkPath  = Environment.GetEnvironmentVariable ("ANDROID_SDK_PATH");
			if (string.IsNullOrEmpty (androidSdkPath)) {
				Assert.Ignore ("The `ANDROID_SDK_PATH` environment variable isn't set; " +
						"cannot test importing parameter names from HTML. Skipping...");
				return;
			}
			try {
				AssertXmlDeclaration ("Collection.class", "ParameterFixupFromDocs.xml", Path.Combine (Environment.GetEnvironmentVariable ("ANDROID_SDK_PATH"), "docs", "reference"));
			} catch (Exception ex) {
				Assert.Fail ("An unexpected exception was thrown : {0}", ex);
			}
		}

		[Test]
		public void XmlDeclaration_DoesNotThrowAnExceptionIfDocumentationNotFound ()
		{
			try {
				AssertXmlDeclaration (new [] {"IParameterInterface.class","ParameterAbstractClass.class", "ParameterClass.class", "ParameterClass2.class"}, "ParameterFixup.xml", "SomeNonExistantPath");
			} catch (Exception ex) {
				Assert.Fail ("An unexpected exception was thrown : {0}", ex);
			}
		}
	}
}

