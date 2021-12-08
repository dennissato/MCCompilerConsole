using System;
using System.IO;

namespace MCCompilerConsole.Converter.Assembler
{
    public class Tokenizer
    {
        static public readonly (string word, AsmTokenKind kind)[] ReserveWord =
        {
            (Mnemonic.MOV.ToString(),       AsmTokenKind.MNEMONIC),
            (Mnemonic.MOVI.ToString(),      AsmTokenKind.MNEMONIC),
            (Mnemonic.MOVS.ToString(),      AsmTokenKind.MNEMONIC),
            (Mnemonic.MOVF.ToString(),      AsmTokenKind.MNEMONIC),
            (Mnemonic.ADD.ToString(),       AsmTokenKind.MNEMONIC),
            (Mnemonic.ADDI.ToString(),      AsmTokenKind.MNEMONIC),
            (Mnemonic.ADDS.ToString(),      AsmTokenKind.MNEMONIC),
            (Mnemonic.ADDF.ToString(),      AsmTokenKind.MNEMONIC),
            (Mnemonic.SUB.ToString(),       AsmTokenKind.MNEMONIC),
            (Mnemonic.SUBI.ToString(),      AsmTokenKind.MNEMONIC),
            (Mnemonic.SUBF.ToString(),      AsmTokenKind.MNEMONIC),
            (Mnemonic.IMUL.ToString(),      AsmTokenKind.MNEMONIC),
            (Mnemonic.IMULI.ToString(),     AsmTokenKind.MNEMONIC),
            (Mnemonic.IMULF.ToString(),     AsmTokenKind.MNEMONIC),
            (Mnemonic.IDIV.ToString(),      AsmTokenKind.MNEMONIC),
            (Mnemonic.IDIVI.ToString(),     AsmTokenKind.MNEMONIC),
            (Mnemonic.IDIVF.ToString(),     AsmTokenKind.MNEMONIC),
            (Mnemonic.POP.ToString(),       AsmTokenKind.MNEMONIC),
            (Mnemonic.PUSH.ToString(),      AsmTokenKind.MNEMONIC),
            (Mnemonic.PUSHI.ToString(),     AsmTokenKind.MNEMONIC),
            (Mnemonic.PUSHS.ToString(),     AsmTokenKind.MNEMONIC),
            (Mnemonic.PUSHF.ToString(),     AsmTokenKind.MNEMONIC),
            (Mnemonic.CALL.ToString(),      AsmTokenKind.MNEMONIC),
            (Mnemonic.RET.ToString(),       AsmTokenKind.MNEMONIC),
            (Mnemonic.JMP.ToString(),       AsmTokenKind.MNEMONIC),
            (Mnemonic.JE.ToString(),        AsmTokenKind.MNEMONIC),
            (Mnemonic.JNE.ToString(),       AsmTokenKind.MNEMONIC),
            (Mnemonic.CMP.ToString(),       AsmTokenKind.MNEMONIC),
            (Mnemonic.SETE.ToString(),      AsmTokenKind.MNEMONIC),
            (Mnemonic.SETNE.ToString(),     AsmTokenKind.MNEMONIC),
            (Mnemonic.SETL.ToString(),      AsmTokenKind.MNEMONIC),
            (Mnemonic.SETLE.ToString(),     AsmTokenKind.MNEMONIC),
            (Mnemonic.MEMCOPY.ToString(),       AsmTokenKind.MNEMONIC),
            (Mnemonic.SAVESPS.ToString(),       AsmTokenKind.MNEMONIC),
            (Mnemonic.LOADSPS.ToString(),       AsmTokenKind.MNEMONIC),
            (Mnemonic.GWST_LIB.ToString(),      AsmTokenKind.MNEMONIC),
            (Mnemonic.GWST_MAG.ToString(),      AsmTokenKind.MNEMONIC),
            (Mnemonic.GWST_SMAG.ToString(),     AsmTokenKind.MNEMONIC),
            (Mnemonic.GWST_UI.ToString(),       AsmTokenKind.MNEMONIC),
            (Mnemonic.GWST_MEPH.ToString(),     AsmTokenKind.MNEMONIC),
            (Mnemonic.GWST_WAMAG.ToString(),    AsmTokenKind.MNEMONIC),
            (Mnemonic.SYSTEM.ToString(),        AsmTokenKind.MNEMONIC),
            (Mnemonic.HEAPALLOCATE.ToString(),  AsmTokenKind.MNEMONIC),
            (Mnemonic.HEAPRELEASE.ToString(),   AsmTokenKind.MNEMONIC),
            (Mnemonic.HEAPGET.ToString(),       AsmTokenKind.MNEMONIC),
            (Mnemonic.HEAPSET.ToString(),       AsmTokenKind.MNEMONIC),
            (Mnemonic.DEBUG_LOG.ToString(),     AsmTokenKind.MNEMONIC),
            (Mnemonic.DEBUG_RLOG.ToString(),    AsmTokenKind.MNEMONIC),
            (Mnemonic.DEBUG_SLOG.ToString(),    AsmTokenKind.MNEMONIC),
            (Mnemonic.DEBUG_PUSH.ToString(),    AsmTokenKind.MNEMONIC),
            (Mnemonic.DEBUG_PAUSE.ToString(),   AsmTokenKind.MNEMONIC),
            (Mnemonic.NOP.ToString(),           AsmTokenKind.MNEMONIC),
            (Mnemonic.MAGICEND.ToString(),      AsmTokenKind.MNEMONIC),

            (Operand.ZERO_I.ToString(), AsmTokenKind.OPERAND),
            (Operand.ZERO_S.ToString(), AsmTokenKind.OPERAND),
            (Operand.ZERO_F.ToString(), AsmTokenKind.OPERAND),
            (Operand.RSP.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RBP.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RGP.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RI1.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RI2.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RI3.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RI4.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RI5.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RI6.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RI7.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RI1P.ToString(),   AsmTokenKind.OPERAND),
            (Operand.RS1.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RS2.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RS3.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RS4.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RS5.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RS6.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RF1.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RF2.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RF3.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RF4.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RF5.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RF6.ToString(),    AsmTokenKind.OPERAND),
            (Operand.RHPI.ToString(),   AsmTokenKind.OPERAND),
            (Operand.RHPIS.ToString(),  AsmTokenKind.OPERAND),
        };

