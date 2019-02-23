using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Watcher
{
    internal class AppLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public AppLoadContext(string path) :
            base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(path);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            if (path != null)
            {
                // We're loading from Stream because I can't figure out how to make loading from file work and reliably
                // unlock the file after unload, the alternative is to shadow copy somewhere (like temp)
                var assemblyStream = new MemoryStream(File.ReadAllBytes(path));
                Stream assemblySymbols = null;

                var symbolsPath = Path.ChangeExtension(path, ".pdb");
                if (File.Exists(symbolsPath))
                {
                    // Found a symbol next to the dll to load it
                    assemblySymbols = new MemoryStream(File.ReadAllBytes(symbolsPath));
                }

                return LoadFromStream(assemblyStream, assemblySymbols);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (path != null)
            {
                // REVIEW: We're going to have to shadow copy here
                return LoadUnmanagedDllFromPath(path);
            }

            return IntPtr.Zero;
        }
    }
}
