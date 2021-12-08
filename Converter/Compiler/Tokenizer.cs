using System;

namespace MCCompilerConsole.Converter.Compiler
{
    public class Tokenizer
    {
        static private readonly (string word, TokenKind kind)[] ReserveWord =
        {
            ("return", TokenKind.RETURN),
            ("if", TokenKind.IF),
            ("else", TokenKind.ELSE),
            ("while", TokenKind.WHILE),
            ("for", TokenKind.FOR),
            ("switch",TokenKind.SWITCH),
            ("case",TokenKind.CASE),
            ("default",TokenKind.DEFAULT),
            ("sizeof",TokenKind.SIZEOF),
            ("#include",TokenKind.INCLUDE),
            ("#init",TokenKind.INIT),
            ("#main",TokenKind.MAIN),
            ("#magic-onetime",TokenKind.ONETIME),
            ("#magic-repeate",TokenKind.REPEATE),
            ("#skill",TokenKind.SKILL),
            ("struct",TokenKind.STRUCT),
            ("group",TokenKind.STRUCT),
            ("enum",TokenKind.ENUM),
            ("naming",TokenKind.ENUM),
            ("break",TokenKind.BREAK),
            ("continue",TokenKind.CONTINUE),
            ("ref",TokenKind.REFERENCE),

            ("int",TokenKind.TYPE),
            ("float",TokenKind.TYPE),
            ("string",TokenKind.TYPE),
            ("void",TokenKind.TYPE),
            ("boxs",TokenKind.TYPE),
        };

        static private readonly (string word, TokenKind kind)[] BuiltinFunction =
        {
            ("gwst.lib",TokenKind.GWST_LIB),
            ("gwst.mag",TokenKind.GWST_MAG),
            ("gwst.smag",TokenKind.GWST_SMAG),
            ("gwst.ui",TokenKind.GWST_UI),
            ("gwst.meph",TokenKind.GWST_MEPH),
            ("gwst.wamag",TokenKind.GWST_WAMAG),
            ("boxs.allocate",TokenKind.ALLOCATE),
            ("boxs.release",TokenKind.RELEASE),
            ("debug.log",TokenKind.DEBUG_LOG),
            ("debug.pause",TokenKind.DEBUG_PAUSE),
        };

        static private readonly (string word, SystemKind kind)[] BuiltinSystemFunction = 
        {
            ("sys.int.tostr", SystemKind.INT_TO_STRING),
            ("sys.float.tostr", SystemKind.FLOAT_TO_STRING),
            ("sys.string.toint", SystemKind.STRING_TO_INT),
        };


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
        public class Token
        {
            public enum ValueKind
            {
                INT,
                FLOAT,
                INVALID,
            }
            public Token(TokenKind kind = TokenKind.Other, ValueKind valueType = ValueKind.INVALID, int valueI = 0, float valueF = 0.0f, int strIdx = 0, int strLen = 0, Token nextToke = null)
            {
                this.Kind = kind;
                this.SysKind = SystemKind.INVALID;
                this.ValueType = valueType;
                this.ValueI = valueI;
                this.ValueF = valueF;
                this.StrIdx = strIdx;
                this.StrLen = strLen;
                this.Next = nextToke;
            }
            public TokenKind Kind { get; set; }     // 種類
            public SystemKind SysKind { get; set; } // システムコール場合その種類
            public ValueKind ValueType { get; set; }//
            public int ValueI { get; set; }         // kindが整数の場合は値
            public float ValueF { get; set; }       // kindが不動小数点数の場合は値
            public int StrIdx { get; set; }         // トークンの文字位置のインデックス
            public int StrLen { get; set; }         // トークンの文字の長さ
            public Token Next { get; set; }         // 次のトークン
        }

        /// <summary>
        /// トークナイズ結果
        /// </summary>
        public class TokenizeResult : ResultBase
        {
            public override void Initialize()
            {
                base.Initialize();
                HeadToken = null;
            }
            public void Set(bool success, Token headToken)
            {
                this.Success = success;
                this.HeadToken = headToken;
            }

            public Token HeadToken { get; set; }
        }

