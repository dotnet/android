using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks.LLVMIR;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Build.Tests.Tasks
{
	[TestFixture]
	public class LlvmIrGeneratorTests
	{
		/// <summary>
		/// Regression test for https://github.com/dotnet/android/issues/10086
		///
		/// When a managed method parameter has a whitespace-only name, the LLVM IR
		/// generator would produce invalid IR like:
		///   define void @test(ptr noundef % )
		///
		/// The fix ensures whitespace-only names are normalized to null before
		/// creating LlvmIrFunctionParameter instances, so that LlvmIrFunction
		/// assigns them valid numeric names (e.g., %0, %1).
		/// </summary>
		[Test]
		[TestCase (null, Description = "Null parameter name")]
		[TestCase ("", Description = "Empty parameter name")]
		[TestCase (" ", Description = "Space-only parameter name")]
		[TestCase ("  ", Description = "Multiple spaces parameter name")]
		[TestCase ("\t", Description = "Tab-only parameter name")]
		public void FunctionParameterWithInvalidName_GetsNumericName (string? paramName)
		{
			var parameters = new List<LlvmIrFunctionParameter> {
				new LlvmIrFunctionParameter (typeof (IntPtr), "env"),
				new LlvmIrFunctionParameter (typeof (IntPtr), "klass"),
				new LlvmIrFunctionParameter (typeof (IntPtr), paramName),
			};

			var func = new LlvmIrFunction ("test_function", typeof (void), parameters);

			// The third parameter should have been assigned a numeric name
			var thirdParam = func.Signature.Parameters[2];
			Assert.IsNotNull (thirdParam.Name, "Parameter name should not be null after LlvmIrFunction construction");
			Assert.IsNotEmpty (thirdParam.Name, "Parameter name should not be empty after LlvmIrFunction construction");
			Assert.That (thirdParam.Name.Trim (), Is.Not.Empty, "Parameter name should not be whitespace-only after LlvmIrFunction construction");
		}

		/// <summary>
		/// Verifies that the LLVM IR generator produces valid function signatures
		/// when parameters have valid names, including numeric names assigned to
		/// previously unnamed parameters.
		/// </summary>
		[Test]
		public void GeneratedIR_FunctionWithUnnamedParameter_ProducesValidOutput ()
		{
			var parameters = new List<LlvmIrFunctionParameter> {
				new LlvmIrFunctionParameter (typeof (IntPtr), "env"),
				new LlvmIrFunctionParameter (typeof (IntPtr), "klass"),
				new LlvmIrFunctionParameter (typeof (IntPtr), null), // unnamed parameter
			};

			var func = new LlvmIrFunction ("test_function", typeof (void), parameters);

			var log = new TaskLoggingHelper (new MockBuildEngine (TestContext.Out, [], [], []), "test");
			var module = new LlvmIrModule (new LlvmIrTypeCache (), log);
			func.Body.Ret (typeof (void));
			module.Add (func);
			module.AfterConstruction ();

			var generator = LlvmIrGenerator.Create (AndroidTargetArch.Arm64, "test.ll");
			using var writer = new StringWriter ();
			generator.Generate (writer, module);

			string output = writer.ToString ();
			// The output should contain valid parameter declarations - no "% " pattern
			Assert.That (output, Does.Not.Contain ("% "), "Generated LLVM IR should not contain whitespace-only parameter names");
			Assert.That (output, Does.Contain ("@test_function"), "Generated LLVM IR should contain the function name");
		}
	}
}
