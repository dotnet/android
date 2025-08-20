using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;

namespace Microsoft.Android.Sdk.ILLink
{
	public abstract class MyBaseSubStep
	{
		protected AnnotationStore Annotations => Context.Annotations;

		LinkContext? _context { get; set; }
		protected LinkContext Context {
			get {
				Debug.Assert (_context != null);
				return _context;
			}
		}

		public abstract SubStepTargets Targets { get; }

		public virtual void Initialize (LinkContext context)
		{
			_context = context;
		}

		public virtual bool IsActiveFor (AssemblyDefinition assembly) => true;

		public virtual void ProcessType (TypeDefinition type)
		{
		}

		public virtual void ProcessField (FieldDefinition field)
		{
		}

		public virtual void ProcessMethod (MethodDefinition method)
		{
		}

		public virtual void ProcessProperty (PropertyDefinition property)
		{
		}

		public virtual void ProcessEvent (EventDefinition @event)
		{
		}
	}
}
