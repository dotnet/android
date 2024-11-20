using Java.Interop;

namespace Android.App;

public sealed partial class InstrumentationAttribute
{
	string IJniNameProviderAttribute.Name => Name ?? "";
}
