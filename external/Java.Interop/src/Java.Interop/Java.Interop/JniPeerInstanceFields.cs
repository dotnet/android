using System;
using System.Collections.Generic;

namespace Java.Interop
{
	public sealed partial class JniPeerInstanceFields
	{
		internal JniPeerInstanceFields (JniPeerMembers members)
		{
			Members = members;
		}

		readonly JniPeerMembers                             Members;
		readonly Dictionary<string, JniInstanceFieldID>     InstanceFields  = new Dictionary<string, JniInstanceFieldID>();

		public JniInstanceFieldID GetFieldID (string encodedMember)
		{
			lock (InstanceFields) {
				JniInstanceFieldID f;
				if (!InstanceFields.TryGetValue (encodedMember, out f)) {
					string field, signature;
					JniPeerMembers.GetNameAndSignature (encodedMember, out field, out signature);
					f = Members.JniPeerType.GetInstanceField (field, signature);
					InstanceFields.Add (encodedMember, f);
				}
				return f;
			}
		}

		public bool GetBooleanValue (IJavaObject self, string encodedMember)
		{
			JniPeerMembers.AssertSelf (self);
			return GetFieldID (encodedMember).GetBooleanValue (self.SafeHandle);
		}

		public void SetValue (IJavaObject self, string encodedMember, bool value)
		{
			JniPeerMembers.AssertSelf (self);
			GetFieldID (encodedMember).SetValue (self.SafeHandle, value);
		}
	}
}

