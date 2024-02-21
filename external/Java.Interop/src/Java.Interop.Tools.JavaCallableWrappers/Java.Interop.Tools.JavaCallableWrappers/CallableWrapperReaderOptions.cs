namespace Java.Interop.Tools.JavaCallableWrappers;

public class CallableWrapperReaderOptions
{
	public string? DefaultApplicationJavaClass { get; set; }
	public bool DefaultGenerateOnCreateOverrides { get; set; }
	public string? DefaultMonoRuntimeInitialization { get; set; }
	public JavaCallableMethodClassifier? MethodClassifier { get; set; }
}
