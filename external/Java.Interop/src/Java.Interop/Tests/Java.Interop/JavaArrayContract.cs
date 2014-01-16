using System;

using Java.Interop;

using Cadenza.Collections.Tests;
using NUnit.Framework;

namespace Java.InteropTests
{
	public abstract class JavaArrayContract<T> : ListContract<T>
	{
		protected abstract T FromInt32 (int value);

		protected override sealed T CreateValueA ()
		{
			return FromInt32 ((int) 'A');
		}

		protected override sealed T CreateValueB ()
		{
			return FromInt32 ((int) 'B');
		}

		protected override sealed T CreateValueC ()
		{
			return FromInt32 ((int) 'C');
		}
	}
}

