namespace MCCompilerConsole.Converter.Assembler
{
    public enum ERROR_TEXT
    {
        ERROR_BASE_FILENAME_LINENO,
        NOT_EXIT_FILE,
        LINKER_NOT_INTERMEDIATE_FINE,
        FILE_SIZE_OVER,
        INVALID_TOKEN,
        DEFFERENT_NUMERIC,
        TYPE_MIN_MAX,
        ST_PREPROCESSOR_ERROR,
        NOT_EXIT_FILE_NAME_INCLUDE,
        NOT_EXIT_FILE_NAME_INIT,
        NOT_EXIT_FILE_NAME_MAIN,
        ALREADY_DEFINE_LABEL,
        NEXT_TOKEN_STRING,
        ST_INVALID_OPERAND,
        COPY_BYTE,
        NEXT_TOKEN_OPERAND,
        NOT_DEFINE_LABEL,
        SPLID_OPERAND_NUM,
        DEBUG_TYPE,
        MNEMONIC_INT,
        MNEMONIC_FLOAT,
        MNEMONIC_STRING,
        MNEMONIC_OPERAND_INT,
        MNEMONIC_OPERAND_FLOAT,
        MNEMONIC_OPERAND_STRING,
        MNEMONIC_OPERAND_OPERAND_INT,
        MNEMONIC_LABEL,
        MNEMONIC_OPERAND,
        MNEMONIC_OPERAND_OPERAND,
        NOT_DEFINE_MNEMONIC,
        LINKER_FILESIZE_ZERO,
        ST_GENERATE_MNIMONIC,
        UNEXPECTED_EXCEPTION,
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
