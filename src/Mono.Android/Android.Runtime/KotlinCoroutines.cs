#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Java.Interop;
using Java.Lang;
using Java.Lang.Reflect;

using Exception = System.Exception;

namespace Android.Runtime {

	/// <summary>
	/// Runs Kotlin <c>suspend</c> functions as <see cref="Task" /> objects.
	/// Generated bindings call these methods from their <c>*Async</c> methods.
	/// </summary>
	/// <remarks>
	/// The <c>kotlin.coroutines.Continuation</c> is a <c>java.lang.reflect.Proxy</c>, so this type has no
	/// build-time dependency on Kotlin. When <c>kotlinx.coroutines</c> is in the app, the continuation context
	/// is a <c>kotlinx.coroutines.Job</c>, and cancellation of the token also cancels that job.
	/// </remarks>
	public static class KotlinCoroutines {
		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

		/// <summary>Runs a Kotlin <c>suspend</c> function that returns <c>Unit</c>.</summary>
		/// <param name="invoke">Calls the bound method with the supplied <c>kotlin.coroutines.Continuation</c>.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		public static Task InvokeAsync (Func<IJavaObject, Java.Lang.Object?> invoke, CancellationToken cancellationToken = default) =>
			InvokeCoreAsync (invoke, cancellationToken);

		/// <summary>Runs a Kotlin <c>suspend</c> function with a non-null result and converts the result to <typeparamref name="T" />.</summary>
		/// <param name="invoke">Calls the bound method with the supplied <c>kotlin.coroutines.Continuation</c>.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		public static async Task<T> InvokeAsync<
				[DynamicallyAccessedMembers (Constructors)]
				T
		> (Func<IJavaObject, Java.Lang.Object?> invoke, CancellationToken cancellationToken = default)
			where T : notnull
		{
			var value = await InvokeCoreAsync (invoke, cancellationToken).ConfigureAwait (false);
			return JavaConvert.FromJavaObject<T> (value) ??
				throw new InvalidOperationException ("Kotlin returned null for a non-null result.");
		}

		/// <summary>Runs a Kotlin <c>suspend</c> function with a nullable result and converts the result to <typeparamref name="T" />.</summary>
		/// <remarks>For a nullable value type, use a <see cref="Nullable{T}" /> type argument, for example <c>int?</c>.</remarks>
		/// <param name="invoke">Calls the bound method with the supplied <c>kotlin.coroutines.Continuation</c>.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		public static async Task<T?> InvokeNullableAsync<
				[DynamicallyAccessedMembers (Constructors)]
				T
		> (Func<IJavaObject, Java.Lang.Object?> invoke, CancellationToken cancellationToken = default)
		{
			var value = await InvokeCoreAsync (invoke, cancellationToken).ConfigureAwait (false);
			return JavaConvert.FromJavaObject<T> (value);
		}

		static Task<Java.Lang.Object?> InvokeCoreAsync (Func<IJavaObject, Java.Lang.Object?> invoke, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull (invoke);
			if (cancellationToken.IsCancellationRequested)
				return Task.FromCanceled<Java.Lang.Object?> (cancellationToken);

			var handler = new ContinuationHandler (cancellationToken);
			try {
				var result = invoke (handler.Continuation);
				if (!ContinuationHandler.IsSuspended (result))
					handler.SetResult (result);
			} catch (Exception e) {
				handler.SetException (e);
			}
			return handler.Task;
		}

		sealed class ContinuationHandler : Java.Lang.Object, IInvocationHandler {

			static readonly KotlinTypes types = new KotlinTypes ();

			readonly TaskCompletionSource<Java.Lang.Object?> completion = new (TaskCreationOptions.RunContinuationsAsynchronously);
			readonly CancellationToken cancellationToken;
			readonly CancellationTokenRegistration registration;
			readonly Java.Lang.Object? job;

			public ContinuationHandler (CancellationToken cancellationToken)
			{
				this.cancellationToken = cancellationToken;
				// Job(parent: Job?) with no parent.
				job = types.CreateJob?.Invoke (null, new Java.Lang.Object [1]);
				Continuation = Proxy.NewProxyInstance (types.Loader, new [] { types.Continuation }, this);
				registration = cancellationToken.Register (Cancel);
			}

