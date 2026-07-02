#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml.Linq;

using Xamarin.Android.Tools;

namespace Java.Interop {

	struct JavaVMInitArgs {
		public  JniVersion  version;
		public  int         nOptions;
		public  IntPtr      options;
		public  byte        ignoreUnrecognized;
	}

	struct JavaVMOption {
		public  IntPtr      optionString;
		public  IntPtr      extraInfo;
	}

	public class TestJVMOptions : JniRuntime.CreationOptions {

		internal    List<string>                Options         {get;} = new List<string> ();
		internal    Dictionary<string, Type>?   typeMappings;
		internal    NativeLibraryJvmLibraryHandler? LibraryHandler;

		public      ICollection<string>         JarFilePaths    {get;} = new List<string> ();
		public      IDictionary<string, Type>   TypeMappings    => typeMappings ??= new Dictionary<string, Type> ();

		internal    List<string>                ClassPath       {get;} = new List<string> ();
		internal    JdkInfo?                    JdkInfo         {get; set;}

		public TestJVMOptions ()
		{
			JniVersion  = JniVersion.v1_2;
		}

		public TestJVMOptions AddOption (string option)
		{
			if (option == null)
				throw new ArgumentNullException (nameof (option));
			Options.Add (option);
			return this;
		}
	}

	public class TestJVM : JniRuntime {

		NativeLibraryJvmLibraryHandler? LibraryHandler;

#if !__ANDROID__
		public JdkInfo? JdkInfo { get; private set; }
#endif  // !__ANDROID__

		public TestJVM (TestJVMOptions options)
			: base (CreateJVM (options))
		{
			LibraryHandler  = options.LibraryHandler;
#if !__ANDROID__
			JdkInfo = options.JdkInfo;
#endif  // !__ANDROID__
		}

		static unsafe TestJVMOptions CreateJVM (TestJVMOptions options)
		{
			if (options == null)
				throw new ArgumentNullException (nameof (options));

			options.TypeManager             ??= new TestJvmTypeManager (options.typeMappings);
			options.ValueManager            ??= new ManagedValueManager ();
			options.ObjectReferenceManager  ??= new ManagedObjectReferenceManager ();

			if (options.InvocationPointer != IntPtr.Zero || options.EnvironmentPointer != IntPtr.Zero)
				return options;

			var dir = GetOutputDirectoryName ();
			var info = GetJdkInfo ();

			options.JvmLibraryPath ??= info.JdkJvmPath;
			options.JdkInfo = info.JdkInfo;

			if (string.IsNullOrEmpty (options.JvmLibraryPath))
				throw new InvalidOperationException ($"Member `{nameof (TestJVMOptions)}.{nameof (TestJVMOptions.JvmLibraryPath)}` must be set.");

			foreach (var jar in options.JarFilePaths)
				options.ClassPath.Add (Path.Combine (dir, jar));
			options.AddOption ("-Xcheck:jni");

			var handler = new NativeLibraryJvmLibraryHandler ();
			options.LibraryHandler = handler;
			try {
				handler.LoadJvmLibrary (options.JvmLibraryPath);
				EnsureJavaInteropJar (options);

				var args = new JavaVMInitArgs {
					version             = options.JniVersion,
					nOptions            = options.Options.Count + 1,
					ignoreUnrecognized  = 0,
				};
				var jvmOptions = new JavaVMOption [options.Options.Count + 1];
				try {
					for (int i = 0; i < options.Options.Count; ++i)
						jvmOptions [i].optionString = Marshal.StringToHGlobalAnsi (options.Options [i]);
					jvmOptions [options.Options.Count].optionString = Marshal.StringToHGlobalAnsi (
							string.Format ("-Djava.class.path={0}", string.Join (Path.PathSeparator.ToString (), options.ClassPath)));

					fixed (JavaVMOption* popts = jvmOptions) {
						args.options = (IntPtr) popts;
						int r = handler.CreateJavaVM (out var javavm, out var jnienv, ref args);
						if (r != 0) {
							handler.Dispose ();
							options.LibraryHandler = null;
							throw new NotSupportedException (
									string.Format (
										"The JDK supports creating at most one JVM per process, ever; " +
										"do you have a JVM running already, or have you already created (and destroyed?) one? " +
										"(JNI_CreateJavaVM returned {0}).",
										r));
						}
						options.InvocationPointer    = javavm;
						options.EnvironmentPointer   = jnienv;
					}
				} finally {
					for (int i = 0; i < jvmOptions.Length; ++i)
						Marshal.FreeHGlobal (jvmOptions [i].optionString);
				}
				return options;
			} catch {
				if (options.LibraryHandler != null) {
					options.LibraryHandler.Dispose ();
					options.LibraryHandler = null;
				}
				throw;
			}
		}