        /// <summary>
        /// トークナイズの実行
        /// </summary>
        /// <param name="source">トークナイズするファイルの内容</param>
        /// <returns>トークナイズ結果</returns>
        public TokenizeResult Do()
        {
            Initialize();
            currentToken = headToke;

            if (ca.Source == null)
            {
                //Error("ソースファイルが読み込まれていない");
                result.Set(false, null);
                return result;
            }

            int sourceIdx = 0;
            while (sourceIdx < ca.Source.Length)
            {
                //if (result.Success == false)
                //{
                //    return result;
                //}

                char c = ca.Source[sourceIdx];
                // 空白文字はスキップする
                if ((0x09 <= c && c <= 0x0d) || c == 0x20)
                {
                    sourceIdx++;
                    continue;
                }

                // 1行コメントの読み飛ばし
                if (StartsWith(ca.Source, "//", sourceIdx))
                {
                    sourceIdx += 2;
                    char _c = ca.Source[sourceIdx];
                    while(_c != '\n' && sourceIdx < ca.Source.Length)
                    {
                        _c = ca.Source[sourceIdx];
                        sourceIdx++;
                    }
                    continue;
                }

                // 複数行コメントの読み飛ばし
                if (StartsWith(ca.Source, "/*", sourceIdx))
                {
                    sourceIdx += 2; // /*の分
                    while(!StartsWith(ca.Source, "*/", sourceIdx))
                    {
                        sourceIdx++;
                    }
                    sourceIdx += 2; // */の分
                    continue;
                }


                // 複数文字記号
                if (StartsWith(ca.Source, ">=", sourceIdx) || StartsWith(ca.Source, "<=", sourceIdx) || StartsWith(ca.Source, "==", sourceIdx) || StartsWith(ca.Source, "!=", sourceIdx)
                 || StartsWith(ca.Source, "+=", sourceIdx) || StartsWith(ca.Source, "-=", sourceIdx) || StartsWith(ca.Source, "*=", sourceIdx) || StartsWith(ca.Source, "/=", sourceIdx)
                 || StartsWith(ca.Source, "++", sourceIdx) || StartsWith(ca.Source, "--", sourceIdx))
                {
                    currentToken = NewToken(TokenKind.RESERVED, sourceIdx, 2);
                    sourceIdx += 2;
                    continue;
                }


                // 組み込み関数
                bool isBuiltincall = false;
                foreach (var builtin in BuiltinFunction)
                {
                    // システムコールと完全一致
                    // 　-> return (予約語) と returnx (ユーザー定義の識別子)とを分けるため
                    // 　-> 予約語の後の文字が識別子を構成する文字ではないことを確認する
                    if (StartsWith(ca.Source, builtin.word, sourceIdx) && !IsAvilableChr(ca.Source[sourceIdx + builtin.word.Length]))
                    {
                        currentToken = NewToken(builtin.kind, sourceIdx, builtin.word.Length);
                        sourceIdx += builtin.word.Length;
                        isBuiltincall = true;
                        break;
                    }
                }
                if (isBuiltincall)
                {
                    continue;
                }

                // 組み込みシステム関数
                bool isSystemcall = false;
                foreach (var s in BuiltinSystemFunction)
                {
                    // システムコールと完全一致
                    // 　-> return (予約語) と returnx (ユーザー定義の識別子)とを分けるため
                    // 　-> 予約語の後の文字が識別子を構成する文字ではないことを確認する
                    if (StartsWith(ca.Source, s.word, sourceIdx) && !IsAvilableChr(ca.Source[sourceIdx + s.word.Length]))
                    {
                        currentToken = NewToken(TokenKind.SYSTEM, sourceIdx, s.word.Length);
                        currentToken.SysKind = s.kind;
                        sourceIdx += s.word.Length;
                        isSystemcall = true;
                        break;
                    }
                }
                if (isSystemcall)
                {
                    continue;
                }


                // 記号
                if ("+-*/()[]{};:.,<>=&".IndexOf(c) > -1)
                {
                    currentToken = NewToken(TokenKind.RESERVED, sourceIdx, 1);
                    sourceIdx++;
                    continue;
                }

                // 文字列
                if (c == '"')
                {
                    sourceIdx++;
                    int strIdx = sourceIdx;
                    int strLen = GetString(ca.Source, ref sourceIdx);
                    currentToken = NewToken(TokenKind.STRING, strIdx, strLen);
                    continue;
                }

                // 予約語
                bool isReserve = false;
                foreach (var v in ReserveWord)
                {
                    // 予約語と完全一致
                    // 　-> return (予約語) と returnx (ユーザー定義の識別子)とを分けるため
                    // 　-> 予約語の後の文字が識別子を構成する文字ではないことを確認する
                    if (StartsWith(ca.Source, v.word, sourceIdx) && !IsAvilableChr(ca.Source[sourceIdx + v.word.Length]))
                    {
                        currentToken = NewToken(v.kind, sourceIdx, v.word.Length);
                        sourceIdx += v.word.Length;
                        isReserve = true;
                        break;
                    }
                }
                if (isReserve)
                {
                    continue;
                }


                // 整数、数字
                if ('0' <= c && c <= '9')
                {
                    int prevIdx = sourceIdx;
                    currentToken = NewToken(TokenKind.Other, sourceIdx, 0);
                    int valueI = 0;
                    float valueF = 0.0f;
                    currentToken.Kind = GetValue(ca.Source, ref sourceIdx, out valueI, out valueF);
                    currentToken.ValueI = valueI;
                    currentToken.ValueF = valueF;
                    currentToken.StrLen = sourceIdx - prevIdx;                                  // sourceIdxとprevIdxの差異から文字の長さ求める
                    continue;
                }

                // 識別子
                if (IsAlphabet(c))
                {
                    currentToken = NewToken(TokenKind.IDENT, sourceIdx, 0);
                    currentToken.StrLen = GetIdentifier(ca.Source, ref sourceIdx);
                    continue;
                }

                // ※エラー報告
                int errorStartIdx = sourceIdx;
                string erroStr = GetErrorToken(ca.Source, ref sourceIdx);
                Error(ca.ErrorData.Str(ERROR_TEXT.INVALID_TOKEN, erroStr), errorStartIdx, erroStr.Length);
                result.Set(false, null);
                //return result;
            }

            NewToken(TokenKind.EOF, sourceIdx, 0);
            result.Set(!isError, headToke.Next);
            return result;
        }

