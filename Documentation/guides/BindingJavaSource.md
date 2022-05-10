---
id:
title: "Binding Java Source"
dateupdated: 2022-05-10
---

# Overview

With .net 6 Bindings can be done on not only .jar and .aar files
but also raw .java code. This will allow developers of bindings
or applications to write custom API's for underlying bindings
and expose them easily to the developer.

Applications already had the ability to include `AndroidJavaSource`
items in them, these would then be compiled into the final `classes.dex`.
However this custom java source code was not "bound" and if users wanted
to call these classes they would need to do that manually.

The `AndroidJavaSource` Item Group now supports a `Bind` attribute. This
will instruct the build system to not only compile the code but also
produce a C# API for it.

The `%(Bind)` attribute metadata is `True` by default. If you do not want
to bind the Java code, set `%(Bind)` to `False`.

```xml
<ItemGroup>
    <AndroidJavaSource Include="MyClass.java" Bind="False" />
</ItemGroup>
```

# Example

Consider the following Java code.

```java
package com.xamarin.test;

class MyClass {
    public boolean IsDave (String name)
    {
        return name.equals ("Dave");
    }
}
```---
id:
title: "Binding Java Source"
dateupdated: 2021-05-21
---

# Overview

With .net 6 Bindings can be done on not only `.jar` and `.aar` files
but also raw `.java` code. This will allow developers of bindings
or applications to write custom API's for underlying bindings
and expose them easily to the developer.

Applications already had the ability to include
[`AndroidJavaSource`](https://docs.microsoft.com/en-us/xamarin/android/deploy-test/building-apps/build-items#androidjavasource)
items in them, these would then be compiled into the final `classes.dex`.
However this custom java source code was not "bound" and if users wanted
to call these classes they would need to do that manually.

Starting with .NET 7, all `*.java` files within the project directory are
automatically added to the `AndroidJavaSource` item group.  Additionally,
`AndroidJavaSource` now supports `Bind` item metadata. This will instruct
the build system to not only compile the code but also produce a C# API for it.

The `%(Bind)` attribute metadata is `True` by default. If you do not want
to bind the Java code, set `%(Bind)` to `False`.

```xml
<ItemGroup>
    <AndroidJavaSource Update="MyClass.java" Bind="False" />
</ItemGroup>
```

# Example

Consider the following Java code.

```java
package com.xamarin.test;

class MyClass {
    public boolean isDave (String name)
    {
        return name.equals ("Dave");
    }
}
```

We want to bind this into a C# API in our app project. By default
the app project will pick up ALL `.java` files and add them to
the `AndroidJavaSource` ItemGroup. The `Bind` attribute is `true`
by default as well so it will be automatically bound.

```xml
<!-- No changes needed in .NET 7; `MyClass.java` is automatically included. -->
```

When we build the app, the java code will be compiled into a
`.jar` file which matches the application project name.
This `.jar` will then be bound and the generated C# API will end
up being something like this.

```csharp
using System;

namespace Com.Xamarin.Test {
    public class MyClass : Java.Lang.Object {
        public virtual bool IsDave (string name) => …;
    }
}
```

All this will happen before the main C# code is compiled. The binding
code will be included in the C# compile process, so you can use the
code directly in your app.

```csharp
public class MainActivity : Activity {
    public override void OnCreate()
    {
        var c = new MyClass ();
        c.IsDave ("Bob");
    }
}
```


# Limitations

This feature is only available in .NET 7.

You will be limited to standard java types and any types that
are available in a `.jar` or `.aar` which you reference.

You *should not* use Java Generics. The Binding process currently does not
really support Java Generic very well. Stick to primitive types
or normal classes as much as possible, so that you don't need to use
[`metadata.xml`](https://docs.microsoft.com/en-us/xamarin/android/platform/binding-java-library/customizing-bindings/java-bindings-metadata)
to alter the API of the Java class.

Because of the point at which the Java compilation happens
(before the `<Csc/>` task), you will not be able to access any of the
`R.*` Resource types either.  This may be addressed in a later release.


We want to bind this into a C# API in our app project. By default
the app project will pick up ALL `.java` files and add them to
the `AndroidJavaSource` ItemGroup. The `Bind` attribute is `true`
by default as well so it will be automatically bound.

```xml
<ItemGroup>
    <AndroidJavaSource Include="MyClass.java" />
</ItemGroup>
```

When we build the app, the java code will be compiled into a
.jar file which matches the application project name.
This .jar will then be bound and the generated C# API will end
up being something like this.

```csharp
using System;

namespace Com.Xamarin.Test {
    public class MyClass : Java.Lang.Object {
        public virtual bool IsDave (string name){
            // binding code
        }
    }
}
```

All this will happen before the main C# code is compiled. The binding
code will be included in the C# compile process, so you can use the
code directly in your app.

```csharp
public class MainActivity : Activity {
    public override void OnCreate()
    {
        var c = new MyClass ();
        c.IsDave ("Bob");
    }
}
```


# Limitations

This feature is only available in .NET 7.

You will be limited to standard java types and any types that
are available in a .jar or .aar which you reference.

You CANNOT use Java Generics. The Binding process currently does not
really support Java Generic very well. So stick to primitive types
or normal classes as much as possible. This is so that you don't need
to use the metadata.xml to alter the API of the Java class.

Because of the point at which the Java compilation happens in this case
(before Csc), you will not be able to access any of the `R.*` Resource
types either.
