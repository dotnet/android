//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// Copyright (C) Lluis Sanchez Gual, 2004
//

#if !MONOTOUCH
using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;

namespace Mono.CodeGeneration
{
	[RequiresUnreferencedCode (MonoAndroidExport.DynamicFeatures)]
	internal class CodeBlock: CodeItem
	{
		ArrayList statements = new ArrayList ();
		
		public void Add (CodeItem code)
		{
			statements.Add (code);
		}
		
		public bool IsEmpty
		{
			get { return statements.Count == 0; }
		}
		
		public static CodeBlock operator+(CodeBlock cb, CodeExpression e)
		{
			cb.Add (e);
			return cb;
		}
		
		public CodeItem GetLastItem ()
		{
			return (CodeItem) statements [statements.Count - 1];
		}
		
		public override void Generate (ILGenerator gen)
		{
			foreach (CodeItem item in statements) {
				if (item is CodeExpression)
					((CodeExpression)item).GenerateAsStatement (gen);
				else
					item.Generate (gen);
			}
		}
		
		public override void PrintCode (CodeWriter cp)
		{
			foreach (CodeItem item in statements) {
				cp.BeginLine ();
				item.PrintCode (cp);
				cp.Write (";");
				cp.EndLine ();
			}
		}
	}
	
	[RequiresUnreferencedCode (MonoAndroidExport.DynamicFeatures)]
	internal class CodePop: CodeStatement
	{
		public override void Generate (ILGenerator gen)
		{
			gen.Emit (OpCodes.Pop);
		}
		
		public override void PrintCode (CodeWriter cp)
		{
		}
	}
}
#endif
