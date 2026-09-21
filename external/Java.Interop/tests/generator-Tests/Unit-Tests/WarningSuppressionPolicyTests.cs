using System;
using System.Collections.Generic;
using System.Linq;
using MonoDroid.Generation;
using NUnit.Framework;
using Xamarin.Android.Binder;

namespace generatortests
{
	// The generator suppresses a C# diagnostic only where it has positively determined that
	// faithfully projecting a Java API provokes it. These tests pin that policy down in both
	// directions: the member that has to be suppressed, and -- just as importantly -- the
	// member that must not be, because a suppression that silences nothing today will
	// silently swallow a regression tomorrow.
	[TestFixture]
	class WarningSuppressionPolicyTests : CodeGeneratorTestBase
	{
		protected override CodeGenerationTarget Target => CodeGenerationTarget.XAJavaInterop1;

		protected override CodeGenerationOptions CreateOptions ()
		{
			var options = base.CreateOptions ();

			options.AssemblyName = "MyAssembly";
			options.SupportNullableReferenceTypes = true;

			return options;
		}

		const string Prologue = @"<api>
			  <enum name='Example.MyEnum' />
			  <package name='java.lang' jni-name='java/lang'>
			    <class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />
			  </package>
			  <package name='com.xamarin.android' jni-name='com/xamarin/android'>";

		const string Epilogue = @"</package>
			</api>";

		static string Api (string body) => Prologue + body + Epilogue;

		static string Class (string name, string body, string extends = "java.lang.Object", string deprecated = "not deprecated", bool isAbstract = false) =>
			$@"<class abstract='{(isAbstract ? "true" : "false")}' deprecated='{deprecated}' extends='{extends}' extends-generic-aware='{extends}' jni-extends='L{extends.Replace ('.', '/')};' final='false' name='{name}' static='false' visibility='public' jni-signature='Lcom/xamarin/android/{name};'>{body}</class>";

		static string Interface (string name, string body, string deprecated = "not deprecated") =>
			$@"<interface abstract='true' deprecated='{deprecated}' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='{name}' static='false' visibility='public' jni-signature='Lcom/xamarin/android/{name};'>{body}</interface>";

		static string Implements (string name) =>
			$@"<implements name='com.xamarin.android.{name}' name-generic-aware='com.xamarin.android.{name}' jni-type='Lcom/xamarin/android/{name};' />";

		// A method returning a reference type, optionally annotated as non-null.
		static string StringMethod (string name, bool returnNotNull, bool isAbstract = false, string deprecated = "not deprecated", string extra = "") =>
			$@"<method abstract='{(isAbstract ? "true" : "false")}' deprecated='{deprecated}' final='false' name='{name}' jni-signature='()Ljava/lang/String;' bridge='false' native='false' return='java.lang.String' jni-return='Ljava/lang/String;' return-not-null='{(returnNotNull ? "true" : "false")}' static='false' synchronized='false' synthetic='false' visibility='public' {extra} />";

		// A void method taking a single reference-type parameter, optionally annotated as
		// non-null.
		static string StringParameterMethod (string name, bool parameterNotNull, bool isAbstract = false, string deprecated = "not deprecated", string extra = "") =>
			$@"<method abstract='{(isAbstract ? "true" : "false")}' deprecated='{deprecated}' final='false' name='{name}' jni-signature='(Ljava/lang/String;)V' bridge='false' native='false' return='void' jni-return='V' static='false' synchronized='false' synthetic='false' visibility='public' {extra}>
			      <parameter name='value' type='java.lang.String' jni-type='Ljava/lang/String;' not-null='{(parameterNotNull ? "true" : "false")}' />
			    </method>";

		// A void method taking a single parameter bound as a generated enum, optionally
		// annotated as non-null.
		static string EnumParameterMethod (string name, string enumType, bool parameterNotNull) =>
			$@"<method abstract='false' deprecated='not deprecated' final='false' name='{name}' jni-signature='(I)V' bridge='false' native='false' return='void' jni-return='V' static='false' synchronized='false' synthetic='false' visibility='public'>
			      <parameter name='value' type='int' jni-type='I' enumType='{enumType}' not-null='{(parameterNotNull ? "true" : "false")}' />
			    </method>";

		string Generate (string xml, string typeName)
		{
			var gens = ParseApiDefinition (xml);
			var gen = gens.Single (g => g.Name == typeName);

			generator.Context.ContextTypes.Push (gen);
			generator.WriteType (gen, string.Empty, new GenerationInfo ("", "", "MyAssembly"));
			generator.Context.ContextTypes.Pop ();

			return writer.ToString ();
		}

