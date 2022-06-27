using System;
using System.Text;

namespace MCCompilerConsole.Converter
{
    class Generic
    {
        /// <summary>
        /// バイト列を整数にする
        /// bytesはビックエンディアン方式
        /// システムアーキテクチャのエンディアンを考慮
        /// </summary>
        /// <param name="bytes">バイト列</param>
        /// <returns>整数</returns>
        static public int ToInt32(byte[] bytes)
        {
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToInt32(bytes, 0);
        }

        /// <summary>
        /// バイト列を不動小数点にする
        /// bytesはビックエンディアン方式
        /// システムアーキテクチャのエンディアンを考慮
        /// </summary>
        /// <param name="bytes">バイト列</param>
        /// <returns>不動小数点</returns>
        static public float ToFloat32(byte[] bytes)
        {
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToSingle(bytes, 0);
        }

        /// <summary>
        /// バイト列の取得
        /// return bytesはビックエンディアン方式
        /// システムアーキテクチャのエンディアンを考慮
        /// </summary>
        /// <param name="value">バイト列にする値</param>
        /// <returns>バイト列</returns>
        static public byte[] GetByte(int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return bytes;
        }

        /// <summary>
        /// バイト列の取得
        /// return bytesはビックエンディアン方式
        /// システムアーキテクチャのエンディアンを考慮
        /// </summary>
        /// <param name="value">バイト列にする値</param>
        /// <returns>バイト列</returns>
        static public byte[] GetByte(uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return bytes;
        }

        /// <summary>
        /// 引数用のレジスタ取得
        /// </summary>
        /// <param name="argNo">引数番号</param>
        /// <param name="vk">引数のタイプ(プリミティブ型)</param>
        /// <returns>レジスタ</returns>
        public static Operand GetArgRegister(int argNo, VariableKind vk)
        {
            if (vk == VariableKind.INT || vk == VariableKind.ARRAY || vk == VariableKind.STRUCT || vk == VariableKind.BOXS)
            {
                switch (argNo)
                {
                    case 0: return Operand.RI2;
                    case 1: return Operand.RI3;
                    case 2: return Operand.RI4;
                    case 3: return Operand.RI5;
                    case 4: return Operand.RI6;
                    case 5: return Operand.RI7;
                }
            }
            if (vk == VariableKind.STRING)
            {
                switch (argNo)
                {
                    case 0: return Operand.RS1;
                    case 1: return Operand.RS2;
                    case 2: return Operand.RS3;
                    case 3: return Operand.RS4;
                    case 4: return Operand.RS5;
                    case 5: return Operand.RS6;
                }
            }
            if (vk == VariableKind.FLOAT)
            {
                switch (argNo)
                {
                    case 0: return Operand.RF1;
                    case 1: return Operand.RF2;
                    case 2: return Operand.RF3;
                    case 3: return Operand.RF4;
                    case 4: return Operand.RF5;
                    case 5: return Operand.RF6;
                }
            }
            return Operand.INVALID;
        }

        /// <summary>
        /// リターン用レジスタ取得
        /// </summary>
        /// <param name="vk">リターンのタイプ(プリミティブ型)</param>
        /// <returns>レジスタ</returns>
        public static Operand GetReturnRegister(VariableKind vk)
        {
            switch (vk)
            {
                case VariableKind.INT: return Operand.RI1;
                case VariableKind.STRING: return Operand.RS1;
                case VariableKind.FLOAT: return Operand.RF1;
            }
            return Operand.INVALID;
        }

