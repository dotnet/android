using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests {
	[TestFixture]
	public class ValidateJavaPeerCallbackFormatsTests : BaseTest {
		[Test]
		public void RejectsLaterRidSpecificUcoAssemblyWithSameFileName ()
		{
			var testDirectory = Path.Combine (Root, "temp", TestName);
			var arm64Assembly = CreateAssembly (Path.Combine (testDirectory, "android-arm64"), callbackFormatVersion: 1);
			var x64Assembly = CreateAssembly (Path.Combine (testDirectory, "android-x64"), callbackFormatVersion: 2);
			var errors = new List<BuildErrorEventArgs> ();
			var task = new ValidateJavaPeerCallbackFormats {
				BuildEngine = new MockBuildEngine (TestContext.Out, errors),
				ResolvedAssemblies = [
					new TaskItem (arm64Assembly),
					new TaskItem (x64Assembly),
				],
			};

			Assert.IsFalse (task.Execute (), "A v2 assembly on a later RID should be rejected.");
			Assert.AreEqual (1, errors.Count);
			var error = errors [0];
			Assert.AreEqual ("XA4265", error.Code);
			StringAssert.Contains ("RidSpecificBinding.dll", error.Message);
		}

		static string CreateAssembly (string directory, int callbackFormatVersion)
		{
			Directory.CreateDirectory (directory);
			var path = Path.Combine (directory, "RidSpecificBinding.dll");
			using var assembly = AssemblyDefinition.CreateAssembly (
				new AssemblyNameDefinition ("RidSpecificBinding", new Version (1, 0)),
				"RidSpecificBinding",
				ModuleKind.Dll);
			var module = assembly.MainModule;
			var attributeType = new TypeReference (
				"Java.Interop",
				"JavaPeerCallbackFormatAttribute",
				module,
				module.TypeSystem.CoreLibrary);
			var constructor = new MethodReference (".ctor", module.TypeSystem.Void, attributeType) {
				HasThis = true,
			};
			constructor.Parameters.Add (new ParameterDefinition (module.TypeSystem.Int32));
			var attribute = new CustomAttribute (constructor);
			attribute.ConstructorArguments.Add (new CustomAttributeArgument (module.TypeSystem.Int32, callbackFormatVersion));
			assembly.CustomAttributes.Add (attribute);
			assembly.Write (path);
			return path;
		}
	}
}
