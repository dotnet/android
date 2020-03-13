# `Mono.Android.dll` API Compatibility

[Historically](#history), there was a separate `Mono.Android.dll` for each
`$(TargetFrameworkVersion)` value, and these assemblies were *not* completely
API compatible with each other.

Starting with `$(TargetFrameworkVersion)` v10.0, we will no longer provide a
separate `Mono.Android.dll` per API level.  Instead, all *stable* bindings of
future API levels will reside in the same `Mono.Android.dll`.

This "single" `Mono.Android.dll` will require C# 8 features.


# Preserving Compatibility

Java and C# have different ideas about what constitutes a change which is
source- and binary-compatible.  Consequently, the `Mono.Android.dll` binding
needs to do extra work to bridge these gaps.


## Added Required Interface Methods

It is an *binary* compatible change to add non-`default` methods to a Java
interface.  `Mono.Android.dll` will support this by using C#8 default
interface methods which try to invoke the underlying Java method or
throw `Java.Lang.AbstractMethodError()`.

For example, given Java API-*X*:

```java
package example;

public interface Fooable {
	void foo();
}
```

which is then updated for API-*Y*:

```java
package example;

public interface Fooable {
	void foo();
	void bar();
}
```

Then the API-*Y* binding will be:

```csharp
namespace Example {
	public partial interface IFooable : IJavaObject, IJavaPeerable {
		[Register ("foo", "()V", "…")]
		void Foo();

		[Register ("bar", "()V", "…")]
		void Bar()
		{
			const string __id = "bar.()V";
			try {
				var __rm = _members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, null);
			}
			catch (Java.Lang.NoSuchMethodError e) {
				throw new Java.Lang.AbstractMethodError (__id);
			}
		}
	}
}
```

Question: will checking for `NoSuchMethodError` actually work with
new required interface methods?


<a name="added-abstract-methods">

## Added Abstract Methods

When a Java class adds a new `abstract` method, the binding needs to alter
the method to instead be `virtual`, with an implementation which eventually
throws `AbstractMethodError`:

```java
package example;

public abstract class Foo {
	// Abstract in API-X
	public abstract void a();

	// Added in API-Y
	public abstract void b();
}
```

will be bound as:

```csharp
namespace Example {
	public abstract partial class Foo {
		public abstract void A();

		public virtual void B()
		{
			const string __id = "b.()V";
			try {
				// Works on API levels which (1) declare the method, and
				// (2) the method is overridden in the runtime type of `this`.
				var __rm = _members.InstanceMethods.InvokeVirtuaVoidMethod (__id, this, null);
			}
			catch (Java.Lang.NoSuchMethodError e) {
				// Triggered on API levels which don't contain the method
				throw new Java.Lang.AbstractMethodError (__id);
			}
		}
	}
}
```


## Added Covariant Return Types

When a base class adds a method which is overridden in a derived class,
Java may make use of covariant return types.  For example, in API-29
the [`android.telephony.CellInfo`][cellinfo] and
[`android.telephony.CellInfoCdma`][celllinfocdma] types:

[cellinfo]: https://developer.android.com/reference/android/telephony/CellInfo
[celllinfocdma]: https://developer.android.com/reference/android/telephony/CellInfoCdma

```java
package android.telephony;

public abstract class CellInfo {
}

public class CellInfoCdma extends CellInfo {
	public CellIdentityCdma getCellIdentity() {…}
}

public abstract class CellIdentity {…}
public class CellIdentityCdma extends CellIdentity {…}
```

is bound as:

```csharp
namespace Android.Telephony {
	public abstract partial class CellInfo {
	}

	public sealed partial class CellInfoCdma : CellInfo {
		public CellIdentityCdma CellIdentity {get;}
	}

	public abstract partial class CellIdentity {}
	public partial class CellIdentityCdma : CellIdentity {}
}
```

In [API-R Developer Preview 1][cellinfo-r-dp1], `CellInfo` is updated:

[cellinfo-r-dp1]: https://developer.android.com/sdk/api_diff/r-dp1/changes/android.telephony.CellInfo

```java
package android.telephony;

public class CellInfo {
	public abstract CellIdentity getCellIdentity();
}
```

Note that this is a new `abstract` method.  As per the
[Added Abstract Methods](#added-abstract-methods) section,
`CellInfo.getCellIdentity()` will need to be bound as a *`virtual`* property:

```csharp
namespace Android.Telephony {
	// API-R binding, take 1
	public abstract partial class CellInfo {
		public virtual CellIdentity CellIdentity {
			get => …;
		}
	}
}
```

However, this is incomplete, for two reasons:

 1. `CellInfoCdma.CellIdentity` will issue a CS0114 warning, as it hides the
    added `CellInfo.CellIdentity` property.  Note that we cannot change the
    type of `CellInfoCdma.CellIdentity`, as that would break the C# API.

 2. At runtime, the expectation is that invoking the `CellInfo.CellIdentity`
    property on a `CellInfoCdma` instance *won't* throw `AbstractMethodError`,
    as it is "overridden" in the Java-side `CellInfoCdma`.

(2) is implicitly handled, if you squint "just right", by having the binding
of `CellInfo.CellIdentity` invoke the Java-side method.  If the runtime type
is e.g. `CellInfoCdma`, then this will hit `CellInfoCdma.getCellIdentity()`,
which will return the same instance as the `CellInfo.CellIdentity` property.

```csharp
namespace Android.Telephony {
	// API-R binding, final
	public abstract partial class CellInfo {
		public virtual CellIdentity CellIdentity {
			get {
				const string __id = "getCellIdentity.()Landroid/telephony/CellIdentity;";
				try {
					var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null);
					return global::Java.Lang.Object.GetObject<Android.Telephony.CellIdentity> (__rm.Handle, JniHandleOwnership.TransferLocalRef);
				}
				catch (Java.Lang.NoSuchMethodError e) {
					throw new Java.Lang.AbstractMethodError (__id);
				}
			}
		}
	}

	public sealed partial class CellInfoCdma : CellInfo {
		public new CellIdentityCdma CellIdentity {get;}
	}
}
```


<a name="history">

# History

Java and C# have different ideas about what constitutes a change which is
source- and binary-compatible.  In Java, it is ABI compatible to add new
required methods to an interface and to abstract classes.  This is not
*source* compatible -- the newly required methods will need to be added when
recompiling code -- but it's *binary* compatible, and this is something that
Android has frequently made use of.

Consider the [`android.database.Cursor`][cursor] interface, which changed in:

  * [API-19][cursor-api-19], adding [`Cursor.getNotificationUri()`][cgnu].
  * [API-29][cursor-api-29], adding [`Cursor.getNotificationUris()`][cgnus]
    and [`Cursor.setNotificationUris()][csnus].

The `Cursor` interface is bound as [`Android.Database.ICursor][icursor], and
before C# 8 it was not possible to add members to a C# interface.

How were these new Java members supported?

Such changes were supported by *breaking* API, and using a *new*
`$(TargetFrameworkVersion)` to contain the updated API.  `Mono.Android.dll`
from v4.3 (API-18) would not contain a binding for
`Cursor.getNotificationUri()`, while the binding in v9.0 (API-28) would not
contain bindings for `Cursor.getNotificationUris()` and
`Cursor.setNotificationUris()`.

*So long as* the `$(TargetFrameworkVersion)` value didn't change, source code
would continue to compile without any errors.  Changing the
`$(TargetFrameworkVersion)` value *may* result in new compiler errors due to
added members not being implemented, and we considered this acceptable.

(The alternative was to bind *no* new methods after the initial binding, which
we didn't consider acceptable.)

Binary compatibility -- using a library built against an older binding with
an app using a newer binding -- was preserved by using a [linker step][linker-fix]
which
would look for "missing" abstract methods and insert them so that they would
throw `AbstractMethodError`.


[cursor]: https://developer.android.com/reference/android/database/Cursor
[cursor-api-19]: https://developer.android.com/sdk/api_diff/19/changes/android.database.Cursor
[cursor-api-29]: https://developer.android.com/sdk/api_diff/29/changes/android.database.Cursor
[cgnu]: https://developer.android.com/reference/android/database/Cursor.html#getNotificationUri()
[cgnus]: https://developer.android.com/reference/android/database/Cursor.html#getNotificationUris()
[csnus]: https://developer.android.com/reference/android/database/Cursor.html#setNotificationUris(android.content.ContentResolver,%20java.util.List%3Candroid.net.Uri%3E)
[icursor]: https://docs.microsoft.com/en-us/dotnet/api/android.database.icursor?view=xamarin-android-sdk-9
[linker-fix]: https://github.com/xamarin/xamarin-android/commit/f96fcf93e157472072576bcc0a8698302899e8cf
