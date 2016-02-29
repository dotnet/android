using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Java.Interop {

	partial class JniRuntime {

		partial class CreationOptions {
			public  JniMarshalMemberBuilder    MarshalMemberBuilder        {get; set;}
		}

		JniMarshalMemberBuilder                exportedMemberBuilder;
		public  JniMarshalMemberBuilder        ExportedMemberBuilder       {
			get {
				if (exportedMemberBuilder == null)
					throw new NotSupportedException ("JniRuntime.ExportedMemberBuilder is not supported.");
				return exportedMemberBuilder;
			}
		}

		partial void SetMarshalMemberBuilder (CreationOptions options)
		{
			if (options.MarshalMemberBuilder != null) {
				exportedMemberBuilder   = SetRuntime (options.MarshalMemberBuilder);
				return;
			}

			Assembly jie;
			try {
				jie = Assembly.Load (new AssemblyName ("Java.Interop.Export"));
			} catch (Exception) {
				return;
			}
			var t   = jie.GetType ("Java.Interop.MarshalMemberBuilder");
			if (t == null)
				throw new InvalidOperationException ("Could not find Java.Interop.MarshalMemberBuilder from Java.Interop.Export.dll!");
			var b   = (JniMarshalMemberBuilder) Activator.CreateInstance (t);
			exportedMemberBuilder   = SetRuntime (b);
		}

		public abstract class JniMarshalMemberBuilder : ISetRuntime
		{
			public      JniRuntime  Runtime     {get; private set;}

			protected JniMarshalMemberBuilder ()
			{
			}

			public virtual void OnSetRuntime (JniRuntime runtime)
			{
				Runtime = runtime;
			}

			public  Delegate                                                CreateMarshalToManagedDelegate (Delegate value)
			{
				if (value == null)
					throw new ArgumentNullException (nameof (value));
				return CreateMarshalToManagedExpression (value.GetMethodInfo ()).Compile ();
			}

			public  abstract    LambdaExpression                            CreateMarshalToManagedExpression (MethodInfo method);
			public  abstract    IEnumerable<JniNativeMethodRegistration>    GetExportedMemberRegistrations (Type declaringType);

			public  abstract    Expression<Func<ConstructorInfo, JniObjectReference, object[], object>>     CreateConstructActivationPeerExpression (ConstructorInfo constructor);

			public  Func<ConstructorInfo, JniObjectReference, object[], object>                             CreateConstructActivationPeerFunc (ConstructorInfo constructor)
			{
				if (constructor == null)
					throw new ArgumentNullException (nameof (constructor));

				var e   = CreateConstructActivationPeerExpression (constructor);
				return e.Compile ();
			}
		}
	}
}

