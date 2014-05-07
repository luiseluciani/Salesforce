using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace LogCleaner
{
    class Program
    {
        static void Main(string[] args)
        {
            List<string> flags = new List<string>();
            if (args.Length > 0)
            {
                if (args.Length > 1)
                {
                    for (int i = 1; i < args.Length; i++)
                    {
                        flags.Add(args[i]);
                    }
                }

                StreamReader reader = File.OpenText(args[0]);
                string str = cleanExtraLines(reader);
                str = indentLogLines(str);

                if (!flags.Contains("\\f"))
                {
                    str = removeFinishedStatements(str);
                }

                if (!flags.Contains("\\t"))
                {
                    str = removeTimeStamps(str);
                }

                File.WriteAllText(Directory.GetCurrentDirectory() + "\\CleanLog" + DateTime.Now.ToFileTime().ToString() + ".txt", str);
            }
            else
            {
                Console.WriteLine(" ");
                Console.WriteLine("INCORRECT USAGE. See guidance below.");
                Console.WriteLine(" ");
                Console.WriteLine("USAGE: LogCleaner.exe LogFileName.txt [\\f] [\\t]");
                Console.WriteLine(" ");
                Console.WriteLine("Flags:");
                Console.WriteLine("\\f  :  Use this flag to keep the 'CODE_UNIT_FINISHED' rows");
                Console.WriteLine("\\t  :  Use this flag to keep the time stamps on the resulting file");
            }

        }

        private static string removeFinishedStatements(string str)
        {
            StringBuilder sb = new StringBuilder();
            string line;
            using (StringReader reader = new StringReader(str))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    if (!line.Contains("|CODE_UNIT_FINISHED|"))
                    {
                        sb.AppendLine(line);
                    }
                }
            }
            return sb.ToString();
        }

        private static string removeTimeStamps(string str)
        {
            StringBuilder sb = new StringBuilder();
            string line;
            using (StringReader reader = new StringReader(str))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    string timestamp = line.Split('|')[0];
                    string indent = Regex.Replace(timestamp,"\\S.*","");
                    line = indent + line.Substring(line.IndexOf('|')+1);
                    sb.AppendLine(line);
                }
            }
            return sb.ToString();
        }

        private static string indentLogLines(string str)
        {
            StringBuilder sb = new StringBuilder();
            string line = "", prevLine = "";
            int indentations = 0;
            List<String> endStrings = new List<string>();
            using (StringReader reader = new StringReader(str))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    if(line.Contains("|CODE_UNIT_STARTED|") && prevLine.Contains("|CODE_UNIT_STARTED"))
                    {
                        indentations++;
                        endStrings.Add("CODE_UNIT_FINISHED|"+prevLine.Substring(prevLine.LastIndexOf('|')+1).Split(' ')[0]);
                    }
                    else if(indentations > 0 && line.Contains(endStrings[indentations-1]))
                    {
                        endStrings.RemoveAt(indentations-1);
                        indentations--;
                    }

                    sb.AppendLine(getIndentations(indentations)+line);

                    prevLine = line;
                }
            }
            return sb.ToString();
        }

        private static string getIndentations(int indentations)
        {
            string result = "";
            for (int i = 0; i < indentations; i++)
            {
                result += "\t\t";
            }
            return result;
        }

        private static string cleanExtraLines(StreamReader reader)
        {
            StringBuilder sb = new StringBuilder();
            while (reader.Peek() >= 0)
            {
                string line = reader.ReadLine();
                if (line.Contains("|CODE_UNIT_") || line.Contains("|FATAL_ERROR|"))
                {
                    //Keep it
                    sb.AppendLine(line);
                }
            }
            return sb.ToString();
        }

    }
}