		static void EnsureJavaInteropJar (TestJVMOptions options)
		{
			if (options.ClassPath.Any (p => p.EndsWith ("java-interop.jar", StringComparison.OrdinalIgnoreCase)))
				return;

			var dir = GetOutputDirectoryName ();
			var jar = Path.Combine (dir, "java-interop.jar");
			if (!File.Exists (jar)) {
				throw new FileNotFoundException ($"`java-interop.jar` is required.  Please add to `TestJVMOptions.ClassPath`.  Tried to find it in `{jar}`.");
			}
			options.ClassPath.Add (jar);
		}

		static string GetOutputDirectoryName ()
		{
			return Path.GetDirectoryName (typeof (TestJVM).Assembly.Location) ??
				Environment.CurrentDirectory;
		}

		public static string? GetJvmLibraryPath (Action<TraceLevel, string>? logger = null) => GetJdkInfo (logger).JdkJvmPath;

		static (JdkInfo? JdkInfo, string? JdkJvmPath) GetJdkInfo (Action<TraceLevel, string>? logger = null)
		{
			var info = ReadJavaSdkDirectoryFromJdkInfoProps (logger);
			if (info.JdkJvmPath != null)
				return (JdkInfo: info.JavaSdkDirectory == null ? null : new JdkInfo (info.JavaSdkDirectory), JdkJvmPath: info.JdkJvmPath);

			var jdk = JdkInfo.GetKnownSystemJdkInfos (logger).FirstOrDefault ();
			return (jdk, jdk?.JdkJvmPath);
		}

		static (string? JavaSdkDirectory, string? JdkJvmPath) ReadJavaSdkDirectoryFromJdkInfoProps (Action<TraceLevel, string>? logger)
		{
			var jdkPropFile = TryProbeForJdkInfoProps (logger);
			logger?.Invoke (TraceLevel.Verbose, $"TestJVM: jdkPropFile? {jdkPropFile}");
			if (!File.Exists (jdkPropFile))
				return (null, null);

			logger?.Invoke (TraceLevel.Verbose, $"TestJVM: Extracting $(JdkJvmPath) from: {jdkPropFile}");
			var msbuild = XNamespace.Get ("http://schemas.microsoft.com/developer/msbuild/2003");

			var jdkProps = XDocument.Load (jdkPropFile);
			var jdkJvmPath = jdkProps.Elements ()
				.Elements (msbuild + "Choose")
				.Elements (msbuild + "When")
				.Elements (msbuild + "PropertyGroup")
				.Elements (msbuild + "JdkJvmPath")
				.FirstOrDefault ();
			if (jdkJvmPath == null)
				return (null, null);

			var jdkPath = jdkProps.Elements ()
				.Elements (msbuild + "PropertyGroup")
				.Elements (msbuild + "JavaSdkDirectory")
				.FirstOrDefault ();

			logger?.Invoke (TraceLevel.Verbose, $"TestJVM: $(JavaSdkDirectory)={jdkPath?.Value}; $(JdkJvmPath)={jdkJvmPath.Value}");
			return (JavaSdkDirectory: jdkPath?.Value, JdkJvmPath: jdkJvmPath.Value);
		}

