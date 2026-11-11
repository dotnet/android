using System;
using System.Collections.Generic;
using System.Linq;

using Java.Interop;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests
{
	[TestClass]
	public class JavaDoubleArrayContractTests : JavaPrimitiveArrayContract<JavaDoubleArray, double>
	{
		protected override ICollection<double> CreateCollection (IEnumerable<double> values)
		{
			return new JavaDoubleArray (values);
		}

		protected override ICollection<double> CreateCollection (IList<double> values)
		{
			return new JavaDoubleArray (values);
		}

		protected override ICollection<double> CreateCollection (int length)
		{
			return new JavaDoubleArray (length);
		}
	}
}

