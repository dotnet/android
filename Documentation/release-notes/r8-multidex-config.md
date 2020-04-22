### R8 now uses ProguardConfiguration items when code shrinking is disabled

In previous versions, Xamarin.Android did not yet pass `ProguardConfiguration`
items to the R8 tool when the **Code shrinker** setting, corresponding to the
`AndroidLinkTool` MSBuild property, was disabled.

Project authors who added a `--pg-conf` option to the `AndroidR8ExtraArguments`
MSBuild property to work around this limitation in the past can now transition
to the standard `ProguardConfiguration` mechanism.
