using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.VisualBasic.FileIO;

namespace MCCompilerConsole.Converter
{
    public enum Language
    {
        JPN,
        ENG,

        NUM,
    }


    public class ErrorDataBase
    {
        public ErrorDataBase(string fileName)
        {
            Lang = Language.JPN;
            Data = new List<string[]>();

            
            string exePath = Environment.GetCommandLineArgs()[0];
            string exeFullPath = Path.GetFullPath(exePath);
            string errorTextFile = Path.GetDirectoryName(exeFullPath) + fileName;

            using (TextFieldParser parser = new TextFieldParser(errorTextFile, System.Text.Encoding.UTF8))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");

                parser.HasFieldsEnclosedInQuotes = true;
                parser.TrimWhiteSpace = false;

                // ファイルの終端までループ
                while (!parser.EndOfData)
                {
                    // フィールドを読込
                    string[] row = parser.ReadFields();
                    if(row.Length != (int)Language.NUM)
                    {
                        return;
                    }
                    Data.Add(row);
                }
            }
        }

        public string Str(int idx)
        {
            return Str(idx, "", "", "", "");
        }
        public string Str(int idx, string replace)
        {
            return Str(idx, replace, "", "", "");
        }
        public string Str(int idx, string replace1, string replace2)
        {
            return Str(idx, replace1, replace2, "", "");
        }
        public string Str(int idx, string replace1, string replace2, string replace3)
        {
            return Str(idx, replace1, replace2, replace3, "");
        }
        public string Str(int idx, string replace1, string replace2, string replace3, string replace4)
        {
            if(idx >= Data.Count)
            {
                return "";
            }
            string ret = Data[idx][(int)Lang];
            ret = ret.Replace(ReplaceStr[0], replace1);
            ret = ret.Replace(ReplaceStr[1], replace2);
            ret = ret.Replace(ReplaceStr[2], replace3);
            ret = ret.Replace(ReplaceStr[3], replace4);
            return ret;
        }

        public Language Lang { get; set; }
        protected List<string[]> Data { get; set; }


        protected static string[] ReplaceStr =
        {
            "<INFO1>",
            "<INFO2>",
            "<INFO3>",
            "<INFO4>",
        };
    }
}