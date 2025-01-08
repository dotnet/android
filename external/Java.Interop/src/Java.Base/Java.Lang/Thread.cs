namespace Java.Lang {
	partial class Thread {

#if JAVA_API_21
		partial interface IBuilder {
			partial class IOfPlatformInvoker {
				IBuilder? IBuilder.InheritInheritableThreadLocals (bool value) =>
					InheritInheritableThreadLocals (value);
				IBuilder? IBuilder.Name (string? name) =>
					Name (name);
				IBuilder? IBuilder.Name (string? name, long v) =>
					Name (name, v);
				IBuilder? IBuilder.UncaughtExceptionHandler (IUncaughtExceptionHandler? u) =>
					UncaughtExceptionHandler (u);
			}
			partial class IOfVirtualInvoker {
				IBuilder? IBuilder.InheritInheritableThreadLocals (bool value) =>
					InheritInheritableThreadLocals (value);
				IBuilder? IBuilder.Name (string? name) =>
					Name (name);
				IBuilder? IBuilder.Name (string? name, long v) =>
					Name (name, v);
				IBuilder? IBuilder.UncaughtExceptionHandler (IUncaughtExceptionHandler? u) =>
					UncaughtExceptionHandler (u);
			}
		}
#endif  // JAVA_API_21

	}
}
