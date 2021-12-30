using System;
using System.Globalization;
using System.Text;

namespace MCCompilerConsole.Converter.Compiler
{
    public class Tokenizer : TokenizerBase
    {
        static private readonly (string word, int kind)[] ReserveWord =
        {
            ("return", (int)TokenKind.RETURN),
            ("if", (int)TokenKind.IF),
            ("else", (int)TokenKind.ELSE),
            ("while", (int)TokenKind.WHILE),
            ("for", (int)TokenKind.FOR),
            ("switch",(int)TokenKind.SWITCH),
            ("case",(int)TokenKind.CASE),
            ("default",(int)TokenKind.DEFAULT),
            ("sizeof",(int)TokenKind.SIZEOF),
            ("#include",(int)TokenKind.INCLUDE),
            ("#init",(int)TokenKind.INIT),
            ("#main",(int)TokenKind.MAIN),
            ("#magic-onetime",(int)TokenKind.ONETIME),
            ("#magic-repeate",(int)TokenKind.REPEATE),
            ("#skill",(int)TokenKind.SKILL),
            ("struct",(int)TokenKind.STRUCT),
            ("group",(int)TokenKind.STRUCT),
            ("enum",(int)TokenKind.ENUM),
            ("naming",(int)TokenKind.ENUM),
            ("break",(int)TokenKind.BREAK),
            ("continue",(int)TokenKind.CONTINUE),
            ("ref",(int)TokenKind.REFERENCE),

            ("int",(int)TokenKind.TYPE),
            ("float",(int)TokenKind.TYPE),
            ("string",(int)TokenKind.TYPE),
            ("void",(int)TokenKind.TYPE),
            ("boxs",(int)TokenKind.TYPE),
        };

        static private readonly (string word, int kind)[] BuiltinFunction =
        {
            ("gwst.lib",(int)TokenKind.GWST_LIB),
            ("gwst.mag",(int)TokenKind.GWST_MAG),
            ("gwst.smag",(int)TokenKind.GWST_SMAG),
            ("gwst.ui",(int)TokenKind.GWST_UI),
            ("gwst.meph",(int)TokenKind.GWST_MEPH),
            ("gwst.wamag",(int)TokenKind.GWST_WAMAG),
            ("boxs.allocate",(int)TokenKind.ALLOCATE),
            ("boxs.release",(int)TokenKind.RELEASE),
            ("debug.log",(int)TokenKind.DEBUG_LOG),
            ("debug.pause",(int)TokenKind.DEBUG_PAUSE),
        };

        static private readonly (string word, int kind)[] BuiltinSystemFunction =
        {
            ("sys.int.tostr", (int)SystemKind.INT_TO_STRING),
            ("sys.float.tostr", (int)SystemKind.FLOAT_TO_STRING),
            ("sys.string.toint", (int)SystemKind.STRING_TO_INT),
        };

        static private readonly string[] NotIdentChar =
        {
            "+", "-", "*", "/", "%", "(", ")", "[", "]", "{", "}", ";", ":", ".", ",", "<", ">", "=", "&",
        };

        static private readonly string[] AssigneChar =
        {
            ">=", "<=", "==", "!=", "+=", "-=", "*=", "/=", "++", "--"
        };

        private bool IsNotIdentChar(string str) { return isExisArray(NotIdentChar, str); }
        private bool IsAssigneChar(string str) { return isExisArray(AssigneChar, str); }

        private bool BuiltinCallDelimiter(string str) { return (IsNotIdentChar(str) || IsControlOrSpace(str)); }

        private Compailer.CompileArgs ca;   // コンパイルの引数


        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="ca"></param>
        public Tokenizer(Compailer.CompileArgs ca)
        {
            headToke = null;
            currentToken = null;
            result = new TokenizeResult();
            this.ca = ca;
        }

        /// <summary>
        /// トークン
        /// </summary>
        public class Token : TokenBase
        {
            public Token(TokenKind kind = TokenKind.Other, ValueKind valueType = ValueKind.INVALID, int valueI = 0, float valueF = 0.0f, int strIdx = 0, int strLen = 0, Token nextToke = null) :
                base((int)kind, valueType, valueI, valueF, strIdx, strLen, nextToke)
            {
                this.SysKind = SystemKind.INVALID;
            }
            public TokenKind Kind
            {
                get => (TokenKind)_kind;
                set => _kind = (int)value;
            }
            public SystemKind SysKind { get; set; } // システムコール場合その種類
        }

