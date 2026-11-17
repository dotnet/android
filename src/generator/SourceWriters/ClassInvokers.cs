using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class JniPeerMembersGetter : PropertyWriter
	{
		// [DebuggerBrowsable (DebuggerBrowsableState.Never)]
		// [EditorBrowsable (EditorBrowsableState.Never)]
		// public override global::Java.Interop.JniPeerMembers JniPeerMembers {
		//   get { return _members; }
		// }
		public JniPeerMembersGetter (string name = "_members")
		{
			Name = "JniPeerMembers";
			PropertyType = new TypeReferenceWriter ("global::Java.Interop.JniPeerMembers");

			IsPublic = true;
			IsOverride = true;

			Attributes.Add (new DebuggerBrowsableAttr ());
			Attributes.Add (new EditorBrowsableAttr ());

			HasGet = true;
			GetBody.Add ($"return {name};");
		}		
	}
}
