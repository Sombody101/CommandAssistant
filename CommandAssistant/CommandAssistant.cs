using Spectre.Console;
using System.Reflection;

namespace CommandAssistant;

[AttributeUsage(AttributeTargets.Field)]
public class CommandArgAttribute : Attribute
{
    public string Switch { get; }
    public string Description { get; }
    public string StaticArgumentHandlerMethodName { get; }
    public int ValuesAfter { get; }
    public Type? ArgumentType { get; }

    /// <summary>
    /// Provides argument helper services to a field or property.
    /// </summary>
    /// <param name="switchLabel"></param>
    /// <param name="description"></param>
    /// <param name="argumentHandler"></param>
    /// <exception cref="InvalidAttributeParameterException"></exception>
    public CommandArgAttribute(string switchLabel, string description, string argumentHandler, int valuesAfter = -1, Type? argumentType = null)
    {
        if (switchLabel is "" or null)
            throw new InvalidAttributeParameterException("Switch label must have a value (--long-hand/-s(horthand))");

        CommandHelp.SplitString(switchLabel, out string longS, out string shortS);

        // Check if the switch has already been added to the list (CommandHelp)
        if (CommandHelp.GetHelpMessage(longS, out _, out _))
            throw new InvalidAttributeParameterException($"'{longS}' is already being used");
        if (CommandHelp.GetHelpMessage(shortS, out _, out _))
            throw new InvalidAttributeParameterException($"'{shortS}' is already being used");

        if (description is "" or null)
            throw new InvalidAttributeParameterException("Switch description must have a value");

        if (argumentHandler is "" or null)
            throw new InvalidAttributeParameterException("Switch argument handler must have a value");

        Switch = switchLabel;
        Description = description;
        StaticArgumentHandlerMethodName = argumentHandler;
        ValuesAfter = valuesAfter;

        //if (ArgumentType is not null && !ArgumentType.IsArray)
        //    throw new ArgumentException("Only arrays or no input parameters are supported as of right now");
        ArgumentType = argumentType;

        CommandHelp.SwitchInformation.Add(switchLabel, description);
    }
}

internal class ArgStorage
{
    internal ArgStorage(MethodInfo method, Type? type, string[] args, string forArg)
    {
        Method = method;
        Type = type;
        Args = args;
        ForArg = forArg;
    }

    internal MethodInfo Method { get; private set; }
    internal Type? Type { get; private set; }
    internal string[] Args { get; private set; }
    internal string ForArg { get; private set; }
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

    internal static string[] _processArguments(string[] a, Type T)
    {
        RefreshActiveFields(T);

        List<string> args = a.ToList();

        if (args.Count is 0 || args[0] is "-h" or "--help")
        {
            CommandHelp.GetHelpInfo(a);

            if (ArgConfig.QuitAfterPrintingHelp)
                Environment.Exit(0);
            return a;
        }

        List<string> unknownSwitches = new();
        List<ArgStorage> methodsToInvoke = new();

        int i = 0;
        void RemArg(int count = 1)
        {
            if (count is 0)
                return;

            for (; count > 0; count--)
                args.RemoveAt(i);
            //i--;
        }

        // Start parsing the arguments in args
        for (; i < args.Count; i++)
        {
            string arg = args[i];
            if (arg is "--")
                break;

            if (!arg.StartsWith('-'))
                continue;

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

                // Get the specified number of arguments from args (-1 means disabled) (might add -2 for "continue till next switch")
                if (i + attr.ValuesAfter + 1 > args.Count)
                    DllUtils.Log($"Insufficient argument count for switch '{arg}'", 3);

                string[] readyArgs = Array.Empty<string>();
                if (attr.ValuesAfter is not -1)
                {
                    a = args.ToArray();
                    if (attr.ValuesAfter is -2)
                    {
                        List<string> tmp = new();
                        for (int t = i; t < i; t++)
                        {
                            if (!args[x].StartsWith('-')) 
                                tmp.Add(args[x]);
                        }
                        readyArgs = tmp.ToArray();
                    }
                    else
                        readyArgs = a[(i + 1)..(i + attr.ValuesAfter + 1)];
                    RemArg(readyArgs.Length);
                }

                methodsToInvoke.Add(new(DllUtils.GetMethod(T, attr.StaticArgumentHandlerMethodName), attr.ArgumentType, readyArgs, arg));
                break;
            }
            RemArg();
        }

        if (unknownSwitches.Count is > 0)
        {
            foreach (string str in unknownSwitches)
                DllUtils.WriteLine($"Unknown switch '[red]{str}[/]'");
            if (ArgConfig.QuitOnError)
                Environment.Exit(1);
            return args.ToArray();
        }

