using System;

using Mono.Cecil;

namespace Java.Interop.Tools.Expressions;

public record ExpressionMethodRegistration (string JniName, string JniSignature, MethodDefinition MarshalMethodDefinition)
{
}
