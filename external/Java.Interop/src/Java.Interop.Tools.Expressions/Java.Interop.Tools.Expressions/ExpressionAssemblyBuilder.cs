using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Text;

using Java.Interop;
using Java.Interop.Tools.Diagnostics;

using Mono.Cecil;
using Mono.Cecil.Cil;
using static System.Formats.Asn1.AsnWriter;

namespace Java.Interop.Tools.Expressions;

public class ExpressionAssemblyBuilder {

	public ExpressionAssemblyBuilder (AssemblyDefinition declaringAssemblyDefinition, Action<TraceLevel, string>? logger = null)
	{
		DeclaringAssemblyDefinition     = declaringAssemblyDefinition;
		Logger                          = logger ?? Diagnostic.CreateConsoleLogger ();
	}

	public  AssemblyDefinition              DeclaringAssemblyDefinition     {get;}
	public  Action<TraceLevel, string>      Logger                          {get;}
	public  bool                            KeepTemporaryFiles              {get; set;}

	public MethodDefinition Compile (LambdaExpression expression)
	{
		var mmDef   = CreateMethodDefinition (DeclaringAssemblyDefinition, expression);
		var decls   = new VariableDefinitions (DeclaringAssemblyDefinition, mmDef, expression, Logger);
		var mmBody  = mmDef.Body;
		var il      = mmBody.GetILProcessor ();
		var v       = new CecilCompilerExpressionVisitor (DeclaringAssemblyDefinition, mmBody, decls, Logger);
		v.Visit (expression);

		if (expression.ReturnType != null && expression.ReturnType != typeof (void) && decls.ReturnValue == null) {
			Logger (TraceLevel.Error, $"# jonp: validation error: expression has a return type but we didn't find a return value! expression={expression}");
		}

		decls.ReturnValue?.Load (il);
		il.Emit (OpCodes.Ret);

		return mmDef;
	}

	static MethodDefinition CreateMethodDefinition (AssemblyDefinition declaringAssembly, LambdaExpression expression)
	{
		var mmDef = new MethodDefinition (
			name:       "@CHANGE-ME@",
			attributes: Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.HideBySig,
			returnType: declaringAssembly.MainModule.ImportReference (expression.ReturnType)
		) {
			Body = {
				InitLocals      = true,
			},
		};
		return mmDef;
	}