			public Java.Lang.Object Continuation { get; }

			public Task<Java.Lang.Object?> Task => completion.Task;

			public static bool IsSuspended (Java.Lang.Object? value) =>
				value != null && JNIEnv.IsSameObject (value.Handle, types.Suspended.Handle);

			public void SetResult (Java.Lang.Object? value)
			{
				completion.TrySetResult (value);
				registration.Unregister ();
			}

			public void SetException (Exception e)
			{
				if (e is Java.Util.Concurrent.CancellationException)
					completion.TrySetCanceled (cancellationToken.IsCancellationRequested ? cancellationToken : new CancellationToken (true));
				else
					completion.TrySetException (e);
				registration.Unregister ();
			}

			void Cancel ()
			{
				// With kotlinx.coroutines, also cancel the Kotlin work. Not all suspend functions observe the Job.
				try {
					if (job != null)
						types.CancelJob?.Invoke (job, new Java.Lang.Object [1]);
				} finally {
					completion.TrySetCanceled (cancellationToken);
				}
			}

			public Java.Lang.Object? Invoke (Java.Lang.Object? proxy, Method? method, Java.Lang.Object []? args)
			{
				switch (method?.Name) {
				case "getContext":
					return job ?? types.EmptyContext;
				case "resumeWith":
					Resume (args?.Length > 0 ? args [0] : null);
					return null;
				case "hashCode":
					return Integer.ValueOf (JavaSystem.IdentityHashCode (proxy));
				case "equals":
					return Java.Lang.Boolean.ValueOf (proxy != null && args?.Length > 0 && args [0] != null && JNIEnv.IsSameObject (proxy.Handle, args [0].Handle));
				case "toString":
					return new Java.Lang.String ("Android.Runtime.KotlinCoroutines continuation");
				default:
					throw new UnsupportedOperationException (method?.Name);
				}
			}

			void Resume (Java.Lang.Object? value)
			{
				// `kotlin.Result` is an inline class: a failure is a `kotlin.Result$Failure`, and a success is the value.
				if (!types.Failure.IsInstance (value)) {
					SetResult (value);
					return;
				}
				var throwable = types.FailureException.Get (value);
				var reference = throwable?.PeerReference ?? default;
				var exception = JniEnvironment.Runtime.GetExceptionForThrowable (ref reference, JniObjectReferenceOptions.Copy);
				GC.KeepAlive (throwable);
				SetException (exception ?? new InvalidOperationException ("Kotlin returned an invalid failure."));
			}
		}

		sealed class KotlinTypes {
			public readonly ClassLoader? Loader;
			public readonly Class Continuation;
			public readonly Java.Lang.Object EmptyContext;
			public readonly Java.Lang.Object Suspended;
			public readonly Class Failure;
			public readonly Field FailureException;
			public readonly Method? CreateJob;
			public readonly Method? CancelJob;

			public KotlinTypes ()
			{
				// The app class loader, which also loads the Kotlin standard library.
				Loader = Class.FromType (typeof (ContinuationHandler)).ClassLoader;
				Continuation = Class.ForName ("kotlin.coroutines.Continuation", false, Loader);
				EmptyContext = GetStaticField ("kotlin.coroutines.EmptyCoroutineContext", "INSTANCE");
				Suspended = GetStaticField ("kotlin.coroutines.intrinsics.CoroutineSingletons", "COROUTINE_SUSPENDED");
				Failure = Class.ForName ("kotlin.Result$Failure", false, Loader);
				FailureException = Failure.GetField ("exception");

				try {
					var job = Class.ForName ("kotlinx.coroutines.Job", false, Loader);
					CreateJob = Class.ForName ("kotlinx.coroutines.JobKt", false, Loader).GetMethod ("Job", job);
					CancelJob = job.GetMethod ("cancel", Class.FromType (typeof (Java.Util.Concurrent.CancellationException)));
				} catch (Throwable) {
					CreateJob = CancelJob = null;
				}
			}

			Java.Lang.Object GetStaticField (string className, string fieldName) =>
				Class.ForName (className, false, Loader).GetField (fieldName).Get (null) ??
					throw new InvalidOperationException ($"Could not read {className}.{fieldName}.");
		}
	}
}
