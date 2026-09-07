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

		ConcurrentDictionary<string, JniFieldInfo>? staticFields;

		ConcurrentDictionary<string, JniFieldInfo> StaticFields => GetOrCreate (ref staticFields, 3);

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
			Clear (ref staticFields);
		}
	}}
}
