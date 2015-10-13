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

using Mono.Linq.Expressions;

using Java.Interop;

namespace Java.Interop.Dynamic {

	class JavaConstructorInfo : JavaMethodBase {

		public JavaConstructorInfo (JniPeerMembers members, JniReferenceSafeHandle method)
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

		public override unsafe object Invoke (IJavaObject self, JValue* arguments)
		{
			if (self == null) {
				var h   = members.InstanceMethods.StartCreateInstance (JniSignature, typeof (JavaInstanceProxy), arguments);
				self    = JniEnvironment.Current.JavaVM.GetObject<JavaInstanceProxy> (h, JniHandleOwnership.Transfer);
			}
			members.InstanceMethods.FinishCreateInstance (JniSignature, self, arguments);
			return new DynamicJavaInstance (self);
		}
	}
}
