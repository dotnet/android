#nullable enable

using System;
using System.Collections.Concurrent;

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

		readonly ConcurrentDictionary<string, JniFieldInfo> StaticFields = new ConcurrentDictionary<string, JniFieldInfo> (1, 3, StringComparer.Ordinal);

		public JniFieldInfo GetFieldInfo (string encodedMember)
		{
			return StaticFields.GetOrAdd (encodedMember, static (member, fields) => {
				string field, signature;
				JniPeerMembers.GetNameAndSignature (member, out field, out signature);
				return fields.Members.JniPeerType.GetStaticField (field, signature);
			}, this);
		}

		internal void Dispose ()
		{
			StaticFields.Clear ();
		}
	}}
}