	public void AddRegistrationMethod (TypeDefinition declaringType, IList<ExpressionMethodRegistration> methods)
	{
		var registrations = new MethodDefinition (
			name:       "__RegisterNativeMembers",
			attributes: MethodAttributes.Static | MethodAttributes.Private | MethodAttributes.HideBySig,
			returnType: DeclaringAssemblyDefinition.MainModule.TypeSystem.Void
		) {
			Body = {
				InitLocals      = true,
			},
		};

		declaringType.Methods.Add (registrations);

		var ctor    = typeof (JniAddNativeMethodRegistrationAttribute).GetConstructor (Type.EmptyTypes);
		var attr    = new CustomAttribute (DeclaringAssemblyDefinition.MainModule.ImportReference (ctor));
		registrations.CustomAttributes.Add (attr);

		var args    = new ParameterDefinition ("args", default, DeclaringAssemblyDefinition.MainModule.ImportReference (typeof (JniNativeMethodRegistrationArguments)));
		registrations.Parameters.Add (args);

		var arrayType   = DeclaringAssemblyDefinition.MainModule.ImportReference (typeof (JniNativeMethodRegistration []));

		var array   = new VariableDefinition (arrayType);
		registrations.Body.Variables.Add (array);

		var il = registrations.Body.GetILProcessor ();
		EmitConsoleWriteLine (il, $"# jonp: called `{declaringType.FullName}.__RegisterNativeMembers()` w/ {methods.Count} methods to register.");
		il.Emit (OpCodes.Ldc_I4, methods.Count);
		il.Emit (OpCodes.Newarr, DeclaringAssemblyDefinition.MainModule.ImportReference (arrayType.GetElementType ()));
		// il.Emit (OpCodes.Stloc_0);

		var JniNativeMethodRegistration_ctor    = typeof (JniNativeMethodRegistration).GetConstructor (new [] { typeof (string), typeof (string), typeof (Delegate) });
		var jnmr_ctor                           = DeclaringAssemblyDefinition.MainModule.ImportReference (JniNativeMethodRegistration_ctor);

		for (int i = 0; i < methods.Count; i++) {
			var delegateCtor = GetMarshalMethodDelegateCtor (methods [i].MarshalMethodDefinition);

			// il.Emit (OpCodes.Ldloc_0);      // args
			il.Emit (OpCodes.Dup);      // args
			il.Emit (OpCodes.Ldc_I4, i);    // index of `args` to set

			// new JniNativeMethodRegistration (JniName, JniSignature, new _JniMarshal_PP… (MarshalMethodDefinition))
			il.Emit (OpCodes.Ldstr,         methods [i].JniName);
			il.Emit (OpCodes.Ldstr,         methods [i].JniSignature);
			il.Emit (OpCodes.Ldnull);
			il.Emit (OpCodes.Ldftn,         methods [i].MarshalMethodDefinition);
			il.Emit (OpCodes.Newobj,        delegateCtor);
			il.Emit (OpCodes.Newobj,        jnmr_ctor);

			il.Emit (OpCodes.Stelem_Any,	arrayType.GetElementType ());   // args [i] = new JniNativeMethodRegistration (…)
		}

		il.Emit (OpCodes.Stloc_0);

		Action<IEnumerable<JniNativeMethodRegistration>> addRegistrations = new JniNativeMethodRegistrationArguments ().AddRegistrations;
		il.Emit (OpCodes.Ldarga_S, args);
		il.Emit (OpCodes.Ldloc_0);
		il.Emit (OpCodes.Call, DeclaringAssemblyDefinition.MainModule.ImportReference (addRegistrations.Method));
		il.Emit (OpCodes.Ret);
	}

	void EmitConsoleWriteLine (ILProcessor il, string message)
	{
		Action<string> cwl = Console.WriteLine;
		il.Emit (OpCodes.Ldstr, message);
		il.Emit (OpCodes.Call,  DeclaringAssemblyDefinition.MainModule.ImportReference (cwl.Method));
	}

	// Keep in sync w/ MarshalMemberBuilder.GetMarshalerType()
	MethodReference GetMarshalMethodDelegateCtor (MethodDefinition method)
	{
		// Too many parameters; does a `_JniMarshal_*` type exist in the type's declaring assembly?
		var delegateName    = GetMarshalMethodDelegateName (method.Parameters, method.ReturnType);

		var delegateDef = DeclaringAssemblyDefinition.MainModule.GetType (delegateName.ToString ());
		if (delegateDef == null) {
			delegateDef     = CreateMarshalMethodDelegateType (delegateName, method.Parameters, method.ReturnType);
			DeclaringAssemblyDefinition.MainModule.Types.Add (delegateDef);
		}
		return delegateDef.Methods.First (m => m.Name == ".ctor");
	}

	string GetMarshalMethodDelegateName (IList<ParameterDefinition> parameters, TypeReference returnType)
	{
		// Too many parameters; does a `_JniMarshal_*` type exist in the type's declaring assembly?
		var delegateName    = new StringBuilder ();
		delegateName.Append ("_JniMarshal_PP");

		for (int i = 2; i < parameters.Count; i++) {
			delegateName.Append (GetJniMarshalDelegateParameterIdentifier (parameters [i].ParameterType));
		}
		delegateName.Append ("_");
		delegateName.Append (GetJniMarshalDelegateParameterIdentifier (returnType));

		return delegateName.ToString ();
	}

