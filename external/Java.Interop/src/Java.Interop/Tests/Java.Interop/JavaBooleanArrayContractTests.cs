using System;
using System.Collections.Generic;
using System.Linq;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JavaBooleanArrayContractTests : JavaPrimitiveArrayContract<JavaBooleanArray, bool>
	{
		protected override bool CreateValueA ()
		{
			return true;
		}

		protected override bool CreateValueB ()
		{
			return false;
		}

		// This is EBIL
		// CollectionContract<T>.Contains() requires that you have three distinct values.
		// This is a problem when `bool` only has two distinct values.
		// (The test should be fixed?)
		// "Create" a new distinct `bool` value. (Yay unsafe code!)
		protected override unsafe bool CreateValueC ()
		{
			bool value      = false;
			void* p         = &value;
			(*(byte*) p)    = 2;
			return value;
		}
	}
}

