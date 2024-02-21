using System;
using System.Collections.Generic;
using System.IO;

namespace Java.Interop.Tools.JavaCallableWrappers.CallableWrapperMembers;

public class CallableWrapperMethod
{
	public string Name { get; set; }
	public string Method { get; set; }
	public string JniSignature { get; set; }
	public string? ManagedParameters { get; set; }
	public string? JavaNameOverride { get; set; }
	public string? Params { get; set; }
	public string? Retval { get; set; }
	public string? JavaAccess { get; set; }
	public bool IsExport { get; set; }
	public bool IsStatic { get; set; }
	public bool IsDynamicallyRegistered { get; set; } = true;
	public string []? ThrownTypeNames { get; set; }
	public string? SuperCall { get; set; }
	public string? ActivateCall { get; set; }
	public string JavaName => JavaNameOverride ?? Name;
	public List<CallableWrapperTypeAnnotation> Annotations { get; } = new List<CallableWrapperTypeAnnotation> ();
	public CallableWrapperType DeclaringType { get; }

	public string? ThrowsDeclaration => ThrownTypeNames?.Length > 0 ? $" throws {string.Join (", ", ThrownTypeNames)}" : null;

	public CallableWrapperMethod (CallableWrapperType declaringType, string name, string method, string jniSignature)
	{
		DeclaringType = declaringType;
		Name = name;
		Method = method;
		JniSignature = jniSignature;
	}

	public virtual void Generate (TextWriter sw, CallableWrapperWriterOptions options)
	{
		sw.WriteLine ();

		foreach (var annotation in Annotations)
			annotation.Generate (sw, "", options);

		sw.Write ("\t");

		sw.Write (IsExport ? JavaAccess : "public");
		sw.Write (' ');

		if (IsStatic)
			sw.Write ("static ");

		sw.Write (Retval);
		sw.Write (' ');

		sw.Write (JavaName);

		sw.Write (" (");
		sw.Write (Params);
		sw.Write (')');

		sw.WriteLine (ThrowsDeclaration);
		sw.WriteLine ("\t{");

#if MONODROID_TIMING
		sw.WriteLine ("\t\tandroid.util.Log.i(\"MonoDroid-Timing\", \"{0}.{1}: time: \"+java.lang.System.currentTimeMillis());", Name, Method);
#endif

		sw.Write ("\t\t");
		sw.Write (Retval == "void" ? string.Empty : "return ");

		sw.Write ("n_");
		sw.Write (Name);

		sw.Write (" (");
		sw.Write (ActivateCall);
		sw.WriteLine (");");

		sw.WriteLine ("\t}");
		sw.WriteLine ();

		sw.Write ("\tprivate ");

		if (IsStatic)
			sw.Write ("static ");

		sw.Write ("native ");

		sw.Write (Retval);

		sw.Write (" n_");
		sw.Write (Name);

		sw.Write (" (");
		sw.Write (Params);
		sw.WriteLine (");");
	}
}
