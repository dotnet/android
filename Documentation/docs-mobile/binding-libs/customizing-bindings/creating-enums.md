---
title: "Creating enumerations"
description: There are cases where Java Android libraries use integer constants to represent states that are passed to properties or methods of the libraries. For widely distributed bindings, it may useful to bind these integer constants to enums in C# to provide a nicer API for consumers.
ms.author: jopobst
ms.date: 05/08/2024
---

# Creating enumerations

There are cases where Java Android libraries use integer constants to 
represent states that are passed to properties or methods of the 
libraries. For widely distributed bindings, it may useful to bind these 
integer constants to enums in C# to provide a nicer API for consumers. 

For internal or low usage bindings it's usually not worth the effort
to set these up, as the consumers can simply use the bound constants
instead of an enumeration.

To facilitate this mapping, two files are added to binding projects by 
the default project template:

- **EnumFields.xml** - This file defines the mapping between Java integer
constants and a C# enumeration

- **EnumMethods.xml** - This file defines which methods/properties that 
currently take an `int` method parameter or have an `int` return type
should be modified to use an enumeration instead.

### Defining an enum using EnumFields.xml

The **EnumFields.xml** file contains the mapping between Java `int` 
constants and C# `enums`. Let's take the following example of a C# enum 
being created for a set of `int` constants: 

```xml 
<mapping jni-class="com/skobbler/ngx/map/realreach/SKRealReachSettings" clr-enum-type="Skobbler.Ngx.Map.RealReach.SKMeasurementUnit">
    <field jni-name="UNIT_SECOND" clr-name="Second" value="0" />
    <field jni-name="UNIT_METER" clr-name="Meter" value="1" />
    <field jni-name="UNIT_MILIWATT_HOURS" clr-name="MilliwattHour" value="2" />
</mapping>
```

Here we have taken the Java class `SKRealReachSettings` and defined a 
C# enum called `SKMeasurementUnit` in the namespace 
`Skobbler.Ngx.Map.RealReach`. The `field` entries defines the name of 
the Java constant (example `UNIT_SECOND`), the name of the enum entry 
(example `Second`), and the integer value represented by both 
entities (example `0`). 

### Defining getter/setter methods using EnumMethods.xml

The **EnumMethods.xml** file allows changing method parameters and
return types from Java `int` constants to C# `enums`. In other words,
it maps the reading and writing of C# enums (defined in the
**EnumFields.xml** file) to Java `int` constant `get` and `set`
methods.

Given the `SKRealReachSettings` enum defined above, the following
**EnumMethods.xml** file would define the getter/setter for this enum:

```xml
<mapping jni-class="com/skobbler/ngx/map/realreach/SKRealReachSettings">
    <method jni-name="getMeasurementUnit" parameter="return" clr-enum-type="Skobbler.Ngx.Map.RealReach.SKMeasurementUnit" />
    <method jni-name="setMeasurementUnit" parameter="measurementUnit" clr-enum-type="Skobbler.Ngx.Map.RealReach.SKMeasurementUnit" />
</mapping>
```

The first `method` line maps the return value of the Java
`getMeasurementUnit` method to the `SKMeasurementUnit` enum. The
second `method` line maps the first parameter of the
`setMeasurementUnit` to the same enum.

With all of these changes in place, you can use the follow code in 
.NET for Android to set the `MeasurementUnit`: 

```csharp
realReachSettings.MeasurementUnit = SKMeasurementUnit.Second;
```