        /// <summary>
        /// トークナイズの実行
        /// </summary>
        /// <returns>トークナイズ結果</returns>
        public TokenizeResult Do()
        {
            Initialize(new Token(TokenKind.Other));
            currentToken = headToke;

            if (ca.Source == null)
            {
                //Error("ソースファイルが読み込まれていない");
                result.Set(false, null);
                return result;
            }

            // SetInfoの設定
            SetStrInfo(ca.Source);

            while (sourceIdx < ca.Source.Length)
            {
                string str1 = GetStrInfoStringOne();
                string str2 = GetStrInfoStringTwo();

                // 空白・制御・改行文字はスキップする
                if (SkipSpaceControlChar(str1))
                {
                    continue;
                }

                // 1行コメントの読み飛ばし
                if (SkipLineComment(str2))
                {
                    continue;
                }

                // 複数行コメントの読み飛ばし
                if (SkipBlockComment(str2))
                {
                    continue;
                }

                // 複数文字記号
                if (IsAssigneChar(str2))
                {
                    currentToken = NewToken((int)TokenKind.RESERVED, sourceIdx, GetStrByteLength(str2));
                    NextStrInfo(2);     // 二文字文進む
                    continue;
                }

                // 組み込み関数
                {
                    int builtcall = GetReserveWords(BuiltinFunction, BuiltinCallDelimiter);
                    if (builtcall != -1)
                    {
                        // システムコールと完全一致
                        // 　-> return (予約語) と returnx (ユーザー定義の識別子)とを分けるため
                        // 　-> 予約語の後の文字が識別子を構成する文字ではないことを確認する
                        currentToken = NewToken(BuiltinFunction[builtcall].kind, sourceIdx, BuiltinFunction[builtcall].word.Length);
                        NextStrInfo(BuiltinFunction[builtcall].word.Length);
                        continue;
                    }
                }

                // 組み込みシステム関数
                {
                    int systemcall = GetReserveWords(BuiltinSystemFunction, BuiltinCallDelimiter);
                    if (systemcall != -1)
                    {
                        // システムコールと完全一致
                        // 　-> return (予約語) と returnx (ユーザー定義の識別子)とを分けるため
                        // 　-> 予約語の後の文字が識別子を構成する文字ではないことを確認する
                        Token _currentToken = NewToken((int)TokenKind.SYSTEM, sourceIdx, BuiltinSystemFunction[systemcall].word.Length) as Token;
                        _currentToken.SysKind = (SystemKind)BuiltinSystemFunction[systemcall].kind;
                        currentToken = _currentToken;
                        NextStrInfo(BuiltinFunction[systemcall].word.Length);
                        continue;
                    }
                }

                // 記号
                if (IsNotIdentChar(str1))
                {
                    currentToken = NewToken((int)TokenKind.RESERVED, sourceIdx, GetStrByteLength(str1));
                    NextStrInfo(1);
                    continue;
                }

                // 文字列
                {
                    Token strToken = GetString(str1) as Token;
                    if (strToken != null)
                    {
                        strToken.Kind = TokenKind.STRING;
                        currentToken = strToken;
                        continue;
                    }
                }

                // 予約語
                {
                    int reserve = GetReserveWords(ReserveWord, BuiltinCallDelimiter);
                    if (reserve != -1)
                    {
                        // システムコールと完全一致
                        // 　-> return (予約語) と returnx (ユーザー定義の識別子)とを分けるため
                        // 　-> 予約語の後の文字が識別子を構成する文字ではないことを確認する
                        currentToken = NewToken(ReserveWord[reserve].kind, sourceIdx, ReserveWord[reserve].word.Length);
                        NextStrInfo(ReserveWord[reserve].word.Length);
                        continue;
                    }
                }

                // 整数、数字
                {
                    (GetNumericError error, TokenBase token) = GetNumeric(str1);
                    if (error == GetNumericError.Success)
                    {
                        Token t = token as Token;
                        if(token.ValueType == TokenBase.ValueKind.INT)
                        {
                            t.Kind = TokenKind.INTEGER;
                        }
                        else if(token.ValueType == TokenBase.ValueKind.FLOAT)
                        {
                            t.Kind = TokenKind.FLOAT;
                        }
                        currentToken = token;
                        continue;
                    }
                    else if (error == GetNumericError.FormatDifferent)
                    {
                        // フォーマットが違う
                        Error(ca.ErrorData.Str(ERROR_TEXT.DEFFERENT_NUMERIC), token.StrIdx, token.StrLen);
                        continue;
                    }
                    else if (error == GetNumericError.MinOrMax)
                    {
                        // MinValue 未満の数値か、MaxValue より大きい数値を表します。
                        Error(ca.ErrorData.Str(ERROR_TEXT.TYPE_MIN_MAX), token.StrIdx, token.StrLen);
                        continue;
                    }
                }

                // 識別子
                {
                    Token token = GetIdentifier(IsNotIdentChar, str1) as Token;
                    if (token != null)
                    {
                        token.Kind = TokenKind.IDENT;
                        currentToken = token;
                        continue;
                    }
                }

                // ※エラー報告
                int errorStartIdx = sourceIdx;
                string erroStr = GetErrorToken();
                Error(ca.ErrorData.Str(ERROR_TEXT.INVALID_TOKEN, erroStr), errorStartIdx, GetStrByteLength(erroStr));
                result.Set(false, null);
            }

            NewToken((int)TokenKind.EOF, sourceIdx, 0);
            result.Set(!isError, headToke.Next);
            return result;
        }

        /// <summary>
        /// トークンの作成
        /// </summary>
        /// <param name="kind">トークンの種類</param>
        /// <param name="strIdx">ソース内のインデックス</param>
        /// <param name="strLen">トークンの文字数</param>
        /// <returns></returns>
        override protected TokenBase NewToken(int kind, int strIdx, int strLen)
        {
            Token token = new Token(kind: (TokenKind)kind, strIdx: strIdx, strLen: strLen, nextToke: null);
            currentToken.Next = token;
            return token;
        }

        /// <summary>
        /// トークナイズのエラー
        /// </summary>
        /// <param name="str">エラー内容</param>
        /// <param name="strIdx">トークンのソース内インデックス</param>
        /// <param name="strLen">トークンの文字数</param>
        private void Error(string str, int strIdx, int strLen)
        {
            (string linestr, int lineno) = Generic.GetaSourceLineStrNo(ca.Source, strIdx, strLen);
            result.Log = result.Log + (result.Log.Length > 0 ? "\n" : "") + ca.ErrorData.Str(ERROR_TEXT.ERROR_BASE_FILENAME_LINENO, ca.File, $"{lineno:d4}", linestr, str);
            result.Success = false;
            isError = true;
            return;
        }
    }
}