using Java.Interop;

using Mono.Options;

bool showHelp = false;

var jreOptions = new JreRuntimeOptions {
};

var options = new OptionSet {
	"Using the JVM from C#!",
	"",
	"Options:",
	{ "jvm=",
	  $"{{PATH}} to JVM to use.",
	  v => jreOptions.JvmLibraryPath = v },
	{ "cp=|classpath",
	  $"Add {{JAR-OR-DIRECTORY}} to JVM classpath.",
	  v => jreOptions.ClassPath.Add (v)},
	{ "J=",
	  $"Pass the specified option to the JVM.",
	  v => jreOptions.AddOption (v) },
	{ "h|help",
	  "Show this message and exit.",
	  v => showHelp = v != null },
};
options.Parse (args);

if (showHelp) {
	options.WriteOptionDescriptions (Console.Out);
	return;
}

if (string.IsNullOrEmpty (jreOptions.JvmLibraryPath) || !File.Exists (jreOptions.JvmLibraryPath)) {
	Error ("Option -jvm=PATH is required.  PATH is a full path to the JVM native library to use, e.g. `libjli.dylib`.");
	return;
}

var jre = jreOptions.CreateJreVM ();

// We now have a JVM!
// The current thread is implicitly attached to the JVM.
// Access of `JniEnvironment` members on other threads will implicitly attach those threads to the JVM.

//
// Useful background info: the JNI documentation! https://docs.oracle.com/javase/8/docs/technotes/guides/jni/spec/functions.html
//

var Object_class = JniEnvironment.Types.FindClass ("java/lang/Object");
Console.WriteLine ($"Object_class={Object_class}");
var Object_ctor  = JniEnvironment.InstanceMethods.GetMethodID (Object_class, "<init>", "()V");
var Object_val   = JniEnvironment.Object.NewObject (Object_class, Object_ctor);

Console.WriteLine ($"Object_val={Object_val}");

// Invoke `Object.toString()`
var Object_toString = JniEnvironment.InstanceMethods.GetMethodID (Object_class, "toString", "()Ljava/lang/String;");
unsafe {
	var Object_desc     = JniEnvironment.InstanceMethods.CallObjectMethod (Object_val, Object_toString, null);
	Console.WriteLine ($"Object_val.toString()={JniEnvironment.Strings.ToString (Object_desc)}");

	// When JNI returns a `jobject` or `jclass` value, JNI returns a *JNI Object Reference*.
	// The `JniObjectReference` struct is used to store JNI Local, Global, and Weak Global references.
	//
	// When an object reference is no longer required, it should be explicitly deleted.

	JniObjectReference.Dispose (ref Object_desc);
}

JniObjectReference.Dispose (ref Object_class);
JniObjectReference.Dispose (ref Object_val);

// There are some OO wrappers over the core `JniEnvironment` members.  `JniType` is useful.
var Object_type = new JniType ("java/lang/Object");
var Object_ctor2 = Object_type.GetConstructor ("()V");

unsafe {
	var Object_val2 = Object_type.NewObject (Object_ctor2, null);
	var Object_desc = JniEnvironment.InstanceMethods.CallObjectMethod (Object_val2, Object_toString, null);
	Console.WriteLine ($"Object_val.toString()={JniEnvironment.Strings.ToString (Object_desc)}");
}

void Error (string message)
{
	var app = Path.GetFileNameWithoutExtension (Environment.GetCommandLineArgs ()[0]);
	Console.Error.WriteLine ($"{app}: {message}");
}
