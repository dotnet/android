using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Xamarin.AndroidTools.AnnotationSupport
{	
	public class AnnotatedItem : AnnotationObject
	{
		public AnnotatedItem (XElement e)
		{
			ManagedInfo = new ManagedMemberInfo ();

			var a = e.Attribute ("name");
			Name = a == null ? null : a.Value;
			Annotations = e.Elements ("annotation")
				.Select (c => new AnnotationData (c))
				.ToArray ();

			if (Name.Contains (' ')) {
				string last = Name.Substring (Name.LastIndexOf (' ') + 1);
				int p;
				ParameterIndex = int.TryParse (last, out p) ? p : -1;
				TypeName = Name.Substring (0, Name.IndexOf (' '));
				var member = Name.Substring (TypeName.Length + 1, Name.Length - TypeName.Length - 1 - (ParameterIndex < 0 ? 0 : last.Length + 1));
				int argStart = member.IndexOf ('(');
				Arguments = argStart < 0 ? null : ParseArguments (member.Substring (argStart + 1, member.Length - argStart - 2))
					.Select (s => s.Trim ())
					.ToArray ();
				var memberNoArgs = argStart < 0 ? member : member.Substring (0, argStart);
				int memberNameIdx = memberNoArgs.IndexOf (' ');
				if (memberNameIdx < 0 && Arguments != null)
					MemberName = "#ctor";
				else {
					MemberType = memberNameIdx < 0 ? null : memberNoArgs.Substring (0, memberNameIdx);
					MemberName = memberNoArgs.Substring (memberNameIdx < 0 ? 0 : memberNameIdx + 1);
				}
				if (MemberName == "#ctor" && argStart < 0) throw new Exception (Name + " | " + member);
			} else {
				TypeName = Name;
			}
		}
		
		public string Name { get; set; }
		public IList<AnnotationData> Annotations { get; private set; }

		public int ParameterIndex { get; private set; }
		public string TypeName { get; private set; }
		public string MemberType { get; private set; }
		public string MemberName { get; private set; }
		public string [] Arguments { get; private set; }

		public ManagedMemberInfo ManagedInfo { get; set; }

		static readonly char [] sep = new char [] {',', '<', '['};
		
		IEnumerable<string> ParseArguments (string args)
		{
			int idx = args.IndexOfAny (sep);
			if (idx < 0) {
				if (!string.IsNullOrWhiteSpace (args))
					yield return args;
			} else if (args [idx] == ',') {
				if (idx > 0)
					yield return args.Substring (0, idx);
				foreach (var x in ParseArguments (args.Substring (idx + 2))) // 2 = ',' and ' '
					yield return x;
			} else if (args [idx] == '[') {
				while (idx < args.Length && args [idx] == '[')
					idx += 2; // []
				yield return args.Substring (0, idx);
				foreach (var x in ParseArguments (args.Substring (idx)))
					yield return x;
			} else {
				int tmp = idx + 1;
				int open = 1;
				int end = args.IndexOf ('>', tmp);
				do {
					int midS = args.IndexOf ('<', tmp);
					end = args.IndexOf ('>', tmp);
					if (midS >= 0 && midS < end) {
						open++;
						tmp = midS + 1;
					} else {
						open--;
						tmp = end + 1;
					}
				} while (open > 0);


				idx = end + 1;
				while (idx < args.Length && args [idx] == '[')
					idx += 2; // []
				string gen = args.Substring (0, idx);
				yield return gen;
				if (idx != args.Length)
					foreach (var x in ParseArguments (args.Substring (idx + 1))) // skip '.' (hence +1)
						yield return x;
			}
		}

		public override string ToString ()
		{
			var s = new System.Text.StringBuilder ();
			foreach (var a in Annotations) {
				s.Append ("@").Append (a.Name);
				if (a.Values.Count > 0) {
					s.Append ("(");
					AppendAnnotationValue (a.Values [0]);
					for (int i = 1; i < a.Values.Count; ++i) {
						s.Append (", ");
						AppendAnnotationValue (a.Values [i]);
					}
					s.Append (")");
				}
				s.Append (" ");
			}
			s.Append (TypeName).Append (".").Append (MemberName);
			if (Arguments?.Length > 0) {
				s.Append ("(").Append (Arguments [0]);
				for (int i = 1; i < Arguments.Length; ++i) {
					s.Append (", ").Append (Arguments [i]);
				}
				s.Append (")");
			}
			return s.ToString ();

			void AppendAnnotationValue (AnnotationValue d)
			{
				s.Append (d.Name).Append("=").Append (d.ValueAsArray);
			}
		}
	}	
}
