namespace Spherewright.Contracts.Errors;

public sealed class BridgeError
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool Retryable { get; set; }

    public string Recovery { get; set; } = string.Empty;

    public Spherewright.Contracts.Players.NativeResumeFailureSnapshot? NativeFirstFailure { get; set; }

    public static BridgeError Create(string code, string message, bool retryable, string recovery)
    {
        return new BridgeError
        {
            Code = code,
            Message = message,
            Retryable = retryable,
            Recovery = recovery,
        };
    }
}

