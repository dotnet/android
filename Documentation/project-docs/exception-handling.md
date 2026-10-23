# Exception Handling

This document describes synchronous exception propagation between managed code
and Java in .NET for Android with CoreCLR and NativeAOT. For callback generation
and registration, see the [JNI interop guide](../guides/internals/JavaJNI_Interop.md).

## Exceptions at a JNI boundary

A Java exception and a managed exception use different runtime mechanisms.
Neither is propagated through a JNI boundary by unwinding the other runtime's
stack. Instead, the binding and callback infrastructure translates exceptions
when control crosses that boundary.

When Java calls managed code, the managed callback boundary catches an escaping
managed exception, arranges for a Java exception to be pending on the calling
thread, and returns to Java. Java then handles that exception using its normal
`catch`/`finally` behavior. When managed code calls Java, the JNI invocation
checks for a pending Java exception and surfaces it as a managed exception.

The [JNI specification](https://docs.oracle.com/en/java/javase/21/docs/specs/jni/design.html#java-exceptions)
describes this pending-exception protocol. It is not a single exception search
that can inspect all the Java and managed frames before either stack unwinds.

## Which code catches a callback exception?

The implementation depends on the callback kind:

| Callback path | Exception boundary |
|---|---|
| Registered binding method | A generated typemap forwarder calls the bound `n_*` marshal method inside a `try`/`catch`. Its catch calls `AndroidEnvironmentInternal.UnhandledException`. |
| Modern generated `n_*` method | The callback uses `JniMarshal.SafeInvokeAction`/`SafeInvokeFunc`, or a typed `JniMarshalTyped` helper, to establish a JNI transition and report exceptions to `JniRuntime.OnUserUnhandledException`. |
| Generated direct `[Export]` method or constructor entrypoint | The generated entrypoint establishes the JNI transition and catches exceptions itself, calling `JniRuntime.OnUserUnhandledException` and ending the transition in `finally`. |

A registered forwarder and a modern bound callback can both be present in the
same call chain. The forwarder does not itself establish a
`BeginMarshalMethod`/`EndMarshalMethod` transition; an older bound marshal method
without a safe-invocation helper relies on the forwarder's exception boundary.

The implementations are in
[`TypeMapAssemblyEmitter`](../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/TypeMapAssemblyEmitter.cs),
[`ExportMethodDispatchEmitter`](../../src/Microsoft.Android.Sdk.TrimmableTypeMap/Generator/ExportMethodDispatchEmitter.cs),
[`JniMarshal.SafeInvoke`](../../external/Java.Interop/src/Java.Interop/Java.Interop/JniMarshal.SafeInvoke.cs),
and [`JniMarshalTyped`](../../src/Mono.Android/Java.Interop/JniMarshalTyped.cs).
They are generated or compiled ahead of execution, not emitted dynamically by
`JNINativeWrapper.CreateDelegate`.

For example, the parameterless `SafeInvokeAction` helper follows this pattern
(simplified from the implementation):

```csharp
if (!JniEnvironment.BeginMarshalMethod (jnienv, out var transition, out var runtime))
	return;

try {
	action (jnienv, self);
} catch (Exception e) {
	runtime.OnUserUnhandledException (ref transition, e);
} finally {
	JniEnvironment.EndMarshalMethod (ref transition);
}
```

`BeginMarshalMethod` enters the marshal method and creates a `JniTransition`
for the current JNI environment. The default `OnEnterMarshalMethod` waits for
GC bridge processing. If the JNI runtime cannot be obtained or entry fails,
`BeginMarshalMethod` logs the failure and returns `false`; the callback does
not run.

The default `OnUserUnhandledException` stores the managed exception in that
transition. `EndMarshalMethod` disposes the transition and calls the runtime's
`RaisePendingException` to make the exception pending in Java. Non-void helpers
return a default ABI value after catching an exception; that value is not a
successful result while a Java exception is pending.

See [`JniEnvironment`](../../external/Java.Interop/src/Java.Interop/Java.Interop/JniEnvironment.cs),
[`JniRuntime`](../../external/Java.Interop/src/Java.Interop/Java.Interop/JniRuntime.cs),
and [`JniTransition`](../../external/Java.Interop/src/Java.Interop/Java.Interop/JniTransition.cs).

### Legacy connector methods

Older generated bindings contain `Get*Handler` connector methods whose bodies
call `JNINativeWrapper.CreateDelegate`. Trimmable typemaps read connector metadata
to locate the marshal method but do not execute the connector.

`JNINativeWrapper.CreateDelegate` remains present so those binary references can
resolve, but it is obsolete with `error: true` and throws
`NotSupportedException` if invoked. It is not the exception boundary for current
Java-to-managed callbacks. Handwritten callbacks must use supported callback
infrastructure rather than allowing managed exceptions to escape an unmanaged
entrypoint.

## Translating the exception

CoreCLR's
[`AndroidRuntime`](../../src/Mono.Android/Android.Runtime/AndroidRuntime.cs)
and NativeAOT's
[`JreRuntime`](../../src/Microsoft.Android.Runtime.NativeAOT/Java.Interop/JreRuntime.cs)
share
[`AndroidExceptionManager`](../../src/Mono.Android/Android.Runtime/AndroidExceptionManager.cs)
for raising pending exceptions and converting Java throwables back to managed
exceptions.

For an ordinary managed exception, the Java-facing object is an
[`Android.Runtime.JavaProxyThrowable`](../../src/Mono.Android/Android.Runtime/JavaProxyThrowable.cs).
It retains the original managed exception. When that proxy returns to managed
code, the runtime can recover the original exception rather than replacing it
with a new exception containing only its message. Existing Java exception
objects can instead retain their Java throwable identity.

When a JNI call returns with an exception pending,
`JniEnvironment.GetExceptionForLastThrowable` retrieves the throwable, clears
the Java pending-exception state, and converts it through
`GetExceptionForThrowable`. Ownership of the retrieved local reference is
consumed according to `JniObjectReferenceOptions`. Clearing the Java state here
does not swallow the failure: the managed invocation reports the converted
exception to its caller.

### Example: managed code calls Java, which calls managed code

Consider this Java method and its generated C# binding, `Demo.Run`:

```java
public static void run(Runnable runnable) {
    try {
        runnable.run();
    } finally {
        System.out.println("Java cleanup");
    }
}
```

The managed caller supplies a runnable that throws:

```csharp
var expected = new InvalidOperationException ("Callback failed.");
using var runnable = new Java.Lang.Runnable (() => throw expected);

try {
	Demo.Run (runnable);
} catch (InvalidOperationException e) when (ReferenceEquals (e, expected)) {
	Console.WriteLine ("The original managed exception returned.");
}
```

For this synchronous round trip, when no exception handler consumes or replaces
the exception:

1. The binding invokes Java's `run` method.
2. Java invokes the registered callback for `Runnable.run`.
3. The managed action throws, and the callback boundary catches the exception.
4. The boundary makes a `JavaProxyThrowable` pending and returns to Java.
5. Java unwinds its frames and executes the `finally` block.
6. The outer binding observes the pending throwable, clears the Java exception
   state, and surfaces the original managed exception to the C# caller.

A Java `catch` block can handle the throwable instead, and an exception thrown
by Java cleanup can replace it. An asynchronous operation that fails after the
callback returns is not part of this synchronous transition; its task or other
asynchronous error-handling mechanism must observe the failure.

## Notification hooks and uncaught exceptions

[`AndroidEnvironment.UnhandledExceptionRaiser`](../../src/Mono.Android/Android.Runtime/AndroidEnvironment.cs)
is a notification hook on paths that call `TryRaiseUnhandledException`.
Setting `RaiseThrowableEventArgs.Handled` to `true` suppresses conversion to a
pending Java exception on those paths. This does not resume the failed managed
method at the throw site or produce its intended return value.

Hook coverage depends on the path: CoreCLR's `AndroidRuntime` override checks
the event before delegating to the default `OnUserUnhandledException`, and the
registered-forwarder handler also checks it through
`AndroidEnvironment.UnhandledException`. NativeAOT's `JreRuntime` inherits the
default `OnUserUnhandledException`; do not assume that every callback path
raises this event identically on both runtimes.

If an exception escapes the Java caller without being caught, Java's
[uncaught-exception handler](https://developer.android.com/reference/java/lang/Thread.UncaughtExceptionHandler)
takes over. The Android runtime hooks can report such failures through
`AppDomain.UnhandledException`; that event is not a recovery mechanism.
CoreCLR's reporting path includes `JNIEnv.PropagateUncaughtException`.
NativeAOT installs an
[`UncaughtExceptionMarshaler`](../../src/Microsoft.Android.Runtime.NativeAOT/UncaughtExceptionMarshaler.cs)
that reports the exception and delegates to the previous Java handler.

## Debugging

Modern safe-invocation helpers use
[`DebuggerDisableUserUnhandledExceptionsAttribute`](https://learn.microsoft.com/dotnet/api/system.diagnostics.debuggerdisableuserunhandledexceptionsattribute).
The default `JniRuntime.OnUserUnhandledException` records the exception in the
transition and calls
[`Debugger.BreakForUserUnhandledException`](https://learn.microsoft.com/dotnet/api/system.diagnostics.debugger.breakforuserunhandledexception).
With a supporting attached debugger and user-unhandled exception breaking
enabled, that call can notify the debugger before disposing the transition
raises the Java exception. An exception handled by the CoreCLR notification
hook does not reach that default handler.

Debugger stops also depend on first-chance exception settings, Just My Code,
the runtime, and debugger support. Do not infer debugger behavior from the
old Mono exception-filter implementation or assume that every callback path
uses the same debugger notification. See
[managing exceptions with the debugger](https://learn.microsoft.com/visualstudio/debugger/managing-exceptions-with-the-debugger)
for exception settings.

Once an exception is pending in Java, most JNI calls are invalid until it is
cleared or control returns to Java. Debugger evaluation of a property or
`ToString()` can itself call Java, so evaluating it while a Java exception is
pending can trigger `JNI DETECTED ERROR` and abort the process. This is distinct
from a managed exception stored in a transition before it is made pending in
Java.

## Regression coverage

- [`ExceptionTest`](../../tests/Mono.Android-Tests/Mono.Android-Tests/System/ExceptionTest.cs)
  checks managed exception and Java throwable identity, causes, and proxy
  conversion.
- [`ExportTests`](../../tests/Mono.Android-Tests/Mono.Android-Tests/Java.Interop/ExportTests.cs)
  checks throwing callbacks, nested JNI calls, and subsequent calls that must
  not inherit stale pending exceptions.
- [`TransferredReferenceTests`](../../tests/Mono.Android-Tests/Mono.Android-Tests/Java.Interop/TransferredReferenceTests.cs)
  checks exception-path reference ownership.
- [`TrimmableTypeMapRuntimeCoverageTests`](../../tests/Mono.Android-Tests/Mono.Android-Tests/Java.Interop/TrimmableTypeMapRuntimeCoverageTests.cs)
  checks that a legacy connector throws when explicitly invoked but is bypassed
  by normal typemap callback dispatch.
