using System;
using MonoDroid.Generation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests;

[TestClassAttribute]
class BlittableTypeTests : CodeGeneratorTestBase
{
	[TestMethodAttribute]
	public void MethodWithBoolReturnType ()
	{
		var klass = new TestClass ("Object", "java.code.MyClass");
		var method = SupportTypeBuilder.CreateMethod (klass, "IsEmpty", options, "boolean");

		klass.Methods.Add (method);

		var actual = GetGeneratedTypeOutput (klass);

		// Return type should be byte
		StringAssert.Contains (actual, "static unsafe sbyte n_IsEmpty");

		// Return statement should convert to 0 or 1
		StringAssert.Contains (actual, "return __this.IsEmpty () ? (sbyte)1 : (sbyte)0");

		// Ensure the marshal delegate is byte
		StringAssert.Contains (actual, "new _JniMarshal_PP_B");
		Assert.DoesNotContain ("new _JniMarshal_PP_Z", actual, StringComparison.Ordinal);
	}

	[TestMethodAttribute]
	public void MethodWithBoolParameter ()
	{
		var klass = new TestClass ("Object", "java.code.MyClass");
		var method = SupportTypeBuilder.CreateMethod (klass, "SetEmpty", options, "void", parameters: new Parameter ("value", "boolean", "bool", false));

		klass.Methods.Add (method);

		var actual = GetGeneratedTypeOutput (klass);

		// Method parameter should be byte
		StringAssert.Contains (actual, "static unsafe void n_SetEmpty_Z (IntPtr jnienv, IntPtr native__this, sbyte native_value)");

		// Method should convert from 0 or 1
		StringAssert.Contains (actual, "var value = native_value != 0;");

		// Ensure the marshal delegate is byte
		StringAssert.Contains (actual, "new _JniMarshal_PPB_V");
		Assert.DoesNotContain ("new _JniMarshal_PPZ_V", actual, StringComparison.Ordinal);
	}

	[TestMethodAttribute]
	public void BoolProperty ()
	{
		var klass = SupportTypeBuilder.CreateClassWithProperty ("MyClass", "com.example.myClass", "IsEmpty", "boolean", options);
		var actual = GetGeneratedTypeOutput (klass);

		// Getter return type should be byte
		StringAssert.Contains (actual, "static unsafe sbyte n_get_IsEmpty");

		// Getter return statement should convert to 0 or 1
		StringAssert.Contains (actual, "return __this.IsEmpty ? (sbyte)1 : (sbyte)0");

		// Setter parameter should be byte
		StringAssert.Contains (actual, "static unsafe void n_set_IsEmpty_Z (IntPtr jnienv, IntPtr native__this, sbyte native_value)");

		// Setter should convert from 0 or 1
		StringAssert.Contains (actual, "var value = native_value != 0;");

		// Ensure the marshal delegate is byte
		StringAssert.Contains (actual, "new _JniMarshal_PP_B");
		Assert.DoesNotContain ("new _JniMarshal_PP_Z", actual, StringComparison.Ordinal);
	}

	[TestMethodAttribute]
	public void MethodWithCharReturnType ()
	{
		var klass = new TestClass ("Object", "java.code.MyClass");
		var method = SupportTypeBuilder.CreateMethod (klass, "GetFirstLetter", options, "char");

		klass.Methods.Add (method);

		var actual = GetGeneratedTypeOutput (klass);

		// Return type should be ushort
		StringAssert.Contains (actual, "static unsafe ushort n_GetFirstLetter");

		// Return statement should convert to ushort
		StringAssert.Contains (actual, "return (ushort)__this.GetFirstLetter ()");

		// Ensure the marshal delegate is ushort
		StringAssert.Contains (actual, "new _JniMarshal_PP_s");
		Assert.DoesNotContain ("new _JniMarshal_PP_C", actual, StringComparison.Ordinal);
	}

	[TestMethodAttribute]
	public void MethodWithCharParameter ()
	{
		var klass = new TestClass ("Object", "java.code.MyClass");
		var method = SupportTypeBuilder.CreateMethod (klass, "SetFirstLetter", options, "void", parameters: new Parameter ("value", "char", "char", false));

		klass.Methods.Add (method);

		var actual = GetGeneratedTypeOutput (klass);

		// Method parameter should be ushort
		StringAssert.Contains (actual, "static unsafe void n_SetFirstLetter_C (IntPtr jnienv, IntPtr native__this, ushort native_value)");

		// Method should convert from ushort to char
		StringAssert.Contains (actual, "var value = (char)native_value;");

		// Ensure the marshal delegate is ushort
		StringAssert.Contains (actual, "new _JniMarshal_PPs_V");
		Assert.DoesNotContain ("new _JniMarshal_PPC_V", actual, StringComparison.Ordinal);
	}

	[TestMethodAttribute]
	public void CharProperty ()
	{
		var klass = SupportTypeBuilder.CreateClassWithProperty ("MyClass", "com.example.myClass", "FirstLetter", "char", options);
		var actual = GetGeneratedTypeOutput (klass);

		// Getter return type should be ushort
		StringAssert.Contains (actual, "static unsafe ushort n_get_FirstLetter");

		// Getter return statement should convert to ushort
		StringAssert.Contains (actual, "return (ushort)__this.FirstLetter");

		// Setter parameter should be ushort
		StringAssert.Contains (actual, "static unsafe void n_set_FirstLetter_C (IntPtr jnienv, IntPtr native__this, ushort native_value)");

		// Setter should convert from ushort to char
		StringAssert.Contains (actual, "var value = (char)native_value;");

		// Ensure the marshal delegate is ushort
		StringAssert.Contains (actual, "new _JniMarshal_PP_s");
		Assert.DoesNotContain ("new _JniMarshal_PP_C", actual, StringComparison.Ordinal);
	}
}
