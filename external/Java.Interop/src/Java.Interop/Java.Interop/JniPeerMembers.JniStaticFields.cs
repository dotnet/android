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
		readonly Utf8ValueCache<JniFieldInfo>                Utf8StaticFields = new Utf8ValueCache<JniFieldInfo> ();

		public JniFieldInfo GetFieldInfo (string encodedMember)
		{
			return StaticFields.GetOrAdd (encodedMember, static (member, fields) => {
				string field, signature;
				JniPeerMembers.GetNameAndSignature (member, out field, out signature);
				return fields.Members.JniPeerType.GetStaticField (field, signature);
			}, this);
		}

		public JniFieldInfo GetFieldInfo (ReadOnlySpan<byte> encodedMember)
		{
			return Utf8StaticFields.GetOrAdd (encodedMember, static (member, fields) => {
				int separator = JniPeerMembers.GetSignatureSeparatorIndex (member);
				var field     = JniPeerMembers.GetNullTerminatedUtf8 (member.Slice (0, separator));
				var signature = JniPeerMembers.GetNullTerminatedUtf8 (member.Slice (separator + 1));
				return fields.Members.JniPeerType.GetStaticField (field, signature);
			}, this);
		}

		internal void Dispose ()
		{
			StaticFields.Clear ();
			Utf8StaticFields.Clear ();
		}
	}}
}
