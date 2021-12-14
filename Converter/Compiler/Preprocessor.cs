using System.IO;
using System.Collections.Generic;

namespace MCCompilerConsole.Converter.Compiler
{
    class Preprocessor
    {
        public Preprocessor(Compailer.CompileArgs ca)
        {
            currentToken = null;
            result = new PreprocessorResult();
            this.ca = ca;
        }

        /// <summary>
        /// プリプロセッサーの実行結果
        /// </summary>
        public class PreprocessorResult : ResultBase
        {
            public PreprocessorResult()
            {
                Files = new List<(string fileName, bool isError)>();
            }
            public override void Initialize()
            {
                base.Initialize();
                Files.Clear();
            }

            public List<(string fileName, bool isError)> Files { get; set; }
        }

        /// <summary>
        /// プリプロセスの実行
        /// </summary>
        /// <param name="sourceFileName">プリプロセスをするファイル名</param>
        /// <returns>プリプロセスの実行結果</returns>
        public PreprocessorResult Do(string sourceFileName)
        {
            Initialize();
            string fullPath = Path.GetFullPath(sourceFileName);
            result.Files.Add((fullPath, false));

            TokenizeResult _tokenizerResult;
            Parser.ParseResult _parseResult;

            //////////////////////////////////////////
            // 最初の登録は元ファイルのみ
            // そこからどのファイルをincludeするか見つけて
            // 見つけたファイルのincludeを見つける
            for (int i = 0; i < result.Files.Count; i++)
            {
                string openFile = result.Files[i].fileName;  // hullPathを持っている
                if (!File.Exists(openFile))
                {
                    // 存在しないファイルが指定されているか
                    // ファイルの指定方法が間違っています。
                    Error(ca.ErrorData.Str(ERROR_TEXT.NOT_EXIT_FILE, openFile));
                    result.Files[i] = (result.Files[i].fileName, true);
                    continue;
                }

                string openFileDirectory = Path.GetDirectoryName(openFile);
                ca.File = openFile;
                ca.Source = File.ReadAllBytes(openFile);

                // -- 元ファイルのinclude処理 --
                _tokenizerResult = ca.Tokenizer.Do();
                if (!_tokenizerResult.Success)
                {
                    Error(_tokenizerResult.Log);
                    result.Files[i] = (result.Files[i].fileName, true);
                    //return result;
                }

                _parseResult = ca.Parser.DefineStructEnum(_tokenizerResult);
                if (!_parseResult.Success)
                {
                    Error(_parseResult.Log);
                    result.Files[i] = (result.Files[i].fileName, true);
                    //return result;
                }

                Processing(ca, openFileDirectory, _tokenizerResult, i == 0);
                if (!result.Success)
                {
                    // 何かしら失敗している
                    Error(ca.ErrorData.Str(ERROR_TEXT.ST_PREPROCESSOR_ERROR, openFile));
                    result.Files[i] = (result.Files[i].fileName, true);
                    //return result;
                }

            }

            // パーサーに構造体定義がincludeファイル分たまっているので
            // 構造体のリサイズと定義の確認をしてもらう
            _parseResult = ca.Parser.StructCheck();
            if (!_parseResult.Success)
            {
                Error(_parseResult.Log);
            }

            //////////////////////////////////////////
            /// 関数定義とグローバル変数定義をする
            for (int i = 0; i < result.Files.Count; i++)
            {
                string openFile = result.Files[i].fileName;  // hullPathを持っている
                if (!File.Exists(openFile))
                {
                    if (result.Files[i].isError == false)
                    {
                        // 存在しないファイルが指定されているか
                        // ファイルの指定方法が間違っています。
                        Error(ca.ErrorData.Str(ERROR_TEXT.NOT_EXIT_FILE, openFile));
                        result.Files[i] = (result.Files[i].fileName, true);
                    }
                    continue;
                }

                ca.File = openFile;
                ca.Source = File.ReadAllBytes(openFile);

                // -- 元ファイルのinclude処理 --
                // トークナイズする
                _tokenizerResult = ca.Tokenizer.Do();
                if (!_tokenizerResult.Success && result.Files[i].isError == false)
                {
                    // エラー原因は1回目と同じはずなので
                    // 2回以上エラー表示はいらない
                    Error(_tokenizerResult.Log);
                    //return result;
                }

                // 関数定義とグルーバル変数定義をしておく
                _parseResult = ca.Parser.DefineFuncGlobalVar(_tokenizerResult);
                if (!_parseResult.Success)
                {
                    Error(_parseResult.Log);
                    //return result;
                }
            }

            //////////////////////////////////////////
            // グローバル変数のサイズを決定する
            for (int i = 0; i < result.Files.Count; i++)
            {
                string openFile = result.Files[i].fileName;  // hullPathを持っている
                if (!File.Exists(openFile))
                {
                    if (result.Files[i].isError == false)
                    {
                        // 存在しないファイルが指定されているか
                        // ファイルの指定方法が間違っています。
                        Error(ca.ErrorData.Str(ERROR_TEXT.NOT_EXIT_FILE, openFile));
                        result.Files[i] = (result.Files[i].fileName, true);
                    }
                    continue;
                }

                ca.File = openFile;
                ca.Source = File.ReadAllBytes(openFile);

                // -- 元ファイルのinclude処理 --
                // トークナイズする
                _tokenizerResult = ca.Tokenizer.Do();
                if (!_tokenizerResult.Success && result.Files[i].isError == false)
                {
                    // エラー原因は1回目と同じはずなので
                    // 2回以上エラー表示はいらない
                    Error(_tokenizerResult.Log);
                    //return result;
                }

                // 関数定義とグルーバル変数定義をしておく
                _parseResult = ca.Parser.FixGlobalArraySize(_tokenizerResult);
                if (!_parseResult.Success)
                {
                    Error(_parseResult.Log);
                    //return result;
                }
            }

            return result;
        }