        static private readonly (string word, AsmTokenKind kind)[] PreprocessorWord =
        {
            ("#include",AsmTokenKind.INCLUDE),
            ("#magic-onetime",AsmTokenKind.ONETIME),
            ("#magic-repeate",AsmTokenKind.REPEATE),
            ("#skill",AsmTokenKind.SKILL),
        };


        public Tokenizer(Assembler.AssembleArgs aa)
        {
            headToke = null;
            currentToken = null;
            result = new TokenizeResult();
            this.aa = aa;
        }

        /// <summary>
        /// トークン
        /// </summary>
        public class AsmToken
        {
            public enum ValueKind
            {
                INT,
                FLOAT,
                INVALID,
            }
            public AsmToken(AsmTokenKind kind = AsmTokenKind.Other, ValueKind valueType = ValueKind.INVALID, int valueI = 0, float valueF = 0.0f, int strIdx = 0, int strLen = 0, AsmToken nextToke = null)
            {
                this.Kind = kind;
                this.ValueType = valueType;
                this.ValueI = valueI;
                this.ValueF = valueF;
                this.StrIdx = strIdx;
                this.StrLen = strLen;
                this.Next = nextToke;
            }
            public AsmTokenKind Kind { get; set; }  // 種類
            public ValueKind ValueType { get; set; }//
            public int ValueI { get; set; }         // kindが整数の場合は値
            public float ValueF { get; set; }       // kindが不動小数点数の場合は値
            public int StrIdx { get; set; }         // トークンの文字位置のインデックス
            public int StrLen { get; set; }         // トークンの文字の長さ
            public AsmToken Next { get; set; }      // 次のトークン
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
            public void Set(bool success, AsmToken headToken)
            {
                this.Success = success;
                this.HeadToken = headToken;
            }

            public AsmToken HeadToken { get; set; }
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

            if (aa.Source == null)
            {
                //Error("ソースファイルが読み込まれていない");
                result.Set(false, null);
                return result;
            }