        /// <summary>
        /// ミーモニックのオペランド数取得
        /// </summary>
        /// <param name="mnemonic">ニーモニック</param>
        /// <returns>オペランド数</returns>
        public static int GetOperandByte(Mnemonic mnemonic)
        {
            switch (mnemonic)
            {
                case Mnemonic.RET:
                case Mnemonic.NOP:
                case Mnemonic.MAGICEND:
                case Mnemonic.SAVESPS:
                case Mnemonic.LOADSPS:
                case Mnemonic.DEBUG_RLOG:
                case Mnemonic.DEBUG_SLOG:
                case Mnemonic.DEBUG_PAUSE:
                    return 0;

                case Mnemonic.PUSH:
                case Mnemonic.POP:
                case Mnemonic.SETE:
                case Mnemonic.SETNE:
                case Mnemonic.SETL:
                case Mnemonic.SETLE:
                case Mnemonic.HEAPALLOCATE:
                case Mnemonic.HEAPRELEASE:
                case Mnemonic.HEAPGET:
                case Mnemonic.HEAPSET:
                    return 1;

                case Mnemonic.MOV:
                case Mnemonic.CMP:
                case Mnemonic.ADD:
                case Mnemonic.SUB:
                case Mnemonic.IMUL:
                case Mnemonic.IDIV:
                    return 2;

                case Mnemonic.PUSHI:
                case Mnemonic.PUSHS:
                case Mnemonic.PUSHF:
                case Mnemonic.JMP:
                case Mnemonic.JE:
                case Mnemonic.JNE:
                case Mnemonic.CALL:
                case Mnemonic.GWST_LIB:
                case Mnemonic.GWST_MAG:
                case Mnemonic.GWST_SMAG:
                case Mnemonic.GWST_UI:
                case Mnemonic.GWST_MEPH:
                case Mnemonic.GWST_WAMAG:
                case Mnemonic.SYSTEM:
                case Mnemonic.DEBUG_LOG:
                    return 4;

                case Mnemonic.MOVI:
                case Mnemonic.ADDI:
                case Mnemonic.SUBI:
                case Mnemonic.IMULI:
                case Mnemonic.IDIVI:

                case Mnemonic.MOVF:
                case Mnemonic.ADDF:
                case Mnemonic.SUBF:
                case Mnemonic.IMULF:
                case Mnemonic.IDIVF:

                case Mnemonic.MOVS:
                case Mnemonic.ADDS:
                case Mnemonic.DEBUG_PUSH:
                    return 5;

                case Mnemonic.MEMCOPY:
                    return 6;

                default:
                    // ※エラー
                    // 定義抜けているので記載する
                    //Debug.LogError($"GetOperandByteの定義抜け。::{mnemonic.ToString()}");
                    return 0xFF;
            }
        }

