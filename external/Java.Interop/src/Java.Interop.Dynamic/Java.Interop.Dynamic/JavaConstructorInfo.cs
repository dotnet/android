using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Text;

using Java.Interop;

namespace Java.Interop.Dynamic {

	class JavaConstructorInfo : JavaMethodBase {

		public JavaConstructorInfo (JniPeerMembers members, JniObjectReference method)
			: base (members, method)
		{
		}

		public  override    string  Name {
			get {return "<init>";}
		}

		public  override    bool    IsStatic {
			get {return false;}
		}

		public override bool IsConstructor {
			get {return true;}
		}

		protected override string JniReturnType {
			get {return "V";}
		}

		public override unsafe object? Invoke (IJavaPeerable? self, JniArgumentValue* arguments)
		{
			var signature   = JniSignature ?? throw new InvalidOperationException ("No JniSignature!");
			if (self == null) {
				var h   = members.InstanceMethods.StartCreateInstance (signature, typeof (JavaInstanceProxy), arguments);
				self    = JniEnvironment.Runtime.ValueManager.GetValue<JavaInstanceProxy> (ref h, JniObjectReferenceOptions.CopyAndDispose);
				if (self == null) {
					throw new InvalidOperationException ($"Could not create instance of {members.ManagedPeerType}!");
				}
			}
			members.InstanceMethods.FinishCreateInstance (signature, self, arguments);
			return new DynamicJavaInstance (self);
		}
	}
}
