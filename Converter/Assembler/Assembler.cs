using System;
using System.IO;
using System.Collections.Generic;

namespace MCCompilerConsole.Converter.Assembler
{
    public class Assembler
    {
        public class AssembleResult : ResultBase
        {

        }

        public class AssembleArgs
        {
            public string MCBinDir { get; set; }
            public string MCDir { get; set; }
            public string File { get; set; }
            public string Source { get; set; }
            public Tokenizer Tokenizer { get; set; }
            public Linker Linker { get; set; }
            public ErrorData ErrorData { get; set; }
            public MagicConfig Config { get; set; }

            public void Init()
            {
                MCBinDir = null;
                MCDir = null;
                File = null;
                Source = null;
                Config = MagicConfig.None;
            }
        }

        public Assembler()
        {
            assembleArgs = new AssembleArgs();

            tokenizer = new Tokenizer(assembleArgs);
            preprocessor = new Preprocessor(assembleArgs);
            codeGenerater = new CodeGenerater(assembleArgs);
            linker = new Linker(assembleArgs);
            errorData = new ErrorData(@"\Text\error_text_assemble.csv");
            result = new AssembleResult();
        }

        public AssembleResult Do(string sourceFileName, string binFileDir, string mcFileDir, bool isRelease)
        {
            Initialize();

            try
            {
                // 使用するファイル名、ディレクトリ名作成
                string fullPath = Path.GetFullPath(sourceFileName);
                string binDirectory = Path.GetFullPath(Path.GetFullPath(binFileDir));
                string mcDirectory = Path.GetFullPath(Path.GetFullPath(mcFileDir));

                // ディレクトリ作成
                Directory.CreateDirectory(binDirectory);
                Directory.CreateDirectory(mcDirectory);
                assembleArgs.MCBinDir = binDirectory + @"\";
                assembleArgs.MCDir = mcDirectory + @"\";

                // プリプロセッサ
                Preprocessor.PreprocessorResult preprocessorResult = preprocessor.Do(fullPath);
                if (!preprocessorResult.Success)
                {
                    Error(preprocessorResult.Log);
                    result.Success = false;
                    return result;
                }

                int offset = 0;
                List<string> binFiles = new List<string>();
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
                    assembleArgs.File = file.fileName;
                    assembleArgs.Source = File.ReadAllText(file.fileName, System.Text.Encoding.UTF8);

                    // トークナイズ
                    Tokenizer.TokenizeResult tokenizeResult = tokenizer.Do();
                    if (!tokenizeResult.Success)
                    {
                        // TODO プリプロセッサで一通りTokenizeしてるはず
                        // Tokenizeでエラーが出てればpreprocessorの時点で終了しているはず
                        Error(tokenizeResult.Log);
                        result.Success = false;
                    }

                    // 作成された構文木から中間ファイルの作成
                    bool isEpilog = fullPath == file.fileName;   // 元になるファイルにはエピローグをつける
                    CodeGenerater.CodeGenResult codeGenResult = codeGenerater.Do(file.fileName, offset, tokenizeResult, isEpilog, isRelease);
                    if (!codeGenResult.Success)
                    {
                        Error(codeGenResult.Log);
                        result.Success = false;
                    }
                    if (codeGenResult.OutFileSize > 0)
                    {
                        binFiles.Add(codeGenResult.BinFileName);
                    }
                    offset += codeGenResult.OutFileSize;
                }


                // TODO この時点でエラー出ていたら一旦中止してみる
                if (result.Success == false)
                {
                    return result;
                }

                // 元ファイルのサイズを調べる
                // サイズが1以上なら中間ファイルを一つにする
                // xx.binファイルをまとめて _xx.bin ファイルを作成 _xx.binを元に.mcファイルを作成
                FileInfo fi = new FileInfo(binFiles[0]);   // 元ファイルの中間ファイルがbinFiles[0]にある
                string filenameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFileName);
                string binFileName = assembleArgs.MCBinDir + "_" + filenameWithoutExtension + Define.McbinEx;
                string outFileName = Path.GetFullPath(assembleArgs.MCDir + filenameWithoutExtension + Define.McEx);
                if (fi.Length > 0)
                {
                    using (FileStream fs = new FileStream(binFileName, FileMode.Create))
                    using (BinaryWriter bw = new BinaryWriter(fs))
                    {
                        foreach (var file in binFiles)
                        {
                            if (!File.Exists(file))
                            {
                                // ファイルが存在しない
                                Error(errorData.Str(ERROR_TEXT.LINKER_NOT_INTERMEDIATE_FINE, file));
                                result.Success = false;
                                return result;
                            }
                            bw.Seek(0, SeekOrigin.End);
                            byte[] write = File.ReadAllBytes(file);
                            bw.Write(write);
                        }

                        FileInfo info = new FileInfo(binFileName);
                        if (info.Length > Int32.MaxValue)
                        {
                            Error(errorData.Str(ERROR_TEXT.FILE_SIZE_OVER, $"{Int32.MaxValue}"));
                        }
                    }
                }

                // リンク処理を行い最終成果物を作成
                Linker.LinkResult linkResult = linker.Do(binFileName, outFileName);
                if (!linkResult.Success)
                {
                    Error(binFileName);
                    Error(linkResult.Log);
                    result.Success = false;
                    return result;
                }


                assembleArgs.Source = null;// 読み込んだソースファイルはもういらない

                result.File = outFileName;
                result.Success = true;
                return result;
            }
            catch(Exception e)
            {
                // 例外命
                Error(assembleArgs.ErrorData.Str(ERROR_TEXT.UNEXPECTED_EXCEPTION, e.GetType().ToString()));

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
            linker.Initialize();

            assembleArgs.Init();
            assembleArgs.Tokenizer = tokenizer;
            assembleArgs.Linker = linker;
            assembleArgs.ErrorData = errorData;
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
        private CodeGenerater codeGenerater;// コードジェネレーター
        private Linker linker;              // リンカー
        private ErrorData errorData;        // エラーデータ
        private AssembleArgs assembleArgs;  // アセンブル時によく使用する引数達
        private AssembleResult result;      // エラー
    }
}