        /// <summary>
        /// source内のstrIdxから始まる文字がstrと同じか
        /// </summary>
        /// <param name="source">ソース</param>
        /// <param name="str">調べる文字</param>
        /// <param name="strIdx">ソース内のインデックス</param>
        /// <returns>同じ場合はtrue</returns>
        private bool StartsWith(in string source, string str, int strIdx)
        {
            return string.Compare(str, 0, source, strIdx, str.Length) == 0;
        }

        /// <summary>
        /// 指定文字がアルファベットか
        /// </summary>
        /// <param name="c">指定文字</param>
        /// <returns>アルファベットの場合はtrue</returns>
        private bool IsAlphabet(char c)
        {
            return (('A' <= c && c <= 'Z') || ('a' <= c && c <= 'z'));
        }

        /// <summary>
        /// 使用できる文字か
        /// </summary>
        /// <param name="c">調べる文字</param>
        /// <returns>使用できる文字の場合はtrue</returns>
        private bool IsAvilableChr(char c)
        {
            return (IsAlphabet(c) || ('0' <= c && c <= '9') || c == '_');
        }

        /// <summary>
        /// 数値として使用できる文字化
        /// </summary>
        /// <param name="c">調べる文字</param>
        /// <returns>使用できる文字の場合はtrue</returns>
        private bool IsAvailableNumber(char c)
        {
            bool isNumber = '0' <= c && c <= '9';
            bool isHex = 'a' <= c && c <= 'f' || 'A' <= c && c <= 'F';
            bool isFloat = c == '.';
            bool isSuffixFloat = c == 'f' || c == 'F';
            return isNumber || isHex || isFloat || isSuffixFloat ;
        }

        /// <summary>
        /// トークンの作成
        /// </summary>
        /// <param name="kind">トークンの種類</param>
        /// <param name="strIdx">ソース内のインデックス</param>
        /// <param name="strLen">トークンの文字数</param>
        /// <returns></returns>
        private Token NewToken(TokenKind kind, int strIdx, int strLen)
        {
            Token token = new Token(kind: kind, strIdx: strIdx, strLen: strLen, nextToke:null);
            currentToken.Next = token;
            return token;
        }

