#nullable enable

using System;
using System.Collections.Concurrent;

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

		ConcurrentDictionary<string, JniFieldInfo>? instanceFields;

		ConcurrentDictionary<string, JniFieldInfo> InstanceFields => GetOrCreate (ref instanceFields, 3);

		internal void Dispose ()
		{
			Clear (ref instanceFields);
		}

		public JniFieldInfo GetFieldInfo (string encodedMember)
		{
			return InstanceFields.GetOrAdd (encodedMember, static (member, fields) => {
				string field, signature;
				JniPeerMembers.GetNameAndSignature (member, out field, out signature);
				return fields.Members.JniPeerType.GetInstanceField (field, signature);
			}, this);
		}
	}}
}
