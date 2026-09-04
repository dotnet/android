using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class PeerMembersField : FieldWriter
	{
		// static readonly JniPeerMembers _members = new JniPeerMembers ("android/provider/ContactsContract$AggregationExceptions", typeof (AggregationExceptions));
		public PeerMembersField (CodeGenerationOptions opt, string rawJniType, string declaringType, bool isInterface, string name = "_members")
		{
			Name = name;
			Type = new TypeReferenceWriter ("JniPeerMembers");

			IsPrivate = isInterface;
			IsStatic = true;
			IsReadonly = true;

			Value = $"new JniPeerMembers (\"{rawJniType}\", typeof ({declaringType}){(isInterface ? ", isInterface: true" : string.Empty)})";
		}		
	}
}
