using System;
using System.IO;
using MCCompilerConsole.Converter;
using MCCompilerConsole.Converter.Compiler;
using MCCompilerConsole.Converter.Assembler;

namespace MCCompilerConsole
{
    class Program
    {
        private enum Arguments
        {
            ExecutionDirectory = 0, // 実行パス  
            SourceFileName,         // ソースファイル(フルパス)
            LogFile,                // コンパイルのログファイル(フルパス)
            Language,               // 言語指定
            ReleaseDebug,           // リリースかデバック
            CompileMCASDir,         // .mcasファイルの出力先
            CompileMCBINDir,        // .mcbinファイルの出力先
            CompileMCDir,           // .mcdirファイルの出力先
            AssembleMCBINDir,       // .mcbinファイルの出力先
            AssembleMCDir,          // .mcdirファイルの出力先

            MinArgNum = 4,          // Argumentsの数
        }

        private enum ReleaseDebug
        {
            r,  // Release
            d,  // Debug

            Invalid,
        }

        private enum ConvertType
        {
            Compile,
            Assemble,
            DisAssemble,

            Invalid,
        }

        private enum Language
        {
            JPN,
            //ENG,

            Invalid
        }

        static void Main(string[] args)
        {
            if (args.Length < (int)Arguments.MinArgNum)
            {
                return;
            }

            string direcotory = args.Length > (int)Arguments.ExecutionDirectory ? args[(int)Arguments.ExecutionDirectory] : "";
            string sourceFile = args.Length > (int)Arguments.SourceFileName ? args[(int)Arguments.SourceFileName] : "";
            string logFile = args.Length > (int)Arguments.LogFile ? args[(int)Arguments.LogFile] : "";
            string language = args.Length > (int)Arguments.Language ? args[(int)Arguments.Language] : "";
            string releasedebug = args.Length > (int)Arguments.ReleaseDebug ? args[(int)Arguments.ReleaseDebug] : "";
            string compilemcasDir = args.Length > (int)Arguments.CompileMCASDir ? args[(int)Arguments.CompileMCASDir] : "";
            string compilemcbinDir = args.Length > (int)Arguments.CompileMCBINDir ? args[(int)Arguments.CompileMCBINDir] : "";
            string compilemcDir = args.Length > (int)Arguments.CompileMCDir ? args[(int)Arguments.CompileMCDir] : "";
            string assemblebinDir = args.Length > (int)Arguments.AssembleMCBINDir ? args[(int)Arguments.AssembleMCBINDir] : "";
            string assemblemcDir = args.Length > (int)Arguments.AssembleMCDir ? args[(int)Arguments.AssembleMCDir] : "";

            // 言語
            Language lang = Language.Invalid;
            {
                foreach (var value in Enum.GetValues(typeof(Language)))
                {
                    string langstr = Enum.GetName(typeof(Language), value);
                    if (langstr == language)
                    {
                        lang = (Language)value;
                        break;
                    }
                }
                // 言語の取得に失敗した場合は日本語で開始
                if (lang == Language.Invalid)
                {
                    lang = Language.JPN;
                }
            }

            // リリースかデバックか？
            bool isRelease = false;
            {
                ReleaseDebug rd = ReleaseDebug.Invalid;
                foreach (var value in Enum.GetValues(typeof(ReleaseDebug)))
                {
                    string modestr = Enum.GetName(typeof(ReleaseDebug), value);
                    if (modestr == releasedebug)
                    {
                        rd = (ReleaseDebug)value;
                        break;
                    }
                }
                // モード取得に失敗した場合はデバックモードで開始
                if (rd == ReleaseDebug.Invalid)
                {
                    rd = ReleaseDebug.d;
                }
                isRelease = rd == ReleaseDebug.r;
            }

            // 変換タイプの取得
            ConvertType convertType = ConvertType.Invalid;
            {
                string extension = Path.GetExtension(sourceFile);
                if (extension == Define.McsEx)
                {
                    convertType = ConvertType.Compile;
                }
                else if(extension == Define.McasEx)
                {
                    convertType = ConvertType.Assemble;
                }
                else if(extension == Define.McEx)
                {
                    convertType = ConvertType.DisAssemble;
                }
                else
                {
                    // TODOエラー
                    return;
                }
            }

            // フルパス作成
            {
                string dir = Path.GetDirectoryName(sourceFile);
                compilemcasDir = Path.GetFullPath(dir + compilemcasDir + @"\");
                compilemcbinDir = Path.GetFullPath(dir + compilemcbinDir + @"\");
                compilemcDir = Path.GetFullPath(dir + compilemcDir + @"\");
                assemblebinDir = Path.GetFullPath(dir + assemblebinDir + @"\");
                assemblemcDir = Path.GetFullPath(dir + assemblemcDir + @"\");
            }

            try
            {
                // コンパイル&アセンブル
                string log = "";
                switch(convertType)
                {
                    case ConvertType.Compile:
                        log = CompileAssemble(sourceFile, compilemcasDir, compilemcbinDir, compilemcDir, isRelease);
                        break;

                    case ConvertType.Assemble:
                        log = Assemble(sourceFile, assemblebinDir, assemblemcDir, isRelease);
                        break;

                    case ConvertType.DisAssemble:
                        log = Disassembler(sourceFile);
                        break;
                }

                // ログファイルの書き出し
                using(StreamWriter sw = new StreamWriter(logFile, false))
                {
                    sw.Write(log);
                }
            }
            catch(Exception e)
            {
                // ログファイルの書き出し
                using (StreamWriter sw = new StreamWriter(logFile, false))
                {
                    
                    sw.Write(consoleText.Str(CONSOLE_TEXT.EXCEPTION, e.ToString()));
                }
            }
        }

        private static string CompileAssemble(string sourceFile, string mcasDirectory, string binDirectory, string mcDirectory, bool isRelease)
        {
            if (File.Exists(sourceFile) && Path.GetExtension(sourceFile) == Define.McsEx)
            {
                Compailer compiler = new Compailer();
                Assembler assembler = new Assembler();

                // パスの作成
                //var saveData = Properties.Settings.Default;
                string directory = Path.GetDirectoryName(sourceFile);

                // コンパイル
                Compailer.CompileResult compileResult = compiler.Do(sourceFile, mcasDirectory, isRelease);
                if (!compileResult.Success)
                {
                    // コンパイルで失敗
                    return ConvertFialedLog(sourceFile, compileResult.Log);
                }

                // アセンブル
                Assembler.AssembleResult assembleResult = assembler.Do(compileResult.File, binDirectory, mcDirectory, isRelease);
                if (!assembleResult.Success)
                {
                    // アセンブルで失敗
                    return ConvertFialedLog(sourceFile, assembleResult.Log);
                }

                // 成功
                {
                    // assembleResult.File; // 最終成果物のファイル名が入っている
                    return ConvertSuccessLog(sourceFile);
                }
            }
            return ConvertFialedLog(sourceFile, consoleText.Str(CONSOLE_TEXT.NOT_FOUND_FILE, sourceFile));
        }

        private static string Assemble(string sourceFile, string binDirectory, string mcDirectory, bool isRelease)
        {
            if (File.Exists(sourceFile) && Path.GetExtension(sourceFile) == Define.McasEx)
            {
                Assembler assembler = new Assembler();

                //var saveData = Properties.Settings.Default;
                Assembler.AssembleResult assembleResult = assembler.Do(sourceFile, binDirectory, mcDirectory, isRelease);
                if (!assembleResult.Success)
                {
                    // アセンブルで失敗
                    return ConvertFialedLog(sourceFile, assembleResult.Log);
                }

                // 成功
                // assembleResult.File; // 最終成果物のファイル名が入っている
                return ConvertSuccessLog(sourceFile);
            }
            return ConvertFialedLog(sourceFile, consoleText.Str(CONSOLE_TEXT.NOT_FOUND_FILE, sourceFile));
        }

        private static string Disassembler(string sourceFile)
        {
            if (File.Exists(sourceFile))
            {
                // ファイル
                string extension = Path.GetExtension(sourceFile);

                Disassembler disassembler = new Disassembler();
                bool success = disassembler.Do(sourceFile);
                if (success)
                {
                    return consoleText.Str(CONSOLE_TEXT.DISASSEMBLE_SUCCESS);
                }
                else
                {
                    return consoleText.Str(CONSOLE_TEXT.DISASSEMBLE_FAILED);
                }
            }
            
            return ConvertFialedLog(sourceFile, consoleText.Str(CONSOLE_TEXT.NOT_FOUND_FILE, sourceFile));
        }

        private static string ConvertSuccessLog(string sourceFile)
        {
            DateTime dt = DateTime.Now;
            return $"{dt}\n{sourceFile}\n{consoleText.Str(CONSOLE_TEXT.MAGIC_SUCCESS)}";
        }

        private static string ConvertFialedLog(string sourceFile, string faileLog)
        {
            DateTime dt = DateTime.Now;
            return $"{dt}\n{sourceFile}\n{consoleText.Str(CONSOLE_TEXT.MAGIC_FAILED)}\n" + faileLog;
        }

        private static ConsoleText consoleText = new ConsoleText(@"\console_text.csv");
    }
}
