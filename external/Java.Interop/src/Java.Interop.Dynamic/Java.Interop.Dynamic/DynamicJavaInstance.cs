using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Java.Interop;


namespace Java.Interop.Dynamic {

	public class DynamicJavaInstance : IDynamicMetaObjectProvider, IDisposable {

		JavaClassInfo   klass;

		public DynamicJavaInstance (IJavaPeerable value)
		{
			if (value == null)
				throw new ArgumentNullException ("value");

			Value   = value;

			var type    = JniEnvironment.Types.GetJniTypeNameFromInstance (value.PeerReference) ??
				throw new InvalidOperationException ($"Could not get JniType from value `{value}`.");
			klass       = JavaClassInfo.GetClassInfo (type);
		}

		bool    disposed;

		public  IJavaPeerable   Value       {get; private set;}

		public void Dispose ()
		{
			Dispose (disposing: true);
			GC.SuppressFinalize (this);
		}

		protected virtual void Dispose (bool disposing)
		{
			if (disposed)
				return;

			if (disposing) {
				var java    = Value as IJavaPeerable;
				if (java != null) {
					java.DisposeUnlessReferenced ();
				}

				if (klass != null)
					klass.Dispose ();
			}

			disposed    = true;
			Value       = null!;
			klass       = null!;
		}

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject (Expression parameter)
		{
			return new MetaObject (parameter, this);
		}

		class MetaObject : JniMetaObject {

			DynamicJavaInstance instance;

			public MetaObject (Expression parameter, DynamicJavaInstance instance)
				: base (parameter, instance, instance.klass)
			{
				this.instance   = instance;
			}

			protected override bool Disposed {
				get {return instance.disposed;}
			}

			protected override JniObjectReference   ConversionTarget {
				get {
					return instance.Value.PeerReference;
				}
			}

			protected override bool HasSelf {
				get {return (instance.Value as IJavaPeerable) != null;}
			}

			protected override Expression GetSelf ()
			{
				return Expression.Constant (instance.Value as IJavaPeerable, typeof (IJavaPeerable));
			}
		}
	}
}

