using Irony.Parsing;

namespace Java.Interop.Tools.JavaSource; 

static class IronyExtensions {

	public static void MakePlusRule (this NonTerminal star, Grammar grammar, BnfTerm delimiter)
	{
		star.Rule = grammar.MakePlusRule (star, delimiter);
	}

	public static void MakeStarRule (this NonTerminal star, Grammar grammar, BnfTerm delimiter, BnfTerm of)
	{
		star.Rule = grammar.MakeStarRule (star, delimiter, of);
	}

	public static void MakeStarRule (this NonTerminal star, Grammar grammar, BnfTerm of)
	{
		star.Rule = grammar.MakeStarRule (star, of);
	}
}
