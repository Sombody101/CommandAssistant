namespace CommandAssistant;

public class InvalidMethodParametersException : Exception
{
    public InvalidMethodParametersException(string message)
        : base(message)
    {
    }
}

public class InvalidAttributeParameterException : Exception
{
    public InvalidAttributeParameterException(string message)
        : base(message)
    {
    }
}

public class MissingSwitchHandlerException : Exception
{
    public MissingSwitchHandlerException(string message)
        : base(message)
    {
    }
}

public class UnsuportedDataTypeException : Exception
{
    public UnsuportedDataTypeException(string message)
        : base(message)
    {
    }
}