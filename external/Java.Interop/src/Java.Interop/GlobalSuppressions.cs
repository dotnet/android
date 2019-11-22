using System.Diagnostics.CodeAnalysis;

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.
[assembly: SuppressMessage ("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "JniEnvironmentInfo.Runtime is IDisposable, but JniRuntime will dispose JniEnvironmentInfo.", Scope = "type", Target = "~T:Java.Interop.JniPeerMembers.JniInstanceMethods")]

[assembly: SuppressMessage ("Design", "CA1008:Enums should have zero value", Justification = "No thanks", Scope = "type", Target = "~T:Java.Interop.JniVersion")]

[assembly: SuppressMessage ("Design", "CA1030:Use events where appropriate", Justification = "This isn't 'raising' an event; it's 'raising' a pending exception within the JVM.", Scope = "member", Target = "~M:Java.Interop.JniRuntime.RaisePendingException(System.Exception)")]

[assembly: SuppressMessage ("Design", "CA1024:Use properties where appropriate", Justification = "<Pending>", Scope = "member", Target = "~M:Java.Interop.JniRuntime.GetRegisteredRuntimes()")]

[assembly: SuppressMessage ("Design", "CA1032:Implement standard exception constructors", Justification = "System.Runtime.Serialization.SerializationInfo doesn't exist in our targeted PCL profile, so we can't provide the (SerializationInfo, StreamingContext) constructor.", Scope = "type", Target = "~T:Java.Interop.JavaProxyThrowable")]
[assembly: SuppressMessage ("Design", "CA1032:Implement standard exception constructors", Justification = "System.Runtime.Serialization.SerializationInfo doesn't exist in our targeted PCL profile, so we can't provide the (SerializationInfo, StreamingContext) constructor.", Scope = "type", Target = "~T:Java.Interop.JniLocationException")]

// See: 045b8af7, 6a42bb89, f60906cf, e10f7cb0, etc.
[assembly: SuppressMessage ("Design", "CA1034:Nested types should not be visible", Justification = "Deliberate choice to 'hide' these types from code completion for `Java.Interop.`.", Scope = "type", Target = "~T:Java.Interop.JniEnvironment.Exceptions")]
[assembly: SuppressMessage ("Design", "CA1034:Nested types should not be visible", Justification = "Deliberate choice to 'hide' these types from code completion for `Java.Interop.`.", Scope = "type", Target = "~T:Java.Interop.JniPeerMembers.JniStaticMethods")]
[assembly: SuppressMessage ("Design", "CA1034:Nested types should not be visible", Justification = "Deliberate choice to 'hide' these types from code completion for `Java.Interop.`.", Scope = "type", Target = "~T:Java.Interop.JniRuntime.JniMarshalMemberBuilder")]
[assembly: SuppressMessage ("Design", "CA1034:Nested types should not be visible", Justification = "Deliberate choice to 'hide' these types from code completion for `Java.Interop.`.", Scope = "type", Target = "~T:Java.Interop.JniPeerMembers.JniStaticFields")]
[assembly: SuppressMessage ("Design", "CA1034:Nested types should not be visible", Justification = "Deliberate choice to 'hide' these types from code completion for `Java.Interop.`.", Scope = "type", Target = "~T:Java.Interop.JniRuntime.JniValueManager")]
[assembly: SuppressMessage ("Design", "CA1034:Nested types should not be visible", Justification = "Deliberate choice to 'hide' these types from code completion for `Java.Interop.`.", Scope = "type", Target = "~T:Java.Interop.JniEnvironment.References")]
[assembly: SuppressMessage ("Design", "CA1034:Nested types should not be visible", Justification = "Deliberate choice to 'hide' these types from code completion for `Java.Interop.`.", Scope = "type", Target = "~T:Java.Interop.JniRuntime.JniTypeManager")]
[assembly: SuppressMessage ("Design", "CA1034:Nested types should not be visible", Justification = "Deliberate choice to 'hide' these types from code completion for `Java.Interop.`.", Scope = "type", Target = "~T:Java.Interop.JniPeerMembers.JniInstanceMethods")]
[assembly: SuppressMessage ("Design", "CA1034:Nested types should not be visible", Justification = "Deliberate choice to 'hide' these types from code completion for `Java.Interop.`.", Scope = "type", Target = "~T:Java.Interop.JniPeerMembers.JniInstanceFields")]
[assembly: SuppressMessage ("Design", "CA1034:Nested types should not be visible", Justification = "Deliberate choice to 'hide' these types from code completion for `Java.Interop.`.", Scope = "type", Target = "~T:Java.Interop.JniRuntime.CreationOptions")]
[assembly: SuppressMessage ("Design", "CA1034:Nested types should not be visible", Justification = "Deliberate choice to 'hide' these types from code completion for `Java.Interop.`.", Scope = "type", Target = "~T:Java.Interop.JniRuntime.JniObjectReferenceManager")]
[assembly: SuppressMessage ("Design", "CA1034:Nested types should not be visible", Justification = "Deliberate choice to 'hide' these types from code completion for `Java.Interop.`.", Scope = "type", Target = "~T:Java.Interop.JniEnvironment.Monitors")]
[assembly: SuppressMessage ("Design", "CA1034:Nested types should not be visible", Justification = "Deliberate choice to 'hide' these types from code completion for `Java.Interop.`.", Scope = "type", Target = "~T:Java.Interop.JniEnvironment.Object")]
[assembly: SuppressMessage ("Design", "CA1034:Nested types should not be visible", Justification = "Deliberate choice to 'hide' these types from code completion for `Java.Interop.`.", Scope = "type", Target = "~T:Java.Interop.JniEnvironment.Strings")]
[assembly: SuppressMessage ("Design", "CA1034:Nested types should not be visible", Justification = "Deliberate choice to 'hide' these types from code completion for `Java.Interop.`.", Scope = "type", Target = "~T:Java.Interop.JniEnvironment.Types")]

[assembly: SuppressMessage ("Design", "CA1051:Do not declare visible instance fields", Justification = "This type is passed to native code, and should use fields, not properties.", Scope = "member", Target = "~F:Java.Interop.JniNativeMethodRegistration.Name")]
[assembly: SuppressMessage ("Design", "CA1051:Do not declare visible instance fields", Justification = "This type is passed to native code, and should use fields, not properties.", Scope = "member", Target = "~F:Java.Interop.JniNativeMethodRegistration.Signature")]
[assembly: SuppressMessage ("Design", "CA1051:Do not declare visible instance fields", Justification = "This type is passed to native code, and should use fields, not properties.", Scope = "member", Target = "~F:Java.Interop.JniNativeMethodRegistration.Marshaler")]

[assembly: SuppressMessage ("Design", "CA1064:Exceptions should be public", Justification = "<Pending>", Scope = "type", Target = "~T:Java.Interop.JniLocationException")]

[assembly: SuppressMessage ("Naming", "CA1707:Identifiers should not contain underscores", Justification = "<Pending>", Scope = "type", Target = "~T:Java.Interop.JniVersion")]
[assembly: SuppressMessage ("Naming", "CA1707:Identifiers should not contain underscores", Justification = "<Pending>", Scope = "member", Target = "~P:Java.Interop.JniRuntime.CreationOptions.ClassLoader_LoadClass_id")]

[assembly: SuppressMessage ("Naming", "CA1710:Identifiers should have correct suffix", Justification = "These represent Java arrays, not collections", Scope = "type", Target = "~T:Java.Interop.JavaObjectArray`1")]
[assembly: SuppressMessage ("Naming", "CA1710:Identifiers should have correct suffix", Justification = "These represent Java arrays, not collections", Scope = "type", Target = "~T:Java.Interop.JavaArray`1")]
[assembly: SuppressMessage ("Naming", "CA1710:Identifiers should have correct suffix", Justification = "These represent Java arrays, not collections", Scope = "type", Target = "~T:Java.Interop.JavaPrimitiveArray`1")]
[assembly: SuppressMessage ("Naming", "CA1710:Identifiers should have correct suffix", Justification = "These represent Java arrays, not collections", Scope = "type", Target = "~T:Java.Interop.JavaBooleanArray")]
[assembly: SuppressMessage ("Naming", "CA1710:Identifiers should have correct suffix", Justification = "These represent Java arrays, not collections", Scope = "type", Target = "~T:Java.Interop.JavaSByteArray")]
[assembly: SuppressMessage ("Naming", "CA1710:Identifiers should have correct suffix", Justification = "These represent Java arrays, not collections", Scope = "type", Target = "~T:Java.Interop.JavaCharArray")]
[assembly: SuppressMessage ("Naming", "CA1710:Identifiers should have correct suffix", Justification = "These represent Java arrays, not collections", Scope = "type", Target = "~T:Java.Interop.JavaInt16Array")]
[assembly: SuppressMessage ("Naming", "CA1710:Identifiers should have correct suffix", Justification = "These represent Java arrays, not collections", Scope = "type", Target = "~T:Java.Interop.JavaInt32Array")]
[assembly: SuppressMessage ("Naming", "CA1710:Identifiers should have correct suffix", Justification = "These represent Java arrays, not collections", Scope = "type", Target = "~T:Java.Interop.JavaInt64Array")]
[assembly: SuppressMessage ("Naming", "CA1710:Identifiers should have correct suffix", Justification = "These represent Java arrays, not collections", Scope = "type", Target = "~T:Java.Interop.JavaSingleArray")]
[assembly: SuppressMessage ("Naming", "CA1710:Identifiers should have correct suffix", Justification = "These represent Java arrays, not collections", Scope = "type", Target = "~T:Java.Interop.JavaDoubleArray")]

[assembly: SuppressMessage ("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Can't break API", Scope = "member", Target = "~M:Java.Interop.JniEnvironment.Exceptions.ThrowNew(Java.Interop.JniObjectReference,System.String)")]

[assembly: SuppressMessage ("Naming", "CA1716:Identifiers should not match keywords", Justification = "'Object' is needed for 'Java.Lang.Object'", Scope = "type", Target = "~T:Java.Interop.JniEnvironment.Object")]

[assembly: SuppressMessage ("Naming", "CA1720:Identifiers should not contain type names", Justification = "'Object' is needed for 'Java.Lang.Object'", Scope = "type", Target = "~T:Java.Interop.JniEnvironment.Object")]

[assembly: SuppressMessage ("Usage", "CA1801:Review unused parameters", Justification = "Used in DEBUG configuration", Scope = "member", Target = "~M:Java.Interop.JniMethodInfo.#ctor(System.String,System.String,System.IntPtr,System.Boolean)")]
[assembly: SuppressMessage ("Usage", "CA1801:Review unused parameters", Justification = "Used in DEBUG configuration", Scope = "member", Target = "~M:Java.Interop.JniFieldInfo.#ctor(System.String,System.String,System.IntPtr,System.Boolean)")]

[assembly: SuppressMessage ("Performance", "CA1813:Avoid unsealed attributes", Justification = "Can't break public API", Scope = "type", Target = "~T:Java.Interop.JniValueMarshalerAttribute")]

[assembly: SuppressMessage ("Performance", "CA1815:Override equals and operator equals on value types", Justification = "<Pending>", Scope = "type", Target = "~T:Java.Interop.JniNativeMethodRegistration")]
[assembly: SuppressMessage ("Performance", "CA1815:Override equals and operator equals on value types", Justification = "<Pending>", Scope = "type", Target = "~T:Java.Interop.JniNativeMethodRegistrationArguments")]
[assembly: SuppressMessage ("Performance", "CA1815:Override equals and operator equals on value types", Justification = "<Pending>", Scope = "type", Target = "~T:Java.Interop.JniTransition")]

[assembly: SuppressMessage ("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:Java.Interop.JniRuntime.JniMarshalMemberBuilder.IsDirectMethod(System.Reflection.ParameterInfo[])~System.Boolean")]
[assembly: SuppressMessage ("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:Java.Interop.JniRuntime.JniValueManager.GetJniIdentityHashCode(Java.Interop.JniObjectReference)~System.Int32")]

[assembly: SuppressMessage ("Performance", "CA1823:Avoid unused private fields", Justification = "Used for native interop", Scope = "type", Target = "~T:Java.Interop.JavaException")]
[assembly: SuppressMessage ("Performance", "CA1823:Avoid unused private fields", Justification = "Used for native interop", Scope = "type", Target = "~T:Java.Interop.JavaObject")]
[assembly: SuppressMessage ("Performance", "CA1823:Avoid unused private fields", Justification = "Used for native interop", Scope = "type", Target = "~T:Java.Interop.JniRuntime")]

[assembly: SuppressMessage ("Reliability", "CA2000:Dispose objects before losing scope", Justification = "We don't *want* to dispose the value!", Scope = "member", Target = "~M:Java.Interop.JniEnvironment.Exceptions.Throw(System.Exception)")]
[assembly: SuppressMessage ("Reliability", "CA2000:Dispose objects before losing scope", Justification = "We don't *want* to dispose the value!", Scope = "member", Target = "~M:Java.Interop.JniRuntime.UnTrack(System.IntPtr)")]
[assembly: SuppressMessage ("Reliability", "CA2000:Dispose objects before losing scope", Justification = "We don't *want* to dispose the value!", Scope = "member", Target = "~M:Java.Interop.ProxyValueMarshaler.CreateGenericObjectReferenceArgumentState(System.Object,System.Reflection.ParameterAttributes)~Java.Interop.JniValueMarshalerState")]
[assembly: SuppressMessage ("Reliability", "CA2000:Dispose objects before losing scope", Justification = "We don't *want* to dispose the value!", Scope = "member", Target = "~M:Java.Interop.ManagedPeer.RegisterNativeMembers(System.IntPtr,System.IntPtr,System.IntPtr,System.IntPtr,System.IntPtr)")]
[assembly: SuppressMessage ("Reliability", "CA2000:Dispose objects before losing scope", Justification = "We don't *want* to dispose the value!", Scope = "member", Target = "~M:Java.Interop.JniRuntime.#ctor(Java.Interop.JniRuntime.CreationOptions)")]

[assembly: SuppressMessage ("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "<Pending>", Scope = "member", Target = "~M:Java.Interop.JniEnvironment.Exceptions.Throw(Java.Interop.JniObjectReference)")]
[assembly: SuppressMessage ("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "<Pending>", Scope = "member", Target = "~M:Java.Interop.JniEnvironment.Exceptions.ThrowNew(Java.Interop.JniObjectReference,System.String)")]
