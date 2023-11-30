using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.Diagnostics;
using Java.Interop.Tools.JavaCallableWrappers;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class JavaStubsState
{
	public MarshalMethodsClassifier? Classifier { get; }
	public List<JavaType> AllJavaTypes { get; }

	public JavaStubsState (List<JavaType> allJavaTypes, MarshalMethodsClassifier? classifier)
	{
		AllJavaTypes = allJavaTypes;
		Classifier = classifier;
	}
}
