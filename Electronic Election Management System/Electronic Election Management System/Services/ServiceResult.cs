namespace Electronic_Election_Management_System.Services
{
    /// <summary>
    /// Wraps a service operation outcome so that controllers never need to catch exceptions
    /// for expected business failures (validation, not-found, conflicts).
    /// </summary>
    public record ServiceResult<T>
    {
        public bool Success { get; init; }
        public T? Data { get; init; }
        public string? Error { get; init; }

        public bool IsNotFound { get; init; }

        public static ServiceResult<T> Ok(T data) => new() { Success = true, Data = data };

        public static ServiceResult<T> Fail(string error) => new() { Success = false, Error = error };

        public static ServiceResult<T> NotFound(string error = "Resource not found.") =>
            new() { Success = false, Error = error, IsNotFound = true };
    }
}
