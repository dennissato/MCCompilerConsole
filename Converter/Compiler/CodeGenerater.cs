using System;
using System.Collections.Generic;
using System.IO;

namespace MCCompilerConsole.Converter.Compiler
{
    public class CodeGenerater
    {

        public CodeGenerater(Compailer.CompileArgs ca)
        {
            labels = new List<AssemblerLabel>();
            htmlLabel = new List<HtmlLabel>();
            labelId = 0;
            localVarIdx = 0;
            switchId = 0;
            isCodeGenError = false;
            result = new CodeGenResult();
            this.ca = ca;

            mnemonicStrLengthMax = 0;
            foreach(Mnemonic mnemonic in Enum.GetValues(typeof(Mnemonic)))
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
        /// コード作成結果
        /// </summary>
        public class CodeGenResult : ResultBase
        {
            public override void Initialize()
            {
                base.Initialize();
                this.OutFileSize = 0;
            }
            public void Set(bool success, string outFileName)
            {
                this.Success = success;
                this.OutFileName = outFileName;
            }
            public string OutFileName { get; set; }
            public int OutFileSize { get; set; }
        }

        /// <summary>
        /// asmファイルの作成
        /// </summary>
        /// <param name="sourceFileName">ソースファイル名</param>
        /// <param name="relativePath">相対パスラベル</param>
        /// <param name="parserResult">パーサーの結果</param>
        /// <param name="isEpilog">エピローグ付けるか</param>
        /// <param name="defineFileName">includeするDefineファイル名</param>
        /// <param name="isRelease">リリースモードか</param>
        /// <returns>.mcasファイル作成結果</returns>
        public CodeGenResult Do(string sourceFileName, string relativePath, Parser.ParseResult parserResult, bool isEpilog, string defineFileName, bool isRelease)
        {
            Initialize();

            string filenameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFileName);
            string asmFileName = ca.MCASDir + relativePath + filenameWithoutExtension + Define.McasEx;
            this.uniqueLabel = "L" + relativePath + filenameWithoutExtension;
            this.isRelease = isRelease;

            // 作成された構文木からasmファイルの作成
            {
                // プリプロセスの出力
                asmFileContent += "#include" + asmLine2Space + $"\"{defineFileName}\"\n";   // インクルードファイルはまとめているのでそれを読み込む
                GeneratePreprocess(parserResult.Codes);

                asmFileContent += "\n\n";

                // エピローグ
                if (isEpilog)
                {
                    string repeateStart = $"{uniqueLabel}_LRepeateStart";

                    // リピート再生(RAX == 1)の場合はグルーバル変数の初期化と初期化関数を読み飛ばす
                    WriteCode(Mnemonic.CMP, Operand.RI1, Operand.ZERO_I);
                    WriteJmpCode(Mnemonic.JNE, repeateStart);

                    // グローバル変数分をスタックに積む
                    // ゼロ初期化しておく
                    if (parserResult.GlobalVals.Count > 0)
                    {
                        WriteCode(Mnemonic.PUSH, Operand.ZERO_I);           // ローカル変数のOffset方式と同じにするため
                        WriteCode(Mnemonic.MOV, Operand.RGP, Operand.RSP);  //      仮でPUSHしておく
                        foreach (var gv in parserResult.GlobalVals)
                        {
                            GenerateInitPush(gv.ValType, Scope.GLOBAL, Operand.INVALID);
                        }
                        // グローバル変数の初期化
                        foreach (var n in parserResult.GlobalInitCodes)
                        {
                            Generate(n);
                        }
                    }
                    // グローバル変数作成後のスタック状態を保存する
                    WriteCode(Mnemonic.SAVESPS);
                    
                    // init関数の指定がある場合はInit関数に飛んでからメイン関数に飛ぶ
                    {
                        if (ca.Linker.FuncInit != "")
                        {
                            (bool success, int _idx) = ca.Linker.GetFuncIndex(ca.Linker.FuncInit);
                            if (!success)
                            {
                                Error(ca.ErrorData.Str(ERROR_TEXT.NOT_DEFINE_INITIALIZE_FUNC, ca.Linker.FuncInit));
                            }
                            WriteJmpCode(Mnemonic.CALL, ca.Linker.FuncInit);
                            WriteCode(Mnemonic.MAGICEND);
                        }
                        else
                        {   // 指定ない場合はinit関数実行と挙動を合わせる為NOPだけ実行
                            WriteCode(Mnemonic.NOP);
                            WriteCode(Mnemonic.MAGICEND);
                        }
                    }

                    // グローバル変数作成後のスタック状態に戻す
                    WriteLabel(repeateStart);
                    WriteCode(Mnemonic.LOADSPS);

                    // main関数の指定がある場合はメイン関数に飛ぶ
                    {
                        int idx = 0;
                        string epilogStr = $"{uniqueLabel}_Epilog_" + filenameWithoutExtension;
                        string jmpLabel = epilogStr;
                        if (ca.Linker.FuncMain != "")
                        {
                            (bool success, int _idx) = ca.Linker.GetFuncIndex(ca.Linker.FuncMain);
                            if (!success)
                            {
                                Error(ca.ErrorData.Str(ERROR_TEXT.NOT_DEFINE_MAIN_FUNC, ca.Linker.FuncInit));
                            }
                            idx = _idx;
                            jmpLabel = ca.Linker.FuncMain;
                        }
                        else
                        {   // 指定ない場合はmain関数実行と挙動を合わせる為NOPだけ実行
                            WriteCode(Mnemonic.NOP);
                            WriteCode(Mnemonic.MAGICEND);
                            // #initで指定された関数以外で一番上の関数にする必要がある
                            (bool success, int _idx) = ca.Linker.TemporaryRegistration(epilogStr, new Parser.VariableType(VariableKind.VOID), null, null);
                            idx = _idx;

                        }
                        WriteJmpCode(Mnemonic.CALL, jmpLabel);

                        WriteCode(Mnemonic.MAGICEND);
                        WriteLabel(epilogStr);          // jmpするかどうかは別としてラベルだけ作成しておく。epilogStr関数は下記NOPを指す
                        WriteCode(Mnemonic.NOP);
                    }
                }

                localVarIdx = 0;    // Generate内でのローカル変数のアクセスもlocalVarIdxを使うのでもとに戻しておく
                foreach (var n in parserResult.Codes)
                {
                    Generate(n);
                    localVarIdx++;  // 次のローカル変数リストへ

                    // 式の評価結果としてスタックに一つの値が残っている
                    // はずなので、スタックが溢れないようにポップしておく
                    //WriteCode(Mnemonic.POP, Operand.RI1);
                }
            }

            using (StreamWriter aSw = File.CreateText(asmFileName))
            {
                aSw.Write(asmFileContent);
                asmFileContent = "";
            }

            result.Set(!isCodeGenError, asmFileName);
            return result;
        }

        /// <summary>
        /// アセンブルファイルラベル
        /// </summary>
        private class AssemblerLabel
        {
            public AssemblerLabel(string name = null, int addr = -1)
            {
                this.Name = name;
                this.Address = addr;
            }
            public string Name { get; set; }
            public int Address { get; set; }
        }

