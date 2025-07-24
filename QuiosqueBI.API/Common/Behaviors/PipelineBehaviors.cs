using MediatR;
using System.Diagnostics;

namespace QuiosqueBI.API.Common.Behaviors;

/// <summary>
/// Pipeline behavior para logging de performance das requisições
/// </summary>
public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestName = typeof(TRequest).Name;

        try
        {
            var response = await next();
            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds > 500) // Log apenas se demorar mais que 500ms
            {
                _logger.LogWarning("Requisição lenta detectada: {RequestName} demorou {ElapsedMilliseconds}ms", 
                    requestName, stopwatch.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Erro ao processar {RequestName} após {ElapsedMilliseconds}ms", 
                requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

/// <summary>
/// Pipeline behavior para validação básica de requests
/// </summary>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Aqui você pode adicionar validações customizadas se necessário
        // Por exemplo, usando FluentValidation
        
        return await next();
    }
}
