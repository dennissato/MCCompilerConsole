using System;
using System.Globalization;
using System.Text;

namespace MCCompilerConsole.Converter.Assembler
{
    public class Tokenizer : TokenizerBase
    {
        static public readonly (string word, int kind)[] ReserveWord =
        {
            (Mnemonic.MOV.ToString(),           (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.MOVI.ToString(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.MOVS.ToString(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.MOVF.ToString(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.ADD.ToString(),           (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.ADDI.ToString(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.ADDS.ToString(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.ADDF.ToString(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.SUB.ToString(),           (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.SUBI.ToString(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.SUBF.ToString(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.IMUL.ToString(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.IMULI.ToString(),         (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.IMULF.ToString(),         (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.IDIV.ToString(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.IDIVI.ToString(),         (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.IDIVF.ToString(),         (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.POP.ToString(),           (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.PUSH.ToString(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.PUSHI.ToString(),         (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.PUSHS.ToString(),         (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.PUSHF.ToString(),         (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.CALL.ToString(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.RET.ToString(),           (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.JMP.ToString(),           (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.JE.ToString(),            (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.JNE.ToString(),           (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.CMP.ToString(),           (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.SETE.ToString(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.SETNE.ToString(),         (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.SETL.ToString(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.SETLE.ToString(),         (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.MEMCOPY.ToString(),       (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.SAVESPS.ToString(),       (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.LOADSPS.ToString(),       (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.GWST_LIB.ToString(),      (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.GWST_MAG.ToString(),      (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.GWST_SMAG.ToString(),     (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.GWST_UI.ToString(),       (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.GWST_MEPH.ToString(),     (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.GWST_WAMAG.ToString(),    (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.SYSTEM.ToString(),        (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.HEAPALLOCATE.ToString(),  (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.HEAPRELEASE.ToString(),   (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.HEAPGET.ToString(),       (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.HEAPSET.ToString(),       (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.DEBUG_LOG.ToString(),     (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.DEBUG_RLOG.ToString(),    (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.DEBUG_SLOG.ToString(),    (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.DEBUG_PUSH.ToString(),    (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.DEBUG_PAUSE.ToString(),   (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.NOP.ToString(),           (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.MAGICEND.ToString(),      (int)AsmTokenKind.MNEMONIC),

            (Operand.ZERO_I.ToString(), (int)AsmTokenKind.OPERAND),
            (Operand.ZERO_S.ToString(), (int)AsmTokenKind.OPERAND),
            (Operand.ZERO_F.ToString(), (int)AsmTokenKind.OPERAND),
            (Operand.RSP.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RBP.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RGP.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RI1.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RI2.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RI3.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RI4.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RI5.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RI6.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RI7.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RI1P.ToString(),   (int)AsmTokenKind.OPERAND),
            (Operand.RS1.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RS2.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RS3.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RS4.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RS5.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RS6.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RF1.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RF2.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RF3.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RF4.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RF5.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RF6.ToString(),    (int)AsmTokenKind.OPERAND),
            (Operand.RHPI.ToString(),   (int)AsmTokenKind.OPERAND),
            (Operand.RHPIS.ToString(),  (int)AsmTokenKind.OPERAND),
        };
        static public readonly (string word, int kind)[] ReserveWordLower =
        {
            (Mnemonic.MOV.ToString().ToLower(),           (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.MOVI.ToString().ToLower(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.MOVS.ToString().ToLower(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.MOVF.ToString().ToLower(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.ADD.ToString().ToLower(),           (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.ADDI.ToString().ToLower(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.ADDS.ToString().ToLower(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.ADDF.ToString().ToLower(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.SUB.ToString().ToLower(),           (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.SUBI.ToString().ToLower(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.SUBF.ToString().ToLower(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.IMUL.ToString().ToLower(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.IMULI.ToString().ToLower(),         (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.IMULF.ToString().ToLower(),         (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.IDIV.ToString().ToLower(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.IDIVI.ToString().ToLower(),         (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.IDIVF.ToString().ToLower(),         (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.POP.ToString().ToLower(),           (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.PUSH.ToString().ToLower(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.PUSHI.ToString().ToLower(),         (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.PUSHS.ToString().ToLower(),         (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.PUSHF.ToString().ToLower(),         (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.CALL.ToString().ToLower(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.RET.ToString().ToLower(),           (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.JMP.ToString().ToLower(),           (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.JE.ToString().ToLower(),            (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.JNE.ToString().ToLower(),           (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.CMP.ToString().ToLower(),           (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.SETE.ToString().ToLower(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.SETNE.ToString().ToLower(),         (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.SETL.ToString().ToLower(),          (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.SETLE.ToString().ToLower(),         (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.MEMCOPY.ToString().ToLower(),       (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.SAVESPS.ToString().ToLower(),       (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.LOADSPS.ToString().ToLower(),       (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.GWST_LIB.ToString().ToLower(),      (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.GWST_MAG.ToString().ToLower(),      (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.GWST_SMAG.ToString().ToLower(),     (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.GWST_UI.ToString().ToLower(),       (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.GWST_MEPH.ToString().ToLower(),     (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.GWST_WAMAG.ToString().ToLower(),    (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.SYSTEM.ToString().ToLower(),        (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.HEAPALLOCATE.ToString().ToLower(),  (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.HEAPRELEASE.ToString().ToLower(),   (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.HEAPGET.ToString().ToLower(),       (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.HEAPSET.ToString().ToLower(),       (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.DEBUG_LOG.ToString().ToLower(),     (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.DEBUG_RLOG.ToString().ToLower(),    (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.DEBUG_SLOG.ToString().ToLower(),    (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.DEBUG_PUSH.ToString().ToLower(),    (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.DEBUG_PAUSE.ToString().ToLower(),   (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.NOP.ToString().ToLower(),           (int)AsmTokenKind.MNEMONIC),
            (Mnemonic.MAGICEND.ToString().ToLower(),      (int)AsmTokenKind.MNEMONIC),

            (Operand.ZERO_I.ToString().ToLower(), (int)AsmTokenKind.OPERAND),
            (Operand.ZERO_S.ToString().ToLower(), (int)AsmTokenKind.OPERAND),
            (Operand.ZERO_F.ToString().ToLower(), (int)AsmTokenKind.OPERAND),
            (Operand.RSP.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RBP.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RGP.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RI1.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RI2.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RI3.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RI4.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RI5.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RI6.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RI7.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RI1P.ToString().ToLower(),   (int)AsmTokenKind.OPERAND),
            (Operand.RS1.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RS2.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RS3.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RS4.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RS5.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RS6.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RF1.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RF2.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RF3.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RF4.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RF5.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RF6.ToString().ToLower(),    (int)AsmTokenKind.OPERAND),
            (Operand.RHPI.ToString().ToLower(),   (int)AsmTokenKind.OPERAND),
            (Operand.RHPIS.ToString().ToLower(),  (int)AsmTokenKind.OPERAND),
        };

        static private readonly (string word, int kind)[] PreprocessorWord =
        {
            ("#include", (int)AsmTokenKind.INCLUDE),
            ("#magic-onetime", (int)AsmTokenKind.ONETIME),
            ("#magic-repeate", (int)AsmTokenKind.REPEATE),
            ("#skill", (int)AsmTokenKind.SKILL),
        };

        private Assembler.AssembleArgs aa;  // コンパイルの引数

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="aa"></param>
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
        public class AsmToken : TokenBase
        {
            public AsmToken(AsmTokenKind kind = AsmTokenKind.Other, ValueKind valueType = ValueKind.INVALID, int valueI = 0, float valueF = 0.0f, int strIdx = 0, int strLen = 0, AsmToken nextToke = null) :
                base((int)kind, valueType, valueI, valueF, strIdx, strLen, nextToke)
            {
            }
            public AsmTokenKind Kind
            {
                get => (AsmTokenKind)_kind;
                set => _kind = (int)value;
            }
        }

        /// <summary>
        /// トークナイズの実行
        /// </summary>
        /// <param name="source">トークナイズするファイルの内容</param>
        /// <returns>トークナイズ結果</returns>
        public TokenizeResult Do()
        {
            Initialize(new AsmToken(AsmTokenKind.Other));
            currentToken = headToke;

            if (aa.Source == null)
            {
                //Error("ソースファイルが読み込まれていない");
                result.Set(false, null);
                return result;
            }

            // SetInfoの設定
            SetStrInfo(aa.Source);

            while (sourceIdx < aa.Source.Length)
            {
                string str1 = GetStrInfoStringOne();
                string str2 = GetStrInfoStringTwo();

                // 空白文字はスキップする
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

                // 文字列
                {
                    AsmToken strToken = GetString(str1) as AsmToken;
                    if (strToken != null)
                    {
                        strToken.Kind = AsmTokenKind.STRING;
                        currentToken = strToken;
                        continue;
                    }
                }

                // 予約語(大文字)
                {
                    int reserve = GetReserveWords(ReserveWord, IsAcsiiControlCharacter);
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
                // 予約語(小文字)
                {
                    int reserve = GetReserveWords(ReserveWordLower, IsAcsiiControlCharacter);
                    if (reserve != -1)
                    {
                        // システムコールと完全一致
                        // 　-> return (予約語) と returnx (ユーザー定義の識別子)とを分けるため
                        // 　-> 予約語の後の文字が識別子を構成する文字ではないことを確認する
                        currentToken = NewToken(ReserveWordLower[reserve].kind, sourceIdx, ReserveWordLower[reserve].word.Length);
                        NextStrInfo(ReserveWordLower[reserve].word.Length);
                        continue;
                    }
                }

                // 予約語(プリプロセッサー)
                {
                    int preprocess = GetReserveWords(PreprocessorWord, IsAcsiiControlCharacter);
                    if (preprocess != -1)
                    {
                        // システムコールと完全一致
                        // 　-> return (予約語) と returnx (ユーザー定義の識別子)とを分けるため
                        // 　-> 予約語の後の文字が識別子を構成する文字ではないことを確認する
                        currentToken = NewToken(PreprocessorWord[preprocess].kind, sourceIdx, PreprocessorWord[preprocess].word.Length);
                        NextStrInfo(PreprocessorWord[preprocess].word.Length);
                        continue;
                    }
                }

                // 整数、数字
                {
                    (GetNumericError error, TokenBase token) = GetNumeric(str1);
                    if (error == GetNumericError.Success)
                    {
                        AsmToken t = token as AsmToken;
                        if (token.ValueType == TokenBase.ValueKind.INT)
                        {
                            t.Kind = AsmTokenKind.INTEGER;
                        }
                        else if (token.ValueType == TokenBase.ValueKind.FLOAT)
                        {
                            t.Kind = AsmTokenKind.FLOAT;
                        }
                        currentToken = token;
                        continue;
                    }
                    else if (error == GetNumericError.FormatDifferent)
                    {
                        // フォーマットが違う
                        Error(aa.ErrorData.Str(ERROR_TEXT.DEFFERENT_NUMERIC), token.StrIdx, token.StrLen);
                        continue;
                    }
                    else if (error == GetNumericError.MinOrMax)
                    {
                        // MinValue 未満の数値か、MaxValue より大きい数値を表します。
                        Error(aa.ErrorData.Str(ERROR_TEXT.TYPE_MIN_MAX), token.StrIdx, token.StrLen);
                        continue;
                    }
                }

                // ラベル
                {
                    AsmToken token = GetLabel(str1);
                    if (token != null)
                    {
                        if (token.StrLen == 0)
                        {
                            // ラベルの最後が:文字ではない
                            //Error()
                        }
                        currentToken = token;
                        continue;
                    }
                }

                // ※エラー報告
                int errorStartIdx = sourceIdx;
                string erroStr = GetErrorToken();
                Error(aa.ErrorData.Str(ERROR_TEXT.INVALID_TOKEN, erroStr), errorStartIdx, erroStr.Length);
                result.Set(false, null);
            }

            NewToken((int)AsmTokenKind.EOF, sourceIdx, 0);
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
            TokenBase token = new AsmToken(kind: (AsmTokenKind)kind, strIdx: strIdx, strLen: strLen, nextToke: null);
            currentToken.Next = token;
            return token;
        }

        /// <summary>
        /// ラベルの取得
        /// </summary>
        /// <param name="source">ソース</param>
        /// <param name="idx">ソースファイルインデックス。読み進めた分idxも影響を受ける</param>
        /// <returns>識別子の文字数</returns>
        private AsmToken GetLabel(string str)
        {
            if(!IsAcsiiControlCharacter(str))
            {
                AsmToken token = NewToken((int)AsmTokenKind.LABEL, sourceIdx, 0) as AsmToken;
                int strLen = 0;
                str = GetStrInfoStringOne();
                while (!IsAcsiiControlCharacter(str) && str != ":" && str != "")
                {
                    strLen += GetStrByteLength(str);
                    NextStrInfo(1);
                    str = GetStrInfoStringOne();
                }

                // ラベルの最後は : 文字
                if(str == ":")
                {
                    strLen += GetStrByteLength(str);
                    token.StrLen = strLen;
                    NextStrInfo(1);
                }

                return token;   // エラーの場合はstrLenが0
            }
            return null;
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
    }
}
