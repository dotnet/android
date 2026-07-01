using System;
using System.Collections.Generic;
using MonoDroid.Generation;
using NUnit.Framework;

namespace generatortests
{
	public class InterfaceTests
	{
		[Test]
		public void ValidateInterfaceMethods ()
		{
			var options = new CodeGenerationOptions { SupportDefaultInterfaceMethods = true };
			var iface = SupportTypeBuilder.CreateEmptyInterface ("My.Test.Interface");

			iface.Methods.Add (SupportTypeBuilder.CreateMethod (iface, "DoAbstractThing", options));
			iface.Methods.Add (SupportTypeBuilder.CreateMethod (iface, "DoDefaultThing", options).SetDefaultInterfaceMethod ());

			// The interface should be valid
			Assert.True (iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()));

			Assert.AreEqual (2, iface.Methods.Count);
		}

		[Test]
		public void ValidateInvalidDefaultInterfaceMethods ()
		{
			var options = new CodeGenerationOptions { SupportDefaultInterfaceMethods = true };
			var iface = SupportTypeBuilder.CreateEmptyInterface ("My.Test.Interface");

			iface.Methods.Add (SupportTypeBuilder.CreateMethod (iface, "DoAbstractThing", options));
			iface.Methods.Add (SupportTypeBuilder.CreateMethod (iface, "DoDefaultThing", options, "potato").SetDefaultInterfaceMethod ());

			// The interface should still be valid despite the default method being invalid
			Assert.True (iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()));

			// The invalid default method should be removed, leaving just the valid abstract method
			Assert.AreEqual (1, iface.Methods.Count);
		}

		[Test]
		public void ValidateInvalidAbstractInterfaceMethods ()
		{
			var options = new CodeGenerationOptions { SupportDefaultInterfaceMethods = true };
			var iface = SupportTypeBuilder.CreateEmptyInterface ("My.Test.Interface");

			iface.Methods.Add (SupportTypeBuilder.CreateMethod (iface, "DoAbstractThing", options, "potato"));
			iface.Methods.Add (SupportTypeBuilder.CreateMethod (iface, "DoDefaultThing", options).SetDefaultInterfaceMethod ());

			// The interface should be invalid because an abstract method is invalid
			Assert.False (iface.Validate (options, new GenericParameterDefinitionList (), new CodeGeneratorContext ()));

			// The invalid abstract method should be removed, leaving just the valid default method
			Assert.AreEqual (1, iface.Methods.Count);
		}
	}
}
