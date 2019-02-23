using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Watcher
{
    public class ProjectOptions
    {
        public string DotNetPath { get; set; }
        public string ProjectPath { get; set; }
        public string[] Args { get; set; }
        public string DllPath { get; set; }
        public object ProjectName { get; set; }
    }
}