		static IEnumerable<string> SuppressedCodes (string generated) =>
			generated.Split ('\n')
				.Select (l => l.Trim ())
				.Where (l => l.StartsWith ("#pragma warning disable ", StringComparison.Ordinal))
				.Select (l => l.Substring ("#pragma warning disable ".Length).Trim ())
				// CS0169 and CS0649 are written unconditionally around the generated field
				// declarations and predate the computed suppressions.
				.Where (c => c != "0169" && c != "0649");

		static void AssertSuppresses (string generated, params string [] expected)
		{
			Assert.AreEqual (
					expected.OrderBy (c => c, StringComparer.Ordinal).ToList (),
					SuppressedCodes (generated).Distinct ().OrderBy (c => c, StringComparer.Ordinal).ToList (),
					$"was:\n{generated}");
		}

		#region Return type nullability

		[Test]
		public void ClassOverrideWideningTheReturnContractIsSuppressed ()
		{
			// The base method guarantees non-null; the override does not, which is CS8764.
			var xml = Api (
					Class ("MyBase", StringMethod ("getName", returnNotNull: true)) +
					Class ("MyClass", StringMethod ("getName", returnNotNull: false), extends: "com.xamarin.android.MyBase"));

			AssertSuppresses (Generate (xml, "MyClass"), "8764");
		}

		[Test]
		public void ClassOverrideNarrowingTheReturnContractIsNotSuppressed ()
		{
			// Return types are covariant: promising non-null where the base allows null is
			// safe, so the compiler says nothing and neither should the generator.
			var xml = Api (
					Class ("MyBase", StringMethod ("getName", returnNotNull: false)) +
					Class ("MyClass", StringMethod ("getName", returnNotNull: true), extends: "com.xamarin.android.MyBase"));

			AssertSuppresses (Generate (xml, "MyClass"));
		}

		#endregion

		#region Parameter nullability

		[Test]
		public void ClassOverrideNarrowingAParameterContractIsSuppressed ()
		{
			// The base method accepts null; the override refuses it, which is CS8765.
			var xml = Api (
					Class ("MyBase", StringParameterMethod ("setName", parameterNotNull: false)) +
					Class ("MyClass", StringParameterMethod ("setName", parameterNotNull: true), extends: "com.xamarin.android.MyBase"));

			AssertSuppresses (Generate (xml, "MyClass"), "8765");
		}

		[Test]
		public void ClassOverrideWideningAParameterContractIsNotSuppressed ()
		{
			// Parameters are contravariant: accepting null where the base requires non-null is
			// safe.
			var xml = Api (
					Class ("MyBase", StringParameterMethod ("setName", parameterNotNull: true)) +
					Class ("MyClass", StringParameterMethod ("setName", parameterNotNull: false), extends: "com.xamarin.android.MyBase"));

			AssertSuppresses (Generate (xml, "MyClass"));
		}

		#endregion

		#region Interface implementations get their own diagnostic codes

		[Test]
		public void ImplicitInterfaceImplementationUsesTheInterfaceReturnCode ()
		{
			var xml = Api (
					Interface ("MyInterface", StringMethod ("getName", returnNotNull: true, isAbstract: true)) +
					Class ("MyClass", Implements ("MyInterface") + StringMethod ("getName", returnNotNull: false)));

			AssertSuppresses (Generate (xml, "MyClass"), "8766");
		}

		[Test]
		public void ImplicitInterfaceImplementationUsesTheInterfaceParameterCode ()
		{
			var xml = Api (
					Interface ("MyInterface", StringParameterMethod ("setName", parameterNotNull: false, isAbstract: true)) +
					Class ("MyClass", Implements ("MyInterface") + StringParameterMethod ("setName", parameterNotNull: true)));

			AssertSuppresses (Generate (xml, "MyClass"), "8767");
		}

		[Test]
		public void CleanInterfaceImplementationIsNotSuppressed ()
		{
			var xml = Api (
					Interface ("MyInterface", StringMethod ("getName", returnNotNull: true, isAbstract: true)) +
					Class ("MyClass", Implements ("MyInterface") + StringMethod ("getName", returnNotNull: true)));

			AssertSuppresses (Generate (xml, "MyClass"));
		}

		#endregion

		#region Obsolete

