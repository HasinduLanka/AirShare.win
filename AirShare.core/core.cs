using System;
using System.Collections.Generic;
using System.Text;

namespace AirShare
{
    public static class Core
    {

        public static string monitor = "";

        private static bool LogBusy = false;
        public static void Log(string s, ConsoleColor color = ConsoleColor.Green)
        {
            WaitFor(ref LogBusy, 5);
            LogBusy = true;

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(TimeStamp + "  ->  ");
            Console.ForegroundColor = color;
            Console.WriteLine(s);
            Console.ForegroundColor = ConsoleColor.Magenta;

            LogBusy = false;
        }


        public static string Prompt(string s, ConsoleColor color = ConsoleColor.Green)
        {
            WaitFor(ref LogBusy, 5);
            LogBusy = true;

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            Console.Write(TimeStamp + "  ->  ");
            Console.ForegroundColor = color;
            Console.WriteLine(s);
            Console.ForegroundColor = ConsoleColor.Magenta;

            LogBusy = false;

            return Console.ReadLine();
        }

        public static string TimeStamp => $"{DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second}.{DateTime.Now.Millisecond}\t";


        public static void LogErr(Exception ex, string note = "")
        {
            Log(ErrorStamp(ex, note) + "\n", ConsoleColor.Red);
        }

        public static string ErrorStamp(Exception ex, string note)
        {
            if (ex == null) return $"\n ---------------------------------- \n \n !!! Fatel Error occured, and it's very bad. That's all I know \n \n ----------------------------------- \n ";
            return $"!!! Error {ex.ToString()} \n {note} \n {ex.Message} \t source : {ex.Source};\t Inner : {ex.InnerException?.ToString()} {ex.InnerException?.Message}; \n @{ex.StackTrace} \n Inner @{ex.InnerException?.StackTrace}";
        }
        public static string VisualizeObj(dynamic obj)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented);
        }

        public static void Sleep(int milies)
        {
            System.Threading.Thread.Sleep(milies);
        }
        public static void WaitFor(ref bool boolean, int checkIntervel = 50)
        {
            while (boolean)
            {
                System.Threading.Thread.Sleep(checkIntervel);
            }
        }
        public static void WaitUntil(ref bool boolean, int checkIntervel = 50)
        {
            while (!boolean)
            {
                System.Threading.Thread.Sleep(checkIntervel);
            }
        }
    }
}
