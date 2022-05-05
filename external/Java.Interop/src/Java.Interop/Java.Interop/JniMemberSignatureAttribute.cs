#nullable enable

using System;

#if NET

namespace Java.Interop
{
	public abstract class JniMemberSignatureAttribute : Attribute {

		internal JniMemberSignatureAttribute (string memberName, string memberSignature)
		{
			if (string.IsNullOrEmpty (memberName))
				throw new ArgumentNullException (nameof (memberName));
			if (string.IsNullOrEmpty (memberSignature))
				throw new ArgumentNullException (nameof (memberSignature));

			MemberName          = memberName;
			MemberSignature	    = memberSignature;
		}

		public      string      MemberName          {get;}
		public      string      MemberSignature     {get;}
	}
}

#endif  // NET
