using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace MCCompilerConsole.Converter.Assembler
{
    public class CodeGenerater
    {

        public CodeGenerater(Assembler.AssembleArgs aa)
        {
            outFile = new List<byte>();
            isCodeGenError = false;
            result = new CodeGenResult();
            this.aa = aa;
        }

        /// <summary>
        /// コード作成結果
        /// </summary>
        public class CodeGenResult : ResultBase
        {
            public override void Initialize()
            {
                base.Initialize();
                this.OutFileSize = 0;
            }
            public void Set(bool success, string binFileName)
            {
                this.Success = success;
                this.BinFileName = binFileName;
            }
            public string BinFileName { get; set; }
            public int OutFileSize { get; set; }
        }

        /// <summary>
        /// 中間ファイルのファイルの作成
        /// </summary>
        /// <param name="sourceFileName">ソースファイル名</param>
        /// <param name="offset">中間ファイルの位置</param>
        /// <param name="parserResult">パーサーの結果</param>
        /// <param name="isEpilog">エピローグをつけるか</param>
        /// <returns>中間ファイル作成結果</returns>
        public CodeGenResult Do(string sourceFileName, int offset, Tokenizer.TokenizeResult tokenResult, bool isEpilog, bool isRelease)
        {
            Initialize();

            string filenameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFileName);
            string mcbinFileName = aa.MCBinDir + filenameWithoutExtension + Define.McbinEx;
            this.isRelease = isRelease;

            // 作成されたトークンから中間ファイルの作成
            {
                if (isEpilog)
                {
                    // Magic Configの書き込み
                    outFile.Add((byte)aa.Config);
                }

                currentToken = tokenResult.HeadToken.Next;
                Generate(offset);
            }

            if (outFile.Count > 0)
            {
                using (FileStream fs = File.Create(mcbinFileName))
                {
                    fs.Write(outFile.ToArray());
                    result.OutFileSize = (int)fs.Length;

                    outFile.Clear();    // 無駄なメモリは解放
                }
            }
            else
            {
                result.OutFileSize = 0;
                result.Set(true, mcbinFileName);
                return result;
            }

            result.Set(!isCodeGenError, mcbinFileName);
            return result;
        }

        /// <summary>
        /// ファイルの作成
        /// </summary>
        /// <param name="token"></param>
        public void Generate(int offset)
        {
            List<byte> writeByte = new List<byte>();
            byte[] workBytes = new byte[4];
            AsmTokenKind[] skipKinds = { AsmTokenKind.MNEMONIC };

            while(!AtEOF())
            {
                switch (currentToken.Kind)
                {
                    case AsmTokenKind.INCLUDE:
                        NextToken();
                        // プリプロセスで処理をしているので
                        // ここでは読み呼ばす
                        if (currentToken.Kind != AsmTokenKind.STRING)
                        {
                            // 続くトークンは文字列トークン
                            Error(aa.ErrorData.Str(ERROR_TEXT.NEXT_TOKEN_STRING));
                            SkipKinds(skipKinds);
                            continue;
                        }
                        break;

                    case AsmTokenKind.ONETIME:
                    case AsmTokenKind.REPEATE:
                    case AsmTokenKind.SKILL:
                        // プリプロセスはここでは読み呼ばして良いはず
                        break;

                    case AsmTokenKind.LABEL:
                        {
                            // ラベルのアドレス登録をする
                            string label = aa.Source.Substring(currentToken.StrIdx, currentToken.StrLen - 1);   // ラベルなので : 文字分を -1 する
                            aa.Linker.Registration(label, offset + outFile.Count);
                        }
                        break;

                    case AsmTokenKind.MNEMONIC:
                        {
                            Tokenizer.AsmToken mnemonicTk = currentToken;
                            string mnemonicstr = aa.Source.Substring(currentToken.StrIdx, currentToken.StrLen).ToUpper();
                            Mnemonic mnemonic;
                            if (Enum.TryParse<Mnemonic>(mnemonicstr, out mnemonic))
                            {
                                /// ニーモニックの書き込み
                                switch(mnemonic)
                                {
                                    case Mnemonic.DEBUG_LOG:
                                    case Mnemonic.DEBUG_RLOG:
                                    case Mnemonic.DEBUG_SLOG:
                                    case Mnemonic.DEBUG_PUSH:
                                    case Mnemonic.DEBUG_PAUSE:
                                        {
                                            if(!isRelease)
                                            {
                                                // ニーモニックの書き込み
                                                outFile.Add((byte)mnemonic);
                                            }
                                        }
                                        break;

                                    default:
                                        {
                                            // ニーモニックの書き込み
                                            outFile.Add((byte)mnemonic);
                                        }
                                        break;
                                }

                                /// オペランドの書き込み
                                NextToken();
                                switch (mnemonic)
                                {
                                    // -- 書き込み
                                    // オペランド
                                    // 数値(4Byte)整数
                                    case Mnemonic.MOVI:
                                    case Mnemonic.ADDI:
                                    case Mnemonic.SUBI:
                                    case Mnemonic.IMULI:
                                    case Mnemonic.IDIVI:
                                        {

                                            // オペランド
                                            if (currentToken.Kind == AsmTokenKind.OPERAND)
                                            {
                                                string str = aa.Source.Substring(currentToken.StrIdx, currentToken.StrLen).ToUpper();
                                                if (!AddOperand(str))
                                                {
                                                    // 不明オペランドです
                                                    Error(aa.ErrorData.Str(ERROR_TEXT.ST_INVALID_OPERAND), mnemonicTk.StrIdx, mnemonicTk.StrLen);
                                                    SkipKinds(skipKinds);
                                                    continue;
                                                }
                                            }
                                            else
                                            {
                                                // ニーモニックエラー
                                                Error(aa.ErrorData.Str(ERROR_TEXT.MNEMONIC_OPERAND_INT, mnemonic.ToString()), mnemonicTk.StrIdx, mnemonicTk.StrLen);
                                                SkipKinds(skipKinds);
                                                continue;
                                            }

                                            // 数値(4Byte)
                                            NextToken();
                                            if (currentToken.Kind == AsmTokenKind.INTEGER)
                                            {
                                                AddNum(currentToken.ValueI);
                                            }
                                            else
                                            {
                                                // ニーモニックエラー
                                                Error(aa.ErrorData.Str(ERROR_TEXT.MNEMONIC_OPERAND_INT, mnemonic.ToString()), mnemonicTk.StrIdx, mnemonicTk.StrLen);
                                                SkipKinds(skipKinds);
                                                continue;
                                            }
                                        }
                                        break;

                                    // -- 書き込み
                                    // オペランド
                                    // 数値(4Byte)不動小数点数
                                    case Mnemonic.MOVF:
                                    case Mnemonic.ADDF:
                                    case Mnemonic.SUBF:
                                    case Mnemonic.IMULF:
                                    case Mnemonic.IDIVF:
                                        {

                                            // オペランド
                                            if (currentToken.Kind == AsmTokenKind.OPERAND)
                                            {
                                                string str = aa.Source.Substring(currentToken.StrIdx, currentToken.StrLen).ToUpper();
                                                if (!AddOperand(str))
                                                {
                                                    // 不明オペランドです
                                                    Error(aa.ErrorData.Str(ERROR_TEXT.ST_INVALID_OPERAND));
                                                    SkipKinds(skipKinds);
                                                    continue;
                                                }
                                            }
                                            else
                                            {
                                                // ニーモニックエラー
                                                Error(aa.ErrorData.Str(ERROR_TEXT.MNEMONIC_OPERAND_FLOAT, mnemonic.ToString()), mnemonicTk.StrIdx, mnemonicTk.StrLen);
                                                SkipKinds(skipKinds);
                                                continue;
                                            }

                                            // 数値(4Byte)
                                            NextToken();
                                            if (currentToken.Kind == AsmTokenKind.FLOAT)
                                            {
                                                AddNum(currentToken.ValueF);
                                            }
                                            else if (currentToken.Kind == AsmTokenKind.INTEGER)
                                            {
                                                AddNum((float)currentToken.ValueI);
                                            }
                                            else
                                            {
                                                // ニーモニックエラー
                                                Error(aa.ErrorData.Str(ERROR_TEXT.MNEMONIC_OPERAND_FLOAT, mnemonic.ToString()), mnemonicTk.StrIdx, mnemonicTk.StrLen);
                                                SkipKinds(skipKinds);
                                                continue;
                                            }
                                        }
                                        break;

                                    // -- 書き込み
                                    // オペランド(1Bte)
                                    // 文字数(4Byte)　※文字はutf8なので必ずbyteの数を数える。 utf8は複数バイトで1文字の場合があるため
                                    // 文字列
                                    case Mnemonic.MOVS:
                                    case Mnemonic.ADDS:
                                        {
                                            // オペランド(1Bte)
                                            string str = aa.Source.Substring(currentToken.StrIdx, currentToken.StrLen).ToUpper();
                                            if (!AddOperand(str))
                                            {
                                                // 不明オペランドです
                                                Error(aa.ErrorData.Str(ERROR_TEXT.ST_INVALID_OPERAND));
                                                SkipKinds(skipKinds);
                                                continue;
                                            }

                                            // 文字数(4Byte)　※文字はutf8なので必ずbyteの数を数える。 utf8は複数バイトで1文字の場合があるため
                                            // 文字列
                                            NextToken();
                                            if (currentToken.Kind != AsmTokenKind.STRING)
                                            {
                                                // ニーモニックエラー
                                                Error(aa.ErrorData.Str(ERROR_TEXT.MNEMONIC_OPERAND_STRING, mnemonic.ToString()), mnemonicTk.StrIdx, mnemonicTk.StrLen);
                                                SkipKinds(skipKinds);
                                                continue;
                                            }
                                            str = aa.Source.Substring(currentToken.StrIdx, currentToken.StrLen);
                                            AddNum(GetUtf8StrLength(str));
                                            AddString(str);
                                        }
                                        break;

                                    // -- 書き込み
                                    // ジャンプ先アドレス(4Byte)
                                    case Mnemonic.JMP:
                                    case Mnemonic.JE:
                                    case Mnemonic.JNE:
                                    case Mnemonic.CALL:
                                        if (currentToken.Kind == AsmTokenKind.STRING)
                                        {
                                            // ジャンプ系の場合は指定ラベルのアドレスにジャンプなので
                                            // ラベルのIndexを入れる
                                            // 最終的にはリンカーが実際のジャンプアドレスを入れてくれる
                                            string label = aa.Source.Substring(currentToken.StrIdx, currentToken.StrLen);
                                            (bool success, int index) = aa.Linker.GetLabelIndex(label);
                                            if (success)
                                            {
                                                AddNum(index);
                                            }
                                            else
                                            {
                                                // 登録されていないラベルです
                                                Error(aa.ErrorData.Str(ERROR_TEXT.NOT_DEFINE_LABEL, label));
                                                SkipKinds(skipKinds);
                                                continue;
                                            }

                                        }
                                        else
                                        {
                                            // ニーモニックエラー
                                            Error(aa.ErrorData.Str(ERROR_TEXT.MNEMONIC_LABEL, mnemonic.ToString()), mnemonicTk.StrIdx, mnemonicTk.StrLen);
                                            SkipKinds(skipKinds);
                                            continue;
                                        }
                                        break;

                                    // -- 書き込み
                                    // 文字数(4Byte)　※文字はutf8なので必ずbyteの数を数える。 utf8は複数バイトで1文字の場合があるため
                                    // 文字列
                                    case Mnemonic.PUSHS:
                                        {
                                            if (currentToken.Kind == AsmTokenKind.STRING)
                                            {
                                                string str = aa.Source.Substring(currentToken.StrIdx, currentToken.StrLen);
                                                AddNum(GetUtf8StrLength(str));
                                                AddString(str);
                                            }
                                            else
                                            {
                                                // ニーモニックエラー
                                                Error(aa.ErrorData.Str(ERROR_TEXT.MNEMONIC_LABEL, mnemonic.ToString()), mnemonicTk.StrIdx, mnemonicTk.StrLen);
                                                SkipKinds(skipKinds);
                                                continue;
                                            }
                                        }
                                        break;

                                    // -- 書き込み
                                    // オペランド1
                                    // オペランド2
                                    // コピーバイト数(4Byte)
                                    case Mnemonic.MEMCOPY:
                                        {
                                            // オペランド1
                                            string str = aa.Source.Substring(currentToken.StrIdx, currentToken.StrLen).ToUpper();
                                            if (!AddOperand(str))
                                            {
                                                // ニーモニックエラー
                                                Error(aa.ErrorData.Str(ERROR_TEXT.MNEMONIC_OPERAND_OPERAND_INT, mnemonic.ToString()), mnemonicTk.StrIdx, mnemonicTk.StrLen);
                                                SkipKinds(skipKinds);
                                                continue;
                                            }
                                            // オペランド2
                                            NextToken();
                                            str = aa.Source.Substring(currentToken.StrIdx, currentToken.StrLen).ToUpper();
                                            if (!AddOperand(str))
                                            {
                                                // ニーモニックエラー
                                                Error(aa.ErrorData.Str(ERROR_TEXT.MNEMONIC_OPERAND_OPERAND_INT, mnemonic.ToString()), mnemonicTk.StrIdx, mnemonicTk.StrLen);
                                                SkipKinds(skipKinds);
                                                continue;
                                            }
                                            // コピーバイト数(4Byte)
                                            NextToken();
                                            if (currentToken.Kind == AsmTokenKind.INTEGER)
                                            {
                                                AddNum(currentToken.ValueI);
                                            }
                                            else
                                            {
                                                // ニーモニックエラー
                                                Error(aa.ErrorData.Str(ERROR_TEXT.MNEMONIC_OPERAND_OPERAND_INT, mnemonic.ToString()), mnemonicTk.StrIdx, mnemonicTk.StrLen);
                                                SkipKinds(skipKinds);
                                                continue;
                                            }
                                        }
                                        break;

                                    // -- 書き込み
                                    // 数値(4Byte)浮動小数点
                                    case Mnemonic.PUSHF:
                                        {
                                            // 数値浮動小数点(4Byte)
                                            if (currentToken.Kind == AsmTokenKind.FLOAT)
                                            {
                                                AddNum(currentToken.ValueF);
                                            }
                                            else if (currentToken.Kind == AsmTokenKind.INTEGER)
                                            {
                                                AddNum((float)currentToken.ValueI);
                                            }
                                            else
                                            {
                                                // ニーモニックエラー
                                                Error(aa.ErrorData.Str(ERROR_TEXT.MNEMONIC_FLOAT, mnemonic.ToString()), mnemonicTk.StrIdx, mnemonicTk.StrLen);
                                                SkipKinds(skipKinds);
                                                continue;
                                            }
                                        }
                                        break;

                                    // -- 書き込み
                                    // 数値(4Byte)整数
                                    case Mnemonic.PUSHI:
                                    case Mnemonic.GWST_LIB:
                                    case Mnemonic.GWST_MAG:
                                    case Mnemonic.GWST_SMAG:
                                    case Mnemonic.GWST_UI:
                                    case Mnemonic.GWST_MEPH:
                                    case Mnemonic.GWST_WAMAG:
                                    case Mnemonic.SYSTEM:
                                        {
                                            // 数値整数(4Byte)
                                            if (currentToken.Kind == AsmTokenKind.INTEGER)
                                            {
                                                AddNum(currentToken.ValueI);
                                            }
                                            else
                                            {
                                                // ニーモニックエラー
                                                Error(aa.ErrorData.Str(ERROR_TEXT.MNEMONIC_INT, mnemonic.ToString()), mnemonicTk.StrIdx, mnemonicTk.StrLen);
                                                SkipKinds(skipKinds);
                                                continue;
                                            }
                                        }
                                        break;

                                    // -- 書き込み
                                    // 文字数(4Byte)　※文字はutf8なので必ずbyteの数を数える。 utf8は複数バイトで1文字の場合があるため
                                    // 文字列
                                    // 置き換えオペランド数(4Byte)
                                    // 置き換えオペランド
                                    case Mnemonic.DEBUG_LOG:
                                        {
                                            if (!isRelease)
                                            {
                                                // 文字数(4Byte)
                                                // 文字列
                                                if (currentToken.Kind == AsmTokenKind.STRING)
                                                {
                                                    string str = aa.Source.Substring(currentToken.StrIdx, currentToken.StrLen);
                                                    AddNum(GetUtf8StrLength(str));
                                                    AddString(str);
                                                }

                                                // 置き換えオペランド数(4Byte)
                                                NextToken();
                                                int operandNum = 0;
                                                if (currentToken.Kind == AsmTokenKind.INTEGER)
                                                {
                                                    operandNum = currentToken.ValueI;
                                                    AddNum(operandNum);
                                                }
                                                else
                                                {
                                                    // オペランド数の指定してください
                                                    Error(aa.ErrorData.Str(ERROR_TEXT.SPLID_OPERAND_NUM));
                                                    SkipKinds(skipKinds);
                                                    continue;
                                                }

                                                // 置き換えオペランド
                                                for (int i = 0; i < operandNum; i++)
                                                {
                                                    // 置き換えオペランド
                                                    NextToken();
                                                    if (currentToken.Kind != AsmTokenKind.OPERAND)
                                                    {
                                                        // オペランドの指定してください
                                                        Error(aa.ErrorData.Str(ERROR_TEXT.NEXT_TOKEN_OPERAND));
                                                        SkipKinds(skipKinds);
                                                        continue;
                                                    }
                                                    string str = aa.Source.Substring(currentToken.StrIdx, currentToken.StrLen).ToUpper();
                                                    if (!AddOperand(str))
                                                    {
                                                        // 不明オペランドです
                                                        Error(aa.ErrorData.Str(ERROR_TEXT.ST_INVALID_OPERAND));
                                                        SkipKinds(skipKinds);
                                                        continue;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                // 文字数(4Byte)
                                                // 文字列

                                                // 置き換えオペランド数(4Byte)
                                                NextToken();
                                                int operandNum = 0;
                                                if (currentToken.Kind == AsmTokenKind.INTEGER)
                                                {
                                                    operandNum = currentToken.ValueI;
                                                }
                                                else
                                                {
                                                    // オペランド数の指定してください
                                                    Error(aa.ErrorData.Str(ERROR_TEXT.SPLID_OPERAND_NUM));
                                                    SkipKinds(skipKinds);
                                                    continue;
                                                }

                                                // 置き換えオペランド
                                                for (int i = 0; i < operandNum; i++)
                                                {
                                                    // 置き換えオペランド
                                                    NextToken();
                                                    if (currentToken.Kind != AsmTokenKind.OPERAND)
                                                    {
                                                        // オペランドの指定してください
                                                        Error(aa.ErrorData.Str(ERROR_TEXT.NEXT_TOKEN_OPERAND));
                                                        SkipKinds(skipKinds);
                                                        continue;
                                                    }
                                                    string str = aa.Source.Substring(currentToken.StrIdx, currentToken.StrLen).ToUpper();
                                                    Operand operand;
                                                    if (!Enum.TryParse<Operand>(str, out operand))
                                                    {
                                                        // 不明オペランドです
                                                        Error(aa.ErrorData.Str(ERROR_TEXT.ST_INVALID_OPERAND));
                                                        SkipKinds(skipKinds);
                                                        continue;
                                                    }
                                                }
                                            }
                                        }
                                        break;

                                    // -- 書き込み
                                    // オペランド(1Bte)
                                    // 文字数(4Byte)　※文字はutf8なので必ずbyteの数を数える。 utf8は複数バイトで1文字の場合があるため
                                    // 文字列
                                    case Mnemonic.DEBUG_PUSH:
                                        {
                                            if (!isRelease)
                                            {
                                                // オペランド(1Bte)
                                                string str = aa.Source.Substring(currentToken.StrIdx, currentToken.StrLen).ToUpper();
                                                if (!AddOperand(str))
                                                {
                                                    // 不明オペランドです
                                                    Error(aa.ErrorData.Str(ERROR_TEXT.ST_INVALID_OPERAND));
                                                    SkipKinds(skipKinds);
                                                    continue;
                                                }

                                                // 文字数(4Byte)　※文字はutf8なので必ずbyteの数を数える。 utf8は複数バイトで1文字の場合があるため
                                                // 文字列
                                                NextToken();
                                                if (currentToken.Kind != AsmTokenKind.STRING)
                                                {
                                                    // ニーモニックエラー
                                                    Error(aa.ErrorData.Str(ERROR_TEXT.MNEMONIC_OPERAND_STRING, mnemonic.ToString()), mnemonicTk.StrIdx, mnemonicTk.StrLen);
                                                    SkipKinds(skipKinds);
                                                    continue;
                                                }
                                                str = aa.Source.Substring(currentToken.StrIdx, currentToken.StrLen);
                                                AddNum(GetUtf8StrLength(str));
                                                AddString(str);
                                            }
                                            else
                                            {

                                                string str = aa.Source.Substring(currentToken.StrIdx, currentToken.StrLen).ToUpper();
                                                Operand operand;
                                                if (!Enum.TryParse<Operand>(str, out operand))
                                                {
                                                    // 不明オペランドです
                                                    Error(aa.ErrorData.Str(ERROR_TEXT.ST_INVALID_OPERAND));
                                                    SkipKinds(skipKinds);
                                                    continue;
                                                }
                                                // 文字数(4Byte)　※文字はutf8なので必ずbyteの数を数える。 utf8は複数バイトで1文字の場合があるため
                                                // 文字列
                                                NextToken();
                                                if (currentToken.Kind != AsmTokenKind.STRING)
                                                {
                                                    // ニーモニックエラー
                                                    Error(aa.ErrorData.Str(ERROR_TEXT.MNEMONIC_OPERAND_STRING, mnemonic.ToString()), mnemonicTk.StrIdx, mnemonicTk.StrLen);
                                                    SkipKinds(skipKinds);
                                                    continue;
                                                }
                                            }
                                        }
                                        break;

                                    // -- 書き込み
                                    // オペランド(1Byte)達
                                    default:
                                        {
                                            int operandNum = Generic.GetOperandByte(mnemonic);
                                            Tokenizer.AsmToken tokne = currentToken;
                                            bool isError = false;
                                            for (int i = 0; i < operandNum; i++)
                                            {
                                                // オペランド
                                                if (currentToken.Kind == AsmTokenKind.OPERAND)
                                                {
                                                    string str = aa.Source.Substring(currentToken.StrIdx, currentToken.StrLen).ToUpper();
                                                    if (!AddOperand(str))
                                                    {
                                                        // 不明オペランドです
                                                        Error(aa.ErrorData.Str(ERROR_TEXT.ST_INVALID_OPERAND));
                                                        isError = true;
                                                        break;
                                                    }
                                                }
                                                else
                                                {
                                                    // ニーモニックエラー
                                                    ERROR_TEXT errorText = operandNum == 1 ? ERROR_TEXT.MNEMONIC_OPERAND : ERROR_TEXT.MNEMONIC_OPERAND_OPERAND;
                                                    Error(aa.ErrorData.Str(errorText, mnemonic.ToString()), mnemonicTk.StrIdx, mnemonicTk.StrLen);
                                                    isError = true;
                                                    break;
                                                }
                                                NextToken();
                                            }
                                            if(isError)
                                            {
                                                SkipKinds(skipKinds);
                                                continue;
                                            }
                                            
                                        }
                                        continue;   // whileのcontinue
                                }
                            }
                            else
                            {
                                // 知らないニーモニック
                                Error(aa.ErrorData.Str(ERROR_TEXT.NOT_DEFINE_MNEMONIC));
                                SkipKinds(skipKinds);
                            }
                        }
                        break;
                }
                NextToken();
            }
        }

        /// <summary>
        /// 次のトークン
        /// </summary>
        public void NextToken()
        {
            if(currentToken == null)
            {
                return;
            }
            if(currentToken.Kind != AsmTokenKind.EOF)
            {
                currentToken = currentToken.Next;
            }
        }

        /// <summary>
        /// トークンの終わりか
        /// </summary>
        /// <returns></returns>
        public bool AtEOF()
        {
            return (currentToken?.Kind ?? AsmTokenKind.EOF) == AsmTokenKind.EOF  ;
        }

        /// <summary>
        /// 指定トークンまで読み飛ばす
        /// </summary>
        /// <param name="tokenKinds"></param>
        private void SkipKinds(AsmTokenKind[] kinds)
        {
            while (!AtEOF())
            {
                foreach (var kind in kinds)
                {
                    if (currentToken.Kind == kind)
                    {
                        return;
                    }
                }
                NextToken();
            }
        }

        /// <summary>
        /// 数値の追加
        /// </summary>
        /// <param name="value">書き込み数値</param>
        private void AddNum(int value)
        {
            byte[] bytes = Generic.GetByte(value);
            outFile.Add(bytes[0]);
            outFile.Add(bytes[1]);
            outFile.Add(bytes[2]);
            outFile.Add(bytes[3]);
        }
        private void AddNum(float value)
        {
            byte[] bytes = Generic.GetByte(BitConverter.SingleToInt32Bits(value));
            outFile.Add(bytes[0]);
            outFile.Add(bytes[1]);
            outFile.Add(bytes[2]);
            outFile.Add(bytes[3]);
        }

        /// <summary>
        /// 文字の追加
        /// </summary>
        /// <param name="str"></param>
        private void AddString(string str)
        {
            // UTF8文字想定
            // UTF8文字としてファイル読み込んでいるので
            // stringの一文字で複数バイトを持っている可能性がある。
            // 実際のバイト数を求めてそれを書き込む
            Byte[] strbytes = Encoding.UTF8.GetBytes(str);
            foreach (var b in strbytes)
            {
                outFile.Add(b);
            }
        }

        /// <summary>
        /// オペランドの追加
        /// </summary>
        /// <param name="operandStr"></param>
        /// <returns></returns>
        private bool AddOperand(string operandStr)
        {
            Operand operand;
            if (!Enum.TryParse<Operand>(operandStr, out operand))
            {
                // TODO エラー 不明オペランドです
                return false;
            }
            outFile.Add((byte)operand);
            return true;
            
        }

        /// <summary>
        /// UTF8も文字数を取得
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private int GetUtf8StrLength(string str)
        {
            Byte[] strbytes = Encoding.UTF8.GetBytes(str);  // 文字はutf8なので必ずbyteの数を数える
            return strbytes.Length;
        }

        private void Error(string errorStr)
        {
            Error(errorStr, currentToken.StrIdx, currentToken.StrLen);
        }
        /// <summary>
        /// エラー
        /// </summary>
        /// <param name="errorStr">エラー内容</param>
        private void Error(string errorStr, int strIdx, int strLen)
        {
            (string linestr, int lineno) = Generic.GetaSourceLineStrNo(aa.Source, currentToken.StrIdx, currentToken.StrLen);
            result.Log = result.Log + (result.Log.Length > 0 ? "\n" : "") + aa.ErrorData.Str(ERROR_TEXT.ERROR_BASE_FILENAME_LINENO, aa.File, $"{lineno:d4}", linestr, errorStr);
            result.Success = false;
            isCodeGenError = true;
        }

        /// <summary>
        /// 初期化
        /// </summary>
        private void Initialize()
        {
            outFile.Clear();
            isCodeGenError = false;
            isRelease = false;
            result.Initialize();
            currentToken = null;
        }

        private List<byte> outFile;                 // 中間ファイルの元となるデータ
        private bool isCodeGenError;                // コード作成でエラーが出たか
        private bool isRelease;                     // リリースモードか
        private CodeGenResult result;               // コード生成の結果
        private Assembler.AssembleArgs aa;          // コンパイラ汎用引数
        private Tokenizer.AsmToken currentToken;
    }
}
