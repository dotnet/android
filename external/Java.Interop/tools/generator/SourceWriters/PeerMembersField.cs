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
		// static readonly JniPeerMembers _members = new XAPeerMembers ("android/provider/ContactsContract$AggregationExceptions", typeof (AggregationExceptions));
		public PeerMembersField (CodeGenerationOptions opt, string rawJniType, string declaringType, bool isInterface)
		{
			Name = "_members";
			Type = new TypeReferenceWriter ("JniPeerMembers");

			IsPrivate = isInterface;
			IsStatic = true;
			IsReadonly = true;

			var peer = opt.CodeGenerationTarget == Xamarin.Android.Binder.CodeGenerationTarget.XAJavaInterop1 ? "XAPeerMembers" : "JniPeerMembers";

			Value = $"new {peer} (\"{rawJniType}\", typeof ({declaringType}){(isInterface ? ", isInterface: true" : string.Empty)})";
		}		
	}
}
