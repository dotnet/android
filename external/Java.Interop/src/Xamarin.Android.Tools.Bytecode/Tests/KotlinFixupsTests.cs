using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Xamarin.Android.Tools.Bytecode;

namespace Xamarin.Android.Tools.BytecodeTests
{
	[TestFixture]
	public class KotlinFixupsTests : ClassFileFixture
	{
		[Test]
		public void HideInternalClass ()
		{
			var klass = LoadClassFile ("InternalClass.class");

			Assert.True (klass.AccessFlags.HasFlag (ClassAccessFlags.Public));

			KotlinFixups.Fixup (new [] { klass });

			Assert.False (klass.AccessFlags.HasFlag (ClassAccessFlags.Public));
		}

		[Test]
		public void HideInternalConstructor ()
		{
			var klass = LoadClassFile ("InternalConstructor.class");
			var ctor = klass.Methods.First (m => m.Name == "<init>");

			Assert.True (ctor.AccessFlags.HasFlag (MethodAccessFlags.Public));

			KotlinFixups.Fixup (new [] { klass });

			Assert.False (ctor.AccessFlags.HasFlag (MethodAccessFlags.Public));
		}

		[Test]
		public void HideImplementationMethod ()
		{
			var klass = LoadClassFile ("MethodImplementation.class");
			var method = klass.Methods.First (m => m.Name == "toString-impl");

			Assert.True (method.AccessFlags.HasFlag (MethodAccessFlags.Public));

			KotlinFixups.Fixup (new [] { klass });

			Assert.False (method.AccessFlags.HasFlag (MethodAccessFlags.Public));
		}

		[Test]
		public void RenameExtensionParameter ()
		{
			var klass = LoadClassFile ("RenameExtensionParameterKt.class");
			var method = klass.Methods.First (m => m.Name == "toUtf8String");
			var p = method.GetParameters () [0];

			Assert.AreEqual ("$this$toUtf8String", p.Name);

			KotlinFixups.Fixup (new [] { klass });

			Assert.AreEqual ("obj", p.Name);
		}

		[Test]
		public void HideInternalMethod ()
		{
			var klass = LoadClassFile ("InternalMethod.class");
			var method = klass.Methods.First (m => m.Name == "take$main");

			Assert.True (method.AccessFlags.HasFlag (MethodAccessFlags.Public));

			KotlinFixups.Fixup (new [] { klass });

			Assert.False (method.AccessFlags.HasFlag (MethodAccessFlags.Public));
		}

		[Test]
		public void ParameterName ()
		{
			var klass = LoadClassFile ("ParameterName.class");
			var method = klass.Methods.First (m => m.Name == "take");
			var p = method.GetParameters () [0];

			Assert.AreEqual ("p0", p.Name);

			KotlinFixups.Fixup (new [] { klass });

			Assert.AreEqual ("count", p.Name);
		}

		[Test]
		public void HideInternalProperty ()
		{
			var klass = LoadClassFile ("InternalProperty.class");
			var getter = klass.Methods.First (m => m.Name == "getCity$main");
			var setter = klass.Methods.First (m => m.Name == "setCity$main");

			Assert.True (getter.AccessFlags.HasFlag (MethodAccessFlags.Public));
			Assert.True (setter.AccessFlags.HasFlag (MethodAccessFlags.Public));

			KotlinFixups.Fixup (new [] { klass });

			Assert.False (getter.AccessFlags.HasFlag (MethodAccessFlags.Public));
			Assert.False (setter.AccessFlags.HasFlag (MethodAccessFlags.Public));
		}

		[Test]
		public void RenameSetterParameter ()
		{
			var klass = LoadClassFile ("SetterParameterName.class");
			var setter = klass.Methods.First (m => m.Name == "setCity");
			var p = setter.GetParameters () [0];

			Assert.AreEqual ("p0", p.Name);

			KotlinFixups.Fixup (new [] { klass });

			Assert.AreEqual ("value", p.Name);
		}
	}
}
