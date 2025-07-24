using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace QuiosqueBI.API.Common;

/// <summary>
/// Interface base para comandos que requerem autenticação
/// </summary>
public interface IAuthenticatedRequest : IRequest<IActionResult>
{
    ClaimsPrincipal User { get; }
}

/// <summary>
/// Interface base para queries que requerem autenticação
/// </summary>
public interface IAuthenticatedQuery<TResponse> : IRequest<TResponse>
{
    ClaimsPrincipal User { get; }
}

/// <summary>
/// Resultado padrão para operações que podem falhar
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public T? Value { get; private set; }
    public string? Error { get; private set; }

    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}