		static string? TryProbeForJdkInfoProps (Action<TraceLevel, string>? logger)
		{
			for (var probing = Path.GetDirectoryName (typeof (TestJVM).Assembly.Location); probing != null; probing = Path.GetDirectoryName (probing)) {
				logger?.Invoke (TraceLevel.Verbose, $"TestJVM: probing for JdkInfo.props around {probing}");
				if (File.Exists (Path.Combine (probing, "Java.Interop.sln")))
					return ProbeFromRootDir (probing);

				var dirName = Path.GetFileName (probing);
				if (dirName.StartsWith ("Test", StringComparison.OrdinalIgnoreCase)) {
					var buildName = dirName.Replace ("Test", "Build");
					if (buildName.Contains ('-'))
						buildName = buildName.Substring (0, buildName.IndexOf ('-'));
					return Path.Combine (Path.GetDirectoryName (probing)!, buildName, "JdkInfo.props");
				}
			}
			return null;

			static string ProbeFromRootDir (string location)
			{
				var buildDebug = Path.Combine (location, "bin", "BuildDebug");
				var buildRelease = Path.Combine (location, "bin", "BuildRelease");
				if (Directory.Exists (buildDebug) && !Directory.Exists (buildRelease))
					return Path.Combine (buildDebug, "JdkInfo.props");
				if (Directory.Exists (buildRelease) && !Directory.Exists (buildDebug))
					return Path.Combine (buildRelease, "JdkInfo.props");
				var dir = Directory.GetLastWriteTime (buildDebug) > Directory.GetLastWriteTime (buildRelease)
					? buildDebug
					: buildRelease;
				return Path.Combine (dir, "JdkInfo.props");
			}
		}

		public TestJVM (string[]? jars = null, Dictionary<string, Type>? typeMappings = null)
			: this (CreateOptions (jars, typeMappings))
		{
		}

		static TestJVMOptions CreateOptions (string[]? jarFiles, Dictionary<string, Type>? typeMappings)
		{
			var options = new TestJVMOptions ();
			if (typeMappings != null) {
				foreach (var e in typeMappings)
					options.TypeMappings.Add (e.Key, e.Value);
			}
			if (jarFiles != null) {
				foreach (var jar in jarFiles)
					options.JarFilePaths.Add (jar);
			}
			return options;
		}

		public override string? GetCurrentManagedThreadName ()
		{
			return Thread.CurrentThread.Name;
		}

		public override string GetCurrentManagedThreadStackTrace (int skipFrames, bool fNeedFileInfo)
		{
			return new StackTrace (skipFrames, fNeedFileInfo).ToString ();
		}

		protected override void Dispose (bool disposing)
		{
			base.Dispose (disposing);
			LibraryHandler?.Dispose ();
			LibraryHandler = null;
		}
	}

	internal sealed class NativeLibraryJvmLibraryHandler : IDisposable {

		IntPtr  handle;
		IntPtr  create;

		public void LoadJvmLibrary (string path)
		{
			handle = NativeLibrary.Load (path);
			if (!NativeLibrary.TryGetExport (handle, "JNI_CreateJavaVM", out create) ||
					!NativeLibrary.TryGetExport (handle, "JNI_GetCreatedJavaVMs", out _)) {
				Dispose ();
				throw new NotSupportedException ($"Library `{path}` does not export the required symbols `JNI_CreateJavaVM` or `JNI_GetCreatedJavaVMs`!");
			}
		}

		public unsafe int CreateJavaVM (out IntPtr javavm, out IntPtr jnienv, ref JavaVMInitArgs args)
		{
			var createJavaVM = (delegate* unmanaged<out IntPtr, out IntPtr, ref JavaVMInitArgs, int>) create;
			return createJavaVM (out javavm, out jnienv, ref args);
		}

		public void Dispose ()
		{
			if (handle != IntPtr.Zero)
				NativeLibrary.Free (handle);
			handle      = IntPtr.Zero;
			create      = IntPtr.Zero;
		}
	}

	class ManagedObjectReferenceManager : JniRuntime.JniObjectReferenceManager {

		int grefCount;
		int wgrefCount;

		public override int GlobalReferenceCount => grefCount;
		public override int WeakGlobalReferenceCount => wgrefCount;

		public override JniObjectReference CreateGlobalReference (JniObjectReference reference)
		{
			var r = base.CreateGlobalReference (reference);
			if (r.IsValid)
				Interlocked.Increment (ref grefCount);
			return r;
		}

		public override void DeleteGlobalReference (ref JniObjectReference reference)
		{
			if (reference.IsValid)
				Interlocked.Decrement (ref grefCount);
			base.DeleteGlobalReference (ref reference);
		}

		public override JniObjectReference CreateWeakGlobalReference (JniObjectReference reference)
		{
			var r = base.CreateWeakGlobalReference (reference);
			if (r.IsValid)
				Interlocked.Increment (ref wgrefCount);
			return r;
		}

