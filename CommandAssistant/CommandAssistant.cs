using Spectre.Console;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;

namespace CommandAssistant;

[AttributeUsage(AttributeTargets.Field)]
public class CommandArgAttribute : Attribute
{
    public string Switch { get; }
    public string Description { get; }
    public string StaticArgumentHandlerMethodName { get; }
    public int ValuesAfter { get; }

    /// <summary>
    /// Provides argument helper services to a field or property.
    /// </summary>
    /// <param name="switchLabel"></param>
    /// <param name="description"></param>
    /// <param name="argumentHandler"></param>
    /// <exception cref="ArgumentException"></exception>
    public CommandArgAttribute(string switchLabel, string description, string argumentHandler, int valuesAfter = -1)
    {
        if (switchLabel is "" or null)
            throw new ArgumentException("Switch label must have a value (--long-name/-s)");

        CommandHelp.SplitString(switchLabel, out string longS, out string shortS);

        // Check if the switch has already been added to the list (CommandHelp)
        if (CommandHelp.GetHelpMessage(longS, out _, out _))
            throw new ArgumentException($"'{longS}' is already being used");
        if (CommandHelp.GetHelpMessage(shortS, out _, out _))
            throw new ArgumentException($"'{shortS}' is already being used");

        if (description is "" or null)
            throw new ArgumentException("Switch description must have a value");

        if (argumentHandler is "" or null)
            throw new ArgumentException("Switch argument handler must have a value");

        Switch = switchLabel;
        Description = description;
        StaticArgumentHandlerMethodName = argumentHandler;
        ValuesAfter = valuesAfter;

        CommandHelp.SwitchInformation.Add(switchLabel, description);
    }
}

public static class ArgumentProcessor
{
    internal static List<CommandArgAttribute> ActiveFields = new();

    /// <summary>
    /// Used when the class with argument switches is static
    /// </summary>
    /// <param name="args"></param>
    /// <param name="T"></param>
    /// <returns></returns>
    public static string[] ProcessArguments(string[] args, Type T)
        => _processArguments(args, T);

    /// <summary>
    /// Used when the class with argument switches is non static
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="args"></param>
    /// <returns></returns>
    public static string[] ProcessArguments<T>(string[] args)
        => _processArguments(args, typeof(T));

    internal static string[] _processArguments(string[] args, Type T)
    {
        RefreshActiveFields(T);

        if (args.Length is 0 || args[0] is "-h" or "--help")
        {
            CommandHelp.GetHelpInfo(args);

            if (ArgConfig.QuitAfterPrintingHelp)
                Environment.Exit(0);
            return args;
        }

        List<string> unknownSwitches = new();
        List<KeyValuePair<MethodInfo, string[]>> methodsToInvoke = new();

        int i = 0;
        void RemArg()
        {
            if (args is null || i is < 0 || i >= args.Length)
                return;

            string[] result = new string[args.Length - 1];
            int targetIndex = 0;

            for (int sourceIndex = 0; sourceIndex < args.Length; sourceIndex++)
            {
                if (sourceIndex == i)
                    continue;

                result[targetIndex] = args[sourceIndex];
                targetIndex++;
            }

            args = result;
            i--;
        }

        // Start parsing the arguments in args
        for (; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg is "--")
                break;

            //Console.WriteLine(arg);
            CommandHelp.SwitchExists(arg, out string? swch);
            if (swch is null)
            {
                unknownSwitches.Add(arg);
                continue;
            }

            for (int x = 0; x < ActiveFields.Count; x++)
            {
                var attr = ActiveFields[x];

                if (!attr.Switch.Contains(arg))
                    continue;

                // Get the specified number of arguments from args (-1 means disabled) (might add -2 "for continue till next switch")
                if (i + attr.ValuesAfter + 1 > args.Length)
                    DllUtils.Log($"Insufficient argument count for switch '{arg}'", 3);

                string[] readyArgs = attr.ValuesAfter is not -1 ? args[(i + 1)..(i + attr.ValuesAfter)] : Array.Empty<string>();
                methodsToInvoke.Add(new(DllUtils.GetMethod(T, attr.StaticArgumentHandlerMethodName), readyArgs));
                break;
            }
            RemArg();
        }

        if (unknownSwitches.Count is > 0)
        {
            foreach (string str in unknownSwitches)
                AnsiConsole.MarkupLine($"Unknown switch '[red]{str}[/]'");
            if (ArgConfig.QuitOnError)
                Environment.Exit(1);
            return args;
        }

        foreach (var methodPair in methodsToInvoke)
            methodPair.Key.Invoke(null, new object[] { methodPair.Value });

        return args;
    }

    internal static void RefreshActiveFields(Type type)
    {
        ActiveFields = new();
        var attr = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                   .Where(field => field.IsDefined(typeof(CommandArgAttribute), false))
                   .ToList();

        for (int i = 0; i < attr.Count; i++)
        {
            ActiveFields.Add(attr[i].GetCustomAttribute<CommandArgAttribute>() ??
                throw new NullReferenceException($"Failed to find CommandArgAttribute in {attr[i].Name}"));
        }
    }
}

