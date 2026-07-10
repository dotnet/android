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
		protected override ICollection<bool> CreateCollection (IEnumerable<bool> values)
		{
			return new JavaBooleanArray (values);
		}

		protected override ICollection<bool> CreateCollection (IList<bool> values)
		{
			return new JavaBooleanArray (values);
		}

		protected override ICollection<bool> CreateCollection (int length)
		{
			return new JavaBooleanArray (length);
		}

		protected override bool CreateValueA ()
		{
			return true;
		}

		protected override bool CreateValueB ()
		{
			return false;
		}
	}
}

