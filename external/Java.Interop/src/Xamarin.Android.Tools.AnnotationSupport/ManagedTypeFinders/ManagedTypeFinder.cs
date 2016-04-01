using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.AndroidTools.AnnotationSupport
{
	public abstract class ManagedTypeFinder : AnnotationParserExtension
	{
		#region type system abstraction

		public interface IType { }
		public interface IProperty { }
		public interface IDefinition { }
		public interface IMethodBase { }

		public abstract string GetManagedName (IType t);

		public abstract string GetJavaName (IType t);

		public abstract string GetJavaName (IDefinition f);

		public abstract IProperty GetAnnotatedField (IType t, string fieldName);

		public abstract IDefinition GetDefinitionField (IType t, string fieldName);

		public abstract IMethodBase GetMethod (IType t, AnnotatedItem item);

		public abstract TypeName [] GetParameterTypes (IMethodBase method);

		public abstract void SetName (ManagedMemberInfo destination, IType type);

		public abstract IEnumerable<IDefinition> GetFields (IType t);

		public abstract string GetPropertyName (IProperty m);

		public abstract string GetDefinitionName (IDefinition m);

		public abstract string GetMethodName (IMethodBase m);

		public abstract string GetMethodReturnManagedTypeName (IMethodBase m);

		public abstract string GetFieldManagedTypeName (IProperty property);

		public abstract string GetParameterManagedTypeName (IMethodBase m, int index);

		#endregion

		List<string> errors = new List<string> ();

		public IList<string> Errors {
			get { return errors; }
		}

		public Func<AnnotatedItem, bool> FilterAnnotatedItem { get; set; }

		List<ManagedTypeFinderExtension> extensions = new List<ManagedTypeFinderExtension> ();
		public IList<ManagedTypeFinderExtension> Extensions {
			get { return extensions; }
		}

		#region Context-sensitive members, which works only within LoadManagedMappings().

		public IType GetContextManagedType (string javaTypeName)
		{
			IType t;
			return typemap.TryGetValue (javaTypeName, out t) ? t : null;
		}

		public virtual void PrepareContextTypes (IType [] types)
		{
			var maptmp = types.Select (t => new { Managed = t, JavaName = GetJavaName (t) })
					  .Where (p => p.JavaName != null)
					  .Select (p => new { Managed = p.Managed, JavaName = p.JavaName.Replace ('/', '.').Replace ('$', '.') });

			foreach (var p in maptmp)
				// We don't want *Invoker classes to overwrite this mapping, so check name and skip them.
				if (!typemap.ContainsKey (p.JavaName) || GetManagedName (typemap [p.JavaName]) == GetManagedName (p.Managed) + "Invoker")
					typemap [p.JavaName] = p.Managed;
		}

		Dictionary<string, IType> typemap = new Dictionary<string, IType> ();

		public IDictionary<string, IType> ContextTypes
		{
			get { return typemap; }
		}

		#endregion

		public void LoadManagedMappings (IEnumerable<AnnotatedItem> anns, IType [] types)
		{
			PrepareContextTypes (types);

			foreach (var ann in anns) {
				if (FilterAnnotatedItem != null && !FilterAnnotatedItem (ann))
					continue;

				// Find annotated members themselves.
				var managedType = GetContextManagedType (ann.TypeName);
				if (managedType == null)
					Errors.Add ("warning: managed type for " + ann.TypeName + " was not found");
				else {
					SetName (ann.ManagedInfo, managedType);
					ann.ManagedInfo.TypeObject = managedType;
					if (ann.MemberName == null) {
						// nothing to do: annotation on type.
					} else if (ann.Arguments == null) {
						var managedProperty = GetAnnotatedField (managedType, ann.MemberName);
						if (managedProperty == null)
							Errors.Add ("warning: managed field for " + ann.TypeName + "." + ann.MemberName + " was not found");
						else {
							ann.ManagedInfo.MemberName = GetPropertyName (managedProperty);
							ann.ManagedInfo.PropertyObject = managedProperty;
						}
					} else {
						// constructor or method.
						var m = GetMethod (managedType, ann);
						if (m != null) {
							ann.ManagedInfo.MemberName = GetMethodName (m);
							ann.ManagedInfo.Arguments = GetParameterTypes (m);
							ann.ManagedInfo.MethodObject = m;
						}
					}
				}
				// Find the managed members that each annotation value mentions.
				foreach (var ext in Extensions)
					ext.ProcessAnnotation (ann);
			}
		}

		#region JNI signature parsing

		public static bool AreArgumentsEqualLax (string [] arguments1, string [] arguments2, IEnumerable<string> genericArguments)
		{
			if (arguments1 == null || arguments2 == null || arguments1.Length != arguments2.Length)
				return false;

			// this .Replace() is needed so that T[] can match generic arguments
			Func<string, string> stripParamSuffix = s => s.Replace ("...", "").Replace ("[]", "");
			Func<string, string> stripGenParams = s => s == null ? s : (s.Contains ('<') ? s.Substring (0, s.IndexOf ('<')) : s).Replace ("...", "[]");
			Func<string, string, bool> cmp = (v, w) => stripGenParams (v) == stripGenParams (w);

			bool mismatch = false;
			for (int a = 0; a < arguments1.Length; a++) {
				if (cmp (arguments1 [a], arguments2 [a])
					|| genericArguments.Any (ga => cmp (ga, stripParamSuffix (arguments2 [a]))))
					continue;
				mismatch = true;
				break;
			}
			return !mismatch;
		}

		public static string [] ParseJniMethodArgumentsSignature (string jni)
		{
			int idx = jni.IndexOf (')');
			string parameters = jni.Substring (1, idx - 1);
			return FromJniToFullName (parameters).ToArray ();
		}

		static IEnumerable<string> FromJniToFullName (string s)
		{
			var l = new List<string> ();
			FromJniToFullName (s, l, 0);
			return l;
		}

		static void FromJniToFullName (string s, IList<string> l, int idx)
		{
			if (s.Length == idx)
				return;

			int next = idx + 1;
			string type = null;
			switch (s [idx]) {
			case 'Z': type = "boolean"; break;
			case 'B': type = "byte"; break;
			case 'C': type = "char"; break;
			case 'S': type = "short"; break;
			case 'I': type = "int"; break;
			case 'J': type = "long"; break;
			case 'F': type = "float"; break;
			case 'D': type = "double"; break;
			case '[':
				var item = l.Count;
				next = idx + 1;
				FromJniToFullName (s, l, next);
				l [item] += "[]";
				return;
			case 'L':
				next = s.IndexOf (';', idx) + 1;
				type = s.Substring (idx + 1, next - idx - 2).Replace ('/', '.').Replace ('$', '.');
				break;
			default:
				throw new InvalidOperationException ("Unexpected JNI type signature: " + s + " index " + idx);
			}
			l.Add (type);
			FromJniToFullName (s, l, next);
		}

		#endregion
	}
}

