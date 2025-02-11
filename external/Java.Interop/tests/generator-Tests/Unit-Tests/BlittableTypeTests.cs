using System;
using MonoDroid.Generation;
using NUnit.Framework;
using Xamarin.Android.Binder;

namespace generatortests;

[TestFixture]
class BlittableTypeTests : CodeGeneratorTestBase
{
	protected override CodeGenerationTarget Target => CodeGenerationTarget.XAJavaInterop1;

	[Test]
	public void MethodWithBoolReturnType ()
	{
		var klass = new TestClass ("Object", "java.code.MyClass");
		var method = SupportTypeBuilder.CreateMethod (klass, "IsEmpty", options, "boolean");

		klass.Methods.Add (method);

		var actual = GetGeneratedTypeOutput (klass);

		// Return type should be byte
		Assert.That (actual, Contains.Substring ("static sbyte n_IsEmpty"));

		// Return statement should convert to 0 or 1
		Assert.That (actual, Contains.Substring ("return __this.IsEmpty () ? (sbyte)1 : (sbyte)0"));

		// Ensure the marshal delegate is byte
		Assert.That (actual, Contains.Substring ("new _JniMarshal_PP_B"));
		Assert.That (actual, Does.Not.Contains ("new _JniMarshal_PP_Z"));
	}

	[Test]
	public void MethodWithBoolParameter ()
	{
		var klass = new TestClass ("Object", "java.code.MyClass");
		var method = SupportTypeBuilder.CreateMethod (klass, "SetEmpty", options, "void", parameters: new Parameter ("value", "boolean", "bool", false));

		klass.Methods.Add (method);

		var actual = GetGeneratedTypeOutput (klass);

		// Method parameter should be byte
		Assert.That (actual, Contains.Substring ("static void n_SetEmpty_Z (IntPtr jnienv, IntPtr native__this, sbyte native_value)"));

		// Method should convert from 0 or 1
		Assert.That (actual, Contains.Substring ("var value = native_value != 0;"));

		// Ensure the marshal delegate is byte
		Assert.That (actual, Contains.Substring ("new _JniMarshal_PPB_V"));
		Assert.That (actual, Does.Not.Contains ("new _JniMarshal_PPZ_V"));
	}

	[Test]
	public void BoolProperty ()
	{
		var klass = SupportTypeBuilder.CreateClassWithProperty ("MyClass", "com.example.myClass", "IsEmpty", "boolean", options);
		var actual = GetGeneratedTypeOutput (klass);

		// Getter return type should be byte
		Assert.That (actual, Contains.Substring ("static sbyte n_get_IsEmpty"));

		// Getter return statement should convert to 0 or 1
		Assert.That (actual, Contains.Substring ("return __this.IsEmpty ? (sbyte)1 : (sbyte)0"));

		// Setter parameter should be byte
		Assert.That (actual, Contains.Substring ("static void n_set_IsEmpty_Z (IntPtr jnienv, IntPtr native__this, sbyte native_value)"));

		// Setter should convert from 0 or 1
		Assert.That (actual, Contains.Substring ("var value = native_value != 0;"));

		// Ensure the marshal delegate is byte
		Assert.That (actual, Contains.Substring ("new _JniMarshal_PP_B"));
		Assert.That (actual, Does.Not.Contains ("new _JniMarshal_PP_Z"));
	}

	[Test]
	public void MethodWithCharReturnType ()
	{
		var klass = new TestClass ("Object", "java.code.MyClass");
		var method = SupportTypeBuilder.CreateMethod (klass, "GetFirstLetter", options, "char");

		klass.Methods.Add (method);

		var actual = GetGeneratedTypeOutput (klass);

		// Return type should be ushort
		Assert.That (actual, Contains.Substring ("static ushort n_GetFirstLetter"));

		// Return statement should convert to ushort
		Assert.That (actual, Contains.Substring ("return (ushort)__this.GetFirstLetter ()"));

		// Ensure the marshal delegate is ushort
		Assert.That (actual, Contains.Substring ("new _JniMarshal_PP_s"));
		Assert.That (actual, Does.Not.Contains ("new _JniMarshal_PP_C"));
	}

	[Test]
	public void MethodWithCharParameter ()
	{
		var klass = new TestClass ("Object", "java.code.MyClass");
		var method = SupportTypeBuilder.CreateMethod (klass, "SetFirstLetter", options, "void", parameters: new Parameter ("value", "char", "char", false));

		klass.Methods.Add (method);

		var actual = GetGeneratedTypeOutput (klass);

		// Method parameter should be ushort
		Assert.That (actual, Contains.Substring ("static void n_SetFirstLetter_C (IntPtr jnienv, IntPtr native__this, ushort native_value)"));

		// Method should convert from ushort to char
		Assert.That (actual, Contains.Substring ("var value = (char)native_value;"));

		// Ensure the marshal delegate is ushort
		Assert.That (actual, Contains.Substring ("new _JniMarshal_PPs_V"));
		Assert.That (actual, Does.Not.Contains ("new _JniMarshal_PPC_V"));
	}

	[Test]
	public void CharProperty ()
	{
		var klass = SupportTypeBuilder.CreateClassWithProperty ("MyClass", "com.example.myClass", "FirstLetter", "char", options);
		var actual = GetGeneratedTypeOutput (klass);

		// Getter return type should be ushort
		Assert.That (actual, Contains.Substring ("static ushort n_get_FirstLetter"));

		// Getter return statement should convert to ushort
		Assert.That (actual, Contains.Substring ("return (ushort)__this.FirstLetter"));

		// Setter parameter should be ushort
		Assert.That (actual, Contains.Substring ("static void n_set_FirstLetter_C (IntPtr jnienv, IntPtr native__this, ushort native_value)"));

		// Setter should convert from ushort to char
		Assert.That (actual, Contains.Substring ("var value = (char)native_value;"));

		// Ensure the marshal delegate is ushort
		Assert.That (actual, Contains.Substring ("new _JniMarshal_PP_s"));
		Assert.That (actual, Does.Not.Contains ("new _JniMarshal_PP_C"));
	}
}
