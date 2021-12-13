using System;
using System.Globalization;
using System.Text;

namespace MCCompilerConsole.Converter
{

    /// <summary>
    /// トークン
    /// </summary>
    public class TokenBase
    {
        public enum ValueKind
        {
            INT,
            FLOAT,
            INVALID,
        }
        public TokenBase(int kind, ValueKind valueType = ValueKind.INVALID, int valueI = 0, float valueF = 0.0f, int strIdx = 0, int strLen = 0, TokenBase nextToke = null)
        {
            this._kind = kind;
            this.ValueType = valueType;
            this.ValueI = valueI;
            this.ValueF = valueF;
            this.StrIdx = strIdx;
            this.StrLen = strLen;
            this.Next = nextToke;
        }
        public int _kind { get; set; }          // 種類
        public ValueKind ValueType { get; set; }//
        public int ValueI { get; set; }         // kindが整数の場合は値
        public float ValueF { get; set; }       // kindが不動小数点数の場合は値
        public int StrIdx { get; set; }         // トークンの文字位置のインデックス
        public int StrLen { get; set; }         // トークンの文字の長さ
        public TokenBase Next { get; set; }         // 次のトークン
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
        public void Set(bool success, TokenBase headToken)
        {
            this.Success = success;
            this.HeadToken = headToken;
        }

        public TokenBase HeadToken { get; set; }
    }

    public class TokenizerBase
    {
        protected enum BaseNumber
        {
            Binary = 2,
            Decimal = 10,
            Hexadecimal = 16,
        }

        protected enum GetNumericError
        {
            None,
            Success,
            FormatDifferent,
            MinOrMax,
        }

        protected TokenBase headToke;     // トークンリストの先頭
        protected TokenBase currentToken; // 現在のトークン
        protected TokenizeResult result;  // トークナイズ結果
        protected StringInfo strInfo;
        protected int strInfoIdx;
        protected int sourceIdx;
        protected bool isError;

        static private readonly string[] Numerices =
        {
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
        };

        static private readonly string[] AvailableNumricesCharacter =
        {
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
            "a", "A", "b" , "B", "c", "C", "d", "D", "e", "E", "f", "F",
            ".", "_"
        };

        static private readonly string[] BinaryNumbers =
        {
            "0", "1", "_"
        };

        static private readonly string[] DecimalNumbers =
        {
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
        };

        static private readonly string[] HexadecimalNumbers =
        {
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
            "a", "A", "b" , "B", "c", "C", "d", "D", "e", "E", "f", "F",
        };

        static private readonly string[] FloatNumbers =
        {
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", ".", "f", "F"
        };

        static private readonly string[] NewLine =
        {
            "\n", "\r", "\r\n"
        };

        static private readonly string[] AsciiControlCharacter =
        {
            ((char)0x09).ToString(), ((char)0x0a).ToString(), ((char)0x0b).ToString(),
            ((char)0x0c).ToString(), ((char)0x0d).ToString(), ((char)0x20).ToString(),
        };

        protected bool isExisArray(string[] array, string str)
        {
            foreach (var s in array)
            {
                if (s == str)
                {
                    return true;
                }
            }
            return false;
        }
        protected bool IsNumerices(string str) { return isExisArray(Numerices, str); }
        protected bool IsAvailableNumricesCharacter(string str) { return isExisArray(AvailableNumricesCharacter, str); }
        protected bool IsBinaryNumber(string str) { return isExisArray(BinaryNumbers, str); }
        protected bool IsDecimalNumber(string str) { return isExisArray(DecimalNumbers, str); }
        protected bool IsHexadecimalNumber(string str) { return isExisArray(HexadecimalNumbers, str); }
        protected bool IsFloatNumbers(string str) { return isExisArray(FloatNumbers, str); }
        protected bool IsNewLine(string str) { return isExisArray(NewLine, str); }
        protected bool IsAcsiiControlCharacter(string str) { return isExisArray(AsciiControlCharacter, str); }

