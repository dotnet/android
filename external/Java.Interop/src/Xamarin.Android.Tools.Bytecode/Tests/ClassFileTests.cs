using System;
using System.Reflection;

using Xamarin.Android.Tools.Bytecode;

using NUnit.Framework;

namespace Xamarin.Android.Tools.BytecodeTests {

	[TestFixture]
	public class ClassFileTests {

		[Test, ExpectedException (typeof (ArgumentNullException))]
		public void Constructor_Exceptions ()
		{
			new ClassFile (null);
		}
	}
}

