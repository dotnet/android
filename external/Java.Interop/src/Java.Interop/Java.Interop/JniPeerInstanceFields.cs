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

		Dictionary<string, JniInstanceFieldID>              InstanceFields  = new Dictionary<string, JniInstanceFieldID>();

		internal void Dispose ()
		{
			if (InstanceFields == null)
				return;

			foreach (var f in InstanceFields.Values)
				f.Dispose ();
			InstanceFields  = null;
		}

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
	}
}

