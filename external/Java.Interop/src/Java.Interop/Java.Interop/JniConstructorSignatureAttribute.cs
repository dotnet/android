#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;

#if NET

namespace Java.Interop
{
	[AttributeUsage (AttributeTargets.Constructor, AllowMultiple = false)]
	public sealed class JniConstructorSignatureAttribute : JniMemberSignatureAttribute {

		public JniConstructorSignatureAttribute (string memberSignature)
			: base (".ctor", memberSignature)
		{
		}
	}
}

#endif  // NET
