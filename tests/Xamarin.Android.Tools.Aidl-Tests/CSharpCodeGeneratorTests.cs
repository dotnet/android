using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using Xamarin.Android.Tools.Aidl;

namespace Xamarin.Android.Tools.Aidl_Tests
{
	// Tests targeting specific code paths of Xamarin.Android.Tools.Aidl.CSharpCodeGenerator.
	// These exist so we don't need to go through the MSBuild integration tests in AidlTest.cs
	// to validate generator behavior.
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
		public void NullableEnabled ()
		{
			var compiler = new AidlCompiler ();
			var results = compiler.Run ("""
				interface Test {
					int add (int left, int right);
					oneway void notify (int value);
				}
				""", out var output);
			Assert.IsEmpty (results.LogMessages);

			const string support = """
				#nullable enable
				namespace Java.Lang {
					public class Object {}
				}
				namespace Android.Runtime {
					public class JavaList {}
					public class JavaDictionary {}
				}
				namespace Android.OS {
					public interface IInterface {
						IBinder AsBinder ();
					}
					public interface IBinder {
						const int InterfaceTransaction = 1598968902;
						IInterface? QueryLocalInterface (string descriptor);
						bool Transact (int code, Parcel data, Parcel? reply, TransactionFlags flags);
					}
					public class Binder : Java.Lang.Object, IBinder {
						public static class InterfaceConsts {
							public const int FirstCallTransaction = 1;
						}
						public void AttachInterface (IInterface owner, string descriptor) {}
						public IInterface? QueryLocalInterface (string descriptor) => null;
						public bool Transact (int code, Parcel data, Parcel? reply, TransactionFlags flags) => false;
						protected virtual bool OnTransact (int code, Parcel data, Parcel? reply, int flags) => false;
					}
					public enum TransactionFlags { Oneway = 1 }
					public class Parcel {
						public static Parcel Obtain () => new Parcel ();
						public int ReadInt () => 0;
						public void WriteInt (int value) {}
						public void WriteString (string value) {}
						public void WriteInterfaceToken (string value) {}
						public void EnforceInterface (string value) {}
						public void WriteNoException () {}
						public void ReadException () {}
						public void Recycle () {}
					}
				}
				public static class NullableConsumer {
					public static ITest? FromNullableBinder (Android.OS.IBinder? binder)
						=> ITestStub.AsInterface (binder);
					public static ITest FromBinder (Android.OS.IBinder binder)
						=> ITestStub.AsInterface (binder);
				}
				""";
			var compilation = CSharpCompilation.Create (
				nameof (NullableEnabled),
				syntaxTrees: new [] {
					CSharpSyntaxTree.ParseText ("#nullable enable\n" + output, path: "ITest.cs"),
					CSharpSyntaxTree.ParseText (support, path: "Support.cs"),
				},
				references: new [] { MetadataReference.CreateFromFile (typeof (object).Assembly.Location) },
				options: new CSharpCompilationOptions (OutputKind.DynamicallyLinkedLibrary,
					nullableContextOptions: NullableContextOptions.Enable,
					generalDiagnosticOption: ReportDiagnostic.Error));

			using var assembly = new MemoryStream ();
			var result = compilation.Emit (assembly);
			Assert.IsTrue (result.Success, string.Join (Environment.NewLine, result.Diagnostics));
		}

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
