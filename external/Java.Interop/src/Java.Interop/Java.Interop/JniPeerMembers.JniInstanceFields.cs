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
				var fieldName = member.Slice (0, separator);
				var fieldType = member.Slice (separator + 1);
				Span<byte> field = fieldName.Length + 1 <= 256
					? stackalloc byte [fieldName.Length + 1]
					: new byte [fieldName.Length + 1];
				Span<byte> signature = fieldType.Length + 1 <= 512
					? stackalloc byte [fieldType.Length + 1]
					: new byte [fieldType.Length + 1];
				fieldName.CopyTo (field);
				fieldType.CopyTo (signature);
				field [fieldName.Length]         = 0;
				signature [fieldType.Length]     = 0;
				return fields.Members.JniPeerType.GetInstanceField (field, signature);
			}, this);
		}
	}}
}
