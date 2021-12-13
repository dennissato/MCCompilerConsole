using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace MCCompilerConsole.Converter.Compiler
{
    public class Compailer
    {
        public Compailer()
        {
            compileArgs = new CompileArgs();

            preprocessor = new Preprocessor(compileArgs);
            tokenizer = new Tokenizer(compileArgs);
            parser = new Parser(compileArgs);
            codeGenerater = new CodeGenerater(compileArgs);
            linker = new Linker(compileArgs);
            errorData = new ErrorData(@"\error_text_compile.csv");
            result = new CompileResult();
        }

        public class CompileResult : ResultBase
        {

        }

        public class CompileArgs
        {
            public string MCASDir { get; set; }
            public string File { get; set; }
            public byte[] Source { get; set; }
            public Tokenizer Tokenizer { get; set; }
            public Parser Parser { get; set; }
            public Linker Linker { get; set; }
            public ErrorData ErrorData { get; set; }
            public MagicConfig Config { get; set; }

            public void Init()
            {
                MCASDir = null;
                File = null;
                Source = null;
                Config = MagicConfig.None;
            }

            public string GetSourceStr(int idx, int len)
            {
                if(Source == null)
                {
                    return "";
                }
                return Encoding.UTF8.GetString(Source, idx, len);
            }
        }

        /// <summary>
        /// 指定ファイルをコンパイルする
        /// </summary>
        /// <param name="sorceFileName">コンパイルするファイル名</param>
        /// <returns>コンパイル成功したかどうか</returns>
        public CompileResult Do(string sourceFileName, string asmFileDir, bool isRelease)
        {
            Initialize();

            try
            {
                // 使用するファイル名、ディレクトリ名作成
                string fullPath = Path.GetFullPath(sourceFileName);
                string directory = Path.GetDirectoryName(Path.GetFullPath(sourceFileName));
                string assemblyDirectory = Path.GetDirectoryName(Path.GetFullPath(asmFileDir));
                string defineFileName = $"define_{Path.GetFileNameWithoutExtension(fullPath)}_msc{Define.McasEx}";
                string defineFilePath = assemblyDirectory + @"\" + defineFileName;

                // ディレクトリ作成
                Directory.CreateDirectory(assemblyDirectory);
                compileArgs.MCASDir = assemblyDirectory + @"\";

                // プリプロセッサ
                Preprocessor.PreprocessorResult preprocessorResult = preprocessor.Do(fullPath);
                if (!preprocessorResult.Success)
                {
                    Error(preprocessorResult.Log);
                    result.Success = false;
                    return result;
                }

                Uri sourceFileUri = new Uri(directory + @"\");
                int offset = 0;
                List<string> outFiles = new List<string>();
                List<string> includeFiles = new List<string>();
                foreach (var file in preprocessorResult.Files)
                {
                    // ファイル読み込み
                    if (!File.Exists(file.fileName))
                    {
                        // ファイルが見つからない

                        Error(errorData.Str(ERROR_TEXT.NOT_EXIT_FILE, file.fileName));
                        result.Success = false;
                        continue;
                    }
                    compileArgs.File = file.fileName;
                    compileArgs.Source = File.ReadAllBytes(file.fileName);

                    // トークナイズ
                    TokenizeResult tokenizeResult = tokenizer.Do();
                    if (!tokenizeResult.Success)
                    {
                        // TODO プリプロセッサで一通りTokenizeしてるはず
                        // Tokenizeでエラーが出てればpreprocessorの時点で終了しているはず
                        Error(tokenizeResult.Log);
                        result.Success = false;
                    }

                    // 作成されたトークンリストから構文木作成
                    Parser.ParseResult parserResult = parser.Do(tokenizeResult);
                    if (!parserResult.Success)
                    {
                        // エラー出ていた場合は中間ファイルを作成しない
                        // nodeがnullの場合があるので
                        Error(parserResult.Log);
                        result.Success = false;
                        continue;
                    }

                    // 元ファイルからの相対パスを変換してファイル名にする
                    // そうすることで一意のファイル名になるはず
                    Uri codeGenFileUri = new Uri(file.fileName);
                    Uri relativeUri = sourceFileUri.MakeRelativeUri(codeGenFileUri);
                    string relativePath = relativeUri.ToString();
                    relativePath = relativePath.Replace('/', '_');
                    relativePath = relativePath.Replace('.', 'x');
                    // 作成された構文木から中間ファイルの作成
                    bool isBaseFile = fullPath == file.fileName;   // 元になるファイルにはエピローグをつける
                    if (isBaseFile)
                    {   // ベースファイルは相対パス名をつける必要はない
                        relativePath = "";
                    }
                    CodeGenerater.CodeGenResult codeGenResult = codeGenerater.Do(file.fileName, relativePath, parserResult, isBaseFile, defineFileName, isRelease);
                    if (!codeGenResult.Success)
                    {
                        Error(codeGenResult.Log);
                        result.Success = false;
                    }
                    outFiles.Add(codeGenResult.OutFileName);
                    includeFiles.Add(relativePath + Path.GetFileNameWithoutExtension(file.fileName) + Define.McasEx);
                    offset += codeGenResult.OutFileSize;
                }

                // エラー出ていたら終了
                if (!result.Success)
                {
                    compileArgs.Source = null;
                    return result;
                }

                // include ファイルの作成
                using (StreamWriter defSw = File.CreateText(defineFilePath))
                {
                    foreach (var file in includeFiles)
                    {
                        defSw.Write($"#include  \"{file}\"\n");
                    }
                }

                compileArgs.Source = null;// 読み込んだソースファイルはもういらない

                result.File = outFiles[0];  // 必ず0番目が元ソースファイルになる
                result.Success = true;
                return result;
            }
            catch (Exception e)
            {
                // 例外命
                Error(compileArgs.ErrorData.Str(ERROR_TEXT.UNEXPECTED_EXCEPTION, e.GetType().ToString()));

                result.Success = false;
                return result;
            }
        }

        /// <summary>
        /// コンパイルに使用する変数の初期化
        /// </summary>
        private void Initialize()
        {
            // コンパイル始めるにあたっての初期化を行う
            result.Initialize();
            parser.Initialize();
            linker.Initialize();

            compileArgs.Init();
            compileArgs.Tokenizer = tokenizer;
            compileArgs.Parser = parser;
            compileArgs.Linker = linker;
            compileArgs.ErrorData = errorData;
        }

        /// <summary>
        /// コンパイルのエラー
        /// 呼び出す度にログがたまっていく
        /// </summary>
        /// <param name="errorStr">エラー内容</param>
        private void Error(string errorStr)
        {
            result.Log = result.Log + (result.Log.Length > 0 ? "\n" : "") + errorStr;
            return;
        }

        private Tokenizer tokenizer;        // トークナイザー
        private Preprocessor preprocessor;  // プリプロセッサー
        private Parser parser;              // パーサー
        private CodeGenerater codeGenerater;// コードジェネレーター
        private Linker linker;              // リンカー
        private ErrorData errorData;        // エラーデータ
        private CompileArgs compileArgs;    // コンパイル時によく使用する引数達
        private CompileResult result;       // エラー
    }
}