		public override void DeleteWeakGlobalReference (ref JniObjectReference reference)
		{
			if (reference.IsValid)
				Interlocked.Decrement (ref wgrefCount);
			base.DeleteWeakGlobalReference (ref reference);
		}
	}

	public class ManagedValueManager : JniRuntime.ReflectionJniValueManager {

		Dictionary<int, List<IJavaPeerable>>? RegisteredInstances = new Dictionary<int, List<IJavaPeerable>> ();

		public override void WaitForGCBridgeProcessing ()
		{
		}

		public override void CollectPeers ()
		{
			var registered = RegisteredInstances ?? throw new ObjectDisposedException (nameof (ManagedValueManager));
			var peers = new List<IJavaPeerable> ();

			lock (registered) {
				foreach (var ps in registered.Values)
					peers.AddRange (ps);
				registered.Clear ();
			}

			List<Exception>? exceptions = null;
			foreach (var peer in peers) {
				try {
					peer.Dispose ();
				} catch (Exception e) {
					exceptions ??= new List<Exception> ();
					exceptions.Add (e);
				}
			}
			if (exceptions != null)
				throw new AggregateException ("Exceptions while collecting peers.", exceptions);
		}

		public override void AddPeer (IJavaPeerable value)
		{
			var registered = RegisteredInstances ?? throw new ObjectDisposedException (nameof (ManagedValueManager));
			var r = value.PeerReference;
			if (!r.IsValid)
				throw new ObjectDisposedException (value.GetType ().FullName);

			if (r.Type != JniObjectReferenceType.Global) {
				value.SetPeerReference (r.NewGlobalRef ());
				JniObjectReference.Dispose (ref r, JniObjectReferenceOptions.CopyAndDispose);
			}

			int key = value.JniIdentityHashCode;
			lock (registered) {
				if (!registered.TryGetValue (key, out var peers)) {
					registered.Add (key, new List<IJavaPeerable> { value });
					return;
				}

				for (int i = peers.Count - 1; i >= 0; i--) {
					var peer = peers [i];
					if (!JniEnvironment.Types.IsSameObject (peer.PeerReference, value.PeerReference))
						continue;
					if (Replaceable (peer, value)) {
						peers [i] = value;
					} else {
						WarnNotReplacing (key, value, peer);
					}
					return;
				}
				peers.Add (value);
			}
		}

		static bool Replaceable (IJavaPeerable peer, IJavaPeerable value)
		{
			return peer.JniManagedPeerState.HasFlag (JniManagedPeerStates.Replaceable) &&
				!value.JniManagedPeerState.HasFlag (JniManagedPeerStates.Replaceable);
		}

		void WarnNotReplacing (int key, IJavaPeerable ignoreValue, IJavaPeerable keepValue)
		{
			Runtime.ObjectReferenceManager.WriteGlobalReferenceLine (
					"Warning: Not registering PeerReference={0} IdentityHashCode=0x{1} Instance={2} Instance.Type={3} Java.Type={4}; " +
					"keeping previously registered PeerReference={5} Instance={6} Instance.Type={7} Java.Type={8}.",
					ignoreValue.PeerReference.ToString (),
					key.ToString ("x"),
					RuntimeHelpers.GetHashCode (ignoreValue).ToString ("x"),
					ignoreValue.GetType ().FullName,
					JniEnvironment.Types.GetJniTypeNameFromInstance (ignoreValue.PeerReference),
					keepValue.PeerReference.ToString (),
					RuntimeHelpers.GetHashCode (keepValue).ToString ("x"),
					keepValue.GetType ().FullName,
					JniEnvironment.Types.GetJniTypeNameFromInstance (keepValue.PeerReference));
		}

		public override IJavaPeerable? PeekPeer (JniObjectReference reference)
		{
			var registered = RegisteredInstances ?? throw new ObjectDisposedException (nameof (ManagedValueManager));
			if (!reference.IsValid)
				return null;

			int key = GetJniIdentityHashCode (reference);
			lock (registered) {
				if (!registered.TryGetValue (key, out var peers))
					return null;

				for (int i = peers.Count - 1; i >= 0; i--) {
					var peer = peers [i];
					if (JniEnvironment.Types.IsSameObject (reference, peer.PeerReference))
						return peer;
				}
				if (peers.Count == 0)
					registered.Remove (key);
			}
			return null;
		}

