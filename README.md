# CommandAssistant

**CommandAssistant** is a C# library designed to assist with command-line argument parsing and handling in your applications. It allows you to define command-line switches along with their associated descriptions and argument handling methods.

## Features

- Define command-line switches using attributes.
- Support for both long and short switch labels.
- Define custom argument handler methods for each switch.
- Automatic generation of help information.
- Option to control behavior after displaying help information or encountering errors.
- Option to print with color using Spectre.Console.

### Basic Example
```csharp

internal class Program 
{
	public static void Main(string[] args) 
	{
		// The method will return args with all of the switches and values stripped so that
		// you can use whatever data remains for the rest of the application.
		args = ArgumentProcessor.ProcessArguments<Args>(args);
	}
}

public class Args
{
	[CommandArg("--get-strings/-s", "Get strings from extracted network packets", nameof(GetStringsFromPackets_Handler))]
	public static bool GetStringsFromPackets = false; // Set it to its default value

	// The return type should remain null as **CommandAssistant** will not do anything with the value(s).
	public static void GetStringsFromPackets_Handler() // Note: The handler name can be whatever you want
	{
		// Do some checks if needed

		GetStringsFromPackets = true;
	}
}
```
The class containing the switches doesn't have to be non-static. There are two variations of the parsing method
to account for that.

As of right now, the handler methods are required to be inside the same class as the switches.

See **CommandAssistantTest** to learn more about how **CommandAssistant** is used.

## Example Using Static Class
```csharp

internal class Program 
{
	public static void Main(string[] args) 
	{
		// Passing the Type of the static class
		args = ArgumentProcessor.ProcessArguments(args, typeof(Args));
	}
}

public static class Args
{
	[CommandArg("--get-strings/-s", "Get strings from extracted network packets", nameof(GetStringsFromPackets_Handler))]
	public static bool GetStringsFromPackets = false; // Set it to its default value

	public static void GetStringsFromPackets_Handler()
	{
		// Do some checks if needed

		GetStringsFromPackets = true;
	}
}
```

# Passing Values

In order to pass a value, you have to specify how many arguments will be after the switch.
Take this example:

```csharp
public static class Args
{
	[CommandArg("--format/-f", "Output format (%D, %H, %C)", nameof(Format_Handler), 
	/* Number of args after the switch */ 1, 
	/* What type should be passed to the handler */ typeof(string))]
	public static string Format = false; // Set it to its default value

	public static void Format_Handler(string value) // Matching argument as specified
	{
		// Do some checks if needed

		Console.WriteLine("Value passed: " + value);
		Format = true;
	}
}
```

> :warning: **The CommandArg attribute must match handler**: An exception will be thrown if the argument count or type do not match the handler

The ValuesAfter parameter is set to **-1** by default (Disabled), and the type will remain **null** when unused.

## Multiple Values
```csharp
public static class Args
{
	[CommandArg("--names/-n", "The names for each table to be presented", nameof(Names_Handler), 
	/* Number of args after the switch */ 3, 
	/* What type should be passed to the handler (string array for multiple values) */ typeof(string[]))]
	public static string[] Names = false; // Set it to its default value

	public static void Names_Handler(string value1, string value2, string value3) // Matching argument as specified
	{
		// Do some checks if needed

		Console.WriteLine($"Values passed: {value1}, {value2}, {value3}");
		Names = new string[] { value1, value2, value3 };
	}
}
```
> :warning: **Using string[] does not mean a string array will be passed**: 
Although we're passing a string[], **CommandAssistant** will not pass an array due to 
data conversion issues. Because of this, we cannot use a dynamic argument count (**-2**) 
for taking all argument until the next switch.