using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCCompilerConsole.Converter
{
    public enum CONSOLE_TEXT
    {
        EXCEPTION,
        NOT_FOUND_FILE,
        MAGIC_SUCCESS,
        MAGIC_FAILED,
        DISASSEMBLE_SUCCESS,
        DISASSEMBLE_FAILED,
    }

    class ConsoleText : ErrorDataBase
    {
        public ConsoleText(string filename) : base(filename)
        {
        }

        public string Str(CONSOLE_TEXT et)
        {
            return base.Str((int)et, "", "", "", "");
        }
        public string Str(CONSOLE_TEXT et, string replace)
        {
            return base.Str((int)et, replace, "", "", "");
        }
        public string Str(CONSOLE_TEXT et, string replace1, string replace2)
        {
            return base.Str((int)et, replace1, replace2, "", "");
        }
        public string Str(CONSOLE_TEXT et, string replace1, string replace2, string replace3)
        {
            return base.Str((int)et, replace1, replace2, replace3, "");
        }
        public string Str(CONSOLE_TEXT et, string replace1, string replace2, string replace3, string replace4)
        {
            return base.Str((int)et, replace1, replace2, replace3, replace4);
        }
    }
}
