#### Application and library build and deployment

- [Developer Community 1086457](https://developercommunity.visualstudio.com/content/problem/1086457/index.html):
  Changes to libraries referenced by the .NET Standard library in a default
  Xamarin.Forms project were not reflected in the running app without a clean
  rebuild.  More generally, this issue affected any library referenced
  indirectly via a .NET Standard library that had the
  `ProduceReferenceAssembly` MSBuild property set to `true`.
