using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Java.Interop.Tools.JavaCallableWrappers.CallableWrapperMembers;

public class CallableWrapperType
{
	public string Name { get; set; }
	public string Package { get; set; }
	public bool IsAbstract { get; set; }
	public string? ApplicationJavaClass { get; set; }
	public bool GenerateOnCreateOverrides { get; set; }
	/// <summary>
	/// The Java source code to be included in Instrumentation.onCreate
	///
	/// Originally came from MonoRuntimeProvider.java delimited by:
	/// // Mono Runtime Initialization {{{
	/// // }}}
	/// </summary>
	public string? MonoRuntimeInitialization { get; set; }
	public string? ExtendsType { get; set; }
	public CallableWrapperApplicationConstructor? ApplicationConstructor { get; set; }
	public bool IsApplication { get; set; }
	public bool IsInstrumentation { get; set; }
	public string PartialAssemblyQualifiedName { get; set; }
	public bool HasExport { get; set; }

	public List<CallableWrapperTypeAnnotation> Annotations { get; } = new List<CallableWrapperTypeAnnotation> ();
	public List<string> ImplementedInterfaces { get; } = new List<string> ();
	public List<CallableWrapperConstructor> Constructors { get; } = new List<CallableWrapperConstructor> ();
	public List<CallableWrapperField> Fields { get; } = new List<CallableWrapperField> ();
	public List<CallableWrapperMethod> Methods { get; } = new List<CallableWrapperMethod> ();
	public List<CallableWrapperType> NestedTypes { get; } = new List<CallableWrapperType> ();

	public bool CannotRegisterInStaticConstructor => IsApplication || IsInstrumentation;

	public CallableWrapperType (string name, string package, string partialAssemblyQualifiedName)
	{
		Name = name;
		Package = package;
		PartialAssemblyQualifiedName = partialAssemblyQualifiedName;
	}

	// example of java target to generate for a type
	//
	// package mono.droid;
	//
	// import android.app.Activity;
	// import android.os.Bundle;
	//
	// public class MonoActivity extends android.app.Activity
	// {
	// 	  static final String __md_methods;
	// 	  static {
	// 	    __md_methods =
	// 	      "n_OnCreate:(Landroid/os/Bundle;)V:GetOnCreate_Landroid_os_Bundle_Handler\n" +
	// 	      "";
	// 	    mono.android.Runtime.register ("Mono.Droid.MonoActivity, AssemblyName", MonoActivity.class, __md_methods);
	// 	  }
	//
	//    public void onCreate(android.os.Bundle savedInstanceState)
	//    {
	//      n_onCreate (savedInstanceState);
	//    }
	//
	//    private native void n_onCreate (android.os.Bundle bundle);
	// }
	public void Generate (TextWriter writer, CallableWrapperWriterOptions options, bool isNested = false)
	{
		if (!isNested && !string.IsNullOrEmpty (Package)) {
			writer.WriteLine ("package " + Package + ";");
			writer.WriteLine ();
		}

		GenerateHeader (writer);

		if (!isNested)
			GenerateInfrastructure (writer);

		GenerateBody (writer, options);

		foreach (var nested in NestedTypes)
			nested.Generate (writer, options, true);

		GenerateFooter (writer);
	}

	public void Generate (TextWriter writer)
	{
		Generate (writer, new CallableWrapperWriterOptions ());
	}

	void GenerateHeader (TextWriter sw)
	{
		sw.WriteLine ();

		// Type annotations
		foreach (var annotation in Annotations)
			annotation.Generate (sw, "");

		sw.WriteLine ("public " + (IsAbstract ? "abstract " : "") + "class " + Name);

		var extends = ExtendsType;

		// Do this check here rather than the constructor because it can be set after the constructor is called
		if (extends == "android.app.Application" && ApplicationJavaClass != null && !string.IsNullOrEmpty (ApplicationJavaClass))
			extends = ApplicationJavaClass;

		sw.WriteLine ("\textends " + extends);

		sw.WriteLine ("\timplements");
		sw.Write ("\t\t");

		sw.Write ("mono.android.IGCUserPeer");

		foreach (var iface in ImplementedInterfaces) {
			sw.WriteLine (",");
			sw.Write ("\t\t");
			sw.Write (iface);
		}

		sw.WriteLine ();
		sw.WriteLine ("{");
	}

	void GenerateInfrastructure (TextWriter writer)
	{
		var needCtor = false;

		if (HasDynamicallyRegisteredMethods) {
			needCtor = true;
			writer.WriteLine ("/** @hide */");
			writer.WriteLine ("\tpublic static final String __md_methods;");
		}

		for (var i = 0; i < NestedTypes.Count; i++) {
			if (!NestedTypes [i].HasDynamicallyRegisteredMethods)
				continue;

			needCtor = true;
			writer.Write ("\tstatic final String __md_");
			writer.Write (i + 1);
			writer.WriteLine ("_methods;");
		}

		if (needCtor) {
			writer.WriteLine ("\tstatic {");

			if (HasDynamicallyRegisteredMethods)
				GenerateRegisterType (writer, this, "__md_methods");

			for (var i = 0; i < NestedTypes.Count; ++i)
				GenerateRegisterType (writer, NestedTypes [i], $"__md_{i + 1}_methods");

			writer.WriteLine ("\t}");
		}
	}

