// This class represents common code paths we always want to AOT

// string interpolation & split
// int.Parse(), int.ToString()
// Culture-aware string comparisons
// ResourceManager
// System.Reflection
// System.Threading.Tasks.Task
// System.Net.Http.HttpClient

// Opt out of this warning, because we actually *want* culture-aware string behavior in the AOT profile
#pragma warning disable CA1310

using System.Reflection;

static class CommonMethods
{
    // Returns '200 OK' if the caller wants to set that on the UI
    public static async Task<string> Invoke()
    {
        var url = $"https://httpstat.us/{200}";

        var foo = "foo";
        foo.StartsWith("f");
        foo.Contains("o");
        var split = "foo;bar".Split(';');
        var x = int.Parse("999");
        x.ToString();

        string someString = AndroidProfiledAot.Resources.Strings.SomeString;

        var type = typeof (CommonMethods);
        var method = type.GetMethod (nameof (FromReflection), BindingFlags.NonPublic | BindingFlags.Static);
        method.Invoke (null, null);

        using var client = new HttpClient();
        var send = client.SendAsync (new HttpRequestMessage (HttpMethod.Get, url));
        var getstring = client.GetStringAsync (url);
        await Task.WhenAll (send, getstring, Task.CompletedTask, Task.Delay(1));
        var text = getstring.Result;

        return text;
    }

    static void FromReflection () { }
}

#pragma warning restore CA1310
