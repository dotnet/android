// This class represents common code paths we always want to AOT

// DateTime.Now
// string interpolation & split
// int.Parse(), int.ToString()
// Culture-aware string comparisons
// ResourceManager
// System.Threading.Tasks.Task
// System.Net.Http.HttpClient

// Opt out of this warning, because we actually *want* culture-aware string behavior in the AOT profile
#pragma warning disable CA1310

static class CommonMethods
{
    // Returns '200 OK' if the caller wants to set that on the UI
    public static async Task<string> Invoke()
    {
        // NOTE: alternate web services if one of these is down
        //var url = $"https://httpstat.us/{200}";
        var url = $"https://httpbin.org/status/{200}";

        var now = DateTime.Now;
        var foo = "foo";
        foo.StartsWith("f");
        foo.Contains("o");
        var split = "foo;bar".Split(';');
        var x = int.Parse("999");
        x.ToString();

        string someString = AndroidProfiledAot.Resources.Strings.SomeString;

        using var client = new HttpClient();
        var send = client.SendAsync (new HttpRequestMessage (HttpMethod.Get, url));
        var getstring = client.GetStringAsync (url);
        await Task.WhenAll (send, getstring, Task.CompletedTask, Task.Delay(1));
        var text = getstring.Result;

        return text;
    }
}

#pragma warning restore CA1310
