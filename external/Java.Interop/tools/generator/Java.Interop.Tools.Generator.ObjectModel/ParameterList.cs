using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Xml;
using System.IO;

using Xamarin.Android.Binder;

namespace MonoDroid.Generation {

	public class ParameterList : IEnumerable<Parameter> {
		
		public static bool Equals (ParameterList l1, ParameterList l2)
		{
			if (l1.Count != l2.Count)
				return false;
			for (int i = 0; i < l1.Count; i++)
				if (!l1 [i].Equals (l2 [i]))
					return false;
			return true;
		}
		
		List<Parameter> items = new List<Parameter> ();

		public Parameter this [int idx] {
			get { return items [idx]; }
		}

		public IEnumerator<Parameter> GetEnumerator ()
		{
			return items.GetEnumerator ();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ();
		}

		public string GetCall (CodeGenerationOptions opt)
		{
			StringBuilder sb = new StringBuilder ();
			foreach (Parameter p in items) {
				if (sb.Length > 0)
					sb.Append (", ");
				sb.Append (opt.GetSafeIdentifier (p.Name));
			}
			return sb.ToString ();
		}

		public string CallDropSender {
			get {
				StringBuilder sb = new StringBuilder ();
				foreach (Parameter p in items) {
					if (p.IsSender)
						continue;
					else if (sb.Length > 0)
						sb.Append (", ");
					sb.Append (p.Name);
				}
				return sb.ToString ();
			}
		}

		public string JavaCall {
			get {
				StringBuilder sb = new StringBuilder ();
				foreach (Parameter p in items) {
					if (sb.Length > 0)
						sb.Append (", ");
					sb.Append (p.JavaName);
				}
				return sb.ToString ();
			}
		}

		public void WriteCallArgs (TextWriter writer, string indent, CodeGenerationOptions opt, bool invoker)
		{
			if (items.Count == 0)
				return;
			string JValue   = "JValue";
			switch (opt.CodeGenerationTarget) {
			case CodeGenerationTarget.XAJavaInterop1:
			case CodeGenerationTarget.JavaInterop1:
				JValue  = invoker ? JValue : "JniArgumentValue";
				break;
			}
			writer.WriteLine ("{0}{1}* __args = stackalloc {1} [{2}];", indent, JValue, items.Count);
			for (int i  = 0; i < items.Count; ++i) {
				var p   = items [i];
				writer.WriteLine ("{0}__args [{1}] = new {2} ({3});", indent, i, JValue, p.GetCall (opt));
			}
		}

		public string GetCallArgs (CodeGenerationOptions opt, bool invoker)
		{
			if (items.Count != 0)
				return ", __args";
			if (opt.CodeGenerationTarget == CodeGenerationTarget.XamarinAndroid || invoker)
				return "";
			return ", null";
		}

		public StringCollection GetCallCleanup (CodeGenerationOptions opt)
		{
			StringCollection result = new StringCollection ();
			foreach (Parameter p in items)
				foreach (string s in p.GetPostCall (opt))
					result.Add (s);
			return result;
		}

		public StringCollection GetCallPrep (CodeGenerationOptions opt)
		{
			StringCollection result = new StringCollection ();
			foreach (Parameter p in items)
				foreach (string s in p.GetPreCall (opt))
					result.Add (s);
			return result;
		}

		public StringCollection GetCallbackCleanup (CodeGenerationOptions opt)
		{
			StringCollection result = new StringCollection ();
			foreach (Parameter p in items)
				foreach (string s in p.GetPostCallback (opt))
					result.Add (s);
			return result;
		}

		public StringCollection GetCallbackPrep (CodeGenerationOptions opt)
		{
			StringCollection result = new StringCollection ();
			foreach (Parameter p in items)
				foreach (string s in p.GetPreCallback (opt))
					result.Add (s);
			return result;
		}