        static private readonly NodeKind[] ARG_NONE = { NodeKind.Other };
        static private readonly NodeKind[] ARG_INT = { NodeKind.INTEGER, };
        static private readonly NodeKind[] ARG_INT_INT = { NodeKind.INTEGER, NodeKind.INTEGER, };
        static private readonly NodeKind[] ARG_INT_INT_INT_INT = { NodeKind.INTEGER, NodeKind.INTEGER, NodeKind.INTEGER, NodeKind.INTEGER, };
        static private readonly NodeKind[] ARG_INT_FLOAT_ADDR_INT = { NodeKind.INTEGER, NodeKind.FLOAT, NodeKind.ADDR, NodeKind.INTEGER };
        static private readonly NodeKind[] ARG_STRING = { NodeKind.STRING };
        static private readonly NodeKind[] ARG_STRING_INT = { NodeKind.STRING, NodeKind.INTEGER };
        static private readonly NodeKind[] ARG_FLOAT = { NodeKind.FLOAT, };
        static private readonly NodeKind[] ARG_FLOAT3 = { NodeKind.FLOAT, NodeKind.FLOAT, NodeKind.FLOAT };
        static private readonly NodeKind[] ARG_ADDR = { NodeKind.ADDR };
        static private readonly NodeKind[] ARG_INT_ADDR = { NodeKind.INTEGER, NodeKind.ADDR };
        static private readonly (int gwstcall, VariableKind RetType, NodeKind[] ArgKinds)[] GWSTCallInfoLib =
        {
            ((int)GWSTCallLib.ChangeMagicConfig,    VariableKind.VOID, ARG_STRING_INT),
            ((int)GWSTCallLib.GetDeltaTime,         VariableKind.FLOAT,ARG_NONE),
            ((int)GWSTCallLib.GetCharState,         VariableKind.VOID, ARG_ADDR),
            ((int)GWSTCallLib.GetTargets,           VariableKind.INT,  ARG_INT_FLOAT_ADDR_INT),
            ((int)GWSTCallLib.InputKey,             VariableKind.INT,  ARG_INT_INT),
            ((int)GWSTCallLib.ManaConversion,       VariableKind.VOID, ARG_INT_INT),
            ((int)GWSTCallLib.GetManaStaysNum,      VariableKind.INT,  ARG_NONE),
            ((int)GWSTCallLib.GetManaStaysType,     VariableKind.INT,  ARG_NONE),
            ((int)GWSTCallLib.SendMana,             VariableKind.VOID, ARG_INT_INT_INT_INT),
            ((int)GWSTCallLib.ConfirmMana,          VariableKind.INT,  ARG_INT),
            ((int)GWSTCallLib.SetMagicSetMyState,   VariableKind.VOID, ARG_INT),
            ((int)GWSTCallLib.GetMagicSetState,     VariableKind.INT,  ARG_STRING),
            ((int)GWSTCallLib.SetManaLileState,     VariableKind.VOID, ARG_INT_INT),
            ((int)GWSTCallLib.GetManaLileState,     VariableKind.INT,  ARG_INT_INT),

        };
        static private readonly (int gwstcall, VariableKind RetType, NodeKind[] ArgKinds)[] GWSTCallInfoMag =
        {
            ((int)GWSTCallMag.Entity,           VariableKind.VOID, ARG_NONE),
            ((int)GWSTCallMag.GetEntityId,      VariableKind.VOID, ARG_ADDR),
            ((int)GWSTCallMag.SpecifyEntity,    VariableKind.VOID, ARG_ADDR),
            ((int)GWSTCallMag.SetAttribute,     VariableKind.VOID, ARG_INT),
            ((int)GWSTCallMag.SetDestroyTime,   VariableKind.VOID, ARG_FLOAT),
            ((int)GWSTCallMag.SetScale,         VariableKind.VOID, ARG_FLOAT3),
            ((int)GWSTCallMag.SetPosition,      VariableKind.VOID, ARG_FLOAT3),
            ((int)GWSTCallMag.GetPoint,         VariableKind.VOID, ARG_INT_ADDR),
            ((int)GWSTCallMag.SetForce,         VariableKind.VOID, ARG_FLOAT3),
            ((int)GWSTCallMag.GetReticleFoce,   VariableKind.VOID, ARG_ADDR),
            ((int)GWSTCallMag.SetManaUsePoint,  VariableKind.VOID, ARG_INT),
        };
        static private readonly (int gwstcall, VariableKind RetType, NodeKind[] ArgKinds)[] GWSTCallInfoSMag =
        {
            ((int)GWSTCallSMag.SetShape,         VariableKind.VOID,  ARG_INT),
            ((int)GWSTCallSMag.SetContact,       VariableKind.VOID,  ARG_INT),
            ((int)GWSTCallSMag.SetContactReaction,VariableKind.VOID, ARG_INT),
        };
        static private readonly (int gwstcall, VariableKind RetType, NodeKind[] ArgKinds)[] GWSTCallInfoUI =
        {
            ((int)GWSTCallUi.CreateUi, VariableKind.VOID, ARG_ADDR),
            ((int)GWSTCallUi.UpdateUi, VariableKind.VOID, ARG_ADDR),
            ((int)GWSTCallUi.DeleteUi, VariableKind.VOID, ARG_ADDR),
        };
        static private readonly (int gwstcall, VariableKind RetType, NodeKind[] ArgKinds)[] GWSTCallInfoMeph =
        {
            ((int)GWSTCallMeph.Reflect,          VariableKind.VOID, ARG_NONE),
            ((int)GWSTCallMeph.SetForce,         VariableKind.VOID, ARG_FLOAT3),
            ((int)GWSTCallMeph.SetAntigravity,   VariableKind.VOID, ARG_INT),
        };
        static private readonly (int gwstcall, VariableKind RetType, NodeKind[] ArgKinds)[] GWSTCallInfoWAMag =
        {
            ((int)GWSTCallWAMag.SetDisChargeType,  VariableKind.VOID, ARG_INT),
            ((int)GWSTCallWAMag.SetEulerAngle,     VariableKind.VOID, ARG_FLOAT3),
        };
        static private readonly (SystemKind syscall, VariableKind RetType, NodeKind[] ArgKinds)[] SYSCallInfo =
        {
            (SystemKind.INT_TO_STRING,   VariableKind.STRING, ARG_INT),
            (SystemKind.FLOAT_TO_STRING, VariableKind.STRING, ARG_FLOAT),
            (SystemKind.STRING_TO_INT,   VariableKind.INT, ARG_STRING),
        };
        /// <summary>
        /// ref引数の時のrefの形式 
        /// </summary>
        static private readonly VariableKind[] ARG_ENTITIYID = {
            VariableKind.INT, VariableKind.INT, VariableKind.INT, VariableKind.INT,
        };
        static private readonly VariableKind[] ARG_CHARSTATE = {
            VariableKind.FLOAT,VariableKind.FLOAT,VariableKind.FLOAT,VariableKind.FLOAT,VariableKind.FLOAT,
            VariableKind.FLOAT,VariableKind.FLOAT,VariableKind.FLOAT,VariableKind.FLOAT,VariableKind.FLOAT,
        };
        static private readonly VariableKind[] ARG_GETTARGETS ={
            VariableKind.INT,
        };
        static private readonly VariableKind[] ARG_UI_INFO = {
            VariableKind.INT, VariableKind.INT, VariableKind.INT, VariableKind.INT, VariableKind.INT,
            VariableKind.INT, VariableKind.INT, VariableKind.INT, VariableKind.INT, VariableKind.INT,
            VariableKind.FLOAT, VariableKind.FLOAT, VariableKind.FLOAT, VariableKind.FLOAT, VariableKind.FLOAT, VariableKind.FLOAT,
            VariableKind.STRING,  VariableKind.INT,
        };
        static private readonly VariableKind[] ARG_GET_POSITION = {
            VariableKind.FLOAT, VariableKind.FLOAT, VariableKind.FLOAT
        };
        static private readonly VariableKind[] ARG_RECTICLEFORCE = {
            VariableKind.FLOAT, VariableKind.FLOAT, VariableKind.FLOAT
        };
        /// <summary>
        /// 各GWSTのrefがある呼び出し達 
        /// </summary>
        static private readonly (GWSTCallLib GwstCall, VariableKind[] Kind)[] GWSTCallRefArgInfoLib =
        {
            (GWSTCallLib.GetCharState, ARG_CHARSTATE),
            (GWSTCallLib.GetTargets, ARG_GETTARGETS),
        };
        static private readonly (GWSTCallMag GwstCall, VariableKind[] Kind)[] GWSTCallRefArgInfoMag =
        {
            (GWSTCallMag.GetEntityId, ARG_ENTITIYID),
            (GWSTCallMag.SpecifyEntity, ARG_ENTITIYID),
            (GWSTCallMag.GetPoint, ARG_GET_POSITION),
            (GWSTCallMag.GetReticleFoce, ARG_RECTICLEFORCE),
        };
        static private readonly (GWSTCallSMag GwstCall, VariableKind[] Kind)[] GWSTCallRefArgInfoSMag =
        {
        };
        static private readonly (GWSTCallUi GwstCall, VariableKind[] Kind)[] GWSTCallRefArgInfoUi =
        {
            (GWSTCallUi.CreateUi, ARG_UI_INFO),
            (GWSTCallUi.UpdateUi, ARG_UI_INFO),
            (GWSTCallUi.DeleteUi, ARG_UI_INFO),
        };
        static private readonly (GWSTCallMeph GwstCall, VariableKind[] Kind)[] GWSTCallRefArgInfoMeph =
        {
        };
        static private readonly (GWSTCallWAMag GwstCall, VariableKind[] Kind)[] GWSTCallRefArgInfoWAMag =
        {
        };