        protected delegate bool Delimiter(string str);

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="ca"></param>
        public TokenizerBase()
        {
            headToke = null;
            currentToken = null;
            result = new TokenizeResult();
            strInfo = null;
            strInfoIdx = 0;
            sourceIdx = 0;
            isError = false;
        }

        /// <summary>
        /// SetInfoの設定
        /// </summary>
        /// <param name="bytes">設定Bytes</param>
        protected void SetStrInfo(byte[] bytes)
        {
            string utf8Source = Encoding.UTF8.GetString(bytes);
            strInfo = new StringInfo(utf8Source);
        }

        /// <summary>
        /// トークンの作成
        /// </summary>
        /// <param name="kind">トークンの種類</param>
        /// <param name="strIdx">ソース内のインデックス</param>
        /// <param name="strLen">トークンの文字数</param>
        /// <returns></returns>
        virtual protected TokenBase NewToken(int kind, int strIdx, int strLen)
        {
            return null;
        }

        /// <summary>
        /// 数の取得
        /// </summary>
        /// <param name="valueI">整数の場合はここに格納される</param>
        /// <param name="valueF">不動小数点の場合はここに格納される</param>
        /// <returns>トークンの種類</returns>
        protected (GetNumericError error, TokenBase token) GetNumeric(string str)
        {
            if (IsNumerices(str))
            {
                TokenBase token = NewToken(0, sourceIdx, 0);

                // 接頭辞
                //  2進数 0b
                // 16進数 0x
                TokenBase.ValueKind valueKind = TokenBase.ValueKind.INT;
                int valueI = 0;
                float valueF = 0.0f;

                string number = "";
                bool isFloat = false;

                BaseNumber fromBase = BaseNumber.Decimal;
                string str2 = GetStrInfoStringTwo();
                if (str2 != "")
                {
                    // 先頭の文字で 2進数か10進数か16進数かわかる
                    if (str2 == "0b" || str2 == "0B")
                    {
                        // 2進数
                        fromBase = BaseNumber.Binary;
                        NextStrInfo(2); // 二文字文進める
                    }
                    else if (str2 == "0x" || str2 == "0X")
                    {
                        // 16進数
                        fromBase = BaseNumber.Hexadecimal;
                        NextStrInfo(2); // 二文字文進める
                    }
                }

                //  2進数は 0 & 1 & _
                // 10進数は 0 ～ 9
                // 16進数は 0 ～ 9 & a ～ F
                //  floatは 0 ～ 0 & .
                bool isError = false;
                string lastStr = "";
                for (str = lastStr = GetStrInfoStringOne(); IsAvailableNumricesCharacter(str); NextStrInfo(1), str = lastStr = GetStrInfoStringOne())
                {
                    number += str;
                    if (isFloat)
                    {
                        isError = !IsFloatNumbers(str);
                    }
                    else
                    {
                        if (str == ".")
                        {
                            if (fromBase == BaseNumber.Decimal)
                            {
                                isFloat = true;
                                continue;
                            }
                            isError = true;
                        }
                        if (str == "f" || str == "F")
                        {
                            if (fromBase == BaseNumber.Decimal)
                            {
                                isFloat = true;
                                continue;
                            }
                            else if (fromBase == BaseNumber.Hexadecimal)
                            {
                                continue;
                            }
                            isError = true;
                        }
                        switch (fromBase)
                        {
                            case BaseNumber.Binary: isError = !IsBinaryNumber(str); break;
                            case BaseNumber.Decimal: isError = !IsDecimalNumber(str); break;
                            case BaseNumber.Hexadecimal: isError = !IsHexadecimalNumber(str); break;
                        }
                    }
                    if (isError)
                    {
                        token.StrLen = sourceIdx - token.StrIdx;
                        return (GetNumericError.FormatDifferent, token);
                    }

                }
                // 16進数ではなく最後の文字が「f」or「F」ならば不動小数点数
                // 2進数指定だった場合はエラー
                if (lastStr == "f" || lastStr == "F")
                {
                    if (fromBase == BaseNumber.Decimal)
                    {
                        isFloat = true;
                    }
                    if (fromBase == BaseNumber.Binary)
                    {
                        return (GetNumericError.FormatDifferent, null);
                    }
                }

                try
                {
                    if (isFloat)
                    {
                        number = number.Replace('f', '\0');
                        number = number.Replace('F', '\0');
                        valueF = Convert.ToSingle(number);
                        valueKind = TokenBase.ValueKind.FLOAT;
                    }
                    else
                    {
                        valueI = Convert.ToInt32(number, (int)fromBase);
                    }
                }
                catch (FormatException)
                {
                    // フォーマットが違う
                    token.StrLen = sourceIdx - token.StrIdx;
                    return (GetNumericError.FormatDifferent, token);
                }
                catch (OverflowException)
                {
                    // MinValue 未満の数値か、MaxValue より大きい数値を表します。
                    token.StrLen = sourceIdx - token.StrIdx;
                    return (GetNumericError.MinOrMax, token);
                }

                token.ValueType = valueKind;
                token.ValueI = valueI;
                token.ValueF = valueF;
                token.StrLen = sourceIdx - token.StrIdx;  // sourceIdxとtoken.StrIdxの差異から文字の長さ求める
                return (GetNumericError.Success, token);
            }
            return (GetNumericError.None, null);
        }