		public string GetCallbackSignature (CodeGenerationOptions opt)
		{
			StringBuilder sb = new StringBuilder ();
			foreach (Parameter p in items) {
				sb.Append (", ");
				sb.Append (p.NativeType);
				sb.Append (" ");
				sb.Append (opt.GetSafeIdentifier (p.UnsafeNativeName));
			}
			return sb.ToString ();
		}

		public int Count {
			get { return items.Count; }
		}

		public string DelegateTypeParams {
			get {
				StringBuilder sb = new StringBuilder ();
				foreach (Parameter p in items) {
					sb.Append (", ");
					sb.Append (p.NativeType);
				}
				return sb.ToString ();
			}
		}

		public bool HasCharSequence {
			get {
				foreach (Parameter p in items)
					if (p.JavaType.StartsWith("java.lang.CharSequence"))
						return true;
				return false;
			}
		}

		public bool HasCleanup {
			get {
				foreach (Parameter p in items)
					if (p.NeedsPrep)
						return true;
				return false;
			}
		}

		public bool HasGeneric {
			get {
				foreach (Parameter p in items)
					if (p.IsGeneric)
						return true;
				return false;
			}
		}

		public bool HasSender {
			get {
				foreach (Parameter p in items)
					if (p.IsSender)
						return true;
				return false;
			}
		}

		public string JavaSignature {
			get {
				StringBuilder sb = new StringBuilder ();
				foreach (Parameter p in items) {
					if (sb.Length > 0)
						sb.Append (", ");
					sb.Append (p.JavaType);
					sb.Append (" ");
					sb.Append (p.JavaName);
				}
				return sb.ToString ();
			}
		}

		public string JniSignature {
			get {
				StringBuilder sb = new StringBuilder ();
				foreach (Parameter p in items)
					sb.Append (p.JniType);
				return sb.ToString ();
			}
		}

		public string JniNestedDerivedSignature {
			get {
				StringBuilder sb = new StringBuilder ();
				foreach (Parameter p in items) {
					if (p.Name == "__self") {
						sb.Append ("L\" + global::Android.Runtime.JNIEnv.GetJniName (GetType ().DeclaringType) + \";");
						continue;
					}
					sb.Append (p.JniType);
				}
				return sb.ToString ();
			}
		}

		public string SenderName {
			get {
				foreach (Parameter p in items)
					if (p.IsSender)
						return p.Name;
				return String.Empty;
			}
		}

		public string GetSignatureDropSender (CodeGenerationOptions opt)
		{
			StringBuilder sb = new StringBuilder ();
			foreach (Parameter p in items) {
				if (p.IsSender)
					continue;
				else if (sb.Length > 0)
					sb.Append (", ");
				sb.Append (opt.GetOutputName (p.Type));
				sb.Append (" ");
				sb.Append (p.Name);
			}
			return sb.ToString ();
		}

		public void Add (Parameter parm)
		{
			items.Add (parm);
		}

		public void AddFirst (Parameter parm)
		{
			items.Insert (0, parm);
		}

		public string GetGenericCall (CodeGenerationOptions opt, Dictionary<string, string> mappings)
		{
			StringBuilder sb = new StringBuilder ();
			foreach (Parameter p in items) {
				if (sb.Length > 0)
					sb.Append (", ");
				sb.Append (p.GetGenericCall (opt, mappings));
			}
			return sb.ToString ();
		}

		public string GetMethodXPathPredicate ()
		{
			if (items.Count == 0)
				return " and count(parameter)=0";

			var sb = new StringBuilder ();
			sb.Append (" and count(parameter)=").Append (items.Count);
			for (int i = 0; i < items.Count; ++i) {
				sb.Append (" and parameter[").Append (i+1).Append ("]")
				  .Append ("[@type='").Append (items [i].RawNativeType.Replace ("<", "&lt;").Replace (">","&gt;")).Append ("']");
			}
			return sb.ToString ();
		}

		public bool Validate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params)
		{
			foreach (Parameter p in items)
				if (!p.Validate (opt, type_params))
					return false;
			return true;
		}
	}
}
