namespace RZPrime.Utilities.Attributes
{
    public class CustomRateLimitAttribute(string message = "Too many requests. Try again later",
        int maxAttemptsCount = 5, int lockoutDurationMinutes = 5) : Attribute
    {
        public string Message { get; } = message;
        public int MaxAttemptsCount { get; } = maxAttemptsCount;
        public int LockoutDurationMinutes { get; } = lockoutDurationMinutes;
    }
}
