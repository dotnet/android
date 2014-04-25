using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace Java.Interop {

	[JniTypeInfo (ManagedPeer.JniTypeName)]
	/* static */ class ManagedPeer : JavaObject {

		internal const string JniTypeName = "com/xamarin/android/ManagedPeer";


		static  readonly    JniPeerMembers  _members        = new JniPeerMembers (JniTypeName, typeof (ManagedPeer));

		static ManagedPeer ()
		{
			_members.JniPeerType.RegisterNativeMethods (
					new JniNativeMethodRegistration ("runConstructor",  RunConstructorSignature,    (Action<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr>) RunConstructor)
			);
		}

		ManagedPeer ()
		{
		}

		internal static void Init ()
		{
			// Present so that JavaVM has _something_ to reference to
			// prompt invocation of the static constructor & registration
		}

		public override JniPeerMembers JniPeerMembers {
			get {return _members;}
		}

		const string RunConstructorSignature = "(Ljava/lang/Object;Ljava/lang/String;Ljava/lang/String;[Ljava/lang/Object;)V";

		static void RunConstructor (
				IntPtr jnienv,
				IntPtr klass,
				IntPtr n_self,
				IntPtr n_assemblyQualifiedName,
				IntPtr n_constructorSignature,
				IntPtr n_constructorArguments)
		{
		}
	}
}

