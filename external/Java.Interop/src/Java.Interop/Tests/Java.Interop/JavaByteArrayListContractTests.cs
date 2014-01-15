using System;
using System.Collections.Generic;
using System.Linq;

using Java.Interop;

using Cadenza.Collections.Tests;
using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JavaByteArrayListContractTests : ListContract<sbyte>
	{
		static JavaByteArrayListContractTests ()
		{
			#pragma warning disable 0219
			var ignore = JVM.Current;
			#pragma warning restore 0219
		}

		#region implemented abstract members of CollectionContract
		protected override ICollection<sbyte> CreateCollection (IEnumerable<sbyte> values)
		{
			var array = new JavaByteArray (values.Count ());
			var e = values.GetEnumerator ();
			for (int i = 0; e.MoveNext (); ++i)
				array [i] = e.Current;
			return array;
		}
		protected override sbyte CreateValueA ()
		{
			return (sbyte) 'A';
		}
		protected override sbyte CreateValueB ()
		{
			return (sbyte) 'B';
		}
		protected override sbyte CreateValueC ()
		{
			return (sbyte) 'C';
		}
		#endregion
	}
}

