using System.IO;

namespace Java.Interop.Tools.JavaCallableWrappers.CallableWrapperMembers;

public class CallableWrapperApplicationConstructor
{
	public string Name { get; set; }

	public CallableWrapperApplicationConstructor (string name)
	{
		Name = name;
	}

	public void Generate (TextWriter sw, CallableWrapperWriterOptions options)
	{
		sw.WriteLine ();

		sw.Write ("\tpublic ");
		sw.Write (Name);
		sw.WriteLine (" ()");

		sw.WriteLine ("\t{");
		sw.WriteLine ("\t\tmono.MonoPackageManager.setContext (this);");
		sw.WriteLine ("\t}");
	}
}
