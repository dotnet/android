using System;
using System.Collections.Generic;
using MonoDroid.Generation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class InterfaceTests
	{
		[TestMethodAttribute]
		public void ValidateInterfaceMethods ()
		{
			var options = new CodeGenerationOptions { SupportDefaultInterfaceMethods = true };
			var iface = SupportTypeBuilder.CreateEmptyInterface ("My.Test.Interface");

			iface.Methods.Add (SupportTypeBuilder.CreateMethod (iface, "DoAbstractThing", options));
			iface.Methods.Add (SupportTypeBuilder.CreateMethod (iface, "DoDefaultThing", options).SetDefaultInterfaceMethod ());

			// The interface should be valid
			Assert.IsTrue (iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()));

			Assert.AreEqual (2, iface.Methods.Count);
		}

		[TestMethodAttribute]
		public void ValidateInvalidDefaultInterfaceMethods ()
		{
			var options = new CodeGenerationOptions { SupportDefaultInterfaceMethods = true };
			var iface = SupportTypeBuilder.CreateEmptyInterface ("My.Test.Interface");

			iface.Methods.Add (SupportTypeBuilder.CreateMethod (iface, "DoAbstractThing", options));
			iface.Methods.Add (SupportTypeBuilder.CreateMethod (iface, "DoDefaultThing", options, "potato").SetDefaultInterfaceMethod ());

			// The interface should still be valid despite the default method being invalid
			Assert.IsTrue (iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()));

			// The invalid default method should be removed, leaving just the valid abstract method
			Assert.AreEqual (1, iface.Methods.Count);
		}

		[TestMethodAttribute]
		public void ValidateInvalidAbstractInterfaceMethods ()
		{
			var options = new CodeGenerationOptions { SupportDefaultInterfaceMethods = true };
			var iface = SupportTypeBuilder.CreateEmptyInterface ("My.Test.Interface");

			iface.Methods.Add (SupportTypeBuilder.CreateMethod (iface, "DoAbstractThing", options, "potato"));
			iface.Methods.Add (SupportTypeBuilder.CreateMethod (iface, "DoDefaultThing", options).SetDefaultInterfaceMethod ());

			// The interface should be invalid because an abstract method is invalid
			Assert.IsFalse (iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()));

			// The invalid abstract method should be removed, leaving just the valid default method
			Assert.AreEqual (1, iface.Methods.Count);
		}
	}
}