        // Method invocation and type handling
        foreach (var AS in methodsToInvoke)
        {
            if (AS.Type is null)
                _ = AS.Method.Invoke(null, null);
            else
            {
                // Arrays
                if (AS.Type.IsArray)
                    if (AS.Type.IsAssignableFrom(AS.Type))
                    {
                        try
                        {
                            Array typeCastedArray = Array.CreateInstance(AS.Type, AS.Args.Length);
                            Array.Copy(AS.Args, typeCastedArray, AS.Args.Length);
                            AS.Method.Invoke(null, new object[] { typeCastedArray });
                        }
                        catch
                        {
                            List<object> obj = new();
                            foreach (var item in DllUtils.CastObjects(AS))
                                obj.Add(item);

                            AS.Method.Invoke(null, obj.ToArray());
                        }
                    }
                    else
                        AS.Method.Invoke(null, new object[] { DllUtils.CastObjects(AS) });
                // Non-arrays
                else
                    foreach (string str in AS.Args)
                        if (AS.Type.IsAssignableFrom(str.GetType()))
                            AS.Method.Invoke(null, new object[] { Convert.ChangeType(str, AS.Type) });
                        else
                            AS.Method.Invoke(null, new object[] { DllUtils.CastObject(AS) });
            }
        }

        return args.ToArray();
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
                throw new MissingSwitchHandlerException($"Failed to find CommandArgAttribute in {attr[i].Name}"));
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
            throw new ArgumentException("A switch specifier should only have one '/' to specify the long and shorthand switches (--long-hand/-s(horthand))");

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

        bool showAll = args.Length is < 2;

        List<string> unknownArgs = new();

        void TryPrintSwitchInfo(string arg)
        {
            if (GetHelpMessage(arg, out string description, out string fullKey))
                DllUtils.WriteLine($"\t[red]{fullKey}[/]: {description}");
            else
                unknownArgs.Add(arg);
        }

        Console.WriteLine(HelpMessage);
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
            DllUtils.WriteLine($"Unknown switch '[red]{unknown}[/]'");
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

    public static void WriteLine(string line)
    {
        if (ArgConfig.PrintWithColor)
            AnsiConsole.MarkupLine(line);
        else
            Console.WriteLine(line.EscapeMarkup());
    }

    internal static object CastObject(ArgStorage AS)
        => _CastObject(AS.Args[0], AS.Type, AS.ForArg);

    internal static object[] CastObjects(ArgStorage AS)
    {
        List<object> obs = new();
        for (int i = 0; i < AS.Args.Length; i++)
        {
            obs.Add(_CastObject(AS.Args[i], AS.Type, AS.ForArg));
        }

        return obs.ToArray();
    }

    private static object _CastObject(string a, Type t, string forArg)
    {
        object output = new();

        Type? T = t.IsArray ? t.GetElementType() : t;

        if (T == typeof(long))
        {
            if (long.TryParse(a, out long l))
                output = l;
            else
                Log($"Malformed long input for '[red]{forArg}[/]'");
        }
        else if (T == typeof(ulong))
        {
            if (ulong.TryParse(a, out ulong ul))
                output = ul;
            else
                Log($"Malformed ulong input for '[red]{forArg}[/]'");
        }
        else if (T == typeof(int))
        {
            if (int.TryParse(a, out int i))
                output = i;
            else
                Log($"Malformed int input for '[red]{forArg}[/]'");
        }
        else if (T == typeof(uint))
        {
            if (int.TryParse(a, out int ui))
                output = ui;
            else
                Log($"Malformed uint input for '[red]{forArg}[/]'");
        }
        else if (T == typeof(short))
        {
            if (int.TryParse(a, out int s))
                output = s;
            else
                Log($"Malformed uint input for '[red]{forArg}[/]'");
        }
        else if (T == typeof(ushort))
        {
            if (int.TryParse((string)a, out int us))
                output = us;
            else
                Log($"Malformed uint input for '[red]{forArg}[/]'");
        }
        else if (T == typeof(byte))
        {
            if (int.TryParse(a, out int b))
                output = b;
            else
                Log($"Malformed uint input for '[red]{forArg}[/]'");
        }
        else
            throw new UnsuportedDataTypeException($"Invalid data type ({T})");

        return output;
    }
}

public static class ArgConfig
{
    private static readonly string AppName = Assembly.GetEntryAssembly()?.GetName().Name ?? "app";

    /// <summary>
    /// Specifies for the app to exit after printing help information on a command/switch
    /// </summary>
    public static bool QuitAfterPrintingHelp = true;

    /// <summary>
    /// Specifies for the app to exit after an error
    /// </summary>
    public static bool QuitOnError = true;

    /// <summary>
    /// Specifies for the DLL to print with color (Via Spectre.Console | Otherwise uses System.Console)
    /// </summary>
    public static bool PrintWithColor = true;

    /// <summary>
    /// Specifies for the app to allow the same arg multiple times (It will process and run the handler method each time the arg is used)
    /// </summary>
    public static bool AllowMultiArg = true;

    // Meant to be overridden in the application (Allows for different error messages while keeping the input arguments)
    public static Action<string, byte, bool, bool> LogFunctionOverride { get; set; } = DefaultLogFunction;

    // The default function to be used when logging
    private static void DefaultLogFunction(string message, byte severity, bool logAndExit, bool showHelpOnCrash)
    {
        DllUtils.WriteLine($"{AppName}: [" +
            $"{(severity is 1 ? "white" : (severity is 2 ? "yellow" : "red"))}" +
            $"]{(severity is 1 ? "message" : (severity is 2 ? "warning" : "fatal"))}[/]: " +
            $"{message}");

        if (severity >= 3)
            Environment.Exit(severity);
    }
}