		[Test]
		public void NonDeprecatedOverrideOfADeprecatedMemberIsSuppressed ()
		{
			// Java allows it; C# reports CS0672 on the declaration. The marshalling callback
			// dispatches to the override, which C# resolves against the deprecated base
			// declaration, so it reports CS0618 there as well.
			var xml = Api (
					Class ("MyBase", StringMethod ("getName", returnNotNull: true, deprecated: "This is deprecated.")) +
					Class ("MyClass", StringMethod ("getName", returnNotNull: true), extends: "com.xamarin.android.MyBase"));

			AssertSuppresses (Generate (xml, "MyClass"), "0672", "0618");
		}

		[Test]
		public void DeprecatedOverrideOfANonDeprecatedMemberIsSuppressed ()
		{
			// The reverse direction is CS0809, not CS0672.
			var xml = Api (
					Class ("MyBase", StringMethod ("getName", returnNotNull: true)) +
					Class ("MyClass", StringMethod ("getName", returnNotNull: true, deprecated: "This is deprecated."), extends: "com.xamarin.android.MyBase"));

			AssertSuppresses (Generate (xml, "MyClass"), "0809");
		}

		[Test]
		public void OverrideMatchingItsBaseDeprecationIsNotSuppressed ()
		{
			var xml = Api (
					Class ("MyBase", StringMethod ("getName", returnNotNull: true, deprecated: "This is deprecated.")) +
					Class ("MyClass", StringMethod ("getName", returnNotNull: true, deprecated: "This is deprecated."), extends: "com.xamarin.android.MyBase"));

			AssertSuppresses (Generate (xml, "MyClass"));
		}

		[Test]
		public void ReferringToADeprecatedTypeIsSuppressed ()
		{
			var xml = Api (
					Class ("MyOldType", "", deprecated: "This is deprecated.") +
					Class ("MyClass", $@"<method abstract='false' deprecated='not deprecated' final='false' name='getOld' jni-signature='()Lcom/xamarin/android/MyOldType;' bridge='false' native='false' return='com.xamarin.android.MyOldType' jni-return='Lcom/xamarin/android/MyOldType;' static='false' synchronized='false' synthetic='false' visibility='public' />"));

			AssertSuppresses (Generate (xml, "MyClass"), "0618");
		}

		[Test]
		public void ReferringToADeprecatedTypeFromInsideADeprecatedTypeIsNotSuppressed ()
		{
			// C# does not report the use of a deprecated member from inside a type that is
			// itself `[Obsolete]`, so a suppression there would silence nothing.
			var xml = Api (
					Class ("MyOldType", "", deprecated: "This is deprecated.") +
					Class ("MyClass", $@"<method abstract='false' deprecated='not deprecated' final='false' name='getOld' jni-signature='()Lcom/xamarin/android/MyOldType;' bridge='false' native='false' return='com.xamarin.android.MyOldType' jni-return='Lcom/xamarin/android/MyOldType;' static='false' synchronized='false' synthetic='false' visibility='public' />", deprecated: "This is deprecated."));

			AssertSuppresses (Generate (xml, "MyClass"));
		}

		#endregion

		#region `new` members hide rather than override

		[Test]
		public void ShadowingMemberDoesNotGetTheClassOverrideSuppression ()
		{
			// `managedOverride='new'` emits `new`, which hides the inherited member instead of
			// overriding it, so the class-override nullability rules never apply.
			var xml = Api (
					Class ("MyBase", StringMethod ("getName", returnNotNull: true)) +
					Class ("MyClass", StringMethod ("getName", returnNotNull: false, extra: "managedOverride='new'"), extends: "com.xamarin.android.MyBase"));

			var generated = Generate (xml, "MyClass");

			StringAssert.Contains ("new", generated);
			AssertSuppresses (generated);
		}

		[Test]
		public void ShadowingMemberStillGetsTheInterfaceSuppression ()
		{
			// A `new` member is still an implicit implementation of a matching interface
			// member, so the interface rules do apply to it.
			var xml = Api (
					Interface ("MyInterface", StringMethod ("getName", returnNotNull: true, isAbstract: true)) +
					Class ("MyBase", StringMethod ("getName", returnNotNull: true)) +
					Class ("MyClass", Implements ("MyInterface") + StringMethod ("getName", returnNotNull: false, extra: "managedOverride='new'"), extends: "com.xamarin.android.MyBase"));

			AssertSuppresses (Generate (xml, "MyClass"), "8766");
		}

		#endregion

		#region Declared mismatches against a hand-bound base

		[Test]
		public void DeclaredReturnMismatchIsSuppressed ()
		{
			// The base member is hand-bound, so the generator cannot see its annotations.
			// `managedNullabilityMismatch` records what the compiler will report instead of
			// the binding having to misstate the Java API's contract.
			var xml = Api (Class ("MyClass", StringMethod ("getName", returnNotNull: false, extra: "managedNullabilityMismatch='return'")));

			var generated = Generate (xml, "MyClass");

			StringAssert.Contains ("string? Name", generated);
			AssertSuppresses (generated, "8764");
		}

