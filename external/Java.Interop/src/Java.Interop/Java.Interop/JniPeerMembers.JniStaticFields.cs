using System;
using System.Collections.Generic;

namespace Java.Interop
{
	partial class JniPeerMembers {
	public sealed partial class JniStaticFields
	{
		internal JniStaticFields (JniPeerMembers members)
		{
			Members = members;
		}

		readonly JniPeerMembers                             Members;

		Dictionary<string, JniStaticFieldInfo>              StaticFields  = new Dictionary<string, JniStaticFieldInfo>();

		public JniStaticFieldInfo GetFieldInfo (string encodedMember)
		{
			lock (StaticFields) {
				JniStaticFieldInfo f;
				if (!StaticFields.TryGetValue (encodedMember, out f)) {
					string field, signature;
					JniPeerMembers.GetNameAndSignature (encodedMember, out field, out signature);
					f = Members.JniPeerType.GetStaticField (field, signature);
					StaticFields.Add (encodedMember, f);
				}
				return f;
			}
		}

		internal void Dispose ()
		{
			if (StaticFields == null)
				return;

			StaticFields = null;
		}
	}}
}