        /// <summary>
        /// Htmlファイルラベル
        /// </summary>
        private class HtmlLabel
        {
            public enum TagKind
            {
                DIV,
                OTHER,
            }
            public HtmlLabel(string name, int addr, TagKind type)
            {
                this.Name = name;
                this.Address = addr;
                this.TagType = type;
            }
            public string Name { get; set; }
            public int Address { get; set; }
            public TagKind TagType { get; set; }
        }

        /// <summary>
        /// 変数の初期化PUSHの作成
        /// </summary>
        /// <param name="vt">変数タイプ</param>
        /// <param name="dt">デバックタイプ</param>
        /// <param name="operand">プッシュするオペランド</param>
        private void GenerateVariableInitPush(Parser.VariableType vt, DebugType dt, Operand operand)
        {
            if (vt.GetEndKind() == VariableKind.STRUCT)
            {
                return;
            }
            if (vt.Type == VariableKind.ARRAY)
            {
                for (int i = 0; i < vt.ArraySize; i++)
                {
                    GenerateVariableInitPush(vt.PointerTo, dt, operand);
                }
                return;
            }
            if (vt.Type == VariableKind.REFERENCE || vt.Type == VariableKind.BOXS)
            {
                operand = Operand.ZERO_I;
            }
            VariableKind vk = vt.Type;
            if (operand == Operand.INVALID)
            {
                switch (vk)
                {
                    case VariableKind.INT:
                        operand = Operand.ZERO_I;
                        break;

                    case VariableKind.STRING:
                        operand = Operand.ZERO_S;
                        break;

                    case VariableKind.FLOAT:
                        operand = Operand.ZERO_F;
                        break;

                    default:
                        Error(ca.ErrorData.Str(ERROR_TEXT.ST_GENERATE_INIT, dt.ToString(), vk.ToString()));
                        break;
                }
            }
            if(isRelease)
            {
                WriteCode(Mnemonic.PUSH, operand);
            }
            else
            {
                WriteDebugPush(operand, dt);
            }
            return;
        }

        /// <summary>
        /// 構造体の初期化PUSHの作成
        /// </summary>
        /// <param name="vt">変数タイプ</param>
        /// <param name="dt">デバックタイプ</param>
        /// <param name="operand">PUSHするオペランド</param>
        private void GenerateStructInitPush(Parser.VariableType vt, DebugType dt, Operand operand)
        {
            if (vt.GetEndKind() != VariableKind.STRUCT)
            {
                return;
            }
            if (vt.Type == VariableKind.ARRAY)
            {
                for (int i = 0; i < vt.ArraySize; i++)
                {
                    GenerateStructInitPush(vt.PointerTo, dt, operand);
                }
                return;
            }
            Parser.Member member = vt.Member;
            foreach(var vari in member.Variables)
            {
                VariableKind endVk = vari.ValType.GetEndKind();
                if (vari.ValType.Type == VariableKind.REFERENCE)
                {
                    // 参照なのでアドレスさえPUSHできる領域があればよい
                    // つまりPUSH(ZERO_I)だけでよい
                    if (isRelease)
                    {
                        WriteCode(Mnemonic.PUSH, Operand.ZERO_I);
                    }
                    else
                    {
                        WriteDebugPush(Operand.ZERO_I, dt);
                    }
                    continue;
                }
                if (vari.ValType.Type == VariableKind.BOXS)
                {
                    // BOXSもヒープへの参照なのでアドレスさえPUSHできる領域があればよい
                    // つまりPUSH(ZERO_I)だけでよい
                    if (isRelease)
                    {
                        WriteCode(Mnemonic.PUSH, Operand.ZERO_I);
                    }
                    else
                    {
                        WriteDebugPush(Operand.ZERO_I, dt);
                    }
                    continue;
                }
                switch (endVk)
                {
                    // Arrayか単純にプリミティブ型か
                    case VariableKind.INT:
                    case VariableKind.STRING:
                    case VariableKind.FLOAT:
                        GenerateVariableInitPush(vari.ValType, dt, operand);
                        break;

                    // structか
                    case VariableKind.STRUCT:
                        GenerateStructInitPush(vari.ValType, dt, operand);
                        break;

                    default:
                        Error(ca.ErrorData.Str(ERROR_TEXT.ST_GENERATE_INIT, dt.ToString(), endVk.ToString()));
                        break;
                }
            }
        }

        /// <summary>
        /// 変数の初期化PUSHの作成
        /// </summary>
        /// <param name="vt">変数のタイプ</param>
        /// <param name="scope">変数のスコープ</param>
        /// <param name="operand">PUSHするオペランド</param>
        private void GenerateInitPush(Parser.VariableType vt, Scope scope, Operand operand)
        {
            VariableKind vk = vt.Type;
            VariableKind endVk = vt.GetEndKind();
            bool isArray = vk == VariableKind.ARRAY;
            
            DebugType dt = DebugType.NONE;
            switch (scope)
            {
                case Scope.GLOBAL:
                    dt = isArray ? endVk == VariableKind.STRUCT ? DebugType.GlobalStructArray : DebugType.GlobalArray : endVk == VariableKind.STRUCT ? DebugType.GlobalStruct : DebugType.GlobalVariable;
                    break;

                case Scope.LOCAL:
                    dt = isArray ? endVk == VariableKind.STRUCT ? DebugType.LocalStructArray : DebugType.LocalArray : endVk == VariableKind.STRUCT ? DebugType.LocalStruct : DebugType.LocalVariable;
                    break;
            }
            if (vt.Type == VariableKind.REFERENCE)
            {
                // 参照なのでアドレスさえPUSHできる領域があればよい
                // つまりPUSH(ZERO_I)だけでよい
                if(operand == Operand.INVALID)
                {
                    WriteDebugPush(Operand.ZERO_I, dt);
                }
                else
                {
                    WriteDebugPush(operand, dt);
                }
                return;
            }
            switch (endVk)
            {
                // Arrayか単純なプリミティブ型
                case VariableKind.INT:
                case VariableKind.STRING:
                case VariableKind.FLOAT:
                    GenerateVariableInitPush(vt, dt, operand);
                    break;

                // struct
                case VariableKind.STRUCT:
                    GenerateStructInitPush(vt, dt, operand);
                    break;

                default:
                    Error(ca.ErrorData.Str(ERROR_TEXT.ST_GENERATE_INIT, dt.ToString(), vk.ToString()));
                    break;
            }
        }

        private void GeneratePreprocess(List<Parser.Node> codes)
        {
            for(int i = 0; i < codes.Count;i++)
            {
                Parser.Node node = codes[i];
                switch (node.Kind)
                {
                    case NodeKind.ONETIME:
                        asmFileContent += "#magic-onetime\n";
                        break;
                    case NodeKind.REPEATE:
                        asmFileContent += "#magic-repeate\n";
                        break;
                    case NodeKind.SKILL:
                        asmFileContent += "#skill\n";
                        break;
                }
                if(node.Block != null)
                {
                    GeneratePreprocess(node.Block);
                }
            }
        }

        /// <summary>
        /// コード作成
        /// </summary>
        /// <param name="node">作成元Node</param>
        private void Generate(Parser.Node node)
        {
            Generate(node, false);
        }

        /// <summary>
        /// デリファレンスのコード作成
        /// </summary>
        /// <param name="node">作成元Node</param>
        private void GenerateDereference(Parser.Node node)
        {
            Generate(node, true);
        }

