#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;

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

		ConcurrentDictionary<string, JniFieldInfo>? InstanceFields;

		internal void Dispose ()
		{
			Interlocked.Exchange (ref InstanceFields, null)?.Clear ();
		}

		public JniFieldInfo GetFieldInfo (string encodedMember)
		{
			return GetOrCreate (ref InstanceFields, 3).GetOrAdd (encodedMember, static (member, fields) => {
				string field, signature;
				JniPeerMembers.GetNameAndSignature (member, out field, out signature);
				return fields.Members.JniPeerType.GetInstanceField (field, signature);
			}, this);
		}
	}}
}