            int sourceIdx = 0;
            while (sourceIdx < aa.Source.Length)
            {
                char c = aa.Source[sourceIdx];
                // 空白文字はスキップする
                if ((0x09 <= c && c <= 0x0d) || c == 0x20)
                {
                    sourceIdx++;
                    continue;
                }

                // 1行コメントの読み飛ばし
                if (StartsWith(aa.Source, "//", sourceIdx))
                {
                    sourceIdx += 2;
                    char _c = aa.Source[sourceIdx];
                    while(_c != '\n' && sourceIdx < aa.Source.Length)
                    {
                        _c = aa.Source[sourceIdx];
                        sourceIdx++;
                    }
                    continue;
                }

                // 複数行コメントの読み飛ばし
                if (StartsWith(aa.Source, "/*", sourceIdx))
                {
                    sourceIdx += 2; // /*の分
                    while(!StartsWith(aa.Source, "*/", sourceIdx))
                    {
                        sourceIdx++;
                    }
                    sourceIdx += 2; // */の分
                    continue;
                }

                // 文字列
                if (c == '"')
                {
                    sourceIdx++;
                    int strIdx = sourceIdx;
                    int strLen = GetString(aa.Source, ref sourceIdx);
                    currentToken = NewToken(AsmTokenKind.STRING, strIdx, strLen);
                    continue;
                }

                // 予約語(大文字)
                bool isReserve = false;
                foreach (var v in ReserveWord)
                {
                    // 予約語と完全一致
                    // 　-> return (予約語) と returnx (ユーザー定義の識別子)とを分けるため
                    // 　-> 予約語の後の文字が識別子を構成する文字ではないことを確認する
                    if (IsMatchWord(aa.Source, sourceIdx, v.word))
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
                // 予約語(小文字)
                foreach (var v in ReserveWord)
                {
                    // 予約語と完全一致
                    // 　-> return (予約語) と returnx (ユーザー定義の識別子)とを分けるため
                    // 　-> 予約語の後の文字が識別子を構成する文字ではないことを確認する
                    if (IsMatchWord(aa.Source, sourceIdx, v.word.ToLower()))
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
                // 予約語(プリプロセッサー)
                foreach (var v in PreprocessorWord)
                {
                    // 予約語と完全一致
                    // 　-> return (予約語) と returnx (ユーザー定義の識別子)とを分けるため
                    // 　-> 予約語の後の文字が識別子を構成する文字ではないことを確認する
                    if (IsMatchWord(aa.Source, sourceIdx, v.word))
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
                    currentToken = NewToken(AsmTokenKind.Other, sourceIdx, 0);
                    int valueI = 0;
                    float valueF = 0.0f;
                    currentToken.Kind = GetValue(aa.Source, ref sourceIdx, out valueI, out valueF);
                    currentToken.ValueI = valueI;
                    currentToken.ValueF = valueF;
                    currentToken.StrLen = sourceIdx - prevIdx;                                  // sourceIdxとprevIdxの差異から文字の長さ求める
                    continue;
                }

                // ラベル
                if (IsAlphabet(c))
                {
                    //Path.GetInvalidFileNameChars();
                    currentToken = NewToken(AsmTokenKind.LABEL, sourceIdx, 0);
                    currentToken.StrLen = GetLabel(aa.Source, ref sourceIdx);
                    continue;
                }

                // ※エラー報告
                int errorStartIdx = sourceIdx;
                string erroStr = GetErrorToken(aa.Source, ref sourceIdx);
                Error(aa.ErrorData.Str(ERROR_TEXT.INVALID_TOKEN, erroStr), errorStartIdx, erroStr.Length);
                result.Set(false, null);
            }

            NewToken(AsmTokenKind.EOF, sourceIdx, 0);
            result.Set(!isError, headToke.Next);
            return result;
        }

        /// <summary>
        /// source内のstrIdxから始まる文字がstrと同じか。大文字小文字も判定
        /// </summary>
        /// <param name="source">ソース</param>
        /// <param name="str">調べる文字</param>
        /// <param name="strIdx">ソース内のインデックス</param>
        /// <returns>同じ場合はtrue</returns>
        private bool StartsWith(in string source, string str, int strIdx)
        {
            return string.Compare(str, 0, source, strIdx, str.Length, false) == 0;
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
        /// 指定文字と同じかチェック
        /// </summary>
        /// <param name="source"></param>
        /// <param name="strIdx"></param>
        /// <param name="reserveword"></param>
        /// <returns></returns>
        private bool IsMatchWord(in string source, int strIdx, string reserveword)
        {
            bool ret = StartsWith(aa.Source, reserveword, strIdx);
            if(reserveword.Length + strIdx == source.Length)
            {
                // 予約文字でsourceが終わっている
                return ret;
            }
            else if(reserveword.Length + strIdx < source.Length)
            {
                // 次の文字が有効な文字ではないかチェックして有効でなない場合はOK
                return ret && !IsAvilableChr(source[strIdx + reserveword.Length]);
            }
            return false;
        }

        /// <summary>
        /// トークンの作成
        /// </summary>
        /// <param name="kind">トークンの種類</param>
        /// <param name="strIdx">ソース内のインデックス</param>
        /// <param name="strLen">トークンの文字数</param>
        /// <returns></returns>
        private AsmToken NewToken(AsmTokenKind kind, int strIdx, int strLen)
        {
            AsmToken token = new AsmToken(kind: kind, strIdx: strIdx, strLen: strLen, nextToke:null);
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
        private AsmTokenKind GetValue(in string source, ref int idx, out int valueI, out float valueF)
        {
            // 接頭辞
            //  2進数 0b
            // 16進数 0x
            AsmTokenKind tk = AsmTokenKind.INTEGER;
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
                        Error(aa.ErrorData.Str(ERROR_TEXT.DEFFERENT_NUMERIC), startIdx, idx - startIdx);
                        return AsmTokenKind.Other;
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
                    Error(aa.ErrorData.Str(ERROR_TEXT.DEFFERENT_NUMERIC), startIdx, idx - startIdx);
                    return AsmTokenKind.Other;
                }
            }

            try
            {
                if(isFloat)
                {
                    number = number.Replace('f', '\0');
                    number = number.Replace('F', '\0');
                    valueF = Convert.ToSingle(number);
                    tk = AsmTokenKind.FLOAT;
                }
                else
                {
                    valueI = Convert.ToInt32(number, (int)fromBase);
                }
            }
            catch(FormatException)
            {
                // フォーマットが違う
                Error(aa.ErrorData.Str(ERROR_TEXT.DEFFERENT_NUMERIC), startIdx, idx - startIdx);
            }
            catch(OverflowException)
            {
                // MinValue 未満の数値か、MaxValue より大きい数値を表します。
                Error(aa.ErrorData.Str(ERROR_TEXT.TYPE_MIN_MAX), startIdx, idx - startIdx);
            }

            return tk;
        }

        /// <summary>
        /// ラベルの取得
        /// </summary>
        /// <param name="source">ソース</param>
        /// <param name="idx">ソースファイルインデックス。読み進めた分idxも影響を受ける</param>
        /// <returns>識別子の文字数</returns>
        private int GetLabel(in string source, ref int idx)
        {
            int count = 0;
            while (idx < source.Length && IsAvilableChr(source[idx]))
            {
                idx++;
                count++;
            }
            // ラベルの最後は : 文字
            if(idx < source.Length && source[idx] == ':')
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
            (string linestr, int lineno) = Generic.GetaSourceLineStrNo(aa.Source, strIdx, strLen);
            result.Log = result.Log + (result.Log.Length > 0 ? "\n" : "") + aa.ErrorData.Str(ERROR_TEXT.ERROR_BASE_FILENAME_LINENO, aa.File, $"{lineno:d4}", linestr, str);
            result.Success = false;
            isError = true;
            return;
        }

        /// <summary>
        /// 初期化処理
        /// </summary>
        private void Initialize()
        {
            headToke = new AsmToken();
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

        private AsmToken headToke;          // トークンリストの先頭
        private AsmToken currentToken;      // 現在のトークン
        private TokenizeResult result;      // トークナイズ結果
        private Assembler.AssembleArgs aa;  // コンパイルの引数
        private bool isError;
    }
}