        /// <summary>
        /// コードの作成
        /// </summary>
        /// <param name="node">作成元Node</param>
        /// <param name="dereference">デリファレンスか</param>
        private void Generate(Parser.Node node, bool dereference)
        {
            if (node == null)
            {
                return;
            }

            int id = labelId;
            labelId++;

            switch (node.Kind)
            {
                case NodeKind.ONETIME:
                case NodeKind.REPEATE:
                case NodeKind.SKILL:
                    // プリプロセスは読み飛ばす
                    return;


                case NodeKind.FUNC_DEF:
                    {
                        // プロローグ
                        Linker.FuncInfo funcInfo = node.FuncInfo;
                        string funcName = funcInfo.Name;
                        //ca.Linker.Registration(funcName, offset + outFile.Count);  // 関数のアドレスは下記PUSHの値になる
                        WriteLabel(funcName);
                        WriteCode(Mnemonic.PUSH, Operand.RBP);                  // 現在のスタックベースポインタをスタックに保存
                        WriteCode(Mnemonic.MOV, Operand.RBP, Operand.RSP);      // 現在のスタックベースポインタをスタックトップに変更

                        // 関数の引数部分も含めたローカル変数の領域確保と初期化
                        GenerateLocalVariableInit(funcInfo, ca.Parser.LVariables.Variables()[localVarIdx]);

                        // 関数の本文
                        foreach (var block in node.Block)
                        {
                            Generate(block);
                        }

                        // エピローグ
                        // 最後の式の結果がRAXに残っているのでそれが返り値になる
                        WriteCode(Mnemonic.MOV, Operand.RSP, Operand.RBP);
                        WriteCode(Mnemonic.POP, Operand.RBP);
                        WriteCode(Mnemonic.RET);
                    }
                    return;

                case NodeKind.FUNC_CALL:
                    {
                        string funcname = node.FuncInfo.Name;
                        (bool success, int regIdx) = ca.Linker.GetFuncIndex(funcname); // リンカー内のIndex番号を受け取る
                        Linker.FuncInfo funcInfo = ca.Linker.GetFuncInfo(funcname);

                        if (!success)
                        {
                            Error(ca.ErrorData.Str(ERROR_TEXT.FUNC_NOT_DEFINE, funcname));
                        }

                        foreach (var argNode in node.Block)
                        {
                            // 引数となるExpression達
                            Generate(argNode);
                        }
                        // 引数ある場合は指定レジスターに引数を入れておく
                        for (int i = funcInfo.ArgNum - 1; i >= 0; i--)
                        {
                            // 引数が配列か構造体の時は変数のアドレス値を入れておく
                            // 呼び出された先で参照先の情報をPUSHする
                            VariableKind vk = funcInfo.ArgType[i].GetEndKind();
                            WriteCode(Mnemonic.POP, Generic.GetArgRegister(i, vk));
                        }
                        WriteJmpCode(Mnemonic.CALL, funcname);

                        VariableKind retvk = funcInfo.ReturnType.GetEndKind();
                        if (retvk == VariableKind.VOID)
                        {
                            //WriteCode(Mnemonic.PUSH, Operand.RI1);          // callから戻ってきた時の戻り値をPUSHしておく
                        }
                        else
                        {
                            WriteCode(Mnemonic.PUSH, GetRegister1(retvk));  // callから戻ってきた時の戻り値をPUSHしておく
                        }
                    }
                    return;

                case NodeKind.GWST_CALL:
                    {
                        //node.Block; 引数の情報が入っている
                        foreach (var n in node.Block)
                        {
                            // 引数となるExpression達
                            Generate(n);
                        }

                        (int argNum, VariableKind retType, NodeKind[] argKinds) = Generic.GWSTInfo(node.GwstType, node.GwstNo);
                        if (retType == VariableKind.INVALID && argKinds == null)
                        {
                            Error(ca.ErrorData.Str(ERROR_TEXT.GWST_CALL_TYPE, node.GwstType.ToString(), $"{Generic.GWSTInfoLength(node.GwstType) - 1}"));
                        }
                        if (argKinds[0] != NodeKind.Other)
                        {
                            // 引数ある場合は指定レジスターに引数を入れておく
                            for (int i = argNum - 1; i >= 0; i--)
                            {
                                if (argKinds[i] != NodeKind.ADDR)
                                {
                                    // 引数が配列か構造体の時は変数のアドレス値を入れておく
                                    // 呼び出された先で参照先の情報をPUSHする
                                    VariableKind vk = Define.ToVariableKind(argKinds[i]);
                                    WriteCode(Mnemonic.POP, Generic.GetArgRegister(i, vk));
                                }
                                else
                                {
                                    // refの時。アドレスなのでINT
                                    WriteCode(Mnemonic.POP, Generic.GetArgRegister(i, VariableKind.INT));
                                }
                            }
                        }
                        switch(node.GwstType)
                        {
                            case GWSTType.gwst_lib:
                                WriteCode(Mnemonic.GWST_LIB, node.GwstNo);
                                break;
                            case GWSTType.gwst_mag:
                                WriteCode(Mnemonic.GWST_MAG, node.GwstNo);
                                break;
                            case GWSTType.gwst_smag:
                                WriteCode(Mnemonic.GWST_SMAG, node.GwstNo);
                                break;
                            case GWSTType.gwst_ui:
                                WriteCode(Mnemonic.GWST_UI, node.GwstNo);
                                break;
                            case GWSTType.gwst_meph:
                                WriteCode(Mnemonic.GWST_MEPH, node.GwstNo);
                                break;
                            case GWSTType.gwst_wamag:
                                WriteCode(Mnemonic.GWST_WAMAG, node.GwstNo);

                            // GWST_CALL
                                break;
                        }
                        if(retType != VariableKind.VOID)
                        {
                            WriteCode(Mnemonic.PUSH, GetRegister1(retType));
                        }
                    }
                    return;

                case NodeKind.SYS_CALL:
                    {
                        //node.Block; 引数の情報が入っている
                        foreach (var n in node.Block)
                        {
                            // 引数となるExpression達
                            Generate(n);
                        }

                        (int argNum, VariableKind retType, NodeKind[] argKinds) = Generic.SYSInfo(node.SysKind);
                        if (retType == VariableKind.INVALID && argKinds == null)
                        {
                            Error(ca.ErrorData.Str(ERROR_TEXT.ST_SYS_CALL_TYPE));

                        }
                        if (argKinds[0] != NodeKind.Other)
                        {
                            // 引数ある場合は指定レジスターに引数を入れておく
                            for (int i = argNum - 1; i >= 0; i--)
                            {
                                if (argKinds[i] != NodeKind.ADDR)
                                {
                                    // 引数が配列か構造体の時は変数のアドレス値を入れておく
                                    // 呼び出された先で参照先の情報をPUSHする
                                    VariableKind vk = Define.ToVariableKind(argKinds[i]);
                                    WriteCode(Mnemonic.POP, Generic.GetArgRegister(i, vk));
                                }
                                else
                                {
                                    // refの時。アドレスなのでINT
                                    WriteCode(Mnemonic.POP, Generic.GetArgRegister(i, VariableKind.INT));
                                }
                            }
                        }
                        WriteCode(Mnemonic.SYSTEM, (int)node.SysKind);
                        if (retType != VariableKind.VOID)
                        {
                            WriteCode(Mnemonic.PUSH, GetRegister1(retType));
                        }
                    }
                    return;

                case NodeKind.DEBUG_LOG:
                    {
                        if (!isRelease)
                        {
                            foreach (var argNode in node.Block)
                            {
                                // 引数となるExpression達
                                Generate(argNode);
                            }
                            // 引数ある場合は指定レジスターに引数を入れておく
                            for (int i = node.DebugArg.Count - 1; i >= 0; i--)
                            {
                                // 引数が配列か構造体の時は変数のアドレス値を入れておく
                                // 呼び出された先で参照先の情報をPUSHする
                                VariableKind vk = Define.ToVariableKind(node.DebugArg[i]);
                                WriteCode(Mnemonic.POP, Generic.GetArgRegister(i, vk));
                            }

                            // Menmonic.DEBUG_LOG , strings..., ArgNum(4Byte), Args(Operand)...
                            WriteDebugLog(ca.GetSourceStr(node.StrIdx, node.StrLen), node.DebugArg);
                        }
                    }
                    return;

                case NodeKind.DEBUG_PAUSE:
                    {

                        if (!isRelease)
                        {
                            WriteCode(Mnemonic.DEBUG_PAUSE);
                        }
                    }
                    return;

                case NodeKind.RETURN:
                    {
                        if (node.Lhs != null)
                        {
                            Generate(node.Lhs); // returnの返り値の出力
                            VariableKind vk = Define.ToVariableKind(node.Lhs.GetEndKind());
                            WriteCode(Mnemonic.POP, GetRegister1(vk));
                        }
                        WriteCode(Mnemonic.MOV, Operand.RSP, Operand.RBP);
                        WriteCode(Mnemonic.POP, Operand.RBP);
                        WriteCode(Mnemonic.RET);
                    }
                    return;

                case NodeKind.BREAK:
                    if (breakId == 0)
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.BREAK));
                    }
                    // 今の階層内のEndに飛ぶ
                    WriteJmpCode(Mnemonic.JMP, $"{uniqueLabel}__Lend{breakId}");
                    return;

