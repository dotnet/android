using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Java.Interop {

	partial class JniRuntime {

		partial class CreationOptions {
			public  JniExportedMemberBuilder    ExportedMemberBuilder       {get; set;}
		}

		JniExportedMemberBuilder                exportedMemberBuilder;
		public  JniExportedMemberBuilder        ExportedMemberBuilder       {
			get {
				if (exportedMemberBuilder == null)
					throw new NotSupportedException ("JniRuntime.ExportedMemberBuilder is not supported.");
				return exportedMemberBuilder;
			}
		}

		partial void SetExportedMemberBuilder (CreationOptions options)
		{
			if (options.ExportedMemberBuilder != null) {
				exportedMemberBuilder   = SetRuntime (options.ExportedMemberBuilder);
				return;
			}

			Assembly jie;
			try {
				jie = Assembly.Load (new AssemblyName ("Java.Interop.Export"));
			} catch (Exception) {
				return;
			}
			var t   = jie.GetType ("Java.Interop.ExportedMemberBuilder");
			if (t == null)
				throw new InvalidOperationException ("Could not find Java.Interop.ExportedMemberBuilder from Java.Interop.Export.dll!");
			var b   = (JniExportedMemberBuilder) Activator.CreateInstance (t);
			exportedMemberBuilder   = SetRuntime (b);
		}

		public abstract class JniExportedMemberBuilder : ISetRuntime
		{
			public      JniRuntime  Runtime     {get; private set;}

			protected JniExportedMemberBuilder ()
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

