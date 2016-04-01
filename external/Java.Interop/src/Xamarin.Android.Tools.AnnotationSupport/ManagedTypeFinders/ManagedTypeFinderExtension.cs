using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.AndroidTools.AnnotationSupport
{
	public abstract class ManagedTypeFinderExtension
	{
		protected ManagedTypeFinderExtension (ManagedTypeFinder m)
		{
			this.ManagedTypeFinder = m;
		}

		protected ManagedTypeFinder ManagedTypeFinder { get; private set; }

		ManagedTypeFinder _ {
			get { return ManagedTypeFinder; }
		}

		public abstract void ProcessAnnotation (AnnotatedItem item);

		protected string GetTargetManagedTypeName (AnnotatedItem item)
		{
			var contextType = _.GetContextManagedType (item.TypeName);
			var contextField = item.Arguments == null ? _.GetAnnotatedField (contextType, item.MemberName) : null;
			if (item.Arguments == null)
				return contextField == null ? null :_.GetFieldManagedTypeName (contextField);
			var contextMethod = item.Arguments == null ? null : _.GetMethod (contextType, item);
			if (contextMethod != null)
				return item.ParameterIndex < 0 ?
					   _.GetMethodReturnManagedTypeName (contextMethod) :
					   _.GetParameterManagedTypeName (contextMethod, item.ParameterIndex);
			return null;
		}
	}
}