		[Test]
		public void DeclaredParameterMismatchIsSuppressed ()
		{
			var xml = Api (Class ("MyClass", StringParameterMethod ("setName", parameterNotNull: true, extra: "managedNullabilityMismatch='parameters'")));

			AssertSuppresses (Generate (xml, "MyClass"), "8765");
		}

		[Test]
		public void UnknownDeclaredMismatchIsRejected ()
		{
			var xml = Api (Class ("MyClass", StringMethod ("getName", returnNotNull: false, extra: "managedNullabilityMismatch='everything'")));

			Assert.Throws<InvalidOperationException> (() => Generate (xml, "MyClass"));
		}

		#endregion

		#region Value types are never annotated

		[Test]
		public void PrimitiveReturnMismatchIsNotSuppressed ()
		{
			const string IntMethod = @"<method abstract='false' deprecated='not deprecated' final='false' name='getCount' jni-signature='()I' bridge='false' native='false' return='int' jni-return='I' static='false' synchronized='false' synthetic='false' visibility='public' {0} />";

			var xml = Api (
					Class ("MyBase", string.Format (IntMethod, "return-not-null='true'")) +
					Class ("MyClass", string.Format (IntMethod, ""), extends: "com.xamarin.android.MyBase"));

			AssertSuppresses (Generate (xml, "MyClass"));
		}

		[Test]
		public void ArrayReturnMismatchIsSuppressed ()
		{
			// An array is a reference type even when its elements are not, so it is annotated
			// and can mismatch.
			const string IntArrayMethod = @"<method abstract='false' deprecated='not deprecated' final='false' name='getCounts' jni-signature='()[I' bridge='false' native='false' return='int[]' jni-return='[I' static='false' synchronized='false' synthetic='false' visibility='public' {0} />";

			var xml = Api (
					Class ("MyBase", string.Format (IntArrayMethod, "return-not-null='true'")) +
					Class ("MyClass", string.Format (IntArrayMethod, ""), extends: "com.xamarin.android.MyBase"));

			AssertSuppresses (Generate (xml, "MyClass"), "8764");
		}

		[Test]
		public void EnumParameterMismatchIsNotSuppressed ()
		{
			// An enum is a value type, so it is never annotated and cannot mismatch.
			var xml = Api (
					Class ("MyBase", EnumParameterMethod ("setMode", "Example.MyEnum", parameterNotNull: false)) +
					Class ("MyClass", EnumParameterMethod ("setMode", "Example.MyEnum", parameterNotNull: true), extends: "com.xamarin.android.MyBase"));

			AssertSuppresses (Generate (xml, "MyClass"));
		}

		[Test]
		public void EnumArrayParameterMismatchIsSuppressed ()
		{
			// An array of enums is still an array, so it is annotated and can mismatch.
			var xml = Api (
					Class ("MyBase", EnumParameterMethod ("setModes", "Example.MyEnum[]", parameterNotNull: false)) +
					Class ("MyClass", EnumParameterMethod ("setModes", "Example.MyEnum[]", parameterNotNull: true), extends: "com.xamarin.android.MyBase"));

			AssertSuppresses (Generate (xml, "MyClass"), "8765");
		}

		#endregion

		#region Explicit interface implementations get a third pair of codes

		[Test]
		public void ExplicitInterfaceImplementationUsesTheExplicitReturnCode ()
		{
			var xml = Api (
					Interface ("MyInterface", StringMethod ("getName", returnNotNull: true, isAbstract: true)) +
					Class ("MyClass", Implements ("MyInterface") + StringMethod ("getName", returnNotNull: false, extra: "explicitInterface='com.xamarin.android.MyInterface'")));

			AssertSuppresses (Generate (xml, "MyClass"), "8768");
		}

		[Test]
		public void ExplicitInterfaceImplementationUsesTheExplicitParameterCode ()
		{
			var xml = Api (
					Interface ("MyInterface", StringParameterMethod ("setName", parameterNotNull: false, isAbstract: true)) +
					Class ("MyClass", Implements ("MyInterface") + StringParameterMethod ("setName", parameterNotNull: true, extra: "explicitInterface='com.xamarin.android.MyInterface'")));

			AssertSuppresses (Generate (xml, "MyClass"), "8769");
		}

		#endregion

		#region Generic interface instantiations