        /// <summary>
        /// GWSTの呼び出し形式の取得
        /// </summary>
        /// <param name="tk">gwstの種類</param>
        /// <param name="gwstNo">gwstの番号</param>
        /// <returns>引数の数, 戻り値型, 引数の種類</returns>
        static public (int ArgNum, VariableKind RetType, NodeKind[] ArgKinds) GWSTInfo(GWSTType tk, int gwstNo)
        {
            int num = 0;
            (int gwstcall, VariableKind RetType, NodeKind[] ArgKinds)[] infos = null;
            switch (tk)
            {
                case GWSTType.gwst_lib:
                    {
                        num = (int)GWSTCallLib.Num;
                        infos = GWSTCallInfoLib;
                    }
                    break;
                case GWSTType.gwst_mag:
                    {
                        num = (int)GWSTCallMag.Num;
                        infos = GWSTCallInfoMag;
                    }
                    break;
                case GWSTType.gwst_smag:
                    {
                        if (0 <= gwstNo && gwstNo < (int)GWSTCallMag.Num)
                        {
                            // Magと共有部分はMagの情報に任せる
                            num = (int)GWSTCallMag.Num;
                            infos = GWSTCallInfoMag;
                        }
                        else
                        {
                            num = (int)GWSTCallSMag.Num;
                            infos = GWSTCallInfoSMag;
                        }
                    }
                    break;
                case GWSTType.gwst_ui:
                    {
                        num = (int)GWSTCallUi.Num;
                        infos = GWSTCallInfoUI;
                    }
                    break;
                case GWSTType.gwst_meph:
                    {
                        num = (int)GWSTCallMeph.Num;
                        infos = GWSTCallInfoMeph;
                    }
                    break;
                case GWSTType.gwst_wamag:
                    {
                        if (0 <= gwstNo && gwstNo < (int)GWSTCallMag.Num)
                        {
                            // Magと共有部分はMagの情報に任せる
                            num = (int)GWSTCallMag.Num;
                            infos = GWSTCallInfoMag;
                        }
                        else
                        {
                            num = (int)GWSTCallWAMag.Num;
                            infos = GWSTCallInfoWAMag;
                        }
                    }
                    break;

                    // TODO GWST_CALL
            }

            // 指定タイプの範囲内にいるか
            if (0 <= gwstNo && gwstNo < num)
            {
                foreach (var info in infos)
                {
                    // 指定番号の物があればその情報を
                    if (info.gwstcall == gwstNo)
                    {
                        return (info.ArgKinds.Length, info.RetType, info.ArgKinds);
                    }
                }
            }
            return (0, VariableKind.INVALID, null);
        }

