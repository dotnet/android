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

		Dictionary<string, JniStaticMethodID>               StaticMethods   = new Dictionary<string, JniStaticMethodID>();

		internal void Dispose ()
		{
			if (StaticMethods == null)
				return;

			foreach (var m in StaticMethods.Values)
				m.Dispose ();
			StaticMethods   = null;
		}

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
	}
}

