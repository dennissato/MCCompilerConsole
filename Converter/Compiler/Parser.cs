using System.Text;
using System.Collections.Generic;

namespace MCCompilerConsole.Converter.Compiler
{
    public class Parser
    {
        static public readonly (string str, VariableKind kind)[] PrimitiveType =
        {
            ("int",VariableKind.INT),
            ("float",VariableKind.FLOAT),
            ("string",VariableKind.STRING),
            ("void",VariableKind.VOID),
            ("boxs",VariableKind.BOXS),
        };

        static public readonly (string str, VariableKind kind)[] BoxsType =
        {
            ("int",VariableKind.INT),
            ("float",VariableKind.FLOAT),
            ("string",VariableKind.STRING),
        };

        public Parser(Compailer.CompileArgs ca)
        {
            currentToken = null;
            codes = new List<Node>();
            globalInitCodes = new List<Node>();
            globalVariables = new List<Variable>();
            members = new List<Member>();
            enums = new List<Enum>();
            LVariables = new LocalVariables();
            currentFuncRetVarType = VariableKind.INVALID;
            isParceError = false;
            result = new ParseResult();
            this.ca = ca;
        }

        /// <summary>
        /// ノード
        /// </summary>
        public class Node
        {
            public Node(NodeKind kind = NodeKind.Other, int valueI = 0, float valueF = 0.0f, Node lhs = null, Node rhs = null)
            {
                this.Kind = kind;
                this.Lhs = lhs;
                this.Rhs = rhs;
                this.ValueI = valueI;
                this.ValueF = valueF;
                this.Offset = 0;

                this.Init = null;
                this.Condition = null;
                this.Then = null;
                this.Else = null;
                this.Increment = null;

                this.Block = null;

                this.StrIdx = 0;
                this.StrLen = 0;

                this.FuncInfo = null;

                this.ValType = null;

                this.GwstType = GWSTType.Invalid;
                this.GwstNo = 0;
            }
            public NodeKind Kind { get; set; }  // 種類
            public Node Lhs { get; set; }       // Left Hand Side.　左側のノード
            public Node Rhs { get; set; }       // Right Hand　Side. 右側のノード
            public int ValueI { get; set; }     // NodeKind.INTEGERの場合の使用。値
            public float ValueF { get; set; }   // NodeKind.FLOATの場合の使用。値
            public int Offset { get; set; }     // NodeKind.VLALの場合のみ使用。RBPからのオフセット（ローカル変数のアドレス）

            // if , for, 変数宣言 用
            public Node Init { get; set; }      // for,変数宣言 初期化部分
            public Node Condition { get; set; } // while,forの継続判定
            public Node Then { get; set; }      // if,while,for の実行部分
            public Node Else { get; set; }      // else
            public Node Increment { get; set; } // forのインクリメント部分

            // switch用
            public List<Node> Cases { get; set; }   // case : ... case :
            public Node DefaultCase { get; set; }
            public int CaseLabel { get; set; }      // アセンブラ用のラベルID
            public int CaseEndLabel { get; set; }   // アセンブラ用のラベルID

            // blok用
            public List<Node> Block { get; set; }   //  { ... }

            // 呼び出し用
            public int StrIdx { get; set; }                 // 識別子
            public int StrLen { get; set; }                 // 識別子の長さ

            // 関数情報
            public Linker.FuncInfo FuncInfo { get; set; }   // 関数情報

            // 変数用
            public VariableType ValType { get; set; }

            // c用
            public GWSTType GwstType { get; set; }
            public int GwstNo { get; set; }

            public SystemKind SysKind { get; set; }

            // Debug Log用　引数の型保存
            public List<NodeKind> DebugArg { get; set; }

            public NodeKind GetEndKind()
            {
                // INTEGER
                // STRING
                // FLOAT
                // を返す
                if (Lhs == null && Rhs == null)
                {
                    switch (Kind)
                    {
                        case NodeKind.INTEGER:
                        case NodeKind.STRING:
                        case NodeKind.FLOAT:
                            return Kind;

                        case NodeKind.GVAL:
                        case NodeKind.LVAL:
                        case NodeKind.GVAL_REFERENCE:
                        case NodeKind.LVAL_REFERENCE:
                        case NodeKind.GVAL_DEREFE:
                        case NodeKind.LVAL_DEREFE:
                        case NodeKind.GVAL_DEF:
                        case NodeKind.LVAL_DEF:
                            return Converter.Define.ToNodeKind(ValType.GetEndKind());

                        case NodeKind.FUNC_CALL:
                            // 呼び出す関数の戻り値を返す
                            return Converter.Define.ToNodeKind(FuncInfo.ReturnType.GetEndKind());

                        case NodeKind.GWST_CALL:
                            // 関数の戻り値を返す
                            return Converter.Define.ToNodeKind(Generic.GWSTInfo(GwstType, GwstNo).RetType);

                        case NodeKind.SYS_CALL:
                            // 関数の戻り値を返す
                            return Converter.Define.ToNodeKind(Generic.SYSInfo(SysKind).RetType);
                    }
                    return NodeKind.Other;
                }
                return Lhs?.GetEndKind() ?? Rhs?.GetEndKind() ?? NodeKind.Other;
            }

            public string DebugArgToString()
            {
                string ret = "";
                foreach (var kind in DebugArg)
                {
                    ret += $"{kind.ToString().ToLower()}, ";
                }
                return ret;
            }
        }

        /// <summary>
        /// 変数
        /// </summary>
        public class Variable
        {
            public Variable(string name, VariableType valtype, int offset = 0)
            {
                this.Name = name;
                this.ValType = valtype;
                this.Offset = offset;
            }
            public string Name { get; set; }    // 変数名
            public int Offset { get; set; }     // RBPからのオフセット
            public int Size { get { return ValType?.GetSize() ?? 1; } } // 変数の大きさ
            public VariableType ValType { get; set; }

            public string FileName { get; set; }
        }

        /// <summary>
        /// 変数のタイプ
        /// </summary>
        public class VariableType
        {
            public VariableType(VariableKind type = VariableKind.INT, VariableType ptrto = null)
            {
                this.Type = type;
                this.PointerTo = ptrto;
            }
            public VariableKind Type { get; set; }

            public VariableType PointerTo { get; set; }

            public int ArraySize { get; set; }

            public Member Member { get; set; }

            public VariableKind GetEndKind()
            {
                VariableType vt = PointerTo;
                if (vt == null)
                {
                    return Type;
                }
                while (vt.PointerTo != null)
                {
                    vt = vt.PointerTo;
                }
                return vt.Type;
            }
            public int GetSize()
            {
                if (PointerTo == null)
                {
                    return Member?.Size ?? 1;
                }
                if (Type == VariableKind.REFERENCE || Type == VariableKind.BOXS)
                {
                    return 1;
                }
                return ArraySize * PointerTo.GetSize();
            }
            public Member GetMember()
            {
                return Member ?? PointerTo?.GetMember() ?? null;
            }
            public bool TypeCheck(VariableType vt)
            {
                if (vt == null)
                {
                    return false;
                }

                switch (Type)
                {
                    case VariableKind.INT:
                    case VariableKind.STRING:
                    case VariableKind.FLOAT:
                        return Type == vt.Type;

                    case VariableKind.BOXS:
                        return vt.Type == VariableKind.BOXS && (PointerTo?.TypeCheck(vt.PointerTo) ?? false);

                    case VariableKind.STRUCT:
                        return Member.Name == (vt.Member?.Name ?? null);

                    case VariableKind.ARRAY:
                        return ArraySize == vt.ArraySize && (PointerTo?.TypeCheck(vt.PointerTo) ?? false);
                }
                return false;
            }
            public bool Equal(VariableType vt)
            {
                VariableType myVt = this;
                while (myVt != null && vt != null)
                {
                    if (!(myVt.Type == vt.Type && myVt.ArraySize == vt.ArraySize))
                    {
                        return false;
                    }
                    if (!((myVt.Member?.Name ?? null) == (vt.Member?.Name ?? null)))
                    {
                        return false;
                    }
                    myVt = myVt.PointerTo;
                    vt = vt.PointerTo;
                }
                if (myVt != null || vt != null)
                {
                    return false;
                }
                return true;
            }
            public string ToTypeString()
            {
                string refstr = "";
                string typestr = "";
                string arraystr = "";
                VariableType variType = PointerTo;

                if (variType.Type == VariableKind.REFERENCE)
                {
                    refstr = "ref ";
                    variType = variType.PointerTo;
                }

                while (variType != null)
                {
                    switch (variType.Type)
                    {
                        case VariableKind.ARRAY:
                            arraystr += $"[{variType.ArraySize}]";
                            break;

                        case VariableKind.STRING:
                            typestr += variType.Member.Name;
                            break;

                        default:
                            typestr += variType.ToString().ToLower();
                            break;
                    }
                    variType = variType.PointerTo;
                }
                return refstr + typestr + arraystr;
            }
        }

        /// <summary>
        /// 構造体情報
        /// </summary>
        public class Member
        {
            public Member()
            {
                Variables = new List<Variable>();
                IsDefined = false;
            }

            public string Name { get; set; }    // 構造体名
            public int Size { get; set; }       // 構造体サイズ
            public List<Variable> Variables { get; set; }
            public bool IsDefined { get; set; }
            public string FileName { get; set; }
            public int LineNo { get; set; }
            public bool ReSize(List<Member> members)
            {
                int size = 0;
                foreach (var variable in Variables)
                {
                    switch (variable.ValType.GetEndKind())
                    {
                        case VariableKind.INT:
                        case VariableKind.STRING:
                        case VariableKind.FLOAT:
                            variable.Offset = size;
                            size += variable.Size;
                            break;

                        case VariableKind.STRUCT:
                            Member child = null;
                            foreach (var m in members)
                            {
                                if (variable.ValType.GetMember().Name == m.Name)
                                {
                                    child = m;
                                    break;
                                }
                            }
                            bool success = child?.ReSize(members) ?? false;
                            if (!success)
                            {
                                return false;
                            }
                            variable.Offset = size;
                            size += variable.Size;
                            break;

                        default:
                            // 想定していない型です。
                            return false;
                    }
                }
                this.Size = size;
                return true;
            }
            public bool IsNested(string name)
            {
                bool isNested = false;
                foreach (var v in Variables)
                {
                    if (v.ValType.GetEndKind() == VariableKind.STRUCT)
                    {
                        isNested = isNested || v.ValType.GetMember().Name == name || v.ValType.GetMember().IsNested(name);
                    }
                }
                return isNested;
            }
        }

        /// <summary>
        /// ENUM情報
        /// </summary>
        public class Enum
        {
            public Enum()
            {
                Elements = new List<(string, int)>();
            }

            public string Name { get; set; }
            public List<(string name, int value)> Elements { get; set; }
            public string FileName { get; set; }

            public bool IsDefine(string name)
            {
                foreach (var e in Elements)
                {
                    if (e.name == name)
                    {
                        return true;
                    }
                }
                return false;
            }

            public (bool define, int value) Value(string name)
            {
                foreach (var e in Elements)
                {
                    if (e.name == name)
                    {
                        return (true, e.value);
                    }
                }
                return (false, 0);
            }
        }

        /// <summary>
        /// ローカル変数情報
        /// </summary>
        public class LocalVariables
        {
            private class BlockVariables
            {
                public BlockVariables()
                {
                    this.Parent = null;
                    this.Bro = null;
                    this.Child = null;
                    this.Variables = new List<Variable>();
                }
                public BlockVariables Parent { get; set; }
                public BlockVariables Bro { get; set; }
                public BlockVariables Child { get; set; }
                public List<Variable> Variables { get; set; }
            }

            public LocalVariables()
            {
                linearList = new List<List<Variable>>();
                hierarchicalList = new List<BlockVariables>();
                varIdx = 0;
                currentVariables = null;
            }

            public void Initialize()
            {
                linearList.Clear();
                hierarchicalList.Clear();
                varIdx = 0;
                currentVariables = null;
            }

            public void NextBlock()
            {
                linearList.Add(new List<Variable>());
                hierarchicalList.Add(new BlockVariables());
                varIdx = hierarchicalList.Count - 1;
                currentVariables = hierarchicalList[varIdx];
            }
            public void DeleteBlock()
            {
                if (linearList.Count <= 0)
                {
                    return;
                }
                linearList.RemoveAt(varIdx);
                hierarchicalList.RemoveAt(varIdx);
                varIdx = hierarchicalList.Count - 1;
            }

            public void Nest()
            {
                BlockVariables child = currentVariables.Child;
                BlockVariables parent = currentVariables;
                if (child == null)
                {
                    child = new BlockVariables();
                    parent.Child = child;
                }
                else
                {
                    while (child.Bro != null)
                    {
                        child = child.Bro;
                    }
                    child.Bro = new BlockVariables();
                    child = child.Bro;
                }
                child.Parent = parent;
                currentVariables = child;
            }

            public void UpNest()
            {
                currentVariables = currentVariables.Parent;
            }

            public Variable DefineVariable(string name, VariableType vt)
            {
                if (FindVariable(name) != null)
                {
                    return null;
                }
                int offset = linearList[varIdx].Count == 0 ? 1 : linearList[varIdx][linearList[varIdx].Count - 1].Offset + linearList[varIdx][linearList[varIdx].Count - 1].Size;
                Variable variable = new Variable(name, vt, offset);
                linearList[varIdx].Add(variable);
                currentVariables.Variables.Add(variable);
                return variable;
            }

            public Variable FindVariable(string name)
            {
                BlockVariables bv = currentVariables;
                while (bv != null)
                {
                    foreach (var v in bv.Variables)
                    {
                        if (v.Name == name)
                        {
                            return v;
                        }
                    }
                    bv = bv.Parent;
                }
                return null;
            }

            public List<List<Variable>> Variables()
            {
                return linearList;
            }

            private List<List<Variable>> linearList;        // ローカル変数のリスト(線形) ※単純な変数定義管理用
            private List<BlockVariables> hierarchicalList;  // ローカル変数のリスト(階層) ※ネスト時の変数定義対応のため
            private int varIdx;                             // ローカル変数の現在のブロック
            private BlockVariables currentVariables;        // 現在のブロック変数
        }

        /// <summary>
        /// パースのエラー
        /// </summary>
        public class ParseResult : ResultBase
        {
            public override void Initialize()
            {
                base.Initialize();
                this.Codes = null;
                this.GlobalInitCodes = null;
                this.GlobalVals = null;
            }
            public void Set(bool success, List<Node> codes, List<Node> globalInitCodes, List<Variable> glovalVals)
            {
                this.Success = success;
                this.Codes = codes;
                this.GlobalInitCodes = globalInitCodes;
                this.GlobalVals = glovalVals;
            }
            public bool isCodeSizeZero()
            {
                return Codes.Count == 0;
            }
            public List<Node> Codes { get; set; }
            public List<Node> GlobalInitCodes { get; set; }
            public List<Variable> GlobalVals { get; set; }
        }


        /// <summary>
        /// グローバル、ローカル、ENUMの定義チェック
        /// </summary>
        private enum CheckType
        {
            GVARIABLE,
            LVARIABLE,
            ENUM,
        }
        private struct DefineInfo
        {
            public string FileName;
            public CheckType Type;
        }

        /// <summary>
        /// 構造体と列挙体の定義
        /// </summary>
        /// <param name="tokenizeResult">トークナイズ結果</param>
        /// <returns>定義処理結果</returns>
        public ParseResult DefineStructEnum(TokenizeResult tokenizeResult)
        {
            Initialize_Preprocess();
            this.currentToken = tokenizeResult.HeadToken as Tokenizer.Token;
            this.isExpectErrorDraw = true;  // エラーを表示

            while (!AtEOF())
            {
                SkipPreprocess();

                if (DefineStruct())
                {
                    // 構造体の定義だった。
                    continue;
                }
                if (DefineEnum())
                {
                    // 列挙体だった
                    continue;
                }

                NextToken();
            }

            result.Set(!isParceError, null, null, null);
            return result;
        }

        /// <summary>
        /// 構造体チェック
        /// 定義されて構造体の定義チェックやサイズチェックなどを行う
        /// </summary>
        /// <returns>チェック結果</returns>
        public ParseResult StructCheck()
        {
            Initialize_Preprocess();
            foreach (var mem in members)
            {
                if (!mem.IsDefined)
                {
                    // 定義されていない
                    Error(mem.FileName, mem.LineNo, mem.Name, ca.ErrorData.Str(ERROR_TEXT.NO_DEFINE_STRUCT, mem.Name));
                }

                if (mem.IsNested(mem.Name))
                {
                    // 自分がネストされている
                    Error(mem.FileName, mem.LineNo, mem.Name, ca.ErrorData.Str(ERROR_TEXT.STRUCT_NEXT_MYSELF, mem.Name));
                }

                if (!mem.ReSize(members))
                {
                    // リサイズに失敗
                    Error(mem.FileName, mem.LineNo, mem.Name, ca.ErrorData.Str(ERROR_TEXT.ST_STRUCT_RESIZE, mem.Name));
                }

            }

            result.Set(!isParceError, null, null, null);
            return result;
        }