	void GenerateBody (TextWriter sw, CallableWrapperWriterOptions options)
	{
		// Legacy options belong only at the virtual extension boundary, including null and custom state.
		foreach (var ctor in Constructors)
			ctor.Generate (sw, options);

		ApplicationConstructor?.Generate (sw);

		foreach (var field in Fields)
			field.Generate (sw);

		foreach (var method in Methods)
			method.Generate (sw, options);

		if (GenerateOnCreateOverrides && IsApplication && !Methods.Any (m => m.Name == "onCreate"))
			WriteApplicationOnCreate (sw);

		if (GenerateOnCreateOverrides && IsInstrumentation && !Methods.Any (m => m.Name == "onCreate"))
			WriteInstrumentationOnCreate (sw);

		sw.WriteLine ();
		sw.WriteLine ("\tprivate java.util.ArrayList refList;");

		sw.WriteLine ("\tpublic void monodroidAddReference (java.lang.Object obj)");
		sw.WriteLine ("\t{");
		sw.WriteLine ("\t\tif (refList == null)");
		sw.WriteLine ("\t\t\trefList = new java.util.ArrayList ();");
		sw.WriteLine ("\t\trefList.add (obj);");
		sw.WriteLine ("\t}");
		sw.WriteLine ();

		sw.WriteLine ("\tpublic void monodroidClearReferences ()");
		sw.WriteLine ("\t{");
		sw.WriteLine ("\t\tif (refList != null)");
		sw.WriteLine ("\t\t\trefList.clear ();");
		sw.WriteLine ("\t}");
	}

	void GenerateFooter (TextWriter sw)
	{
		sw.WriteLine ("}");
	}

	void WriteApplicationOnCreate (TextWriter sw)
	{
		sw.WriteLine ();

		sw.WriteLine ("\tpublic void onCreate ()");
		sw.WriteLine ("\t{");

		sw.Write ("\t\tmono.android.Runtime.register (\"");
		sw.Write (PartialAssemblyQualifiedName);
		sw.Write ("\", ");
		sw.Write (Name);
		sw.WriteLine (".class, __md_methods);");

		sw.WriteLine ("\t\tsuper.onCreate ();");
		sw.WriteLine ("\t}");
	}

	void WriteInstrumentationOnCreate (TextWriter sw)
	{
		sw.WriteLine ();
		sw.WriteLine ("\tpublic void onCreate (android.os.Bundle arguments)");
		sw.WriteLine ("\t{");

#if MONODROID_TIMING
		sw.WriteLine ("\t\tandroid.util.Log.i(\"MonoDroid-Timing\", \"{0}.onCreate(Bundle): time: \"+java.lang.System.currentTimeMillis());", Name);
		sw.WriteLine ();
#endif

		sw.WriteLine ("\t\tandroid.content.Context context = getContext ();");
		sw.WriteLine ();

		if (!string.IsNullOrEmpty (MonoRuntimeInitialization)) {
			sw.WriteLine (MonoRuntimeInitialization);
			sw.WriteLine ();
		}

		sw.Write ("\t\tmono.android.Runtime.register (\"");
		sw.Write (PartialAssemblyQualifiedName);
		sw.Write ("\", ");
		sw.Write (Name);
		sw.WriteLine (".class, __md_methods);");

		sw.WriteLine ("\t\tsuper.onCreate (arguments);");
		sw.WriteLine ("\t}");
	}

	void GenerateRegisterType (TextWriter sw, CallableWrapperType self, string field)
	{
		if (!self.HasDynamicallyRegisteredMethods)
			return;

		sw.Write ("\t\t");
		sw.Write (field);
		sw.WriteLine (" = ");

		foreach (var method in self.Methods) {
			if (method.IsDynamicallyRegistered) {
				sw.Write ("\t\t\t\"", method.Method);
				sw.Write (method.Method);
				sw.WriteLine ("\\n\" +");
			}
		}

		sw.WriteLine ("\t\t\t\"\";");

		if (CannotRegisterInStaticConstructor)
			return;

		sw.Write ("\t\t");

		sw.Write ("mono.android.Runtime.register (\"");
		sw.Write (self.PartialAssemblyQualifiedName);
		sw.Write ("\", ");
		sw.Write (self.Name);
		sw.Write (".class, ");
		sw.Write (field);
		sw.WriteLine (");");
	}

	// If there are no methods, we need to generate "empty" registration because of backward compatibility
	public bool HasDynamicallyRegisteredMethods => Methods.Count == 0 || Methods.Any (sig => sig.IsDynamicallyRegistered);

	/// <summary>
	/// Returns a destination file path based on the package name of this Java type
	/// </summary>
	public string GetDestinationPath (string outputPath)
	{
		var dir = Package.Replace ('.', Path.DirectorySeparatorChar);
		return Path.Combine (outputPath, dir, Name + ".java");
	}

	public void Generate (string outputPath, CallableWrapperWriterOptions options)
	{
		using (StreamWriter sw = OpenStream (outputPath))
			Generate (sw, options);
	}

	public void Generate (string outputPath)
	{
		using (StreamWriter sw = OpenStream (outputPath))
			Generate (sw);
	}

	StreamWriter OpenStream (string outputPath)
	{
		var destination = GetDestinationPath (outputPath);
		Directory.CreateDirectory (Path.GetDirectoryName (destination)!);

		return new StreamWriter (new FileStream (destination, FileMode.Create, FileAccess.Write));
	}
}
