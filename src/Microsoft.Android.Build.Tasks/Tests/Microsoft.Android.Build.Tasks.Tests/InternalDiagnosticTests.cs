using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Android.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Resources = Xamarin.Android.Tasks.Properties.Resources;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class InternalDiagnosticTests
{
	[Test]
	public void InternalAndOrphanedMessagesAreNotLocalizable ()
	{
		var resources = Resources.ResourceManager.GetResourceSet (CultureInfo.InvariantCulture, true, false)
			?? throw new InvalidOperationException ("The neutral build task resources were not found.");
		string removed = """
			XA0116 XA4219 XA4223 XA4224 XA4226 XA4240 XA4254 XA4255 XA4305
			XA4325 XA4325_MappingNotFound XA4325_MappingDataFailure XA4325_AssemblyReadFailure
			XA4325_AssemblyNotFound XA4325_AssemblyHasNoMetadata XA4325_AmbiguousEntry
			XA4325_NativeAotObjectRequired XA4325_NativeAotObjectReadFailure XA4325_NativeAotModeRequired
			XA4325_NativeAotObjectFormat XA4325_NativeAotInvalidSection XA4325_NativeAotTruncatedSection
			XA4325_NativeAotMissingSections XA4325_NativeAotInvalidDehydration
			XA4327 XA4328 XA4328_NoInputs XA4328_InvalidName XA5303 XA5304 XAGCA7009_UnsupportedRuntime
			XA4217 XA4227 XA4253 XA4264 XA4305_File_Missing XA4308 XA4324 XA0105 XA0107_Ignoring
			XA0118_Parse XA0118_Target XA0119_AAB XA0122 XA0125 XA0139 XA1015 XA1016 XA1017 XA1019
			XA2002_Framework XA2002_NuGet XA2006 XA2007 XA2008 XA2024 XA4209 XA4214 XA4214_Result
			XA4231 XA4243 XA4300 XA4307 XA4311 XA5211 XA5300_Android_Platforms XA5301
			""";

		Assert.Multiple (() => {
			foreach (var name in removed.Split ([' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)) {
				Assert.IsNull (resources.GetString (name), name);
				Assert.IsNull (typeof (Resources).GetProperty (name), name + " accessor");
			}
			Assert.IsNotNull (resources.GetString ("XA4234"), "Public AndroidMavenLibrary metadata errors remain localizable.");
			Assert.IsNotNull (resources.GetString ("XA4232"), "Public AndroidCodegenTarget errors remain localizable.");
			Assert.IsNotNull (resources.GetString ("XA4326"), "Unsupported R8 signatures remain localizable.");
		});
	}

	[Test]
	[SetUICulture ("fr-FR")]
	public void InternalMetadataErrorIsNotLocalized ()
	{
		var errors = new List<BuildErrorEventArgs> ();
		var task = new GenerateTypeMapProguardConfiguration {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
		};
		var item = new TaskItem ("library.dll");

		Assert.IsFalse (item.TryGetRequiredMetadata ("ResolvedAssemblies", "Abi", task.Log, out _));
		Assert.AreEqual (1, errors.Count);
		Assert.AreEqual ("XA4234", errors [0].Code);
		Assert.AreEqual ("'<ResolvedAssemblies>' item 'library.dll' is missing required attribute 'Abi'.", errors [0].Message);
	}

	[Test]
	[SetUICulture ("fr-FR")]
	public void InternalTypeMapErrorIsNotLocalized ()
	{
		var errors = new List<BuildErrorEventArgs> ();
		var task = new GenerateTypeMapProguardConfiguration {
			BuildEngine = new MockBuildEngine (TestContext.Out, errors),
			OutputFile = "classes.cfg",
		};

		Assert.IsFalse (task.Execute ());
		Assert.AreEqual (1, errors.Count);
		Assert.AreEqual ("XA4328", errors [0].Code);
		Assert.AreEqual ("Could not generate typemap ProGuard configuration from 'classes.cfg': No retained Java type map key files were provided.", errors [0].Message);
	}
}
