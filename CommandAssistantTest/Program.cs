// Comment this line out to use a non-static version of the Args class
#define USING_STATIC_ARGS

using CommandAssistant;

namespace CommandAssistantTest;

internal class Program
{
    //public static ArgsD Args = new();
    static void Main(string[] args)
    {
        // A test case (Overrides input arguments)
        //args = new string[] { "-T", "94", "69" };

        // Change the help message that is presented when args information is listed
        CommandHelp.SetHelpMessage("Usage: <args> <search path>");

#if USING_STATIC_ARGS
        ArgumentProcessor.ProcessArguments(args, typeof(Args));
#else
        ArgumentProcessor.ProcessArguments<Args>(args);
#endif
    }

#if USING_STATIC_ARGS
    public static class Args
#else
    public class Args
#endif
    {
        /*
         * Switches should always be static. It may be more difficult to reset them to their default state, but makes them
         * more accessible throughout the program
         *
         * CommandArg:
         *      Switch          : The format of the switch (Should always be set --longhand/-shorthand)
         *      Description     : The description of what the switch does (Can contain newlines, but is best if "\t\t" is placed before them)
         *      Handler         : The name of the *static void* method for handling the argument
         *      *Values after   : The number of arguments after the switch that should be passed to the handler method (-1 = disabled, -2 = until the next switch)
         *      *Argument type  : The parameter type for the handler method
         *  
         *  *  = Experimental
         *  ** = Will not work
         *  
         *  Currently supported argument types:
         *      string, (u)int, (u)long, (u)short, and byte
         *      
         *  Currently supported array types:
         *      string[], (u)int[], (u)long[], (u)short[], and byte[]
         *  (Bool will not be supported as a return type. It would be redundant to have the library just return true when the switch already specifies it)
         */

        [CommandArg("--some-str/-s",                // Switch name
            "This is a switch, for an arg",         // Description
            nameof(LStr_Helper),                    // Handler method
            1,                                      // Arg count
            typeof(string))]                        // Arg type
        public static string LStr = string.Empty;   // Field

        [CommandArg("-b", "This is a test for only shorthand switches", nameof(LBool_Helper))]
        public static bool LBool = false;

        [CommandArg("--bool-value", "This is a test for only longhand switches", nameof(LBool2_Helper))]
        public static bool LBool2 = false;

        [CommandArg("--type-test/-T", "This is a type conversion test", nameof(TypeTest_Helper), 2, typeof(int[]))]
        public static int TypeTest = 0;

        [CommandArg("--arr-test/-a", "A test on array types", nameof(ArrayTest_Handler), 3, typeof(int[]))]
        public static bool ArrayTest = false;

        // Command handlers
        public static void LStr_Helper(string arg)
        {
            Console.WriteLine("[METHOD] Solving for LStr: " + arg);
        }

        public static void LBool_Helper()
        {
            Console.WriteLine("[METHOD] Solving for LBool");
        }

        public static void LBool2_Helper()
        {
            Console.WriteLine("[METHOD] Solving for LBool2");
        }

        public static void TypeTest_Helper(int[] input)
        {
            Console.WriteLine($"[METHOD] Solving for TypeTest: {string.Join(", ", input)}");
        }

        public static void ArrayTest_Handler(int[] input)
        {
            Console.WriteLine($"[METHOD] Solving for ArrayTest: {string.Join(", ", input)}");
            ArrayTest = true;
        }
    }
}