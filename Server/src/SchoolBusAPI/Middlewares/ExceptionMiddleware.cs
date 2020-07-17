﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading.Tasks;

namespace SchoolBusAPI.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _logger = logger;
            _next = next;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message != "StatusCode cannot be set because the response has already started.")
                {
                    var guid = Guid.NewGuid();
                    _logger.LogError($"HMCR Exception{guid}: {ex}");
                    await HandleExceptionAsync(httpContext, guid);
                }
            }
            catch (Exception ex)
            {
                var guid = Guid.NewGuid();
                _logger.LogError($"HMCR Exception{guid}: {ex}");
                await HandleExceptionAsync(httpContext, guid);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Guid guid)
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var problem = new ValidationProblemDetails()
            {
                Type = "https://sb.bc.gov.ca/exception",
                Title = "An unexpected error occurred!",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "The instance value should be used to identify the problem when calling customer support",
                Instance = $"urn:hmcr:error:{Guid.NewGuid()}"
            };

            problem.Extensions.Add("traceId", context.TraceIdentifier);

            await context.Response.WriteJsonAsync(problem, "application/problem+json");
        }
    }

}