#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;


namespace Java.Interop
{
	[AttributeUsage (AttributeTargets.Method, AllowMultiple = false)]
	public sealed class JniMethodSignatureAttribute : JniMemberSignatureAttribute {

		public JniMethodSignatureAttribute (string memberName, string memberSignature)
			: base (memberName, memberSignature)
		{
		}
	}
}
