using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Xamarin.Android.Tools.Aidl;

namespace Xamarin.Android.Tools.Aidl_Tests
{
	// Tests targeting specific code paths of Xamarin.Android.Tools.Aidl.CSharpCodeGenerator.
	// These exist so we don't need to go through the MSBuild integration tests in AidlTest.cs
	// to validate generator behavior. See https://github.com/dotnet/android/issues/...
	[TestFixture]
	public class CSharpCodeGeneratorTests : AidlCompilerTestBase
	{
		[Test]
		public void ParcelableIgnore () => RunTest (nameof (ParcelableIgnore), ParcelableHandling.Ignore);

		[Test]
		public void ParcelableStub () => RunTest (nameof (ParcelableStub), ParcelableHandling.Stub);

		[Test]
		public void ParcelableError ()
		{
			const string input = @"package com.xamarin.test;
parcelable MyData;
";
			var compiler = new AidlCompiler ();
			Assert.Throws<InvalidOperationException> (() =>
				compiler.Run (input, out _, parcelableHandling: ParcelableHandling.Error));
		}

		[Test]
		public void MultipleInterfaces () => RunTest (nameof (MultipleInterfaces));

		[Test]
		public void OnewayMethods () => RunTest (nameof (OnewayMethods));

		[Test]
		public void IBinderTypes () => RunTest (nameof (IBinderTypes));

		[Test]
		public void CharSequenceType () => RunTest (nameof (CharSequenceType));

		[Test]
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
