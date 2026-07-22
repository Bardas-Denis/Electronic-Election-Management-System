using Electronic_Election_Management_System.Constants;

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
        public ErrorCode? ErrorCode { get; init; }

        public bool IsNotFound { get; init; }

        public static ServiceResult<T> Ok(T data) => new() { Success = true, Data = data };

        public static ServiceResult<T> Fail(ErrorCode errorCode) => new() { Success = false, ErrorCode = errorCode };

        public static ServiceResult<T> NotFound(ErrorCode errorCode = Constants.ErrorCode.ResourceNotFound) =>
            new() { Success = false, ErrorCode = errorCode, IsNotFound = true };
    }
}