        /// <summary>
        /// SYSの呼び出し形式の取得
        /// </summary>
        /// <param name="kind">sysのタイプ</param>
        /// <returns>引数の数, 戻り値型, 引数の種類</returns>
        static public (int ArgNum, VariableKind RetType, NodeKind[] ArgKinds) SYSInfo(SystemKind kind)
        {
            foreach (var info in SYSCallInfo)
            {
                if (info.syscall == kind)
                {
                    return (info.ArgKinds.Length, info.RetType, info.ArgKinds);
                }
            }
            return (0, VariableKind.INVALID, null);
        }

        /// <summary>
        /// 各GWSTの機能数取得
        /// </summary>
        /// <param name="type">gwstタイプ</param>
        /// <returns>機能数の取得</returns>
        static public int GWSTInfoLength(GWSTType type)
        {
            switch (type)
            {
                case GWSTType.gwst_lib:
                    return (int)GWSTCallLib.Num;

                case GWSTType.gwst_mag:
                    return GWSTCallInfoMag.Length;

                case GWSTType.gwst_smag:
                    return (int)GWSTCallMag.Num;

                case GWSTType.gwst_ui:
                    return (int)GWSTCallUi.Num;

                case GWSTType.gwst_meph:
                    return (int)GWSTCallMeph.Num;

                case GWSTType.gwst_wamag:
                    return (int)GWSTCallWAMag.Num;

                    // TODO GWST_CALL
            }
            return 0;
        }

        /// <summary>
        /// ref時の引数の方取得
        /// </summary>
        /// <param name="type">gwstタイプ</param>
        /// <param name="no">番号</param>
        /// <returns>引数</returns>
        static public VariableKind[] GwstCallRefArgInfo(GWSTType type, int no)
        {
            // 共有部分
            switch (type)
            {

                case GWSTType.gwst_mag:
                case GWSTType.gwst_smag:
                case GWSTType.gwst_wamag:
                    GWSTCallMag magNo = (GWSTCallMag)no;
                    foreach (var mag in GWSTCallRefArgInfoMag)
                    {
                        if (mag.GwstCall == magNo)
                        {
                            return mag.Kind;
                        }
                    }
                    break;
            }

            // タイプごと
            switch (type)
            {
                case GWSTType.gwst_lib:
                    GWSTCallLib libNo = (GWSTCallLib)no;
                    foreach (var lib in GWSTCallRefArgInfoLib)
                    {
                        if (lib.GwstCall == libNo)
                        {
                            return lib.Kind;
                        }
                    }
                    break;

                case GWSTType.gwst_smag:
                    GWSTCallSMag smagNo = (GWSTCallSMag)no;
                    foreach (var smag in GWSTCallRefArgInfoSMag)
                    {
                        if (smag.GwstCall == smagNo)
                        {
                            return smag.Kind;
                        }
                    }
                    break;

                case GWSTType.gwst_ui:
                    GWSTCallUi uiNo = (GWSTCallUi)no;
                    foreach (var ui in GWSTCallRefArgInfoUi)
                    {
                        if (ui.GwstCall == uiNo)
                        {
                            return ui.Kind;
                        }
                    }
                    break;

                case GWSTType.gwst_meph:
                    GWSTCallMeph mephNo = (GWSTCallMeph)no;
                    foreach (var meph in GWSTCallRefArgInfoMeph)
                    {
                        if (meph.GwstCall == mephNo)
                        {
                            return meph.Kind;
                        }
                    }
                    break;

                case GWSTType.gwst_wamag:
                    GWSTCallWAMag wamagNo = (GWSTCallWAMag)no;
                    foreach (var wamag in GWSTCallRefArgInfoWAMag)
                    {
                        if (wamag.GwstCall == wamagNo)
                        {
                            return wamag.Kind;
                        }
                    }
                    break;
            }
            return null;
        }