	char GetJniMarshalDelegateParameterIdentifier (TypeReference type)
	{
		switch (type?.FullName) {
			case "System.Boolean":  return 'Z';
			case "System.Byte":     return 'B';
			case "System.SByte":    return 'B';
			case "System.Char":     return 'C';
			case "System.Int16":    return 'S';
			case "System.UInt16":   return 's';
			case "System.Int32":    return 'I';
			case "System.UInt32":   return 'i';
			case "System.Int64":    return 'J';
			case "System.UInt64":   return 'j';
			case "System.Single":   return 'F';
			case "System.Double":   return 'D';
			case null:
			case "System.Void":     return 'V';
			default:                return 'L';
		}
	}

	public TypeDefinition CreateMarshalMethodDelegateType (string delegateName, IList<ParameterDefinition> parameters, TypeReference returnType)
	{
		var delegateDef = new TypeDefinition (
				@namespace: "",
				name:       delegateName,
				attributes: TypeAttributes.Class | TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.AnsiClass | TypeAttributes.AutoClass
		);
		delegateDef.BaseType = DeclaringAssemblyDefinition.MainModule.ImportReference (typeof (MulticastDelegate));

		var ufpCtor     = typeof (UnmanagedFunctionPointerAttribute).GetConstructor (new[]{typeof (CallingConvention)});
		var ufpCtorRef  = DeclaringAssemblyDefinition.MainModule.ImportReference (ufpCtor);
		var ufpAttr     = new CustomAttribute (ufpCtorRef);
		ufpAttr.ConstructorArguments.Add (
				new CustomAttributeArgument (
					DeclaringAssemblyDefinition.MainModule.ImportReference (typeof (CallingConvention)),
					CallingConvention.Winapi));
		delegateDef.CustomAttributes.Add (ufpAttr);

		var delegateCtor = new MethodDefinition (
				name:       ".ctor",
				attributes: MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
				returnType: DeclaringAssemblyDefinition.MainModule.TypeSystem.Void
		);
		delegateCtor.ImplAttributes     = MethodImplAttributes.Runtime | MethodImplAttributes.Managed;
		delegateCtor.Parameters.Add (new ParameterDefinition ("object", default, DeclaringAssemblyDefinition.MainModule.TypeSystem.Object));
		delegateCtor.Parameters.Add (new ParameterDefinition ("method", default, DeclaringAssemblyDefinition.MainModule.TypeSystem.IntPtr));
		delegateDef.Methods.Add (delegateCtor);

		var invoke = new MethodDefinition (
				name: "Invoke",
				attributes: MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
				returnType: returnType
		);
		invoke.ImplAttributes   = MethodImplAttributes.Runtime | MethodImplAttributes.Managed;
		foreach (var p in parameters) {
			invoke.Parameters.Add (new ParameterDefinition (p.Name, p.Attributes, p.ParameterType));
		}
		delegateDef.Methods.Add (invoke);

		return delegateDef;
	}


	public void Write (string path)
	{
		Logger (TraceLevel.Verbose, $"# jonp: ExpressionAssemblyBuilder.Write to path={path}");
		var module = DeclaringAssemblyDefinition.MainModule;

		var c       = new MemoryStream ();
		DeclaringAssemblyDefinition.Write (c);
		c.Position  = 0;

		if (KeepTemporaryFiles) {
			using var intermediate = File.Create (path + ".cecil");
			c.WriteTo (intermediate);
			c.Position  = 0;
		}

		Logger (TraceLevel.Verbose, $"# jonp: ---");

		var rp = new ReaderParameters {
			InMemory    = true,
			ReadSymbols = false,
			ReadWrite   = false,
			ReadingMode = ReadingMode.Immediate,
		};
		var newAsm              = AssemblyDefinition.ReadAssembly (c, rp);
		module                  = newAsm.MainModule;

		var selfRef             = module.AssemblyReferences.FirstOrDefault (r => r.Name == newAsm.Name.Name);
		foreach (var member in module.GetMemberReferences ()) {
			Logger (TraceLevel.Verbose, $"# jonp: looking at ref for member: [{member.DeclaringType.Scope?.Name}]{member}");
			if (member.DeclaringType.Scope == selfRef) {
				Logger (TraceLevel.Verbose, $"# jonp: Fixing scope self ref for member: {member}");
				member.DeclaringType.Scope = null;
				continue;
			}
		}
		foreach (var type in module.GetTypeReferences ()) {
			Logger (TraceLevel.Verbose, $"# jonp: looking at ref for type: [{type.Scope}]{type}");
			if (type.Scope == selfRef) {
				Logger (TraceLevel.Verbose, $"# jonp: Fixing scope self ref for type: {type}");
				type.Scope = null;
				continue;
			}
		}
		if (selfRef != null) {
			module.AssemblyReferences.Remove (selfRef);
		}
		newAsm.Write (path);
	}

