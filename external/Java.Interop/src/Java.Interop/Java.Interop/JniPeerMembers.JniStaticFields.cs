#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;

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
			return GetStaticFields ().GetOrAdd (encodedMember, static (member, fields) => {
				string field, signature;
				JniPeerMembers.GetNameAndSignature (member, out field, out signature);
				return fields.Members.JniPeerType.GetStaticField (field, signature);
			}, this);
		}

		internal void Dispose ()
		{
			Interlocked.Exchange (ref StaticFields, null)?.Clear ();
		}

		ConcurrentDictionary<string, JniFieldInfo> GetStaticFields ()
		{
			var fields = Volatile.Read (ref StaticFields);
			if (fields != null)
				return fields;

			var candidate = new ConcurrentDictionary<string, JniFieldInfo> (1, 3, StringComparer.Ordinal);
			return Interlocked.CompareExchange (ref StaticFields, candidate, null) ?? candidate;
		}
	}}
}
