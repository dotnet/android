using System;
using System.Collections.Generic;
using System.Text;

namespace Java.Interop.Tools.JavaCallableWrappers;

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
