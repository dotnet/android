using System;
using System.Collections.Generic;

namespace Java.Interop
{
	partial class JniPeerMembers {
	public sealed partial class JniInstanceFields
	{
		internal JniInstanceFields (JniPeerMembers members)
		{
			Members = members;
		}

		readonly JniPeerMembers                             Members;

		Dictionary<string, JniInstanceFieldInfo>            InstanceFields  = new Dictionary<string, JniInstanceFieldInfo>();

		internal void Dispose ()
		{
			if (InstanceFields == null)
				return;

			InstanceFields  = null;
		}

		public JniInstanceFieldInfo GetFieldInfo (string encodedMember)
		{
			lock (InstanceFields) {
				JniInstanceFieldInfo f;
				if (!InstanceFields.TryGetValue (encodedMember, out f)) {
					string field, signature;
					JniPeerMembers.GetNameAndSignature (encodedMember, out field, out signature);
					f = Members.JniPeerType.GetInstanceField (field, signature);
					InstanceFields.Add (encodedMember, f);
				}
				return f;
			}
		}
	}}
}

