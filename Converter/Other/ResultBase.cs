using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCCompilerConsole.Converter
{
    public class ResultBase
    {
        public virtual void Initialize()
        {
            Success = true;
            File = "";
            ErrorCount = 0;
            Log = "";
        }
        public bool Success { get; set; }
        public string File { get; set; }
        public int ErrorCount { get; set; }
        public string Log { get; set; }
    }
}