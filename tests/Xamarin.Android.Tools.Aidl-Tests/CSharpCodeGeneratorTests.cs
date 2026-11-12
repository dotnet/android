using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xamarin.Android.Tools.Aidl;

namespace Xamarin.Android.Tools.Aidl_Tests
{
	// Tests targeting specific code paths of Xamarin.Android.Tools.Aidl.CSharpCodeGenerator.
	// These exist so we don't need to go through the MSBuild integration tests in AidlTest.cs
	// to validate generator behavior.
	[TestClass]
	public class CSharpCodeGeneratorTests : AidlCompilerTestBase
	{
		[TestMethod]
		public void ParcelableIgnore () => RunTest (nameof (ParcelableIgnore), ParcelableHandling.Ignore);

		[TestMethod]
		public void ParcelableStub () => RunTest (nameof (ParcelableStub), ParcelableHandling.Stub);

		[TestMethod]
		public void ParcelableError ()
		{
			const string input = @"package com.xamarin.test;
parcelable MyData;
";
			var compiler = new AidlCompiler ();
			Assert.ThrowsExactly<InvalidOperationException> (() =>
				compiler.Run (input, out _, parcelableHandling: ParcelableHandling.Error));
		}

		[TestMethod]
		public void MultipleInterfaces () => RunTest (nameof (MultipleInterfaces));

		[TestMethod]
		public void OnewayMethods () => RunTest (nameof (OnewayMethods));

		[TestMethod]
		public void IBinderTypes () => RunTest (nameof (IBinderTypes));

		[TestMethod]
		public void CharSequenceType () => RunTest (nameof (CharSequenceType));

		[TestMethod]
		public void ParseErrorIsReported ()
		{
			const string input = "this is not valid aidl @@@";

			var compiler = new AidlCompiler ();
			var results = compiler.Run (input, out var output);

			Assert.IsNull (output, "Output should be null when the input fails to parse.");
			Assert.IsTrue (results.LogMessages.Any (), "Parser error messages should be reported.");
		}
	}
}
