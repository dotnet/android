using System;
using MonoDroid.Generation;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	class JavaInteropInterfaceConstantsTests : InterfaceConstantsTests
	{
		protected override Xamarin.Android.Binder.CodeGenerationTarget Target => Xamarin.Android.Binder.CodeGenerationTarget.JavaInterop1;
	}

	[TestFixture]
	class XamarinAndroidInterfaceConstantsTests : InterfaceConstantsTests
	{
		protected override Xamarin.Android.Binder.CodeGenerationTarget Target => Xamarin.Android.Binder.CodeGenerationTarget.XamarinAndroid;
	}

	abstract class InterfaceConstantsTests : CodeGeneratorTestBase
	{
		protected override CodeGenerationOptions CreateOptions ()
		{
			var options = base.CreateOptions ();

			options.SupportInterfaceConstants = true;

			return options;
		}

		[Test]
		public void WriteInterfaceFields ()
		{
			// This is an interface that has both fields and method declarations
			var iface = SupportTypeBuilder.CreateEmptyInterface("java.code.IMyInterface");

			iface.Fields.Add (new TestField ("int", "MyConstantField").SetConstant ().SetValue ("7"));
			iface.Methods.Add (new TestMethod (iface, "DoSomething").SetAbstract ());

			iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ());

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceDeclaration (iface, string.Empty);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteInterfaceFields)), writer.ToString ().NormalizeLineEndings ());
		}

		[Test]
		public void WriteConstSugarInterfaceFields ()
		{
			// This is an interface that only has fields (IsConstSugar)
			// We treat   a little differenly because they don't need to interop with Java
			var iface = SupportTypeBuilder.CreateEmptyInterface ("java.code.IMyInterface");

			// These interface fields are supported and should be in the output
			iface.Fields.Add (new TestField ("int", "MyConstantField").SetConstant ().SetValue ("7"));
			iface.Fields.Add (new TestField ("java.lang.String", "MyConstantStringField").SetConstant ().SetValue ("\"hello\""));
			iface.Fields.Add (new TestField ("int", "MyDeprecatedField").SetConstant ().SetValue ("7").SetDeprecated ());

			// These interface fields are not supported and should be ignored
			iface.Fields.Add (new TestField ("int", "MyDeprecatedEnumField").SetConstant ().SetValue ("MyEnumValue").SetDeprecated ("This constant will be removed in the future version."));
			iface.Fields.Add (new TestField ("int", "MyStaticField").SetStatic ().SetValue ("7"));

			iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ());

			generator.Context.ContextTypes.Push (iface);
			generator.WriteInterfaceDeclaration (iface, string.Empty);
			generator.Context.ContextTypes.Pop ();

			Assert.AreEqual (GetTargetedExpected (nameof (WriteConstSugarInterfaceFields)), writer.ToString ().NormalizeLineEndings ());
		}
	}
}