		public override void RemovePeer (IJavaPeerable value)
		{
			var registered = RegisteredInstances ?? throw new ObjectDisposedException (nameof (ManagedValueManager));
			if (value == null)
				throw new ArgumentNullException (nameof (value));

			int key = value.JniIdentityHashCode;
			lock (registered) {
				if (!registered.TryGetValue (key, out var peers))
					return;

				for (int i = peers.Count - 1; i >= 0; i--) {
					if (object.ReferenceEquals (value, peers [i]))
						peers.RemoveAt (i);
				}
				if (peers.Count == 0)
					registered.Remove (key);
			}
		}

		public override void FinalizePeer (IJavaPeerable value)
		{
			var h = value.PeerReference;
			var o = Runtime.ObjectReferenceManager;
			// MUST NOT use SafeHandle.ReferenceType: local refs are tied to a JniEnvironment
			// and the JniEnvironment's corresponding thread; it's a thread-local value.
			// Accessing SafeHandle.ReferenceType won't kill anything (so far...), but
			// instead it always returns JniReferenceType.Invalid.
			if (!h.IsValid || h.Type == JniObjectReferenceType.Local) {
				RemovePeer (value);
				value.SetPeerReference (new JniObjectReference ());
				value.Finalized ();
				return;
			}

			RemovePeer (value);
			if (o.LogGlobalReferenceMessages) {
				o.WriteGlobalReferenceLine ("Finalizing PeerReference={0} IdentityHashCode=0x{1} Instance=0x{2} Instance.Type={3}",
						h.ToString (),
						value.JniIdentityHashCode.ToString ("x"),
						RuntimeHelpers.GetHashCode (value).ToString ("x"),
						value.GetType ().ToString ());
			}
			value.SetPeerReference (new JniObjectReference ());
			JniObjectReference.Dispose (ref h);
			value.Finalized ();
		}

		public override List<JniSurfacedPeerInfo> GetSurfacedPeers ()
		{
			var registered = RegisteredInstances ?? throw new ObjectDisposedException (nameof (ManagedValueManager));
			lock (registered) {
				var peers = new List<JniSurfacedPeerInfo> (registered.Count);
				foreach (var e in registered) {
					foreach (var peer in e.Value)
						peers.Add (new JniSurfacedPeerInfo (e.Key, new WeakReference<IJavaPeerable> (peer)));
				}
				return peers;
			}
		}

		protected override void Dispose (bool disposing)
		{
			RegisteredInstances = null;
			base.Dispose (disposing);
		}
	}

	public class TestJvmTypeManager : JniRuntime.ReflectionJniTypeManager {

		const DynamicallyAccessedMemberTypes MethodsAndPrivateNested = DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes;

		IDictionary<string, Type>? typeMappings;

		public TestJvmTypeManager (IDictionary<string, Type>? typeMappings)
		{
			this.typeMappings = typeMappings;
		}

		protected override IEnumerable<Type> GetTypesForSimpleReference (string jniSimpleReference)
		{
			foreach (var type in base.GetTypesForSimpleReference (jniSimpleReference))
				yield return type;
			if (typeMappings != null && typeMappings.TryGetValue (jniSimpleReference, out var target))
				yield return target;
		}

		protected override IEnumerable<string> GetSimpleReferences (Type type)
		{
			return base.GetSimpleReferences (type)
				.Concat (CreateSimpleReferencesEnumerator (type));
		}

		IEnumerable<string> CreateSimpleReferencesEnumerator (Type type)
		{
			if (typeMappings == null)
				yield break;
			foreach (var e in typeMappings) {
				if (e.Value == type)
					yield return e.Key;
			}
		}

		public override void RegisterNativeMembers (
				JniType nativeClass,
				[DynamicallyAccessedMembers (MethodsAndPrivateNested)]
				Type type,
				ReadOnlySpan<char> methods)
		{
			if (TryRegisterNativeMembers (nativeClass, type, methods) || methods.IsEmpty)
				return;

			throw new NotSupportedException (
				$"Could not register native members for type '{type.FullName}'. " +
				"Ensure that the type has the appropriate [JniAddNativeMethodRegistration] attribute and static registration method.");
		}
	}
}
