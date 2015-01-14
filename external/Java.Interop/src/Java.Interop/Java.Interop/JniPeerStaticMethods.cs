using System;
using System.Collections.Generic;

namespace Java.Interop
{
	public sealed partial class JniPeerStaticMethods {

		internal JniPeerStaticMethods (JniPeerMembers members)
		{
			Members = members;
		}

		readonly JniPeerMembers                             Members;
		readonly Dictionary<string, JniStaticMethodID>      StaticMethods   = new Dictionary<string, JniStaticMethodID>();

		public JniStaticMethodID GetMethodID (string encodedMember)
		{
			lock (StaticMethods) {
				JniStaticMethodID m;
				if (!StaticMethods.TryGetValue (encodedMember, out m)) {
					string method, signature;
					JniPeerMembers.GetNameAndSignature (encodedMember, out method, out signature);
					m = Members.JniPeerType.GetStaticMethod (method, signature);
					StaticMethods.Add (encodedMember, m);
				}
				return m;
			}
		}

		public object CallMethod (string encodedMember, JValue[] arguments)
		{
			var n   = JniPeerMembers.GetSignatureSeparatorIndex (encodedMember);
			var e   = encodedMember.LastIndexOf (')');
			if (e == -1)
				throw new ArgumentException (
						string.Format ("Invalid method JNI signature in '{0}'; no ')' found!", encodedMember.Substring (n + 1)),
						"encodedMember");
			if (e == encodedMember.Length)
				throw new ArgumentException (
					string.Format ("Invalid method JNI signature in '{0}'; no return type found found!", encodedMember.Substring (n + 1)),
					"encodedMember");
			switch (encodedMember [e + 1]) {
			case 'Z':   return CallBooleanMethod (encodedMember, arguments);
			case 'B':   return CallSByteMethod (encodedMember, arguments);
			case 'C':   return CallCharMethod (encodedMember, arguments);
			case 'S':   return CallInt16Method (encodedMember, arguments);
			case 'I':   return CallInt32Method (encodedMember, arguments);
			case 'J':   return CallInt64Method (encodedMember, arguments);
			case 'F':   return CallSingleMethod (encodedMember, arguments);
			case 'D':   return CallDoubleMethod (encodedMember, arguments);
			case 'L':
			case '[':
				var lref = CallObjectMethod (encodedMember, arguments);
				return JniEnvironment.Current.JavaVM.GetObject (lref, JniHandleOwnership.Transfer);
			default:
				throw new NotSupportedException ("Unsupported argument type: " + encodedMember.Substring (n + 1));
			}
		}
	}
}

