using System.Runtime.CompilerServices;

using LargeMauiApp.Pages;

namespace LargeMauiApp;

public static class PageCatalog
{
	static readonly Func<ContentPage> [] factories = [
		static () => new Page01 (),
		static () => new Page02 (),
		static () => new Page03 (),
		static () => new Page04 (),
		static () => new Page05 (),
		static () => new Page06 (),
		static () => new Page07 (),
		static () => new Page08 (),
		static () => new Page09 (),
		static () => new Page10 (),
		static () => new Page11 (),
		static () => new Page12 (),
		static () => new Page13 (),
		static () => new Page14 (),
		static () => new Page15 (),
		static () => new Page16 (),
		static () => new Page17 (),
		static () => new Page18 (),
		static () => new Page19 (),
		static () => new Page20 (),
		static () => new Page021 (),
		static () => new Page022 (),
		static () => new Page023 (),
		static () => new Page024 (),
		static () => new Page025 (),
		static () => new Page026 (),
		static () => new Page027 (),
		static () => new Page028 (),
		static () => new Page029 (),
		static () => new Page030 (),
		static () => new Page031 (),
		static () => new Page032 (),
		static () => new Page033 (),
		static () => new Page034 (),
		static () => new Page035 (),
		static () => new Page036 (),
		static () => new Page037 (),
		static () => new Page038 (),
		static () => new Page039 (),
		static () => new Page040 (),
		static () => new Page041 (),
		static () => new Page042 (),
		static () => new Page043 (),
		static () => new Page044 (),
		static () => new Page045 (),
		static () => new Page046 (),
		static () => new Page047 (),
		static () => new Page048 (),
		static () => new Page049 (),
		static () => new Page050 (),
		static () => new Page051 (),
		static () => new Page052 (),
		static () => new Page053 (),
		static () => new Page054 (),
		static () => new Page055 (),
		static () => new Page056 (),
		static () => new Page057 (),
		static () => new Page058 (),
		static () => new Page059 (),
		static () => new Page060 (),
		static () => new Page061 (),
		static () => new Page062 (),
		static () => new Page063 (),
		static () => new Page064 (),
		static () => new Page065 (),
		static () => new Page066 (),
		static () => new Page067 (),
		static () => new Page068 (),
		static () => new Page069 (),
		static () => new Page070 (),
		static () => new Page071 (),
		static () => new Page072 (),
		static () => new Page073 (),
		static () => new Page074 (),
		static () => new Page075 (),
		static () => new Page076 (),
		static () => new Page077 (),
		static () => new Page078 (),
		static () => new Page079 (),
		static () => new Page080 (),
		static () => new Page081 (),
		static () => new Page082 (),
		static () => new Page083 (),
		static () => new Page084 (),
		static () => new Page085 (),
		static () => new Page086 (),
		static () => new Page087 (),
		static () => new Page088 (),
		static () => new Page089 (),
		static () => new Page090 (),
		static () => new Page091 (),
		static () => new Page092 (),
		static () => new Page093 (),
		static () => new Page094 (),
		static () => new Page095 (),
		static () => new Page096 (),
		static () => new Page097 (),
		static () => new Page098 (),
		static () => new Page099 (),
		static () => new Page100 (),
	];

	public static int Count => factories.Length;

	public static ContentPage Create (int index)
	{
		if ((uint) index >= factories.Length) {
			throw new ArgumentOutOfRangeException (nameof (index));
		}

		return factories [index] ();
	}

	public static void PrepareForNavigation ()
	{
		foreach (Type type in typeof (PageCatalog).Assembly.GetTypes ()) {
			if (!typeof (ContentPage).IsAssignableFrom (type)) {
				continue;
			}

			foreach (var method in type.GetMethods (
				System.Reflection.BindingFlags.Instance |
				System.Reflection.BindingFlags.NonPublic |
				System.Reflection.BindingFlags.Public |
				System.Reflection.BindingFlags.DeclaredOnly)) {
				RuntimeHelpers.PrepareMethod (method.MethodHandle);
			}
		}
	}
}