	static AssemblyNameReference GetSystemRuntimeReference ()
	{
		var privateCorelibDir   = Path.GetDirectoryName (typeof (object).Assembly.Location) ??
			throw new NotSupportedException ("Cannot find directory of `System.Private.CoreLib.dll`!");
		var systemRuntimePath   = Path.Combine (privateCorelibDir, "System.Runtime.dll");
		if (!File.Exists (systemRuntimePath)) {
			throw new NotSupportedException ($"Could not find `System.Runtime.dll`; looked at `{systemRuntimePath}`.");
		}
		var rp                  = new ReaderParameters {
			InMemory        = false,
			ReadSymbols     = false,
			ReadWrite       = false,
			ReadingMode     = ReadingMode.Deferred,
		};
		using var systemRuntime = AssemblyDefinition.ReadAssembly (systemRuntimePath, rp);
		var nameDef             = systemRuntime.Name;
		return new AssemblyNameReference (nameDef.Name, nameDef.Version) {
			HashAlgorithm   = nameDef.HashAlgorithm,
			PublicKeyToken  = nameDef.PublicKeyToken,
		};
	}
}

sealed class VariableInfo {
	public VariableInfo (Action<ILProcessor> load, Action<ILProcessor> loadAddress, Action<ILProcessor> store)
	{
		Load            = load;
		LoadAddress     = loadAddress;
		Store           = store;
	}

	public  readonly    Action<ILProcessor> Load;
	public  readonly    Action<ILProcessor> LoadAddress;
	public  readonly    Action<ILProcessor> Store;
}

sealed class VariableDefinitions {

	Dictionary<ParameterExpression, VariableInfo>   variables = new ();
	Action<TraceLevel, string>                      Logger;

	public VariableDefinitions (AssemblyDefinition declaringAssembly, MethodDefinition declaringMethod, LambdaExpression expression, Action<TraceLevel, string> logger)
	{
		Logger  = logger;
		for (int i = 0; i < expression.Parameters.Count; ++i) {
			var c = expression.Parameters [i];
			var d = new ParameterDefinition (c.Name, default, declaringAssembly.MainModule.ImportReference (c.Type));
			declaringMethod.Parameters.Add (d);

			VariableInfo v;

			switch (i) {
			case 0:
				v = new VariableInfo (il => il.Emit (OpCodes.Ldarg_0),  il => il.Emit (OpCodes.Ldarga, 0),  il => il.Emit (OpCodes.Starg, 0));
				break;
			case 1:
				v = new VariableInfo (il => il.Emit (OpCodes.Ldarg_1),  il => il.Emit (OpCodes.Ldarga, 1),  il => il.Emit (OpCodes.Starg, 1));
				break;
			case 2:
				v = new VariableInfo (il => il.Emit (OpCodes.Ldarg_2),  il => il.Emit (OpCodes.Ldarga, 2),  il => il.Emit (OpCodes.Starg, 2));
				break;
			case 3:
				v = new VariableInfo (il => il.Emit (OpCodes.Ldarg_3),  il => il.Emit (OpCodes.Ldarga, 3),  il => il.Emit (OpCodes.Starg, 3));
				break;
			default:
				int x = i;
				v = new VariableInfo (il => il.Emit (OpCodes.Ldarg, x), il => il.Emit (OpCodes.Ldarga, x),  il => il.Emit (OpCodes.Starg, x));
				break;
			}
			variables [c] = v;
		}
		FillVariables (declaringAssembly, declaringMethod, expression);
	}

