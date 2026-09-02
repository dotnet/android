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

		readonly ConcurrentDictionary<string, JniFieldInfo> InstanceFields = new ConcurrentDictionary<string, JniFieldInfo> (1, 3, StringComparer.Ordinal);
		readonly Utf8ValueCache<JniFieldInfo>                Utf8InstanceFields = new Utf8ValueCache<JniFieldInfo> ();

		internal void Dispose ()
		{
			InstanceFields.Clear ();
			Utf8InstanceFields.Clear ();
		}

		public JniFieldInfo GetFieldInfo (string encodedMember)
		{
			return InstanceFields.GetOrAdd (encodedMember, static (member, fields) => {
				string field, signature;
				JniPeerMembers.GetNameAndSignature (member, out field, out signature);
				return fields.Members.JniPeerType.GetInstanceField (field, signature);
			}, this);
		}

		public JniFieldInfo GetFieldInfo (ReadOnlySpan<byte> encodedMember)
		{
			return Utf8InstanceFields.GetOrAdd (encodedMember, static (member, fields) => {
				int separator = JniPeerMembers.GetSignatureSeparatorIndex (member);
				var field     = JniPeerMembers.GetNullTerminatedUtf8 (member.Slice (0, separator));
				var signature = JniPeerMembers.GetNullTerminatedUtf8 (member.Slice (separator + 1));
				return fields.Members.JniPeerType.GetInstanceField (field, signature);
			}, this);
		}
	}}
}
