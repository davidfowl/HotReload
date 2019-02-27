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
                // Try to load this assembly from the default context
                Assembly defaultAssembly = null;
                try
                {
                    defaultAssembly = Default.LoadFromAssemblyName(assemblyName);
                }
                catch
                {
                    // This sucks but it's the only "easy" way besides storing a list of things in the default context
                }


                // Nothing in the default context, use this assembly
                if (defaultAssembly != null)
                {
                    var appAssemblyName = AssemblyName.GetAssemblyName(path);

                    // If the local assembly overrides the one in the default load context (version is higher), then it wins
                    if (appAssemblyName.Version <= defaultAssembly.GetName().Version)
                    {
                        return defaultAssembly;
                    }
                }

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