        /// <summary>
        /// プリプロセス処理
        /// </summary>
        /// <param name="ca">コンパイル引数</param>
        /// <param name="directory">処理するファイルのディレクトリ</param>
        /// <param name="tokenizeResult">トークナイズ結果</param>
        /// <param name="isCompileFile">大元のコンパイルファイル</param>
        private void Processing(in Compailer.CompileArgs ca, in string directory, TokenizeResult tokenizeResult, bool isCompileFile)
        {
            currentToken = tokenizeResult.HeadToken as Tokenizer.Token;
            while (!AtEOF())
            {
                if (Include(ca, directory))
                {
                    continue;
                }
                // 大元ファイルの場合
                // init,main関数やmagic,skill、onetime,repeateの取得をする
                if (isCompileFile)
                {
                    // 元ファイルから init,main関数の指定を取得
                    if (Func())
                    {
                        continue;
                    }
                    // スキルの取得
                    if (Skill())
                    {
                        continue;
                    }
                    // 実行回数の取得
                    if (Execution())
                    {
                        continue;
                    }

                }
                NextToken();
            }
        }

        /// <summary>
        /// #includeの読み込み処理
        /// </summary>
        /// <param name="ca">共通引数</param>
        /// <param name="directory">処理をするファイルがあったディレクトリ</param>
        /// <returns>ture:読み込み処理をした</returns>
        private bool Include(in Compailer.CompileArgs ca, in string directory)
        {
            Tokenizer.Token includeTk = Consume(TokenKind.INCLUDE);
            if (includeTk != null)
            {
                Tokenizer.Token fileToken = Consume(TokenKind.STRING);
                if (fileToken != null)
                {
                    // 処理をするファイルがあったディレクトリと指定ファイル名から
                    // フルパスかしてそのファイルを開く
                    string fileName = ca.GetSourceStr(fileToken.StrIdx, fileToken.StrLen);
                    string fullPath = Path.GetFullPath(fileName, directory);
                    (string name, bool isError) = result.Files.Find(files => files.fileName == fullPath);
                    if (name == null)
                    {
                        // 一覧に無ければ追加
                        result.Files.Add((fullPath, false));
                    }
                }
                else
                {
                    Error(ca.ErrorData.Str(ERROR_TEXT.NOT_EXIT_FILE_NAME_INCLUDE), includeTk.StrIdx, includeTk.StrLen);
                    return false;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// #init
        /// #main
        /// の読み込み処理
        /// </summary>
        /// <returns>true:読み込み処理をした</returns>
        private bool Func()
        {
            Tokenizer.Token tk = Consume(TokenKind.MAIN);
            if (tk != null)
            {
                tk = Consume(TokenKind.STRING);
                if (tk != null)
                {
                    // 文字数を見る必要がある
                    if (tk.StrLen > 0)
                    {
                        ca.Linker.FuncMain = ca.GetSourceStr(tk.StrIdx, tk.StrLen);
                        return true;
                    }
                }
                Error(ca.ErrorData.Str(ERROR_TEXT.NOT_EXIT_FILE_NAME_MAIN), tk.StrIdx, tk.StrLen);
                return false;
            }
            tk = Consume(TokenKind.INIT);
            if (tk != null)
            {
                tk = Consume(TokenKind.STRING);
                if (tk != null)
                {
                    // 文字数を見る必要がある
                    if (tk.StrLen > 0)
                    {
                        ca.Linker.FuncInit = ca.GetSourceStr(tk.StrIdx, tk.StrLen);
                        return true;
                    }
                }
                Error(ca.ErrorData.Str(ERROR_TEXT.NOT_EXIT_FILE_NAME_INIT), tk.StrIdx, tk.StrLen);
                return false;
            }
            return false;
        }

        /// <summary>
        /// スキルの取得
        /// </summary>
        /// <returns>true:取得した</returns>
        private bool Skill()
        {
            Tokenizer.Token tk = Consume(TokenKind.SKILL);
            if(tk != null)
            {
                // skill type
                ca.Config |= MagicConfig.Skill;
                return true;
            }

            return false;
        }

        /// <summary>
        /// mcの実行回数取得
        /// </summary>
        /// <returns>true:取得した</returns>
        private bool Execution()
        {
            Tokenizer.Token tk = Consume(TokenKind.ONETIME);
            if (tk != null)
            {
                // One Time
                ca.Config |= MagicConfig.Magic;
                ca.Config |= MagicConfig.OneTime;
                return true;
            }
            tk = Consume(TokenKind.REPEATE);
            if (tk != null)
            {
                // Repeate
                ca.Config |= MagicConfig.Magic;
                ca.Config |= MagicConfig.Repeate;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 現在のトークンが最後かどうか
        /// </summary>
        /// <returns>トークンが最後かどうか</returns>
        private bool AtEOF()
        {
            return currentToken.Kind == TokenKind.EOF;
        }

        /// <summary>
        /// 現在のトークンが指定トークンだった場合は
        /// 次のトークンに行く
        /// </summary>
        /// <param name="tk">トークンの種類</param>
        /// <returns>指定トークンだった場合はそのトークン</returns>
        private Tokenizer.Token Consume(TokenKind tk)
        {
            if (currentToken.Kind != tk)
            {
                return null;
            }
            Tokenizer.Token token = currentToken;
            currentToken = currentToken.Next as Tokenizer.Token;
            return token;
        }

        /// <summary>
        /// 現在のトークンを次に進める
        /// 現在のトークンが最後の場合は何もしない。
        /// </summary>
        private void NextToken()
        {
            if (currentToken == null || (currentToken?.Next ?? null) == null)
            {
                // 現在か次がnullの場合は読み進めない
                return;
            }
            if (AtEOF())
            {
                // 最後まで読んでいる場合は何もしない
                return;
            }
            currentToken = currentToken.Next as Tokenizer.Token;
        }

        /// <summary>
        /// 初期化処理
        /// </summary>
        private void Initialize()
        {
            result.Initialize();
        }

        /// <summary>
        /// プリプロセスのエラー
        /// </summary>
        /// <param name="errorStr">エラー内容</param>
        private void Error(string errorStr)
        {
            result.Log = result.Log + (result.Log.Length > 0 ? "\n" : "") + errorStr;
            result.Success = false;
        }

        /// <summary>
        /// プリプロセッサのエラー
        /// エラーのファイル名と行番号も表示
        /// </summary>
        /// <param name="errorStr"></param>
        /// <param name="strIdx"></param>
        /// <param name="strLen"></param>
        private void Error(string errorStr, int strIdx, int strLen)
        {
            (string linestr, int lineno) = Generic.GetaSourceLineStrNo(ca.Source, strIdx, strLen);
            Error(ca.ErrorData.Str(ERROR_TEXT.ERROR_BASE_FILENAME_LINENO, ca.File, $"{lineno:d4}", linestr, errorStr));
        }

        private Tokenizer.Token currentToken;// 現在のトークン
        private PreprocessorResult result;   // 実行結果
        private Compailer.CompileArgs ca;
    }
}
