using System;
using System.IO;
using System.Collections.Generic;

namespace MCCompilerConsole.Converter.Compiler
{
    public class Linker
    {
        /// <summary>
        /// 関数情報
        /// </summary>
        public class FuncInfo
        {
            public FuncInfo(string name = null, int address = 0, Parser.VariableType retType = null, List<Parser.VariableType> argType = null, List<string> argName = null)
            {
                this.Name = name;
                this.Address = address;
                this.ReturnType = retType;
                this.ArgType = argType;
                this.ArgName = argName;
            }
            public string Name { get; set; }
            public int Address { get; set; }
            public int ArgNum { get { return ArgType?.Count ?? 0; }  }  // 引数の数
            public Parser.VariableType ReturnType { get; set; }         // 戻り値タイプ
            public List<Parser.VariableType> ArgType { get; set; }      // 引数のタイプ
            public List<string> ArgName { get; set; }                   // 引数の名前
        }

        public Linker(Compailer.CompileArgs ca)
        {
            funcs = new List<FuncInfo>();
            result = new LinkResult();
            this.ca = ca;
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
            funcs.Clear();
            result.Initialize();
            FuncInit = "";
            FuncMain = "";
        }

        /// <summary>
        /// 関数の登録
        /// </summary>
        /// <param name="funcName">登録関数名</param>
        /// <param name="address">関数のアドレス</param>
        public void Registration(string funcName, int address)
        {
            foreach (var f in funcs)
            {
                if (f.Name == funcName && f.Address == 0)
                {
                    // 仮登録自体はされているので指定アドレスに書き換える
                    f.Address = address;
                    break;
                }
            }
            // 登録されていないので登録
            funcs.Add(new FuncInfo(funcName, address));
        }

        /// <summary>
        /// 関数の仮登録
        /// </summary>
        /// <param name="funcName">登録関数名</param>
        /// <param name="returnType">リターンタイプ</param>
        /// <param name="argTypes">仮引数タイプs</param>
        /// <param name="argNames">仮引数名s</param>
        /// <returns></returns>
        public (bool success, int index) TemporaryRegistration(string funcName, Parser.VariableType returnType, List<Parser.VariableType> argTypes, List<string> argNames)
        {
            for (int i = 0; i < funcs.Count; i++)
            {
                if (funcs[i].Name == funcName)
                {
                    // すでに登録済み
                    return (false, -1);
                }
            }
            funcs.Add(new FuncInfo(name: funcName, address: 0, retType: returnType, argType: argTypes, argName: argNames));
            return (true, funcs.Count - 1);
        }

        /// <summary>
        /// 関数のインデックスを取得
        /// </summary>
        /// <param name="funcName">関数名</param>
        /// <returns>インデックス</returns>
        public (bool success, int index) GetFuncIndex(string funcName)
        {
            for (int i = 0; i < funcs.Count; i++)
            {
                if (funcs[i].Name == funcName)
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
        public string GetFuncName(int idx)
        {
            if (idx < 0 || funcs.Count <= idx)
            {
                return null;
            }
            return funcs[idx].Name;
        }

        /// <summary>
        /// 関数情報の取得
        /// </summary>
        /// <param name="funcName">関数名</param>
        /// <returns>関数情報</returns>
        public FuncInfo GetFuncInfo(string funcName)
        {
            (bool success, int index) = GetFuncIndex(funcName);
            if (!success)
            {
                return null;
            }
            return funcs[index];
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

        public string FuncInit { get; set; } = "";
        public string FuncMain { get; set; } = "";

        private List<FuncInfo> funcs = null;
        private LinkResult result;
        private Compailer.CompileArgs ca;                   // コンパイラ汎用引数
    }
}