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

	public class ClassThresholdClassGetter : PropertyWriter
	{
		// [DebuggerBrowsable (DebuggerBrowsableState.Never)]
		// [EditorBrowsable (EditorBrowsableState.Never)]
		// protected override IntPtr ThresholdClass {
		// 	get { return _members.JniPeerType.PeerReference.Handle; }
		// }
		public ClassThresholdClassGetter ()
		{
			Name = "ThresholdClass";
			PropertyType = TypeReferenceWriter.IntPtr;

			IsProtected = true;
			IsOverride = true;

			Attributes.Add (new DebuggerBrowsableAttr ());
			Attributes.Add (new EditorBrowsableAttr ());

			HasGet = true;
			GetBody.Add ("return _members.JniPeerType.PeerReference.Handle;");
		}
	}

	public class InterfaceThresholdClassGetter : PropertyWriter
	{
		// [DebuggerBrowsable (DebuggerBrowsableState.Never)]
		// [EditorBrowsable (EditorBrowsableState.Never)]
		// protected override IntPtr ThresholdClass {
		// 	get { return _members.JniPeerType.PeerReference.Handle; }
		// }
		public InterfaceThresholdClassGetter (string getExpression)
		{
			Name = "ThresholdClass";
			PropertyType = TypeReferenceWriter.IntPtr;

			IsProtected = true;
			IsOverride = true;

			Attributes.Add (new DebuggerBrowsableAttr ());
			Attributes.Add (new EditorBrowsableAttr ());

			HasGet = true;
			GetBody.Add ($"return {getExpression};");
		}
	}

	public class ThresholdTypeGetter : PropertyWriter
	{
		// [DebuggerBrowsable (DebuggerBrowsableState.Never)]
		// [EditorBrowsable (EditorBrowsableState.Never)]
		// protected override global::System.Type ThresholdType {
		// 	get { return _members.ManagedPeerType; }
		// }
		public ThresholdTypeGetter (string members = "_members")
		{
			Name = "ThresholdType";
			PropertyType = new TypeReferenceWriter ("global::System.Type");

			IsProtected = true;
			IsOverride = true;

			Attributes.Add (new DebuggerBrowsableAttr ());
			Attributes.Add (new EditorBrowsableAttr ());

			HasGet = true;
			GetBody.Add ($"return {members}.ManagedPeerType;");
		}
	}
}
