namespace MonoDroid.Generation;

public interface IRequireGenericMarshal
{
	bool MayHaveManagedGenericArguments { get; }
	string GetGenericJavaObjectTypeOverride ();
	string ToInteroperableJavaObject (string varname);
}

