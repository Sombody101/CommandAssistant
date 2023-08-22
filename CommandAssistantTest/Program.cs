using CommandAssistant;

namespace CommandAssistantTest;

internal class Program
{
    //public static ArgsD Args = new();
    static void Main(string[] args)
    {
        // Just a test case
        //args = new string[] { "-h" };
        CommandHelp.SetHelpMessage("Usage: <args> <search path>");
        ArgumentProcessor.ProcessArguments(args, typeof(ArgsD));
    }

    public static class ArgsD
    {
        // Switches
        [CommandArg("--some-str/-s", "This is a switch, for an arg", nameof(LStr_Helper))]
        public static string LStr = string.Empty;

        [CommandArg("-b", "This is a test for only shorthand switches", nameof(LBool_Helper))]
        public static bool LBool = false;

        [CommandArg("--bool-value", "This is a test for only longhand switches", nameof(LBool2_Helper))]
        public static bool LBool2 = false;

        // Command handlers
        public static void LStr_Helper(string[] arg)
        {
            Console.WriteLine("[METHOD] Solving for LStr" + string.Join(", ", arg));
        }

        public static void LBool_Helper(string[] arg)
        {
            Console.WriteLine("[METHOD] Solving for LBool" + string.Join(", ", arg));
        }

        public static void LBool2_Helper(string[] arg)
        {
            Console.WriteLine("[METHOD] Solving for LBool2" + string.Join(", ", arg));
        }
    }
}