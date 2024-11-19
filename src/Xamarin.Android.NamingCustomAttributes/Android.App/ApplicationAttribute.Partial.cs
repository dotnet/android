using Java.Interop;

namespace Android.App;

public sealed partial class ApplicationAttribute
{
	string IJniNameProviderAttribute.Name => Name ?? "";
}
