using System.Linq;

namespace Java.Lang.Invoke
{
#if ANDROID_34
	// A new interface (Java.Lang.Invoke.ITypeDescriptor.IOfMethod) was added to the MethodType class in API-34.
	// The existing methods have covariant return types so they cannot fulfill the interface contract,
	// and we cannot change them without breaking API. Create new versions of these interface
	// methods that can fulfill the contract.
	public sealed partial class MethodType
	{
		Java.Lang.Object[]? ITypeDescriptor.IOfMethod.ParameterArray ()
			=> ParameterArray ();

		System.Collections.IList? ITypeDescriptor.IOfMethod.ParameterList ()
			=> (Android.Runtime.JavaList<Java.Lang.Class>?)ParameterList ();

		// Helper method needed to cast Object to Class for an explicitly implemented method:
		// Java.Lang.Invoke.ITypeDescriptor.IOfMethod.InsertParameterTypes (int p0, params Java.Lang.Object[]? p1)
		Java.Lang.Invoke.MethodType? InsertParameterTypes (int num, params Java.Lang.Object []? ptypesToInsert)
			=> InsertParameterTypes (num, ptypesToInsert?.Cast<Java.Lang.Class> ().ToArray ());
	}
#endif
}