		// An interface with a single type parameter, whose method takes a `T` the Java API
		// annotates as non-null.
		static string GenericInterface (string name) =>
			$@"<interface abstract='true' deprecated='not deprecated' extends='java.lang.Object' extends-generic-aware='java.lang.Object' jni-extends='Ljava/lang/Object;' final='false' name='{name}' static='false' visibility='public' jni-signature='Lcom/xamarin/android/{name};'>
			      <typeParameters><typeParameter name='T' /></typeParameters>
			      <method abstract='true' deprecated='not deprecated' final='false' name='handle' jni-signature='(Ljava/lang/Object;)V' bridge='false' native='false' return='void' jni-return='V' static='false' synchronized='false' synthetic='false' visibility='public'>
			        <parameter name='value' type='T' jni-type='Ljava/lang/Object;' not-null='true' />
			      </method>
			    </interface>";

		static string GenericImplementation (string name, string typeArgument, string jniTypeArgument) =>
			Class (name,
					$@"<implements name='com.xamarin.android.MyGeneric' name-generic-aware='com.xamarin.android.MyGeneric&lt;{typeArgument}&gt;' jni-type='Lcom/xamarin/android/MyGeneric;' />
			      <method abstract='false' deprecated='not deprecated' final='false' name='handle' jni-signature='({jniTypeArgument})V' bridge='false' native='false' return='void' jni-return='V' static='false' synchronized='false' synthetic='false' visibility='public'>
			        <parameter name='value' type='{typeArgument}' jni-type='{jniTypeArgument}' not-null='true' />
			      </method>");

		[Test]
		public void GenericStringInstantiationMarshalIsSuppressed ()
		{
			// The `string` instantiation marshals through `value?.ToString ()`, which the
			// compiler considers nullable, so passing it to a parameter annotated as non-null
			// is CS8604.
			var xml = Api (GenericInterface ("MyGeneric") + GenericImplementation ("MyStringClass", "java.lang.String", "Ljava/lang/String;"));

			var generated = Generate (xml, "MyStringClass");

			StringAssert.Contains ("?.ToString ()", generated);
			AssertSuppresses (generated, "8604");
		}

		[Test]
		public void GenericObjectInstantiationMarshalIsNotSuppressed ()
		{
			// Every other instantiation marshals through `JavaCast`, which is emitted with a
			// null-forgiving operator, so there is nothing to suppress.
			var xml = Api (GenericInterface ("MyGeneric") + GenericImplementation ("MyObjectClass", "java.lang.Object", "Ljava/lang/Object;"));

			var generated = Generate (xml, "MyObjectClass");

			AssertSuppresses (generated);
		}

		#endregion

		#region Clean code stays clean

		[Test]
		public void AMemberWithNothingToSuppressEmitsNoPragma ()
		{
			var xml = Api (Class ("MyClass", StringMethod ("getName", returnNotNull: true) + StringParameterMethod ("setOther", parameterNotNull: false)));

			AssertSuppresses (Generate (xml, "MyClass"));
		}

		#endregion

		#region Generated invokers and implementors

		[Test]
		public void InterfaceInvokerInheritsTheInterfaceMismatch ()
		{
			// The invoker generated for an interface implements that interface, so the
			// compiler compares its members against the interface's declarations too.
			var xml = Api (
					Interface ("MyInterface", StringMethod ("getName", returnNotNull: true, isAbstract: true)) +
					Interface ("MyDerived", Implements ("MyInterface") + StringMethod ("getName", returnNotNull: false, isAbstract: true)));

			AssertSuppresses (Generate (xml, "IMyDerived"), "8766");
		}

		[Test]
		public void DeprecatedInterfaceEmitsObsoleteHelpersRatherThanTypeWideSuppression ()
		{
			// The invoker for a deprecated interface carries that deprecation itself, so no
			// type-wide CS0618 scope is needed to cover the members that name it.
			var xml = Api (Interface ("MyInterface", StringMethod ("getName", returnNotNull: true, isAbstract: true), deprecated: "This is deprecated."));

			var generated = Generate (xml, "IMyInterface");

			AssertSuppresses (generated);
			Assert.GreaterOrEqual (CountOf (generated, "[global::System.Obsolete"), 2, $"was:\n{generated}");
		}

		#endregion

		static int CountOf (string haystack, string needle)
		{
			var count = 0;

			for (var i = haystack.IndexOf (needle, StringComparison.Ordinal); i >= 0; i = haystack.IndexOf (needle, i + needle.Length, StringComparison.Ordinal))
				count++;

			return count;
		}
	}
}
