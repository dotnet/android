using Mono.Cecil;

namespace Java.Interop.Tools.JavaCallableWrappers
{
	public abstract class JavaCallableMethodClassifier
	{
		public abstract bool ShouldBeDynamicallyRegistered (TypeDefinition topType, MethodDefinition registeredMethod, MethodDefinition implementedMethod, CustomAttribute? registerAttribute);
	}
}
