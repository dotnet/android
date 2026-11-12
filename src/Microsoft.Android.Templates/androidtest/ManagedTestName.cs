using System.Globalization;
using System.Reflection;

namespace AndroidTest1;

static class ManagedTestName
{
    public static bool Matches(MethodInfo method, string signature, int arity)
    {
        var parameters = method.GetParameters();
        var candidate = parameters.Length == 0 ? method.Name
            : $"{method.Name}({string.Join(",", parameters.Select(parameter => GetTypeName(parameter.ParameterType)))})";
        return candidate == signature && method.GetGenericArguments().Length == arity;
    }

    public static string GetTypeName(Type type)
    {
        if (type.IsArray)
        {
            var element = type.GetElementType() ?? throw new InvalidOperationException("An array has no element type.");
            return $"{GetTypeName(element)}[{new string(',', type.GetArrayRank() - 1)}]";
        }
        if (type.IsGenericParameter)
            return $"{(type.DeclaringMethod is null ? "!" : "!!")}{type.GenericParameterPosition}";
        if (type.IsPointer)
        {
            var element = type.GetElementType() ?? throw new InvalidOperationException("A pointer has no element type.");
            return $"{GetTypeName(element)}*";
        }

        var name = GetDefinitionName(type);
        var arguments = type.GetGenericArguments();
        return arguments.Length == 0 ? name : $"{name}<{string.Join(",", arguments.Select(GetTypeName))}>";
    }

    static string GetDefinitionName(Type type)
    {
        var declaringType = type.DeclaringType;
        int outerArity = declaringType?.GetGenericArguments().Length ?? 0;
        int arity = type.IsGenericType ? type.GetGenericArguments().Length - outerArity : 0;
        var name = EscapeIdentifier(type.Name, arity);
        if (declaringType is not null)
            return $"{GetDefinitionName(declaringType)}+{name}";
        if (type.Namespace is null)
            return name;
        return $"{string.Join(".", type.Namespace.Split('.').Select(part => EscapeIdentifier(part, 0, isNamespace: true)))}.{name}";
    }

    static string EscapeIdentifier(string name, int arity, bool isNamespace = false)
    {
        string suffix = "";
        int tick = name.LastIndexOf('`');
        if (tick > 0 && int.TryParse(name.AsSpan(tick + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedArity)
            && parsedArity == arity)
        {
            suffix = parsedArity > 0 ? $"`{parsedArity.ToString(CultureInfo.InvariantCulture)}" : "";
            name = name[..tick];
        }

        bool valid = true;
        int brackets = 0;
        for (int i = 0; i < name.Length; i++)
        {
            char character = name[i];
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            bool identifierCharacter = !(i == 0 && char.IsDigit(character)) &&
                (char.IsLetterOrDigit(character) || character == '_' || category is UnicodeCategory.LetterNumber
                or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.ConnectorPunctuation or UnicodeCategory.Format);
            if (identifierCharacter || (!isNamespace && character == '.'))
                continue;
            if (!isNamespace && i > 0 && character == '<')
                brackets++;
            else if (!isNamespace && i > 0 && character == '>' && name[i - 1] != '<' && brackets > 0)
                brackets--;
            else
                valid = false;
        }
        return valid && brackets == 0 ? name + suffix : "'" + name.Replace("\\", "\\\\").Replace("'", "\\'") + "'" + suffix;
    }
}
