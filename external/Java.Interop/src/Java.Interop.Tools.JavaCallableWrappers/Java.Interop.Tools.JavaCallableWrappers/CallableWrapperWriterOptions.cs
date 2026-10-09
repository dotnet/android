using System;
using System.Collections.Generic;
using System.Text;

namespace Java.Interop.Tools.JavaCallableWrappers;

/// <summary>
/// Compatibility options for callers selecting the single supported XAJavaInterop1 style.
/// </summary>
public class CallableWrapperWriterOptions
{
	JavaPeerStyle codeGenerationTarget;

	public JavaPeerStyle CodeGenerationTarget {
		get => codeGenerationTarget;
		set {
			if (value != JavaPeerStyle.XAJavaInterop1)
				throw new NotSupportedException ($"The Java callable wrapper code generation target '{value}' is not supported. Use XAJavaInterop1 instead.");
			codeGenerationTarget = value;
		}
	}
}
