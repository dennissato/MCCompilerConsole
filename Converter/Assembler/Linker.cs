using System;
using System.IO;
using System.Collections.Generic;

namespace MCCompilerConsole.Converter.Assembler
{
    public class Linker
    {
        /// <summary>
        /// 関数情報
        /// </summary>
        public class LabelInfo
        {
            public LabelInfo(string name, int address)
            {
                this.Name = name;
                this.Address = address;
            }
            public string Name { get; set; }
            public int Address { get; set; }
        }

        public Linker(Assembler.AssembleArgs aa)
        {
            labels = new List<LabelInfo>();
            result = new LinkResult();
            this.aa = aa;
        }

        /// <summary>
        /// リンクの実行結果
        /// </summary>
        public class LinkResult : ResultBase
        {
            public override void Initialize()
            {
                base.Initialize();
            }
        }

        /// <summary>
        /// 初期化
        /// </summary>
        public void Initialize()
        {
            labels.Clear();
            result.Initialize();
            LabelInit = "";
            LabelMain = "";
        }

        /// <summary>
        /// リンク処理
        /// </summary>
        /// <param name="binFileName">中間ファイル</param>
        /// <param name="outFileName">最終結果ファイル</param>
        /// <returns>実行結果</returns>
        public LinkResult Do(string binFileName, string outFileName)
        {
            if (!File.Exists(binFileName))
            {
                
                Error(aa.ErrorData.Str(ERROR_TEXT.LINKER_NOT_INTERMEDIATE_FINE, binFileName));
                return result;
            }
            source = File.ReadAllBytes(binFileName);

            if (source.Length == 0)
            {
                Error(aa.ErrorData.Str(ERROR_TEXT.LINKER_FILESIZE_ZERO, binFileName));
                return result;
            }

            // 中間ファイル内のファンクションから調べる
            // sourceファイルの中身を見て
            // call 命令が出てきた時にとび先のアドレスに置き換える
            using (FileStream fs = File.Create(outFileName))
            {
                // MagicConfigの書き込み
                fs.WriteByte(source[0]);

                // MagicConfigは別途書き込んでいるので
                // MagicConfigのバイト分とばす
                for (int i = Define.MagicConfigSize; i < source.Length;)
                {
                    //int writeByte = i;
                    Mnemonic mnemonic = (Mnemonic)source[i];
                    fs.WriteByte((byte)mnemonic);
                    i++;    // 次へ

                    // ジャンプ系の実アドレスの入れ替えと
                    // 単純なOperanが分らない物は個別に書き出す
                    if (mnemonic == Mnemonic.CALL || mnemonic == Mnemonic.JMP || mnemonic == Mnemonic.JE || mnemonic == Mnemonic.JNE)
                    {
                        // 実際のジャンプアドレスに入れ替え
                        uint address = 0;
                        byte[] bytes = new byte[4];
                        Array.Copy(source, i, bytes, 0, 4);
                        int idx = Generic.ToInt32(bytes);
                        i += 4;

                        address = (uint)labels[idx].Address;
                        if (address == 0)
                        {
                            // 未定義ラベルを呼び出そうとしています。
                            return result;
                        }
                        byte[] addressByte = Generic.GetByte(labels[idx].Address);
                        fs.WriteByte(addressByte[0]);
                        fs.WriteByte(addressByte[1]);
                        fs.WriteByte(addressByte[2]);
                        fs.WriteByte(addressByte[3]);

                        continue;
                    }
                    /// オペランド(1Byte)
                    /// 文字数(4Byte)
                    /// 文字列
                    else if (mnemonic == Mnemonic.MOVS || mnemonic == Mnemonic.ADDS || mnemonic == Mnemonic.DEBUG_PUSH)
                    {
                        /// オペランド(1Byte)
                        fs.WriteByte((byte)source[i]);
                        i++;

                        /// 文字数(4Byte)
                        byte[] bytes = new byte[4];
                        Array.Copy(source, i, bytes, 0, 4);
                        int strlen = Generic.ToInt32(bytes);
                        fs.WriteByte(source[i + 0]);
                        fs.WriteByte(source[i + 1]);
                        fs.WriteByte(source[i + 2]);
                        fs.WriteByte(source[i + 3]);
                        i += 4;
                        /// 文字列
                        for (int s = 0; s < strlen; s++)
                        {
                            fs.WriteByte(source[i + s]);
                        }
                        i += strlen;
                        continue;
                    }
                    /// 文字数(4Byte)
                    /// 文字列
                    else if (mnemonic == Mnemonic.PUSHS)
                    {
                        /// 文字数(4Byte)
                        byte[] bytes = new byte[4];
                        Array.Copy(source, i, bytes, 0, 4);
                        int strlen = Generic.ToInt32(bytes);
                        fs.WriteByte(source[i + 0]);
                        fs.WriteByte(source[i + 1]);
                        fs.WriteByte(source[i + 2]);
                        fs.WriteByte(source[i + 3]);
                        i += 4;
                        /// 文字列
                        for (int s = 0; s < strlen; s++)
                        {
                            fs.WriteByte(source[i + s]);
                        }
                        i += strlen;
                        continue;
                    }
                    /// 文字数(4Byte)
                    /// 文字列
                    /// オペランド数(4Byte)
                    /// オペランド(1Byte)達
                    else if (mnemonic == Mnemonic.DEBUG_LOG)
                    {
                        /// 文字数(4Byte)
                        byte[] bytes = new byte[4];
                        Array.Copy(source, i, bytes, 0, 4);
                        int strLen = Generic.ToInt32(bytes);
                        fs.WriteByte(source[i + 0]);
                        fs.WriteByte(source[i + 1]);
                        fs.WriteByte(source[i + 2]);
                        fs.WriteByte(source[i + 3]);
                        i += 4;
                        /// 文字列
                        for (int s = 0; s < strLen; s++)
                        {
                            fs.WriteByte(source[i + s]);
                        }
                        i += strLen;

                        /// オペランド数(4Byte)
                        Array.Copy(source, i, bytes, 0, 4);
                        int operandNum = Generic.ToInt32(bytes);
                        fs.WriteByte(source[i + 0]);
                        fs.WriteByte(source[i + 1]);
                        fs.WriteByte(source[i + 2]);
                        fs.WriteByte(source[i + 3]);
                        i += 4;
                        /// オペランド(1Byte)達
                        for (int o = 0; o < operandNum; o++)
                        {
                            fs.WriteByte(source[i + o]);
                        }
                        i += operandNum;
                        continue;
                    }

                    int count = Generic.GetOperandByte(mnemonic);
                    int j = 0;
                    for (; j < count && i < source.Length; j++)
                    {
                        fs.WriteByte(source[i]);
                        i++;
                    }
                    if (j != count)
                    {
                        aa.ErrorData.Str(ERROR_TEXT.ST_GENERATE_MNIMONIC, mnemonic.ToString(), j.ToString(), count.ToString());
                        Error("");
                        return result;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// ラベルの登録
        /// </summary>
        /// <param name="funcName">登録関数名</param>
        /// <param name="address">関数のアドレス</param>
        public void Registration(string label, int address)
        {
            foreach (var l in labels)
            {
                if (l.Name == label && l.Address == 0)
                {
                    // 仮登録自体はされているので指定アドレスに書き換える
                    l.Address = address;
                    break;
                }
            }
            // 登録されていないので登録
            labels.Add(new LabelInfo(label, address));
        }

        /// <summary>
        /// ラベルの仮登録
        /// </summary>
        /// <param name="funcName">登録関数名</param>
        /// <param name="returnType">リターンタイプ</param>
        /// <param name="argTypes">仮引数タイプs</param>
        /// <param name="argNames">仮引数名s</param>
        /// <returns></returns>
        public (bool success, int index) TemporaryRegistration(string label)
        {
            for (int i = 0; i < labels.Count; i++)
            {
                if (labels[i].Name == label)
                {
                    // すでに登録済み
                    return (false, -1);
                }
            }
            labels.Add(new LabelInfo(name: label, address: 0));
            return (true, labels.Count - 1);
        }

        /// <summary>
        /// 関数のインデックスを取得
        /// </summary>
        /// <param name="funcName">関数名</param>
        /// <returns>インデックス</returns>
        public (bool success, int index) GetLabelIndex(string label)
        {
            for (int i = 0; i < labels.Count; i++)
            {
                if (labels[i].Name == label)
                {
                    // 見つかったのでインデックスを返す
                    return (true, i);
                }
            }
            return (false, -1);
        }

        /// <summary>
        /// 関数名を取得
        /// </summary>
        /// <param name="idx">インデックス</param>
        /// <returns>関数名</returns>
        public string GetLabel(int idx)
        {
            if (idx < 0 || labels.Count <= idx)
            {
                return null;
            }
            return labels[idx].Name;
        }

        /// <summary>
        /// 関数情報の取得
        /// </summary>
        /// <param name="funcName">関数名</param>
        /// <returns>関数情報</returns>
        public LabelInfo GetLabelInfo(string label)
        {
            (bool success, int index) = GetLabelIndex(label);
            if (!success)
            {
                return null;
            }
            return labels[index];
        }

        /// <summary>
        /// エラー
        /// </summary>
        /// <param name="errorStr">エラー内容</param>
        private void Error(string errorStr)
        {
            result.Log = result.Log + (result.Log.Length > 0 ? "\n" : "") + errorStr;
            result.Success = false;
        }

        public string LabelInit { get; set; } = "";
        public string LabelMain { get; set; } = "";

        private List<LabelInfo> labels = null;
        private byte[] source = null;
        private LinkResult result;
        private Assembler.AssembleArgs aa;                   // コンパイラ汎用引数
    }
}