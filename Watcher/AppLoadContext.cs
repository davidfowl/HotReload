using System.Reflection;
using System.Runtime.Loader;

namespace Watcher
{
    internal class AppLoadContext : AssemblyLoadContext
    {
        public AppLoadContext() :
            base(isCollectible: true)
        {
            
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            return null;
        }
    }
}