        /// <summary>
        /// 数の取得
        /// </summary>
        /// <param name="source">ソースファイル</param>
        /// <param name="idx">ソースファイル内のインデックス</param>
        /// <param name="valueI">整数の場合はここに格納される</param>
        /// <param name="valueF">不動小数点の場合はここに格納される</param>
        /// <returns>トークンの種類</returns>
        private TokenKind GetValue(in string source, ref int idx, out int valueI, out float valueF)
        {
            // 接頭辞
            //  2進数 0b
            // 16進数 0x
            TokenKind tk = TokenKind.INTEGER;
            valueI = 0;
            valueF = 0.0f;

            string number = "";
            bool isFloat = false;
            int startIdx = idx;

            BaseNumber fromBase = BaseNumber.Decimal;
            if(idx + 1 < source.Length)
            {
                // 先頭の文字で 2進数か10進数か16進数かわかる
                if (source[idx] == '0')
                {
                    fromBase = source[idx + 1] == 'b' || source[idx + 1] == 'B' ? BaseNumber.Binary : fromBase;
                    fromBase = source[idx + 1] == 'x' || source[idx + 1] == 'X' ? BaseNumber.Hexadecimal : fromBase;
                    if (fromBase != BaseNumber.Decimal)
                    {
                        idx += 2;
                        if(idx >= source.Length)
                        {
                            return tk;
                        }
                    }
                }
            }

            for (char c = source[idx]; idx < source.Length && IsAvailableNumber(c); ++idx, c = source[idx])
            {
                number += c;
                if (c == '.')
                {
                    // 10進数以外は.を使用できない
                    if(fromBase != BaseNumber.Decimal)
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.DEFFERENT_NUMERIC), startIdx, idx - startIdx);
                        return TokenKind.Other;
                    }
                    isFloat = true;
                }

            }
            // 16進数ではなく最後の文字がforFならば不動小数点数
            // 2進数指定だった場合はエラー
            if (number[number.Length - 1] == 'f' || number[number.Length - 1] == 'F')
            {
                if(fromBase == BaseNumber.Decimal)
                {
                    isFloat = true;
                }
                if(fromBase == BaseNumber.Binary)
                {
                    Error(ca.ErrorData.Str(ERROR_TEXT.DEFFERENT_NUMERIC), startIdx, idx - startIdx);
                    return TokenKind.Other;
                }
            }

            try
            {
                if(isFloat)
                {
                    number = number.Replace('f', '\0');
                    number = number.Replace('F', '\0');
                    valueF = Convert.ToSingle(number);
                    tk = TokenKind.FLOAT;
                }
                else
                {
                    valueI = Convert.ToInt32(number, (int)fromBase);
                }
            }
            catch(FormatException)
            {
                // フォーマットが違う
                Error(ca.ErrorData.Str(ERROR_TEXT.DEFFERENT_NUMERIC), startIdx, idx - startIdx);
            }
            catch(OverflowException)
            {
                // MinValue 未満の数値か、MaxValue より大きい数値を表します。
                Error(ca.ErrorData.Str(ERROR_TEXT.TYPE_MIN_MAX), startIdx, idx - startIdx);
            }

            return tk;
        }

        /// <summary>
        /// 識別子の取得
        /// </summary>
        /// <param name="source">ソース</param>
        /// <param name="idx">ソースファイルインデックス。読み進めた分idxも影響を受ける</param>
        /// <returns>識別子の文字数</returns>
        private int GetIdentifier(in string source, ref int idx)
        {
            int count = 0;
            while (idx < source.Length && IsAvilableChr(source[idx]))
            {
                idx++;
                count++;
            }
            return count;
        }

        /// <summary>
        /// 文字列の取得
        /// </summary>
        /// <param name="source">ソース</param>
        /// <param name="idx">ソースファイルのインデックス。読み進めた分idxも影響を受ける</param>
        /// <returns>文字列の文字数</returns>
        private int GetString(in string source, ref int idx)
        {
            int count = 0;
            bool isSuccess = true;
            while (source[idx] != '"')
            {
                if (idx >= source.Length)
                {
                    isSuccess = false;
                    break;
                }
                idx++;
                count++;
            }
            if (!isSuccess) 
            {
                return 0;
            }
            idx++;  // 文字リテラルの最後の"の読み飛ばし
            return count;

        }

        private string GetErrorToken(in string source, ref int idx)
        {
            int startIdx = idx;
            while (!((0x09 != source[idx] && source[idx] <= 0x0d) || source[idx] == 0x20))
            {
                if (idx >= source.Length)
                {
                    break;
                }
                idx++;
            }
            return source.Substring(startIdx, idx - startIdx);
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

        /// <summary>
        /// 初期化処理
        /// </summary>
        private void Initialize()
        {
            headToke = new Token();
            currentToken = null;
            result.Initialize();
            isError = false;
        }

        private enum BaseNumber
        {
            Binary      = 2,
            Decimal     = 10,
            Hexadecimal = 16,
        }

        private Token headToke;             // トークンリストの先頭
        private Token currentToken;         // 現在のトークン
        private TokenizeResult result;      // トークナイズ結果
        private Compailer.CompileArgs ca;   // コンパイルの引数
        private bool isError;
    }
}
