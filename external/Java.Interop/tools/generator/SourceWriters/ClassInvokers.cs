using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class ClassHandleGetter : PropertyWriter
	{
		// internal static new IntPtr class_ref {
		//   get { return _members.JniPeerType.PeerReference.Handle; }
		// }
		public ClassHandleGetter (bool requireNew)
		{
			Name = "class_ref";
			PropertyType = TypeReferenceWriter.IntPtr;

			IsInternal = true;
			IsStatic = true;
			IsShadow = requireNew;

			HasGet = true;
			GetBody.Add ("return _members.JniPeerType.PeerReference.Handle;");
		}
	}

	public class InterfaceHandleGetter : PropertyWriter
	{
		// static IntPtr java_class_ref {
		//   get { return _members.JniPeerType.PeerReference.Handle; }
		// }
		public InterfaceHandleGetter ()
		{
			Name = "java_class_ref";
			PropertyType = TypeReferenceWriter.IntPtr;

			IsStatic = true;

			HasGet = true;
			GetBody.Add ("return _members.JniPeerType.PeerReference.Handle;");
		}
	}

	public class JniPeerMembersGetter : PropertyWriter
	{
		// public override global::Java.Interop.JniPeerMembers JniPeerMembers {
		//   get { return _members; }
		// }
		public JniPeerMembersGetter ()
		{
			Name = "JniPeerMembers";
			PropertyType = new TypeReferenceWriter ("global::Java.Interop.JniPeerMembers");

			IsPublic = true;
			IsOverride = true;

			HasGet = true;
			GetBody.Add ("return _members;");
		}		
	}

	public class ClassThresholdClassGetter : PropertyWriter
	{
		// protected override IntPtr ThresholdClass {
		// 	get { return _members.JniPeerType.PeerReference.Handle; }
		// }
		public ClassThresholdClassGetter ()
		{
			Name = "ThresholdClass";
			PropertyType = TypeReferenceWriter.IntPtr;

			IsProtected = true;
			IsOverride = true;

			HasGet = true;
			GetBody.Add ("return _members.JniPeerType.PeerReference.Handle;");
		}
	}

	public class InterfaceThresholdClassGetter : PropertyWriter
	{
		// protected override IntPtr ThresholdClass {
		// 	get { return class_ref; }
		// }
		public InterfaceThresholdClassGetter ()
		{
			Name = "ThresholdClass";
			PropertyType = TypeReferenceWriter.IntPtr;

			IsProtected = true;
			IsOverride = true;

			HasGet = true;
			GetBody.Add ("return class_ref;");
		}
	}

	public class ThresholdTypeGetter : PropertyWriter
	{
		// protected override global::System.Type ThresholdType {
		// 	get { return _members.ManagedPeerType; }
		// }
		public ThresholdTypeGetter ()
		{
			Name = "ThresholdType";
			PropertyType = new TypeReferenceWriter ("global::System.Type");

			IsProtected = true;
			IsOverride = true;

			HasGet = true;
			GetBody.Add ("return _members.ManagedPeerType;");
		}
	}
}
