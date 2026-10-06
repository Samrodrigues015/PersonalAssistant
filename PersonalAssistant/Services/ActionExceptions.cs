namespace PersonalAssistant.Services;

/// <summary>
/// Lançada quando é pedida uma ação com um nome que não está registado no ActionService.
/// </summary>
public class ActionNotFoundException : Exception
{
    public ActionNotFoundException(string message) : base(message)
    {
    }
}

/// <summary>
/// Lançada quando uma ação registada existe, mas a sua execução falha.
/// </summary>
public class ActionExecutionException : Exception
{
    public ActionExecutionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