                case NodeKind.CONTINUE:
                    if (continueId == 0)
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.CONTINUE));
                    }
                    // 今の階層内のTopに飛ぶ
                    WriteJmpCode(Mnemonic.JMP, $"{uniqueLabel}__Lcontinue{continueId}");
                    return;

                case NodeKind.BLOCK:
                    foreach (var nodeBlock in node.Block)
                    {
                        Generate(nodeBlock);
                        //WriteCode(Mnemonic.POP, Operand.RI1);   // statementは何かしらの結果をPUSHしているのでPOPしておく
                    }
                    return;

                case NodeKind.IF:

                    if (node.Else == null)
                    {
                        // メモ
                        // if( A ) B
                        // -- 下記アセンブラ実装 --
                        // Aをコンパイルしたコード
                        // pop rax        :: Aの結果はプッシュされているはず
                        // cmp rax, 0
                        // je .LendXXX    :: Aが0の場合はBを実行しないためジャンプ
                        // Bをコンパイルしたコード
                        //.LendXXX
                        Generate(node.Condition);                               // Aをコンパイルしたコード
                        WriteCode(Mnemonic.POP, Operand.RI1);                   // Aの結果はプッシュされているはず
                        WriteCode(Mnemonic.CMP, Operand.RI1, Operand.ZERO_I);   //
                        WriteJmpCode(Mnemonic.JE, $"{uniqueLabel}__Lend{id}");                // Aが0の場合はBを実行しないためジャンプ
                        Generate(node.Then);                                    // Bをコンパイルしたコード
                        WriteLabel($"{uniqueLabel}__Lend{id}");                               //.LendXXX
                        return;
                    }
                    else
                    {
                        // メモ
                        // if( A ) B else C
                        // -- 下記アセンブラ実装 --
                        // Aをコンパイルしたコード
                        // pop rax          :: Aの結果はプッシュされているはず
                        // cmp rax, 0
                        // je .LjmpCXXX     :: Aが0の場合はCを実行したいのでジャンプ
                        // Bをコンパイルしたコード
                        // jmp .LenbXXX     :: B実行語はCは実行しないのでCの終わりまでジャンプ
                        //.LjmpCXXX
                        // Cをコンパイルしたコード
                        //.LenbXXX

                        Generate(node.Condition);                               // Aをコンパイルしたコード
                        WriteCode(Mnemonic.POP, Operand.RI1);                   // Aの結果はプッシュされているはず
                        WriteCode(Mnemonic.CMP, Operand.RI1, Operand.ZERO_I);   //
                        WriteJmpCode(Mnemonic.JE, $"{uniqueLabel}__LjmpC{id}");// Aが0の場合はCを実行したいのでジャンプ
                        Generate(node.Then);                                    // Bをコンパイルしたコード
                        WriteJmpCode(Mnemonic.JMP, $"{uniqueLabel}__Lend{id}");// B実行語はCは実行しないのでCの終わりまでジャンプ
                        WriteLabel($"{uniqueLabel}__LjmpC{id}");               //.LjmpCXX1
                        Generate(node.Else.Lhs);                                // Cをコンパイルしたコード
                        WriteLabel($"{uniqueLabel}__Lend{id}");                //.LendXX2
                        return;
                    }

                case NodeKind.WHILE:
                    // メモ
                    // while( A ) B
                    // -- 下記アセンブラ実装 --
                    //.LstartXXX
                    // Aをコンパイルしたコード
                    // pop rax        :: Aの結果はプッシュされているはず
                    // cmp rax, 0
                    // je .LendXXX      :: Aが0の場合はBの実行をしない
                    // Bをコンパイルしたコード
                    // jmp .LstartXXX   :: whileの先頭に戻る
                    //.LendXXX
                    {
                        int bid = breakId;
                        int cid = continueId;
                        breakId = continueId = id;
                        WriteLabel($"{uniqueLabel}__Lcontinue{id}");                   //.LcontinueXXX
                        Generate(node.Condition);                                       // Aをコンパイルしたコード
                        WriteCode(Mnemonic.POP, Operand.RI1);                           // Aの結果はプッシュされているはず
                        WriteCode(Mnemonic.CMP, Operand.RI1, Operand.ZERO_I);           //
                        WriteJmpCode(Mnemonic.JE, $"{uniqueLabel}__Lend{id}");         // Aが0の場合はBの実行をしない
                        Generate(node.Then);                                            // Bをコンパイルしたコード
                        WriteJmpCode(Mnemonic.JMP, $"{uniqueLabel}__Lcontinue{id}");   // whileの先頭に戻る
                        WriteLabel($"{uniqueLabel}__Lend{id}"); 
                        breakId = bid;
                        continueId = cid;
                    }
                    return;

                case NodeKind.FOR:
                    // メモ
                    // for( A; B; C ) D
                    // -- 下記アセンブラ実装 --
                    // Aをコンパイルしたコード
                    //.LconXXX
                    // Bをコンパイルしたコード
                    // pop rax
                    // cmp rax, 0
                    // je .LendXXX
                    // Dをコンパイルしたコード
                    // Cをコンパイルしたコード
                    // jmp .LconXXX
                    //.LendXXX
                    {
                        int bid = breakId;
                        int cid = continueId;
                        breakId = continueId = id;
                        if (node.Init != null)
                        {
                            Generate(node.Init);                        // Aをコンパイルしたコード
                        }
                        WriteLabel($"{uniqueLabel}__Lcon{id}");        //.LalbeXXX1
                        if (node.Condition != null)
                        {
                            Generate(node.Condition);                   // Bをコンパイルしたコード
                            WriteCode(Mnemonic.POP, Operand.RI1);
                            WriteCode(Mnemonic.CMP, Operand.RI1, Operand.ZERO_I);
                            WriteJmpCode(Mnemonic.JE, $"{uniqueLabel}__Lend{id}");
                        }
                        Generate(node.Then);                            // Dをコンパイルしたコード
                        WriteLabel($"{uniqueLabel}__Lcontinue{id}");
                        if (node.Increment != null)
                        {
                            Generate(node.Increment);                   // Cをコンパイルしたコード
                            // cはSTMT_EXPRとしてパースしている
                            // STMT_EXPR自体でPOPしているのでここではPOPしない
                            //WriteCode(Mnemonic.POP, Operand.RI1);       // 結果がはいていいるはず
                        }
                        WriteJmpCode(Mnemonic.JMP, $"{uniqueLabel}__Lcon{id}");
                        WriteLabel($"{uniqueLabel}__Lend{id}");
                        breakId = bid;
                        continueId = cid;
                    }
                    return;

                case NodeKind.SWITCH:
                    {
                        int bid = breakId;
                        breakId = id;
                        Generate(node.Condition);
                        WriteCode(Mnemonic.POP, Operand.RI1);

                        foreach(var n in node.Cases)
                        {
                            n.CaseLabel = switchId++;
                            n.CaseEndLabel = id;

                            WriteCode(Mnemonic.PUSHI, n.ValueI);
                            WriteCode(Mnemonic.POP, Operand.RI4);
                            WriteCode(Mnemonic.CMP, Operand.RI1, Operand.RI4);
                            WriteJmpCode(Mnemonic.JE, $"{uniqueLabel}__Lcase{n.CaseLabel}");
                        }

                        if (node.DefaultCase != null)
                        {
                            node.DefaultCase.CaseLabel = switchId++;
                            node.DefaultCase.CaseEndLabel = id;
                            WriteJmpCode(Mnemonic.JMP, $"{uniqueLabel}__Lcase{node.DefaultCase.CaseLabel}");
                        }

                        WriteJmpCode(Mnemonic.JMP, $"{uniqueLabel}__Lend{id}");
                        Generate(node.Then);
                        WriteLabel($"{uniqueLabel}__Lend{id}");

                        breakId = bid;
                    }
                    return;

                case NodeKind.CASE:
                    {
                        WriteLabel($"{uniqueLabel}__Lcase{node.CaseLabel}");
                        Generate(node.Then);
                    }
                    return;

                case NodeKind.ASSIGN:
                    if (node.Lhs.Kind == NodeKind.HVAL)
                    {
                        VariableKind vk = node.Lhs.ValType.GetEndKind();
                        GenerateVariable(node.Lhs);
                        Generate(node.Rhs);

                        WriteCode(Mnemonic.POP, GetRegister2(vk));      // 2にRhs Node の値を入れる
                        WriteCode(Mnemonic.HEAPSET, GetRegister2(vk));  // heapに値をセットする
                        WriteCode(Mnemonic.PUSH, GetRegister2(vk));     // 代入結果がAssignの最終結果(代入結果はそもそもRDIに入っている)
                    }
                    else
                    {
                        VariableKind vk = node.Lhs.ValType.GetEndKind();
                        GenerateVariable(node.Lhs);
                        Generate(node.Rhs);
                        {
                            WriteCode(Mnemonic.POP, GetRegister2(vk));               // RDIにRhs Node の値を入れる
                            WriteCode(Mnemonic.POP, Operand.RI1);                    // RAXにLval のアドレス値を入れる
                            WriteCode(Mnemonic.MOV, Operand.RI1P, GetRegister2(vk)); // RAXのアドレスの場所にRDIの値を入れる
                            WriteCode(Mnemonic.PUSH, GetRegister2(vk));              // 代入結果がAssignの最終結果(代入結果はそもそもRDIに入っている)
                        }
                    }
                    return;

                case NodeKind.INTEGER:
                    WriteCode(Mnemonic.PUSHI, node.ValueI);      // pushv node.Value
                    return;

                case NodeKind.STRING:
                    WriteCode(Mnemonic.PUSHS, ca.GetSourceStr(node.StrIdx, node.StrLen));
                    return;

                case NodeKind.FLOAT:
                    WriteCode(Mnemonic.PUSHF, node.ValueF);// pushf node.Value
                    return;

                case NodeKind.GVAL:
                case NodeKind.LVAL:
                    GenerateVariable(node);
                    WriteCode(Mnemonic.POP, Operand.RI1);
                    {
                        VariableKind vk = node.ValType.GetEndKind();
                        WriteCode(Mnemonic.MOV, GetRegister1(vk), Operand.RI1P);
                        WriteCode(Mnemonic.PUSH, GetRegister1(vk));
                    }
                    return;

                // heap variable
                case NodeKind.HVAL:
                    {
                        VariableKind vk = node.ValType.GetEndKind();
                        Generate(node.Lhs); // rhpi
                        Generate(node.Rhs); // offset(rhpis)
                        WriteCode(Mnemonic.POP, Operand.RHPIS);
                        WriteCode(Mnemonic.POP, Operand.RHPI);
                        WriteCode(Mnemonic.HEAPGET, GetRegister1(vk));
                        WriteCode(Mnemonic.PUSH, GetRegister1(vk));
                    }
                    return;

                case NodeKind.GVAL_REFERENCE:
                case NodeKind.LVAL_REFERENCE:
                    GenerateVariable(node);
                    WriteCode(Mnemonic.POP, Operand.RI1);
                    WriteCode(Mnemonic.MOV, Operand.RI1, Operand.RI1P);
                    WriteCode(Mnemonic.PUSH, Operand.RI1);
                    return;

                case NodeKind.GVAL_DEREFE:
                case NodeKind.LVAL_DEREFE:
                    // この後のノードでアドレス計算をするので
                    // 元となる変数のアドレスをプッシュだけしておく
                    GenerateVariable(node);
                    return;

                case NodeKind.GVAL_DEF:
                case NodeKind.LVAL_DEF:
                    if(node.Init == null)
                    {
                        return;
                    }
                    // initがあるので初期化処理する
                    // 配列の場合は複数初期化値がある.
                    if (node.ValType.Type == VariableKind.ARRAY || node.ValType.Type == VariableKind.STRUCT)
                    {
                        foreach (var initNode in node.Init.Block)
                        {
                            VariableKind vk = Define.ToVariableKind(initNode.GetEndKind());
                            Generate(initNode);                                // Assignまで行う。Assignの結果がPUSHされているので
                            WriteCode(Mnemonic.POP, GetRegister2(vk));         // 結果はいらないのでPOPしておく
                        }
                    }
                    else if(node.ValType.Type ==  VariableKind.REFERENCE)
                    {
                        GenerateVariable(node);
                        GenerateDereference(node.Init);
                        WriteCode(Mnemonic.POP, Operand.RI2);               // RDIにInit の値を入れる
                        WriteCode(Mnemonic.POP, Operand.RI1);               // RAXに変数のアドレス値を入れる
                        WriteCode(Mnemonic.MOV, Operand.RI1P, Operand.RI2); // RAXのアドレスの場所にRDIの値を入れる
                    }
                    else
                    {
                        GenerateVariable(node);
                        Generate(node.Init);
                        VariableKind vk = node.ValType.GetEndKind();
                        WriteCode(Mnemonic.POP, GetRegister2(vk));              // RDIにInit の値を入れる
                        WriteCode(Mnemonic.POP, Operand.RI1);                   // RAXに変数のアドレス値を入れる
                        WriteCode(Mnemonic.MOV, Operand.RI1P, GetRegister2(vk));// RAXのアドレスの場所にRDIの値を入れる
                    }
                    return;

                case NodeKind.DEREFE:
                    GenerateDereference(node.Lhs);
                    WriteCode(Mnemonic.POP, Operand.RI1);
                    {
                        VariableKind vk = node.ValType.GetEndKind();
                        WriteCode(Mnemonic.MOV, GetRegister1(vk), Operand.RI1P);
                        WriteCode(Mnemonic.PUSH, GetRegister1(vk));
                    }
                    return;

                case NodeKind.ADDR:
                    GenerateDereference(node.Lhs);
                    WriteCode(Mnemonic.POP, Operand.RI1);
                    WriteCode(Mnemonic.PUSH, Operand.RI1);
                    return;

                case NodeKind.REF_ADDR:
                    GenerateDereference(node.Lhs);
                    WriteCode(Mnemonic.POP, Operand.RI1);
                    WriteCode(Mnemonic.MOV, Operand.RI1, Operand.RI1P);
                    WriteCode(Mnemonic.PUSH, Operand.RI1);
                    return;

                case NodeKind.STMT_EXPR:
                    Generate(node.Then);
                    NodeKind nodeKind = node.Then.GetEndKind();
                    bool NoReturnValue = false;
                    {
                        // user function call
                        bool isUserFunctionNoReturnValue = (node.Then.Kind == NodeKind.FUNC_CALL && node.Then.FuncInfo.ReturnType.Type == VariableKind.VOID);

                        // built in function call
                        bool isGwstCallNoReturnValue = (node.Then.Kind == NodeKind.GWST_CALL && Generic.GWSTInfo(node.Then.GwstType, node.Then.GwstNo).RetType == VariableKind.VOID);
                        bool isSystemCallNoReturnValue = (node.Then.Kind == NodeKind.SYS_CALL && Generic.SYSInfo(node.SysKind).RetType == VariableKind.VOID);
                        bool isDebugFunc = (node.Then.Kind == NodeKind.DEBUG_LOG || node.Then.Kind == NodeKind.DEBUG_PAUSE);
                        bool isRelase = (node.Then.Kind == NodeKind.RELEASE);

                        NoReturnValue = isUserFunctionNoReturnValue || isGwstCallNoReturnValue || isSystemCallNoReturnValue || isDebugFunc || isRelase;
                    }
                    if (NoReturnValue)
                    {
                        // 戻り値がvoidの関数は呼び出しは何もしない。
                    }
                    else
                    {
                        WriteCode(Mnemonic.POP, GetRegister1(Define.ToVariableKind(nodeKind)));       // 何かしらの結果が入っているので出しておく
                    }
                    return;

                case NodeKind.ALLOCATE:
                    Generate(node.Lhs); // 確保サイズの生成
                    WriteCode(Mnemonic.POP, Generic.GetArgRegister(0, VariableKind.INT));
                    WriteCode(Mnemonic.HEAPALLOCATE, Generic.GetArgRegister(0, VariableKind.INT));
                    WriteCode(Mnemonic.PUSH, Generic.GetReturnRegister(VariableKind.INT));
                    return;

                case NodeKind.RELEASE:;
                    GenerateVariable(node.Then); // 解放先Rhpi値入れる
                    WriteCode(Mnemonic.HEAPRELEASE, Operand.RHPI);
                    return;
            }

            // 以降　左側ノード 、 右側ノードがあるKindの処理
            if (node.Lhs == null || node.Rhs == null)
            {
                Error(ca.ErrorData.Str(ERROR_TEXT.ST_LR_NODE_NULL));
                return;
            }

            Generate(node.Lhs);
            Generate(node.Rhs);

            // 左右どちらかのノードをしらべて
            // int, float, string どのレジスタを使えばいいか見つける
            NodeKind nk = node.GetEndKind();
            if (dereference)
            {
                // Dereferenceのアドレス計算の四則演算はINTEGERとして扱いたい。
                nk = NodeKind.INTEGER;
            }

            // 左側ノードと右側ノードの値がスタックにpushされている
            Operand operand1 = Operand.INVALID;
            Operand operand2 = Operand.INVALID;
            switch(nk)
            {
                case NodeKind.INTEGER:
                    operand1 = Operand.RI1;
                    operand2 = Operand.RI2;
                    break;

                case NodeKind.STRING:
                    operand1 = Operand.RS1;
                    operand2 = Operand.RS2;
                    break;

                case NodeKind.FLOAT:
                    operand1 = Operand.RF1;
                    operand2 = Operand.RF2;
                    break;
            }
            if (operand1 == Operand.INVALID || operand2 == Operand.INVALID)
            {
                Error(ca.ErrorData.Str(ERROR_TEXT.ST_INVALID_OPERAND));
            }
            WriteCode(Mnemonic.POP, operand2);   // RDIにRhs Node の値を入れる
            WriteCode(Mnemonic.POP, operand1);   // RAXにLhs Node の値を入れる

            switch (node.Kind)
            {
                case NodeKind.ADD:
                    WriteCode(Mnemonic.ADD, operand1, operand2);
                    WriteCode(Mnemonic.PUSH, operand1);  //何かしらの結果をスタックのトップに入れておく
                    break;

                case NodeKind.SUB:
                    WriteCode(Mnemonic.SUB, operand1, operand2);
                    WriteCode(Mnemonic.PUSH, operand1);  //何かしらの結果をスタックのトップに入れておく
                    break;

                case NodeKind.MUL:
                    WriteCode(Mnemonic.IMUL, operand1, operand2);
                    WriteCode(Mnemonic.PUSH, operand1);  //何かしらの結果をスタックのトップに入れておく
                    break;

                case NodeKind.DIV:
                    WriteCode(Mnemonic.IDIV, operand1, operand2); // (商)EAX　(余)EDX
                    WriteCode(Mnemonic.PUSH, operand1);  //何かしらの結果をスタックのトップに入れておく
                    break;

                case NodeKind.EQ:
                    // cmp rax, rdi
                    // sete al
                    // movzb rax, al

                    WriteCode(Mnemonic.CMP, operand1, operand2);
                    WriteCode(Mnemonic.SETE, Operand.RI1);  // アセンブラだとseteに指定できるのは8bitレジスタだが
                                                            // RAXレジスタにもセットできるようにしているのでここでは
                                                            // movzb rax, al　のコードは出力しない

                    WriteCode(Mnemonic.PUSH, Operand.RI1);  //何かしらの結果をスタックのトップに入れておく
                    break;

                case NodeKind.NE:
                    // cmp rax, rdi
                    // setne al
                    // movzb rax, al

                    WriteCode(Mnemonic.CMP, operand1, operand2);
                    WriteCode(Mnemonic.SETNE, Operand.RI1);
                    WriteCode(Mnemonic.PUSH, Operand.RI1);  //何かしらの結果をスタックのトップに入れておく
                    break;

                case NodeKind.LT:
                    // cmp rax, rdi
                    // setl al
                    // movzb rax, al

                    WriteCode(Mnemonic.CMP, operand1, operand2);
                    WriteCode(Mnemonic.SETL, Operand.RI1);
                    WriteCode(Mnemonic.PUSH, Operand.RI1);  //何かしらの結果をスタックのトップに入れておく
                    break;

                case NodeKind.LTE:
                    // cmp rax, rdi
                    // setle al
                    // movzb rax, al

                    WriteCode(Mnemonic.CMP, operand1, operand2);
                    WriteCode(Mnemonic.SETLE, Operand.RI1);
                    WriteCode(Mnemonic.PUSH, Operand.RI1);  //何かしらの結果をスタックのトップに入れておく
                    break;

                default:
                    break;
            }

            // 不動小数や文字列の比較の場合はRAXの値を入れたい
            //WriteCode(Mnemonic.PUSH, operand1);  //何かしらの結果をスタックのトップに入れておく
        }

        /// <summary>
        /// 指定変数タイプのレジスター1取得
        /// </summary>
        /// <param name="vk">変数タイプ</param>
        /// <returns>レジスター</returns>
        private Operand GetRegister1(VariableKind vk)
        {
            switch (vk)
            {
                case VariableKind.INT:
                    return Operand.RI1;
                    
                case VariableKind.STRING:
                    return Operand.RS1;

                case VariableKind.FLOAT:
                    return Operand.RF1;
            }
            return Operand.INVALID;
        }

        /// <summary>
        /// 指定変数タイプのレジスター2取得
        /// </summary>
        /// <param name="vk">変数タイプ</param>
        /// <returns>レジスター</returns>
        private Operand GetRegister2(VariableKind vk)
        {
            switch (vk)
            {
                case VariableKind.INT:
                    return Operand.RI2;

                case VariableKind.STRING:
                    return Operand.RS2;

                case VariableKind.FLOAT:
                    return Operand.RF2;
            }
            return Operand.INVALID;
        }

        /// <summary>
        /// 変数のアドレスをRAXにプッシュする
        /// </summary>
        /// <param name="node">作成元Node</param>
        private void GenerateVariable(Parser.Node node)
        {
            if (!Generic.IsVariableKind(node.Kind))
            {
                if (node.Kind == NodeKind.DEREFE)
                {
                    // デリファレンスなので
                    GenerateDereference(node.Lhs);
                    return;
                }
                // ヒープ変数のアクセス生成
                if (node.Kind == NodeKind.HVAL)
                {
                    Generate(node.Lhs);     // RHPI 値の生成
                    if (node.Rhs != null)
                    {
                        Generate(node.Rhs); // RHPIS値の生成
                        WriteCode(Mnemonic.POP, Operand.RHPIS);
                    }
                    WriteCode(Mnemonic.POP, Operand.RHPI);
                    return;
                }
                // ※エラー報告
                Error(ca.ErrorData.Str(ERROR_TEXT.ASSIGN_LEFT));
            }

            if (Define.ToScope(node.Kind) == Scope.GLOBAL)
            {
                WriteCode(Mnemonic.MOV, Operand.RI1, Operand.RGP);
            }
            else
            {
                WriteCode(Mnemonic.MOV, Operand.RI1, Operand.RBP);
            }
            WriteCode(Mnemonic.ADDI, Operand.RI1, node.Offset); // addi rax, node.Offset
            WriteCode(Mnemonic.PUSH, Operand.RI1);              // 変数のアドレスをプッシュする

        }

        /// <summary>
        /// ローカル変数の初期化コード作成
        /// </summary>
        /// <param name="funcInfo">関数情報</param>
        /// <param name="variables">初期化する変数たち</param>
        private void GenerateLocalVariableInit(Linker.FuncInfo funcInfo, List<Parser.Variable> variables)
        {
            // 関数の引数分スタックを進める
            // RBPに近い順に第1引数、第2引数...としていく。
            // 引数の数を見てPUSHする数を変える
            // TODO 現在は第6引数まで対応
            int debugVariableCount = 0;
            if (funcInfo.ArgNum > 0)
            {
                for (int i = 0; i < funcInfo.ArgNum; i++)
                {
                    Parser.VariableType vt = funcInfo.ArgType[i];
                    VariableKind vk = vt.Type;
                    Operand operand = (vk == VariableKind.ARRAY || vk == VariableKind.STRUCT) ? Operand.INVALID : Generic.GetArgRegister(i, vt.GetEndKind());
                    GenerateInitPush(vt, Scope.LOCAL, operand);
                    debugVariableCount++;
                }
            }
            // ローカル変数分をスタックに積む
            // 定義順上関数の引数が先に定義されているので
            // そのほかの定義分のスタックあける
            // ゼロ初期化しておく
            if (variables.Count > 0)
            {
                for (int i = funcInfo.ArgNum; i < variables.Count; i++)
                {
                    Parser.Variable v = variables[i];
                    GenerateInitPush(v.ValType, Scope.LOCAL, Operand.INVALID);
                    debugVariableCount++;
                }
            }
            if (debugVariableCount != variables.Count)
            {
                Error(ca.ErrorData.Str(ERROR_TEXT.ST_LOCAL_STACK));
            }

            // 関数の引数初期化の続き
            // 配列、構造体の値受け渡し
            if (funcInfo.ArgNum > 0)
            {
                for (int i = 0; i < funcInfo.ArgNum; i++)
                {
                    //WriteCode(Mnemonic.PUSH, MagicRun.GetArgRegister(i, vk));
                    //WriteDebugPush(MagicRun.GetArgRegister(i, vk), DebugType.LVARIABLE);
                    Parser.VariableType vt = funcInfo.ArgType[i];
                    string argName = funcInfo.ArgName[i];
                    VariableKind vk = vt.Type;
                    if (vk != VariableKind.ARRAY && vk != VariableKind.STRUCT)
                    {
                        continue;
                    }
                    Parser.Variable variable = null;
                    foreach(var v in variables)
                    {
                        if(argName == v.Name)
                        {
                            variable = v;
                            break;
                        }
                    }
                    if (variable == null)
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.NOT_DEFINE_VARIABLE, argName));
                        return;
                    }

                    // src アドレスの保存
                    WriteCode(Mnemonic.PUSH, Generic.GetArgRegister(i, vt.GetEndKind()));

                    // dest アドレスの保存
                    WriteCode(Mnemonic.MOV, Operand.RI1, Operand.RBP);
                    WriteCode(Mnemonic.ADDI, Operand.RI1, variable.Offset); // addi rax, variable.Offset
                    WriteCode(Mnemonic.PUSH, Operand.RI1);                  // 変数のアドレスをプッシュする

                    // mem copy　処理
                    WriteCode(Mnemonic.POP, Operand.RI1);
                    WriteCode(Mnemonic.POP, Operand.RI2);
                    WriteMemcopy(Operand.RI1, Operand.RI2, vt.GetSize());
                }
            }
        }

        /// <summary>
        /// jmp系コードの書き込み
        /// </summary>
        /// <param name="mnemonic"></param>
        /// <param name="jmplabel"></param>
        private void WriteJmpCode(Mnemonic mnemonic, string jmplabel)
        {
            string writeStr = asmLine4Space + mnemonic.ToString() + GetSpaceAfterMnemonic(mnemonic) + asmLine2Space + $"\"{jmplabel}\"";
            asmFileContent += writeStr + "\n";
        }

        /// <summary>
        /// コードの書き込み
        /// </summary>
        /// <param name="mnemonic">ニーモニック</param>
        /// <param name="operand1">オペランド1</param>
        /// <param name="operand2">オペランド2</param>
        private void WriteCode(Mnemonic mnemonic, Operand operand1 = Operand.INVALID, Operand operand2 = Operand.INVALID)
        {
            string writeStr = asmLine4Space + mnemonic.ToString();
            if (operand1 != Operand.INVALID) writeStr += GetSpaceAfterMnemonic(mnemonic) + asmLine2Space + operand1.ToString();
            if (operand2 != Operand.INVALID) writeStr += GetSpaceAfterOperand(operand1)  + asmLine2Space + operand2.ToString();
            asmFileContent += writeStr + "\n";
        }
        private void WriteCode(Mnemonic mnemonic, Operand operand1, int value)
        {
            string writeStr = asmLine4Space + mnemonic.ToString();
            writeStr += GetSpaceAfterMnemonic(mnemonic) + asmLine2Space + operand1.ToString();
            writeStr += GetSpaceAfterOperand(operand1) + asmLine2Space + value.ToString();
            asmFileContent += writeStr + "\n";
        }

        private void WriteCode(Mnemonic mnemonic, int value)
        {

            string writeStr = asmLine4Space + mnemonic.ToString();
            writeStr += GetSpaceAfterMnemonic(mnemonic) + asmLine2Space + value.ToString();
            asmFileContent += writeStr + "\n";
        }
        private void WriteCode(Mnemonic mnemonic, float value)
        {

            string writeStr = asmLine4Space + mnemonic.ToString();
            writeStr += GetSpaceAfterMnemonic(mnemonic) + asmLine2Space + value.ToString();
            asmFileContent += writeStr + "\n";
        }
        private void WriteCode(Mnemonic mnemonic, string value)
        {

            string writeStr = asmLine4Space + mnemonic.ToString();
            writeStr += GetSpaceAfterMnemonic(mnemonic) + asmLine2Space + $"\"{value}\"";
            asmFileContent += writeStr + "\n";
        }

        /// <summary>
        /// ラベルの書き込み
        /// </summary>
        /// <param name="label"></param>
        private void WriteLabel(string label)
        {
            asmFileContent += label + ":\n";
        }

        /// <summary>
        /// Debug Pushの書き込み
        /// </summary>
        /// <param name="operand">オペランド</param>
        /// <param name="debugType">デバックタイプ</param>
        private void WriteDebugPush(Operand operand, DebugType debugType)
        {
            string writeStr = asmLine4Space + Mnemonic.DEBUG_PUSH.ToString();
            writeStr += GetSpaceAfterMnemonic(Mnemonic.DEBUG_PUSH) + asmLine2Space + operand.ToString();
            writeStr += GetSpaceAfterOperand(operand) + asmLine2Space + $"\"{debugType.ToString()}\"";
            asmFileContent += writeStr + "\n";
        }

        /// <summary>
        /// debug logの書き込み
        /// </summary>
        /// <param name="str"></param>
        /// <param name="nodes"></param>
        private void WriteDebugLog(string str, List<NodeKind> nodes)
        {
            string writeStr = asmLine4Space + Mnemonic.DEBUG_LOG.ToString() + GetSpaceAfterMnemonic(Mnemonic.DEBUG_LOG) + asmLine2Space;
            writeStr +=  $"\"{str}\"" + asmLine2Space + nodes.Count.ToString();
            for (int i = 0; i < nodes.Count; i++)
            {
                writeStr += asmLine2Space + Generic.GetArgRegister(i, Define.ToVariableKind(nodes[i])).ToString();
            }
            writeStr += "\n";
            asmFileContent += writeStr;
        }

        /// <summary>
        /// memcopyの書き込み
        /// </summary>
        /// <param name="operand1"></param>
        /// <param name="operand2"></param>
        /// <param name="value"></param>
        private void WriteMemcopy(Operand operand1, Operand operand2, int value)
        {
            string write = asmLine4Space + Mnemonic.MEMCOPY.ToString();
            write += GetSpaceAfterMnemonic(Mnemonic.DEBUG_LOG) + asmLine2Space + operand1.ToString();
            write += asmLine2Space + operand2.ToString();
            write += asmLine2Space + value.ToString();
            asmFileContent += write + "\n";
        }

        /// <summary>
        /// ニーモニック後のスペース取得
        /// </summary>
        /// <param name="mnemonic"></param>
        /// <returns></returns>
        private string GetSpaceAfterMnemonic(Mnemonic mnemonic)
        {
            int spaceNum = mnemonicStrLengthMax - mnemonic.ToString().Length;
            return new string(' ', spaceNum);
        }

        /// <summary>
        /// オペランド後のスペース取得
        /// </summary>
        /// <param name="operand"></param>
        /// <returns></returns>
        private string GetSpaceAfterOperand(Operand operand)
        {
            int spaceNum = operandStrLengthMax - operand.ToString().Length;
            return new string(' ', spaceNum);
        }


        /// <summary>
        /// エラー
        /// </summary>
        /// <param name="errorStr">エラー内容</param>
        private void Error(string errorStr)
        {
            result.Log = result.Log + (result.Log.Length > 0 ? "\n" : "") + ca.ErrorData.Str(ERROR_TEXT.ERROR_BASE_FILENAME_LINENO, ca.File, "", "", errorStr);
            result.Success = false;
            isCodeGenError = true;
        }

        /// <summary>
        /// 初期化
        /// </summary>
        private void Initialize()
        {
            labels.Clear();
            htmlLabel.Clear();
            labelId = 0;
            breakId = 0;
            continueId = 0;
            localVarIdx = 0;
            isCodeGenError = false;
            result.Initialize();
            asmFileContent = null;
            uniqueLabel = "";
        }

        private readonly string asmLine4Space = "    ";
        private readonly string asmLine2Space = "  ";

        private List<AssemblerLabel> labels;    // アセンブララベル
        private List<HtmlLabel> htmlLabel;      // アセンブラファイル用
        private int labelId;                    // アセンブララベルのID
        private int breakId;                    // break用のID
        private int continueId;                 // continue用のID
        private int switchId;                   // switch用のID
        private int localVarIdx;                // ローカル変数のリストの現在のIdx
        private bool isCodeGenError;            // コード作成でエラーが出たか
        private CodeGenResult result;           // コード生成の結果
        private Compailer.CompileArgs ca;       // コンパイラ汎用引数
        private string asmFileContent;          // アセンブラファイルの内容
        private int mnemonicStrLengthMax;       // ニーモニックの一番大きい文字数数
        private int operandStrLengthMax;        // オペランドの一番大きい文字数数
        private string uniqueLabel;
        private bool isRelease;
    }
}
