using System;
using System.IO;
using System.Collections.Generic;

namespace MCCompilerConsole.Converter
{
    class Disassembler
    {
        private List<(int address, string label)> labelsInfo;
        private int mnemonicStrLengthMax;
        private int operandStrLengthMax;

        public Disassembler()
        {
            labelsInfo = new List<(int address, string label)>();

            mnemonicStrLengthMax = 0;
            foreach (Mnemonic mnemonic in Enum.GetValues(typeof(Mnemonic)))
            {
                if (Mnemonic.INVALID == mnemonic) continue;
                mnemonicStrLengthMax = Math.Max(mnemonicStrLengthMax, mnemonic.ToString().Length);
            }
            operandStrLengthMax = 0;
            foreach (Operand operand in Enum.GetValues(typeof(Operand)))
            {
                if (Operand.INVALID == operand) continue;
                operandStrLengthMax = Math.Max(operandStrLengthMax, operand.ToString().Length);
            }
        }

        /// <summary>
        /// ニーモニック後のスペース取得
        /// </summary>
        /// <param name="mnemonic"></param>
        /// <returns></returns>
        private string GetSpaceAfterMnemonic(Mnemonic mnemonic)
        {
            int spaceNum = mnemonicStrLengthMax - mnemonic.ToString().Length;
            return new string(' ', spaceNum + 2);
        }

        /// <summary>
        /// オペランド後のスペース取得
        /// </summary>
        /// <param name="operand"></param>
        /// <returns></returns>
        private string GetSpaceAfterOperand(Operand operand)
        {
            int spaceNum = operandStrLengthMax - operand.ToString().Length;
            return new string(' ', spaceNum + 2);
        }

