namespace MCCompilerConsole.Converter.Compiler
{
    public enum ERROR_TEXT
    {
        ERROR_BASE_FILENAME_LINENO,
        NOT_EXIT_FILE,
        NOT_EXIT_FILE_NAME_INCLUDE,
        NOT_EXIT_FILE_NAME_INIT,
        NOT_EXIT_FILE_NAME_MAIN,
        ST_PREPROCESSOR_ERROR,
        SYMBOL_IS_REQUIRED,
        INVALID_TOKEN,
        DEFFERENT_NUMERIC,
        TYPE_MIN_MAX,
        NO_DEFINE_STRUCT,
        FUNC_NOT_DEFINE,
        STRUCT_NEXT_MYSELF,
        ST_STRUCT_RESIZE,
        FUNC_NOT_SPECIFY_RET_TYPE,
        FUNC_ARG_NOT_SPECIFY_TYPE,
        FUNC_ARG_NOT_SPECIFY_VARIABLE,
        STATEMENT_WRITING_IS_INCORRENCT,
        FUNC_ALREADY_DEFINE,
        VARIABLE_ALREADY_DEFINE,
        NOT_DEFINE_VARIABLE,
        TYPE_DEFINE_FAILD,
        MISTAKE_BY_VARIABLE_DEFINITION,
        DEFINE_NOT_IDENT,
        ST_NOT_DEFINE_FUNC,
        ST_VARIABLE_GLOBAL,
        STATEMENT_WRITING_IS_INCORRECT,
        FUNC_RETURN_VOID,
        FUNC_RETURN_DIFFERENT_TYPE,
        STATEMENT_CASE,
        STATEMENT_DEFAULT,
        VARIABLE_NOT_DEFINE,
        STATEMENT_NOT_STMT,
        STRING_SUB,
        STRING_DIV,
        STRING_MUL,
        STRING_RELATIONAL,
        STRING_UNARY,
        FUNC_ARG_NUM_DIFFERENT,
        FUNC_ARG_NUM_OVER,
        FUNC_ARG_NOT_TYPE,
        FUNC_ARG_SPECIFY_VARIABLE,
        FUNC_ARG_REF_SPECIFY_ADDR,
        FUNC_ARG_REF_DIFFERENT_TYPE,
        NUM_OR_STRING,
        GWST_CALL_TYPE,
        GWST_ALRG_REF_SPECIFY_ADDR,
        GWST_NOT_STMT,
        ST_SYS_CALL_TYPE,
        SYS_NOT_STMT,
        DEBUGLOG_ARG_OVER_LIMT,
        DEBUGLOG_ARG_DIFFERENCE,
        DEBUGLOG_NOT_STMT,
        ARRAY_DS_OVER,
        ARRAY_INDEX,
        STRUCT_NOT_STRUCT,
        STRUCT_NOT_MEMBER,
        ARRAY_DS_NOT_ENOUGH,
        STRUCT_SPECIFY_NOT_ENOUGH,
        ARRAY_INIT,
        STRUCT_INIT,
        REFERENCE_INIT_ADDR,
        REFERENCE_INIT_TYPE,
        VARIABLE_INIT,
        STRUCT_ALREADY_DEFINE,
        STRUCT_ALREADY_DEFINE_MEMBER,
        STRUCT_NOT_DEFINE_VARIABLE_TYPE,
        ENUM_ALREADY_DEFINE,
        ENUM_MEMBER_IS_NUMBER,
        ENUM_ALREADY_DEFINE_MEMBER,
        ENUM_DIFFERENCE_DEFINE,
        INTEGER_ENUM,
        ENUM_DIFFERENCE_SPECIFY,
        ASSIGN_LEFT,
        ASSIGN_TYPE,
        ARRAY_INIT_OVER,
        ARRAY_INIT_ORDER,
        ARRAY_NOT_ENOUGH,
        ARRAY_SIZE_INDEFINITE,
        ARRAY_SIZE_ZERO,
        ARRAY_SIZE_UNUSED_INDEX,
        ARRAY_INIT_ELEMENT_OVER,
        STRUCT_INIT_OVER,
        STRUCT_INIT_ELEMENT_OVER,
        STRUCT_INIT_ELEMENT_SPECIFY,
        NOT_DEFINE_INITIALIZE_FUNC,
        NOT_DEFINE_MAIN_FUNC,
        ST_GENERATE_MNIMONIC,
        ST_GENERATE_INIT,
        BREAK,
        CONTINUE,
        ST_LR_NODE_NULL,
        ST_INVALID_OPERAND,
        ST_LOCAL_STACK,
        LINKER_NOT_INTERMEDIATE_FINE,
        LINKER_FILESIZE_ZERO,
        UNEXPECTED_EXCEPTION,
        SIZEOF,
        BOXS_RELEASE,
        BOXS_TYPE,
        BOXS_NO_TYPE,
        BOXS_ALLOCATE_INTEGER,
        BOXS_ALLOCATE_INIT,
    }

    public class ErrorData : ErrorDataBase
    {
        public ErrorData(string filename) : base(filename)
        {
        }

        public string Str(ERROR_TEXT et)
        {
            return base.Str((int)et, "", "", "", "");
        }
        public string Str(ERROR_TEXT et, string replace)
        {
            return base.Str((int)et, replace, "", "", "");
        }
        public string Str(ERROR_TEXT et, string replace1, string replace2)
        {
            return base.Str((int)et, replace1, replace2, "", "");
        }
        public string Str(ERROR_TEXT et, string replace1, string replace2, string replace3)
        {
            return base.Str((int)et, replace1, replace2, replace3, "");
        }
        public string Str(ERROR_TEXT et, string replace1, string replace2, string replace3, string replace4)
        {
            return base.Str((int)et, replace1, replace2, replace3, replace4);
        }
    }
}
