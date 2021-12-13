using System.IO;
using System.Collections.Generic;

namespace MCCompilerConsole.Converter.Assembler
{
    class Preprocessor
    {
        public Preprocessor(Assembler.AssembleArgs aa)
        {
            currentToken = null;
            result = new PreprocessorResult();
            this.aa = aa;
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
                    //Error(ca.ErrorData.Str(ERROR_TEXT.NOT_EXIT_FILE, openFile));
                    result.Files[i] = (result.Files[i].fileName, true);
                    continue;
                }

                string openFileDirectory = Path.GetDirectoryName(openFile);
                aa.File = openFile;
                aa.Source = File.ReadAllBytes(openFile);

                // -- 元ファイルのinclude処理 --
                _tokenizerResult = aa.Tokenizer.Do();
                if (!_tokenizerResult.Success)
                {
                    Error(_tokenizerResult.Log);
                    result.Files[i] = (result.Files[i].fileName, true);
                }

                Processing(openFileDirectory, _tokenizerResult, i == 0);
                if (!result.Success)
                {
                    // 何かしら失敗している
                    Error(aa.ErrorData.Str(ERROR_TEXT.ST_PREPROCESSOR_ERROR, openFile));
                    result.Files[i] = (result.Files[i].fileName, true);
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
        private void Processing(in string directory, TokenizeResult tokenizeResult, bool isAssembleFile)
        {
            currentToken = tokenizeResult.HeadToken as Tokenizer.AsmToken;
            while (!AtEOF())
            {
                // インクルードファイルの処理
                if (Include(directory))
                {
                    continue;
                }
                // 大元ファイルの場合
                // init,main関数やmagic,skill、onetime,repeateの取得をする
                if (isAssembleFile)
                {
                    // 魔法タイプの取得
                    if (Type())
                    {
                        continue;
                    }
                    // 実行回数の取得
                    if (Execution())
                    {
                        continue;
                    }
                }
                if (Labe())
                {
                    continue;
                }
                NextToken();
            }
        }

        /// <summary>
        /// #includeの読み込み処理
        /// </summary>
        /// <param name="ca">共通引数</param>
        /// <param name="directory">処理をするファイルがあったディレクトリ</param>
        /// <returns></returns>
        private bool Include(in string directory)
        {
            Tokenizer.AsmToken includeTk = Consume(AsmTokenKind.INCLUDE);
            if (includeTk != null)
            {
                Tokenizer.AsmToken fileToken = Consume(AsmTokenKind.STRING);
                if (fileToken != null)
                {
                    // 処理をするファイルがあったディレクトリと指定ファイル名から
                    // フルパスかしてそのファイルを開く
                    string fileName = aa.GetSourceStr(fileToken.StrIdx, fileToken.StrLen);
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
                    Error(aa.ErrorData.Str(ERROR_TEXT.NOT_EXIT_FILE_NAME_INCLUDE), includeTk.StrIdx, includeTk.StrLen);
                    return false;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// mcのタイプ取得
        /// </summary>
        /// <returns></returns>
        private bool Type()
        {
            Tokenizer.AsmToken tk = Consume(AsmTokenKind.SKILL);
            if(tk != null)
            {
                // skill type
                aa.Config |= MagicConfig.Skill;
                return true;
            }

            return false;
        }

        /// <summary>
        /// mcの実行回数取得
        /// </summary>
        /// <returns></returns>
        private bool Execution()
        {
            Tokenizer.AsmToken tk = Consume(AsmTokenKind.ONETIME);
            if (tk != null)
            {
                // One Time
                aa.Config |= MagicConfig.Magic;
                aa.Config |= MagicConfig.OneTime;
                return true;
            }
            tk = Consume(AsmTokenKind.REPEATE);
            if (tk != null)
            {
                // Repeate
                aa.Config |= MagicConfig.Magic;
                aa.Config |= MagicConfig.Repeate;
                return true;
            }
            return false;
        }

        /// <summary>
        /// ラベルの登録
        /// </summary>
        /// <returns></returns>
        private bool Labe()
        {
            Tokenizer.AsmToken tk = Consume(AsmTokenKind.LABEL);
            if (tk != null)
            {
                string str = aa.GetSourceStr(tk.StrIdx, tk.StrLen - 1); // :文字をつけない為に-1している
                (bool success, int index) = aa.Linker.TemporaryRegistration(str);
                if(!success)
                {
                    // ラベルの登録失敗(すでに登録済み)
                    Error(aa.ErrorData.Str(ERROR_TEXT.ALREADY_DEFINE_LABEL, str));
                }
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
            return currentToken.Kind == AsmTokenKind.EOF;
        }

        /// <summary>
        /// 現在のトークンが指定トークンだった場合は
        /// 次のトークンに行く
        /// </summary>
        /// <param name="tk">トークンの種類</param>
        /// <returns>指定トークンだった場合はそのトークン</returns>
        private Tokenizer.AsmToken Consume(AsmTokenKind tk)
        {
            if (currentToken.Kind != tk)
            {
                return null;
            }
            Tokenizer.AsmToken token = currentToken;
            currentToken = currentToken.Next as Tokenizer.AsmToken;
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
            currentToken = currentToken.Next as Tokenizer.AsmToken;
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
            (string linestr, int lineno) = Generic.GetaSourceLineStrNo(aa.Source, strIdx, strLen);
            Error(aa.ErrorData.Str(ERROR_TEXT.ERROR_BASE_FILENAME_LINENO, aa.File, $"{lineno:d4}", linestr, errorStr));
        }

        private Tokenizer.AsmToken currentToken;// 現在のトークン
        private PreprocessorResult result;      // 実行結果
        private Assembler.AssembleArgs aa;
    }
}
