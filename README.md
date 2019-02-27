# HotReload

This is an experiment to see what `dotnet watch` would look like if we could unload the application without stopping the process. The idea is that we'd use unloadable AssemblyLoadContext to load the application while 
the hosting process would be in another application context.