        /// <summary>
        /// Asciiの制御文字とスペースの読み飛ばし
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        protected bool SkipSpaceControlChar(string str)
        {
            // 空白・制御・改行文字はスキップする
            if (IsAcsiiControlCharacter(str) || IsNewLine(str))
            {
                NextStrInfo(1);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 1行コメントの読み飛ばし
        /// </summary>
        /// <param name="str">開始文字</param>
        /// <returns>true:1行コメントを読み飛ばした</returns>
        protected bool SkipLineComment(string str)
        {
            // 1行コメントの読み飛ばし
            if (str == "//")
            {
                NextStrInfo(2);     // 二文字文進む
                str = GetStrInfoStringOne();
                while (!IsNewLine(str) && str != "")
                {
                    NextStrInfo(1); // 一文字文進む
                    str = GetStrInfoStringOne();
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// ブロックコメントの読み飛ばし
        /// </summary>
        /// <param name="str">開始文字</param>
        /// <returns>true:ブロックコメントを読み飛ばした</returns>
        protected bool SkipBlockComment(string str)
        {
            // 複数行コメントの読み飛ばし
            if (str == "/*")
            {
                NextStrInfo(2);     // /*の二文字文進む
                str = GetStrInfoStringTwo();
                while (str != "*/" && str != "")
                {
                    NextStrInfo(1);     // 一文字文進む
                    str = GetStrInfoStringTwo();
                }
                NextStrInfo(2);     // */の二文字文進む
                return true;
            }
            return false;
        }

        /// <summary>
        /// 指定予約語なのかチェック
        /// </summary>
        /// <param name="array">指定予約語達</param>
        /// <param name="delimiter">区切り文字判定関数</param>
        /// <returns>arrayの番号、-1:見つからなかった</returns>
        protected int GetReserveWords((string word, int kind)[] array, Delimiter delimiter)
        {
            for(int i = 0; i < array.Length; i++)
            {
                // 予約語と完全一致
                // 　-> return (予約語) と returnx (ユーザー定義の識別子)とを分けるため
                // 　-> 予約語の後の文字が識別子を構成する文字ではないことを確認する
                string word = GetStrInfoString(array[i].word.Length);
                string nextchar = GetStrInfoString(strInfoIdx + array[i].word.Length, 1);
                if (word == array[i].word && delimiter(nextchar))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 識別子の取得
        /// </summary>
        /// <returns>識別子の文字数</returns>
        protected TokenBase GetIdentifier(Delimiter delimiter, string str)
        {
            if (!delimiter(str))
            {
                currentToken = NewToken(0, sourceIdx, 0);
                int strLen = 0;
                {
                    str = GetStrInfoStringOne();
                    while (!IsAcsiiControlCharacter(str) && !delimiter(str) && str != "")
                    {
                        strLen += GetStrByteLength(str);
                        NextStrInfo(1);
                        str = GetStrInfoStringOne();
                    }
                }
                currentToken.StrLen = strLen;
                return currentToken;
            }
            return null;
        }

        /// <summary>
        /// 文字列の取得
        /// </summary>
        /// <returns>文字列の文字数（バイト）</returns>
        protected TokenBase GetString(string str)
        {
            // 文字列
            if (str == "\"")
            {
                NextStrInfo(1);
                int startIdx = sourceIdx;
                int strLen = 0;
                {
                    str = GetStrInfoStringOne();
                    while (str != "\"" && str != "")
                    {
                        strLen += GetStrByteLength(str);
                        NextStrInfo(1);
                        str = GetStrInfoStringOne();
                    }
                    if (str == "")
                    {
                        strLen = 0;
                    }
                    NextStrInfo(1); // "文字文進める(文字列の終わり"まで読み進める)
                }
                return NewToken(0, startIdx, strLen);
            }
            return null;
        }

        protected string GetStrInfoStringOne()
        {
            return GetStrInfoString(1);
        }
        protected string GetStrInfoStringTwo()
        {
            return GetStrInfoString(2);
        }
        protected string GetStrInfoString(int length)
        {
            return GetStrInfoString(strInfoIdx, length);
        }
        protected string GetStrInfoString(int idx, int length)
        {
            int Max = Math.Min(idx + length, strInfo.LengthInTextElements);
            int len = Max - idx;
            if (idx < 0 || idx >= strInfo.LengthInTextElements)
            {
                return "";
            }
            if (idx + len > strInfo.LengthInTextElements)
            {
                return "";
            }
            return strInfo.SubstringByTextElements(idx, len);
        }

        /// <summary>
        /// 次の文字に進む
        /// </summary>
        /// <param name="next"></param>
        protected void NextStrInfo(int next)
        {
            string str = GetStrInfoString(next);
            int byteLength = GetStrByteLength(str);
            strInfoIdx += next;
            sourceIdx += byteLength;
        }

        /// <summary>
        /// UTF8文字のバイトサイズを取得
        /// </summary>
        /// <param name="str">UTF8文字</param>
        /// <returns>バイトサイズ</returns>
        protected int GetStrByteLength(string str)
        {
            return Encoding.UTF8.GetBytes(str).Length;
        }

        /// <summary>
        /// エラートークンの取得
        /// </summary>
        /// <param name="source"></param>
        /// <param name="idx"></param>
        /// <returns></returns>
        protected string GetErrorToken()
        {
            string str = GetStrInfoStringOne();
            while (str != "")
            {
                // 空白文字はスキップする
                if (IsAcsiiControlCharacter(str))
                {
                    NextStrInfo(1);
                    str = GetStrInfoStringOne();
                    continue;
                }
                break;
            }
            return str;

        }

        ///// <summary>
        ///// トークナイズのエラー
        ///// </summary>
        ///// <param name="str">エラー内容</param>
        ///// <param name="strIdx">トークンのソース内インデックス</param>
        ///// <param name="strLen">トークンの文字数</param>
        //private void Error(string str, int strIdx, int strLen)
        //{
        //    (string linestr, int lineno) = Generic.GetaSourceLineStrNo(ca.Source, strIdx, strLen);
        //    result.Log = result.Log + (result.Log.Length > 0 ? "\n" : "") + ca.ErrorData.Str(ERROR_TEXT.ERROR_BASE_FILENAME_LINENO, ca.File, $"{lineno:d4}", linestr, str);
        //    result.Success = false;
        //    isError = true;
        //    return;
        //}

        /// <summary>
        /// 初期化処理
        /// </summary>
        protected void Initialize(TokenBase token)
        {
            headToke = token;
            currentToken = null;
            result.Initialize();
            isError = false;

            strInfo = null;
            strInfoIdx = 0;
            sourceIdx = 0;
        }
    }
}