using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCCompilerConsole.Converter
{
    public enum Mnemonic
    {
        MOV,
        MOVI,
        MOVS,
        MOVF,

        ADD,
        ADDI,
        ADDS,
        ADDF,

        SUB,
        SUBI,
        SUBF,

        IMUL,
        IMULI,
        IMULF,

        IDIV,
        IDIVI,
        IDIVF,

        POP,

        PUSH,       // 指定レジスタをPUSH
        PUSHI,      // 数値をPUSH
        PUSHS,      // 文字をPUSH
        PUSHF,      // 不動小数点をPUSH

        CALL,
        RET,

        JMP,
        JE,
        JNE,

        CMP,

        SETE,
        SETNE,
        SETL,
        SETLE,

        MEMCOPY,

        SAVESPS,
        LOADSPS,

        GWST_LIB = 0xA0,
        GWST_MAG,
        GWST_SMAG,
        GWST_UI,
        GWST_MEPH,
        GWST_WAMAG,
        SYSTEM = 0xB0,

        HEAPALLOCATE = 0xD0,
        HEAPRELEASE,
        HEAPGET,
        HEAPSET,

        DEBUG_LOG = 0xE0,
        DEBUG_RLOG,
        DEBUG_SLOG,
        DEBUG_PUSH,
        DEBUG_PAUSE,

        NOP = 0xFD,
        MAGICEND = 0xFE,
        INVALID = 0xFF,
    }

    public enum Operand
    {
        ZERO_I,
        ZERO_S,
        ZERO_F,

        RSP,            // スタックポインター
        RBP,            // ベースポインター
        RGP,            // グローバル変数用スタックポインター

        RI1,
        RI2,
        RI3,
        RI4,
        RI5,
        RI6,
        RI7,
        RI1P,           // [RI1P]として扱う(RI1をアドレス値として使用する場合)

        RS1,            // ストリング型レジスター
        RS2,
        RS3,
        RS4,
        RS5,
        RS6,

        RF1,            // 不動小数点レジスター
        RF2,
        RF3,
        RF4,
        RF5,
        RF6,

        RHPI,           // ヒープポインタインデックス
        RHPIS,          // ヒープポインタインデックスサブ

        INVALID = 0xff,
    }

    public enum TokenKind
    {
        RESERVED,   // 記号
        IDENT,      // 識別子
        INTEGER,    // 整数、数字
        STRING,     // 文字列
        FLOAT,      // 不動小数点数
        RETURN,     // return
        IF,         // if
        ELSE,       // else
        WHILE,      // while
        FOR,        // for
        SWITCH,     // switch
        CASE,       // case
        DEFAULT,    // default
        TYPE,       // 型(int, float, void, string...
        BOXTYPE,    // ボックスの型(int, float, void, string
        REFERENCE,  // 参照(ref_
        SIZEOF,     // seizeof
        INCLUDE,    // include
        INIT,       // init function
        MAIN,       // maing function
        SKILL,      // skill
        ONETIME,    // onetime
        REPEATE,    // repeate
        STRUCT,     // struct
        ENUM,       // enum
        GWST_LIB,   // システムコール的なもの
        GWST_MAG,   // システムコール的なもの
        GWST_SMAG,  // システムコール的なもの
        GWST_UI,    // システムコール的なもの
        GWST_MEPH,  // システムコール的なもの
        GWST_WAMAG, // システムコール的なもの
        SYSTEM,     // システムコール
        BREAK,      // break
        CONTINUE,   // continue
        ALLOCATE,   // boxs allocaste
        RELEASE,    // boxs release
        EOF,        // 入力の終わり
        DEBUG_LOG,  // デバック用のログ
        DEBUG_PAUSE,// デバック用一時停止

        Other,      // その他　エラー用
    }

    public enum AsmTokenKind
    {
        RESERVED,   // 記号
        MNEMONIC,   // ニーモニック
        OPERAND,    // オペランド
        //IDENT,    // 識別子
        INTEGER,    // 整数、数字
        STRING,     // 文字列
        FLOAT,      // 不動小数点数
        LABEL,      // ラベル


        INCLUDE,    // include
        SKILL,      // skill
        ONETIME,    // onetime
        REPEATE,    // repeate

        EOF,        // 入力の終わり
        Other,      // その他　エラー用
    }

    public enum SystemKind
    {
        INT_TO_STRING,
        FLOAT_TO_STRING,
        STRING_TO_INT,

        INVALID = -1,
    }

    public enum NodeKind
    {
        ADD,            // +
        SUB,            // -
        MUL,            // *
        DIV,            // /
        EQ,             // ==
        NE,             // !=
        LT,             // <
        LTE,            // <=
        ASSIGN,         // =
        GVAL_DEF,       // グローバル変数定義
        LVAL_DEF,       // ローカル変数定義
        GVAL,           // グローバル変数
        LVAL,           // ローカル変数
        HVAL,           // heap変数
        GVAL_REFERENCE, // グローバル変数の参照
        LVAL_REFERENCE, // ローカル変数の参照
        GVAL_DEREFE,    // グルーバル変数のデリファレンス
        LVAL_DEREFE,    // ローカル変数のデリファレンス
        INTEGER,        // 整数
        STRING,         // 文字列
        FLOAT,          // 不動小数点数
        BOXS,           // ボックス
        ALLOCATE,       // ボックス 確保
        RELEASE,        // ボックス 解放
        RETURN,         // return
        IF,             // if
        ELSE,           // else
        WHILE,          // while
        FOR,            // for
        SWITCH,         // switch
        CASE,           // case
        BLOCK,          // { ... }
        FUNC_CALL,      // call function
        FUNC_DEF,       // 関数定義
        DEREFE,         // デリファレンス
        ADDR,           // アドレス
        REF_ADDR,       // ref変数の中身取得
        STMT_EXPR,      // statement内のexpression
        GWST_CALL,      // システムコール的なもの
        SYS_CALL,       // システムコール
        BREAK,          // break
        CONTINUE,       // continue
        DEBUG_LOG,      // debug log
        DEBUG_PAUSE,    // debug pause

        SKILL,          // skill
        ONETIME,        // onetime
        REPEATE,        // repeate

        Other,          // その他　エラー用
    }

    public enum VariableKind
    {
        INT,
        FLOAT,
        STRING,
        VOID,
        REFERENCE,
        ARRAY,
        STRUCT,
        BOXS,

        INVALID,
    }

    public enum Scope
    {
        GLOBAL,
        LOCAL,

        INVALID,
    }

    public enum ReturnExist
    {
        STATEMENT = 0b00000000_00000001,
        IF = 0b00000000_00000010,
        ELSE = 0b00000000_00000100,
        WHILE = 0b00000000_00001000,
        FOR = 0b00000000_00010000,
    }

    public enum DebugType
    {
        NONE,

        GlobalArray,
        LocalArray,
        GlobalStructArray,
        LocalStructArray,

        GlobalVariable,
        LocalVariable,

        GlobalStruct,
        LocalStruct,
    }

    public enum GWSTType
    {
        gwst_lib,
        gwst_mag,
        gwst_smag,
        gwst_ui,
        gwst_meph,
        gwst_wamag,

        Invalid,
    }
    public enum GWSTCallLib
    {
        ChangeMagicConfig,  // コンフィグファイルの切り替え
        GetDeltaTime,       // 経過時間取得(秒)
        GetCharState,       // キャラクターのステート取得
        GetTargets,         // ターゲットの設定
        InputKey,           // キーが押されたら
        GetManaPercent,     // マナの存在量取得
        SetManaPercent,     // マナの存在量移動

        Num
    }
    public enum GWSTCallMag
    {
        Entity,             // 現在の設定状態で魔法を実体化
        GetEntityId,        // 実体化した魔法のID取得
        SpecifyEntity,      // 実体の指定
        SetAttribute,       // 魔法の属性
        SetDestroyTime,     // 魔法消滅時間
        SetPosition,        // 魔法出現位置
        SetScale,           // 魔法のサイズ
        SetForce,           // 魔法への力
        GetPoint,           // 指定位置の取得
        GetReticleFoce,     // レティクル方向の力を取得

        Num
    }
    public enum GWSTCallSMag
    {
        // GWSTCallMagの部分も指定できるので
        // GWSTCallMagの続きから始める
        SetContact = GWSTCallMag.Num,   // 魔法の接触タイプ
        SetContactReaction,             // 魔法接触時の反応
        SetShape,                       // 魔法の形

        // TODO
        //AddMana,            // 魔法にマナを加える
        //GetMana,            // TODO マナの取得
        //ForcusGetMana,      // TODO 集中してマナを取得

        Num
    }
    public enum GWSTCallUi
    {
        CreateUi,           // UIの作成
        UpdateUi,           // TODO UIの更新
        DeleteUi,           // TODO UIの削除

        Num
    }
    public enum GWSTCallMeph
    {
        Reflect,            // 現在の状態を反映
        SetForce,           // 自らの肉体に力を加える
        SetAntigravity,     // 反重力

        Num
    }
    public enum GWSTCallWAMag
    {
        // GWSTCallMagの部分も指定できるので
        // GWSTCallMagの続きから始める
        SetDisChargeType = GWSTCallMag.Num, // 放出タイプを設定
        SetEulerAngle,                      // オイラーアングルの設定

        Num
    }
    public enum StackType
    {
        INT,
        FLOAT,
        STRING,
        BOXS,

        INVALID,
    }

    [Flags]
    public enum MagicConfig
    {
        None = 0,

        // 魔法タイプ
        Magic = 0b0000_0000_0000_0000_0000_0001,
        Skill = 0b0000_0000_0000_0000_0000_0010,
        Type = Magic | Skill,

        // 実行回数
        OneTime = 0b0000_0000_0000_0000_0000_0100,
        Repeate = 0b0000_0000_0000_0000_0000_1000,
        Execution = OneTime | Repeate,

        // 実行情報
        ExecutionInfo = Type | Execution,
    }

    static public class Define
    {
        static public readonly int MagicConfigSize = 1;
        static public readonly string McsEx = ".mcs";
        static public readonly string McasEx = ".mcas";
        static public readonly string McEx = ".mc";
        static public readonly string McbinEx = ".mcbin";

        static public NodeKind ToNodeKind(VariableKind variableKind)
        {
            switch (variableKind)
            {
                case VariableKind.INT: return NodeKind.INTEGER;
                case VariableKind.STRING: return NodeKind.STRING;
                case VariableKind.FLOAT: return NodeKind.FLOAT;
            }
            return NodeKind.Other;
        }
        static public StackType ToStackType(VariableKind variableKind)
        {
            switch (variableKind)
            {
                case VariableKind.INT: return StackType.INT;
                case VariableKind.STRING: return StackType.STRING;
                case VariableKind.FLOAT: return StackType.FLOAT;
            }
            return StackType.INVALID;
        }

        static public VariableKind ToVariableKind(NodeKind nodeKind)
        {
            switch (nodeKind)
            {
                case NodeKind.INTEGER: return VariableKind.INT;
                case NodeKind.STRING: return VariableKind.STRING;
                case NodeKind.FLOAT: return VariableKind.FLOAT;
            }
            return VariableKind.INVALID;
        }
        static public Scope ToScope(NodeKind nodeKind)
        {
            switch (nodeKind)
            {
                case NodeKind.GVAL:
                case NodeKind.GVAL_REFERENCE:
                case NodeKind.GVAL_DEF:
                case NodeKind.GVAL_DEREFE:
                    return Scope.GLOBAL;

                case NodeKind.LVAL:
                case NodeKind.LVAL_REFERENCE:
                case NodeKind.LVAL_DEF:
                case NodeKind.LVAL_DEREFE:
                    return Scope.LOCAL;
            }
            return Scope.INVALID;
        }
        static public StackType ToStackType(NodeKind nodeKind)
        {
            switch (nodeKind)
            {
                case NodeKind.INTEGER: return StackType.INT;
                case NodeKind.STRING: return StackType.STRING;
                case NodeKind.FLOAT: return StackType.FLOAT;
            }
            return StackType.INVALID;
        }
    }
}
