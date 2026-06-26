using System;
using System.IO;

namespace Xamarin.Android.Tools.Bytecode
{
	// https://docs.oracle.com/javase/specs/jvms/se8/html/jvms-4.html#jvms-4.7.20
	public enum TypeAnnotationTargetType : byte
	{
		ClassTypeParameter              = 0x00,
		MethodTypeParameter             = 0x01,
		ClassExtends                    = 0x10,
		ClassTypeParameterBound         = 0x11,
		MethodTypeParameterBound        = 0x12,
		Field                           = 0x13,
		MethodReturn                    = 0x14,
		MethodReceiver                  = 0x15,
		MethodFormalParameter           = 0x16,
		Throws                          = 0x17,
		LocalVariable                   = 0x40,
		ResourceVariable                = 0x41,
		ExceptionParameter              = 0x42,
		Instanceof                      = 0x43,
		New                             = 0x44,
		ConstructorReference            = 0x45,
		MethodReference                 = 0x46,
		Cast                            = 0x47,
		ConstructorInvocationTypeArgument = 0x48,
		MethodInvocationTypeArgument    = 0x49,
		ConstructorReferenceTypeArgument = 0x4A,
		MethodReferenceTypeArgument     = 0x4B,
	}

	// https://docs.oracle.com/javase/specs/jvms/se8/html/jvms-4.html#jvms-4.7.20
	public sealed class TypeAnnotation
	{
		public  TypeAnnotationTargetType    TargetType          { get; }

		// `formal_parameter_index` for MethodFormalParameter; otherwise 0.
		public  int                         FormalParameterIndex    { get; }

		// `path_length` from `type_path`; we don't retain the individual
		// path entries — see AppliesToTopLevelType.
		public  int                         TypePathLength      { get; }

		public  Annotation                  Annotation          { get; }

		// JSpecify-style nullness annotations only apply to the top-level
		// type when there is no `type_path`. Annotations with a non-empty
		// path describe inner types (e.g. `Map<@Nullable K, V>`) which the
		// API XML schema cannot currently represent.
		public  bool                        AppliesToTopLevelType => TypePathLength == 0;

		public TypeAnnotation (ConstantPool constantPool, Stream stream)
		{
			TargetType = (TypeAnnotationTargetType) stream.ReadNetworkByte ();

			// target_info — size depends on target_type.
			switch (TargetType) {
			case TypeAnnotationTargetType.ClassTypeParameter:
			case TypeAnnotationTargetType.MethodTypeParameter:
				stream.ReadNetworkByte ();              // type_parameter_index
				break;
			case TypeAnnotationTargetType.ClassExtends:
				stream.ReadNetworkUInt16 ();            // supertype_index
				break;
			case TypeAnnotationTargetType.ClassTypeParameterBound:
			case TypeAnnotationTargetType.MethodTypeParameterBound:
				stream.ReadNetworkByte ();              // type_parameter_index
				stream.ReadNetworkByte ();              // bound_index
				break;
			case TypeAnnotationTargetType.Field:
			case TypeAnnotationTargetType.MethodReturn:
			case TypeAnnotationTargetType.MethodReceiver:
				// empty_target — no bytes
				break;
			case TypeAnnotationTargetType.MethodFormalParameter:
				FormalParameterIndex = stream.ReadNetworkByte ();
				break;
			case TypeAnnotationTargetType.Throws:
				stream.ReadNetworkUInt16 ();            // throws_type_index
				break;
			case TypeAnnotationTargetType.LocalVariable:
			case TypeAnnotationTargetType.ResourceVariable: {
				var table_length = stream.ReadNetworkUInt16 ();
				for (int i = 0; i < table_length; ++i) {
					stream.ReadNetworkUInt16 ();    // start_pc
					stream.ReadNetworkUInt16 ();    // length
					stream.ReadNetworkUInt16 ();    // index
				}
				break;
			}
			case TypeAnnotationTargetType.ExceptionParameter:
				stream.ReadNetworkUInt16 ();            // exception_table_index
				break;
			case TypeAnnotationTargetType.Instanceof:
			case TypeAnnotationTargetType.New:
			case TypeAnnotationTargetType.ConstructorReference:
			case TypeAnnotationTargetType.MethodReference:
				stream.ReadNetworkUInt16 ();            // offset
				break;
			case TypeAnnotationTargetType.Cast:
			case TypeAnnotationTargetType.ConstructorInvocationTypeArgument:
			case TypeAnnotationTargetType.MethodInvocationTypeArgument:
			case TypeAnnotationTargetType.ConstructorReferenceTypeArgument:
			case TypeAnnotationTargetType.MethodReferenceTypeArgument:
				stream.ReadNetworkUInt16 ();            // offset
				stream.ReadNetworkByte ();              // type_argument_index
				break;
			default:
				throw new NotSupportedException ($"Unknown type_annotation target_type: 0x{(byte)TargetType:X2}");
			}

			// type_path: u1 path_length, then path_length * { u1 type_path_kind; u1 type_argument_index; }
			TypePathLength = stream.ReadNetworkByte ();
			for (int i = 0; i < TypePathLength; ++i) {
				stream.ReadNetworkByte ();              // type_path_kind
				stream.ReadNetworkByte ();              // type_argument_index
			}

			// The remaining bytes match the regular `annotation` structure.
			Annotation = new Annotation (constantPool, stream);
		}

		public override string ToString ()
		{
			return $"TypeAnnotation({TargetType}, paramIndex={FormalParameterIndex}, pathLength={TypePathLength}, {Annotation})";
		}
	}
}