public static class CommandHelp
{
    internal static string HelpMessage = "Usage: <args>";

    internal static Dictionary<string, string> SwitchInformation = new();

    internal static bool SplitString(string input, out string longS, out string shortS)
    {
        var sKey = input.Split('/');

        if (sKey.Length is > 2)
            throw new ArgumentException("A switch specifier should only have one '/' to specify the long and shorthand switches");

        if (sKey.Length is 1)
        {
            if (sKey[0].StartsWith("--"))
            {
                longS = sKey[0];
                shortS = "";
            }
            else
            {
                shortS = sKey[0];
                longS = "";
            }
            return true;
        }

        var longFirst = sKey[0].StartsWith("--");
        longS = longFirst ? sKey[0] : sKey[1];

        if (sKey.Length is 1)
        {
            shortS = "";
            return false;
        }

        shortS = longFirst ? sKey[1] : sKey[0];
        return true;
    }

    internal static bool SwitchExists(string key, out string? description)
    {
        foreach (var dKey in SwitchInformation.Keys)
        {
            SplitString(dKey, out string longS, out string shortS);
            if (key.StartsWith("--"))
            {
                if (key == longS)
                {
                    GetHelpMessage(key, out description, out description);
                    return true;
                }
            }
            else if (key.StartsWith('-'))
            {
                if (key == shortS)
                {
                    GetHelpMessage(key, out description, out description);
                    return true;
                }
            }
            else
                break;
        }

        description = null;
        return false;
    }

    internal static bool GetHelpMessage(string input, out string output, out string fullKey)
    {
        if (input is not "")
            foreach (var key in SwitchInformation.Keys)
            {
                SplitString(key, out string longS, out string shortS);
                if (longS == input || shortS == input || key == input)
                {
                    output = SwitchInformation[key];
                    fullKey = key;
                    return true;
                }
            }
        output = "";
        fullKey = "";
        return false;
    }

    /// <summary>
    /// Displays help information for each registered switch (Prints all if only one arg and it's "-h" or "--help")
    /// </summary>
    /// <param name="args"></param>
    public static void GetHelpInfo(string[] args)
    {
        SwitchInformation.Add("--help/-h", "Displays this help information (--help/-h <arg(s)>)");
        Console.WriteLine(HelpMessage);

        bool showAll = args.Length is 1;

        List<string> unknownArgs = new();

        void TryPrintSwitchInfo(string arg)
        {
            if (GetHelpMessage(arg, out string description, out string fullKey))
                AnsiConsole.MarkupLine($"\t[red]{fullKey}[/]: {description}");
            else
                unknownArgs.Add(arg);
        }

        if (showAll)
            foreach (string arg in SwitchInformation.Keys)
                TryPrintSwitchInfo(arg);
        else
            for (int i = 1; i < args.Length; i++)
                if (args[i].StartsWith("--"))
                    TryPrintSwitchInfo(args[i]);
                else if (args[i].StartsWith('-'))
                    for (int x = 1; x < args[i].Length; x++)
                        TryPrintSwitchInfo("-" + args[i][x]);

        foreach (string unknown in unknownArgs)
            AnsiConsole.MarkupLine($"Unknown switch '[red]{unknown}[/]'");
    }

    public static void SetHelpMessage(string message)
        => HelpMessage = message;
}

internal static class DllUtils
{
    internal static MethodInfo GetMethod(Type type, string methodName)
        => type.GetMethod(methodName) ?? throw new MissingMethodException($"Failed to find '{methodName}' in class '{type.Name}'");

    // Used for providing runtime or crash information
    public static void Log(string message, byte severity = 1, bool logAndExit = false, bool showHelpOnCrash = false)
    {
        ArgConfig.LogFunctionOverride(message, severity, logAndExit, showHelpOnCrash);
    }
}

public static class ArgConfig
{
    private static readonly string AppName = Assembly.GetEntryAssembly().GetName().Name ?? "app";

    /// <summary>
    /// Specifies for the app to exit after printing help information on a command/switch
    /// </summary>
    public static bool QuitAfterPrintingHelp = true;

    /// <summary>
    /// Specifies for the app to exit after an error
    /// </summary>
    public static bool QuitOnError = true;

    /// <summary>
    /// Specifies for the app to allow the same arg multiple times (It will process and run the handler method each time the arg is used)
    /// </summary>
    public static bool AllowMultiArg = true;

    // Meant to be overridden in the application (Allows for different error messages while keeping the input arguments)
    public static Action<string, byte, bool, bool> LogFunctionOverride { get; set; } = DefaultLogFunction;

    private static void DefaultLogFunction(string message, byte severity, bool logAndExit, bool showHelpOnCrash)
    {
        AnsiConsole.MarkupLine($"{AppName}: [" +
            $"{(severity is 1 ? "white" : (severity is 2 ? "yellow" : "red"))}" +
            $"]{(severity is 1 ? "message" : (severity is 2 ? "warning" : "fatal"))}[/]: " +
            $"{message}");

        if (severity >= 3)
            Environment.Exit(severity);
    }
}