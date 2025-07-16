
using System.Threading.Tasks;

namespace TableManagement.Application.Services
{


    public interface ILoggingService
    {
        Task LogRequestAsync(
            string ipAddress,
            string requestPath,
            string httpMethod,
            string? requestBody = null,
            string? queryString = null
        );

        Task LogResponseAsync(
            string requestPath,
            string httpMethod,
            string? responseBody = null,
            string? statusCode = null
        );

        Task LogErrorAsync(
            string message,
            Exception? exception = null,
            string? requestPath = null,
            string? httpMethod = null,
            string? ipAddress = null
        );
    }
}