        /// <summary>
        /// 関数定義と、グローバル変数定義
        /// </summary>
        /// <param name="tokenizeResult">トークナイズ結果</param>
        /// <returns>処理結果</returns>
        public ParseResult DefineFuncGlobalVar(TokenizeResult tokenizeResult)
        {
            Initialize_Preprocess();
            this.currentToken = tokenizeResult.HeadToken as Tokenizer.Token;
            this.isExpectErrorDraw = true;  // エラーを表示する

            while (!AtEOF())
            {
                if (SkipPreprocess())
                {
                    continue;
                }
                if (SkipStructDefine())
                {
                    continue;
                }
                if (SkipEnumDefine())
                {
                    continue;
                }

                VariableType vt;
                int size = 1;
                Tokenizer.Token typeTk = currentToken;
                int nowErrorCount = result.ErrorCount;
                if (Type(out vt, out size, false))
                {
                    if (nowErrorCount < result.ErrorCount)
                    {
                        // 型の指定に失敗
                        string typeStr = ca.GetSourceStr(typeTk.StrIdx, typeTk.StrLen);
                        Error(ca.ErrorData.Str(ERROR_TEXT.TYPE_DEFINE_FAILD, typeStr), typeTk.StrIdx, typeTk.StrLen);

                        // 型のエラーがあった場合は
                        // 識別子または関数の始まり変数の終わりまでとばす
                        (TokenKind, string)[] kinds = { (TokenKind.IDENT, ""), (TokenKind.RESERVED, "("), (TokenKind.RESERVED, ";") };
                        SkipKinds(kinds);
                    }

                    Tokenizer.Token identTk;
                    if (Consum(TokenKind.IDENT, out identTk))
                    {
                        if (Consum("("))
                        {
                            if (!Generic.IsValidRetType(vt.Type))
                            {
                                Error(ca.ErrorData.Str(ERROR_TEXT.FUNC_NOT_SPECIFY_RET_TYPE), typeTk.StrIdx, typeTk.StrLen);
                                //result.Set(!isParceError, null, null, null);
                                //return result;
                            }

                            // 関数定義
                            string identStr = ca.GetSourceStr(identTk.StrIdx, identTk.StrLen);

                            // 引数の数
                            List<Parser.VariableType> argList = new List<VariableType>();
                            List<string> argNames = new List<string>();
                            while (!Consum(")") && !AtEOF())
                            {
                                VariableType argv;
                                int lvsize = 1;
                                Tokenizer.Token token = currentToken;
                                (TokenKind, string)[] nextArgOrFuncEndKInd = { (TokenKind.RESERVED, ","), (TokenKind.RESERVED, ")") };
                                int argErrorCount = result.ErrorCount;
                                if (!Type(out argv, out lvsize, false))
                                {
                                    Error(ca.ErrorData.Str(ERROR_TEXT.FUNC_ARG_NOT_SPECIFY_TYPE), token.StrIdx, token.StrLen);
                                    SkipKinds(nextArgOrFuncEndKInd);
                                }
                                if (argErrorCount < result.ErrorCount)
                                {
                                    // 型の指定に失敗
                                    string typeStr = ca.GetSourceStr(typeTk.StrIdx, typeTk.StrLen);
                                    Error(ca.ErrorData.Str(ERROR_TEXT.TYPE_DEFINE_FAILD, typeStr), typeTk.StrIdx, typeTk.StrLen);
                                    SkipKinds(nextArgOrFuncEndKInd);
                                }
                                Tokenizer.Token argToken = currentToken;
                                if (!Consum(TokenKind.IDENT))
                                {
                                    Error(ca.ErrorData.Str(ERROR_TEXT.FUNC_ARG_NOT_SPECIFY_VARIABLE), argToken.StrIdx, argToken.StrLen);
                                    SkipKinds(nextArgOrFuncEndKInd);
                                }
                                argList.Add(argv);
                                argNames.Add(ca.GetSourceStr(argToken.StrIdx, argToken.StrLen));
                                if (!Consum(","))
                                {
                                    // 引数はもうないので抜ける
                                    break;
                                }
                            }

                            (bool success, int idx) = ca.Linker.TemporaryRegistration(identStr, vt, argList, argNames);
                            if (!success)
                            {
                                Error(ca.ErrorData.Str(ERROR_TEXT.FUNC_ALREADY_DEFINE, identStr), identTk.StrIdx, identTk.StrLen);
                            }

                            Skip(")");

                            // ブロックの読み飛ばし
                            SkipBlock();
                            continue;
                        }

                        // グローバル変数定義
                        string ident = ca.GetSourceStr(identTk.StrIdx, identTk.StrLen);
                        Variable gv = DefineVariable(ident, vt, Scope.GLOBAL);
                        if (gv == null)
                        {
                            // すでに登録済み
                            Error(ca.ErrorData.Str(ERROR_TEXT.VARIABLE_ALREADY_DEFINE, ca.GetSourceStr(identTk.StrIdx, identTk.StrLen)), identTk.StrIdx, identTk.StrLen);
                            (TokenKind, string)[] kinds = { (TokenKind.RESERVED, ";") };
                            SkipKinds(kinds);
                        }

                        // 初期化がある
                        if (Consum("="))
                        {
                            // 代入式の終わりまで読み飛ばす
                            (TokenKind, string)[] kinds = { (TokenKind.RESERVED, ";") };
                            SkipKinds(kinds);
                            continue;
                        }
                        // ;
                        if (!Consum(";"))
                        {

                            // 代入式の終わりまで読み飛ばす
                            Error(ca.ErrorData.Str(ERROR_TEXT.MISTAKE_BY_VARIABLE_DEFINITION));
                            (TokenKind, string)[] kinds = { (TokenKind.RESERVED, ";") };
                            SkipKinds(kinds);
                            continue;
                        }
                        continue;
                    }

                    // 型　識別子ではない場合エラーを出す
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.DEFINE_NOT_IDENT));
                        (TokenKind, string)[] kinds = { (TokenKind.RESERVED, "("), (TokenKind.RESERVED, ";") };
                        SkipKinds(kinds);
                        if (Consum("("))
                        {
                            //　関数名がないものとして扱う
                            while (!Consum(")") && !AtEOF())
                            {
                                NextToken();
                            }

                            // ブロックの読み飛ばし
                            SkipBlock();
                            continue;
                        }
                        else
                        {
                            // 変数名がないものとして扱う
                            Skip(";");
                            continue;
                        }
                    }
                }
                NextToken();
            }

            result.Set(!isParceError, null, null, null);
            return result;
        }

        /// <summary>
        /// グローバル変数の初期化処理をパース
        /// 結果サイズ不定の配列のサイズが決まる
        /// </summary>
        /// <param name="tokenizeResult"></param>
        /// <returns></returns>
        public ParseResult FixGlobalArraySize(TokenizeResult tokenizeResult)
        {
            Initialize_Preprocess();
            this.currentToken = tokenizeResult.HeadToken as Tokenizer.Token;

            while (!AtEOF())
            {
                if (SkipPreprocess())
                {
                    continue;
                }
                if (SkipStructDefine())
                {
                    continue;
                }
                if (SkipEnumDefine())
                {
                    continue;
                }

                VariableType vt;
                int size = 1;
                int nowErrorCount = result.ErrorCount;
                if (Type(out vt, out size, false))
                {
                    if (nowErrorCount < result.ErrorCount)
                    {
                        // 知らない型
                        (TokenKind, string)[] nextKinds = { (TokenKind.RESERVED, ";"), (TokenKind.RESERVED, "("), (TokenKind.RESERVED, "{") };
                        SkipKinds(nextKinds);

                        //　変数だと思われる
                        if (Consum(";"))
                        {
                            continue;
                        }
                        // 関数だと思われる
                        if (Consum("("))
                        {
                            (TokenKind, string)[] kinds = { (TokenKind.RESERVED, ")") };
                            SkipKinds(kinds);
                            if (Consum("{"))
                            {
                                SkipBlock();
                            }
                            continue;
                        }
                    }

                    Tokenizer.Token identTk = currentToken;
                    if (Consum(TokenKind.IDENT))
                    {
                        if (!SkipFuncDefine())
                        {
                            // グローバル変数定義
                            string ident = ca.GetSourceStr(identTk.StrIdx, identTk.StrLen);
                            (Scope scope, Variable gv) = FindVariable(ident, Scope.GLOBAL);
                            if (gv == null)
                            {
                                //Error(ca.ErrorData.Str(ERROR_TEXT.NOT_DEFINE_VARIABLE, ident), identTk.StrIdx, identTk.StrLen);
                                // 定義は済んでいるのでgvが見つからない事はそもそもgvの定義でエラーが出ているはず
                                // 見つからない以上は文を読み飛ばす
                                (TokenKind, string)[] kinds = { (TokenKind.RESERVED, ";") };
                                SkipKinds(kinds);
                                continue;
                            }

                            if (!GLVariableInitialize(identTk, Scope.GLOBAL, null, gv, true))
                            {
                                // 失敗した場合は;まで読み飛ばす
                                (TokenKind, string)[] kinds = { (TokenKind.RESERVED, ";") };
                                SkipKinds(kinds);
                            }
                            Skip(";");
                        }
                    }
                    continue;
                }
                NextToken();
            }

            result.Set(!isParceError, null, null, null);
            return result;
        }

        /// <summary>
        /// パースの実行
        /// </summary>
        /// <param name="tokenizeResult">トークナイズ結果</param>
        /// <returns>パースの結果</returns>
        public ParseResult Do(TokenizeResult tokenizeResult)
        {
            Initialize_Do();
            this.currentToken = tokenizeResult.HeadToken as Tokenizer.Token;

            Program();

            result.Set(!isParceError, codes, globalInitCodes, globalVariables);
            return result;
        }

        /// <summary>
        /// program = define*
        /// </summary>
        /// <returns></returns>
        private Node Program()
        {
            while (!AtEOF() && !isParceError)
            {
                LVariables.NextBlock();
                Node node = Define();
                if (node == null)
                {
                    // 空だった場合は削除
                    LVariables.DeleteBlock();
                    continue;
                }
                codes.Add(node);
            }
            return null;
        }

        /// <summary>
        /// define = preprocess
        ///        | struct ident "{" (type ident ";")* "}" ";"
        ///        | enum ident "{" ident (= num)* ";" "}" ";"
        ///        | type ident ("=" numberstring)* ";"
        ///        | returntype ident "(" (type ident ("," type ident)*)? ")" "{" statement* "}"
        /// </summary>
        /// <returns></returns>
        private Node Define()
        {
            // プリプロセス関係
            Node preprocess = null;
            if (Preprocess(out preprocess))
            {
                return preprocess;
            }

            if (SkipStructDefine())
            {
                return null;
            }
            if (SkipEnumDefine())
            {
                return null;
            }

            VariableType vt;
            int size = 1;
            Tokenizer.Token typeToken = currentToken;
            int nowErrorCount = result.ErrorCount;
            if (Type(out vt, out size, false))
            {
                if (nowErrorCount < result.ErrorCount)
                {
                    // 型のエラーがあった場合は
                    // 識別子または関数の始まり変数の終わりまでとばす
                    (TokenKind, string)[] kinds = { (TokenKind.IDENT, ""), (TokenKind.RESERVED, "("), (TokenKind.RESERVED, ";") };
                    SkipKinds(kinds);
                }

                Tokenizer.Token identTk;
                if (Consum(TokenKind.IDENT, out identTk))
                {
                    if (Consum("("))
                    {
                        // すでにエラー表示は終えているのでここでは何もしない
                        //if (!Generic.IsValidRetType(vt.Type))
                        //{
                        //    Error(ca.ErrorData.Str(ERROR_TEXT.FUNC_RET_TYPE), identTk.StrIdx, identTk.StrLen);
                        //    return null;
                        //}

                        // 関数定義
                        Node func = new Node(NodeKind.FUNC_DEF);
                        string funcName = ca.GetSourceStr(identTk.StrIdx, identTk.StrLen);
                        Linker.FuncInfo funcInfo = ca.Linker.GetFuncInfo(funcName);
                        func.FuncInfo = funcInfo;

                        if (funcInfo == null)
                        {
                            Error(ca.ErrorData.Str(ERROR_TEXT.ST_NOT_DEFINE_FUNC), identTk.StrIdx, identTk.StrLen);
                        }

                        // 引数の数
                        int argCount = 0;
                        while (!Consum(")") && !AtEOF())
                        {
                            VariableType argvt;
                            int lvsize = 1;
                            Tokenizer.Token token = currentToken;
                            (TokenKind, string)[] nextArgOrFuncEndKInd = { (TokenKind.RESERVED, ","), (TokenKind.RESERVED, ")") };
                            if (!Type(out argvt, out lvsize, false))
                            {
                                //TODO 修正を行う
                                // すでにエラーは出ているので読み飛ばしだけ行う
                                SkipKinds(nextArgOrFuncEndKInd);
                                return null;
                            }
                            Tokenizer.Token argToken = currentToken;
                            if (!Consum(TokenKind.IDENT))
                            {
                                // すでにエラーは出ているので読み飛ばしだけ行う
                                SkipKinds(nextArgOrFuncEndKInd);
                                return null;
                            }

                            string identArg = ca.GetSourceStr(argToken.StrIdx, argToken.StrLen);
                            Variable lv = DefineVariable(identArg, argvt, Scope.LOCAL);
                            if (lv == null)
                            {
                                Error(ca.ErrorData.Str(ERROR_TEXT.VARIABLE_ALREADY_DEFINE, identArg), argToken.StrIdx, argToken.StrLen);
                                SkipKinds(nextArgOrFuncEndKInd);
                            }
                            argCount++;
                            if (!Consum(","))
                            {
                                // 引数はもうないので抜ける
                                break;
                            }
                        }
                        Skip(")");
                        Expect("{", currentToken.StrIdx, currentToken.StrLen);
                        func.Block = new List<Node>();
                        currentFuncRetVarType = funcInfo.ReturnType.GetEndKind();   // 現在の関数の戻り値型。statement()内のreturnで使用。
                        while (!Consum("}") && !AtEOF())
                        {
                            func.Block.Add(Statement());
                        }
                        currentFuncRetVarType = VariableKind.INVALID;
                        return func;
                    }
                    else
                    {
                        // グローバル変数定義
                        string ident = ca.GetSourceStr(identTk.StrIdx, identTk.StrLen);
                        (Scope scope, Variable gv) = FindVariable(ident, Scope.GLOBAL);   // Global変数はPreDo時に定義されているので探す
                        if (gv == null)
                        {
                            // PreDo時に定義されているはずなのでここに来ることはおかしい。
                            Error(ca.ErrorData.Str(ERROR_TEXT.ST_VARIABLE_GLOBAL), identTk.StrIdx, identTk.StrLen);
                            (TokenKind, string)[] kinds = { (TokenKind.RESERVED, ";") };
                            SkipKinds(kinds);
                        }
                        gv.ValType = vt;
                        Node node = new Node(NodeKind.GVAL_DEF);
                        SetVariableState(node, gv, identTk.StrIdx, identTk.StrLen);

                        if (!GLVariableInitialize(identTk, Scope.GLOBAL, node, gv, false))
                        {
                            // エラー出た場合は ; まで読み飛ばす
                            (TokenKind, string)[] kinds = { (TokenKind.RESERVED, ";") };
                            SkipKinds(kinds);
                        }
                        if (node.Init != null)
                        {
                            globalInitCodes.Add(node);
                        }

                        Expect(";", identTk.StrIdx, identTk.StrLen);
                        return null;
                    }
                }
                // 型　識別子ではない場合エラーを出す
                {
                    //Error(ca.ErrorData.Str(ERROR_TEXT.DEFINE_NOT_IDENT));
                    (TokenKind, string)[] kinds = { (TokenKind.RESERVED, "("), (TokenKind.RESERVED, ";") };
                    SkipKinds(kinds);
                    if (Consum("("))
                    {
                        //　関数名がないものとして扱う
                        while (!Consum(")") && !AtEOF())
                        {
                            NextToken();
                        }

                        // ブロックの読み飛ばし
                        SkipBlock();
                        return null;
                    }
                    else
                    {
                        // 変数名がないものとして扱う
                        Skip(";");
                        return null;
                    }
                }
            }

            // Preprocess()
            // StructDefine()
            // EnumDefine()
            // Type()
            // ではない状況が分りかねる
            // とりあえず ; か AtEOF() まで読み飛ばす
            {
                Error(ca.ErrorData.Str(ERROR_TEXT.STATEMENT_WRITING_IS_INCORRENCT), typeToken.StrIdx, typeToken.StrLen);
                (TokenKind, string)[] kinds = { (TokenKind.RESERVED, ";") };
                SkipKinds(kinds);
                return null;
            }
        }

        /// <summary>
        /// preprocess = "#include" string | "#init" string | "#main" string | "#magic-onetime" | "#magic-repeate" | "#skill"
        /// </summary>
        /// <returns></returns>
        private bool Preprocess(out Node node)
        {
            node = null;
            // ※読み飛ばし処理だけなので
            // ※書式エラーは書式を処理しているところで行う
            if (Consum(TokenKind.INCLUDE))
            {
                (bool success, int strIdx, int strLen) = ExpectString();
                return true;
            }
            if (Consum(TokenKind.MAIN))
            {
                (bool success, int strIdx, int strLen) = ExpectString();
                return true;
            }
            if (Consum(TokenKind.INIT))
            {
                (bool success, int strIdx, int strLen) = ExpectString();
                return true;
            }
            if (Consum(TokenKind.SKILL))
            {
                node = new Node(NodeKind.SKILL);
                return true;
            }
            if (Consum(TokenKind.ONETIME))
            {
                node = new Node(NodeKind.ONETIME);
                return true;
            }
            if (Consum(TokenKind.REPEATE))
            {
                node = new Node(NodeKind.REPEATE);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Statementの処理にローカル変数のネスト処理を追加したもの
        /// </summary>
        /// <returns></returns>
        private Node StatementNestVariable()
        {
            LVariables.Nest();
            Node node = Statement();
            LVariables.UpNest();
            return node;
        }

        /// <summary>
        /// statement = "{" statement* "}"
        ///           | "return" expression ";"
        ///           | "break" ";"
        ///           | "conitnue" ";"
        ///           | "if" "(" statement ")" statement ("else" statement)?
        ///           | "while" "(" expression ")" statrement
        ///           | "for" "(" localvariableexpression? ";" expression? ";" expression? ")" statement
        ///           | "switch" "(" expression ")" statement
        ///           | "case" num ":"
        ///           | "default" ":"
        ///           | localvariablestmtexpression ";"
        /// </summary>
        /// <returns></returns>
        private Node Statement()
        {
            Node node = null;
            (TokenKind, string)[] stmtEndKinds = { (TokenKind.RESERVED, ";") };
            Tokenizer.Token consumeTK = currentToken;

            // "{" statement "}"
            if (Consum("{"))
            {
                node = new Node(NodeKind.BLOCK);
                node.Block = new List<Node>();
                LVariables.Nest();
                while (!Consum("}") && !AtEOF())
                {
                    node.Block.Add(Statement());
                }
                LVariables.UpNest();
                return node;
            }

            // "return" expression ";"
            if (Consum(TokenKind.RETURN))
            {
                node = new Node(NodeKind.RETURN);
                if (currentFuncRetVarType == VariableKind.VOID)
                {
                    if (!Peek(";"))
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.FUNC_RETURN_VOID), consumeTK.StrIdx, consumeTK.StrLen);
                        SkipKinds(stmtEndKinds);
                        Skip(";");
                        return node;
                    }
                    //node.Lhs = NewNum(0);
                }
                else
                {
                    int nowErrorCount = result.ErrorCount;
                    node.Lhs = Expression();
                    if (nowErrorCount < result.ErrorCount)
                    {
                        // Expressionでエラーが起きている
                        // return文の最後(;)まで読み飛ばす
                        SkipKinds(stmtEndKinds);
                        Skip(";");
                        return node;
                    }
                    if (!IsAssign(Converter.Define.ToNodeKind(currentFuncRetVarType), node.Lhs))
                    {
                        // returnの型にExpression()の結果が代入できないということは
                        // return型としてふさわしくない
                        Error(ca.ErrorData.Str(ERROR_TEXT.FUNC_RETURN_DIFFERENT_TYPE, currentFuncRetVarType.ToString()), consumeTK.StrIdx, consumeTK.StrLen);
                        SkipKinds(stmtEndKinds);
                        Skip(";");
                        return node;
                    }
                }
                Expect(";", consumeTK.StrIdx, consumeTK.StrLen);
                return node;
            }

            // "break" ";"
            if (Consum(TokenKind.BREAK))
            {
                node = NewNode(NodeKind.BREAK);
                Expect(";", consumeTK.StrIdx, consumeTK.StrLen);
                return node;
            }

            // "continue" ";"
            if (Consum(TokenKind.CONTINUE))
            {
                node = NewNode(NodeKind.CONTINUE);
                Expect(";", consumeTK.StrIdx, consumeTK.StrLen);
                return node;
            }

            // "if" "(" expression ")" statement ("else" statement)?
            if (Consum(TokenKind.IF))
            {
                Expect("(", consumeTK.StrIdx, consumeTK.StrLen);
                node = new Node(NodeKind.IF);
                int nowErrorCount = result.ErrorCount;
                node.Condition = Expression();
                if (nowErrorCount < result.ErrorCount)
                {
                    // エラーが出た場合はexpressionの最後まで読み飛ばす
                    (TokenKind, string)[] kinds = { (TokenKind.RESERVED, ")") };
                    SkipKinds(kinds);
                }
                Expect(")", consumeTK.StrIdx, consumeTK.StrLen);
                node.Then = StatementNestVariable();

                //("else" statement)?
                if (Consum(TokenKind.ELSE))
                {
                    node.Else = new Node(NodeKind.ELSE);
                    node.Else.Lhs = StatementNestVariable();
                }
                return node;
            }

            // "while" "(" epression ")" statrement
            if (Consum(TokenKind.WHILE))
            {
                node = new Node(NodeKind.WHILE);
                Expect("(", consumeTK.StrIdx, consumeTK.StrLen);
                int nowErrorCount = result.ErrorCount;
                node.Condition = Expression();
                if (nowErrorCount < result.ErrorCount)
                {
                    // エラーが出ていた場合は ) まで読み飛ばす
                    (TokenKind, string)[] kinds = { (TokenKind.RESERVED, ")") };
                    SkipKinds(kinds);
                }
                Expect(")", consumeTK.StrIdx, consumeTK.StrLen);
                node.Then = StatementNestVariable();
                return node;
            }

            // "for" "(" localvariablestmtexpression? ";" expression? ";" stmtexpression? ")" statement
            if (Consum(TokenKind.FOR))
            {
                node = new Node(NodeKind.FOR);
                Expect("(", consumeTK.StrIdx, consumeTK.StrLen);
                LVariables.Nest();
                if (!Consum(";"))
                {
                    // 1つ目のexpressionがある
                    int nowErrorCount = result.ErrorCount;
                    node.Init = LocalVariableStmtExpression();
                    if (nowErrorCount > result.ErrorCount)
                    {
                        SkipKinds(stmtEndKinds);
                    }
                    Expect(";", consumeTK.StrIdx, consumeTK.StrLen);
                }
                if (!Consum(";"))
                {
                    // 2つ目のexpressionがある
                    int nowErrorCount = result.ErrorCount;
                    node.Condition = Expression();
                    if (nowErrorCount < result.ErrorCount)
                    {
                        SkipKinds(stmtEndKinds);
                    }
                    Expect(";", consumeTK.StrIdx, consumeTK.StrLen);
                }
                if (!Consum(")"))
                {
                    // 3つ目のexpressionがある
                    int nowErrorCount = result.ErrorCount;
                    node.Increment = StmtExpression();
                    if (nowErrorCount < result.ErrorCount)
                    {
                        // エラーがあった場合は ) まで読み飛ばす
                        (TokenKind, string)[] kinds = { (TokenKind.RESERVED, ")") };
                        SkipKinds(kinds);
                    }
                }
                Expect(")", consumeTK.StrIdx, consumeTK.StrLen);
                node.Then = Statement();
                LVariables.UpNest();
                return node;
            }

            // "switch" "(" expression ")" statement
            if (Consum(TokenKind.SWITCH))
            {
                node = new Node(NodeKind.SWITCH);
                Expect("(", consumeTK.StrIdx, consumeTK.StrLen);
                int nowErrorCount = result.ErrorCount;
                node.Condition = Expression();
                if (nowErrorCount < result.ErrorCount)
                {
                    // エラーがあった場合は ) まで読み飛ばす
                    (TokenKind, string)[] kinds = { (TokenKind.RESERVED, ")") };
                    SkipKinds(kinds);
                }
                Expect(")", consumeTK.StrIdx, consumeTK.StrLen);

                node.Cases = new List<Node>();

                Node sw = currentSwitch;
                currentSwitch = node;
                currentSwitch.Then = Statement();
                currentSwitch = sw;
                return node;
            }

            // "case" num ":"
            Tokenizer.Token caseTk;
            if (Consum(TokenKind.CASE, out caseTk))
            {
                if (currentSwitch == null)
                {
                    Error(ca.ErrorData.Str(ERROR_TEXT.STATEMENT_CASE), caseTk.StrIdx, caseTk.StrLen);
                    return null;
                }
                node = new Node(NodeKind.CASE);
                currentSwitch.Cases.Add(node);
                int nowErrorCount = result.ErrorCount;
                node.ValueI = ExpectIntegerEnum();
                if (nowErrorCount < result.ErrorCount)
                {
                    (TokenKind, string)[] kinds = { (TokenKind.REFERENCE, ":") };
                    SkipKinds(kinds);
                }
                Expect(":", caseTk.StrIdx, caseTk.StrLen);
                node.Then = Statement();
                return node;
            }

            // "default" ":"
            Tokenizer.Token defaultTk;
            if (Consum(TokenKind.DEFAULT, out defaultTk))
            {
                if (currentSwitch == null)
                {
                    Error(ca.ErrorData.Str(ERROR_TEXT.STATEMENT_DEFAULT), defaultTk.StrIdx, defaultTk.StrLen);
                    Skip(":");
                    return null;
                }
                node = new Node(NodeKind.CASE);
                currentSwitch.DefaultCase = node;
                Expect(":", defaultTk.StrIdx, defaultTk.StrLen);
                node.Then = Statement();
                return node;
            }

            // localvariableexpression
            int nowErroCount = result.ErrorCount;
            node = LocalVariableStmtExpression();
            if (nowErroCount < result.ErrorCount)
            {
                // エラー出ていた場合は ; まで読み飛ばす
                SkipKinds(stmtEndKinds);
                Skip(";");
                return null;
            }
            Expect(";", consumeTK.StrIdx, consumeTK.StrLen);
            return node;
        }

        /// <summary>
        /// expression = assign
        /// </summary>
        /// <returns></returns>
        private Node Expression()
        {
            return Assign();
        }

        /// <summary>
        /// assign = equality (assign-op assign)?
        /// assign-op = "=" | "+=" | "-=" | "*=" | "/="
        /// </summary>
        /// <returns></returns>
        private Node Assign()
        {
            Node node = Equality();
            if (Consum("="))
            {
                // nodeは必ず変数である
                Node rhsNode = Assign();
                ExpectAssign(node, rhsNode);
                node = NewBinary(NodeKind.ASSIGN, node, rhsNode);
            }
            else if (Consum("+="))
            {
                // nodeは必ず変数である
                Node rhsNode = Assign();
                ExpectAssign(node, rhsNode);
                node = NewBinary(NodeKind.ASSIGN, node, NewBinary(NodeKind.ADD, node, rhsNode));
            }
            else if (Consum("-="))
            {
                NodeKind nk = node.GetEndKind();
                if (nk == NodeKind.STRING)
                {
                    Error(ca.ErrorData.Str(ERROR_TEXT.STRING_SUB));
                    return null;
                }
                // nodeは必ず変数である
                Node rhsNode = Assign();
                ExpectAssign(node, rhsNode);
                node = NewBinary(NodeKind.ASSIGN, node, NewBinary(NodeKind.SUB, node, rhsNode));
            }
            else if (Consum("*="))
            {
                NodeKind nk = node.GetEndKind();
                if (nk == NodeKind.STRING)
                {
                    Error(ca.ErrorData.Str(ERROR_TEXT.STRING_MUL));
                    return null;
                }
                // nodeは必ず変数である
                Node rhsNode = Assign();
                ExpectAssign(node, rhsNode);
                node = NewBinary(NodeKind.ASSIGN, node, NewBinary(NodeKind.MUL, node, rhsNode));
            }
            else if (Consum("/="))
            {
                NodeKind nk = node.GetEndKind();
                if (nk == NodeKind.STRING)
                {
                    Error(ca.ErrorData.Str(ERROR_TEXT.STRING_DIV));
                    return null;
                }
                // nodeは必ず変数である
                Node rhsNode = Assign();
                ExpectAssign(node, rhsNode);
                node = NewBinary(NodeKind.ASSIGN, node, NewBinary(NodeKind.DIV, node, rhsNode));
            }
            return node;
        }

        /// <summary>
        /// equality = relational ("==" relational | "!=" relational)*
        /// </summary>
        /// <returns></returns>
        private Node Equality()
        {
            Node node = Relational();

            while (true)
            {
                if (Consum("=="))
                {
                    node = NewBinary(NodeKind.EQ, node, Relational());
                }
                else if (Consum("!="))
                {
                    node = NewBinary(NodeKind.NE, node, Relational());
                }
                else
                {
                    return node;
                }
            }
        }

        /// <summary>
        /// relational = add ("<" add | "<=" add | ">" add | ">=" add)*
        /// </summary>
        /// <returns></returns>
        private Node Relational()
        {
            Node node = Add();
            while (true)
            {
                if (Consum("<"))
                {
                    node = NewBinary(NodeKind.LT, node, Add());
                }
                else if (Consum("<="))
                {
                    node = NewBinary(NodeKind.LTE, node, Add());
                }
                else if (Consum(">"))
                {
                    // A > B は　B < A　と同じ意味。A,Bを逆にして < として登録すれば同じこと
                    node = NewBinary(NodeKind.LT, Add(), node);
                }
                else if (Consum(">="))
                {
                    // A >= B は　B <= A　と同じ意味。A,Bを逆にして <= として登録すれば同じこと
                    node = NewBinary(NodeKind.LTE, Add(), node);
                }
                else
                {
                    return node;
                }

                NodeKind nk = node.GetEndKind();
                if (nk == NodeKind.STRING)
                {
                    Error(ca.ErrorData.Str(ERROR_TEXT.STRING_RELATIONAL));
                    return null;
                }
            }
        }

        /// <summary>
        /// add = mul ("+" mul | "-" mul)*
        /// </summary>
        /// <returns></returns>
        private Node Add()
        {
            Node node = Mul();

            while (true)
            {
                if (Consum("+"))
                {
                    node = NewBinary(NodeKind.ADD, node, Mul());
                }
                else if (Consum("-"))
                {
                    NodeKind nk = node.GetEndKind();
                    if (nk == NodeKind.STRING)
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.STRING_SUB));
                        return null;
                    }
                    node = NewBinary(NodeKind.SUB, node, Mul());
                }
                else
                {
                    return node;
                }
            }
        }

        /// <summary>
        /// mul = unary ("*" unary | "/" unary)*
        /// </summary>
        /// <returns></returns>
        private Node Mul()
        {
            Node node = Unary();

            while (true)
            {
                if (Consum("*"))
                {
                    NodeKind nk = node.GetEndKind();
                    if (nk == NodeKind.STRING)
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.STRING_MUL));
                        return null;
                    }
                    node = NewBinary(NodeKind.MUL, node, Unary());
                }
                else if (Consum("/"))
                {
                    NodeKind nk = node.GetEndKind();
                    if (nk == NodeKind.STRING)
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.STRING_DIV));
                        return null;
                    }
                    node = NewBinary(NodeKind.DIV, node, Unary());
                }
                else
                {
                    return node;
                }
            }
        }

        /// <summary>
        /// unary = "sizeof" unary
        ///       | "+"? primary
        ///       | "-"? primary
        ///       | "&" unary
        /// </summary>
        /// <returns></returns>
        private Node Unary()
        {
            while (true)
            {
                Tokenizer.Token sizeofTk = currentToken;
                if (Consum(TokenKind.SIZEOF))
                {
                    Node node = null;
                    (TokenKind, string)[] skipKinds = { (TokenKind.RESERVED, ")") };
                    if (Consum("("))
                    {
                        Tokenizer.Token identTk = currentToken;
                        if (Consum(TokenKind.IDENT))
                        {
                            string ident = ca.GetSourceStr(identTk.StrIdx, identTk.StrLen);
                            bool isFound = false;
                            int size = 0;

                            // 構造体
                            foreach (var m in members)
                            {
                                if (m.Name == ident)
                                {
                                    size = m.Size;
                                    isFound = true;
                                    break;
                                }
                            }

                            // グローバル変数
                            if (isFound == false)
                            {
                                foreach (var v in globalVariables)
                                {
                                    if (v.Name == ident)
                                    {
                                        size = v.Size;
                                        isFound = true;
                                        break;
                                    }
                                }
                            }

                            // 見つかった
                            if (isFound)
                            {
                                node = NewNum(size);
                            }
                            // 見つからなかった
                            else
                            {
                                Error(ca.ErrorData.Str(ERROR_TEXT.SIZEOF), sizeofTk.StrIdx, sizeofTk.StrLen);
                                SkipKinds(skipKinds);
                            }

                        }
                    }
                    else
                    {
                        SkipKinds(skipKinds);
                    }
                    Expect(")", sizeofTk.StrIdx, sizeofTk.StrLen);
                    return node;
                }
                if (Consum("+"))
                {
                    return Primary();
                }
                else if (Consum("-"))
                {
                    Node node = Primary();
                    NodeKind nk = node.GetEndKind();
                    if (nk == NodeKind.STRING)
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.STRING_UNARY));
                        return null;
                    }
                    if (node.GetEndKind() == NodeKind.FLOAT)
                    {
                        return NewBinary(NodeKind.SUB, NewNum(0.0f), node);
                    }
                    else
                    {
                        return NewBinary(NodeKind.SUB, NewNum(0), node);
                    }
                }
                return Primary();
            }
        }

        /// <summary>
        /// primary = numberstring
        ///         | "(" expression ")"
        ///         | ident ("(" expression "," ")")?
        ///         | enuminteger
        ///         | localdvariable
        ///         | gwstcall
        /// </summary>
        /// <returns></returns>
        private Node Primary()
        {
            Tokenizer.Token consumeTK = currentToken;
            if (Consum("("))
            {
                int nowErrorCount = result.ErrorCount;
                Node node = Expression();
                if (nowErrorCount < result.ErrorCount)
                {
                    (TokenKind, string)[] skipKinds = { (TokenKind.RESERVED, ")") };
                    SkipKinds(skipKinds);
                }
                Expect(")", consumeTK.StrIdx, consumeTK.StrLen);
                return node;
            }

            Tokenizer.Token token = currentToken;
            if (Consum(TokenKind.IDENT))
            {
                string ident = ca.GetSourceStr(token.StrIdx, token.StrLen);

                // 関数呼び出し
                if (Consum("("))
                {
                    // ※引数の数は6まで
                    Linker.FuncInfo funcInfo = ca.Linker.GetFuncInfo(ident);

                    Node callNode = new Node(NodeKind.FUNC_CALL);
                    callNode.Block = new List<Node>(6);
                    callNode.FuncInfo = funcInfo;

                    if (funcInfo == null)
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.FUNC_NOT_DEFINE, ident));
                        return null;
                    }

                    int argCount = 0;
                    while (!Consum(")"))
                    {
                        argCount++;
                        if (argCount > funcInfo.ArgNum)
                        {
                            Error(ca.ErrorData.Str(ERROR_TEXT.FUNC_ARG_NUM_OVER, ident, funcInfo.ArgNum.ToString()));
                            return null;
                        }

                        VariableType argvt = funcInfo.ArgType[argCount - 1];
                        Node argNode = null;
                        Tokenizer.Token argTk = currentToken;

                        // 仮引数の型が配列&構造体とそれ以外の場合は処理が異なる
                        // 配列&構造体の場合は変数のアドレスを渡す
                        if (argvt.Type == VariableKind.ARRAY || argvt.Type == VariableKind.STRUCT)
                        {
                            // 変数名のはず
                            //Tokenizer.Token argIdentTk = currentToken;
                            if (Consum(TokenKind.IDENT))
                            {
                                // 変数のアドレスをプッシュする.
                                string argIdent = ca.GetSourceStr(argTk.StrIdx, argTk.StrLen);
                                (Scope argS, Variable argV) = FindVariable(argIdent);
                                if (argV == null)
                                {
                                    Error(ca.ErrorData.Str(ERROR_TEXT.NOT_DEFINE_VARIABLE, argIdent), argTk.StrIdx, argTk.StrLen);
                                    return null;
                                }
                                // 型チェック
                                if (!argvt.TypeCheck(argV.ValType))
                                {
                                    Error(ca.ErrorData.Str(ERROR_TEXT.FUNC_ARG_NOT_TYPE, argCount.ToString(), argvt.ToTypeString()), argTk.StrIdx, argTk.StrLen);
                                    return null;
                                }

                                argNode = NewNode(argS == Scope.GLOBAL ? NodeKind.GVAL_DEREFE : NodeKind.LVAL_DEREFE);
                                argNode.Offset = argV.Offset;
                            }
                            else
                            {
                                Error(ca.ErrorData.Str(ERROR_TEXT.FUNC_ARG_SPECIFY_VARIABLE, argvt.ToTypeString()), argTk.StrIdx, argTk.StrLen);
                                return null;
                            }
                        }
                        else if (argvt.Type == VariableKind.REFERENCE)
                        {
                            //　&変数名のはず
                            argNode = AddressGLVariable();
                            if (argNode == null)
                            {
                                Error(ca.ErrorData.Str(ERROR_TEXT.FUNC_ARG_REF_SPECIFY_ADDR), argTk.StrIdx, argTk.StrLen);
                                return null;
                            }
                            //nodeとnode.initの型が同じか調べる
                            if (!argvt.PointerTo.Equal(argNode.ValType))
                            {
                                argvt.ToTypeString();
                                Error(ca.ErrorData.Str(ERROR_TEXT.FUNC_ARG_REF_DIFFERENT_TYPE, argvt.ToTypeString()), argTk.StrIdx, argTk.StrLen);
                                return null;
                            }
                        }
                        else
                        {
                            int nowErrorCount = result.ErrorCount;
                            argNode = Expression();
                            if (nowErrorCount < result.ErrorCount)
                            {
                                (TokenKind, string)[] skipKinds = { (TokenKind.RESERVED, ")"), (TokenKind.RESERVED, ",") };
                                SkipKinds(skipKinds);
                            }
                            else
                            {
                                // 型チェック
                                if (!IsAssign(Converter.Define.ToNodeKind(argvt.GetEndKind()), argNode))
                                {
                                    // 代入できない == 型の変換ができない
                                    Error(ca.ErrorData.Str(ERROR_TEXT.FUNC_ARG_NOT_TYPE, argCount.ToString(), argvt.GetEndKind().ToString()));
                                    return null;
                                }
                            }
                        }

                        callNode.Block.Add(argNode);
                        if (Consum(")"))
                        {
                            break;
                        }
                        Expect(",", argTk.StrIdx, argTk.StrLen);
                    }
                    if (argCount != funcInfo.ArgNum)
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.FUNC_ARG_NUM_DIFFERENT, ident, funcInfo.ArgNum.ToString(), argCount.ToString()), token.StrIdx, token.StrLen);
                        return null;
                    }
                    return callNode;
                }

                // Enum
                Node enumNode = EnumInteger(ident);
                if (enumNode != null)
                {
                    return enumNode;
                }

                // 変数
                Node variNode = GLVariable(token);

                if (Consum("++"))
                {
                    // インデント
                    variNode = NewBinary(NodeKind.ASSIGN, variNode, NewBinary(NodeKind.ADD, variNode, NewNum(1)));
                }
                else if (Consum("--"))
                {
                    // デクリメント
                    variNode = NewBinary(NodeKind.ASSIGN, variNode, NewBinary(NodeKind.SUB, variNode, NewNum(1)));
                }
                return variNode;
            }
            else if (Consum(TokenKind.GWST_LIB))
            {
                return GwstCall(GWSTType.gwst_lib);
            }
            else if (Consum(TokenKind.GWST_MAG))
            {
                return GwstCall(GWSTType.gwst_mag);
            }
            else if (Consum(TokenKind.GWST_SMAG))
            {
                return GwstCall(GWSTType.gwst_smag);
            }
            else if (Consum(TokenKind.GWST_UI))
            {
                return GwstCall(GWSTType.gwst_ui);
            }
            else if (Consum(TokenKind.GWST_MEPH))
            {
                return GwstCall(GWSTType.gwst_meph);
            }
            else if (Consum(TokenKind.GWST_WAMAG))
            {
                return GwstCall(GWSTType.gwst_wamag);
            }
            else if (Consum(TokenKind.SYSTEM))
            {
                return SystemCall(token.SysKind);
            }
            else if (Consum(TokenKind.DEBUG_LOG))
            {
                return DebugLog();
            }
            else if (Consum(TokenKind.DEBUG_PAUSE))
            {
                return DebugPause();
            }
            else if (Consum(TokenKind.RELEASE))
            {
                return BoxsRelease();
            }

            {
                Node node = NumberString();
                if (node == null)
                {
                    // 整数、不動小数点数、文字列を期待していた
                    Error(ca.ErrorData.Str(ERROR_TEXT.NUM_OR_STRING));
                    return null;
                }
                return node;
            }
        }

        /// <summary>
        /// localvariable = type ("[" num "]")* ("*")* ident ("=" expression)* 
        /// </summary>
        /// <param name="isError"></param>
        /// <returns></returns>
        private Node LocalVariable()
        {
            // 変数定義(初期化も含む)
            // 配列定義(初期化も含む)
            // type ident ("=" expression)* ";"
            // type "[" (num)* "]" ("[" num "]")* ("*")* ident ("=" "{" expression* ("," expression)* "}")* ";"
            Node node = null;
            VariableType vt;
            int size;
            Tokenizer.Token typeTk = currentToken;
            int nowErrorCount = result.ErrorCount;
            if (Type(out vt, out size, true))
            {
                if (nowErrorCount < result.ErrorCount)
                {
                    // 式の終わりまで読み飛ばす
                    string type = ca.GetSourceStr(typeTk.StrIdx, typeTk.StrLen);
                    Error(ca.ErrorData.Str(ERROR_TEXT.TYPE_DEFINE_FAILD, type), typeTk.StrIdx, typeTk.StrLen);
                    return null;
                }

                node = new Node(NodeKind.LVAL_DEF);
                Tokenizer.Token identTK = currentToken;
                if (Consum(TokenKind.IDENT))
                {
                    string ident = ca.GetSourceStr(identTK.StrIdx, identTK.StrLen);
                    Variable lv = DefineVariable(ident, vt, Scope.LOCAL);
                    if (lv == null)
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.VARIABLE_ALREADY_DEFINE, ident), identTK.StrIdx, identTK.StrLen);
                        return null;
                    }
                    SetVariableState(node, lv, identTK.StrIdx, identTK.StrLen);

                    if (!GLVariableInitialize(identTK, Scope.LOCAL, node, lv, true))
                    {
                        ErrorBase();
                        return null;
                    }

                    return node;
                }

                Error(ca.ErrorData.Str(ERROR_TEXT.VARIABLE_NOT_DEFINE), identTK.StrIdx, identTK.StrLen);
                return null;
            }

            return null;
        }

        /// <summary>
        /// localvariableexpression = localvariable | stmtexpression
        /// </summary>
        /// <param name="isError"></param>
        /// <returns></returns>
        private Node LocalVariableStmtExpression()
        {
            // 変数定義(初期化も含む)
            // 配列定義(初期化も含む)
            // type ident ("=" expression)* ";"
            // type "[" (num)* "]" ("[" num "]")* ("*")* ident ("=" "{" expression* ("," expression)* "}")* ";"
            int nowErrorCount = result.ErrorCount;
            Node node = LocalVariable();
            Tokenizer.Token identTk = currentToken;
            if (node != null && nowErrorCount == result.ErrorCount)
            {
                return node;
            }
            if (nowErrorCount < result.ErrorCount)
            {
                Error(ca.ErrorData.Str(ERROR_TEXT.STATEMENT_NOT_STMT));
                return null;
            }

            // expression
            // 代入か関数呼び出しではないと意味がない
            return StmtExpression();
        }

        /// <summary>
        /// stmtexpression = expression
        /// </summary>
        /// <param name="isError"></param>
        /// <returns></returns>
        private Node StmtExpression()
        {
            // 代入か関数呼び出しではないと意味がない
            Node node = new Node(NodeKind.STMT_EXPR);
            node.Then = Expression();
            if (!IsValidStatementExpression(node.Then))
            {
                Error(ca.ErrorData.Str(ERROR_TEXT.STATEMENT_NOT_STMT));
                return null;
            }
            return node;
        }

        /// <summary>
        /// numberstring = num | str
        /// </summary>
        /// <returns></returns>
        private Node NumberString()
        {
            if (currentToken.Kind == TokenKind.INTEGER)
            {
                // num
                (bool success, int value) = ExpectInteger();
                if (success) return NewNum(value);
            }
            else if (currentToken.Kind == TokenKind.STRING)
            {
                (bool success, int strIdx, int strLen) = ExpectString();
                if (success) return NewString(strIdx, strLen);
            }
            else if (currentToken.Kind == TokenKind.FLOAT)
            {
                (bool success, float value) = ExpectFloat();
                return NewNum(value);
            }
            return null;
        }

        /// <summary>
        /// enuminteger = ident "." ident
        /// </summary>
        /// <param name="ident">識別子</param>
        /// <returns>enumのノード</returns>
        private Node EnumInteger(string ident)
        {
            Enum e = GetEnum(ident);
            if (e != null)
            {
                if (Consum("."))
                {
                    Tokenizer.Token elementTk = currentToken;
                    if (Consum(TokenKind.IDENT))
                    {
                        string element = ca.GetSourceStr(elementTk.StrIdx, elementTk.StrLen);
                        (bool define, int value) = e.Value(element);
                        if (define)
                        {
                            return NewNum(value);
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// gwstcall = "gwst_call" "(" num | enuminteger  "," ... ")"
        /// </summary>
        /// <returns></returns>
        private Node GwstCall(GWSTType gwstType)
        {
            (TokenKind, string)[] gwstEndKinds = { (TokenKind.RESERVED, ")") };
            (TokenKind, string)[] gwstArgEndKinds = { (TokenKind.RESERVED, ","), (TokenKind.RESERVED, ")") };
            Tokenizer.Token token = currentToken;
            int nowErrorCount;
            if (Consum("("))
            {
                nowErrorCount = result.ErrorCount;
                int gwstcallNo = ExpectIntegerEnum();
                if (nowErrorCount < result.ErrorCount)
                {
                    SkipKinds(gwstEndKinds);
                    Expect(")", token.StrIdx, token.StrLen);
                    return null;
                }

                Node node = NewNode(NodeKind.GWST_CALL);
                node.GwstType = gwstType;
                node.GwstNo = gwstcallNo;
                node.Block = new List<Node>();

                (int argNum, VariableKind retType, NodeKind[] argKinds) = Generic.GWSTInfo(gwstType, gwstcallNo);
                if (retType == VariableKind.INVALID && argKinds == null)
                {
                    Error(ca.ErrorData.Str(ERROR_TEXT.GWST_CALL_TYPE, gwstType.ToString(), $"{Generic.GWSTInfoLength(gwstType) - 1}"));
                    SkipKinds(gwstEndKinds);
                    Expect(")", token.StrIdx, token.StrLen);
                    return null;
                }
                // 引数がある
                if (argKinds[0] != NodeKind.Other)
                {
                    for (int i = 0; i < argNum; i++)
                    {
                        Expect(",", currentToken.StrIdx, currentToken.StrLen);
                        Node argNode = null;
                        if (argKinds[i] != NodeKind.ADDR)
                        {
                            nowErrorCount = result.ErrorCount;
                            argNode = Expression();
                            if (nowErrorCount < result.ErrorCount)
                            {
                                // エラーが起きた場合
                                SkipKinds(gwstArgEndKinds);
                            }
                            else
                            {
                                // 引数がアドレスでは無ければそのまま型チェック
                                if (!IsAssign(argKinds[i], argNode))
                                {
                                    // 代入できない == 型の変換ができない
                                    Error(ca.ErrorData.Str(ERROR_TEXT.FUNC_ARG_NOT_TYPE, i.ToString(), argKinds[i].ToString()));
                                    SkipKinds(gwstArgEndKinds);
                                }
                            }
                        }
                        else
                        {
                            // 引数がアドレス
                            // argNodeが指定の基本型をもているかチェック
                            VariableKind[] kinds = Generic.GwstCallRefArgInfo(node.GwstType, node.GwstNo);
                            Tokenizer.Token argTK = currentToken;
                            argNode = AddressGLVariable();
                            if (argNode == null)
                            {
                                Error(ca.ErrorData.Str(ERROR_TEXT.GWST_ALRG_REF_SPECIFY_ADDR, i.ToString(), Generic.GwstRefArgToString(kinds)), argTK.StrIdx, argTK.StrLen);
                                SkipKinds(gwstArgEndKinds);
                            }
                            if (kinds != null)
                            {
                                if (!RefArgCheck(kinds, argNode))
                                {
                                    // 型が違います
                                    Error(ca.ErrorData.Str(ERROR_TEXT.GWST_ALRG_REF_SPECIFY_ADDR, i.ToString(), Generic.GwstRefArgToString(kinds)), argTK.StrIdx, argTK.StrLen);
                                    SkipKinds(gwstArgEndKinds);
                                }
                            }
                        }

                        node.Block.Add(argNode);
                    }
                }
                Expect(")", token.StrIdx, token.StrLen);
                return node;
            }
            else
            {
                Error(ca.ErrorData.Str(ERROR_TEXT.GWST_NOT_STMT, gwstType.ToString()));
                return null;
            }
        }

        /// <summary>
        /// systemcall = "systemcall" "(" ... ")"
        /// </summary>
        /// <returns></returns>
        public Node SystemCall(SystemKind sysKind)
        {
            (TokenKind, string)[] sysEndKinds = { (TokenKind.RESERVED, ")") };
            (TokenKind, string)[] sysArgEndKinds = { (TokenKind.RESERVED, ","), (TokenKind.RESERVED, ")") };
            Tokenizer.Token token = currentToken;
            if (Consum("("))
            {
                (int argNum, VariableKind retType, NodeKind[] argKinds) = Generic.SYSInfo(sysKind);

                Node node = NewNode(NodeKind.SYS_CALL);
                node.SysKind = sysKind;
                node.Block = new List<Node>();

                if (retType == VariableKind.INVALID && argKinds == null)
                {
                    Error(ca.ErrorData.Str(ERROR_TEXT.ST_SYS_CALL_TYPE));
                }
                if (argKinds[0] != NodeKind.Other)
                {
                    for (int i = 0; i < argNum; i++)
                    {
                        Tokenizer.Token argTk = currentToken;
                        Node argNode = null;
                        if (argKinds[i] != NodeKind.ADDR)
                        {
                            int nowErrorCount = result.ErrorCount;
                            argNode = Expression();
                            if (nowErrorCount < result.ErrorCount)
                            {
                                SkipKinds(sysArgEndKinds);
                            }
                            // 引数がアドレスでは無ければそのまま型チェック
                            if (!IsAssign(argKinds[i], argNode))
                            {
                                // 代入できない == 型の変換ができない
                                Error(ca.ErrorData.Str(ERROR_TEXT.FUNC_ARG_NOT_TYPE, i.ToString(), argKinds[i].ToString()), argTk.StrIdx, argTk.StrLen);
                                SkipKinds(sysArgEndKinds);
                            }
                        }
                        else
                        {
                            // 引数がアドレス
                            // argNodeが指定の基本型をもているかチェック
                            VariableKind[] kinds = Generic.SysCallRefArgInfo(sysKind);
                            argNode = AddressGLVariable();
                            if (argNode == null)
                            {
                                Error(ca.ErrorData.Str(ERROR_TEXT.GWST_ALRG_REF_SPECIFY_ADDR, i.ToString(), Generic.GwstRefArgToString(kinds)), argTk.StrIdx, argTk.StrLen);
                                SkipKinds(sysEndKinds);
                            }
                            if (kinds != null)
                            {
                                if (!RefArgCheck(kinds, argNode))
                                {
                                    Error(ca.ErrorData.Str(ERROR_TEXT.GWST_ALRG_REF_SPECIFY_ADDR, i.ToString(), Generic.GwstRefArgToString(kinds)), argTk.StrIdx, argTk.StrLen);
                                    SkipKinds(sysEndKinds);
                                }
                            }
                        }

                        // , の数は引数-1個文だけある
                        if (i < argNum - 1)
                        {
                            Expect(",", currentToken.StrIdx, currentToken.StrLen);
                        }

                        node.Block.Add(argNode);
                    }
                }
                Expect(")", token.StrIdx, token.StrLen);
                return node;
            }
            else
            {
                Error(ca.ErrorData.Str(ERROR_TEXT.SYS_NOT_STMT));
                return null;
            }
        }

        private Node DebugLog()
        {
            /// debug_log("あいうえお", a, b ...);
            /// %d は 整数
            /// %f は 不動小数点数
            /// %s は 文字列と置き換え
            /// 最大引数数は6
            if (Consum("("))
            {
                (TokenKind, string)[] endKind = { (TokenKind.RESERVED, ")") };
                Node node = NewNode(NodeKind.DEBUG_LOG);
                Tokenizer.Token strTk;
                Consum(TokenKind.STRING, out strTk);
                if (strTk != null)
                {
                    node.StrIdx = strTk.StrIdx;
                    node.StrLen = strTk.StrLen;
                    node.Block = new List<Node>();
                    node.DebugArg = new List<NodeKind>();
                    string str = ca.GetSourceStr(strTk.StrIdx, strTk.StrLen);
                    int idx = 0;
                    while (idx < str.Length)
                    {
                        idx = str.IndexOf('%', idx);

                        if (idx == -1)
                        {
                            // 見つからなかった
                            break;
                        }

                        if (idx + 1 >= str.Length)
                        {
                            // %〇はもうない(%が最後の文字のため)
                            break;
                        }

                        if (str[idx + 1] == 'd')
                        {
                            // %d
                            node.DebugArg.Add(NodeKind.INTEGER);
                        }
                        else if (str[idx + 1] == 'f')
                        {
                            // %f
                            node.DebugArg.Add(NodeKind.FLOAT);
                        }
                        else if (str[idx + 1] == 's')
                        {
                            // %s
                            node.DebugArg.Add(NodeKind.STRING);
                        }
                        idx += 2;
                    }

                    if (node.DebugArg.Count > 6)
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.DEBUGLOG_ARG_OVER_LIMT));
                        return null;
                    }

                    // 置換文字無し
                    if (node.DebugArg.Count == 0)
                    {
                        Expect(")", strTk.StrIdx, strTk.StrLen);
                        return node;
                    }

                    Node argNode = null;
                    idx = 0;
                    while (!Consum(")"))
                    {
                        Tokenizer.Token argTk = currentToken;
                        int nowErrorCount = result.ErrorCount;
                        Expect(",", argTk.StrIdx, argTk.StrLen);
                        argNode = Expression();

                        if (idx >= node.DebugArg.Count)
                        {
                            Error(ca.ErrorData.Str(ERROR_TEXT.DEBUGLOG_ARG_DIFFERENCE, node.DebugArgToString()), argTk.StrIdx, argTk.StrLen);
                            SkipKinds(endKind);
                            Skip(")");
                            break;
                        }

                        (TokenKind, string)[] argEndkinds = { (TokenKind.RESERVED, ",") };
                        if (nowErrorCount < result.ErrorCount)
                        {
                            // expressionでエラーが起きた
                            SkipKinds(argEndkinds);
                        }
                        // 型チェック
                        else if (!IsAssign(node.DebugArg[idx], argNode))
                        {
                            // 代入できない == 型の変換ができない
                            Error(ca.ErrorData.Str(ERROR_TEXT.DEBUGLOG_ARG_DIFFERENCE, node.DebugArgToString()), argTk.StrIdx, argTk.StrLen);
                            SkipKinds(argEndkinds);
                        }

                        node.Block.Add(argNode);
                        idx++;
                    }
                    return node;
                }
                else
                {
                    Error(ca.ErrorData.Str(ERROR_TEXT.DEBUGLOG_NOT_STMT), strTk.StrIdx, strTk.StrLen);
                    SkipKinds(endKind);
                    Skip(")");
                    return null;
                }
            }
            Error(ca.ErrorData.Str(ERROR_TEXT.DEBUGLOG_NOT_STMT));
            return null;
        }

        private Node DebugPause()
        {
            if (Consum("("))
            {
                Node node = new Node(NodeKind.DEBUG_PAUSE);
                Expect(")", currentToken.StrIdx, currentToken.StrLen);
                return node;
            }
            Error(ca.ErrorData.Str(ERROR_TEXT.DEBUGLOG_NOT_STMT));
            return null;
        }

        private Node BoxsRelease()
        {
            if (Consum("("))
            {
                Tokenizer.Token identTk = currentToken;
                if (Consum(TokenKind.IDENT))
                {
                    (TokenKind, string)[] endKind = { (TokenKind.RESERVED, ")") };
                    int nowErroCount = result.ErrorCount;
                    Node node = GLVariableHeapRelease(identTk);
                    if (nowErroCount < result.ErrorCount)
                    {
                        // GLVAriableでエラーが出た場合は終了
                        SkipKinds(endKind);
                        Skip(")");
                        return null;
                    }
                    if (node.Kind != NodeKind.HVAL)
                    {
                        // ヒープ変数ではない場合は終了
                        Error(ca.ErrorData.Str(ERROR_TEXT.BOXS_RELEASE), identTk.StrIdx, identTk.StrLen);
                        SkipKinds(endKind);
                        Skip(")");
                        return null;
                    }

                    Expect(")", identTk.StrLen, identTk.StrLen);

                    // 変数情報をノードにする
                    Node retnode = NewBinary(NodeKind.RELEASE, null, null);
                    retnode.Then = node;
                    return retnode;
                }
            }
            Error(ca.ErrorData.Str(ERROR_TEXT.DEBUGLOG_NOT_STMT));
            return null;
        }

        /// <summary>
        /// addressglvariable = "&" glvariable
        /// </summary>
        /// <returns></returns>
        private Node AddressGLVariable()
        {
            if (Consum("&"))
            {
                Tokenizer.Token identTk = currentToken;
                if (Consum(TokenKind.IDENT))
                {
                    string ident = ca.GetSourceStr(identTk.StrIdx, identTk.StrLen);
                    (Scope scope, Variable variable) = FindVariable(ident);
                    if (variable == null)
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.NOT_DEFINE_VARIABLE, ident), identTk.StrIdx, identTk.StrLen);
                        return null;
                    }

                    Node variNode = NewNode(scope == Scope.GLOBAL ? NodeKind.GVAL_DEREFE : NodeKind.LVAL_DEREFE);
                    VariableType readVt = variable.ValType;
                    SetVariableState(variNode, variable, identTk.StrIdx, identTk.StrLen);

                    bool isReference = readVt.Type == VariableKind.REFERENCE;

                    if (isReference)
                    {
                        readVt = readVt.PointerTo;
                    }

                    Node offsetNode = NewNum(0);
                    bool isDerefe = false;
                    while (true)
                    {
                        // 配列
                        Tokenizer.Token kakkoTK = currentToken;
                        if (Consum("["))
                        {
                            if (readVt.Type != VariableKind.ARRAY)
                            {
                                Error(ca.ErrorData.Str(ERROR_TEXT.ARRAY_DS_OVER), kakkoTK.StrIdx, kakkoTK.StrLen);
                            }

                            // *(ident + expression) に変換する
                            int arraysize = readVt.PointerTo.GetSize();
                            Tokenizer.Token numTk = currentToken;
                            int nowErrorCount = result.ErrorCount;
                            Node numNode = Expression();
                            if (nowErrorCount < result.ErrorCount)
                            {
                                (TokenKind, string)[] skipKinds = { (TokenKind.RESERVED, "]") };
                                SkipKinds(skipKinds);
                            }
                            else
                            {
                                bool isPossible = IsAssign(NodeKind.INTEGER, offsetNode);
                                if (!isPossible)
                                {
                                    int strLen = currentToken.StrIdx - identTk.StrIdx + 1;
                                    Error(ca.ErrorData.Str(ERROR_TEXT.ARRAY_INDEX), identTk.StrIdx, strLen);
                                }
                            }
                            offsetNode = NewBinary(NodeKind.ADD, offsetNode, NewBinary(NodeKind.MUL, numNode, NewNum(arraysize)));
                            Expect("]", kakkoTK.StrIdx, kakkoTK.StrLen);
                            readVt = readVt.PointerTo;
                            isDerefe = true;
                            continue;
                        }
                        // 構造体
                        if (Consum("."))
                        {
                            if (readVt.Type != VariableKind.STRUCT)
                            {
                                Error(ca.ErrorData.Str(ERROR_TEXT.STRUCT_NOT_STRUCT));
                            }
                            Tokenizer.Token memberTk = currentToken;
                            if (Consum(TokenKind.IDENT))
                            {
                                Member member = readVt.Member;
                                string varName = ca.GetSourceStr(memberTk.StrIdx, memberTk.StrLen);
                                Variable variableMember = null;
                                foreach (var v in member.Variables)
                                {
                                    if (v.Name == varName)
                                    {
                                        // 見つかった
                                        variableMember = v;
                                        break;
                                    }
                                }
                                if (variableMember == null)
                                {
                                    Error(ca.ErrorData.Str(ERROR_TEXT.STRUCT_NOT_MEMBER, member.Name, varName));
                                }
                                offsetNode = NewBinary(NodeKind.ADD, offsetNode, NewNum(variableMember.Offset));
                                readVt = variableMember.ValType;
                                variNode.ValType = variableMember.ValType;  // Valノードの変数情報を更新
                                if (variNode.Lhs != null)
                                {
                                    variNode.Lhs.ValType = variableMember.ValType;
                                }
                                isDerefe = true;
                                continue;
                            }
                        }
                        break;
                    }

                    if (isReference || isDerefe)
                    {
                        if (isReference && !isDerefe)
                        {
                            // ref変数の中身のアドレス
                            variNode = NewBinary(NodeKind.REF_ADDR, variNode, null);    // refの中身のアドレス
                        }
                        else if (!isReference && isDerefe)
                        {
                            // 変数のアドレスにoffsetを足したアドレス
                            variNode = NewBinary(NodeKind.ADD, variNode, offsetNode);   // offsetの計算
                            variNode = NewBinary(NodeKind.ADDR, variNode, null);        // アドレス取得
                        }
                        else
                        {
                            // ref変数の中身のアドレスにoffsetを足したアドレス
                            variNode = NewBinary(NodeKind.REF_ADDR, variNode, null);    // refの中身のアドレス
                            variNode = NewBinary(NodeKind.ADD, variNode, offsetNode);   // offsetの計算
                            variNode = NewBinary(NodeKind.ADDR, variNode, null);        // アドレス取得
                        }
                        variNode.ValType = readVt;
                    }

                    return variNode;
                }
            }
            return null;
        }

        private Node GLVariable(Tokenizer.Token identTk)
        {
            return GLVariable(identTk, false);
        }

        private Node GLVariableHeapRelease(Tokenizer.Token identTk)
        {
            return GLVariable(identTk, true);
        }

        /// <summary>
        /// localdvariable = ident ("[" expression "]")* ("++" | "--")?
        ///                | ident "." ident ("." ident)*
        /// </summary>
        /// <param name="identTk"></param>
        /// <returns></returns>
        private Node GLVariable(Tokenizer.Token identTk, bool isHeapRelease)
        {
            // 変数名
            string ident = ca.GetSourceStr(identTk.StrIdx, identTk.StrLen);

            // 変数名からローカル変数・グローバル変数の順で調べる
            (Scope scope, Variable variable) = FindVariable(ident);
            if (variable == null)
            {
                Error(ca.ErrorData.Str(ERROR_TEXT.NOT_DEFINE_VARIABLE, ident), identTk.StrIdx, identTk.StrLen);
                return null;
            }

            // 変数情報をノードにする
            Node variNode = new Node(scope == Scope.GLOBAL ? NodeKind.GVAL : NodeKind.LVAL);
            VariableType vt = variable.ValType;
            SetVariableState(variNode, variable, identTk.StrIdx, identTk.StrLen);

            // ref変数か？
            bool isReference = vt.Type == VariableKind.REFERENCE;
            if (isReference)
            {
                vt = vt.PointerTo;  // ref変数の場合は次の情報から見る
            }

            // 
            Node offsetNode = NewNum(0);    // 配列、構造体の場合は先頭からのoffsetを動的に求めるのでそのためのNode
            Node boxsOffsetNode = NewNum(0);// boxsの場合は先頭からのoffsetを動的に求めるのでそのためのNode
            bool isDerefe = false;          // デリファレンスが必要か？
            bool isHeap = false;            // boxs変数か？
            while (true)
            {
                // 配列とBoxs
                Tokenizer.Token kakkoTK = currentToken;
                if (Consum("["))
                {
                    if (vt.Type == VariableKind.BOXS)
                    {
                        // *(ident + expression) に変換する
                        int nowErrorCount = result.ErrorCount;
                        Node numNode = Expression();
                        if (nowErrorCount < result.ErrorCount)
                        {
                            (TokenKind, string)[] skipKinds = { (TokenKind.RESERVED, "]") };
                            SkipKinds(skipKinds);
                        }
                        else
                        {
                            if (!IsAssign(NodeKind.INTEGER, numNode))
                            {
                                int strLen = currentToken.StrIdx - identTk.StrIdx + 1;
                                Error(ca.ErrorData.Str(ERROR_TEXT.ARRAY_INDEX), identTk.StrIdx, strLen);
                            }
                        }
                        boxsOffsetNode = NewBinary(NodeKind.ADD, boxsOffsetNode, numNode);
                        Expect("]", kakkoTK.StrIdx, kakkoTK.StrLen);
                        vt = vt.PointerTo;
                        isHeap = true;
                    }
                    else if (vt.Type == VariableKind.ARRAY)
                    {
                        // *(ident + expression) に変換する
                        int arraysize = vt.PointerTo.GetSize();
                        int nowErrorCount = result.ErrorCount;
                        Node numNode = Expression();
                        if (nowErrorCount < result.ErrorCount)
                        {
                            (TokenKind, string)[] skipKinds = { (TokenKind.RESERVED, "]") };
                            SkipKinds(skipKinds);
                        }
                        else
                        {
                            // 添え字は整数の必要がある。INTEGERに代入できれば整数
                            bool isPossible = IsAssign(NodeKind.INTEGER, numNode);
                            if (!isPossible)
                            {
                                int strLen = currentToken.StrIdx - identTk.StrIdx + 1;
                                Error(ca.ErrorData.Str(ERROR_TEXT.ARRAY_INDEX), identTk.StrIdx, strLen);
                            }
                        }
                        offsetNode = NewBinary(NodeKind.ADD, offsetNode, NewBinary(NodeKind.MUL, numNode, NewNum(arraysize)));
                        Expect("]", kakkoTK.StrIdx, kakkoTK.StrLen);
                        vt = vt.PointerTo;
                        isDerefe = true;
                        continue;   // 配列の指定が続く場合があるので繰り返す
                    }
                    else
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.ARRAY_DS_OVER));
                    }
                }
                // 構造体
                if (Consum("."))
                {
                    if (vt.Type != VariableKind.STRUCT)
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.STRUCT_NOT_STRUCT));
                    }
                    Tokenizer.Token memberTk = currentToken;
                    if (Consum(TokenKind.IDENT))
                    {
                        Member member = vt.Member;
                        string varName = ca.GetSourceStr(memberTk.StrIdx, memberTk.StrLen);
                        Variable varMem = null;
                        foreach (var v in member.Variables)
                        {
                            if (v.Name == varName)
                            {
                                // 見つかった
                                varMem = v;
                                break;
                            }
                        }
                        if (varMem == null)
                        {
                            Error(ca.ErrorData.Str(ERROR_TEXT.STRUCT_NOT_MEMBER, member.Name, varName));
                        }
                        offsetNode = NewBinary(NodeKind.ADD, offsetNode, NewNum(varMem.Offset));
                        vt = varMem.ValType;
                        variNode.ValType = varMem.ValType;  // Valノードの変数情報を更新
                        if (variNode.Lhs != null)
                        {
                            variNode.Lhs.ValType = varMem.ValType;
                        }
                        isDerefe = true;
                        continue;
                    }
                }
                break;
            }

            if (vt.Type == VariableKind.ARRAY)
            {
                Error(ca.ErrorData.Str(ERROR_TEXT.ARRAY_DS_NOT_ENOUGH));
            }
            if (vt.Type == VariableKind.STRUCT)
            {
                Error(ca.ErrorData.Str(ERROR_TEXT.STRUCT_SPECIFY_NOT_ENOUGH));
            }
            if (vt.Type == VariableKind.BOXS)
            {
                if(!isHeapRelease)
                {
                    Error(ca.ErrorData.Str(ERROR_TEXT.ARRAY_DS_NOT_ENOUGH));
                }
                else
                {
                    // ヒープ変数の指定
                    // ヒープ解放なのでヒープ変数名だけ指定して問題はない
                    isHeap = true;
                }
            }

            if (!isHeap)
            {
                if (isReference || isDerefe)
                {
                    if (isReference && !isDerefe)
                    {
                        // ref変数の中身のアドレスの中身
                        variNode.Kind = scope == Scope.GLOBAL ? NodeKind.GVAL_REFERENCE : NodeKind.LVAL_REFERENCE;
                        variNode = NewBinary(NodeKind.DEREFE, variNode, null);
                    }
                    else if (!isReference && isDerefe)
                    {
                        // 変数のアドレスにoffsetを足したアドレスの中身
                        variNode.Kind = scope == Scope.GLOBAL ? NodeKind.GVAL_DEREFE : NodeKind.LVAL_DEREFE;
                        variNode = NewBinary(NodeKind.DEREFE, NewBinary(NodeKind.ADD, variNode, offsetNode), null);
                    }
                    else
                    {
                        // ref変数の中身のアドレスにoffsetを足したアドレスの中身
                        variNode.Kind = scope == Scope.GLOBAL ? NodeKind.GVAL_REFERENCE : NodeKind.LVAL_REFERENCE;
                        variNode = NewBinary(NodeKind.DEREFE, NewBinary(NodeKind.ADD, variNode, offsetNode), null);
                    }
                    variNode.ValType = vt;  // DEREFE先の変数情報を保持しておく
                }
            }
            else
            {
                if (isReference || isDerefe)
                {
                    if (isReference && !isDerefe)
                    {
                        // ref変数の中身のアドレスの中身
                        variNode.Kind = scope == Scope.GLOBAL ? NodeKind.GVAL_REFERENCE : NodeKind.LVAL_REFERENCE;
                        variNode = NewBinary(NodeKind.DEREFE, variNode, null);
                    }
                    else if (!isReference && isDerefe)
                    {
                        // 変数のアドレスにoffsetを足したアドレスの中身
                        variNode.Kind = scope == Scope.GLOBAL ? NodeKind.GVAL_DEREFE : NodeKind.LVAL_DEREFE;
                        variNode = NewBinary(NodeKind.DEREFE, NewBinary(NodeKind.ADD, variNode, offsetNode), null);
                    }
                    else
                    {
                        // ref変数の中身のアドレスにoffsetを足したアドレスの中身
                        variNode.Kind = scope == Scope.GLOBAL ? NodeKind.GVAL_REFERENCE : NodeKind.LVAL_REFERENCE;
                        variNode = NewBinary(NodeKind.DEREFE, NewBinary(NodeKind.ADD, variNode, offsetNode), null);
                    }
                    variNode.ValType = vt;  // DEREFE先の変数情報を保持しておく
                }
                variNode = NewBinary(NodeKind.HVAL, variNode, boxsOffsetNode);
                SetVariableState(variNode, variable, identTk.StrIdx, identTk.StrLen);   // トップノードにも変数情報を入れておく
                variNode.ValType = vt;  // HVALにも変数情報を保持しておく
            }


            return variNode;
        }

        /// <summary>
        /// 変数の初期化部分
        /// </summary>
        /// <param name="identTk">IDENTのトークン</param>
        /// <param name="scope">スコープ</param>
        /// <param name="varDefNode">initのNode追加先変数定義Node</param>
        /// <param name="variable">変数情報</param>
        /// <returns>true:成功 false:失敗</returns>
        private bool GLVariableInitialize(Tokenizer.Token identTk, Scope scope, Node varDefNode, Variable variable, bool OutPutError)
        {
            if (Consum("="))
            {
                NodeKind derefeNK = scope == Scope.GLOBAL ? NodeKind.GVAL_DEREFE : NodeKind.LVAL_DEREFE;
                NodeKind defNK = scope == Scope.GLOBAL ? NodeKind.GVAL_DEF : NodeKind.LVAL_DEF;
                (TokenKind, string)[] skipkinds = { (TokenKind.RESERVED, ";") };
                // 変数の初期化部分
                if (variable.ValType.Type == VariableKind.ARRAY)
                {
                    if (Consum("{"))
                    {
                        Node varDerefeNode = NewNode(derefeNK);
                        SetVariableState(varDerefeNode, variable, identTk.StrIdx, identTk.StrLen);
                        if (varDefNode != null)
                        {
                            varDefNode.Init = NewNode(defNK);
                            varDefNode.Init.Block = new List<Node>();     // 配列の場合は複数初期化式があるのでBlockに追加していく
                        }

                        // 代入先がrefの場合はアドレスを入れないといけない
                        if (!ArrayInitialize(varDefNode?.Init.Block ?? null, varDerefeNode, variable.ValType.GetEndKind(), variable.ValType, 0))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (OutPutError)
                        {
                            Error(ca.ErrorData.Str(ERROR_TEXT.ARRAY_INIT));
                        }
                        return false;
                    }
                }
                else if (variable.ValType.Type == VariableKind.STRUCT)
                {

                    if (Consum("{"))
                    {
                        Node lvarNode = NewNode(derefeNK);
                        SetVariableState(lvarNode, variable, identTk.StrIdx, identTk.StrLen);
                        if (varDefNode != null)
                        {
                            varDefNode.Init = NewNode(defNK);
                            varDefNode.Init.Block = new List<Node>();     // 配列の場合は複数初期化式があるのでBlockに追加していく
                        }

                        // 代入先がrefの場合はアドレスを入れないといけない
                        if (!StructInitialize(varDefNode?.Init.Block ?? null, lvarNode, variable.ValType, 0))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (OutPutError)
                        {
                            Error(ca.ErrorData.Str(ERROR_TEXT.STRUCT_INIT));
                        }
                        return false;

                    }
                }
                else if (variable.ValType.Type == VariableKind.REFERENCE)
                {
                    Node addressNode = AddressGLVariable();
                    if (varDefNode != null)
                    {
                        varDefNode.Init = addressNode;
                        if (varDefNode.Init == null)
                        {
                            if (OutPutError)
                            {
                                Error(ca.ErrorData.Str(ERROR_TEXT.REFERENCE_INIT_ADDR));
                            }
                            return false;
                        }
                        //nodeとnode.initの型が同じか調べる
                        if (!variable.ValType.PointerTo.Equal(varDefNode.Init.ValType))
                        {

                            if (OutPutError)
                            {
                                Error(ca.ErrorData.Str(ERROR_TEXT.REFERENCE_INIT_TYPE));
                            }
                            return false;
                        }
                    }
                }
                else if (variable.ValType.Type == VariableKind.BOXS)
                {
                    if (varDefNode != null)
                    {
                        Tokenizer.Token allocTk = currentToken;
                        if (Consum(TokenKind.ALLOCATE))
                        {
                            Expect("(", allocTk.StrIdx, allocTk.StrLen);
                            int nowErrorCount = result.ErrorCount;
                            Node allocateNode = new Node(NodeKind.ALLOCATE);
                            allocateNode.Lhs = Expression();    // 左ノードに確保サイズを求めるNodeを入れておく1
                            Expect(")", allocTk.StrIdx, allocTk.StrLen);
                            if (nowErrorCount < result.ErrorCount)
                            {
                                // エラーが増えている
                                return false;
                            }
                            varDefNode.Init = allocateNode;
                        }
                        else
                        {

                        }
                    }
                }
                else
                {
                    int nowErrorCount = result.ErrorCount;
                    Node expressionNode = Expression();
                    if (nowErrorCount < result.ErrorCount)
                    {
                        // エラーが増えている
                        return false;
                    }
                    if (varDefNode != null)
                    {
                        varDefNode.Init = expressionNode;
                        NodeKind kind = Converter.Define.ToNodeKind(variable.ValType.GetEndKind());
                        if (!IsAssign(kind, varDefNode.Init))
                        {
                            if (OutPutError)
                            {
                                Error(ca.ErrorData.Str(ERROR_TEXT.VARIABLE_INIT, variable.ValType.Type.ToString()));
                            }
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// タイプの取得と定義
        /// </summary>
        /// <param name="vt">タイプの格納先</param>
        /// <param name="size">タイプのサイズ</param>
        /// <returns>タイプの取得に成功した場合はtrue</returns>
        private bool TypeDefine(out VariableType vt, out int size)
        {
            return Type(out vt, out size, true, false);
        }

        /// <summary>
        /// タイプの取得
        /// </summary>
        /// <param name="vt">タイプの格納先</param>
        /// <param name="size">タイプのサイズ</param>
        /// <returns>タイプの取得に成功した場合はtrue</returns>
        private bool Type(out VariableType vt, out int size, bool isdSkip)
        {
            return Type(out vt, out size, false, isdSkip);
        }

        /// <summary>
        /// タイプの取得と定義
        /// </summary>
        /// <param name="vt">タイプの格納先</param>
        /// <param name="size">タイプのサイズ</param>
        /// <param name="isDefine">タイプを定義するか</param>
        /// <returns>タイプの取得に成功した場合はtrue</returns>
        private bool Type(out VariableType vt, out int size, bool isDefine, bool isSkip)
        {
            // type ("[" num? "]") ("[" num "]")* 
            Tokenizer.Token typeToken = currentToken;
            VariableType vtRef = null;
            vt = null;
            size = 1;

            if (typeToken.Kind == TokenKind.REFERENCE)
            {
                vtRef = new VariableType(VariableKind.REFERENCE);
                NextToken();
                typeToken = currentToken;
            }

            //TODO ここで読み進めると
            if (typeToken.Kind == TokenKind.TYPE || typeToken.Kind == TokenKind.IDENT)
            {
                string typeStr = ca.GetSourceStr(typeToken.StrIdx, typeToken.StrLen);

                // 基本型
                foreach (var t in PrimitiveType)
                {
                    if (typeStr == t.str)
                    {
                        // 一致する型名があった
                        vt = new VariableType(t.kind);
                        break;
                    }
                }
                //　構造体
                foreach (var mem in members)
                {
                    if (typeStr == mem.Name)
                    {
                        // 一致する構造体名があった
                        vt = new VariableType(VariableKind.STRUCT);
                        vt.Member = mem;
                        size = mem.Size;    // 構造体の場合は構造体の大きさが変数の大きさになる
                        break;
                    }
                }
                if (vt == null)
                {
                    if (isDefine)
                    {
                        // typeが見つからなかったので構造体として定義する。
                        Member member = new Member();
                        member.Name = typeStr;
                        member.FileName = ca.File;
                        (string lineStr, int lineNo) = Generic.GetaSourceLineStrNo(ca.Source, typeToken.StrIdx, typeToken.StrLen);
                        member.LineNo = lineNo;
                        vt = new VariableType(VariableKind.STRUCT);
                        vt.Member = member;
                        members.Add(member);
                    }
                    else
                    {
                        if (!isSkip)
                        {
                            ErrorBase();
                        }
                        return false;
                    }
                }
                NextToken();

                VariableType vtArray = new VariableType(VariableKind.INVALID);// 作業用
                VariableType vtArrayHead = vtArray;

                if (vt.GetEndKind() == VariableKind.BOXS)
                {
                    // boxsのタイプ指定
                    if (Consum("<"))
                    {
                        Tokenizer.Token boxtypeTk = currentToken;
                        if (Consum(TokenKind.TYPE))
                        {
                            string ident = ca.GetSourceStr(boxtypeTk.StrIdx, boxtypeTk.StrLen);
                            VariableKind vk = VariableKind.INVALID;
                            foreach (var type in BoxsType)
                            {
                                if (ident == type.str)
                                {
                                    vk = type.kind;
                                    break;
                                }
                            }
                            if (vk != VariableKind.INVALID)
                            {
                                // 型があった
                                vt.PointerTo = new VariableType(vk);
                            }
                            else
                            {
                                // 知らない型
                                Error(ca.ErrorData.Str(ERROR_TEXT.BOXS_TYPE), boxtypeTk.StrIdx, boxtypeTk.StrLen);
                                (TokenKind kind, string reserve)[] skipKinds = { (TokenKind.RESERVED, ">"), };
                                SkipKinds(skipKinds);
                            }
                        }
                        else
                        {
                            Error(ca.ErrorData.Str(ERROR_TEXT.BOXS_TYPE), boxtypeTk.StrIdx, boxtypeTk.StrLen);
                            (TokenKind kind, string reserve)[] skipKinds = { (TokenKind.RESERVED, ">"), };
                            SkipKinds(skipKinds);
                        }
                        Expect(">", boxtypeTk.StrIdx, boxtypeTk.StrLen);
                    }
                    else
                    {
                        //エラー　boxsの型指定がない
                        Error(ca.ErrorData.Str(ERROR_TEXT.BOXS_NO_TYPE), typeToken.StrIdx, typeToken.StrLen);
                        (TokenKind kind, string reserve)[] skipKinds = { (TokenKind.IDENT, ""), };
                        SkipKinds(skipKinds);
                    }
                }
                else
                {
                    // n次元配列の作成
                    // array -> array -> ...
                    int count = 0;
                    while (Consum("[") && !AtEOF())
                    {
                        vtArray.PointerTo = new VariableType(VariableKind.ARRAY);
                        if (Consum("]"))
                        {
                            if (count == 0)
                            {
                                // 最初の配列のみサイズ指定なくても大丈夫.
                                vtArray.ArraySize = 0;  // 初期化式の際にサイズ決めるので現状は0
                                vtArray = vtArray.PointerTo;
                                count++;
                                continue;
                            }
                            else
                            {
                                Error(ca.ErrorData.Str(ERROR_TEXT.ARRAY_SIZE_INDEFINITE));
                                (TokenKind, string)[] kinds = { (TokenKind.RESERVED, "]") };
                                SkipKinds(kinds);

                                // サイズが分らないのでとりあえず0にしておく
                                vtArray.PointerTo.ArraySize = 0;
                                vtArray = vtArray.PointerTo;
                                count++;
                                Skip("]");
                                continue;
                                //return false;
                            }
                        }

                        Tokenizer.Token indexTK = currentToken;
                        (bool success, int value) = ExpectInteger();
                        vtArray.PointerTo.ArraySize = value;
                        if (success && vtArray.PointerTo.ArraySize == 0)
                        {
                            // エラー表示して読み飛ばし
                            Error(ca.ErrorData.Str(ERROR_TEXT.ARRAY_SIZE_ZERO));
                            (TokenKind kind, string reserve)[] skipKinds = { (TokenKind.RESERVED, "]"), };
                            SkipKinds(skipKinds);
                        }
                        else if (!success)
                        {
                            // エラー表示して読み飛ばし
                            Error(ca.ErrorData.Str(ERROR_TEXT.ARRAY_SIZE_UNUSED_INDEX));
                            (TokenKind kind, string reserve)[] skipKinds = { (TokenKind.RESERVED, "]"), };
                            SkipKinds(skipKinds);
                        }
                        size *= vtArray.PointerTo.ArraySize;
                        vtArray = vtArray.PointerTo;
                        Expect("]", indexTK.StrIdx, indexTK.StrLen);
                        count++;
                    }
                }
                // PointerToは配列がある場合はarrayを先頭にする
                // array -> type
                if (vtArrayHead.Type != vtArray.Type)
                {
                    vtArray.PointerTo = vt;
                    vt = vtArrayHead.PointerTo;
                }
                // reference -> (array -> type)
                if (vtRef != null)
                {
                    vtRef.PointerTo = vt;
                    vt = vtRef;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// definestruct = "struct" ident "{" type ident ";" (type ident ";")* "}" ";"
        /// </summary>
        /// <returns>構造体定義に成功した場合はtrue</returns>
        private bool DefineStruct()
        {
            // struct A {
            //  int a;
            //  float b;
            //  ...
            // };
            if (Consum(TokenKind.STRUCT))
            {
                Tokenizer.Token IdentTk = currentToken;
                if (Consum(TokenKind.IDENT))
                {
                    Expect("{", IdentTk.StrIdx, IdentTk.StrLen);

                    Member member = null;
                    string ident = ca.GetSourceStr(IdentTk.StrIdx, IdentTk.StrLen);
                    // 定義済みかチェック
                    foreach (var m in members)
                    {
                        if (m.Name == ident)
                        {
                            if (m.IsDefined)
                            {
                                // すでに定義されています
                                // 名前だけ同じなのでそのエラーだけ出して
                                // その後の構文は間違っていないか？はチェックする
                                Error(ca.ErrorData.Str(ERROR_TEXT.STRUCT_ALREADY_DEFINE, m.FileName, ident), IdentTk.StrIdx, IdentTk.StrLen);
                                break;
                            }
                            member = m;
                            break;
                        }
                    }

                    // 定義されていなかった。
                    if (member == null)
                    {
                        member = new Member();
                        member.Name = ident;
                        member.FileName = ca.File;
                        (string lineStr, int lineNo) = Generic.GetaSourceLineStrNo(ca.Source, IdentTk.StrIdx, IdentTk.StrLen);
                        member.LineNo = lineNo;
                        members.Add(member);
                    }

                    // Type or Struct
                    int offset = 0;
                    (TokenKind, string reserved)[] structEndKind = { (TokenKind.RESERVED, "}") };
                    while (!Consum("}") && !AtEOF())
                    {
                        // タイプ取得
                        VariableType vt;
                        int vSize;
                        int nowErrorCount = result.ErrorCount;
                        if (TypeDefine(out vt, out vSize))
                        {
                            if (nowErrorCount < result.ErrorCount)
                            {
                                // ; までスキップ
                                SkipKinds(structEndKind);
                                continue;
                            }

                            Tokenizer.Token memberIdentTk = currentToken;
                            if (Consum(TokenKind.IDENT))
                            {
                                // 変数の追加
                                string memberName = ca.GetSourceStr(memberIdentTk.StrIdx, memberIdentTk.StrLen);
                                foreach (var mem in member.Variables)
                                {
                                    // 同じメンバ名がないかチェック
                                    if (mem.Name == memberName)
                                    {
                                        Error(ca.ErrorData.Str(ERROR_TEXT.STRUCT_ALREADY_DEFINE_MEMBER, memberName), memberIdentTk.StrIdx, memberIdentTk.StrLen);
                                        //return false;
                                    }
                                }
                                Variable v = new Variable(memberName, vt, offset);
                                member.Variables.Add(v);

                                offset += vSize;
                                Expect(";", memberIdentTk.StrIdx, memberIdentTk.StrLen);
                            }
                            else
                            {
                                Error(ca.ErrorData.Str(ERROR_TEXT.VARIABLE_NOT_DEFINE));
                                // } までスキップ
                                SkipKinds(structEndKind);
                            }
                        }
                        else
                        {
                            Error(ca.ErrorData.Str(ERROR_TEXT.STRUCT_NOT_DEFINE_VARIABLE_TYPE));
                            // } までスキップ
                            SkipKinds(structEndKind);
                        }
                    }
                    Expect(";", IdentTk.StrIdx, IdentTk.StrLen);
                    member.IsDefined = true;
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// defineenum = "enum" ident "{" ident ("=" num)* ";" "}" ";"
        /// </summary>
        /// <returns>ENUMの定義に成功した場合はtrue</returns>
        private bool DefineEnum()
        {
            // enum A {
            //    A
            // }
            if (Consum(TokenKind.ENUM))
            {
                Tokenizer.Token IdentTk = currentToken;
                if (Consum(TokenKind.IDENT))
                {
                    string name = ca.GetSourceStr(IdentTk.StrIdx, IdentTk.StrLen);

                    DefineInfo info;
                    if (IsDefine(CheckType.ENUM, name, out info))
                    {
                        // 同じ名前で何か定義されている
                        // エラーだけ出してその後の構文に間違いがないか？はチェックする
                        Error(ca.ErrorData.Str(ERROR_TEXT.ENUM_ALREADY_DEFINE, info.FileName, name), IdentTk.StrIdx, IdentTk.StrLen);
                    }

                    if (Consum("{"))
                    {
                        Enum _enum = new Enum();
                        _enum.Name = name;

                        int value = 0;
                        while (!Consum("}") && !AtEOF())
                        {
                            Tokenizer.Token elementTk = currentToken;
                            if (Consum(TokenKind.IDENT))
                            {
                                string eName = ca.GetSourceStr(elementTk.StrIdx, elementTk.StrLen);

                                if (Consum("="))
                                {
                                    (bool success, int expectValue) = ExpectInteger();
                                    if (!success)
                                    {
                                        Error(ca.ErrorData.Str(ERROR_TEXT.ENUM_MEMBER_IS_NUMBER));
                                        (TokenKind, string)[] kinds = { (TokenKind.RESERVED, ","), (TokenKind.RESERVED, "}") };
                                        SkipKinds(kinds);
                                    }
                                    if (Consum("-"))
                                    {
                                        value = 0 - expectValue;
                                    }
                                    else
                                    {
                                        value = expectValue;
                                    }
                                    Expect(",", elementTk.StrIdx, elementTk.StrLen);
                                }
                                else
                                {
                                    Expect(",", elementTk.StrIdx, elementTk.StrLen);
                                }

                                // 定義済みメンバーか調べる
                                foreach (var element in _enum.Elements)
                                {
                                    if (eName == element.name)
                                    {
                                        Error(ca.ErrorData.Str(ERROR_TEXT.ENUM_ALREADY_DEFINE_MEMBER, name, eName), elementTk.StrIdx, elementTk.StrLen);
                                    }
                                }
                                _enum.Elements.Add((eName, value));

                                value++;
                            }
                            else
                            {
                                Error(ca.ErrorData.Str(ERROR_TEXT.ENUM_DIFFERENCE_DEFINE), elementTk.StrIdx, elementTk.StrLen);
                                (TokenKind, string)[] kinds = { (TokenKind.RESERVED, ","), (TokenKind.RESERVED, "}") };
                                SkipKinds(kinds);
                            }
                        }
                        Expect(";", IdentTk.StrIdx, IdentTk.StrLen);

                        enums.Add(_enum);
                    }
                }
                else
                {
                    Error(ca.ErrorData.Str(ERROR_TEXT.ENUM_DIFFERENCE_DEFINE), IdentTk.StrIdx, IdentTk.StrLen);
                    (TokenKind, string)[] kinds = { (TokenKind.RESERVED, "}") };
                    SkipKinds(kinds);
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 指定ノードタイプでノード作成
        /// </summary>
        /// <param name="kind">ノードタイプ</param>
        /// <returns>作成ノード</returns>
        private Node NewNode(NodeKind kind)
        {
            Node node = new Node(kind);
            return node;
        }

        /// <summary>
        /// 指定ノードタイプでノード作成
        /// LhsとRhsの設定も行う
        /// </summary>
        /// <param name="kind">ノードタイプ</param>
        /// <param name="lhs">レフトハンドサイド</param>
        /// <param name="rhs">ライトハンドサイド</param>
        /// <returns>作成ノード</returns>
        private Node NewBinary(NodeKind kind, Node lhs, Node rhs)
        {
            Node node = NewNode(kind);
            node.Lhs = lhs;
            node.Rhs = rhs;
            return node;
        }

        /// <summary>
        /// 整数ノードの作成
        /// </summary>
        /// <param name="value">ノードの整数値</param>
        /// <returns>作成ノード</returns>
        private Node NewNum(int value)
        {
            Node node = NewNode(NodeKind.INTEGER);
            node.ValueI = value;
            return node;
        }

        /// <summary>
        ///  浮動小数点ノードの作成
        /// </summary>
        /// <param name="value">ノードの浮動小数値</param>
        /// <returns>作成ノード</returns>
        private Node NewNum(float value)
        {
            Node node = NewNode(NodeKind.FLOAT);
            node.ValueF = value;
            return node;
        }

        /// <summary>
        /// 文字列ノードの作成
        /// </summary>
        /// <param name="strIdx">ソース内の文字開始位置</param>
        /// <param name="strLen">文字列の長さ</param>
        /// <returns>作成ノード</returns>
        private Node NewString(int strIdx, int strLen)
        {
            Node node = NewNode(NodeKind.STRING);
            node.StrIdx = strIdx;
            node.StrLen = strLen;
            return node;
        }

        /// <summary>
        /// 指定変数のデリファレンス初期化ノード作成
        /// </summary>
        /// <param name="variableNode">指定変数</param>
        /// <param name="assignValueNode">初期化値ノード</param>
        /// <param name="offset">デリファレンスのためのOffset</param>
        /// <returns></returns>
        private Node NewDereferenceVariableInit(Node variableNode, Node assignValueNode, int offset)
        {
            Node derfeNode = NewBinary(NodeKind.DEREFE, NewBinary(NodeKind.ADD, variableNode, NewNum(offset)), null);
            derfeNode.ValType = variableNode.ValType;
            Node assignNode = NewBinary(NodeKind.ASSIGN, derfeNode, assignValueNode);
            return assignNode;
        }

        /// <summary>
        /// 指定トークンが指定記号か
        /// </summary>
        /// <param name="token">トークン</param>
        /// <param name="op">記号</param>
        /// <returns></returns>
        private bool isReserved(Tokenizer.Token token, string op)
        {
            if (token == null) return false;
            int length = Encoding.UTF8.GetByteCount(op);
            string str = Encoding.UTF8.GetString(ca.Source, token.StrIdx, token.StrLen);
            return token.Kind == TokenKind.RESERVED && token.StrLen == length && op == str;
        }

        /// <summary>
        /// 現在のトークンが期待している記号のときには、トークンを1つ読み進めて
        /// 真を返す。それ以外の場合には偽を返す。
        /// </summary>
        /// <param name="token"></param>
        /// <param name="op"></param>
        /// <returns>現在のトークンが指定記号の場合はtrue</returns>
        private bool Consum(string op)
        {
            if (!isReserved(currentToken, op))
            {
                return false;
            }
            NextToken();
            return true;
        }

        /// <summary>
        /// 現在のトークンが期待しているタイプのときには、トークンを1つ読み進めて
        /// 真を返す。それ以外の場合には偽を返す。
        /// </summary>
        /// <param name="kind">タイプ</param>
        /// <param name="token">期待していたトークン</param>
        /// <returns>現在のトークンが指定タイプの場合はtrue</returns>
        private bool Consum(TokenKind kind, out Tokenizer.Token token)
        {
            token = null;
            if (currentToken.Kind != kind)
            {
                return false;
            }
            token = currentToken;
            NextToken();
            return true;
        }

        /// <summary>
        /// 現在のトークンが期待しているタイプのときには、トークンを1つ読み進めて
        /// 真を返す。それ以外の場合には偽を返す。
        /// </summary>
        /// <param name="kind">タイプ</param>
        /// <returns>現在のトークンが指定タイプの場合はtrue</returns>
        private bool Consum(TokenKind kind)
        {
            Tokenizer.Token token;
            return Consum(kind, out token);
        }


        /// <summary>
        /// 現在のトークンとその次のトークンが期待しているタイプのときには、トークンを2つ読み進めて
        /// 真を返す。それ以外の場合には偽を返す。
        /// </summary>
        /// <param name="kind1">タイプ1</param>
        /// <param name="kind2">タイプ2</param>
        /// <param name="token1">期待していたトークン1</param>
        /// <param name="token2">期待していたトークン2</param>
        /// <returns>現在のトークンが指定タイプの場合はtrue</returns>
        private bool Consum(TokenKind kind1, string op, out Tokenizer.Token token1, out Tokenizer.Token token2)
        {
            token1 = null;
            token2 = null;
            if (currentToken.Kind == kind1 && isReserved(currentToken.Next as Tokenizer.Token, op))
            {
                token1 = currentToken;
                NextToken();
                token2 = currentToken;
                NextToken();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 現在のトークンが期待している記号のときには、トークンを1つ読み進めて
        /// それ以外の場合にはエラーを報告する。
        /// </summary>
        /// <param name="op">期待している記号</param>
        /// <param name="errorStrIdx">エラーがでた時に表示したい文字位置</param>
        /// <param name="errorStrLen">エラーがでた時に表示したい文字の長さ</param>
        /// <returns>現在のトークンが指定記号の場合はtrue</returns>
        private void Expect(string op, int errorStrIdx, int errorStrLen)
        {
            if (!isReserved(currentToken, op))
            {
                // ※エラー報告
                if (isExpectErrorDraw)
                {
                    Error(ca.ErrorData.Str(ERROR_TEXT.SYMBOL_IS_REQUIRED, op), errorStrIdx, errorStrLen);
                }
                return;
            }
            NextToken();
        }

        /// <summary>
        /// 現在のトークンが数字またはENUMのときには、トークンを1つ読み進めて
        /// それ以外の場合にはエラーを報告する。
        /// </summary>
        /// <returns></returns>
        private int ExpectIntegerEnum()
        {
            TokenKind tk = currentToken.Kind;
            if (tk != TokenKind.INTEGER && tk != TokenKind.IDENT)
            {
                // ※エラー報告
                Error(ca.ErrorData.Str(ERROR_TEXT.INTEGER_ENUM));
                return 0;
            }

            int retval = 0;
            switch (tk)
            {
                case TokenKind.INTEGER:
                    {
                        retval = currentToken.ValueI;
                        NextToken();
                    }
                    break;

                case TokenKind.IDENT:
                    Tokenizer.Token identTK = currentToken;
                    NextToken();
                    if (!Consum("."))
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.ENUM_DIFFERENCE_SPECIFY));
                        return 0;
                    }
                    Tokenizer.Token elementTk = currentToken;
                    if (!Consum(TokenKind.IDENT))
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.ENUM_DIFFERENCE_SPECIFY));
                        return 0;
                    }
                    string ident = ca.GetSourceStr(identTK.StrIdx, identTK.StrLen);
                    string element = ca.GetSourceStr(elementTk.StrIdx, elementTk.StrLen);
                    (bool define, int value) = GetEnumValue(ident, element);
                    if (define)
                    {
                        retval = value;
                        break;
                    }

                    Error(ca.ErrorData.Str(ERROR_TEXT.STRUCT_NOT_MEMBER, ident, element), identTK.StrIdx, identTK.StrLen);
                    return 0;
            }
            return retval;
        }

        /// <summary>
        /// 現在のトークンが数字のときには、トークンを1つ読み進める
        /// </summary>
        /// <returns>成功か,トークンの数値</returns>
        private (bool success, int value) ExpectInteger()
        {
            if (currentToken.Kind != TokenKind.INTEGER)
            {
                // エラー
                //Error(ca.ErrorData.Str(ERROR_TEXT.NOT_INTEGER));
                return (false, 0);
            }
            int value = currentToken.ValueI;
            NextToken();
            return (true, value);
        }

        /// <summary>
        /// 現在のトークンが文字列の場合はトークンを1つ読み進める
        /// </summary>
        /// <returns>成功か、トークンの始まる位置と文字数</returns>
        private (bool success, int strIdx, int strLen) ExpectString()
        {
            if (currentToken.Kind != TokenKind.STRING)
            {
                // エラー
                //Error(ca.ErrorData.Str(ERROR_TEXT.NOT_STRING));
                return (false, 0, 0);
            }
            int sidx = currentToken.StrIdx;
            int slen = currentToken.StrLen;
            NextToken();
            return (true, sidx, slen);
        }

        /// <summary>
        /// 現在のトークンが期待している数字のときには、トークンを1つ読み進める
        /// </summary>
        /// <returns>成功か、トークンの数値</returns>
        private (bool success, float value) ExpectFloat()
        {
            if (currentToken.Kind != TokenKind.FLOAT)
            {
                // エラー
                //Error(ca.ErrorData.Str(ERROR_TEXT.NOT_FLOAT));
                return (false, 0.0f);
            }
            float value = currentToken.ValueF;
            NextToken();
            return (true, value);
        }

        /// <summary>
        /// lhsにrhsは代入可能か
        /// </summary>
        /// <param name="lhs">ライトハンドサイド</param>
        /// <param name="rhs">レフトハンドサイド</param>
        /// <returns>代入可能の場合はtrue</returns>
        private bool ExpectAssign(Node lhs, Node rhs)
        {
            VariableType vt = lhs?.ValType;

            // lhsは必ず変数である
            if (vt == null)
            {
                Error(ca.ErrorData.Str(ERROR_TEXT.ASSIGN_LEFT));
            }
            if (!IsAssign(Converter.Define.ToNodeKind(vt.GetEndKind()), rhs))
            {
                Error(ca.ErrorData.Str(ERROR_TEXT.ASSIGN_TYPE, vt.GetEndKind().ToString(), rhs.GetEndKind().ToString()));
            }
            return true;
        }

        /// <summary>
        /// 現在のトークンが期待した記号か？
        /// </summary>
        /// <param name="op">記号</param>
        /// <returns>期待した記号の場合はtrue</returns>
        private bool Peek(string op)
        {

            if (!isReserved(currentToken, op))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 現在のトークンが期待している記号のときには、トークンを1つ読み進めて
        /// それ以外の特に何もしない
        /// </summary>
        /// <param name="op">期待している記号</param>
        private void Skip(string op)
        {
            if (!isReserved(currentToken, op))
            {
                return;
            }
            NextToken();
        }

        /// <summary>
        /// 前処理の読み飛ばし
        /// </summary>
        /// <returns>読み飛ばしした場合はtrue</returns>
        private bool SkipPreprocess()
        {
            // ※読み飛ばし処理だけなので
            // ※書式エラーは書式を処理しているところで行う
            if (Consum(TokenKind.INCLUDE))
            {
                (bool success, int strIdx, int strLen) = ExpectString();
                //if (strLen == 0)
                //{
                //    Error(ca.ErrorData.Str(ERROR_TEXT.PREPROCESSOR_DIFFERENCE, TokenKind.INCLUDE.ToString().ToLower()));
                //    return true;
                //}
                return true;
            }
            if (Consum(TokenKind.MAIN))
            {
                (bool success, int strIdx, int strLen) = ExpectString();
                //if (strLen == 0)
                //{
                //    Error(ca.ErrorData.Str(ERROR_TEXT.PREPROCESSOR_DIFFERENCE, TokenKind.MAIN.ToString().ToLower()));
                //    return true;
                //}
                return true;
            }
            if (Consum(TokenKind.INIT))
            {
                (bool success, int strIdx, int strLen) = ExpectString();
                //if (strLen == 0)
                //{
                //    Error(ca.ErrorData.Str(ERROR_TEXT.PREPROCESSOR_DIFFERENCE, TokenKind.INIT.ToString().ToLower()));
                //    return true;
                //}
                return true;
            }
            if (Consum(TokenKind.SKILL))
            {
                return true;
            }
            if (Consum(TokenKind.ONETIME))
            {
                return true;
            }
            if (Consum(TokenKind.REPEATE))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// structの読み飛ばし
        /// </summary>
        /// <returns>読み飛ばした場合はtrue</returns>
        private bool SkipStructDefine()
        {
            if (Consum(TokenKind.STRUCT))
            {
                while (!Consum("}") && !AtEOF())
                {
                    NextToken();
                }
                Skip(";");
                return true;
            }
            return false;
        }

        /// <summary>
        /// enumの読み飛ばし
        /// </summary>
        /// <returns>読み飛ばした場合はtrue</returns>
        private bool SkipEnumDefine()
        {
            if (Consum(TokenKind.ENUM))
            {
                while (!Consum("}") && !AtEOF())
                {
                    NextToken();
                }
                Skip(";");
                return true;
            }
            return false;
        }

        /// <summary>
        /// 関数定義の読み飛ばし
        /// </summary>
        /// <returns>読み飛ばした場合はtrue</returns>
        private bool SkipFuncDefine()
        {
            if (Consum("("))
            {
                while (!Consum(")") && !AtEOF())
                {
                    NextToken();
                }

                // ブロックの読み飛ばし
                SkipBlock();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 指定トークンまで読み飛ばす
        /// </summary>
        /// <param name="tokenKinds"></param>
        private void SkipKinds((TokenKind kind, string reserved)[] tokenKinds)
        {
            while (!AtEOF())
            {
                foreach (var token in tokenKinds)
                {
                    if (token.reserved != "" && token.reserved != null)
                    {
                        if (isReserved(currentToken, token.reserved))
                        {
                            return;
                        }
                    }
                    else
                    {
                        if (currentToken.Kind == token.kind)
                        {
                            return;
                        }
                    }
                }
                NextToken();
            }
        }

        /// <summary>
        /// ブロック部分の読み飛ばし
        /// </summary>
        private bool SkipBlock()
        {
            int blockCount = 1;
            Skip("{");

            while (!AtEOF())
            {
                if (Consum("{"))
                {
                    blockCount++;
                    continue;
                }
                else if (Consum("}"))
                {
                    blockCount--;
                    continue;
                }
                if (blockCount == 0)
                {
                    // ブロックの終わり
                    return true;
                }
                NextToken();
            }
            return false;
        }

        /// <summary>
        /// 配列の初期化Node作成再帰関数
        /// </summary>
        /// <param name="block">初期化Node格納先</param>
        /// <param name="variNode">変数Node</param>
        /// <param name="vk">変数種類</param>
        /// <param name="vt">変数タイプ</param>
        /// <param name="parentSize">親のサイズ</param>
        private bool ArrayInitialize(List<Node> block, Node variNode, VariableKind vk, VariableType vt, int parentSize)
        {
            if (vt == null)
            {
                return true;
            }
            if (vt.Type != VariableKind.ARRAY)
            {
                Error(ca.ErrorData.Str(ERROR_TEXT.ARRAY_INIT_OVER));
                return false;
            }

            VariableKind childType = vt?.PointerTo.Type ?? VariableKind.INVALID;
            bool isArrayEnd = vt.Type == VariableKind.ARRAY && (childType != VariableKind.ARRAY);
            int arraySize = vt.ArraySize; // 現在のネストレベルの配列個数を取得
            int valueCount = 0;
            int currentIdx = -1;  // 配列の添え字は0から始めたいため.
            while (true && !AtEOF())
            {
                if (Consum(TokenKind.INTEGER, ":", out Tokenizer.Token token1, out Tokenizer.Token token2))
                {
                    //int[][...]* a = { 0:{ ... }, 1:{ ... }, ... };
                    //初期化列を選べるように
                    if (currentIdx >= token1.ValueI)
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.ARRAY_INIT_ORDER));
                        return false;
                    }
                    currentIdx = token1.ValueI;
                }
                else
                {
                    currentIdx++;
                }

                Tokenizer.Token token = currentToken;
                if (Consum("{"))
                {
                    if (vt.PointerTo.Type != VariableKind.STRUCT)
                    {
                        // ネスト
                        if (!ArrayInitialize(block, variNode, vk, vt.PointerTo, parentSize + currentIdx * vt.PointerTo.GetSize()))
                        {
                            // ネスト要素でエラー出た場合は終了
                            return false;
                        }

                        // 次が } ではない場合は,が必要
                        if (!Peek("}"))
                        {
                            Expect(",", token.StrIdx, token.StrLen);
                        }

                        if (Consum("}"))
                        {
                            // 今のネストレベルの初期化は終わり
                            if (arraySize == 0)
                            {
                                // 配列のサイズが決まっていないのでここで決めておく
                                vt.ArraySize = currentIdx + 1;
                            }
                            return true;
                        }
                        continue;
                    }
                }

                if (!isArrayEnd)
                {
                    // ここに来る時点で配列の末端ではないといけないので
                    // 末端ではない場合エラー
                    Error(ca.ErrorData.Str(ERROR_TEXT.ARRAY_NOT_ENOUGH));
                    return false;
                }

                if (vt.PointerTo.Type == VariableKind.STRUCT)
                {
                    if (!StructInitialize(block, variNode, vt.PointerTo, parentSize + currentIdx * vt.PointerTo.GetSize()))
                    {
                        // ネスト要素でエラー出た場合は終了
                        return false;
                    }
                }
                else
                {
                    // 初期化の値を取得。末端のみ有効。
                    // a...[valueCount] = valueI | valueS | valuF
                    NodeKind endKind = Converter.Define.ToNodeKind(vk);
                    int nowErrorCount = result.ErrorCount;
                    Node assignValNode = Expression();
                    if (nowErrorCount < result.ErrorCount)
                    {
                        return false;
                    }
                    if (!IsAssign(endKind, assignValNode))
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.VARIABLE_INIT, endKind.ToString().ToLower()));
                        return false;
                    }
                    block?.Add(NewDereferenceVariableInit(variNode, assignValNode, parentSize + currentIdx));
                }

                // 次が } ではない場合は,が必要
                if (!Peek("}"))
                {
                    Expect(",", token.StrIdx, token.StrLen);
                }

                if (Consum("}"))
                {
                    // 今のネストレベルの初期化は終わり
                    if (arraySize == 0)
                    {
                        // 配列のサイズが決まっていないのでここで決めておく
                        vt.ArraySize = currentIdx + 1;
                    }
                    return true;
                }
                valueCount++;
                if (arraySize != 0 && valueCount >= arraySize)
                {
                    Error(ca.ErrorData.Str(ERROR_TEXT.ARRAY_INIT_ELEMENT_OVER, arraySize.ToString()));
                    return false;
                }
            }

            // 配列の初期化途中でファイルが終わっている
            return false;
        }

        /// <summary>
        /// 構造体の初期化Node作成再帰関数
        /// </summary>
        /// <param name="block">Node追加先</param>
        /// <param name="variNode">構造体の変数Node</param>
        /// <param name="vt">現在の変数タイプ</param>
        /// <param name="parentSize">親のサイズ</param>
        private bool StructInitialize(List<Node> block, Node variNode, VariableType vt, int parentSize)
        {
            if (vt == null)
            {
                return true;
            }
            if (vt.Type != VariableKind.STRUCT)
            {
                Error(ca.ErrorData.Str(ERROR_TEXT.STRUCT_INIT_OVER));
                return false;
            }

            Member member = vt.Member;
            int index = 0;
            while (true && !AtEOF())
            {
                if (index >= member.Variables.Count)
                {
                    Error(ca.ErrorData.Str(ERROR_TEXT.STRUCT_INIT_ELEMENT_OVER, member.Variables.Count.ToString()));
                    return false;
                }

                Tokenizer.Token IdentTk = currentToken;
                Variable variable = null;
                if (Consum(TokenKind.IDENT))
                {
                    // indexの更新

                    string ident = ca.GetSourceStr(IdentTk.StrIdx, IdentTk.StrLen);
                    // struct a { int a; int b;} b = { a:0, b:1 };
                    // 初期化子を選べるように
                    if (Consum(":"))
                    {
                        for (int i = index; i < member.Variables.Count; i++)
                        {
                            Variable v = member.Variables[i];
                            if (ident == v.Name)
                            {
                                variable = member.Variables[i];
                                index = i;
                                break;
                            }
                        }
                        if (variable == null)
                        {
                            Error(ca.ErrorData.Str(ERROR_TEXT.STRUCT_ALREADY_DEFINE_MEMBER, ident));
                            return false;
                        }
                    }
                    else
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.STRUCT_INIT_ELEMENT_SPECIFY));
                        return false;
                    }
                }
                else
                {
                    variable = member.Variables[index];
                }

                if (variable.ValType.Type == VariableKind.ARRAY)
                {
                    if (Consum("{"))
                    {
                        // TODO variNodeをそのまま渡さない
                        // variNodeはStructなのでvariableをNodeかしたものを渡す
                        Node memNode = NewNode(variNode.Kind);
                        memNode.ValType = variable.ValType;
                        memNode.Offset = variNode.Offset;
                        memNode.StrIdx = variNode.StrIdx;
                        memNode.StrLen = variNode.StrLen;
                        if (!ArrayInitialize(block, memNode, memNode.ValType.GetEndKind(), memNode.ValType, parentSize + variable.Offset))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.ARRAY_INIT));
                        return false;
                    }
                }
                else if (variable.ValType.GetEndKind() == VariableKind.STRUCT)
                {
                    if (Consum("{"))
                    {
                        // 再帰呼び出しする
                        if (!StructInitialize(block, variNode, variable.ValType, variable.Offset))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.STRUCT_INIT));
                        return false;
                    }
                }
                else if (variable.ValType.Type == VariableKind.BOXS)
                {
                    Tokenizer.Token allocTk = currentToken;
                    if (Consum(TokenKind.ALLOCATE))
                    {
                        Expect("(", allocTk.StrIdx, allocTk.StrLen);
                        int nowErrorCount = result.ErrorCount;
                        Node allocateNode = new Node(NodeKind.ALLOCATE);
                        allocateNode.Lhs = Expression();    // 左ノードに確保サイズを求めるNodeを入れておく1
                        Expect(")", allocTk.StrIdx, allocTk.StrLen);
                        if (nowErrorCount < result.ErrorCount)
                        {
                            // エラーが増えている
                            return false;
                        }
                        if (!IsAssign(NodeKind.INTEGER, allocateNode.Lhs))
                        {
                            Error(ca.ErrorData.Str(ERROR_TEXT.BOXS_ALLOCATE_INTEGER), allocTk.StrIdx, allocTk.StrLen);
                            return false;
                        }
                        if (block != null)
                        {
                            Node memNode = NewNode(Converter.Define.ToScope(variNode.Kind) == Scope.GLOBAL ? NodeKind.GVAL_DEREFE : NodeKind.LVAL_DEREFE);
                            memNode.ValType = variable.ValType;
                            memNode.Offset = variNode.Offset;
                            memNode.StrIdx = variNode.StrIdx;
                            memNode.StrLen = variNode.StrLen;
                            block.Add(NewDereferenceVariableInit(memNode, allocateNode, parentSize + variable.Offset));
                        }
                    }
                    else
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.BOXS_ALLOCATE_INIT), allocTk.StrIdx, allocTk.StrLen);
                        return false;
                    }
                }
                else
                {
                    // 初期化の値を取得。末端のみ有効。
                    // a = valueI | valueS | valuF
                    NodeKind endKind = Converter.Define.ToNodeKind(variable.ValType.GetEndKind());
                    int nowErrorCount = result.ErrorCount;
                    Node assignValNode = Expression();
                    if (nowErrorCount < result.ErrorCount)
                    {
                        return false;
                    }
                    if (!IsAssign(endKind, assignValNode))
                    {
                        Error(ca.ErrorData.Str(ERROR_TEXT.VARIABLE_INIT, endKind.ToString().ToLower()));
                        return false;
                    }
                    if (block != null)
                    {
                        Node memNode = NewNode(Converter.Define.ToScope(variNode.Kind) == Scope.GLOBAL ? NodeKind.GVAL_DEREFE : NodeKind.LVAL_DEREFE);
                        memNode.ValType = variable.ValType;
                        memNode.Offset = variNode.Offset;
                        memNode.StrIdx = variNode.StrIdx;
                        memNode.StrLen = variNode.StrLen;
                        block.Add(NewDereferenceVariableInit(memNode, assignValNode, parentSize + variable.Offset));
                    }
                }

                // 次が } ではない場合は,が必要
                if (!Peek("}"))
                {
                    Expect(",", IdentTk.StrIdx, IdentTk.StrLen);
                }
                index++;

                if (Consum("}"))
                {
                    return true;
                }
            }

            // 配列の初期化途中でファイルが終わっている
            return false;
        }

        /// <summary>
        /// トークンの終わりか
        /// </summary>
        /// <returns></returns>
        private bool AtEOF()
        {
            return currentToken.Kind == TokenKind.EOF;
        }

        /// <summary>
        /// 変数の定義
        /// </summary>
        /// <param name="ident">定義変数名</param>
        /// <param name="variType">変数タイプ</param>
        /// <param name="scope">変数のスコープ</param>
        /// <returns>定義変数</returns>
        private Variable DefineVariable(string ident, VariableType variType, Scope scope)
        {
            if (scope == Scope.LOCAL)
            {
                // ローカル変数
                Variable lv = LVariables.DefineVariable(ident, variType);
                if (lv == null)
                {
                    // すでに登録済み.
                    // 登録できなかった
                    return null;
                }
                return lv;
            }
            else
            {
                DefineInfo info;
                // グローバル変数
                if (IsDefine(CheckType.GVARIABLE, ident, out info))
                {
                    // すでに登録済み.
                    // 登録できなかった
                    return null;
                }
                int prevIdx = globalVariables.Count - 1;
                int offset = globalVariables.Count == 0 ? 1 : globalVariables[prevIdx].Offset + globalVariables[prevIdx].Size;
                Variable gv = new Variable(ident, variType, offset);
                globalVariables.Add(gv);
                return gv;
            }
        }

        /// <summary>
        /// 変数の検索
        /// </summary>
        /// <param name="ident">検索する変数名</param>
        /// <param name="scope">スコープ</param>
        /// <returns>見つからなかった場合はスコープと変数</returns>
        private (Scope scope, Variable va) FindVariable(string ident, Scope scope = Scope.INVALID)
        {
            // ローカル変数を探してからなければグローバル変数を探す
            // Scopeの指定があれが指定されてスコープのみ探す
            if (scope == Scope.LOCAL || scope == Scope.INVALID)
            {
                Variable lv = LVariables.FindVariable(ident);
                if (lv != null)
                {
                    return (Scope.LOCAL, lv);
                }
            }
            if (scope == Scope.GLOBAL || scope == Scope.INVALID)
            {
                foreach (var gv in globalVariables)
                {
                    if (gv.Name == ident)
                    {
                        return (Scope.GLOBAL, gv);
                    }
                }
            }
            return (Scope.GLOBAL, null);
        }

        /// <summary>
        /// 定義されているか
        /// </summary>
        /// <param name="checkType">チェックタイプ</param>
        /// <param name="ident">検索名</param>
        /// <returns>定義されていればtrue</returns>
        private bool IsDefine(CheckType checkType, string ident, out DefineInfo info)
        {
            info = new DefineInfo();

            // ローカル変数
            if (checkType == CheckType.LVARIABLE || checkType == CheckType.ENUM)
            {
                if (LVariables.FindVariable(ident) != null)
                {
                    info.FileName = ca.File;
                    info.Type = CheckType.LVARIABLE;
                    return true;
                }
            }
            if (checkType == CheckType.GVARIABLE || checkType == CheckType.ENUM)
            {
                // グローバル変数
                foreach (var g in globalVariables)
                {
                    if (g.Name == ident)
                    {
                        // すでに登録済み.
                        info.FileName = g.FileName;
                        info.Type = CheckType.GVARIABLE;
                        return true;
                    }
                }
            }
            // ENUM
            foreach (var e in enums)
            {
                if (e.Name == ident)
                {
                    info.FileName = e.FileName;
                    info.Type = CheckType.ENUM;
                    return true;
                }
            }
            return false;

        }

        /// <summary>
        /// 代入できるか
        /// </summary>
        /// <param name="expectKind">代入先の型</param>
        /// <param name="node">代入するnode</param>
        /// <returns></returns>
        private bool IsAssign(NodeKind expectKind, Parser.Node node)
        {
            if (node == null)
            {
                // 特に問題なし
                return true;
            }

            switch (node.Kind)
            {
                case NodeKind.INTEGER:
                    if (node.Lhs == null && node.Rhs == null)
                    {
                        // 末端なので自分の情報を返す
                        // float = int; はfloatに許容してもらう
                        return NodeKind.FLOAT == expectKind || NodeKind.INTEGER == expectKind;
                    }
                    break;

                // 変数系の場合は変数の型を調べる
                case NodeKind.GVAL:
                case NodeKind.LVAL:
                case NodeKind.GVAL_REFERENCE:
                case NodeKind.LVAL_REFERENCE:
                case NodeKind.GVAL_DEF:
                case NodeKind.LVAL_DEF:
                case NodeKind.GVAL_DEREFE:
                case NodeKind.LVAL_DEREFE:
                case NodeKind.DEREFE:
                    VariableKind vk = node.ValType.Type;
                    if (node.ValType.Type == VariableKind.BOXS)
                    {
                        // boxsの場合はPointerToにタイプが入っているはず、なければエラー
                        vk = (node.ValType?.PointerTo.Type ?? VariableKind.INVALID);
                    }
                    NodeKind nk = Converter.Define.ToNodeKind(vk);
                    if (nk == NodeKind.INTEGER)
                    {
                        // float = int; はfloatに許容してもらう
                        return NodeKind.FLOAT == expectKind || NodeKind.INTEGER == expectKind;
                    }
                    else
                    {
                        return expectKind == nk;
                    }

                // 関数呼び出しの場合は戻り値の型を調べる
                case NodeKind.FUNC_CALL:
                    string funcName = node.FuncInfo.Name;
                    Parser.VariableType retVt = ca.Linker.GetFuncInfo(funcName)?.ReturnType;
                    if (retVt == null)
                    {
                        //Error(ca.ErrorData.Str(ERROR_TEXT.FUNC_RET_TYPE_NOT_DEFINE, funcName));
                        return false;
                    }
                    return expectKind == Converter.Define.ToNodeKind(retVt.Type);

                case NodeKind.GWST_CALL:
                    {
                        VariableKind variableKind = Generic.GWSTInfo(node.GwstType, node.GwstNo).RetType;
                        return expectKind == Converter.Define.ToNodeKind(variableKind);
                    }

                case NodeKind.SYS_CALL:
                    {
                        VariableKind variableKind = Generic.SYSInfo(node.SysKind).RetType;
                        return expectKind == Converter.Define.ToNodeKind(variableKind);
                    }

                case NodeKind.EQ:
                case NodeKind.NE:
                case NodeKind.LT:
                case NodeKind.LTE:
                    // 比較系は数値として扱っておく
                    return NodeKind.INTEGER == expectKind;

                default:
                    // 特に問題はない
                    //return true;
                    break;
            }

            if (node.Lhs == null && node.Rhs == null)
            {
                // 末端なので自分の情報を返す
                return node.Kind == expectKind;
            }

            // 枝葉の情報を返す
            return IsAssign(expectKind, node.Lhs) && IsAssign(expectKind, node.Rhs);

        }

        /// <summary>
        /// 指定ノードが有効なStatementExpressionか判定する
        /// </summary>
        /// <param name="node">調べるノード</param>
        /// <returns>有効な物の場合はtrue</returns>
        private bool IsValidStatementExpression(Node node)
        {
            // a = b ... ;
            // func();
            // gwst_call();
            // が無ければ無効
            // 関数 or gwst_callでも
            // func() + a ... ;
            // などは演算結果の格納をしないので無効
            if (node == null)
            {
                return true;
            }

            if (node.Kind == NodeKind.ASSIGN)
            {
                return true;
            }

            if (node.Kind == NodeKind.FUNC_CALL || node.Kind == NodeKind.GWST_CALL || node.Kind == NodeKind.DEBUG_LOG || node.Kind == NodeKind.DEBUG_PAUSE || node.Kind == NodeKind.RELEASE)
            {
                if (node.Lhs == null && node.Rhs == null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return false;
        }


        /// <summary>
        /// Nodeに変数情報をセット
        /// </summary>
        /// <param name="node">セット先のNode</param>
        /// <param name="v">セット内容</param>
        /// <param name="strIdx">ソース内の変数名開始位置</param>
        /// <param name="strLen">変数名数</param>
        private void SetVariableState(Node node, Variable v, int strIdx, int strLen)
        {
            node.StrIdx = strIdx;
            node.StrLen = strLen;
            node.Offset = v.Offset;
            node.ValType = v.ValType;
        }

        /// <summary>
        /// 次のトークンへ進める。
        /// 最後まで読み込んでいる場合は何もしない
        /// </summary>
        private void NextToken()
        {
            if (currentToken == null || (currentToken?.Next ?? null) == null)
            {
                // 現在か次がnullの場合は読み進めない
                return;
            }
            if (AtEOF())
            {
                // 最後まで読んでいる場合は何もしない
                return;
            }
            currentToken = currentToken.Next as Tokenizer.Token;
        }

        /// <summary>
        /// 指定した名前のENUM取得
        /// なかった場合はnull
        /// </summary>
        /// <param name="name">名前</param>
        /// <returns>ENUM</returns>
        private Enum GetEnum(string name)
        {
            foreach (var e in enums)
            {
                if (e.Name == name)
                {
                    return e;
                }
            }
            return null;
        }

        /// <summary>
        /// 指定した名前のENUMの指定した要素の値を取得
        /// ない場合はdefineがfalse
        /// </summary>
        /// <param name="name">enum名</param>
        /// <param name="elementName">要素名</param>
        /// <returns>要素の値</returns>
        private (bool define, int value) GetEnumValue(string name, string elementName)
        {
            return GetEnum(name)?.Value(elementName) ?? (false, 0);
        }

        /// <summary>
        /// GwstCallの引数チェック
        /// </summary>
        /// <param name="kinds">変数タイプ</param>
        /// <param name="size">サイズ</param>
        /// <param name="argNode">ノード</param>
        private bool RefArgCheck(VariableKind[] kinds, Node argNode)
        {
            if (kinds == null)
            {
                return false;
            }
            if (argNode == null)
            {
                return false;
            }


            //TODO ここから　変数の取得はこれでいいのか？
            //多分大丈夫だと思います
            //gwstの引数がaddrの場合addressGLVariableでパースするので
            VariableType vt = argNode.ValType;
            while (vt == null)
            {
                if (argNode == null)
                {
                    return false;
                }
                argNode = argNode.Lhs;
                vt = argNode.ValType;
            }

            int idx = 0;
            switch (vt.Type)
            {
                case VariableKind.STRUCT:
                    return RefArgCheckStrct(kinds, ref idx, vt.Member);

                case VariableKind.ARRAY:
                    return RefArgCheckArray(kinds, ref idx, vt);

                default:
                    return RefArgCheckPrimitive(kinds, ref idx, vt);

            }
        }


        private bool RefArgCheckArray(VariableKind[] kinds, ref int idx, VariableType vt)
        {
            if (idx < 0 || kinds.Length <= idx || vt.Type != VariableKind.ARRAY)
            {
                return false;
            }

            for (int i = 0; i < vt.GetSize(); i++)
            {
                VariableKind nextVk = vt.PointerTo.Type;
                if (nextVk == VariableKind.STRUCT)
                {
                    // 再帰呼び出し
                    if (!RefArgCheckStrct(kinds, ref idx, vt.PointerTo.Member))
                    {
                        return false;
                    }
                }
                else if (nextVk == VariableKind.ARRAY)
                {
                    // 再帰呼び出し
                    if (!RefArgCheckArray(kinds, ref idx, vt.PointerTo))
                    {
                        return false;
                    }
                }
                else
                {
                    // 基本型チェック
                    if (!RefArgCheckPrimitive(kinds, ref idx, vt.PointerTo))
                    {
                        return false;
                    }
                }

                if (idx >= kinds.Length)
                {
                    // 最後まで問題なく確認できたので成功
                    return true;
                }
            }

            // 問題なくサイズ分チェックできたので成功
            return true;

        }
        private bool RefArgCheckStrct(VariableKind[] kinds, ref int idx, Member mem)
        {
            if (idx < 0 || kinds.Length <= idx || mem == null)
            {
                return false;
            }

            foreach (var variable in mem.Variables)
            {
                if (variable.ValType.Type == VariableKind.STRUCT)
                {
                    if (!RefArgCheckStrct(kinds, ref idx, variable.ValType.Member))
                    {
                        return false;
                    }
                }
                else if (variable.ValType.Type == VariableKind.ARRAY)
                {
                    if (!RefArgCheckArray(kinds, ref idx, variable.ValType))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!RefArgCheckPrimitive(kinds, ref idx, variable.ValType))
                    {
                        return false;
                    }
                }

                if (idx >= kinds.Length)
                {
                    // 最後まで問題なく確認できたので成功
                    return true;
                }
            }

            // 問題なく最後までチェックできたので成功
            return true;
        }

        private bool RefArgCheckPrimitive(VariableKind[] kinds, ref int idx, VariableType vt)
        {
            if (idx < 0 || kinds.Length <= idx || vt == null)
            {
                return false;
            }

            int _idx = idx;
            idx++;
            return vt.Type == kinds[_idx];
        }

        /// <summary>
        /// エラー
        /// </summary>
        /// <param name="errorStr">エラー内容</param>
        private void Error(string errorStr)
        {
            Error(errorStr, currentToken.StrIdx, currentToken.StrLen);
        }

        /// <summary>
        /// エラー
        /// </summary>
        /// <param name="errorStr">エラー内容</param>
        /// <param name="strIdx">エラーが起きたソース内の位置</param>
        /// <param name="strLen">エラーが起きたトークンの文字数</param>
        private void Error(string errorStr, int strIdx, int strLen)
        {
            (string linestr, int lineno) = Generic.GetaSourceLineStrNo(ca.Source, strIdx, strLen);
            Error(ca.File, lineno, linestr, errorStr);
        }

        /// <summary>
        /// エラー
        /// </summary>
        /// <param name="lineno">エラーが出た行数</param>
        /// <param name="linestr">エラーが出た行</param>
        /// <param name="errorstr">エラー内容</param>
        private void Error(string filename, int lineno, string linestr, string errorstr)
        {
            ErrorBase(ca.ErrorData.Str(ERROR_TEXT.ERROR_BASE_FILENAME_LINENO, filename, $"{lineno:d4}", linestr, errorstr));
        }

        /// <summary>
        /// エラーとスローする
        /// </summary>
        /// <param name="str">エラー文言</param>
        private void ErrorBase(string str)
        {
            result.Log = result.Log + (result.Log.Length > 0 ? "\n" : "") + str;
            ErrorBase();
        }

        /// <summary>
        /// エラーとスローする
        /// </summary>
        private void ErrorBase()
        {
            result.Success = false;
            result.ErrorCount++;
            isParceError = true;

            // TODO 一定のエラー数になったら強制定期に終わる
            //if (false)
            //{
            //    throw new Compailer.CompileException();
            //}
        }

        /// <summary>
        /// 初期化
        /// </summary>
        public void Initialize()
        {
            currentToken = null;
            codes.Clear();
            globalInitCodes.Clear();
            globalVariables.Clear();
            members.Clear();
            enums.Clear();
            LVariables.Initialize();
            isParceError = false;
            result.Initialize();
            isExpectErrorDraw = false;
        }

        /// <summary>
        /// Define系前の初期化
        /// </summary>
        public void Initialize_Preprocess()
        {
            currentToken = null;
            codes.Clear();
            isParceError = false;
            result.Initialize();
            isExpectErrorDraw = false;
        }

        /// <summary>
        /// パース前の初期化
        /// </summary>
        private void Initialize_Do()
        {
            currentToken = null;
            codes.Clear();
            LVariables.Initialize();
            isParceError = false;
            result.Initialize();
            isExpectErrorDraw = true;
        }


        public LocalVariables LVariables { get; set; }      // ローカル変数

        private Tokenizer.Token currentToken;       // 現在のトークン
        private List<Node> codes;                   // パーサー処理後の構文木
        private List<Node> globalInitCodes;         // グルーバル変数の初期化式
        private List<Variable> globalVariables;     // グローバル変数のリスト
        private List<Member> members;               // 構造体定義
        private List<Enum> enums;                   // enum定義
        private VariableKind currentFuncRetVarType; // 現在のファンクションの戻り値
        private Node currentSwitch;                 // 現在のswitch
        private bool isParceError;                  // パーサーでエラーが出たか
        private ParseResult result;                 // パーサーの結果
        private Compailer.CompileArgs ca;           // コンパイラ汎用引数        
        private bool isExpectErrorDraw;
    }
}