	public VariableInfo? ReturnValue {get; private set;}

	public VariableInfo this [ParameterExpression e] {
		get => variables [e];
	}

	void FillVariables (
			AssemblyDefinition declaringAssembly,
			MethodDefinition declaringMethod,
			Expression e)
	{
		var variableVisitor = new VariableExpressionVisitor (variables.Keys, Logger);
		variableVisitor.Visit (e);

		Logger (TraceLevel.Verbose, $"# jonp: filling {variableVisitor.Variables.Count} variables");
		for (int i = 0; i < variableVisitor.Variables.Count; ++i) {
			var c = variableVisitor.Variables [i];
			var d = new VariableDefinition (declaringAssembly.MainModule.ImportReference (c.Type));
			declaringMethod.Body.Variables.Add (d);

			VariableInfo v;

			switch (i) {
			case 0:
				v = new VariableInfo (il => il.Emit (OpCodes.Ldloc_0),  il => il.Emit (OpCodes.Ldloca, 0),  il => il.Emit (OpCodes.Stloc_0));
				break;
			case 1:
				v = new VariableInfo (il => il.Emit (OpCodes.Ldloc_1),  il => il.Emit (OpCodes.Ldloca, 1),  il => il.Emit (OpCodes.Stloc_1));
				break;
			case 2:
				v = new VariableInfo (il => il.Emit (OpCodes.Ldloc_2),  il => il.Emit (OpCodes.Ldloca, 2),  il => il.Emit (OpCodes.Stloc_2));
				break;
			case 3:
				v = new VariableInfo (il => il.Emit (OpCodes.Ldloc_3),  il => il.Emit (OpCodes.Ldloca, 3),  il => il.Emit (OpCodes.Stloc_3));
				break;
			default:
				var x = i;
				v = new VariableInfo (il => il.Emit (OpCodes.Ldloc, x),	il => il.Emit (OpCodes.Ldloca, x),  il => il.Emit (OpCodes.Stloc, x));
				break;
			}
			variables [c] = v;
			if (c == variableVisitor.ReturnValue) {
				ReturnValue = v;
			}
			Logger (TraceLevel.Verbose, $"# jonp: FillVariables: local var {c.Name} is index {i}");
		}
	}
}

class VariableExpressionVisitor : ExpressionVisitor {

	public VariableExpressionVisitor (ICollection<ParameterExpression> arguments, Action<TraceLevel, string> logger)
	{
		Arguments   = arguments;
		Logger      = logger;
	}

	ICollection<ParameterExpression>    Arguments;
	Action<TraceLevel, string>          Logger;

	public  List<ParameterExpression>       Variables        = new ();
	public  ParameterExpression?            ReturnValue;

	protected override Expression VisitGoto (
			GotoExpression node)
	{
		Logger (TraceLevel.Verbose, $"# jonp: VariableExpressionVisitor.Goto: {node}; node.Kind={node.Kind}; node.Type={node.Type}");
		if (node.Kind != GotoExpressionKind.Return) {
			return base.VisitGoto (node);
		}
		if (ReturnValue != null) {
			return base.VisitGoto (node);
		}
		Logger (TraceLevel.Verbose, $"# jonp: VariableExpressionVisitor.Goto: node.Target={node.Target} node.Value={node.Value}");
		if (node.Value is ParameterExpression rv) {
			ReturnValue    = rv;
			return base.VisitGoto (node);
		}
		if (node.Type == typeof (void)) {
			return base.VisitGoto (node);
		}
		var p = Expression.Parameter (node.Type, "__goto.Return.Temporary");
		Variables.Add (p);
		ReturnValue = p;
		Logger (TraceLevel.Verbose, $"# jonp: VariableExpressionVisitor.Goto: setting ReturnValue={p}");
		return base.VisitGoto (node);
	}

	protected override Expression VisitParameter (
			ParameterExpression node)
	{
		if (!Arguments.Contains (node) && !Variables.Contains (node)) {
			Variables.Add (node);
		}
		return node;
	}
}
