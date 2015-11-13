using System;
using System.Reflection;
using System.Collections.Generic;

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
				jie = Assembly.Load ("Java.Interop.Export");
			} catch (Exception) {
				return;
			}
			var t   = jie.GetType ("Java.Interop.ExportedMemberBuilder", throwOnError: true);
			var b   = (JniExportedMemberBuilder) Activator.CreateInstance (t);
			exportedMemberBuilder   = SetRuntime (b);
		}

		public abstract class JniExportedMemberBuilder : ISetRuntime
		{
			protected   JniRuntime  Runtime     {get; private set;}

			void ISetRuntime.SetRuntime (JniRuntime runtime)
			{
				Runtime = runtime;
			}

			protected void SetRuntime (JniRuntime runtime)
			{
				Runtime = runtime;
			}

			protected JniExportedMemberBuilder ()
			{
			}

			public abstract IEnumerable<JniNativeMethodRegistration> GetExportedMemberRegistrations (Type declaringType);
		}
	}
}