        /// <summary>
        /// ref時の引数の方取得
        /// </summary>
        /// <param name="kind">sysタイプ</param>
        /// <returns>引数</returns>
        static public VariableKind[] SysCallRefArgInfo(SystemKind kind)
        {
            return null;    // refを引数にするのがnull返す
        }


        /// <summary>
        /// ref時の引数を文字列変換する
        /// </summary>
        /// <param name="refargs">ref時の引数</param>
        /// <returns>文字列</returns>
        static public string GwstRefArgToString(VariableKind[] refargs)
        {
            string ret = "{\n";
            foreach (var vk in refargs)
            {
                ret += vk.ToString() + "\n";
            }
            ret += "}";
            return ret;
        }

        /// <summary>
        /// 変数タイプかチェック
        /// </summary>
        /// <param name="nodeKind">ノードの種類</param>
        /// <returns>true:変数タイプ</returns>
        static public bool IsVariableKind(NodeKind nodeKind)
        {
            switch (nodeKind)
            {
                case NodeKind.GVAL:
                case NodeKind.LVAL:

                case NodeKind.GVAL_REFERENCE:
                case NodeKind.LVAL_REFERENCE:

                case NodeKind.GVAL_DEF:
                case NodeKind.LVAL_DEF:

                case NodeKind.GVAL_DEREFE:
                case NodeKind.LVAL_DEREFE:
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 有効な戻り値型かチェック
        /// </summary>
        /// <param name="vk">変数タイプ</param>
        /// <returns>true:有効な戻り値型</returns>
        static public bool IsValidRetType(VariableKind vk)
        {
            return (vk != VariableKind.REFERENCE && vk != VariableKind.ARRAY && vk != VariableKind.STRUCT && vk != VariableKind.BOXS);
        }

        /// <summary>
        /// ある文字列のソース内行番号と行文字を取得
        /// </summary>
        /// <param name="source">ソース</param>
        /// <param name="strIdx">ある文字開始位置</param>
        /// <param name="strLen">ある文字の文字数</param>
        /// <returns>行文字, 行番号</returns>
        static public (string linestr, int lineNo) GetaSourceLineStrNo(byte[] source, int strIdx, int strLen)
        {
            //string sourceStr = Encoding.UTF8.GetString(source);
            int lastIdx = source.Length - 1;
            int lineStart = Math.Min(strIdx, lastIdx);
            int lineEnd = Math.Min(strIdx + strLen, lastIdx);
            int line = 1;
            while (lineStart > 0 && source[lineStart] != 0x0a && source[lineStart] != 0x0d)
            {
                lineStart--;
            }
            while (lineEnd < source.Length - 1 && source[lineEnd] != 0x0a && source[lineEnd] != 0x0d)
            {
                lineEnd++;
            }
            for (int i = 0; i < strIdx && i < source.Length; ++i)
            {
                byte c = source[i];
                if (0x0a == c || 0x0d == c)
                {
                    if (c == 0x0d && i + 1 < source.Length && source[i + 1] == 0x0a)
                    {
                        // "\r\n" の場合
                        i++;
                    }
                    line++;
                }
            }
            lineStart = source[lineStart] == 0x0a || source[lineStart] == 0x0d ? lineStart + 1 : lineStart;
            lineEnd = source[lineEnd] == 0x0a || source[lineEnd] == 0x0d ? lineEnd - 1 : lineEnd;
            string linestr = Encoding.UTF8.GetString(source, lineStart, lineEnd - lineStart + 1);
            return (linestr, line);
        }
    }
}