        /// <summary>
        /// ディスアセンブラの実行
        /// </summary>
        /// <param name="filename">実行するファイル</param>
        public bool Do(string filename)
        {
            // 初期化
            labelsInfo.Clear();

            try
            {

                Preprocess(filename);

                // ディレクトリ
                string directory = Path.GetDirectoryName(filename);
                string filenameWithoutExtension = Path.GetFileNameWithoutExtension(filename);

                string asesbleDirectory = directory + @"\";
                string assebleFile = asesbleDirectory + filenameWithoutExtension + "_disassemble" + Define.McasEx;

                // 中間ファイルとアセンブリファイルのディレクトリ作成
                //Directory.CreateDirectory(asesbleDirectory);

                using (StreamWriter aSw = File.CreateText(assebleFile))
                {
                    using (FileStream fs = File.OpenRead(filename))
                    using (BinaryReader br = new BinaryReader(fs))
                    {
                        int fsLen = (int)fs.Length;
                        byte[] readFile = new byte[fsLen];
                        readFile = br.ReadBytes(fsLen);
                        //string space2 = "  ";

                        MagicConfig config = (MagicConfig)readFile[0];
                        if (config.HasFlag(MagicConfig.Magic))
                        {
                            if (config.HasFlag(MagicConfig.OneTime))
                            {
                                aSw.WriteLine("#magic-onetime");
                            }
                            if (config.HasFlag(MagicConfig.Repeate))
                            {
                                aSw.WriteLine("#magic-repeate");
                            }
                        }
                        if (config.HasFlag(MagicConfig.Skill))
                        {
                            aSw.WriteLine("#skill");
                        }

                        for (int i = (int)Define.MagicConfigSize; i < readFile.Length;)
                        {
                            foreach (var info in labelsInfo)
                            {
                                if (info.address == i)
                                {
                                    aSw.WriteLine($"{info.label}:");
                                    break;
                                }
                            }

                            int writeByte = i;
                            Mnemonic mnemonic = (Mnemonic)readFile[i];
                            i++;    // 次へ

                            string writeByteString = $"/*0x{writeByte:x8}::*/  {mnemonic.ToString()}{GetSpaceAfterMnemonic(mnemonic)}";

                            /// オペランドの読み込み
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
                                        Operand operand = (Operand)readFile[i];
                                        i++;

                                        // 数値(4Byte)整数
                                        byte[] bytes = new byte[4];
                                        Array.Copy(readFile, i, bytes, 0, 4);
                                        int value = Generic.ToInt32(bytes);
                                        i += 4;

                                        aSw.WriteLine(writeByteString + operand.ToString() + GetSpaceAfterOperand(operand) + value.ToString());
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
                                        Operand operand = (Operand)readFile[i];
                                        i++;

                                        // (4Byte)不動小数点数
                                        byte[] bytes = new byte[4];
                                        Array.Copy(readFile, i, bytes, 0, 4);
                                        float value = Generic.ToFloat32(bytes);
                                        i += 4;

                                        aSw.WriteLine(writeByteString + operand.ToString() + GetSpaceAfterOperand(operand) + value.ToString() + "f");
                                    }
                                    break;

                                // -- 書き込み
                                // オペランド(1Bte)
                                // 文字数(4Byte)
                                // 文字列
                                case Mnemonic.MOVS:
                                case Mnemonic.ADDS:
                                case Mnemonic.DEBUG_PUSH:
                                    {
                                        // オペランド(1Bte)
                                        Operand operand = (Operand)readFile[i];
                                        i++;

                                        // 文字数(4Byte)
                                        byte[] bytes = new byte[4];
                                        Array.Copy(readFile, i, bytes, 0, 4);
                                        int len = Generic.ToInt32(bytes);
                                        i += 4;

                                        // 文字列
                                        string str = System.Text.Encoding.UTF8.GetString(readFile, i, len);
                                        i += len;

                                        aSw.WriteLine(writeByteString + operand.ToString() + GetSpaceAfterOperand(operand) + $"\"{str}\"");
                                    }
                                    break;

                                // -- 書き込み
                                // ジャンプ先アドレス(4Byte)
                                case Mnemonic.JMP:
                                case Mnemonic.JE:
                                case Mnemonic.JNE:
                                case Mnemonic.CALL:
                                    {
                                        // 数値(4Byte)整数
                                        byte[] bytes = new byte[4];
                                        Array.Copy(readFile, i, bytes, 0, 4);
                                        int value = Generic.ToInt32(bytes);
                                        i += 4;

                                        (int address, string label)? _info = null;
                                        foreach (var info in labelsInfo)
                                        {
                                            if (info.address == value)
                                            {
                                                _info = info;
                                                break;
                                            }
                                        }
                                        if (_info == null)
                                        {
                                            // エラー
                                            return false;
                                        }
                                        aSw.WriteLine(writeByteString + $"\"{_info.Value.label}\"  /*0x{_info.Value.address:x8}*/");
                                    }
                                    break;

                                // -- 書き込み
                                // 文字数(4Byte)　※文字はutf8なので必ずbyteの数を数える。 utf8は複数バイトで1文字の場合があるため
                                // 文字列
                                case Mnemonic.PUSHS:
                                    {
                                        // 文字数(4Byte)
                                        byte[] bytes = new byte[4];
                                        Array.Copy(readFile, i, bytes, 0, 4);
                                        int len = Generic.ToInt32(bytes);
                                        i += 4;

                                        // 文字列
                                        string str = System.Text.Encoding.UTF8.GetString(readFile, i, len);
                                        i += len;

                                        aSw.WriteLine(writeByteString + $"\"{str}\"");
                                    }
                                    break;

                                // -- 書き込み
                                // オペランド1
                                // オペランド2
                                // コピーバイト数(4Byte)
                                case Mnemonic.MEMCOPY:
                                    {
                                        // オペランド1(1Bte)
                                        Operand operand1 = (Operand)readFile[i];
                                        i++;

                                        // オペランド2(1Bte)
                                        Operand operand2 = (Operand)readFile[i];
                                        i++;

                                        // コピーバイト数(4Byte)
                                        byte[] bytes = new byte[4];
                                        Array.Copy(readFile, i, bytes, 0, 4);
                                        int len = Generic.ToInt32(bytes);
                                        i += 4;

                                        aSw.WriteLine(writeByteString + operand1.ToString() + GetSpaceAfterOperand(operand1) + operand2.ToString());
                                    }
                                    break;

                                // -- 書き込み
                                // 数値(4Byte)浮動小数点
                                case Mnemonic.PUSHF:
                                    {
                                        // (4Byte)不動小数点数
                                        byte[] bytes = new byte[4];
                                        Array.Copy(readFile, i, bytes, 0, 4);
                                        float value = Generic.ToFloat32(bytes);
                                        i += 4;

                                        aSw.WriteLine(writeByteString + value.ToString() + "f");
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
                                        // 数値(4Byte)整数
                                        byte[] bytes = new byte[4];
                                        Array.Copy(readFile, i, bytes, 0, 4);
                                        int value = Generic.ToInt32(bytes);
                                        i += 4;

                                        aSw.WriteLine(writeByteString + value.ToString());
                                    }
                                    break;

                                // -- 書き込み
                                // 文字数(4Byte)　※文字はutf8なので必ずbyteの数を数える。 utf8は複数バイトで1文字の場合があるため
                                // 文字列
                                // 置き換えオペランド数(4Byte)
                                // 置き換えオペランド
                                case Mnemonic.DEBUG_LOG:
                                    {

                                        // 文字数(4Byte)
                                        byte[] bytes = new byte[4];
                                        Array.Copy(readFile, i, bytes, 0, 4);
                                        int len = Generic.ToInt32(bytes);
                                        i += 4;

                                        // 文字列
                                        string str = System.Text.Encoding.UTF8.GetString(readFile, i, len);
                                        i += len;

                                        // 置き換えオペランド数(4Byte)
                                        Array.Copy(readFile, i, bytes, 0, 4);
                                        int operandNum = Generic.ToInt32(bytes);
                                        i += 4;

                                        // 置き換えオペランド
                                        string operandsStr = "";
                                        for (int count = 0; count < operandNum; count++)
                                        {
                                            operandsStr += "  " + ((Operand)readFile[i + count]).ToString();
                                        }
                                        i += operandNum;

                                        aSw.WriteLine(writeByteString + $"\"{str}\"" + "  " + operandNum.ToString() + operandsStr);
                                    }
                                    break;

                                // -- 書き込み
                                // オペランド(1Byte)達
                                default:
                                    {
                                        int operandNum = Generic.GetOperandByte(mnemonic);
                                        string operandsStr = "";
                                        switch(operandNum)
                                        {
                                            case 0:
                                                break;

                                            case 1:
                                                operandsStr = ((Operand)readFile[i]).ToString();
                                                break;

                                            case 2:
                                                {
                                                    Operand operand1 = (Operand)readFile[i + 0];
                                                    Operand operand2 = (Operand)readFile[i + 1];
                                                    operandsStr = operand1.ToString() + GetSpaceAfterOperand(operand1) + operand2.ToString();
                                                }
                                                break;

                                            default:
                                                return false;
                                        }
                                        aSw.WriteLine(writeByteString + operandsStr);

                                        i += operandNum;
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        private void Preprocess(string filename)
        {
            string directory = Path.GetDirectoryName(filename);

            using (FileStream fs = File.OpenRead(filename))
            using (BinaryReader br = new BinaryReader(fs))
            {
                int fsLen = (int)fs.Length;
                byte[] readFile = new byte[fsLen];
                readFile = br.ReadBytes(fsLen);

                for (int i = (int)Define.MagicConfigSize; i < readFile.Length;)
                {
                    int writeByte = i;
                    Mnemonic mnemonic = (Mnemonic)readFile[i];
                    i++;    // 次へ

                    // JMP系のとび先を保存
                    switch (mnemonic)
                    {
                        // -- 書き込み
                        // オペランド(1Bte)
                        // 文字数(4Byte)
                        // 文字列
                        case Mnemonic.MOVS:
                        case Mnemonic.ADDS:
                        case Mnemonic.DEBUG_PUSH:
                            {
                                // オペランド(1Bte)
                                Operand operand = (Operand)readFile[i];
                                i++;

                                // 文字数(4Byte)
                                byte[] bytes = new byte[4];
                                Array.Copy(readFile, i, bytes, 0, 4);
                                int len = Generic.ToInt32(bytes);
                                i += 4;

                                // 文字列
                                string str = System.Text.Encoding.UTF8.GetString(readFile, i, len);
                                i += len;
                            }
                            break;

                        // -- 書き込み
                        // 文字数(4Byte)　※文字はutf8なので必ずbyteの数を数える。 utf8は複数バイトで1文字の場合があるため
                        // 文字列
                        case Mnemonic.PUSHS:
                            {
                                // 文字数(4Byte)
                                byte[] bytes = new byte[4];
                                Array.Copy(readFile, i, bytes, 0, 4);
                                int len = Generic.ToInt32(bytes);
                                i += 4;

                                // 文字列
                                string str = System.Text.Encoding.UTF8.GetString(readFile, i, len);
                                i += len;
                            }
                            break;

                        // -- 書き込み
                        // ジャンプ先アドレス(4Byte)
                        case Mnemonic.JMP:
                        case Mnemonic.JE:
                        case Mnemonic.JNE:
                        case Mnemonic.CALL:
                            {

                                // 数値(4Byte)整数 or (4Byte)不動小数点数
                                byte[] bytes = new byte[4];
                                Array.Copy(readFile, i, bytes, 0, 4);
                                int value = Generic.ToInt32(bytes);
                                i += 4;

                                // 見つからなかったら追加
                                bool isFind = false;
                                foreach (var info in labelsInfo)
                                {
                                    if (info.address == value)
                                    {
                                        isFind = true;
                                        break;
                                    }
                                }
                                if (!isFind)
                                {
                                    int count = labelsInfo.Count;
                                    labelsInfo.Add((value, $"label{count}"));
                                }
                            }
                            break;

                        // -- 書き込み
                        // 文字数(4Byte)
                        // 文字列
                        // 置き換えオペランド数(4Byte)
                        // 置き換えオペランド
                        case Mnemonic.DEBUG_LOG:
                            {
                                // 文字数(4Byte)
                                byte[] bytes = new byte[4];
                                Array.Copy(readFile, i, bytes, 0, 4);
                                int len = Generic.ToInt32(bytes);
                                i += 4;

                                // 文字列
                                string str = System.Text.Encoding.UTF8.GetString(readFile, i, len);
                                i += len;

                                // 置き換えオペランド数(4Byte)
                                bytes = new byte[4];
                                Array.Copy(readFile, i, bytes, 0, 4);
                                int num = Generic.ToInt32(bytes);
                                i += 4;

                                // 置き換えオペランド
                                i += num;
                            }
                            break;

                        // -- 書き込み
                        // オペランド(1Byte)達
                        default:
                            {
                                int operandNum = Generic.GetOperandByte(mnemonic);
                                i += operandNum;
                            }
                            break;
                    }

                    labelsInfo.Sort();
                }
            }
        }
    }
}
