using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Permissions;

[assembly: AssemblyTitle("Chrono")]
[assembly: AssemblyDescription("A roguelike game.")]
#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif
[assembly: AssemblyProduct("Chrono")]
[assembly: AssemblyCopyright("Copyright 2004 Adam Milazzo")]

[assembly: AssemblyVersion("0.1.*")]