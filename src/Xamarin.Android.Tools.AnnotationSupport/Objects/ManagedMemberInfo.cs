using System;
namespace Xamarin.AndroidTools.AnnotationSupport
{
	public class ManagedMemberInfo
	{
		public ManagedMemberInfo ()
		{
			Type = new TypeName ();
		}

		public TypeName Type { get; private set; }
		public string MemberName { get; set; }
		public TypeName [] Arguments { get; set; }

		// These fields are optional, as they are extraneous except for use in generator.exe.
		public ManagedTypeFinder.IType TypeObject { get; set; }
		public ManagedTypeFinder.IProperty PropertyObject { get; set; }
		public ManagedTypeFinder.IMethodBase MethodObject { get; set; }
	}

	public class TypeName
	{
		public string FullName {
			get { return string.IsNullOrEmpty (Namespace) ? Name : Namespace + '.' + Name; }
		}
		public string Namespace { get; set; }
		public string Name { get; set; }
	}
}

