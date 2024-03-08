using System;

using System.Runtime.CompilerServices;

// PublicApiAnalyzers doesn't like TypeForwards
#pragma warning disable RS0016 // Symbol is not part of the declared API
[assembly: TypeForwardedTo (typeof (Java.Interop.JavaTypeParametersAttribute))]
#pragma warning restore RS0016 // Symbol is not part of the declared API
