using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Java.Interop
{
	public abstract class TypeMapProxyBaseAttribute : Attribute
	{
		public abstract string JniName { get; }
	}

	public abstract class TypeMapProxyAttribute : TypeMapProxyBaseAttribute
	{
		public TypeMapProxyAttribute (string jniName)
		{
			if (string.IsNullOrEmpty (jniName))
				throw new ArgumentException ("must not be null or empty", nameof (jniName));
			JniName = jniName;
		}

		public override string JniName { get; }
	}
}
