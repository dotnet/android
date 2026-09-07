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

		ConcurrentDictionary<string, JniFieldInfo>? StaticFields;

		public JniFieldInfo GetFieldInfo (string encodedMember)
		{
			return GetOrCreate (ref StaticFields, 3).GetOrAdd (encodedMember, static (member, fields) => {
				string field, signature;
				JniPeerMembers.GetNameAndSignature (member, out field, out signature);
				return fields.Members.JniPeerType.GetStaticField (field, signature);
			}, this);
		}

		internal void Dispose ()
		{
			Clear (ref StaticFields);
		}
	}}
}
