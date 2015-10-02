using System;
using System.Collections.Generic;

namespace Java.Interop
{
	public sealed partial class JniPeerStaticFields
	{
		internal JniPeerStaticFields (JniPeerMembers members)
		{
			Members = members;
		}

		readonly JniPeerMembers                             Members;

		Dictionary<string, JniStaticFieldID>                StaticFields  = new Dictionary<string, JniStaticFieldID>();

		public JniStaticFieldID GetFieldID (string encodedMember)
		{
			lock (StaticFields) {
				JniStaticFieldID f;
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

			foreach (var f in StaticFields.Values)
				f.Dispose ();
			StaticFields = null;
		}
	}
}

